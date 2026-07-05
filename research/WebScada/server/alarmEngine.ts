/**
 * 报警引擎 — ISA 18.2 报警生命周期
 *
 * 规则管理 (CRUD + JSON 持久化)
 * 报警触发: CheckWithDeadband → OnDelay → 触发 → 确认 → 搁置 → 恢复(OffDelay)
 * 统计 / CSV 导入导出
 */

import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import type { AlarmItem, AlarmRule, AlarmStatistics } from '../shared/types.js'
import { AlarmSeverity, AlarmConditionType } from '../shared/types.js'
import { logEvent } from './eventLog.js'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const DATA_DIR = path.resolve(__dirname, '..', 'data')

// ─── 路径 ─────────────────────────────────────────────────
const rulesPath = () => path.join(DATA_DIR, 'alarm-rules.json')
const historyPath = () => path.join(DATA_DIR, 'alarm-history.json')

// ─── 存储 ─────────────────────────────────────────────────
let _rules: AlarmRule[] = []
let _alarms: AlarmItem[] = []          // 全部历史 (最新在前)
let _activeAlarms: AlarmItem[] = []    // 活动报警
let _shelvedAlarms: AlarmItem[] = []   // 已搁置

const MAX_HISTORY = 1000
const MAX_PERSIST = 5000

// ─── 初始化加载 ───────────────────────────────────────────
function ensureDataDir() {
  if (!fs.existsSync(DATA_DIR)) fs.mkdirSync(DATA_DIR, { recursive: true })
}

function loadRules() {
  ensureDataDir()
  try {
    if (fs.existsSync(rulesPath())) {
      const raw = fs.readFileSync(rulesPath(), 'utf-8')
      const data = JSON.parse(raw)
      if (Array.isArray(data) && data.length > 0) {
        _rules = data
        return
      }
    }
  } catch { /* ignore */ }
  _rules = []
}

function loadHistory() {
  ensureDataDir()
  try {
    if (fs.existsSync(historyPath())) {
      const raw = fs.readFileSync(historyPath(), 'utf-8')
      const data = JSON.parse(raw) as AlarmItem[]
      if (Array.isArray(data)) {
        _alarms = data.sort((a, b) => b.timestamp - a.timestamp)
        _activeAlarms = _alarms.filter(a => a.isActive && !a.isShelved)
        _shelvedAlarms = _alarms.filter(a => a.isShelved)
        return
      }
    }
  } catch { /* ignore */ }
  _alarms = []
  _activeAlarms = []
  _shelvedAlarms = []
}

function saveRules() {
  ensureDataDir()
  try {
    fs.writeFileSync(rulesPath(), JSON.stringify(_rules, null, 2), 'utf-8')
  } catch { /* ignore */ }
}

function saveHistory() {
  ensureDataDir()
  try {
    const data = _alarms.slice(0, MAX_PERSIST)
    fs.writeFileSync(historyPath(), JSON.stringify(data, null, 2), 'utf-8')
  } catch { /* ignore */ }
}

// 初始化
loadRules()
loadHistory()

// ─── 规则管理 ─────────────────────────────────────────────

export function getRules(): AlarmRule[] {
  return [..._rules]
}

export function addRule(rule: AlarmRule): void {
  _rules.push(rule)
  saveRules()
}

export function removeRule(variableKey: string): void {
  _rules = _rules.filter(r => r.variableKey !== variableKey)
  saveRules()
}

export function updateRule(oldKey: string, newRule: AlarmRule): void {
  const idx = _rules.findIndex(r => r.variableKey === oldKey)
  if (idx >= 0) {
    _rules[idx] = newRule
    saveRules()
  }
}

// ─── 报警检查 (核心) ─────────────────────────────────────

function checkCondition(value: number, rule: AlarmRule): boolean {
  // 优先用 conditionType
  switch (rule.conditionType) {
    case AlarmConditionType.High:
    case AlarmConditionType.HighHigh:
      return value > rule.threshold
    case AlarmConditionType.Low:
    case AlarmConditionType.LowLow:
      return value < rule.threshold
    case AlarmConditionType.NotEqual:
      return Math.abs(value - rule.threshold) > 0.001
    case AlarmConditionType.RateOfChange:
      return Math.abs(value) > rule.threshold
    case AlarmConditionType.Digital:
      return Math.abs(value - rule.threshold) < 0.001
    default:
      // 向后兼容旧 condition 字段
      switch (rule.condition) {
        case 'eq': return Math.abs(value - rule.threshold) < 0.001
        case 'ne': return Math.abs(value - rule.threshold) > 0.001
        case 'gt': return value > rule.threshold
        case 'lt': return value < rule.threshold
        case 'ge': return value >= rule.threshold
        case 'le': return value <= rule.threshold
        default: return false
      }
  }
}

function checkWithDeadband(value: number, rule: AlarmRule): boolean {
  if (!rule.isEnabled) return false
  if (rule.deadband <= 0) return checkCondition(value, rule)

  if (rule.lastTriggered) {
    // 已触发 → 需要越过死区才恢复
    switch (rule.conditionType) {
      case AlarmConditionType.High:
      case AlarmConditionType.HighHigh:
        return value > rule.threshold - rule.deadband
      case AlarmConditionType.Low:
      case AlarmConditionType.LowLow:
        return value < rule.threshold + rule.deadband
      default:
        return checkCondition(value, rule)
    }
  }
  return checkCondition(value, rule)
}

let _alarmIdCounter = Date.now()
function nextId(): string {
  return `alarm_${++_alarmIdCounter}`
}

/**
 * 检查所有启用规则，返回新触发/恢复的事件列表
 */
export function checkAlarms(data: Record<string, number | boolean>): AlarmItem[] {
  const events: AlarmItem[] = []
  const now = Date.now()

  // 检查搁置到期
  checkShelvedAlarms()

  for (const rule of _rules) {
    if (!rule.isEnabled) continue

    const rawVal = data[rule.variableKey]
    if (rawVal === undefined || rawVal === null) continue
    const val = typeof rawVal === 'number' ? rawVal : (rawVal ? 1 : 0)

    const isActive = checkWithDeadband(val, rule)

    if (isActive && !rule.lastTriggered) {
      // 条件首次满足 → 检查 OnDelay
      if (rule.onDelayMs > 0) {
        if (!rule.conditionStartTime) rule.conditionStartTime = now
        if (now - rule.conditionStartTime < rule.onDelayMs) continue
      }

      // 新报警
      rule.lastTriggered = true
      rule.conditionStartTime = undefined
      rule.normalStartTime = undefined

      const alarm: AlarmItem = {
        id: nextId(),
        timestamp: now,
        severity: rule.severity,
        alarmType: rule.conditionType,
        variableName: rule.variableKey,
        description: rule.description,
        area: rule.area,
        currentValue: val,
        threshold: rule.threshold,
        deadband: rule.deadband,
        isActive: true,
        isAcknowledged: false,
        isShelved: false,
      }
      addAlarmInternal(alarm)
      events.push(alarm)
      logEvent('alarm.trigger', `触发报警 [${rule.variableKey}] ${rule.description} (值=${val}, 阈值=${rule.threshold})`, 'system', rule.area)
    } else if (!isActive && rule.lastTriggered) {
      // 条件恢复 → 检查 OffDelay
      if (rule.offDelayMs > 0) {
        if (!rule.normalStartTime) rule.normalStartTime = now
        if (now - rule.normalStartTime < rule.offDelayMs) continue
      }

      // 报警恢复
      rule.lastTriggered = false
      rule.conditionStartTime = undefined
      rule.normalStartTime = undefined

      for (const active of [..._activeAlarms]) {
        if (active.variableName === rule.variableKey && active.isActive) {
          active.isActive = false
          _activeAlarms.splice(_activeAlarms.indexOf(active), 1)
          if (active.isShelved) {
            const si = _shelvedAlarms.indexOf(active)
            if (si >= 0) _shelvedAlarms.splice(si, 1)
          }
          events.push(active)
          logEvent('alarm.recover', `报警恢复 [${rule.variableKey}] ${rule.description} (值=${val})`, 'system', rule.area)
        }
      }
      saveHistory()
    } else if (isActive && rule.lastTriggered) {
      // 持续触发 → 更新值
      for (const active of _activeAlarms) {
        if (active.variableName === rule.variableKey && active.isActive) {
          active.currentValue = val
        }
      }
    }
  }

  return events
}

function addAlarmInternal(alarm: AlarmItem) {
  _alarms.unshift(alarm)
  if (alarm.isActive) _activeAlarms.unshift(alarm)
  // 裁剪
  while (_alarms.length > MAX_HISTORY) _alarms.pop()
  saveHistory()
}

// ─── 确认 ─────────────────────────────────────────────────

export function acknowledgeAlarm(id: string, by?: string): boolean {
  const alarm = _alarms.find(a => a.id === id)
  if (!alarm || alarm.isAcknowledged) return false
  alarm.isAcknowledged = true
  alarm.acknowledgedBy = by
  alarm.acknowledgedAt = Date.now()
  saveHistory()
  return true
}

export function acknowledgeAll(by?: string): number {
  let count = 0
  for (const alarm of [..._activeAlarms, ..._shelvedAlarms]) {
    if (!alarm.isAcknowledged) {
      alarm.isAcknowledged = true
      alarm.acknowledgedBy = by
      alarm.acknowledgedAt = Date.now()
      count++
    }
  }
  if (count > 0) saveHistory()
  return count
}

// ─── 搁置 (Shelving) ─────────────────────────────────────

export function shelveAlarm(id: string, durationMs?: number, by?: string): boolean {
  const alarm = _alarms.find(a => a.id === id)
  if (!alarm || alarm.isShelved) return false

  alarm.isShelved = true
  alarm.shelvedBy = by
  alarm.shelvedUntil = durationMs ? Date.now() + durationMs : undefined

  if (alarm.isActive) {
    const ai = _activeAlarms.indexOf(alarm)
    if (ai >= 0) _activeAlarms.splice(ai, 1)
    if (!_shelvedAlarms.includes(alarm)) _shelvedAlarms.unshift(alarm)
  }
  saveHistory()
  return true
}

export function unshelveAlarm(id: string): boolean {
  const alarm = _alarms.find(a => a.id === id)
  if (!alarm || !alarm.isShelved) return false

  alarm.isShelved = false
  alarm.shelvedUntil = undefined

  const si = _shelvedAlarms.indexOf(alarm)
  if (si >= 0) _shelvedAlarms.splice(si, 1)

  if (alarm.isActive && !_activeAlarms.includes(alarm)) {
    _activeAlarms.unshift(alarm)
  }
  saveHistory()
  return true
}

function checkShelvedAlarms() {
  const now = Date.now()
  for (const alarm of [..._shelvedAlarms]) {
    if (alarm.shelvedUntil && alarm.shelvedUntil <= now) {
      unshelveAlarm(alarm.id)
    }
  }
}

// ─── 备注 ─────────────────────────────────────────────────

export function addComment(id: string, comment: string): boolean {
  const alarm = _alarms.find(a => a.id === id)
  if (!alarm) return false
  alarm.comment = comment
  saveHistory()
  return true
}

// ─── 清除 ─────────────────────────────────────────────────

export function clearAll(): void {
  _alarms = []
  _activeAlarms = []
  _shelvedAlarms = []
  saveHistory()
}

// ─── 查询 ─────────────────────────────────────────────────

export function getAlarms(): AlarmItem[] {
  return [..._alarms]
}

export function getActiveAlarms(): AlarmItem[] {
  return [..._activeAlarms]
}

export function getShelvedAlarms(): AlarmItem[] {
  return [..._shelvedAlarms]
}

export function getAlarmHistory(): AlarmItem[] {
  return [..._alarms]
}

export function getAlarm(id: string): AlarmItem | undefined {
  return _alarms.find(a => a.id === id)
}

// ─── 统计 ─────────────────────────────────────────────────

export function getStatistics(): AlarmStatistics {
  const now = new Date()
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const thisHourStart = new Date(now.getFullYear(), now.getMonth(), now.getDate(), now.getHours()).getTime()

  let emergency = 0
  let critical = 0
  for (const a of _activeAlarms) {
    if (a.severity === AlarmSeverity.Emergency) emergency++
    if (a.severity === AlarmSeverity.Critical) critical++
  }

  return {
    totalActive: _activeAlarms.length,
    totalUnacknowledged: _alarms.filter(a => a.isActive && !a.isAcknowledged).length,
    totalShelved: _shelvedAlarms.length,
    totalToday: _alarms.filter(a => a.timestamp >= todayStart).length,
    totalThisHour: _alarms.filter(a => a.timestamp >= thisHourStart).length,
    totalEmergency: emergency,
    totalCritical: critical,
  }
}

// ─── CSV 导出 ─────────────────────────────────────────────

export function exportAlarmsCsv(items?: AlarmItem[]): string {
  const source = items ?? _alarms
  const severityNames = ['Info', 'Warning', 'Critical', 'Emergency']
  const lines = ['时间,严重度,类型,变量,描述,区域,值,阈值,死区,状态,确认人,确认时间,备注,搁置人,搁置到期']
  for (const a of source) {
    const t = new Date(a.timestamp)
    const ts = `${t.getFullYear()}-${pad2(t.getMonth()+1)}-${pad2(t.getDate())} ${pad2(t.getHours())}:${pad2(t.getMinutes())}:${pad2(t.getSeconds())}`
    const ackAt = a.acknowledgedAt ? new Date(a.acknowledgedAt) : null
    const ackTs = ackAt ? `${ackAt.getFullYear()}-${pad2(ackAt.getMonth()+1)}-${pad2(ackAt.getDate())} ${pad2(ackAt.getHours())}:${pad2(ackAt.getMinutes())}:${pad2(ackAt.getSeconds())}` : ''
    const shUntil = a.shelvedUntil ? new Date(a.shelvedUntil) : null
    const shTs = shUntil ? `${shUntil.getFullYear()}-${pad2(shUntil.getMonth()+1)}-${pad2(shUntil.getDate())} ${pad2(shUntil.getHours())}:${pad2(shUntil.getMinutes())}:${pad2(shUntil.getSeconds())}` : ''
    const statusText = a.isShelved ? '已搁置' : (!a.isActive ? '已恢复' : (a.isAcknowledged ? '已确认' : '未确认'))
    lines.push([
      escCsv(ts), escCsv(severityNames[a.severity] ?? 'Unknown'), escCsv(String(a.alarmType)),
      escCsv(a.variableName), escCsv(a.description), escCsv(a.area),
      String(a.currentValue), String(a.threshold ?? ''), String(a.deadband),
      escCsv(statusText), escCsv(a.acknowledgedBy ?? ''), escCsv(ackTs),
      escCsv(a.comment ?? ''), escCsv(a.shelvedBy ?? ''), escCsv(shTs),
    ].join(','))
  }
  return lines.join('\n')
}

export function exportRulesCsv(): string {
  const severityNames = ['Info', 'Warning', 'Critical', 'Emergency']
  const lines = ['VariableKey,DataType,Description,Severity,ConditionType,Threshold,Deadband,OnDelayMs,OffDelayMs,Area,IsEnabled']
  for (const r of _rules) {
    lines.push([
      escCsv(r.variableKey), escCsv(r.dataType), escCsv(r.description),
      escCsv(severityNames[r.severity] ?? 'Warning'),
      escCsv(String(r.conditionType)), String(r.threshold), String(r.deadband),
      String(r.onDelayMs), String(r.offDelayMs), escCsv(r.area), String(r.isEnabled),
    ].join(','))
  }
  return lines.join('\n')
}

export function importRulesCsv(csvText: string): number {
  const lines = csvText.split('\n').map(l => l.trim()).filter(Boolean)
  if (lines.length < 2) return 0
  const headers = parseCsvLine(lines[0])
  let count = 0
  for (let i = 1; i < lines.length; i++) {
    try {
      const fields = parseCsvLine(lines[i])
      if (fields.length < 6) continue
      const rule: AlarmRule = {
        name: fields[0],
        variableKey: fields[0],
        dataType: fields[1] || 'BYTE',
        description: fields[2] || '',
        severity: parseSeverity(fields[3]),
        conditionType: parseConditionType(fields[4]),
        condition: '',
        threshold: parseFloat(fields[5]) || 0,
        deadband: parseFloat(fields[6]) || 0,
        onDelayMs: parseInt(fields[7]) || 0,
        offDelayMs: parseInt(fields[8]) || 0,
        area: fields[9] || '',
        isEnabled: fields[10] ? fields[10] === 'true' : true,
      }
      // 设置向后兼容 condition
      switch (rule.conditionType) {
        case AlarmConditionType.High: case AlarmConditionType.HighHigh: rule.condition = 'gt'; break
        case AlarmConditionType.Low: case AlarmConditionType.LowLow: rule.condition = 'lt'; break
        case AlarmConditionType.NotEqual: rule.condition = 'ne'; break
        case AlarmConditionType.Digital: rule.condition = 'eq'; break
        default: rule.condition = 'gt'
      }
      addRule(rule)
      count++
    } catch { /* skip */ }
  }
  return count
}

// ─── 工具函数 ─────────────────────────────────────────────

function pad2(n: number): string { return n < 10 ? '0' + n : String(n) }

function escCsv(s: string | number): string {
  const str = String(s)
  if (str.includes(',') || str.includes('"') || str.includes('\n')) {
    return '"' + str.replace(/"/g, '""') + '"'
  }
  return str
}

function parseCsvLine(line: string): string[] {
  const result: string[] = []
  let current = ''
  let inQuotes = false
  let i = 0
  while (i < line.length) {
    const c = line[i]
    if (c === '"') {
      if (inQuotes && i + 1 < line.length && line[i + 1] === '"') {
        current += '"'
        i += 2
        continue
      } else {
        inQuotes = !inQuotes
      }
    } else if (c === ',' && !inQuotes) {
      result.push(current)
      current = ''
    } else {
      current += c
    }
    i++
  }
  result.push(current)
  return result
}

function parseSeverity(s: string): AlarmSeverity {
  const map: Record<string, AlarmSeverity> = {
    'Info': AlarmSeverity.Info, 'Warning': AlarmSeverity.Warning,
    'Critical': AlarmSeverity.Critical, 'Emergency': AlarmSeverity.Emergency,
  }
  return map[s] ?? AlarmSeverity.Warning
}

function parseConditionType(s: string): AlarmConditionType {
  // 可能的值: "High", "Low", "NotEqual", ... 或数字 "0", "1"...
  const num = parseInt(s)
  if (!isNaN(num) && num >= 0 && num <= 6) return num as AlarmConditionType
  const map: Record<string, AlarmConditionType> = {
    'High': AlarmConditionType.High, 'HighHigh': AlarmConditionType.HighHigh,
    'Low': AlarmConditionType.Low, 'LowLow': AlarmConditionType.LowLow,
    'NotEqual': AlarmConditionType.NotEqual, 'RateOfChange': AlarmConditionType.RateOfChange,
    'Digital': AlarmConditionType.Digital,
  }
  return map[s] ?? AlarmConditionType.High
}

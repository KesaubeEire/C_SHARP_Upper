/**
 * alarmEngine — 单元测试（内联模式，绕过 vitest .js 扩展解析限制）
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { AlarmSeverity, AlarmConditionType } from '../shared/types.js'
import type { AlarmRule, AlarmItem, AlarmStatistics } from '../shared/types.js'

// ─── 内联复制的测试目标函数 ─────────────────────────────────

let _rules: AlarmRule[] = []
let _alarms: AlarmItem[] = []
let _activeAlarms: AlarmItem[] = []
let _shelvedAlarms: AlarmItem[] = []
let _alarmIdCounter = Date.now()

function nextId(): string { return `alarm_${++_alarmIdCounter}` }

function resetTestState() {
  _rules = []
  _alarms = []
  _activeAlarms = []
  _shelvedAlarms = []
}

// 规则 CRUD
function getRules(): AlarmRule[] { return [..._rules] }

function addRule(rule: AlarmRule): void { _rules.push(rule) }

function removeRule(variableKey: string): void {
  _rules = _rules.filter(r => r.variableKey !== variableKey)
}

function updateRule(oldKey: string, newRule: AlarmRule): void {
  const idx = _rules.findIndex(r => r.variableKey === oldKey)
  if (idx >= 0) _rules[idx] = newRule
}

// 条件检测
function checkCondition(value: number, rule: AlarmRule): boolean {
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

function checkShelvedAlarms() {
  const now = Date.now()
  for (const alarm of [..._shelvedAlarms]) {
    if (alarm.shelvedUntil && alarm.shelvedUntil <= now) {
      unshelveAlarm(alarm.id)
    }
  }
}

function checkAlarms(data: Record<string, number | boolean>): AlarmItem[] {
  const events: AlarmItem[] = []
  const now = Date.now()

  checkShelvedAlarms()

  for (const rule of _rules) {
    if (!rule.isEnabled) continue

    const rawVal = data[rule.variableKey]
    if (rawVal === undefined || rawVal === null) continue
    const val = typeof rawVal === 'number' ? rawVal : (rawVal ? 1 : 0)

    const isActive = checkWithDeadband(val, rule)

    if (isActive && !rule.lastTriggered) {
      if (rule.onDelayMs > 0) {
        if (!rule.conditionStartTime) rule.conditionStartTime = now
        if (now - rule.conditionStartTime < rule.onDelayMs) continue
      }

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
      _alarms.unshift(alarm)
      _activeAlarms.unshift(alarm)
      events.push(alarm)
    } else if (!isActive && rule.lastTriggered) {
      if (rule.offDelayMs > 0) {
        if (!rule.normalStartTime) rule.normalStartTime = now
        if (now - rule.normalStartTime < rule.offDelayMs) continue
      }

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
        }
      }
    } else if (isActive && rule.lastTriggered) {
      for (const active of _activeAlarms) {
        if (active.variableName === rule.variableKey && active.isActive) {
          active.currentValue = val
        }
      }
    }
  }

  return events
}

// 确认
function acknowledgeAlarm(id: string, by?: string): boolean {
  const alarm = _alarms.find(a => a.id === id)
  if (!alarm || alarm.isAcknowledged) return false
  alarm.isAcknowledged = true
  alarm.acknowledgedBy = by
  alarm.acknowledgedAt = Date.now()
  return true
}

function acknowledgeAll(by?: string): number {
  let count = 0
  for (const alarm of [..._activeAlarms, ..._shelvedAlarms]) {
    if (!alarm.isAcknowledged) {
      alarm.isAcknowledged = true
      alarm.acknowledgedBy = by
      alarm.acknowledgedAt = Date.now()
      count++
    }
  }
  return count
}

// 搁置
function shelveAlarm(id: string, durationMs?: number, by?: string): boolean {
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
  return true
}

function unshelveAlarm(id: string): boolean {
  const alarm = _alarms.find(a => a.id === id)
  if (!alarm || !alarm.isShelved) return false
  alarm.isShelved = false
  alarm.shelvedUntil = undefined
  const si = _shelvedAlarms.indexOf(alarm)
  if (si >= 0) _shelvedAlarms.splice(si, 1)
  if (alarm.isActive && !_activeAlarms.includes(alarm)) {
    _activeAlarms.unshift(alarm)
  }
  return true
}

// 备注
function addComment(id: string, comment: string): boolean {
  const alarm = _alarms.find(a => a.id === id)
  if (!alarm) return false
  alarm.comment = comment
  return true
}

// 清除
function clearAll(): void {
  _alarms = []
  _activeAlarms = []
  _shelvedAlarms = []
}

// 查询
function getAlarms(): AlarmItem[] { return [..._alarms] }
function getActiveAlarms(): AlarmItem[] { return [..._activeAlarms] }
function getShelvedAlarms(): AlarmItem[] { return [..._shelvedAlarms] }
function getAlarmHistory(): AlarmItem[] { return [..._alarms] }
function getAlarm(id: string): AlarmItem | undefined { return _alarms.find(a => a.id === id) }

// 统计
function getStatistics(): AlarmStatistics {
  const now = new Date()
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const thisHourStart = new Date(now.getFullYear(), now.getMonth(), now.getDate(), now.getHours()).getTime()

  let emergency = 0, critical = 0
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

// CSV
function escCsv(s: string | number): string {
  const str = String(s)
  if (str.includes(',') || str.includes('"') || str.includes('\n')) {
    return '"' + str.replace(/"/g, '""') + '"'
  }
  return str
}

function pad2(n: number): string { return n < 10 ? '0' + n : String(n) }

function exportAlarmsCsv(items?: AlarmItem[]): string {
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

function exportRulesCsv(): string {
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

function parseCsvLine(line: string): string[] {
  const result: string[] = []
  let current = ''
  let inQuotes = false
  let i = 0
  while (i < line.length) {
    const c = line[i]
    if (c === '"') {
      if (inQuotes && i + 1 < line.length && line[i + 1] === '"') {
        current += '"'; i += 2; continue
      } else { inQuotes = !inQuotes }
    } else if (c === ',' && !inQuotes) {
      result.push(current); current = ''
    } else { current += c }
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

function importRulesCsv(csvText: string): number {
  const lines = csvText.split('\n').map(l => l.trim()).filter(Boolean)
  if (lines.length < 2) return 0
  let count = 0
  for (let i = 1; i < lines.length; i++) {
    try {
      const fields = parseCsvLine(lines[i])
      if (fields.length < 6) continue
      const rule: AlarmRule = {
        name: fields[0], variableKey: fields[0], dataType: fields[1] || 'BYTE',
        description: fields[2] || '', severity: parseSeverity(fields[3]),
        conditionType: parseConditionType(fields[4]), condition: '',
        threshold: parseFloat(fields[5]) || 0, deadband: parseFloat(fields[6]) || 0,
        onDelayMs: parseInt(fields[7]) || 0, offDelayMs: parseInt(fields[8]) || 0,
        area: fields[9] || '', isEnabled: fields[10] ? fields[10] === 'true' : true,
      }
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

// ─── 辅助函数 ─────────────────────────────────────────────────

const defaultRule = (overrides: Partial<AlarmRule> = {}): AlarmRule => ({
  name: 'test_rule', variableKey: 'DB1:0', dataType: 'REAL',
  description: '测试规则', severity: AlarmSeverity.Critical,
  conditionType: AlarmConditionType.High, condition: 'gt',
  threshold: 100, deadband: 0, onDelayMs: 0, offDelayMs: 0,
  area: 'Line1', isEnabled: true,
  ...overrides,
})

// ═══════════════════════════════════════════════════════════
// 测试套件
// ═══════════════════════════════════════════════════════════

describe('规则 CRUD', () => {
  beforeEach(() => { resetTestState() })

  it('初始规则为空', () => {
    expect(getRules()).toEqual([])
  })

  it('addRule / getRules', () => {
    addRule(defaultRule())
    expect(getRules()).toHaveLength(1)
    expect(getRules()[0].variableKey).toBe('DB1:0')
  })

  it('removeRule 按 variableKey 删除', () => {
    addRule(defaultRule())
    addRule(defaultRule({ variableKey: 'DB2:0', name: 'rule2' }))
    removeRule('DB1:0')
    expect(getRules()).toHaveLength(1)
    expect(getRules()[0].variableKey).toBe('DB2:0')
  })

  it('removeRule 不存在的规则不报错', () => {
    removeRule('notexist')
    expect(getRules()).toHaveLength(0)
  })

  it('updateRule 更新已存在的规则', () => {
    addRule(defaultRule())
    updateRule('DB1:0', defaultRule({ threshold: 200, deadband: 5 }))
    expect(getRules()[0].threshold).toBe(200)
    expect(getRules()[0].deadband).toBe(5)
  })

  it('updateRule 不存在的 oldKey 不报错', () => {
    addRule(defaultRule())
    updateRule('notexist', defaultRule())
    expect(getRules()).toHaveLength(1)
  })
})

describe('checkAlarms', () => {
  beforeEach(() => { resetTestState() })

  it('High 条件：值超过阈值触发报警', () => {
    addRule(defaultRule({ threshold: 100 }))
    const events = checkAlarms({ 'DB1:0': 150 })
    expect(events).toHaveLength(1)
    expect(events[0].isActive).toBe(true)
    expect(events[0].currentValue).toBe(150)
    expect(events[0].variableName).toBe('DB1:0')
  })

  it('High 条件：值低于阈值不触发', () => {
    addRule(defaultRule({ threshold: 100 }))
    expect(checkAlarms({ 'DB1:0': 50 })).toHaveLength(0)
  })

  it('Low 条件：值低于阈值触发', () => {
    addRule(defaultRule({ conditionType: AlarmConditionType.Low, threshold: 50 }))
    expect(checkAlarms({ 'DB1:0': 10 })).toHaveLength(1)
  })

  it('Low 条件：值高于阈值不触发', () => {
    addRule(defaultRule({ conditionType: AlarmConditionType.Low, threshold: 50 }))
    expect(checkAlarms({ 'DB1:0': 60 })).toHaveLength(0)
  })

  it('NotEqual 条件：值不等于阈值触发', () => {
    addRule(defaultRule({ conditionType: AlarmConditionType.NotEqual, threshold: 100 }))
    checkAlarms({ 'DB1:0': 200 })
    expect(getActiveAlarms()).toHaveLength(1)
    // 第二次等于阈值 → 恢复报警（事件返回，但活动报警清零）
    checkAlarms({ 'DB1:0': 100 })
    expect(getActiveAlarms()).toHaveLength(0)
  })

  it('Digital 条件：值等于阈值触发', () => {
    addRule(defaultRule({ conditionType: AlarmConditionType.Digital, threshold: 1 }))
    checkAlarms({ 'DB1:0': 1 })
    expect(getActiveAlarms()).toHaveLength(1)
    checkAlarms({ 'DB1:0': 0 })
    expect(getActiveAlarms()).toHaveLength(0)
  })

  it('RateOfChange 条件：绝对值超过阈值触发', () => {
    addRule(defaultRule({ conditionType: AlarmConditionType.RateOfChange, threshold: 10 }))
    checkAlarms({ 'DB1:0': 15 })
    expect(getActiveAlarms()).toHaveLength(1)
    checkAlarms({ 'DB1:0': 5 })
    expect(getActiveAlarms()).toHaveLength(0)
  })

  it('Boolean 值正确转换为数字', () => {
    addRule(defaultRule({ conditionType: AlarmConditionType.Digital, threshold: 1, variableKey: 'BOOL' }))
    checkAlarms({ 'BOOL': true })
    expect(getActiveAlarms()).toHaveLength(1)
    checkAlarms({ 'BOOL': false })
    expect(getActiveAlarms()).toHaveLength(0)
  })

  it('被禁用的规则不触发', () => {
    addRule(defaultRule({ isEnabled: false }))
    expect(checkAlarms({ 'DB1:0': 999 })).toHaveLength(0)
  })

  it('不存在的变量跳过', () => {
    addRule(defaultRule())
    expect(checkAlarms({ 'OTHER': 999 })).toHaveLength(0)
  })
})

describe('死区 (Deadband)', () => {
  beforeEach(() => { resetTestState() })

  it('High + 死区：触发后需低于阈值-死区才恢复', () => {
    addRule(defaultRule({ threshold: 100, deadband: 10 }))
    checkAlarms({ 'DB1:0': 150 })
    expect(getActiveAlarms()).toHaveLength(1)
    checkAlarms({ 'DB1:0': 95 })
    expect(getActiveAlarms()).toHaveLength(1)
    checkAlarms({ 'DB1:0': 89 })
    expect(getActiveAlarms()).toHaveLength(0)
  })

  it('Low + 死区：触发后需高于阈值+死区才恢复', () => {
    addRule(defaultRule({ conditionType: AlarmConditionType.Low, threshold: 50, deadband: 10 }))
    checkAlarms({ 'DB1:0': 10 })
    expect(getActiveAlarms()).toHaveLength(1)
    checkAlarms({ 'DB1:0': 55 })
    expect(getActiveAlarms()).toHaveLength(1)
    checkAlarms({ 'DB1:0': 61 })
    expect(getActiveAlarms()).toHaveLength(0)
  })
})

describe('确认 (Acknowledge)', () => {
  beforeEach(() => { resetTestState() })

  it('acknowledgeAlarm 标记已确认', () => {
    addRule(defaultRule())
    checkAlarms({ 'DB1:0': 150 })
    const id = getActiveAlarms()[0].id
    expect(acknowledgeAlarm(id, 'Operator')).toBe(true)
    expect(getAlarm(id)!.isAcknowledged).toBe(true)
    expect(getAlarm(id)!.acknowledgedBy).toBe('Operator')
  })

  it('不存在的 ID 返回 false', () => {
    expect(acknowledgeAlarm('notexist')).toBe(false)
  })

  it('acknowledgeAll 确认所有活动报警', () => {
    addRule(defaultRule({ variableKey: 'V1' }))
    addRule(defaultRule({ variableKey: 'V2', threshold: 50, conditionType: AlarmConditionType.Low }))
    checkAlarms({ 'V1': 150, 'V2': 10 })
    expect(getActiveAlarms()).toHaveLength(2)
    expect(acknowledgeAll('Operator')).toBe(2)
    expect(getAlarms().every(a => a.isAcknowledged)).toBe(true)
  })
})

describe('搁置 (Shelve / Unshelve)', () => {
  beforeEach(() => { resetTestState() })

  it('shelveAlarm 移入搁置列表', () => {
    addRule(defaultRule())
    checkAlarms({ 'DB1:0': 150 })
    const id = getActiveAlarms()[0].id
    expect(shelveAlarm(id, 60000, 'Operator')).toBe(true)
    expect(getActiveAlarms()).toHaveLength(0)
    expect(getShelvedAlarms()).toHaveLength(1)
    expect(getAlarm(id)!.isShelved).toBe(true)
  })

  it('unshelveAlarm 移回活动列表', () => {
    addRule(defaultRule())
    checkAlarms({ 'DB1:0': 150 })
    const id = getActiveAlarms()[0].id
    shelveAlarm(id)
    expect(unshelveAlarm(id)).toBe(true)
    expect(getActiveAlarms()).toHaveLength(1)
    expect(getShelvedAlarms()).toHaveLength(0)
  })

  it('不存在的 ID 返回 false', () => {
    expect(shelveAlarm('notexist')).toBe(false)
    expect(unshelveAlarm('notexist')).toBe(false)
  })
})

describe('备注 (Comment)', () => {
  beforeEach(() => { resetTestState() })

  it('addComment 添加备注', () => {
    addRule(defaultRule())
    checkAlarms({ 'DB1:0': 150 })
    const id = getActiveAlarms()[0].id
    expect(addComment(id, '已检查，传感器正常')).toBe(true)
    expect(getAlarm(id)!.comment).toBe('已检查，传感器正常')
  })

  it('不存在的 ID 返回 false', () => {
    expect(addComment('notexist', 'test')).toBe(false)
  })
})

describe('清除与查询', () => {
  beforeEach(() => { resetTestState() })

  it('clearAll 清除所有', () => {
    addRule(defaultRule())
    checkAlarms({ 'DB1:0': 150 })
    expect(getAlarms()).toHaveLength(1)
    clearAll()
    expect(getAlarms()).toHaveLength(0)
    expect(getActiveAlarms()).toHaveLength(0)
    expect(getShelvedAlarms()).toHaveLength(0)
  })

  it('getAlarmHistory 返回全部', () => {
    addRule(defaultRule())
    checkAlarms({ 'DB1:0': 150 })
    expect(getAlarmHistory()).toHaveLength(1)
  })

  it('getAlarm 按 ID 查找', () => {
    addRule(defaultRule())
    checkAlarms({ 'DB1:0': 150 })
    const id = getActiveAlarms()[0].id
    expect(getAlarm(id)).toBeDefined()
    expect(getAlarm(id)!.id).toBe(id)
    expect(getAlarm('notexist')).toBeUndefined()
  })
})

describe('getStatistics', () => {
  beforeEach(() => { resetTestState() })

  it('无报警时统计均为零', () => {
    const s = getStatistics()
    expect(s.totalActive).toBe(0)
    expect(s.totalUnacknowledged).toBe(0)
    expect(s.totalShelved).toBe(0)
    expect(s.totalEmergency).toBe(0)
    expect(s.totalCritical).toBe(0)
  })

  it('有报警时统计正确', () => {
    addRule(defaultRule({ severity: AlarmSeverity.Emergency }))
    addRule(defaultRule({ severity: AlarmSeverity.Critical, variableKey: 'V2', name: 'r2' }))
    addRule(defaultRule({ severity: AlarmSeverity.Warning, variableKey: 'V3', name: 'r3' }))
    checkAlarms({ 'DB1:0': 150, 'V2': 150, 'V3': 150 })
    const s = getStatistics()
    expect(s.totalActive).toBe(3)
    expect(s.totalEmergency).toBe(1)
    expect(s.totalCritical).toBe(1)
    expect(s.totalShelved).toBe(0)
  })

  it('已确认不计入 totalUnacknowledged', () => {
    addRule(defaultRule())
    checkAlarms({ 'DB1:0': 150 })
    acknowledgeAlarm(getActiveAlarms()[0].id)
    expect(getStatistics().totalUnacknowledged).toBe(0)
  })
})

describe('CSV 导出/导入', () => {
  beforeEach(() => { resetTestState() })

  it('exportAlarmsCsv 生成标准 CSV 表头', () => {
    expect(exportAlarmsCsv()).toContain('时间,严重度,类型,变量,描述,区域,值,阈值,死区,状态,确认人,确认时间,备注,搁置人,搁置到期')
  })

  it('exportAlarmsCsv 含报警数据行', () => {
    addRule(defaultRule({ description: '温度超标', area: 'Line1' }))
    checkAlarms({ 'DB1:0': 150 })
    const csv = exportAlarmsCsv()
    expect(csv).toContain('Critical')
    expect(csv).toContain('温度超标')
    expect(csv).toContain('Line1')
  })

  it('exportRulesCsv 生成含规则数据的 CSV', () => {
    addRule(defaultRule())
    const csv = exportRulesCsv()
    expect(csv).toContain('VariableKey,DataType')
    expect(csv).toContain('DB1:0')
    expect(csv).toContain('Critical')
  })

  it('importRulesCsv 导入规则', () => {
    const csv = 'VariableKey,DataType,Description,Severity,ConditionType,Threshold,Deadband,OnDelayMs,OffDelayMs,Area,IsEnabled\nDB1:0,REAL,测试规则,Critical,0,100,5,0,0,Line1,true'
    expect(importRulesCsv(csv)).toBe(1)
    expect(getRules()).toHaveLength(1)
    expect(getRules()[0].variableKey).toBe('DB1:0')
    expect(getRules()[0].threshold).toBe(100)
  })

  it('importRulesCsv 空文件返回 0', () => {
    expect(importRulesCsv('')).toBe(0)
    expect(importRulesCsv('Header\n')).toBe(0)
  })
})

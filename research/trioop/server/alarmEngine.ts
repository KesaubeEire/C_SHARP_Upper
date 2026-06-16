/**
 * 报警引擎
 *
 * 定义报警规则 → 每次数据更新时检查 → 触发/恢复 → 记录历史
 */

export type AlarmCondition = 'eq' | 'ne' | 'gt' | 'lt' | 'ge' | 'le'

export interface AlarmRule {
  name: string                // 规则唯一名
  variableName: string        // 监听的变量名
  condition: AlarmCondition   // 条件
  threshold: number           // 阈值
  message: string             // 报警文本
  enabled: boolean
}

export interface AlarmEvent {
  ruleName: string
  variableName: string
  message: string
  value: number | boolean
  threshold: number
  condition: AlarmCondition
  active: boolean             // true=触发, false=已恢复
  triggeredAt: number
  ackedAt?: number            // 确认时间
  recoveredAt?: number        // 恢复时间
}

// ─── 存储 ────────────────────────────────────────────────
let rules: AlarmRule[] = [
  // 预设示例（用户可删改）
  // { name: '故障', variableName: '状态_故障', condition: 'eq', threshold: 1, message: '设备故障', enabled: true },
]

let activeAlarms: Map<string, AlarmEvent> = new Map()
let alarmHistory: AlarmEvent[] = []
const MAX_HISTORY = 500

// ─── 规则管理 ────────────────────────────────────────────
export function getRules(): AlarmRule[] {
  return [...rules]
}

export function setRule(rule: AlarmRule): void {
  const idx = rules.findIndex(r => r.name === rule.name)
  if (idx >= 0) rules[idx] = rule
  else rules.push(rule)
}

export function deleteRule(name: string): void {
  rules = rules.filter(r => r.name !== name)
  activeAlarms.delete(name)
}

// ─── 报警检查 ────────────────────────────────────────────
function evaluateCondition(value: number, condition: AlarmCondition, threshold: number): boolean {
  switch (condition) {
    case 'eq': return value === threshold
    case 'ne': return value !== threshold
    case 'gt': return value > threshold
    case 'lt': return value < threshold
    case 'ge': return value >= threshold
    case 'le': return value <= threshold
  }
}

/**
 * 检查所有启用规则，返回新触发的报警事件
 * @param data  { variableName: value }
 * @returns 新报警事件数组（用于 SSE 推送）
 */
export function checkAlarms(data: Record<string, number | boolean>): AlarmEvent[] {
  const newEvents: AlarmEvent[] = []
  const now = Date.now()

  for (const rule of rules) {
    if (!rule.enabled) continue
    const rawVal = data[rule.variableName]
    if (rawVal === undefined || rawVal === null) continue
    const val = typeof rawVal === 'number' ? rawVal : (rawVal ? 1 : 0)
    const isActive = evaluateCondition(val, rule.condition, rule.threshold)
    const existing = activeAlarms.get(rule.name)

    if (isActive && !existing) {
      // 新触发
      const ev: AlarmEvent = {
        ruleName: rule.name, variableName: rule.variableName, message: rule.message,
        value: val, threshold: rule.threshold, condition: rule.condition,
        active: true, triggeredAt: now,
      }
      activeAlarms.set(rule.name, ev)
      alarmHistory.push(ev)
      newEvents.push(ev)
    } else if (!isActive && existing) {
      // 恢复
      existing.active = false
      existing.recoveredAt = now
      existing.value = val
      alarmHistory.push({ ...existing })
      activeAlarms.delete(rule.name)
    } else if (isActive && existing) {
      // 持续触发，更新值
      existing.value = val
    }
  }

  // 裁剪历史
  if (alarmHistory.length > MAX_HISTORY) {
    alarmHistory = alarmHistory.slice(alarmHistory.length - MAX_HISTORY)
  }

  return newEvents
}

// ─── 查询 ────────────────────────────────────────────────
export function getActiveAlarms(): AlarmEvent[] {
  return Array.from(activeAlarms.values())
}

export function getAlarmHistory(): AlarmEvent[] {
  return [...alarmHistory]
}

export function acknowledgeAlarm(ruleName: string): boolean {
  const alarm = activeAlarms.get(ruleName)
  if (alarm && alarm.active) {
    alarm.ackedAt = Date.now()
    return true
  }
  return false
}

export function acknowledgeAll(): number {
  let count = 0
  for (const [name, alarm] of activeAlarms) {
    if (alarm.active) {
      alarm.ackedAt = Date.now()
      count++
    }
  }
  return count
}

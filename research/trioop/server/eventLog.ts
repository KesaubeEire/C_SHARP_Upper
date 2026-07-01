/**
 * 操作事件日志 — 审计追踪
 *
 * 记录所有关键操作：写值、配方修改、报警确认、连接变更等。
 * 内存环形缓冲区，最多保留 2000 条。
 */

export interface EventEntry {
  id: number
  time: string          // ISO 8601
  type: EventType
  user: string
  message: string
  detail?: string       // 可选详细信息（如旧值 → 新值）
}

export type EventType =
  | 'plc.write'
  | 'plc.connect'
  | 'plc.disconnect'
  | 'recipe.create'
  | 'recipe.update'
  | 'recipe.delete'
  | 'recipe.copy'
  | 'recipe.apply'
  | 'recipe.upload'
  | 'recipe.snapshot'
  | 'recipe.restore'
  | 'alarm.trigger'
  | 'alarm.recover'
  | 'alarm.ack'
  | 'alarm.shelve'
  | 'alarm.unshelve'
  | 'alarm.comment'
  | 'alarm.clear'
  | 'alarm.rule_add'
  | 'alarm.rule_update'
  | 'alarm.rule_delete'
  | 'alarm.export'
  | 'alarm.rules_export'
  | 'alarm.rules_import'
  | 'system'
  | 'auth.login'
  | 'auth.logout'
  | 'auth.user'

const MAX_EVENTS = 2000
const events: EventEntry[] = []
let nextId = 1

export function logEvent(type: EventType, message: string, user = 'system', detail?: string): void {
  const entry: EventEntry = {
    id: nextId++,
    time: new Date().toISOString(),
    type,
    user,
    message,
    detail,
  }
  events.push(entry)
  if (events.length > MAX_EVENTS) events.shift()
}

export function getEvents(limit = 100, offset = 0, type?: EventType): EventEntry[] {
  let filtered = events
  if (type) filtered = filtered.filter(e => e.type === type)
  return filtered.slice(-offset - limit, -offset || undefined).reverse()
}

export function getEventCount(type?: EventType): number {
  if (type) return events.filter(e => e.type === type).length
  return events.length
}

export function getRecentEvents(minutes = 5): EventEntry[] {
  const cutoff = Date.now() - minutes * 60 * 1000
  return events.filter(e => new Date(e.time).getTime() > cutoff)
}

/** 获取事件类型统计 */
export function getEventStats(): Record<EventType, number> {
  const stats: Record<string, number> = {}
  for (const e of events) {
    stats[e.type] = (stats[e.type] || 0) + 1
  }
  return stats as Record<EventType, number>
}

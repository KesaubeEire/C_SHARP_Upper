import { useState, useEffect, useCallback } from 'react'

interface EventEntry {
  id: number
  time: string
  type: string
  user: string
  message: string
  detail?: string
}

const EVENT_TYPE_LABELS: Record<string, string> = {
  'plc.write': '✏️ PLC 写入',
  'plc.connect': '🔌 PLC 连接',
  'plc.disconnect': '🔌 PLC 断开',
  'recipe.create': '📋 配方创建',
  'recipe.update': '📋 配方更新',
  'recipe.delete': '📋 配方删除',
  'recipe.upload': '📋 配方上传',
  'recipe.copy': '📋 配方复制',
  'recipe.apply': '📋 配方下载',
  'recipe.snapshot': '📋 配方快照',
  'recipe.restore': '📋 配方恢复',
  'alarm.trigger': '🔔 报警触发',
  'alarm.recover': '🔔 报警恢复',
  'alarm.ack': '🔔 报警确认',
  'alarm.shelve': '🔔 报警搁置',
  'alarm.unshelve': '🔔 取消搁置',
  'alarm.comment': '🔔 报警备注',
  'alarm.clear': '🔔 报警清除',
  'alarm.rule_add': '⚙️ 规则添加',
  'alarm.rule_update': '⚙️ 规则更新',
  'alarm.rule_delete': '⚙️ 规则删除',
  'alarm.export': '📤 报警导出',
  'alarm.rules_export': '📤 规则导出',
  'alarm.rules_import': '📂 规则导入',
  'auth.login': '🔐 登录',
  'auth.logout': '🔐 登出',
  'system': '⚙️ 系统',
}

const EVENT_ORDER: string[] = [
  'plc.write', 'plc.connect', 'plc.disconnect',
  'recipe.create', 'recipe.update', 'recipe.delete', 'recipe.copy', 'recipe.apply', 'recipe.upload', 'recipe.snapshot', 'recipe.restore',
  'alarm.trigger', 'alarm.recover', 'alarm.ack', 'alarm.shelve', 'alarm.unshelve', 'alarm.comment', 'alarm.clear',
  'alarm.rule_add', 'alarm.rule_update', 'alarm.rule_delete',
  'alarm.export', 'alarm.rules_export', 'alarm.rules_import',
  'auth.login', 'auth.logout',
]

/** 根据事件类型和消息推断 severity，决定左侧边框颜色 */
function eventSeverity(type: string, message: string): 'info' | 'warn' | 'error' {
  if (message.includes('失败') || message.includes('超时') || message.includes('error') || message.includes('failed')) return 'error'
  if (type.startsWith('alarm') || message.includes('警告') || message.includes('warn')) return 'warn'
  if (type === 'plc.write' && (message.includes('失败') || message.includes('fail'))) return 'error'
  return 'info'
}

/** 格式化时间为 HH:mm:ss */
function fmtTime(iso: string): string {
  const d = new Date(iso)
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}:${String(d.getSeconds()).padStart(2, '0')}`
}

/** 格式化时间为完整日期时间 */
function fmtDateTime(iso: string): string {
  const d = new Date(iso)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}:${String(d.getSeconds()).padStart(2, '0')}`
}

export default function EventLogPanel() {
  const [events, setEvents] = useState<EventEntry[]>([])
  const [stats, setStats] = useState<Record<string, number>>({})
  const [filter, setFilter] = useState<string>('')
  const [autoRefresh, setAutoRefresh] = useState(true)

  const fetchEvents = useCallback(() => {
    const url = filter ? `/api/events?limit=200&type=${filter}` : '/api/events?limit=200'
    fetch(url)
      .then(r => r.json())
      .then(data => setEvents(data.events || []))
      .catch(() => {})
    fetch('/api/events/stats')
      .then(r => r.json())
      .then(data => setStats(data || {}))
      .catch(() => {})
  }, [filter])

  useEffect(() => { fetchEvents() }, [fetchEvents])

  useEffect(() => {
    if (!autoRefresh) return
    const timer = setInterval(fetchEvents, 5000)
    return () => clearInterval(timer)
  }, [autoRefresh, fetchEvents])

  return (
    <div className="vt-event-log" style={{ border: 'none', borderRadius: 0, background: 'transparent' }}>
      {/* ── 工具栏 ── */}
      <div className="vt-event-log__toolbar" style={{ flexWrap: 'wrap', gap: 8 }}>
        <select
          className="vt-event-log__filter"
          value={filter} onChange={e => setFilter(e.target.value)}
          style={{ width: 160, height: 26, padding: '0 6px' }}
        >
          <option value="">全部事件</option>
          {EVENT_ORDER.map(t => (
            <option key={t} value={t}>{EVENT_TYPE_LABELS[t] || t}</option>
          ))}
        </select>

        <label style={{ display: 'inline-flex', alignItems: 'center', gap: 4, cursor: 'pointer', userSelect: 'none', height: 26, fontSize: 12 }}>
          <input type="checkbox" checked={autoRefresh} onChange={e => setAutoRefresh(e.target.checked)} />
          自动刷新
        </label>

        <button className="btn btn--ghost btn--sm" onClick={fetchEvents}>⟳ 刷新</button>

        <span style={{ marginLeft: 'auto', fontSize: 11, color: 'var(--vt-text-muted)' }}>
          {events.length} 条
        </span>
      </div>

      {/* ── 统计标签 ── */}
      {Object.keys(stats).length > 0 && (
        <div style={{
          display: 'flex', gap: 4, flexWrap: 'wrap',
          padding: '4px 12px 8px',
          background: 'var(--vt-bg-elevated)',
          borderBottom: '1px solid var(--vt-border)',
        }}>
          {Object.entries(stats)
            .sort(([, a], [, b]) => b - a)
            .slice(0, 12)
            .map(([type, count]) => (
              <span
                key={type}
                onClick={() => setFilter(filter === type ? '' : type)}
                style={{
                  fontSize: 10, padding: '1px 8px', borderRadius: 4, cursor: 'pointer',
                  whiteSpace: 'nowrap', fontFamily: 'var(--vt-font-sans)',
                  background: filter === type ? 'var(--vt-color-info)' : 'var(--vt-bg-panel)',
                  color: filter === type ? '#fff' : 'var(--vt-text-label)',
                  border: '1px solid var(--vt-border)',
                  transition: 'all 0.15s',
                }}
              >
                {(EVENT_TYPE_LABELS[type] || type).replace(/^[^\s]+\s/, '')}: {count}
              </span>
            ))}
        </div>
      )}

      {/* ── 事件列表 ── */}
      <div style={{
        maxHeight: 420, overflowY: 'auto',
        background: 'var(--vt-bg-panel)',
        border: '1px solid var(--vt-border)',
        borderRadius: 'var(--vt-radius-md)',
      }}>
        {events.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 32, color: 'var(--vt-text-muted)', fontSize: 13 }}>
            暂无事件记录
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column' }}>
            {/* 表头 */}
            <div style={{
              display: 'grid',
              gridTemplateColumns: '75px 110px 70px 1fr',
              gap: 0,
              padding: '6px 12px',
              background: 'var(--vt-bg-elevated)',
              borderBottom: '1px solid var(--vt-border)',
              fontSize: 11,
              fontWeight: 600,
              color: 'var(--vt-text-label)',
              position: 'sticky',
              top: 0,
              zIndex: 1,
            }}>
              <span>时间</span>
              <span>类型</span>
              <span>用户</span>
              <span>描述</span>
            </div>

            {/* 行 */}
            {events.map(e => {
              const sev = eventSeverity(e.type, e.message)
              return (
                <div
                  key={e.id}
                  title={`${fmtDateTime(e.time)} | ${EVENT_TYPE_LABELS[e.type] || e.type} | ${e.user}${e.detail ? `\n${e.detail}` : ''}`}
                  style={{
                    display: 'grid',
                    gridTemplateColumns: '75px 110px 70px 1fr',
                    gap: 0,
                    padding: '5px 12px',
                    borderLeft: '3px solid transparent',
                    borderLeftColor: sev === 'error' ? 'var(--vt-color-danger)'
                      : sev === 'warn' ? 'var(--vt-color-warn)'
                      : 'var(--vt-color-info)',
                    borderBottom: '1px solid var(--vt-border)',
                    fontSize: 12,
                    fontFamily: 'var(--vt-font-sans)',
                    color: 'var(--vt-text-primary)',
                    alignItems: 'baseline',
                    transition: 'background 0.1s',
                    cursor: 'default',
                  }}
                  onMouseEnter={e => (e.currentTarget as HTMLElement).style.background = 'var(--vt-bg-elevated)'}
                  onMouseLeave={e => (e.currentTarget as HTMLElement).style.background = 'transparent'}
                >
                  <span style={{
                    fontFamily: 'var(--vt-font-mono)',
                    fontSize: 11,
                    color: 'var(--vt-text-muted)',
                    fontVariantNumeric: 'tabular-nums',
                  }}>
                    {fmtTime(e.time)}
                  </span>
                  <span style={{
                    fontSize: 11,
                    color: sev === 'error' ? 'var(--vt-color-danger)'
                      : sev === 'warn' ? 'var(--vt-color-warn)'
                      : 'var(--vt-text-label)',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                  }}>
                    {EVENT_TYPE_LABELS[e.type] || e.type}
                  </span>
                  <span style={{
                    fontSize: 11,
                    color: 'var(--vt-text-muted)',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                  }}>
                    {e.user}
                  </span>
                  <span style={{
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                  }}>
                    {e.message}
                    {e.detail && (
                      <span style={{ color: 'var(--vt-text-muted)', marginLeft: 6, fontSize: 11 }}>
                        ({e.detail})
                      </span>
                    )}
                  </span>
                </div>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}

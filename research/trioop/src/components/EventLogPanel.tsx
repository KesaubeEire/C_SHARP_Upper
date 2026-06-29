import React, { useState, useEffect, useCallback } from 'react'

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
  'recipe.copy': '📋 配方复制',
  'recipe.apply': '📋 配方下载',
  'recipe.snapshot': '📋 配方快照',
  'recipe.restore': '📋 配方恢复',
  'alarm.ack': '🔔 报警确认',
  'alarm.shelve': '🔔 报警搁置',
  'alarm.comment': '🔔 报警备注',
  'alarm.clear': '🔔 报警清除',
  'auth.login': '🔐 登录',
  'auth.logout': '🔐 登出',
  'system': '⚙️ 系统',
}

const EVENT_ORDER: string[] = [
  'plc.write', 'plc.connect', 'plc.disconnect',
  'recipe.create', 'recipe.update', 'recipe.delete', 'recipe.copy', 'recipe.apply', 'recipe.snapshot', 'recipe.restore',
  'alarm.ack', 'alarm.shelve', 'alarm.comment', 'alarm.clear',
  'auth.login', 'auth.logout',
]

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

  useEffect(() => {
    fetchEvents()
  }, [fetchEvents])

  // Auto refresh every 5 seconds
  useEffect(() => {
    if (!autoRefresh) return
    const timer = setInterval(fetchEvents, 5000)
    return () => clearInterval(timer)
  }, [autoRefresh, fetchEvents])

  return (
    <div className="event-log">
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
        <select value={filter} onChange={e => setFilter(e.target.value)}
          className="input" style={{ width: 160, fontSize: 12 }}>
          <option value="">全部事件</option>
          {EVENT_ORDER.map(t => (
            <option key={t} value={t}>{EVENT_TYPE_LABELS[t] || t}</option>
          ))}
        </select>
        <label style={{ fontSize: 12, display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer' }}>
          <input type="checkbox" checked={autoRefresh} onChange={e => setAutoRefresh(e.target.checked)} />
          自动刷新
        </label>
        <button className="btn btn--ghost btn--sm" onClick={fetchEvents}>⟳ 刷新</button>
        <span style={{ fontSize: 11, color: 'var(--text-muted)', marginLeft: 'auto' }}>{events.length} 条</span>
      </div>

      {/* 统计摘要 */}
      {Object.keys(stats).length > 0 && (
        <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', marginBottom: 6 }}>
          {Object.entries(stats)
            .sort(([, a], [, b]) => b - a)
            .slice(0, 10)
            .map(([type, count]) => (
              <span key={type} style={{
                fontSize: 10, padding: '1px 6px', borderRadius: 4,
                background: filter === type ? 'var(--primary)' : 'var(--bg-surface)',
                color: filter === type ? '#fff' : 'var(--text-muted)',
                cursor: 'pointer', whiteSpace: 'nowrap',
              }} onClick={() => setFilter(filter === type ? '' : type)}>
                {EVENT_TYPE_LABELS[type] || type}: {count}
              </span>
            ))}
        </div>
      )}

      <div style={{ maxHeight: 400, overflowY: 'auto', fontSize: 12, fontFamily: 'monospace' }}>
        {events.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 24, color: 'var(--text-muted)' }}>暂无事件记录</div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                <th style={{ ...thStyle, position: 'sticky', top: 0, background: 'var(--bg)', zIndex: 1 }}>时间</th>
                <th style={{ ...thStyle, position: 'sticky', top: 0, background: 'var(--bg)', zIndex: 1 }}>类型</th>
                <th style={{ ...thStyle, position: 'sticky', top: 0, background: 'var(--bg)', zIndex: 1 }}>用户</th>
                <th style={{ ...thStyle, position: 'sticky', top: 0, background: 'var(--bg)', zIndex: 1 }}>描述</th>
              </tr>
            </thead>
            <tbody>
              {events.map(e => (
                <tr key={e.id} style={{ borderBottom: '1px solid var(--border)', opacity: e.type.startsWith('plc.write') && e.message.includes('失败') ? 0.6 : 1 }}>
                  <td style={tdStyle}>{new Date(e.time).toLocaleTimeString()}</td>
                  <td style={tdStyle}><span title={e.type}>{EVENT_TYPE_LABELS[e.type] || e.type}</span></td>
                  <td style={tdStyle}>{e.user}</td>
                  <td style={tdStyle}>
                    {e.message}
                    {e.detail && <span style={{ color: 'var(--text-muted)', marginLeft: 4 }}>({e.detail})</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}

const thStyle: React.CSSProperties = {
  padding: '4px 6px',
  textAlign: 'left',
  fontWeight: 600,
  fontSize: 11,
  color: 'var(--text-muted)',
  whiteSpace: 'nowrap',
}

const tdStyle: React.CSSProperties = {
  padding: '3px 6px',
  whiteSpace: 'nowrap',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  maxWidth: 300,
}

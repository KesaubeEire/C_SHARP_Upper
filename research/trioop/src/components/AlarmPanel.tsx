import { useState, useEffect, useCallback, useRef } from 'react'
import CollapsibleSection from './CollapsibleSection'
import type { AlarmItem, AlarmRule, AlarmStatistics } from '../../shared/types'

enum AlarmSeverity { Info = 0, Warning = 1, Critical = 2, Emergency = 3 }
enum AlarmConditionType { High = 0, HighHigh = 1, Low = 2, LowLow = 3, NotEqual = 4, RateOfChange = 5, Digital = 6 }

const SEV_NAMES = ['Info', 'Warning', 'Critical', 'Emergency']
const COND_NAMES = ['High', 'HighHigh', 'Low', 'LowLow', 'NotEqual', 'RateOfChange', 'Digital']
const DATA_TYPES = ['BYTE', 'WORD', 'INT', 'DINT', 'REAL', 'LREAL']

type SortDir = 'asc' | 'desc' | 'none'
type TabView = 'active' | 'history' | 'rules'

export default function AlarmPanel() {
  // ─── 数据 ────────────────────────────────────────────────────
  const [alarms, setAlarms] = useState<AlarmItem[]>([])
  const [rules, setRules] = useState<AlarmRule[]>([])
  const [stats, setStats] = useState<AlarmStatistics>({
    totalActive: 0, totalUnacknowledged: 0, totalShelved: 0,
    totalToday: 0, totalThisHour: 0, totalEmergency: 0, totalCritical: 0,
  })

  // ─── UI 状态 ─────────────────────────────────────────────────
  const [tab, setTab] = useState<TabView>('active')
  const [showRuleManager, setShowRuleManager] = useState(false)
  const [isEditingRule, setIsEditingRule] = useState(false)
  const [editingRule, setEditingRule] = useState<AlarmRule | null>(null)

  // 规则编辑表单
  const [formVarKey, setFormVarKey] = useState('')
  const [formDataType, setFormDataType] = useState('BYTE')
  const [formDesc, setFormDesc] = useState('')
  const [formSeverity, setFormSeverity] = useState(AlarmSeverity.Warning)
  const [formCondType, setFormCondType] = useState(AlarmConditionType.High)
  const [formThreshold, setFormThreshold] = useState(0)
  const [formDeadband, setFormDeadband] = useState(2)
  const [formOnDelay, setFormOnDelay] = useState(0)
  const [formOffDelay, setFormOffDelay] = useState(0)
  const [formArea, setFormArea] = useState('')
  const [formEnabled, setFormEnabled] = useState(true)

  // 过滤
  const [filterText, setFilterText] = useState('')
  const [filterSeverity, setFilterSeverity] = useState<AlarmSeverity | 'all'>('all')
  const [filterArea, setFilterArea] = useState('')
  const [showShelved, setShowShelved] = useState(true)
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')

  // 排序
  const [sortCol, setSortCol] = useState('timestamp')
  const [sortDir, setSortDir] = useState<SortDir>('desc')

  // 行内 flyout
  const [flyoutAlarmId, setFlyoutAlarmId] = useState<string | null>(null)
  const [flyoutType, setFlyoutType] = useState<'ack' | 'shelve' | null>(null)

  // 状态条
  const [statusText, setStatusText] = useState('报警系统就绪')

  // ─── 数据加载 ────────────────────────────────────────────────
  const loadRules = useCallback(async () => {
    try { setRules(await (await fetch('/api/alarm/rules')).json()) } catch {}
  }, [])

  const loadHistory = useCallback(async () => {
    try { setAlarms(await (await fetch('/api/alarm/history')).json()) } catch {}
  }, [])

  const loadStats = useCallback(async () => {
    try { setStats(await (await fetch('/api/alarm/statistics')).json()) } catch {}
  }, [])

  const loadAll = useCallback(() => { loadRules(); loadHistory(); loadStats() }, [loadRules, loadHistory, loadStats])

  useEffect(() => { loadAll() }, [loadAll])

  // ─── 轮询 SSE/活动报警 ─────────────────────────────────────
  useEffect(() => {
    const t = setInterval(() => { loadHistory(); loadStats() }, 3000)
    return () => clearInterval(t)
  }, [loadHistory, loadStats])

  // ─── 过滤 & 排序 ────────────────────────────────────────────
  const filteredAlarms = alarms
    .filter(a => {
      if (!showShelved && a.isShelved) return false
      if (filterSeverity !== 'all' && a.severity !== filterSeverity) return false
      if (filterArea && !a.area.toLowerCase().includes(filterArea.toLowerCase())) return false
      if (filterText) {
        const t = filterText.toLowerCase()
        if (!a.variableName.toLowerCase().includes(t) &&
            !a.description.toLowerCase().includes(t) &&
            !a.area.toLowerCase().includes(t)) return false
      }
      if (dateFrom && a.timestamp < new Date(dateFrom).getTime()) return false
      if (dateTo) {
        const d = new Date(dateTo)
        d.setDate(d.getDate() + 1)
        if (a.timestamp > d.getTime()) return false
      }
      return true
    })
    .sort((a, b) => {
      const dir = sortDir === 'asc' ? 1 : -1
      switch (sortCol) {
        case 'severity': return (a.severity - b.severity) * dir
        case 'variableName': return a.variableName.localeCompare(b.variableName) * dir
        case 'status': {
          const sa = a.isShelved ? 3 : (!a.isActive ? 2 : (a.isAcknowledged ? 1 : 0))
          const sb = b.isShelved ? 3 : (!b.isActive ? 2 : (b.isAcknowledged ? 1 : 0))
          return (sa - sb) * dir
        }
        case 'area': return a.area.localeCompare(b.area) * dir
        default: return (a.timestamp - b.timestamp) * dir
      }
    })

  const toggleSort = (col: string) => {
    if (sortCol === col) {
      setSortDir(s => s === 'desc' ? 'asc' : s === 'asc' ? 'none' : 'desc')
    } else {
      setSortCol(col); setSortDir('desc')
    }
  }

  const sortArrow = (col: string) => {
    if (sortCol !== col) return ''
    return sortDir === 'desc' ? ' ▼' : sortDir === 'asc' ? ' ▲' : ''
  }

  // ─── 报警操作 ───────────────────────────────────────────────
  const handleAck = async (id?: string) => {
    try {
      await fetch('/api/alarm/ack', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(id ? { id } : {}) })
      setStatusText(id ? '已确认报警' : '已全部确认')
      setFlyoutAlarmId(null)
      loadHistory(); loadStats()
    } catch {}
  }

  const handleShelve = async (id: string, durationMs?: number) => {
    try {
      await fetch(`/api/alarm/shelve/${id}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ durationMs }) })
      setStatusText('已搁置报警')
      setFlyoutAlarmId(null)
      loadHistory(); loadStats()
    } catch {}
  }

  const handleUnshelve = async (id: string) => {
    try {
      await fetch(`/api/alarm/unshelve/${id}`, { method: 'POST' })
      setStatusText('已取消搁置')
      loadHistory(); loadStats()
    } catch {}
  }

  const handleClearAll = async () => {
    if (!confirm('确定清除所有报警历史？此操作不可恢复。')) return
    try {
      await fetch('/api/alarm/clear', { method: 'POST' })
      setStatusText('报警历史已清除')
      loadAll()
    } catch {}
  }

  const handleExportCsv = async () => {
    try {
      const res = await fetch('/api/alarm/export')
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a'); a.href = url; a.download = `alarms-${new Date().toISOString().slice(0,10)}.csv`
      a.click(); URL.revokeObjectURL(url)
      setStatusText(`已导出 ${alarms.length} 条报警`)
    } catch {}
  }

  // ─── 规则操作 ───────────────────────────────────────────────
  const resetForm = () => {
    setFormVarKey(''); setFormDataType('BYTE'); setFormDesc('')
    setFormSeverity(AlarmSeverity.Warning); setFormCondType(AlarmConditionType.High)
    setFormThreshold(0); setFormDeadband(2); setFormOnDelay(0); setFormOffDelay(0)
    setFormArea(''); setFormEnabled(true); setEditingRule(null)
  }

  const handleAddRule = () => { resetForm(); setIsEditingRule(true) }

  const handleEditRule = (rule: AlarmRule) => {
    setFormVarKey(rule.variableKey); setFormDataType(rule.dataType)
    setFormDesc(rule.description); setFormSeverity(rule.severity)
    setFormCondType(rule.conditionType); setFormThreshold(rule.threshold)
    setFormDeadband(rule.deadband); setFormOnDelay(rule.onDelayMs)
    setFormOffDelay(rule.offDelayMs); setFormArea(rule.area)
    setFormEnabled(rule.isEnabled); setEditingRule(rule); setIsEditingRule(true)
  }

  const handleSaveRule = async () => {
    if (!formVarKey.trim()) { setStatusText('⚠ 变量名不能为空'); return }
    const body = {
      variableKey: formVarKey.trim(), dataType: formDataType,
      description: formDesc.trim(), severity: formSeverity,
      conditionType: formCondType, threshold: formThreshold,
      deadband: formDeadband, onDelayMs: formOnDelay, offDelayMs: formOffDelay,
      area: formArea.trim(), isEnabled: formEnabled,
    }
    try {
      if (editingRule) {
        await fetch(`/api/alarm/rules/${encodeURIComponent(editingRule.variableKey)}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
        setStatusText('规则已更新')
      } else {
        await fetch('/api/alarm/rules', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
        setStatusText('规则已添加')
      }
      setIsEditingRule(false); resetForm(); loadRules()
    } catch {}
  }

  const handleDeleteRule = async (key: string) => {
    try {
      await fetch(`/api/alarm/rules/${encodeURIComponent(key)}`, { method: 'DELETE' })
      setStatusText('规则已删除'); loadRules()
    } catch {}
  }

  const handleExportRulesCsv = async () => {
    try {
      const res = await fetch('/api/alarm/rules/export')
      const blob = await res.blob()
      const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'alarm-rules.csv'
      a.click()
      setStatusText(`已导出 ${rules.length} 条规则`)
    } catch {}
  }

  const handleImportRulesCsv = () => {
    const input = document.createElement('input'); input.type = 'file'; input.accept = '.csv'
    input.onchange = async () => {
      const file = input.files?.[0]
      if (!file) return
      try {
        const text = await file.text()
        const res = await fetch('/api/alarm/rules/import', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ csv: text }) })
        const data = await res.json()
        setStatusText(`已导入 ${data.imported || 0} 条规则`)
        loadRules()
      } catch {}
    }
    input.click()
  }

  // ─── 渲染 ────────────────────────────────────────────────────
  const sevClass = (s: AlarmSeverity) => `severity-pill--${SEV_NAMES[s].toLowerCase()}`
  const statusClass = (a: AlarmItem) => {
    if (a.isShelved) return 'status-pill--shelved'
    if (!a.isActive) return 'status-pill--recovered'
    if (a.isAcknowledged) return 'status-pill--acknowledged'
    return 'status-pill--active'
  }
  const statusTextFn = (a: AlarmItem) => {
    if (a.isShelved) return '已搁置'
    if (!a.isActive) return '已恢复'
    if (a.isAcknowledged) return '已确认'
    return '未确认'
  }

  return (
    <CollapsibleSection title="🔔 报警管理" storageKey="alarm-manager">
      {/* ─── 统计卡片 ─────────────────────────────────── */}
      <div className="alarm-stats">
        {[
          { label: '总报警', value: stats.totalActive + stats.totalShelved, cls: '' },
          { label: '活动', value: stats.totalActive, cls: '' },
          { label: '未确认', value: stats.totalUnacknowledged, cls: 'alarm-stat-card--emergency' },
          { label: '今日', value: stats.totalToday, cls: '' },
          { label: '本小时', value: stats.totalThisHour, cls: '' },
          { label: '紧急', value: stats.totalEmergency, cls: 'alarm-stat-card--emergency' },
          { label: '严重', value: stats.totalCritical, cls: 'alarm-stat-card--critical' },
        ].map(s => (
          <div key={s.label} className={`alarm-stat-card ${s.cls}`}>
            <div className="alarm-stat-card__value">{s.value}</div>
            <div className="alarm-stat-card__label">{s.label}</div>
          </div>
        ))}
      </div>

      {/* ─── 工具栏 ───────────────────────────────────── */}
      <div className="alarm-toolbar">
        <button className="btn btn--sm btn--primary" onClick={loadAll} title="刷新">🔄 刷新</button>

        <div style={{ position: 'relative', display: 'inline-block' }}>
          <button className="btn btn--sm btn--success" onClick={() => { setFlyoutAlarmId('__all__'); setFlyoutType('ack') }}>✓ 全部确认</button>
          {flyoutAlarmId === '__all__' && flyoutType === 'ack' && (
            <>
              <div className="alarm-flyout-overlay" onClick={() => setFlyoutAlarmId(null)} />
              <div className="alarm-flyout" style={{ top: '100%', left: 0, marginTop: 4 }}>
                <div className="alarm-flyout__title">确认所有未确认报警？</div>
                <div className="alarm-flyout__actions">
                  <button className="btn btn--sm" onClick={() => setFlyoutAlarmId(null)}>取消</button>
                  <button className="btn btn--sm btn--primary" onClick={() => handleAck()}>确定</button>
                </div>
              </div>
            </>
          )}
        </div>

        <div style={{ position: 'relative', display: 'inline-block' }}>
          <button className="btn btn--sm btn--secondary" onClick={() => { setFlyoutAlarmId('__shelve__'); setFlyoutType('shelve') }}>⌛ 批量搁置</button>
          {flyoutAlarmId === '__shelve__' && flyoutType === 'shelve' && (
            <>
              <div className="alarm-flyout-overlay" onClick={() => setFlyoutAlarmId(null)} />
              <div className="alarm-flyout" style={{ top: '100%', left: 0, marginTop: 4 }}>
                <div className="alarm-flyout__title">搁置时长</div>
                <div className="alarm-flyout__actions" style={{ flexWrap: 'wrap' }}>
                  <button className="btn btn--sm" onClick={() => setFlyoutAlarmId(null)}>取消</button>
                  <button className="btn btn--sm btn--primary" onClick={async () => {
                    const active = alarms.filter(a => a.isActive && !a.isShelved)
                    for (const a of active) await fetch(`/api/alarm/shelve/${a.id}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ durationMs: 30 * 60 * 1000 }) })
                    setFlyoutAlarmId(null); setStatusText(`已搁置 ${active.length} 条`); loadHistory()
                  }}>30 分钟</button>
                  <button className="btn btn--sm btn--primary" onClick={async () => {
                    const active = alarms.filter(a => a.isActive && !a.isShelved)
                    for (const a of active) await fetch(`/api/alarm/shelve/${a.id}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ durationMs: 60 * 60 * 1000 }) })
                    setFlyoutAlarmId(null); setStatusText(`已搁置 ${active.length} 条`); loadHistory()
                  }}>1 小时</button>
                  <button className="btn btn--sm btn--primary" onClick={async () => {
                    const active = alarms.filter(a => a.isActive && !a.isShelved)
                    for (const a of active) await fetch(`/api/alarm/shelve/${a.id}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ durationMs: 8 * 60 * 60 * 1000 }) })
                    setFlyoutAlarmId(null); setStatusText(`已搁置 ${active.length} 条`); loadHistory()
                  }}>8 小时</button>
                  <button className="btn btn--sm btn--primary" onClick={async () => {
                    const active = alarms.filter(a => a.isActive && !a.isShelved)
                    for (const a of active) await fetch(`/api/alarm/shelve/${a.id}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({}) })
                    setFlyoutAlarmId(null); setStatusText(`已搁置 ${active.length} 条`); loadHistory()
                  }}>永久</button>
                </div>
              </div>
            </>
          )}
        </div>

        <div className="alarm-toolbar__spacer" />

        <button className="btn btn--sm btn--secondary" onClick={handleExportCsv}>📤 导出 CSV</button>
        <button className="btn btn--sm btn--secondary" onClick={() => setShowRuleManager(!showRuleManager)}>
          {showRuleManager ? '✕ 关闭规则' : '⚙ 规则管理'}
        </button>
        <button className="btn btn--sm btn--danger" onClick={handleClearAll}>🗑 清除</button>
      </div>

      {/* ─── 规则管理面板 ─────────────────────────────── */}
      {showRuleManager && (
        <div className="alarm-rule-panel">
          <div className="alarm-rule-header">
            <span className="alarm-rule-header__title">规则管理</span>
            <div style={{ display: 'flex', gap: 4 }}>
              <button className="btn btn--sm btn--secondary" onClick={handleExportRulesCsv}>导出 CSV</button>
              <button className="btn btn--sm btn--secondary" onClick={handleImportRulesCsv}>导入 CSV</button>
              <button className="btn btn--sm btn--primary" onClick={handleAddRule}>+ 添加规则</button>
            </div>
          </div>

          {/* 规则列表 */}
          <table className="alarm-rule-table">
            <thead>
              <tr>
                <th>启用</th><th>变量</th><th>数据类型</th><th>级别</th><th>报警类型</th><th>阈值</th><th>区域</th><th>操作</th>
              </tr>
            </thead>
            <tbody>
              {rules.map(r => (
                <tr key={r.variableKey}>
                  <td style={{ textAlign: 'center' }}>
                    <input type="checkbox" checked={r.isEnabled} onChange={async () => {
                      try {
                        await fetch(`/api/alarm/rules/${encodeURIComponent(r.variableKey)}`, {
                          method: 'PUT',
                          headers: { 'Content-Type': 'application/json' },
                          body: JSON.stringify({
                            variableKey: r.variableKey,
                            dataType: r.dataType,
                            description: r.description,
                            severity: r.severity,
                            conditionType: r.conditionType,
                            condition: r.condition,
                            threshold: r.threshold,
                            deadband: r.deadband,
                            onDelayMs: r.onDelayMs,
                            offDelayMs: r.offDelayMs,
                            area: r.area,
                            isEnabled: !r.isEnabled,
                          }),
                        })
                        setStatusText(r.isEnabled ? '规则已禁用' : '规则已启用')
                        loadRules()
                      } catch {}
                    }} />
                  </td>
                  <td><span className="alarm-rule-table__dt">{r.dataType}</span></td>
                  <td><span className={`severity-pill ${sevClass(r.severity)}`}>{SEV_NAMES[r.severity]}</span></td>
                  <td style={{ color: 'var(--muted-foreground)' }}>{COND_NAMES[r.conditionType]}</td>
                  <td>
                    {r.threshold}
                    {r.deadband > 0 && <span style={{ marginLeft: 4, fontSize: 10, color: 'var(--muted-foreground)' }}>±{r.deadband}</span>}
                  </td>
                  <td style={{ fontSize: 11 }}>{r.area}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <button className="btn btn--sm btn--secondary" onClick={() => handleEditRule(r)} style={{ marginRight: 4 }}>编辑</button>
                    <button className="btn btn--sm btn--danger" onClick={() => handleDeleteRule(r.variableKey)}>删除</button>
                  </td>
                </tr>
              ))}
              {rules.length === 0 && (
                <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: 'var(--muted-foreground)' }}>暂无规则，点击「添加规则」创建</td></tr>
              )}
            </tbody>
          </table>

          {/* 编辑/添加表单 */}
          {isEditingRule && (
            <div className="alarm-form">
              <div className="alarm-form__group">
                <span className="alarm-form__label">变量名 *</span>
                <input className="alarm-form__input" placeholder="I0, Q5, M10, DB1:6..." value={formVarKey} onChange={e => setFormVarKey(e.target.value)} />
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">数据类型</span>
                <select className="alarm-form__select" value={formDataType} onChange={e => setFormDataType(e.target.value)}>
                  {DATA_TYPES.map(d => <option key={d} value={d}>{d}</option>)}
                </select>
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">级别</span>
                <select className="alarm-form__select" value={formSeverity} onChange={e => setFormSeverity(Number(e.target.value))}>
                  {SEV_NAMES.map((n, i) => <option key={n} value={i}>{n}</option>)}
                </select>
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">报警类型</span>
                <select className="alarm-form__select" value={formCondType} onChange={e => setFormCondType(Number(e.target.value))}>
                  {COND_NAMES.map((n, i) => <option key={n} value={i}>{n}</option>)}
                </select>
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">阈值</span>
                <input className="alarm-form__input" type="number" value={formThreshold} onChange={e => setFormThreshold(Number(e.target.value))} />
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">死区</span>
                <input className="alarm-form__input" type="number" min="0" value={formDeadband} onChange={e => setFormDeadband(Number(e.target.value))} />
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">触发延时 (ms)</span>
                <input className="alarm-form__input" type="number" min="0" max="60000" value={formOnDelay} onChange={e => setFormOnDelay(Number(e.target.value))} />
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">恢复延时 (ms)</span>
                <input className="alarm-form__input" type="number" min="0" max="60000" value={formOffDelay} onChange={e => setFormOffDelay(Number(e.target.value))} />
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">区域</span>
                <input className="alarm-form__input" placeholder="反应釜A" value={formArea} onChange={e => setFormArea(e.target.value)} />
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">描述</span>
                <input className="alarm-form__input" placeholder="例如：温度高限报警" value={formDesc} onChange={e => setFormDesc(e.target.value)} />
              </div>
              <div className="alarm-form__group">
                <span className="alarm-form__label">启用</span>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 4, fontSize: 12 }}>
                  <input type="checkbox" checked={formEnabled} onChange={e => setFormEnabled(e.target.checked)} />
                  {formEnabled ? '已启用' : '已禁用'}
                </label>
              </div>
              <div className="alarm-form__actions">
                <button className="btn btn--sm" onClick={() => { setIsEditingRule(false); setEditingRule(null) }}>取消</button>
                <button className="btn btn--sm btn--primary" onClick={handleSaveRule}>保存</button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ─── View tabs ────────────────────────────────── */}
      <div className="alarm-tabs" style={{ display: 'flex', gap: 4, marginBottom: 8 }}>
        {(['active', 'history', 'rules'] as const).map(t => {
          const counts: Record<string, number> = { active: stats.totalActive + stats.totalShelved, history: alarms.length, rules: rules.length }
          return (
            <button key={t} className={`btn btn--sm ${tab === t ? 'btn--primary' : ''}`} onClick={() => setTab(t)}>
              {{ active: '活动报警', history: '报警历史', rules: '报警规则' }[t]}
              {t === 'active' && (stats.totalActive + stats.totalShelved) > 0 && (
                <span style={{ marginLeft: 4, padding: '0 5px', borderRadius: 3, background: 'var(--destructive)', color: '#fff', fontSize: 10 }}>{stats.totalActive + stats.totalShelved}</span>
              )}
            </button>
          )
        })}
      </div>

      {/* ─── 过滤栏 ───────────────────────────────────── */}
      <div className="alarm-filterbar">
        <div className="alarm-filterbar__group">
          <span className="alarm-filterbar__label">搜索</span>
          <input className="alarm-filterbar__input" placeholder="变量/描述/区域..." value={filterText} onChange={e => setFilterText(e.target.value)} />
        </div>
        <div className="alarm-filterbar__group">
          <span className="alarm-filterbar__label">级别</span>
          <select className="alarm-filterbar__select" value={filterSeverity} onChange={e => setFilterSeverity(e.target.value === 'all' ? 'all' : Number(e.target.value))}>
            <option value="all">全部级别</option>
            {SEV_NAMES.map((n, i) => <option key={n} value={i}>{n}</option>)}
          </select>
        </div>
        <div className="alarm-filterbar__group">
          <span className="alarm-filterbar__label">区域</span>
          <input className="alarm-filterbar__input" placeholder="过滤区域..." value={filterArea} onChange={e => setFilterArea(e.target.value)} />
        </div>
        <div className="alarm-filterbar__group">
          <span className="alarm-filterbar__label">开始</span>
          <input className="alarm-filterbar__input" type="date" value={dateFrom} onChange={e => setDateFrom(e.target.value)} />
        </div>
        <div className="alarm-filterbar__group">
          <span className="alarm-filterbar__label">结束</span>
          <input className="alarm-filterbar__input" type="date" value={dateTo} onChange={e => setDateTo(e.target.value)} />
        </div>
        <div className="alarm-filterbar__group" style={{ alignSelf: 'flex-end' }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 12, cursor: 'pointer' }}>
            <input type="checkbox" checked={showShelved} onChange={e => setShowShelved(e.target.checked)} />
            显示搁置
          </label>
        </div>
        <div className="alarm-filterbar__group" style={{ alignSelf: 'flex-end' }}>
          <button className="btn btn--sm btn--secondary" onClick={() => { setFilterText(''); setFilterSeverity('all'); setFilterArea(''); setDateFrom(''); setDateTo(''); setShowShelved(true) }}>重置</button>
        </div>
      </div>

      {/* ─── 报警表 ───────────────────────────────────── */}
      {tab !== 'rules' && (
        <div className="alarm-table-wrap">
          {filteredAlarms.length === 0 ? (
            <div className="alarm-empty">{tab === 'active' ? '✅ 无活动报警' : '📭 无报警历史'}</div>
          ) : (
            <table className="alarm-table">
              <thead>
                <tr>
                  <th onClick={() => toggleSort('status')}>状态{sortArrow('status')}</th>
                  <th onClick={() => toggleSort('timestamp')}>时间{sortArrow('timestamp')}</th>
                  <th onClick={() => toggleSort('severity')}>级别{sortArrow('severity')}</th>
                  <th onClick={() => toggleSort('variableName')}>变量{sortArrow('variableName')}</th>
                  <th>描述</th>
                  <th onClick={() => toggleSort('area')}>区域{sortArrow('area')}</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {filteredAlarms.map(a => (
                  <tr key={a.id} className={(a.isActive && !a.isAcknowledged && a.severity >= AlarmSeverity.Critical) ? 'alarm-row--flash' : ''}>
                    <td><span className={`status-pill ${statusClass(a)}`}>{statusTextFn(a)}</span></td>
                    <td className="alarm-table__time">{new Date(a.timestamp).toLocaleTimeString()}</td>
                    <td><span className={`severity-pill ${sevClass(a.severity)}`}>{SEV_NAMES[a.severity]}</span></td>
                    <td className="alarm-table__var">{a.variableName}</td>
                    <td style={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {a.description}
                      {a.currentValue !== undefined && (
                        <span style={{ marginLeft: 6, fontSize: 11, color: 'var(--muted-foreground)', fontFamily: 'var(--vt-font-mono)' }}>
                          ({a.currentValue})
                        </span>
                      )}
                    </td>
                    <td style={{ fontSize: 11 }}>{a.area}</td>
                    <td className="alarm-table__actions">
                      {/* 确认按钮 */}
                      <div style={{ position: 'relative', display: 'inline-block' }}>
                        <button className="btn btn--sm btn--primary"
                          disabled={a.isAcknowledged || !a.isActive}
                          onClick={() => { setFlyoutAlarmId(a.id); setFlyoutType('ack') }}>
                          确认
                        </button>
                        {flyoutAlarmId === a.id && flyoutType === 'ack' && (
                          <>
                            <div className="alarm-flyout-overlay" onClick={() => setFlyoutAlarmId(null)} />
                            <div className="alarm-flyout" style={{ top: '100%', left: 0, marginTop: 2 }}>
                              <div className="alarm-flyout__title">确认此报警？</div>
                              <div className="alarm-flyout__actions">
                                <button className="btn btn--sm" onClick={() => setFlyoutAlarmId(null)}>取消</button>
                                <button className="btn btn--sm btn--primary" onClick={() => handleAck(a.id)}>确定</button>
                              </div>
                            </div>
                          </>
                        )}
                      </div>
                      {/* 搁置/取消搁置 */}
                      <div style={{ position: 'relative', display: 'inline-block' }}>
                        {a.isShelved ? (
                          <button className="btn btn--sm btn--secondary" onClick={() => handleUnshelve(a.id)}>取消搁置</button>
                        ) : (
                          <button className="btn btn--sm btn--secondary"
                            disabled={!a.isActive}
                            onClick={() => { setFlyoutAlarmId(a.id); setFlyoutType('shelve') }}>
                            搁置
                          </button>
                        )}
                        {flyoutAlarmId === a.id && flyoutType === 'shelve' && (
                          <>
                            <div className="alarm-flyout-overlay" onClick={() => setFlyoutAlarmId(null)} />
                            <div className="alarm-flyout" style={{ top: '100%', left: 0, marginTop: 2 }}>
                              <div className="alarm-flyout__title">搁置时长</div>
                              <div className="alarm-flyout__actions" style={{ flexWrap: 'wrap' }}>
                                <button className="btn btn--sm" onClick={() => setFlyoutAlarmId(null)}>取消</button>
                                <button className="btn btn--sm btn--primary" onClick={() => handleShelve(a.id, 30 * 60 * 1000)}>30 分钟</button>
                                <button className="btn btn--sm btn--primary" onClick={() => handleShelve(a.id, 60 * 60 * 1000)}>1 小时</button>
                                <button className="btn btn--sm btn--primary" onClick={() => handleShelve(a.id, 8 * 60 * 60 * 1000)}>8 小时</button>
                                <button className="btn btn--sm btn--primary" onClick={() => handleShelve(a.id)}>永久</button>
                              </div>
                            </div>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* ─── 仅规则标签显示 ──────────────────────────── */}
      {tab === 'rules' && (
        <div className="alarm-rule-panel" style={{ marginTop: 0 }}>
          <table className="alarm-rule-table">
            <thead>
              <tr>
                <th>启用</th><th>变量</th><th>数据类型</th><th>级别</th><th>报警类型</th><th>阈值</th><th>触发延时</th><th>恢复延时</th><th>区域</th><th>操作</th>
              </tr>
            </thead>
            <tbody>
              {rules.map(r => (
                <tr key={r.variableKey}>
                  <td style={{ textAlign: 'center' }}>
                    <input type="checkbox" checked={r.isEnabled} onChange={async () => {
                      try {
                        await fetch(`/api/alarm/rules/${encodeURIComponent(r.variableKey)}`, {
                          method: 'PUT',
                          headers: { 'Content-Type': 'application/json' },
                          body: JSON.stringify({
                            variableKey: r.variableKey, dataType: r.dataType, description: r.description,
                            severity: r.severity, conditionType: r.conditionType, condition: r.condition,
                            threshold: r.threshold, deadband: r.deadband,
                            onDelayMs: r.onDelayMs, offDelayMs: r.offDelayMs,
                            area: r.area, isEnabled: !r.isEnabled,
                          }),
                        })
                        loadRules()
                      } catch {}
                    }} />
                  </td>
                  <td><span className="alarm-rule-table__dt">{r.dataType}</span></td>
                  <td><span className={`severity-pill ${sevClass(r.severity)}`}>{SEV_NAMES[r.severity]}</span></td>
                  <td style={{ color: 'var(--muted-foreground)' }}>{COND_NAMES[r.conditionType]}</td>
                  <td>
                    {r.threshold}
                    {r.deadband > 0 && <span style={{ marginLeft: 4, fontSize: 10, color: 'var(--muted-foreground)' }}>±{r.deadband}</span>}
                  </td>
                  <td style={{ fontSize: 11 }}>{r.onDelayMs > 0 ? `${r.onDelayMs}ms` : '-'}</td>
                  <td style={{ fontSize: 11 }}>{r.offDelayMs > 0 ? `${r.offDelayMs}ms` : '-'}</td>
                  <td style={{ fontSize: 11 }}>{r.area}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <button className="btn btn--sm btn--secondary" onClick={() => handleEditRule(r)} style={{ marginRight: 4 }}>编辑</button>
                    <button className="btn btn--sm btn--danger" onClick={() => handleDeleteRule(r.variableKey)}>删除</button>
                  </td>
                </tr>
              ))}
              {rules.length === 0 && (
                <tr><td colSpan={10} style={{ textAlign: 'center', padding: 20, color: 'var(--muted-foreground)' }}>暂无报警规则</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* ─── 状态条 ───────────────────────────────────── */}
      <div className="alarm-status-strip">
        <div className="alarm-status-strip__dot" style={{ background: 'var(--vt-color-info)' }} />
        <span>{statusText}</span>
        <span style={{ flex: 1 }} />
        <span style={{ fontSize: 11, color: 'var(--muted-foreground)' }}>{filteredAlarms.length} 条 / 共 {alarms.length} 条</span>
      </div>
    </CollapsibleSection>
  )
}

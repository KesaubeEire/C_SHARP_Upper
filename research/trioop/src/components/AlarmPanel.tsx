import { useState, useEffect, useCallback } from 'react'
import CollapsibleSection from './CollapsibleSection'

interface AlarmRule {
  name: string; variableName: string; condition: string; threshold: number; message: string; enabled: boolean
}
interface AlarmEvent {
  ruleName: string; variableName: string; message: string; value: number | boolean; threshold: number
  condition: string; active: boolean; triggeredAt: number; ackedAt?: number; recoveredAt?: number
}

export default function AlarmPanel() {
  const [rules, setRules] = useState<AlarmRule[]>([])
  const [active, setActive] = useState<AlarmEvent[]>([])
  const [history, setHistory] = useState<AlarmEvent[]>([])
  const [tab, setTab] = useState<'active' | 'rules' | 'history'>('active')
  const [showNew, setShowNew] = useState(false)
  const [form, setForm] = useState({ name: '', variableName: '', condition: 'eq', threshold: '1', message: '', enabled: true })

  const loadRules = useCallback(async () => {
    try { setRules(await (await fetch('/api/alarm/rules')).json()) } catch {}
  }, [])
  const loadActive = useCallback(async () => {
    try { setActive(await (await fetch('/api/alarm/active')).json()) } catch {}
  }, [])
  const loadHistory = useCallback(async () => {
    try { setHistory(await (await fetch('/api/alarm/history')).json()) } catch {}
  }, [])

  useEffect(() => { loadRules(); loadActive(); loadHistory() }, [loadRules, loadActive, loadHistory])
  useEffect(() => { const t = setInterval(loadActive, 2000); return () => clearInterval(t) }, [loadActive])

  const handleSave = async () => {
    await fetch('/api/alarm/rules', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...form, threshold: Number(form.threshold) }),
    })
    setShowNew(false); setForm({ name: '', variableName: '', condition: 'eq', threshold: '1', message: '', enabled: true })
    loadRules()
  }

  const handleDelete = async (name: string) => {
    await fetch(`/api/alarm/rules/${encodeURIComponent(name)}`, { method: 'DELETE' })
    loadRules()
  }

  const handleAck = async (name?: string) => {
    await fetch('/api/alarm/ack', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(name ? { name } : {}),
    })
    loadActive(); loadHistory()
  }

  const condLabel: Record<string, string> = { eq: '==', ne: '!=', gt: '>', lt: '<', ge: '>=', le: '<=' }

  return (
    <CollapsibleSection title="🔔 报警" storageKey="alarm" className="alarm-panel">
      <div className="alarm-tabs">
        <button className={`btn btn--sm ${tab === 'active' ? 'btn--primary' : ''}`} onClick={() => setTab('active')}>
          活动 {active.length > 0 && <span className="alarm-badge">{active.length}</span>}
        </button>
        <button className={`btn btn--sm ${tab === 'rules' ? 'btn--primary' : ''}`} onClick={() => setTab('rules')}>规则</button>
        <button className={`btn btn--sm ${tab === 'history' ? 'btn--primary' : ''}`} onClick={() => setTab('history')}>历史</button>
      </div>

      {tab === 'active' && (
        <div className="alarm-list">
          {active.length === 0 ? <div className="alarm-empty">✅ 无活动报警</div> : (
            <>
              <button className="btn btn--sm btn--ghost" style={{ marginBottom: 6 }} onClick={() => handleAck()}>全部确认</button>
              {active.map(a => (
                <div key={a.ruleName} className={`alarm-item ${a.ackedAt ? 'alarm-item--acked' : 'alarm-item--active'}`}>
                  <span className="alarm-item__msg">{a.message}</span>
                  <span className="alarm-item__val">值={String(a.value)}</span>
                  <span className="alarm-item__time">{new Date(a.triggeredAt).toLocaleTimeString()}</span>
                  {!a.ackedAt && <button className="btn btn--sm btn--primary" onClick={() => handleAck(a.ruleName)}>确认</button>}
                </div>
              ))}
            </>
          )}
        </div>
      )}

      {tab === 'rules' && (
        <div className="alarm-rules">
          <button className="btn btn--sm btn--primary" style={{ marginBottom: 6 }} onClick={() => setShowNew(!showNew)}>
            {showNew ? '取消' : '+ 新建规则'}
          </button>
          {showNew && (
            <div className="alarm-form">
              <input placeholder="规则名" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} />
              <input placeholder="变量名" value={form.variableName} onChange={e => setForm(f => ({ ...f, variableName: e.target.value }))} />
              <select value={form.condition} onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}>
                {Object.entries(condLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
              <input placeholder="阈值" type="number" value={form.threshold} onChange={e => setForm(f => ({ ...f, threshold: e.target.value }))} />
              <input placeholder="报警文本" value={form.message} onChange={e => setForm(f => ({ ...f, message: e.target.value }))} />
              <button className="btn btn--primary" onClick={handleSave}>保存</button>
            </div>
          )}
          {rules.map(r => (
            <div key={r.name} className="alarm-rule-item">
              <span className="alarm-rule__name">{r.name}</span>
              <span className="alarm-rule__cond">{r.variableName} {condLabel[r.condition] || r.condition} {r.threshold}</span>
              <span className="alarm-rule__msg">{r.message}</span>
              <button className="btn btn--danger btn--sm" onClick={() => handleDelete(r.name)}>✕</button>
            </div>
          ))}
          {rules.length === 0 && <div className="alarm-empty">暂无报警规则</div>}
        </div>
      )}

      {tab === 'history' && (
        <div className="alarm-list">
          {history.length === 0 ? <div className="alarm-empty">无报警历史</div> : (
            [...history].reverse().slice(0, 50).map((a, i) => (
              <div key={i} className={`alarm-item ${a.active ? 'alarm-item--active' : 'alarm-item--recovered'}`}>
                <span className="alarm-item__msg">{a.message}</span>
                <span className="alarm-item__val">值={String(a.value)}</span>
                <span className="alarm-item__time">{new Date(a.triggeredAt).toLocaleTimeString()}</span>
                {!a.active && <span className="alarm-item__recovered">已恢复</span>}
              </div>
            ))
          )}
        </div>
      )}
    </CollapsibleSection>
  )
}

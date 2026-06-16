import { useState, useEffect, useCallback, useRef } from 'react'
import { Gauge } from '@altara/core'
import type { AltaraDataSource } from '@altara/core'

type GadgetType = 'value' | 'lamp' | 'gauge'

interface Gadget {
  id: string
  type: GadgetType
  name: string
  variableName: string
  min?: number
  max?: number
  unit?: string
}

const STORAGE_KEY = 'trioop_dashboard'

function loadGadgets(): Gadget[] {
  try { const raw = localStorage.getItem(STORAGE_KEY); if (raw) return JSON.parse(raw) } catch {}
  return []
}

const COLORS = ['#42a5f5', '#ef5350', '#66bb6a', '#ffa726', '#ab47bc', '#26c6da', '#ec407a', '#8d6e63']

const EMPTY_FORM = { id: '', type: 'value' as GadgetType, name: '', variableName: '', min: 0, max: 100, unit: '' }

export default function Dashboard({ liveData }: { liveData?: Record<string, { value: number | boolean }> }) {
  const [gadgets, setGadgets] = useState<Gadget[]>(loadGadgets)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<Gadget>(EMPTY_FORM)

  useEffect(() => { localStorage.setItem(STORAGE_KEY, JSON.stringify(gadgets)) }, [gadgets])

  const openNew = useCallback(() => {
    setForm(EMPTY_FORM)
    setEditingId('__new__')
  }, [])

  const openEdit = useCallback((g: Gadget) => {
    setForm({ ...g })
    setEditingId(g.id)
  }, [])

  const closeModal = useCallback(() => {
    setEditingId(null)
    setForm(EMPTY_FORM)
  }, [])

  const saveGadget = useCallback(() => {
    if (!form.name || !form.variableName) return
    setGadgets(g => {
      if (editingId === '__new__') return [...g, { ...form, id: Date.now().toString() }]
      return g.map(x => x.id === editingId ? { ...form } : x)
    })
    closeModal()
  }, [form, editingId, closeModal])

  const removeGadget = useCallback((id: string) => {
    setGadgets(g => g.filter(x => x.id !== id))
  }, [])

  const modalOpen = editingId !== null

  return (
    <section className="section">
      <h2 className="section__title">🎛️ 仪表盘</h2>
      <div className="dashboard-bar">
        <button className="btn btn--sm btn--primary" onClick={openNew}>+ 添加组件</button>
      </div>

      {gadgets.length === 0 ? (
        <div className="db-empty">尚未添加组件</div>
      ) : (
        <div className="dashboard-grid">
          {gadgets.map((g, i) => (
            <div key={g.id} className="dashboard-card" style={{ borderTopColor: COLORS[i % COLORS.length] }}>
              <div className="dashboard-card__bar">
                <span className="dashboard-card__title">{g.name}</span>
                <div style={{ display: 'flex', gap: 4 }}>
                  <button className="btn btn--ghost btn--sm" onClick={() => openEdit(g)} title="编辑">✏️</button>
                  <button className="btn btn--danger btn--sm" onClick={() => removeGadget(g.id)}>✕</button>
                </div>
              </div>
              {renderGadget(g, liveData)}
            </div>
          ))}
        </div>
      )}

      {/* Modal 弹窗 */}
      {modalOpen && (
        <div className="modal-overlay" onClick={closeModal}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h3 className="modal-title">{editingId === '__new__' ? '添加组件' : '编辑组件'}</h3>
            <div className="modal-form">
              <label className="modal-label">类型</label>
              <select className="modal-input" value={form.type} onChange={e => setForm(f => ({ ...f, type: e.target.value as GadgetType }))}>
                <option value="value">数值</option>
                <option value="lamp">指示灯</option>
                <option value="gauge">表盘</option>
              </select>

              <label className="modal-label">显示名称</label>
              <input className="modal-input" placeholder="如 电机转速" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} />

              <label className="modal-label">变量名</label>
              <input className="modal-input" placeholder="如 VDF_频率" value={form.variableName} onChange={e => setForm(f => ({ ...f, variableName: e.target.value }))} />

              {form.type === 'gauge' && (
                <>
                  <label className="modal-label">量程范围</label>
                  <div style={{ display: 'flex', gap: 8 }}>
                    <input className="modal-input" type="number" placeholder="最小" value={form.min} onChange={e => setForm(f => ({ ...f, min: Number(e.target.value) }))} />
                    <input className="modal-input" type="number" placeholder="最大" value={form.max} onChange={e => setForm(f => ({ ...f, max: Number(e.target.value) }))} />
                  </div>
                </>
              )}

              <label className="modal-label">单位</label>
              <input className="modal-input" placeholder="如 rpm、℃、kW" value={form.unit} onChange={e => setForm(f => ({ ...f, unit: e.target.value }))} />

              <div className="modal-actions">
                <button className="btn btn--ghost" onClick={closeModal}>取消</button>
                <button className="btn btn--primary" onClick={saveGadget} disabled={!form.name || !form.variableName}>保存</button>
              </div>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}

function renderGadget(g: Gadget, liveData?: Record<string, { value: number | boolean }>) {
  const pt = liveData?.[g.variableName]
  const val = pt?.value
  const hasVal = val !== undefined && val !== null
  const numVal = typeof val === 'number' ? val : (val ? 1 : 0)
  const gMin = g.min ?? 0, gMax = g.max ?? 100

  switch (g.type) {
    case 'gauge':
      return (
        <div className="dashboard-gauge">
          <Gauge min={gMin} max={gMax} unit={g.unit} label={g.name}
            size="md" mockMode={!hasVal}
            thresholds={gMin < 0 ? [] : [
              { value: gMax * 0.8, color: '#ff9800' },
              { value: gMax * 0.9, color: '#ef5350' },
            ]}
          />
        </div>
      )
    case 'lamp':
      return (
        <div className={`dashboard-lamp ${hasVal && val ? 'dashboard-lamp--on' : ''}`}>
          <div className="dashboard-lamp__circle" />
          <span className="dashboard-lamp__val">{val !== undefined && val !== null ? (val ? 'ON' : 'OFF') : '--'}</span>
        </div>
      )
    default:
      return (
        <div className="dashboard-value">
          <span className="dashboard-value__num">{val !== undefined && val !== null ? (typeof val === 'number' ? numVal.toFixed(2) : (val ? 'ON' : 'OFF')) : '--'}</span>
          {g.unit && <span className="dashboard-value__unit">{g.unit}</span>}
        </div>
      )
  }
}

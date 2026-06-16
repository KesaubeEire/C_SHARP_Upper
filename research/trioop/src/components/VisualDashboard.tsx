import { useState, useEffect, useCallback } from 'react'
import { Gauge } from '@altara/core'
import { OEEDashboard, MotorDashboard, PredictiveMaintenanceGauge, AlarmAnnunciatorPanel, TrendRecorder, WaterfallSpectrogram, PIDTuningPanel } from '@altara/industrial'

type WidgetType = 'value' | 'lamp' | 'gauge' | 'trend' | 'oee' | 'motor' | 'predictive' | 'alarm' | 'pid' | 'spectrogram'

interface Widget {
  id: string
  type: WidgetType
  title: string
  variableName?: string
  min?: number
  max?: number
  unit?: string
  w: number
  h: number
}

const STORAGE_KEY = 'trioop_visual_dashboard'

function loadWidgets(): Widget[] {
  try { const raw = localStorage.getItem(STORAGE_KEY); if (raw) return JSON.parse(raw) } catch {}
  return []
}

const WIDGET_META: Record<WidgetType, { label: string; icon: string; defaultW: number; defaultH: number; hasVar: boolean }> = {
  value:       { label: '数值', icon: '🔢', defaultW: 1, defaultH: 1, hasVar: true },
  lamp:        { label: '指示灯', icon: '💡', defaultW: 1, defaultH: 1, hasVar: true },
  gauge:       { label: '表盘', icon: '📊', defaultW: 1, defaultH: 1, hasVar: true },
  trend:       { label: '趋势图', icon: '📈', defaultW: 2, defaultH: 1, hasVar: false },
  oee:         { label: 'OEE', icon: '🏭', defaultW: 2, defaultH: 1, hasVar: false },
  motor:       { label: '电机', icon: '⚡', defaultW: 2, defaultH: 1, hasVar: false },
  predictive:  { label: '预测维护', icon: '🔮', defaultW: 1, defaultH: 1, hasVar: false },
  alarm:       { label: '报警面板', icon: '🚨', defaultW: 2, defaultH: 1, hasVar: false },
  pid:         { label: 'PID调谐', icon: '🎛️', defaultW: 2, defaultH: 1, hasVar: false },
  spectrogram: { label: '频谱图', icon: '🌊', defaultW: 2, defaultH: 1, hasVar: false },
}

export default function VisualDashboard({ liveData }: { liveData?: Record<string, { value: number | boolean }> }) {
  const [widgets, setWidgets] = useState<Widget[]>(loadWidgets)
  const [showPalette, setShowPalette] = useState(false)
  const [editing, setEditing] = useState<string | null>(null)
  const [form, setForm] = useState<Widget>({ id: '', type: 'value', title: '', variableName: '', min: 0, max: 100, unit: '', w: 1, h: 1 })

  useEffect(() => { localStorage.setItem(STORAGE_KEY, JSON.stringify(widgets)) }, [widgets])

  const addWidget = useCallback((type: WidgetType) => {
    const meta = WIDGET_META[type]
    const id = Date.now().toString()
    setWidgets(w => [...w, { id, type, title: meta.label, variableName: '', w: meta.defaultW, h: meta.defaultH }])
    const w: Widget = { id, type, title: meta.label, variableName: '', w: meta.defaultW, h: meta.defaultH, min: 0, max: 100, unit: '' }
    setForm(w)
    setEditing(id)
    setShowPalette(false)
  }, [])

  const saveWidget = useCallback(() => {
    setWidgets(w => w.map(x => x.id === editing ? { ...form } : x))
    setEditing(null)
  }, [form, editing])

  const removeWidget = useCallback((id: string) => {
    setWidgets(w => w.filter(x => x.id !== id))
  }, [])

  return (
    <section className="section">
      <div className="section__title-row">
        <h2 className="section__title" style={{ margin: 0 }}>🎛️ 可视化仪表盘</h2>
        <button className="btn btn--sm btn--primary" onClick={() => setShowPalette(!showPalette)}>+ 添加组件</button>
        <span className="pfd-hint">{widgets.length} 个组件</span>
      </div>

      {showPalette && (
        <div className="vdb-palette">
          {Object.entries(WIDGET_META).map(([type, meta]) => (
            <button key={type} className="vdb-palette__item" onClick={() => addWidget(type as WidgetType)}>
              <span className="vdb-palette__icon">{meta.icon}</span>
              <span className="vdb-palette__label">{meta.label}</span>
            </button>
          ))}
        </div>
      )}

      {widgets.length === 0 ? (
        <div className="db-empty">点击「+ 添加组件」开始构建仪表盘</div>
      ) : (
        <div className="vdb-grid">
          {widgets.map(w => (
            <div key={w.id} className="vdb-widget" style={{ gridColumn: `span ${w.w}`, gridRow: `span ${w.h}` }}>
              <div className="vdb-widget__bar">
                <span className="vdb-widget__title">{WIDGET_META[w.type]?.icon} {w.title}</span>
                <div className="vdb-widget__actions">
                  <button className="btn btn--ghost btn--sm" onClick={() => { setForm({ ...w }); setEditing(w.id) }}>✏️</button>
                  <button className="btn btn--danger btn--sm" onClick={() => removeWidget(w.id)}>✕</button>
                </div>
              </div>
              <div className="vdb-widget__body">
                <WidgetRenderer widget={w} liveData={liveData} />
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Config Modal */}
      {editing && (
        <div className="modal-overlay" onClick={() => setEditing(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ width: 480 }}>
            <h3 className="modal-title">✏️ {form.title} 设置</h3>
            <div className="modal-form">
              <label className="modal-label">标题</label>
              <input className="modal-input" value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))} />

              {WIDGET_META[form.type]?.hasVar && (
                <>
                  <label className="modal-label">变量名</label>
                  <input className="modal-input" placeholder="如 VDF_频率" value={form.variableName || ''} onChange={e => setForm(f => ({ ...f, variableName: e.target.value }))} />
                  <label className="modal-label">单位</label>
                  <input className="modal-input" placeholder="如 rpm" value={form.unit || ''} onChange={e => setForm(f => ({ ...f, unit: e.target.value }))} />
                  {form.type === 'gauge' && (
                    <>
                      <label className="modal-label">量程</label>
                      <div style={{ display: 'flex', gap: 8 }}>
                        <input className="modal-input" type="number" placeholder="最小" value={form.min ?? 0} onChange={e => setForm(f => ({ ...f, min: Number(e.target.value) }))} />
                        <input className="modal-input" type="number" placeholder="最大" value={form.max ?? 100} onChange={e => setForm(f => ({ ...f, max: Number(e.target.value) }))} />
                      </div>
                    </>
                  )}
                </>
              )}

              <div className="modal-actions">
                <button className="btn btn--ghost" onClick={() => setEditing(null)}>取消</button>
                <button className="btn btn--primary" onClick={saveWidget}>保存</button>
              </div>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}

function WidgetRenderer({ widget, liveData }: { widget: Widget; liveData?: Record<string, { value: number | boolean }> }) {
  const pt = liveData?.[widget.variableName || '']
  const val = pt?.value
  const numVal = typeof val === 'number' ? val : (val ? 1 : 0)

  switch (widget.type) {
    case 'gauge':
      return <Gauge min={widget.min ?? 0} max={widget.max ?? 100} unit={widget.unit} label="" size="md" mockMode={!widget.variableName} />
    case 'oee':
      return <OEEDashboard mockMode shift="A1" />
    case 'motor':
      return <MotorDashboard mockMode ratedRPM={3000} ratedCurrent={50} />
    case 'predictive':
      return <PredictiveMaintenanceGauge mockMode size="md" />
    case 'alarm':
      return <AlarmAnnunciatorPanel mockMode columns={4} />
    case 'trend':
      return <TrendRecorder mockMode showLegend timeScale="5m" />
    case 'pid':
      return <PIDTuningPanel mockMode />
    case 'spectrogram':
      return <WaterfallSpectrogram mockMode width={400} height={240} />
    case 'lamp': {
      const on = val !== undefined && val !== null && !!val
      return (
        <div className="vdb-lamp" style={{ justifyContent: 'center', display: 'flex', gap: 12, alignItems: 'center', padding: 12 }}>
          <div className="vdb-lamp__dot" style={{ background: on ? '#4caf50' : '#333', boxShadow: on ? '0 0 12px #4caf50' : 'none', width: 32, height: 32, borderRadius: '50%', transition: 'all 0.2s' }} />
          <span style={{ fontSize: 18, fontWeight: 600, color: on ? '#4caf50' : 'var(--text-muted)' }}>{on ? 'ON' : 'OFF'}</span>
        </div>
      )
    }
    default:
      return (
        <div style={{ textAlign: 'center', padding: 16 }}>
          <div style={{ fontSize: 28, fontWeight: 600, fontFamily: 'monospace' }}>
            {val !== undefined && val !== null ? (typeof val === 'number' ? numVal.toFixed(2) : (val ? 'ON' : 'OFF')) : '--'}
          </div>
          {widget.unit && <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>{widget.unit}</div>}
        </div>
      )
  }
}

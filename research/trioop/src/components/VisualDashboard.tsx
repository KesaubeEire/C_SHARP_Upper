// @ts-nocheck
import { useState, useEffect, useCallback, useRef } from 'react'
import CollapsibleSection from './CollapsibleSection'
import { Responsive } from 'react-grid-layout'
import 'react-grid-layout/css/styles.css'
import { Gauge, SignalPanel, EventLog } from '@altara/core'
import { OEEDashboard, MotorDashboard, PredictiveMaintenanceGauge, AlarmAnnunciatorPanel, TrendRecorder, PIDTuningPanel } from '@altara/industrial'
import { useContextMenu } from './VDBContextMenu'
import { resolveVarName, loadMapping, writePLC } from '../hooks/useDBMapping'


type WidgetType = 'value' | 'lamp' | 'button' | 'gauge' | 'trend' | 'oee' | 'motor' | 'predictive' | 'alarm' | 'pid' | 'signal' | 'eventlog'

interface Widget { id: string; type: WidgetType; title: string; config: Record<string, any> }

const STORAGE_KEY = 'trioop_vdb_v4'

function loadData(): { widgets: Widget[]; layouts: Record<string, any[]> } {
  try { const raw = localStorage.getItem(STORAGE_KEY); if (raw) return JSON.parse(raw) } catch {}
  return { widgets: [], layouts: { lg: [] } }
}

const WIDGET_META: Record<WidgetType, { label: string; icon: string; w: number; h: number }> = {
  value:      { label: '数值', icon: '🔢', w: 2, h: 2 },
  lamp:       { label: '指示灯', icon: '💡', w: 2, h: 2 },
  button:     { label: '按钮', icon: '🔘', w: 2, h: 2 },
  gauge:      { label: '表盘', icon: '🎯', w: 3, h: 3 },
  trend:      { label: '趋势图', icon: '📈', w: 4, h: 3 },
  oee:        { label: 'OEE', icon: '🏭', w: 4, h: 3 },
  motor:      { label: '电机', icon: '⚡', w: 4, h: 3 },
  predictive: { label: '预测维护', icon: '🔮', w: 3, h: 3 },
  alarm:      { label: '报警面板', icon: '🚨', w: 4, h: 3 },
  pid:        { label: 'PID调谐', icon: '🎛️', w: 4, h: 3 },
  signal:     { label: '信号面板', icon: '📡', w: 3, h: 3 },
  eventlog:   { label: '事件日志', icon: '📋', w: 4, h: 3 },
}

const BREAKPOINTS = { lg: 1200, md: 996, sm: 768, xs: 480, xxs: 0 }
const COLS: Record<string, number> = { lg: 12, md: 10, sm: 6, xs: 4, xxs: 2 }
const ROW_HEIGHT_KEY = 'trioop_vdb_rowh'

interface FieldDef { key: string; label: string; type: 'number' | 'text' | 'select' | 'boolean'; default: any; options?: { label: string; value: any }[] }

const CONFIG_FIELDS: Record<WidgetType, FieldDef[]> = {
  value:      [{ key:'variableName', label:'变量名', type:'text', default:'' }, { key:'unit', label:'单位', type:'text', default:'' }],
  lamp:       [{ key:'variableName', label:'变量名', type:'text', default:'' }],
  button:     [{ key:'variableName', label:'变量名', type:'text', default:'' }, { key:'mode', label:'按钮模式', type:'select', default:'momentary', options:[{ label:'按1松0', value:'momentary' },{ label:'按0松1', value:'momentary_off' },{ label:'取反', value:'toggle' }] }, { key:'label', label:'按钮文字', type:'text', default:'运行' }],
  gauge:      [{ key:'variableName', label:'变量名', type:'text', default:'' }, { key:'min', label:'量程下限', type:'number', default:0 }, { key:'max', label:'量程上限', type:'number', default:100 }, { key:'unit', label:'单位', type:'text', default:'%' }],
  trend:      [{ key:'timeScale', label:'时间刻度', type:'select', default:'5m', options:[{ label:'1分钟', value:'1m' },{ label:'5分钟', value:'5m' },{ label:'15分钟', value:'15m' },{ label:'1小时', value:'1h' },{ label:'4小时', value:'4h' },{ label:'8小时', value:'8h' },{ label:'24小时', value:'24h' }] }, { key:'showGrid', label:'网格', type:'boolean', default:true }, { key:'showLegend', label:'图例', type:'boolean', default:true }],
  oee:        [{ key:'availability', label:'可用率', type:'number', default:0.85 }, { key:'performance', label:'性能率', type:'number', default:0.78 }, { key:'quality', label:'质量率', type:'number', default:0.95 }, { key:'shift', label:'班次', type:'text', default:'A' }],
  motor:      [{ key:'rpm', label:'转速', type:'number', default:2850 }, { key:'torque', label:'扭矩', type:'number', default:42 }, { key:'current', label:'电流', type:'number', default:38 }, { key:'temperature', label:'温度', type:'number', default:72 }],
  predictive: [{ key:'healthScore', label:'健康指数', type:'number', default:74 }, { key:'rulDays', label:'剩余寿命', type:'number', default:45 }],
  alarm:      [{ key:'columns', label:'列数', type:'number', default:4 }, { key:'flashRate', label:'闪烁Hz', type:'number', default:2 }, { key:'groupBy', label:'分组', type:'text', default:'' }],
  pid:        [{ key:'kp', label:'Kp', type:'number', default:2.5 }, { key:'ki', label:'Ki', type:'number', default:0.8 }, { key:'kd', label:'Kd', type:'number', default:0.3 }],
  signal:     [{ key:'ch1Label', label:'通道1', type:'text', default:'电流' }, { key:'ch1Val', label:'通道1值', type:'number', default:38 }, { key:'ch1Unit', label:'通道1单位', type:'text', default:'A' }, { key:'ch2Label', label:'通道2', type:'text', default:'温度' }, { key:'ch2Val', label:'通道2值', type:'number', default:72 }, { key:'ch2Unit', label:'通道2单位', type:'text', default:'°C' }],
  eventlog:   [],
}

function renderWidget(type: WidgetType, cfg: Record<string, any>, liveData?: Record<string, { value: number | boolean }>) {
  const pt = liveData?.[cfg.variableName || '']
  const liveVal = pt?.value; const liveNum = typeof liveVal === 'number' ? liveVal : (liveVal ? 1 : 0); const hasLive = liveVal !== undefined && liveVal !== null
  switch (type) {
    case 'gauge': { const ds = hasLive && cfg.variableName ? { subscribe: (cb: any) => { cb({ timestamp: Date.now(), value: liveNum }); return () => {} }, getHistory: () => [{ timestamp: Date.now(), value: liveNum }], status: 'connected' as const, destroy: () => {} } : undefined; return <Gauge min={cfg.min ?? 0} max={cfg.max ?? 100} unit={cfg.unit} label="" size="md" dataSource={ds} mockMode={!ds} /> }
    case 'trend': return <TrendRecorder timeScale={cfg.timeScale || '5m'} showGrid={cfg.showGrid !== false} showLegend={cfg.showLegend !== false} mockMode />
    case 'oee': return <OEEDashboard availability={cfg.availability ?? 0.85} performance={cfg.performance ?? 0.78} quality={cfg.quality ?? 0.95} shift={cfg.shift || 'A'} mockMode />
    case 'motor': return <MotorDashboard rpm={cfg.rpm ?? 2850} torque={cfg.torque ?? 42} current={cfg.current ?? 38} temperature={cfg.temperature ?? 72} mockMode />
    case 'predictive': return <PredictiveMaintenanceGauge healthScore={cfg.healthScore ?? 74} rulDays={cfg.rulDays ?? 45} size="md" mockMode />
    case 'alarm': return <AlarmAnnunciatorPanel columns={cfg.columns ?? 4} flashRate={cfg.flashRate ?? 2} groupBy={cfg.groupBy || undefined} mockMode />
    case 'pid': return <PIDTuningPanel kp={cfg.kp ?? 2.5} ki={cfg.ki ?? 0.8} kd={cfg.kd ?? 0.3} mockMode />
    case 'signal': return <SignalPanel signals={[{ key:'ch1', label:cfg.ch1Label||'CH1', value:cfg.ch1Val, unit:cfg.ch1Unit },{ key:'ch2', label:cfg.ch2Label||'CH2', value:cfg.ch2Val, unit:cfg.ch2Unit }] as any} />
    case 'eventlog': return <EventLog entries={[{ timestamp: Date.now()-5000, message:'系统启动', severity:'info' },{ timestamp: Date.now()-3000, message:'温度警告', severity:'warn' },{ timestamp: Date.now()-1000, message:'通信超时', severity:'error' }]} maxEntries={100} />
    case 'button': return (<div style={{display:'flex',justifyContent:'center',alignItems:'center',height:'100%'}}><button className="btn btn--primary" style={{padding:'12px 32px',fontSize:16,fontWeight:600}}
      onMouseDown={()=>{if(!cfg.variableName)return;writePLC(cfg.variableName,cfg.mode==='momentary_off'?0:1).catch(()=>{})}}
      onMouseUp={()=>{if(!cfg.variableName||cfg.mode==='toggle')return;writePLC(cfg.variableName,cfg.mode==='momentary_off'?1:0).catch(()=>{})}}>{cfg.label||'按钮'}</button></div>)
    case 'lamp': return (<div style={{ display:'flex', justifyContent:'center', alignItems:'center', gap:12, height:'100%' }}><div style={{ width:32, height:32, borderRadius:'50%', background:hasLive&&liveVal?'#4caf50':'#333', boxShadow:hasLive&&liveVal?'0 0 16px #4caf50':'none', transition:'all 0.2s' }} /><span style={{ fontSize:18, fontWeight:600, color:hasLive&&liveVal?'#4caf50':'var(--text-muted)' }}>{hasLive?(liveVal?'ON':'OFF'):'--'}</span></div>)
    default: return (<div style={{ textAlign:'center', padding:16 }}><div style={{ fontSize:28, fontWeight:600, fontFamily:'monospace' }}>{hasLive?(typeof liveVal==='number'?liveNum.toFixed(2):(liveVal?'ON':'OFF')):'--'}</div>{cfg.unit && <div style={{ fontSize:13, color:'var(--text-muted)' }}>{cfg.unit}</div>}</div>)
  }
}

export default function VisualDashboard({ liveData }: { liveData?: Record<string, { value: number | boolean }> }) {
  const [data, setData] = useState(() => loadData())
  const [showPalette, setShowPalette] = useState(false)
  const [editing, setEditing] = useState<string | null>(null)
  const [formTitle, setFormTitle] = useState(''); const [formCfg, setFormCfg] = useState<Record<string, any>>({}); const [formType, setFormType] = useState<WidgetType>('value')
  const [rowH, setRowH] = useState(() => { try { return Number(localStorage.getItem(ROW_HEIGHT_KEY)) || 120 } catch { return 120 } })
  const paletteRef = useRef<HTMLDivElement>(null)

  const containerRef = useRef<HTMLDivElement>(null); const [containerWidth, setContainerWidth] = useState(0)
  useEffect(() => { localStorage.setItem(STORAGE_KEY, JSON.stringify(data)) }, [data])
  useEffect(() => { localStorage.setItem(ROW_HEIGHT_KEY, String(rowH)) }, [rowH])
  useEffect(() => { const el = containerRef.current; if (!el) return; const ro = new ResizeObserver(entries => { for (const e of entries) setContainerWidth(e.contentRect.width) }); ro.observe(el); return () => ro.disconnect() }, [])

  const addWidget = useCallback((type: WidgetType) => {
    const meta = WIDGET_META[type]; const id = `w${Date.now()}`
    const cfg: Record<string, any> = {}; for (const f of CONFIG_FIELDS[type] || []) cfg[f.key] = f.default
    const layout = data.layouts.lg || []
    const maxY = layout.reduce((m: number, l: any) => Math.max(m, l.y + l.h), 0)
    const newLayout = { i: id, x: 0, y: maxY, w: meta.w, h: meta.h }
    setData(d => ({ widgets: [...d.widgets, { id, type, title: meta.label, config: cfg }], layouts: { ...d.layouts, lg: [...layout, newLayout] } }))
    setFormTitle(meta.label); setFormType(type); setFormCfg({ ...cfg }); setEditing(id); setShowPalette(false)
  }, [data.layouts])

  // 点击外部 / Escape → 关闭 palette dropdown
  useEffect(() => {
    if (!showPalette) return
    const handler = (e: MouseEvent) => {
      if (paletteRef.current && !paletteRef.current.contains(e.target as Node)) setShowPalette(false)
    }
    const keyHandler = (e: KeyboardEvent) => { if (e.key === 'Escape') setShowPalette(false) }
    const t = setTimeout(() => document.addEventListener('mousedown', handler), 0)
    document.addEventListener('keydown', keyHandler)
    return () => { clearTimeout(t); document.removeEventListener('mousedown', handler); document.removeEventListener('keydown', keyHandler) }
  }, [showPalette])

  const onLayoutChange = useCallback((layout: any, allLayouts: any) => {
    setData(d => ({ ...d, layouts: allLayouts }))
  }, [])

  const openEdit = useCallback((w: Widget) => { setFormTitle(w.title); setFormType(w.type); setFormCfg({ ...w.config }); setEditing(w.id) }, [])
  const saveWidget = useCallback(() => { setData(d => ({ ...d, widgets: d.widgets.map(w => w.id === editing ? { ...w, title: formTitle, config: { ...formCfg } } : w) })); setEditing(null) }, [formTitle, formCfg, editing])
  const removeWidget = useCallback((id: string) => { setData(d => ({ widgets: d.widgets.filter(w => w.id !== id), layouts: Object.fromEntries(Object.entries(d.layouts).map(([k, v]) => [k, (v as any[]).filter(l => l.i !== id)])) })) }, [])

  const fields = CONFIG_FIELDS[formType] || []
  const ctx = useContextMenu()
  const [importedDBs, setImportedDBs] = useState<{ dbNumber: number; dbName: string }[]>([])
  useEffect(() => {
    fetch('/api/plc/imported-dbs').then(r => r.json()).then((dbs: any[]) => {
      const m = loadMapping()
      for (const db of dbs) { if (m[db.dbName] !== undefined) db.dbNumber = m[db.dbName] }
      setImportedDBs(dbs)
    }).catch(() => {})
  }, [editing])

  return (
    <CollapsibleSection title="🎛️ 可视化仪表盘" storageKey="visual-dashboard" keepMounted
      actions={<><span style={{ whiteSpace: 'nowrap', fontSize: 12, color: 'var(--text-muted)' }}>{data.widgets.length} 组件</span>
        <div style={{ position: 'relative' }} ref={paletteRef}>
          <button className="btn btn--sm btn--primary" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }} onClick={() => setShowPalette(p => !p)}>
            <span>+ 添加</span>
            <svg className={`dropdown__arrow ${showPalette ? 'dropdown__arrow--open' : ''}`} width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="m6 9 6 6 6-6" />
            </svg>
          </button>
          {showPalette && (
            <div className="vdb-palette-dropdown">
              {Object.entries(WIDGET_META).map(([type, meta]) => (
                <button key={type} className="vdb-palette-dropdown__item" onClick={() => addWidget(type as WidgetType)}>
                  <span className="vdb-palette-dropdown__icon">{meta.icon}</span>
                  <span className="vdb-palette-dropdown__label">{meta.label}</span>
                </button>
              ))}
            </div>
          )}
        </div>
        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>行高</span>
          <input type="range" min={80} max={300} step={10} value={rowH} onChange={e => setRowH(Number(e.target.value))} style={{ width: 80, accentColor: '#2196f3' }} />
          <span style={{ fontSize: 11, color: 'var(--text-muted)', minWidth: 36 }}>{rowH}px</span>
        </div></>}
    >

      {data.widgets.length === 0 ? (
        <div className="db-empty" style={{ textAlign: 'center', padding: 40 }}>点击「+ 添加组件」开始构建仪表盘</div>
      ) : (
        <div className="vdb-rgl-wrapper">
          <div ref={containerRef} style={{ width: "100%" }}><Responsive width={containerWidth}
            className="vdb-rgl"
            layouts={data.layouts}
            breakpoints={BREAKPOINTS}
            cols={COLS}
            rowHeight={rowH}
            onLayoutChange={onLayoutChange}
            draggableHandle=".vdb-widget__bar"
            isDraggable
            isResizable
            compactType="vertical"
            preventCollision={false}
            margin={[10, 10]}
            containerPadding={[0, 0]}
            useCSSTransforms
          >
            {data.widgets.map(w => (
              <div key={w.id} className="vdb-widget" onContextMenu={e => ctx.show(e, [
                { label: '编辑', icon: '✏️', action: () => openEdit(w) },
                { label: '删除', icon: '✕', action: () => removeWidget(w.id), danger: true },
              ])}>
                <div className="vdb-widget__bar">
                  <span className="vdb-widget__title">{WIDGET_META[w.type]?.icon} {w.title}</span>
                  <div className="vdb-widget__actions">
                    <button className="btn btn--ghost btn--sm" onClick={() => openEdit(w)}>✏️</button>
                    <button className="btn btn--destructive btn--sm" onClick={() => removeWidget(w.id)}>✕</button>
                  </div>
                </div>
                <div className="vdb-widget__body">{renderWidget(w.type, w.config, liveData)}</div>
              </div>
            ))}
          </Responsive></div>
        </div>
      )}

      {editing && <EscapeHandler onEscape={() => setEditing(null)} />}
      {ctx.menu}
      {editing && (
        <div className="modal-overlay" onMouseDown={e => { if (e.target === e.currentTarget) setEditing(null) }}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ width: 480 }}>
            <h3 className="modal-title">✏️ 设置</h3>
            <div className="modal-form">
              <label className="modal-label">标题</label>
              <input className="modal-input" value={formTitle} onChange={e => setFormTitle(e.target.value)} placeholder="自定义标题" />

              {fields.some(f => f.key === 'variableName') && (
                <div>
                  <label className="modal-label">变量</label>
                  <div style={{ display:'flex', gap:8 }}>
                    <select className="modal-input" style={{ width:100, flexShrink:0 }} value={(formCfg.variableName||'').split(':')[0]||''}
                      onChange={e => { const dbName=e.target.value; const rest=(formCfg.variableName||'').split(':').slice(1).join(':'); setFormCfg(c => ({...c, variableName: dbName?`${dbName}:${rest}`:rest })) }}>
                      <option value="">--</option>
                      {importedDBs.map(d => <option key={d.dbName} value={d.dbName}>{d.dbName}</option>)}
                    </select>
                    <input className="modal-input" placeholder="变量名" value={(formCfg.variableName||'').split(':').slice(1).join(':')}
                      onChange={e => { const dbName=(formCfg.variableName||'').split(':')[0]||''; setFormCfg(c => ({...c, variableName: dbName?`${dbName}:${e.target.value}`:e.target.value })) }} />
                  </div>
                </div>
              )}

              {fields.some(f => f.key === 'min') && (
                <div>
                  <label className="modal-label">量程</label>
                  <div style={{ display:'flex', gap:8 }}>
                    <input className="modal-input" type="number" placeholder="最小" value={formCfg.min??0} onChange={e => setFormCfg(c => ({...c, min:Number(e.target.value)}))} />
                    <input className="modal-input" type="number" placeholder="最大" value={formCfg.max??100} onChange={e => setFormCfg(c => ({...c, max:Number(e.target.value)}))} />
                  </div>
                </div>
              )}

              {fields.map(f => {
                if (f.key === 'variableName' || f.key === 'min' || f.key === 'max') return null
                return (<div key={f.key}>
                  {f.type === 'boolean' ? (<label className="modal-label" style={{ display:'flex', alignItems:'center', gap:8, cursor:'pointer' }}><input type="checkbox" checked={!!formCfg[f.key]} onChange={e => setFormCfg(c => ({...c, [f.key]:e.target.checked}))} />{f.label}</label>)
                  : f.type === 'select' ? (<><label className="modal-label">{f.label}</label><select className="modal-input" value={formCfg[f.key]??f.default} onChange={e => setFormCfg(c => ({...c, [f.key]:e.target.value}))}>{f.options?.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}</select></>)
                  : (<><label className="modal-label">{f.label}</label><input className="modal-input" type={f.type==='number'?'number':'text'} value={formCfg[f.key]??''} onChange={e => setFormCfg(c => ({...c, [f.key]:f.type==='number'?Number(e.target.value):e.target.value}))} /></>)}
                </div>)})}
              <div className="modal-actions"><button className="btn btn--ghost" onClick={() => setEditing(null)}>取消</button><button className="btn btn--primary" onClick={saveWidget}>保存</button></div>
            </div>
          </div>
        </div>
      )}
    </CollapsibleSection>
  )
}

function EscapeHandler({ onEscape }: { onEscape: () => void }) {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onEscape() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [onEscape])
  return null
}

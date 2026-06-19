// @ts-nocheck
import React, { useState, useEffect, useCallback, useRef, useMemo } from 'react'
import CollapsibleSection from './CollapsibleSection'
import { Responsive } from 'react-grid-layout'
import 'react-grid-layout/css/styles.css'
import { SignalPanel, EventLog, ConnectionBar } from '@altara/core'
import { AltaraGauge } from '../components/AltaraGauge'
import { OEEDashboard, MotorDashboard, PredictiveMaintenanceGauge, AlarmAnnunciatorPanel, PIDTuningPanel, ProcessFlowDiagram } from '@altara/industrial'
import { TrendRecorder } from '../components/AltaraTrendRecorder'
import { useContextMenu } from './VDBContextMenu'
import { resolveVarName, loadMapping, loadAllDBData, writePLC } from '../hooks/useDBMapping'


type WidgetType = 'value' | 'lamp' | 'button' | 'gauge' | 'trend' | 'oee' | 'motor' | 'predictive' | 'alarm' | 'pid' | 'signal' | 'eventlog' | 'connectionbar' | 'process'

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
  connectionbar: { label: '连接状态', icon: '🔌', w: 3, h: 1 },
  process:    { label: '工艺流程图', icon: '🏗️', w: 5, h: 4 },
}

const BREAKPOINTS = { lg: 1200, md: 996, sm: 768, xs: 480, xxs: 0 }
const COLS: Record<string, number> = { lg: 12, md: 10, sm: 6, xs: 4, xxs: 2 }
const ROW_HEIGHT_KEY = 'trioop_vdb_rowh'

interface FieldDef { key: string; label: string; type: 'number' | 'text' | 'select' | 'boolean'; default: any; options?: { label: string; value: any }[] }

const CONFIG_FIELDS: Record<WidgetType, FieldDef[]> = {
  value:      [{ key:'variableName', label:'变量名', type:'text', default:'' }, { key:'unit', label:'单位', type:'text', default:'' }],
  lamp:       [{ key:'variableName', label:'变量名', type:'text', default:'' }],
  button:     [{ key:'variableName', label:'变量名', type:'text', default:'' }, { key:'mode', label:'按钮模式', type:'select', default:'momentary', options:[{ label:'按1松0', value:'momentary' },{ label:'按0松1', value:'momentary_off' },{ label:'取反', value:'toggle' }] }, { key:'label', label:'按钮文字', type:'text', default:'运行' }],
  gauge:      [{ key:'variableName', label:'变量名', type:'text', default:'' }, { key:'min', label:'量程下限', type:'number', default:0 }, { key:'max', label:'量程上限', type:'number', default:100 }, { key:'unit', label:'单位', type:'text', default:'%' }, { key:'threshold1', label:'预警值', type:'number', default:80 }, { key:'threshold2', label:'危险值', type:'number', default:90 }, { key:'easingMs', label:'缓动时长(ms)', type:'number', default:500 }],
  trend:      [
    { key:'timeScale', label:'时间刻度', type:'select', default:'5m', options:[{ label:'1分钟', value:'1m' },{ label:'5分钟', value:'5m' },{ label:'15分钟', value:'15m' },{ label:'1小时', value:'1h' },{ label:'4小时', value:'4h' },{ label:'8小时', value:'8h' },{ label:'24小时', value:'24h' }] },
    { key:'mockMode', label:'🎲 演示模式', type:'boolean', default:true },
    { key:'showGrid', label:'网格线', type:'boolean', default:true },
    { key:'showLegend', label:'图例', type:'boolean', default:true },
    { key:'showPoints', label:'采样标记', type:'boolean', default:false },
    { key:'lineWidth', label:'线宽', type:'number', default:1.5 },
    { key:'backgroundColor', label:'背景色', type:'text', default:'' },
    { key:'yAxisLabel', label:'Y轴标签', type:'text', default:'' },
    { key:'ch1En', label:'', type:'hidden', default:true }, { key:'ch1Label', label:'', type:'hidden', default:'CH1' }, { key:'ch1Color', label:'', type:'hidden', default:'#E24B4A' }, { key:'ch1Min', label:'', type:'hidden', default:0 }, { key:'ch1Max', label:'', type:'hidden', default:100 }, { key:'ch1Unit', label:'', type:'hidden', default:'' },
    { key:'ch2En', label:'', type:'hidden', default:true }, { key:'ch2Label', label:'', type:'hidden', default:'CH2' }, { key:'ch2Color', label:'', type:'hidden', default:'#37D3E0' }, { key:'ch2Min', label:'', type:'hidden', default:0 }, { key:'ch2Max', label:'', type:'hidden', default:16 }, { key:'ch2Unit', label:'', type:'hidden', default:'' },
    { key:'ch3En', label:'', type:'hidden', default:true }, { key:'ch3Label', label:'', type:'hidden', default:'CH3' }, { key:'ch3Color', label:'', type:'hidden', default:'#1D9E75' }, { key:'ch3Min', label:'', type:'hidden', default:0 }, { key:'ch3Max', label:'', type:'hidden', default:50 }, { key:'ch3Unit', label:'', type:'hidden', default:'' },
    { key:'ch4En', label:'', type:'hidden', default:true }, { key:'ch4Label', label:'', type:'hidden', default:'CH4' }, { key:'ch4Color', label:'', type:'hidden', default:'#F4D03F' }, { key:'ch4Min', label:'', type:'hidden', default:0 }, { key:'ch4Max', label:'', type:'hidden', default:100 }, { key:'ch4Unit', label:'', type:'hidden', default:'' },
  ],
  oee:        [{ key:'availability', label:'可用率', type:'number', default:0.85 }, { key:'performance', label:'性能率', type:'number', default:0.78 }, { key:'quality', label:'质量率', type:'number', default:0.95 }, { key:'shift', label:'班次', type:'text', default:'A' }],
  motor:      [{ key:'rpm', label:'转速', type:'number', default:2850 }, { key:'torque', label:'扭矩', type:'number', default:42 }, { key:'current', label:'电流', type:'number', default:38 }, { key:'temperature', label:'温度', type:'number', default:72 }],
  predictive: [{ key:'healthScore', label:'健康指数', type:'number', default:74 }, { key:'rulDays', label:'剩余寿命', type:'number', default:45 }],
  alarm:      [{ key:'columns', label:'列数', type:'number', default:4 }, { key:'flashRate', label:'闪烁Hz', type:'number', default:2 }, { key:'groupBy', label:'分组', type:'text', default:'' }],
  pid:        [{ key:'kp', label:'Kp', type:'number', default:2.5 }, { key:'ki', label:'Ki', type:'number', default:0.8 }, { key:'kd', label:'Kd', type:'number', default:0.3 }],
  signal:     [{ key:'ch1Label', label:'通道1', type:'text', default:'电流' }, { key:'ch1Val', label:'通道1值', type:'number', default:38 }, { key:'ch1Unit', label:'通道1单位', type:'text', default:'A' }, { key:'ch2Label', label:'通道2', type:'text', default:'温度' }, { key:'ch2Val', label:'通道2值', type:'number', default:72 }, { key:'ch2Unit', label:'通道2单位', type:'text', default:'°C' }],
  eventlog:   [],
  connectionbar: [{ key:'title', label:'连接地址', type:'text', default:'PLC-1200' }, { key:'status', label:'状态', type:'select', default:'connected', options:[{ label:'已连接', value:'connected' },{ label:'未连接', value:'disconnected' }] }],
  process:    [{ key:'mockMode', label:'🎲 演示模式', type:'boolean', default:true }],
}

function renderWidget(type: WidgetType, cfg: Record<string, any>, liveData?: Record<string, { value: number | boolean }>) {
  const pt = liveData?.[cfg.variableName || '']
  const liveVal = pt?.value; const liveNum = typeof liveVal === 'number' ? liveVal : (liveVal ? 1 : 0); const hasLive = liveVal !== undefined && liveVal !== null
  switch (type) {
    case 'gauge': { const ds = hasLive && cfg.variableName ? { subscribe: (cb: any) => { cb({ timestamp: Date.now(), value: liveNum }); return () => {} }, getHistory: () => [{ timestamp: Date.now(), value: liveNum }], status: 'connected' as const, destroy: () => {} } : undefined; return <AltaraGauge min={cfg.min ?? 0} max={cfg.max ?? 100} unit={cfg.unit} label="" size="md" dataSource={ds} mockMode={!ds} easingMs={cfg.easingMs ?? 500}
      thresholds={[{ value: cfg.threshold1 ?? 80, color: '#ff9800' }, { value: cfg.threshold2 ?? 90, color: '#ef5350' }].filter(t => t.value > (cfg.min ?? 0))} /> }
    case 'trend': {
      const channels: { key: string; label: string; color: string; unit: string; min: number; max: number }[] = []
      for (let i = 1; i <= 4; i++) {
        const en = cfg[`ch${i}En`] ?? ((cfg.chCount ?? 4) >= i)
        if (!en) continue
        channels.push({
          key: `ch${i}`,
          label: cfg[`ch${i}Label`] || `CH${i}`,
          color: cfg[`ch${i}Color`] || CHANNEL_COLORS[i - 1] || '#888',
          unit: cfg[`ch${i}Unit`] || '',
          min: cfg[`ch${i}Min`] ?? 0,
          max: cfg[`ch${i}Max`] ?? 100,
        })
      }
      return <TrendRecorderCell channels={channels} timeScale={cfg.timeScale || '5m'} showGrid={cfg.showGrid !== false} showLegend={cfg.showLegend !== false} showPoints={!!cfg.showPoints} lineWidth={cfg.lineWidth || 1.5} backgroundColor={cfg.backgroundColor || undefined} yAxisLabel={cfg.yAxisLabel || ''} mockMode={cfg.mockMode !== false} liveData={liveData} varMap={channels.reduce((m: Record<string, string>, c) => { const num = c.key.replace('ch', ''); m[c.key] = cfg[`ch${num}Var`] || ''; return m }, {})} />
    }
    case 'oee': return <OEEDashboard availability={cfg.availability ?? 0.85} performance={cfg.performance ?? 0.78} quality={cfg.quality ?? 0.95} shift={cfg.shift || 'A'} mockMode />
    case 'motor': return <MotorDashboard rpm={cfg.rpm ?? 2850} torque={cfg.torque ?? 42} current={cfg.current ?? 38} temperature={cfg.temperature ?? 72} mockMode />
    case 'predictive': return <PredictiveMaintenanceGauge healthScore={cfg.healthScore ?? 74} rulDays={cfg.rulDays ?? 45} size="md" mockMode />
    case 'alarm': return <AlarmAnnunciatorPanel columns={cfg.columns ?? 4} flashRate={cfg.flashRate ?? 2} groupBy={cfg.groupBy || undefined} mockMode />
    case 'pid': return <PIDTuningPanel kp={cfg.kp ?? 2.5} ki={cfg.ki ?? 0.8} kd={cfg.kd ?? 0.3} mockMode />
    case 'signal': return <SignalPanel signals={[{ key:'ch1', label:cfg.ch1Label||'CH1', value:cfg.ch1Val, unit:cfg.ch1Unit },{ key:'ch2', label:cfg.ch2Label||'CH2', value:cfg.ch2Val, unit:cfg.ch2Unit }] as any} />
    case 'eventlog': return <EventLog entries={[{ timestamp: Date.now()-5000, message:'系统启动', severity:'info' },{ timestamp: Date.now()-3000, message:'温度警告', severity:'warn' },{ timestamp: Date.now()-1000, message:'通信超时', severity:'error' }]} maxEntries={100} />
    case 'connectionbar': return <ConnectionBar url={cfg.title || 'PLC'} status={cfg.status === 'connected' ? 'connected' : 'disconnected'} />
    case 'process': return <ProcessFlowDiagram mockMode={cfg.mockMode !== false} />
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
                <div className="vdb-widget__body" onMouseDown={e => e.stopPropagation()}>{renderWidget(w.type, w.config, liveData)}</div>
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
            <h3 className="modal-title" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              ✏️ 设置
              <button className="btn btn--ghost btn--sm" style={{ color: 'var(--muted-foreground)', fontSize: 11 }}
                onClick={() => {
                  setFormTitle(WIDGET_META[formType]?.label || '')
                  const defaults: Record<string, any> = {}
                  for (const f of CONFIG_FIELDS[formType] || []) defaults[f.key] = f.default
                  setFormCfg(defaults)
                }}
                title="重置所有字段">重置</button>
            </h3>
            <div className="modal-form">
              <label className="modal-label">标题</label>
              <div className="modal-field-row">
                <input className="modal-input" value={formTitle} onChange={e => setFormTitle(e.target.value)} placeholder="自定义标题" />
                {formTitle && <button className="modal-clear-btn" onClick={() => setFormTitle('')} title="清空">✕</button>}
              </div>

              {fields.some(f => f.key === 'variableName') && (
                <VariablePicker
                  dbName={(formCfg.variableName||'').split(':')[0]||''}
                  varName={(formCfg.variableName||'').split(':').slice(1).join(':')}
                  importedDBs={importedDBs}
                  onChange={(dbName, varName) => {
                    setFormCfg(c => {
                      // 判断 label 是否还是默认值（取 CONFIG_FIELDS 中 label 字段的 default）
                      const labelDefault = CONFIG_FIELDS[formType]?.find(f => f.key === 'label')?.default
                      const isLabelDefault = !c.label || c.label === labelDefault
                      return {
                        ...c,
                        variableName: dbName ? `${dbName}:${varName}` : varName,
                        ...(varName ? { label: isLabelDefault ? varName : c.label } : {}),
                      }
                    })
                    if (varName) {
                      setFormTitle(prev => {
                        const titleDefault = WIDGET_META[formType]?.label
                        return (!prev || prev === titleDefault) ? `${dbName}_${varName}` : prev
                      })
                    }
                  }}
                />
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
                if (f.type === 'hidden') return null
                return (<div key={f.key}>
                  {f.type === 'boolean' ? (<label className="modal-label" style={{ display:'flex', alignItems:'center', gap:8, cursor:'pointer' }}><input type="checkbox" checked={!!formCfg[f.key]} onChange={e => setFormCfg(c => ({...c, [f.key]:e.target.checked}))} />{f.label}</label>)
                  : f.type === 'select' ? (<><label className="modal-label">{f.label}</label><select className="modal-input" value={formCfg[f.key]??f.default} onChange={e => setFormCfg(c => ({...c, [f.key]:e.target.value}))}>{f.options?.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}</select></>)
                  : f.type === 'separator' ? (<div style={{ borderTop:'1px solid var(--border)', margin:'12px 0 4px', paddingTop:8, fontSize:13, fontWeight:600, color:'var(--foreground)' }}>{f.label}</div>)
                  : (<><label className="modal-label">{f.label}</label><div className="modal-field-row">{f.type === 'text' ? (<><input className="modal-input" type="text" value={formCfg[f.key]??''} onChange={e => setFormCfg(c => ({...c, [f.key]:e.target.value}))} />{formCfg[f.key] ? <button className="modal-clear-btn" onClick={() => setFormCfg(c => ({...c, [f.key]:''}))} title="清空">✕</button> : null}</>) : <input className="modal-input" type="number" value={formCfg[f.key]??''} onChange={e => setFormCfg(c => ({...c, [f.key]:Number(e.target.value)}))} />}</div></>)}
                </div>)})}

              {formType === 'trend' && <TrendChannelConfig formCfg={formCfg} setFormCfg={setFormCfg} importedDBs={importedDBs} />}

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

const CHANNEL_COLORS = ['#E24B4A', '#37D3E0', '#1D9E75', '#F4D03F']
const CHANNEL_LABELS = ['CH1', 'CH2', 'CH3', 'CH4']

/** 通道变量选择器：选 DB → 搜变量 → 点击选中 */
function ChannelVarPicker({ channel, formCfg, setFormCfg, importedDBs }: {
  channel: number; formCfg: Record<string, any>; setFormCfg: (fn: (prev: Record<string, any>) => Record<string, any>) => void
  importedDBs: { dbNumber: number; dbName: string }[]
}) {
  const fullName = formCfg[`ch${channel}Var`] || ''
  const dbName = fullName.split(':')[0] || ''
  const varName = fullName.split(':').slice(1).join(':')
  const [search, setSearch] = useState(varName)
  const [open, setOpen] = useState(false)
  const [pos, setPos] = useState({ top: 0, left: 0, width: 200 })
  const ref = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const openDrop = useCallback(() => {
    if (inputRef.current) {
      const r = inputRef.current.getBoundingClientRect()
      setPos({ top: r.bottom + 2, left: r.left, width: r.width })
    }
    setOpen(true)
  }, [])
  const allVars = useMemo(() => {
    if (!dbName) return []
    const dbs = loadAllDBData()
    return dbs.find(d => d.dbName === dbName)?.variables ?? []
  }, [dbName])
  const filtered = useMemo(() => {
    if (!search) return allVars
    const q = search.toLowerCase()
    return allVars.filter((v: any) => v.name.toLowerCase().includes(q))
  }, [allVars, search])
  useEffect(() => {
    if (!open) return
    const h = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    const t = setTimeout(() => document.addEventListener('mousedown', h), 0)
    return () => { clearTimeout(t); document.removeEventListener('mousedown', h) }
  }, [open])

  return (
    <div className="vdb-ch-config__row" ref={ref}>
      <span className="vdb-ch-config__label">变量</span>
      <select style={{ width: 70, flexShrink: 0, fontSize: 11, height: 26 }} className="modal-input"
        value={dbName} onChange={e => setFormCfg(c => ({ ...c, [`ch${channel}Var`]: e.target.value ? `${e.target.value}:` : '' }))}>
        <option value="">--</option>
        {importedDBs.map(d => <option key={d.dbName} value={d.dbName}>{d.dbName}</option>)}
      </select>
      <div style={{ position: 'relative', flex: 1 }}>
        <input ref={inputRef} style={{ width: '100%', fontSize: 11, height: 26, fontFamily: 'monospace' }}
          className="modal-input" placeholder={dbName ? '搜索...' : '先选DB'}
          value={dbName ? search : ''} disabled={!dbName}
          onFocus={openDrop}
          onChange={e => { setSearch(e.target.value); openDrop() }}
          onKeyDown={e => { if (e.key === 'Escape') setOpen(false); if (e.key === 'Enter' && filtered.length === 1) { setFormCfg(c => ({ ...c, [`ch${channel}Var`]: `${dbName}:${filtered[0].name}` })); setSearch(filtered[0].name); setOpen(false) } }} />
        {open && dbName && (
          <div style={{ position: 'fixed', top: pos.top, left: pos.left, width: pos.width, zIndex: 99999, maxHeight: 160, overflowY: 'auto', background: 'var(--card)', border: '1px solid var(--border)', borderRadius: 'var(--radius)', boxShadow: '0 4px 20px rgba(0,0,0,0.35)' }}>
            {filtered.length === 0 ? <div style={{ padding: 8, fontSize: 11, color: 'var(--muted-foreground)' }}>无匹配</div>
            : filtered.map((v: any) => (
              <button key={v.name} style={{ display: 'flex', justifyContent: 'space-between', width: '100%', padding: '4px 8px', background: 'transparent', border: 'none', color: 'var(--foreground)', fontSize: 11, cursor: 'pointer', textAlign: 'left', fontFamily: 'Consolas, monospace', gap: 8 }}
                onMouseDown={() => { setFormCfg(c => ({ ...c, [`ch${channel}Var`]: `${dbName}:${v.name}` })); setSearch(v.name); setOpen(false) }}>
                <span>{v.name}</span>
                <span style={{ color: 'var(--muted-foreground)', fontSize: 10, flexShrink: 0 }}>{v.type.toUpperCase()} @{v.offset}</span>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

/** 趋势图通道配置：打勾即展开配置面板 */
function TrendChannelConfig({ formCfg, setFormCfg, importedDBs }: {
  formCfg: Record<string, any>; setFormCfg: (fn: (prev: Record<string, any>) => Record<string, any>) => void
  importedDBs: { dbNumber: number; dbName: string }[]
}) {
  return (
    <div style={{ borderTop: '1px solid var(--border)', marginTop: 12, paddingTop: 12 }}>
      <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8, color: 'var(--foreground)' }}>📊 通道配置</div>
      {[1, 2, 3, 4].map(i => {
        const enabled = formCfg[`ch${i}En`] ?? ((formCfg.chCount ?? 4) >= i)
        return (
          <div key={i} className="vdb-ch-config">
            <label className="vdb-ch-config__header">
              <input type="checkbox" checked={enabled}
                onChange={e => setFormCfg(c => ({ ...c, [`ch${i}En`]: e.target.checked }))} />
              <span className="vdb-ch-config__dot" style={{ background: formCfg[`ch${i}Color`] || CHANNEL_COLORS[i - 1] }} />
              <span className="vdb-ch-config__name">{formCfg[`ch${i}Label`] || CHANNEL_LABELS[i - 1]}</span>
            </label>
            {enabled && (
              <div className="vdb-ch-config__body">
                <div className="vdb-ch-config__row">
                  <span className="vdb-ch-config__label">标签</span>
                  <input className="modal-input" value={formCfg[`ch${i}Label`] ?? ''} onChange={e => setFormCfg(c => ({ ...c, [`ch${i}Label`]: e.target.value }))} />
                </div>
                <div className="vdb-ch-config__row">
                  <span className="vdb-ch-config__label">颜色</span>
                  <input className="modal-input" style={{ fontFamily:'monospace' }} value={formCfg[`ch${i}Color`] ?? CHANNEL_COLORS[i - 1]} onChange={e => setFormCfg(c => ({ ...c, [`ch${i}Color`]: e.target.value }))} />
                  <input type="color" value={formCfg[`ch${i}Color`] || CHANNEL_COLORS[i - 1]} onChange={e => setFormCfg(c => ({ ...c, [`ch${i}Color`]: e.target.value }))} />
                </div>
                <ChannelVarPicker channel={i} formCfg={formCfg} setFormCfg={setFormCfg} importedDBs={importedDBs} />
                <div className="vdb-ch-config__row">
                  <span className="vdb-ch-config__label">量程</span>
                  <input className="modal-input" type="number" value={formCfg[`ch${i}Min`] ?? 0} onChange={e => setFormCfg(c => ({ ...c, [`ch${i}Min`]: Number(e.target.value) }))} />
                  <span className="vdb-ch-config__range-sep">~</span>
                  <input className="modal-input" type="number" value={formCfg[`ch${i}Max`] ?? 100} onChange={e => setFormCfg(c => ({ ...c, [`ch${i}Max`]: Number(e.target.value) }))} />
                </div>
                <div className="vdb-ch-config__row">
                  <span className="vdb-ch-config__label">单位</span>
                  <input className="modal-input" value={formCfg[`ch${i}Unit`] ?? ''} onChange={e => setFormCfg(c => ({ ...c, [`ch${i}Unit`]: e.target.value }))} placeholder="℃ MPa" />
                </div>
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}

/** 带搜索过滤的变量选择器：选 DB → 搜变量名 → 点击选中 */
function VariablePicker({ dbName, varName, importedDBs, onChange }: {
  dbName: string; varName: string; importedDBs: { dbNumber: number; dbName: string }[]
  onChange: (dbName: string, varName: string) => void
}) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState(varName)
  const [pos, setPos] = useState({ top: 0, left: 0, width: 0 })
  const inputRef = useRef<HTMLInputElement>(null)
  const wrapRef = useRef<HTMLDivElement>(null)

  // 当前选中的 DB 下的所有变量
  const allVars = useMemo(() => {
    if (!dbName) return []
    const dbs = loadAllDBData()
    const db = dbs.find(d => d.dbName === dbName)
    return db?.variables ?? []
  }, [dbName])

  // 按搜索词过滤
  const filtered = useMemo(() => {
    if (!search) return allVars
    const q = search.toLowerCase()
    return allVars.filter(v => v.name.toLowerCase().includes(q))
  }, [allVars, search])

  // 点外部关闭
  useEffect(() => {
    if (!open) return
    const handler = (e: MouseEvent) => { if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false) }
    const t = setTimeout(() => document.addEventListener('mousedown', handler), 0)
    return () => { clearTimeout(t); document.removeEventListener('mousedown', handler) }
  }, [open])

  const openDropdown = useCallback(() => {
    if (!inputRef.current) return
    const rect = inputRef.current.getBoundingClientRect()
    setPos({ top: rect.bottom + 4, left: rect.left, width: rect.width })
    setOpen(true)
  }, [])

  return (
    <div>
      <label className="modal-label">变量</label>
      <div ref={wrapRef} style={{ display: 'flex', gap: 8 }}>
        <select className="modal-input" style={{ width: 100, flexShrink: 0 }} value={dbName}
          onChange={e => { onChange(e.target.value, ''); setSearch(''); setOpen(false) }}>
          <option value="">--</option>
          {importedDBs.map(d => <option key={d.dbName} value={d.dbName}>{d.dbName}</option>)}
        </select>
        <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 4 }}>
          <input ref={inputRef} className="modal-input" style={{ width: '100%' }} placeholder={dbName ? '搜索变量名...' : '先选择 DB'}
            value={dbName ? search : ''} disabled={!dbName}
            onFocus={openDropdown}
            onInput={openDropdown}
            onChange={e => { setSearch(e.target.value); openDropdown() }}
            onKeyDown={e => { if (e.key === 'Escape') setOpen(false); if (e.key === 'Enter' && filtered.length === 1) { onChange(dbName, filtered[0].name); setSearch(filtered[0].name); setOpen(false) } }} />
          {search && <button className="modal-clear-btn" onClick={() => { setSearch(''); onChange(dbName, ''); }} title="清空">✕</button>}
        </div>
      </div>
      {open && dbName && (
        <div className="vdb-var-picker__dropdown" style={{ position: 'fixed', top: pos.top, left: pos.left, width: pos.width, zIndex: 99999 }}>
          {filtered.length === 0 ? (
            <div className="vdb-var-picker__empty">无匹配变量</div>
          ) : (
            filtered.map(v => (
              <button key={v.name} className={`vdb-var-picker__item${v.name === varName ? ' vdb-var-picker__item--active' : ''}`}
                onMouseDown={() => { onChange(dbName, v.name); setSearch(v.name); setOpen(false) }}>
                <span className="vdb-var-picker__name">{v.name}</span>
                <span className="vdb-var-picker__type">{v.type.toUpperCase()} @{v.offset}{v.bit !== undefined ? `.${v.bit}` : ''}</span>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}

/** 趋势图自适应容器：ResizeObserver→width/height→TrendRecorder 原生分辨率渲染 */
const TrendRecorderCell = React.memo(function TrendRecorderCell({ channels, timeScale, showGrid, showLegend, showPoints, lineWidth, backgroundColor: bg, yAxisLabel, mockMode, liveData, varMap }: {
  channels?: { key: string; label: string; color: string; unit: string; min: number; max: number }[]
  timeScale: string; showGrid: boolean; showLegend: boolean; showPoints: boolean; lineWidth: number; backgroundColor?: string; yAxisLabel?: string; mockMode?: boolean
  liveData?: Record<string, { value: number | boolean }>
  varMap?: Record<string, string>
}) {
  const ref = useRef<HTMLDivElement>(null)
  const [size, setSize] = useState({ w: 400, h: 200 })

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const ro = new ResizeObserver(entries => {
      for (const e of entries) setSize({ w: Math.round(e.contentRect.width), h: Math.round(e.contentRect.height) })
    })
    ro.observe(el)
    const r = el.getBoundingClientRect()
    if (r.width > 0 && r.height > 0) setSize({ w: Math.round(r.width), h: Math.round(r.height) })
    return () => ro.disconnect()
  }, [])

  return (
    <div ref={ref} style={{ width: '100%', height: '100%' }}>
      <TrendRecorder
        channels={channels}
        width={size.w}
        height={size.h}
        timeScale={timeScale as any}
        showGrid={showGrid}
        showLegend={showLegend}
        showPoints={showPoints}
        lineWidth={lineWidth}
        backgroundColor={bg}
        yAxisLabel={yAxisLabel}
        mockMode={mockMode}
        liveData={liveData}
        varMap={varMap}
      />
    </div>
  )
})

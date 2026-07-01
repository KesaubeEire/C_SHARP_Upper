// @ts-nocheck
import React, { useState, useEffect, useCallback, useRef, useMemo } from 'react'
import CollapsibleSection from './CollapsibleSection'
import Tooltip from './Tooltip'
import { Responsive, noCompactor } from 'react-grid-layout'
import 'react-grid-layout/css/styles.css'
import { AltaraGauge } from '../components/AltaraGauge'
import { TrendRecorder } from '../components/AltaraTrendRecorder'
import { SignalPanel } from './altara/core/components/SignalPanel/SignalPanel'
import { EventLog } from './altara/core/components/EventLog/EventLog'
import { ConnectionBar } from './altara/core/components/ConnectionBar/ConnectionBar'
import { OEEDashboard } from './altara/industrial/components/OEEDashboard'
import { MotorDashboard } from './altara/industrial/components/MotorDashboard'
import { PredictiveMaintenanceGauge } from './altara/industrial/components/PredictiveMaintenanceGauge'
import { AlarmAnnunciatorPanel } from './altara/industrial/components/AlarmAnnunciatorPanel'
import { PIDTuningPanel } from './altara/industrial/components/PIDTuningPanel'
import { ProcessFlowDiagram } from './altara/industrial/components/ProcessFlowDiagram'
import { useContextMenu } from './VDBContextMenu'
import type { IOData } from '../hooks/usePLCData'
import { resolveVarName, loadMapping, loadAllDBData, writePLC, reregisterAllDBs } from '../hooks/useDBMapping'


type WidgetType = 'value' | 'lamp' | 'button' | 'gauge' | 'trend' | 'oee' | 'motor' | 'predictive' | 'alarm' | 'pid' | 'signal' | 'eventlog' | 'connectionbar' | 'process'

interface Widget { id: string; type: WidgetType; title: string; config: Record<string, any> }

const STORAGE_KEY = 'trioop_vdb_v4'

function loadData(): { widgets: Widget[]; layouts: Record<string, any[]> } {
  try { const raw = localStorage.getItem(STORAGE_KEY); if (raw) return JSON.parse(raw) } catch {}
  return { widgets: [], layouts: { lg: [] } }
}

const WIDGET_HELP: Record<WidgetType, string> = {
  value: `绑定一个 PLC 变量，显示实时数值。

• 变量名：选择已导入的 DB 块和变量
• 单位：显示在数值右侧，如 ℃、MPa、rpm`,
  lamp: `绑定一个 Bool 变量或 I/Q/M 点位，显示状态灯。

• ON 时显示绿色，OFF 时显示灰色
• 支持 DB 块变量（如 DB1:启动）和 I/Q/M 区（如 Q8.3）
• 适合显示运行状态、报警信号`,
  button: `绑定一个 Bool 变量或 Q/M 点位，点击即可写入。

• 按1松0：按下鼠标写 1，松开写 0
• 取反：点一次翻转当前值（0→1 或 1→0）
• 支持 DB 块变量和 Q/M 区（如 Q8.6、M10.3）
• 按钮文字：自定义按键显示名称`,
  gauge: `指针式仪表盘，适合显示实时数值。

• 变量名：选择要显示的 PLC 变量
• 量程：设置表盘的最小值和最大值
• 预警值/危险值：在表盘上显示彩色弧线段
• 缓动时长：指针摆动动画速度，0=瞬间跳转`,
  trend: `实时趋势曲线，支持多通道对比。

• 演示模式：开=显示 4 条模拟曲线
• 每个通道可独立配置变量/颜色/量程
• 采样标记：在每个数据点画小圆点
• 关闭演示模式后，按配置的变量名采集实时数据`,
  oee: `OEE 综合仪表盘（设备综合效率）。

• 可用率：设备实际运行时间占比
• 性能率：实际生产速度与设计速度之比
• 质量率：合格产品占总产量之比
• OEE = 可用率 × 性能率 × 质量率`,
  motor: `电机监控面板。

• 转速：电机当前转速（RPM）
• 扭矩：输出扭矩（Nm）
• 电流：相电流（A）
• 温度：绕组温度（°C）`,
  predictive: `预测维护表盘。

• 健康指数：0~100，越高越健康
• 剩余寿命：预计可继续使用天数`,
  alarm: `报警瓷砖面板，类似控制室报警墙。

• 列数：每行显示几个报警瓷砖
• 闪烁频率：未确认报警的闪烁速度
• 分组：按指定字段分组显示`,
  pid: `PID 调谐参数面板。

• Kp：比例增益
• Ki：积分增益
• Kd：微分增益`,
  signal: `双通道信号面板，每个通道独立配置。

• 通道1/2 分别设置标签、数值、单位
• 适合显示成对的测量值（如温度+压力）`,
  eventlog: `事件日志，显示系统事件。

• 每条记录包含时间戳、级别和消息
• 支持 info/warn/error 三种级别`,
  connectionbar: `连接状态条，显示 PLC 通信状态。

• 连接地址：PLC 的标识名称
• 状态：已连接/未连接`,
  process: `工艺流程图，开演示模式显示 3 罐体工艺动画。

• 演示模式：开=显示内置动画
• 后续可绑定实际设备点位数据`,
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
  value:      [{ key:'variableName', label:'变量名', type:'text', default:'' }, { key:'unit', label:'单位', type:'text', default:'' }, { key:'minValue', label:'下限', type:'number', default:-Infinity }, { key:'maxValue', label:'上限', type:'number', default:Infinity }, { key:'writable', label:'允许修改', type:'boolean', default:false }],
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

/** 解析 I/Q/M 区变量名（如 Q8.6 → { area: 'q', byte: 8, bit: 6 }） */
function parseIOVar(name: string): { area: 'i' | 'q' | 'm'; byte: number; bit: number } | null {
  const m = name?.match(/^([IQM])(\d+)\.(\d+)$/)
  if (!m) return null
  const area = m[1].toLowerCase() as 'i' | 'q' | 'm'
  return { area, byte: parseInt(m[2]), bit: parseInt(m[3]) }
}

/** 从 ioData 中读取 I/Q/M 特定位的值 */
function readIOBit(ioData: IOData | undefined, area: 'i' | 'q' | 'm', byte: number, bit: number): boolean | undefined {
  const b = ioData?.[area]?.[byte]
  if (b === undefined) return undefined
  return (b & (1 << bit)) !== 0
}

function renderWidget(type: WidgetType, cfg: Record<string, any>, liveData?: Record<string, { value: number | boolean }>, ioData?: IOData) {
  // 检查是否是 I/Q/M 区变量
  const ioVar = parseIOVar(cfg.variableName || '')
  const isIO = ioVar !== null
  // DB 变量走 liveData
  const pt = isIO ? undefined : liveData?.[cfg.variableName || '']
  const liveVal = isIO ? (ioVar ? readIOBit(ioData, ioVar.area, ioVar.byte, ioVar.bit) : undefined) : pt?.value
  const liveNum = typeof liveVal === 'number' ? liveVal : (liveVal ? 1 : 0)
  const hasLive = liveVal !== undefined && liveVal !== null
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
    case 'button': return <WidgetButton cfg={cfg} liveVal={liveVal} liveNum={liveNum} hasLive={hasLive} ioVar={ioVar} />
    case 'lamp': return (<div style={{ display:'flex', justifyContent:'center', alignItems:'center', gap:12, height:'100%' }}><div style={{ width:32, height:32, borderRadius:'50%', background:hasLive&&liveVal?'#4caf50':'#333', boxShadow:hasLive&&liveVal?'0 0 16px #4caf50':'none', transition:'all 0.2s' }} /><span style={{ fontSize:18, fontWeight:600, color:hasLive&&liveVal?'#4caf50':'var(--text-muted)' }}>{hasLive?(liveVal?'ON':'OFF'):'--'}</span></div>)
    case 'value': return <WidgetValue cfg={cfg} liveVal={liveVal} liveNum={liveNum} hasLive={hasLive} />
    default: return (<div style={{ textAlign:'center', padding:16 }}><div style={{ fontSize:28, fontWeight:600, fontFamily:'monospace' }}>{hasLive?(typeof liveVal==='number'?liveNum.toFixed(2):(liveVal?'ON':'OFF')):'--'}</div>{cfg.unit && <div style={{ fontSize:13, color:'var(--text-muted)' }}>{cfg.unit}</div>}</div>)
  }
}

/** 写入 Q/M 点位（通过 write-raw API） */
async function writeIOArea(area: 'q' | 'm', byte: number, bit: number, value: number): Promise<void> {
  const res = await fetch('/api/plc/write-raw', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ address: `${area.toUpperCase()}B${byte}`, bit, value }),
  })
  if (!res.ok) throw new Error(await res.text())
}

/** 仪表盘按钮组件 — 用 useRef 持久化按压状态，跨 SSE 重渲染也不丢失 */
function WidgetButton({ cfg, liveVal, liveNum, hasLive, ioVar }: {
  cfg: Record<string, any>; liveVal: number | boolean | undefined; liveNum: number; hasLive: boolean;
  ioVar: { area: 'i' | 'q' | 'm'; byte: number; bit: number } | null;
}) {
  const pressedRef = useRef(false)
  const writeVal = useCallback((val: number) => {
    if (!cfg.variableName) return Promise.resolve()
    if (ioVar && (ioVar.area === 'q' || ioVar.area === 'm')) {
      return writeIOArea(ioVar.area, ioVar.byte, ioVar.bit, val)
    }
    return writePLC(cfg.variableName, val)
  }, [cfg.variableName, ioVar])
  const getVal = useCallback(() => {
    return cfg.mode === 'toggle' ? (hasLive ? (liveVal ? 0 : 1) : 1) : (cfg.mode === 'momentary_off' ? 0 : 1)
  }, [cfg.mode, hasLive, liveVal])
  const handleMouseDown = useCallback(() => {
    if (!cfg.variableName) return
    pressedRef.current = true
    writeVal(getVal()).catch(() => {})
  }, [cfg.variableName, writeVal, getVal])
  const handleMouseUp = useCallback(() => {
    if (!cfg.variableName || cfg.mode === 'toggle') return
    pressedRef.current = false
    writeVal(cfg.mode === 'momentary_off' ? 1 : 0).catch(() => {})
  }, [cfg.variableName, cfg.mode, writeVal])
  const handleMouseLeave = useCallback(() => {
    if (!cfg.variableName || cfg.mode === 'toggle' || !pressedRef.current) return
    pressedRef.current = false
    writeVal(cfg.mode === 'momentary_off' ? 1 : 0).catch(() => {})
  }, [cfg.variableName, cfg.mode, writeVal])
  return (<div style={{display:'flex',justifyContent:'center',alignItems:'center',height:'100%'}}><button className="btn btn--primary" style={{padding:'12px 32px',fontSize:16,fontWeight:600}}
    onMouseDown={handleMouseDown} onMouseUp={handleMouseUp} onMouseLeave={handleMouseLeave}>{cfg.label||'按钮'}</button></div>)
}

/** 数值组件 — 可点击编辑 + 阶跃按钮 */
function WidgetValue({ cfg, liveVal, liveNum, hasLive }: { cfg: Record<string, any>; liveVal: number | boolean | undefined; liveNum: number; hasLive: boolean }) {
  const [editing, setEditing] = useState(false)
  const [editVal, setEditVal] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)
  const writable = !!cfg.writable
  const curVal = hasLive ? liveNum : 0
  const minV = cfg.minValue ?? -Infinity
  const maxV = cfg.maxValue ?? Infinity

  useEffect(() => {
    if (editing) {
      setEditVal(String(hasLive ? liveNum.toFixed(1) : '0'))
      inputRef.current?.focus()
      inputRef.current?.select()
    }
  }, [editing])

  const commitEdit = useCallback(() => {
    const n = parseFloat(editVal)
    if (!isNaN(n) && cfg.variableName) {
      const clamped = Math.max(minV, Math.min(maxV, n))
      if (clamped !== curVal) writePLC(cfg.variableName, clamped).catch(() => {})
    }
    setEditing(false)
  }, [editVal, cfg.variableName, minV, maxV, curVal])

  const stepBtn = useCallback((step: number) => {
    const next = Math.max(minV, Math.min(maxV, curVal + step))
    if (!cfg.variableName || next === curVal) return
    writePLC(cfg.variableName, next).catch(() => {})
  }, [cfg.variableName, minV, maxV, curVal])

  return (<div style={{ textAlign:'center', padding:'12px 8px', height:'100%', display:'flex', flexDirection:'column', justifyContent:'center' }}>
    {editing ? (
      <input ref={inputRef} type="number" step="any" className="modal-input"
        style={{ width:'80%', margin:'0 auto', textAlign:'center', fontSize:20, fontFamily:'var(--vt-font-mono)' }}
        value={editVal}
        onChange={e => setEditVal(e.target.value)}
        onKeyDown={e => { if (e.key === 'Enter') commitEdit(); if (e.key === 'Escape') setEditing(false) }}
        onBlur={commitEdit} />
    ) : (
      <div style={{ fontSize:28, fontWeight:600, fontFamily:'var(--vt-font-mono)', lineHeight:1.2, cursor: writable && hasLive ? 'pointer' : 'default' }}
        onClick={() => { if (writable && hasLive) setEditing(true) }}
        title={writable ? '点击编辑' : ''}>
        {hasLive ? liveNum.toFixed(1) : '--'}
      </div>
    )}
    {cfg.unit && <div style={{ fontSize:13, color:'var(--text-muted)', marginBottom: writable ? 6 : 0 }}>{cfg.unit}</div>}
    {writable && cfg.variableName && hasLive && !editing && (
      <div style={{ display:'flex', gap:4, justifyContent:'center', marginTop:4 }}>
        <button className="btn btn--sm" style={{ fontSize:10, padding:'2px 6px', minWidth:32 }} onClick={() => stepBtn(-10)} disabled={!isFinite(minV) || curVal - 10 < minV}>−10</button>
        <button className="btn btn--sm" style={{ fontSize:10, padding:'2px 6px', minWidth:32 }} onClick={() => stepBtn(-1)} disabled={!isFinite(minV) || curVal - 1 < minV}>−1</button>
        <button className="btn btn--sm btn--primary" style={{ fontSize:10, padding:'2px 6px', minWidth:32 }} onClick={() => stepBtn(1)} disabled={!isFinite(maxV) || curVal + 1 > maxV}>+1</button>
        <button className="btn btn--sm btn--primary" style={{ fontSize:10, padding:'2px 6px', minWidth:32 }} onClick={() => stepBtn(10)} disabled={!isFinite(maxV) || curVal + 10 > maxV}>+10</button>
      </div>
    )}
  </div>)
}

export default function VisualDashboard({ liveData, ioData }: {
  liveData?: Record<string, { value: number | boolean }>
  ioData?: IOData
}) {
  const [data, setDataRaw] = useState(() => loadData())
  const [showPalette, setShowPalette] = useState(false)
  const [editing, setEditing] = useState<string | null>(null)
  const [helpWidget, setHelpWidget] = useState<WidgetType | null>(null)
  const [formTitle, setFormTitle] = useState(''); const [formCfg, setFormCfg] = useState<Record<string, any>>({}); const [formType, setFormType] = useState<WidgetType>('value')
  const [rowH, setRowH] = useState(() => { try { return Number(localStorage.getItem(ROW_HEIGHT_KEY)) || 120 } catch { return 120 } })
  const paletteRef = useRef<HTMLDivElement>(null)
  const fileRef = useRef<HTMLInputElement>(null)
  const [currentBreakpoint, setCurrentBreakpoint] = useState<string>('lg')
  const [ghostCell, setGhostCell] = useState<{x: number; y: number} | null>(null)
  const [rowGapHover, setRowGapHover] = useState<number | null>(null)   // 行间隙悬停的行号
  const [rowGapMenu, setRowGapMenu] = useState<{row: number; top: number; left: number} | null>(null)  // 行间隙右键菜单
  const [insertMenu, setInsertMenu] = useState<{x: number; y: number; top: number; left: number} | null>(null)
  const [resizing, setResizing] = useState(false)
  const insertMenuRef = useRef<HTMLDivElement>(null)
  const rowGapMenuRef = useRef<HTMLDivElement>(null)

  // ─── 撤销 / 重做 ─────────────────────────────────────
  const historyRef = useRef<{ stack: any[]; idx: number }>({ stack: [], idx: -1 })
  const setData = useCallback((fn: (prev: any) => any) => {
    setDataRaw(prev => {
      const next = fn(prev)
      const h = historyRef.current
      // 剪掉当前位置之后的历史
      h.stack = h.stack.slice(0, h.idx + 1)
      h.stack.push(JSON.parse(JSON.stringify(prev)))
      if (h.stack.length > 50) h.stack.shift()
      h.idx = h.stack.length - 1
      return next
    })
  }, [])
  const undo = useCallback(() => {
    const h = historyRef.current
    if (h.idx < 0) return
    setDataRaw(_ => { const prev = h.stack[h.idx]; h.idx--; return JSON.parse(JSON.stringify(prev)) })
  }, [])
  const redo = useCallback(() => {
    const h = historyRef.current
    if (h.idx + 1 >= h.stack.length) return
    setDataRaw(_ => { h.idx++; return JSON.parse(JSON.stringify(h.stack[h.idx])) })
  }, [])

  // ─── 导入 / 导出 ─────────────────────────────────────
  const exportData = useCallback(() => {
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'dashboard.json'; a.click()
  }, [data])
  const importData = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]; if (!file) return
    file.text().then(text => { try { setDataRaw(JSON.parse(text)) } catch {} }).catch(() => {})
    if (fileRef.current) fileRef.current.value = ''
  }, [setDataRaw])

  const containerRef = useRef<HTMLDivElement>(null); const [containerWidth, setContainerWidth] = useState(0)
  useEffect(() => { localStorage.setItem(STORAGE_KEY, JSON.stringify(data)) }, [data])
  useEffect(() => { localStorage.setItem(ROW_HEIGHT_KEY, String(rowH)) }, [rowH])
  useEffect(() => { const el = containerRef.current; if (!el) return; const ro = new ResizeObserver(entries => { for (const e of entries) setContainerWidth(e.contentRect.width) }); ro.observe(el); return () => ro.disconnect() }, [])

  // ─── 网格度量 ────────────────────────────────────────────
  const getGridMetrics = useCallback(() => {
    const cols = COLS[currentBreakpoint] ?? 12
    const margin = [10, 10]
    const colWidth = (containerWidth - margin[0] * (cols - 1) - 0 * 2) / cols
    return { cols, colWidth, cellHeight: rowH, margin }
  }, [containerWidth, rowH, currentBreakpoint])

  const mouseToGrid = useCallback((clientX: number, clientY: number) => {
    const container = containerRef.current?.parentElement // .vdb-rgl-wrapper
    if (!container) return null
    const rect = container.getBoundingClientRect()
    const { cols, colWidth, cellHeight, margin } = getGridMetrics()
    const mx = clientX - rect.left
    const my = clientY - rect.top
    const col = Math.floor(mx / (colWidth + margin[0]))
    const row = Math.floor(my / (cellHeight + margin[1]))
    if (col < 0 || col >= cols || row < 0) return null
    // 检查该格是否已被占
    const layout = data.layouts[currentBreakpoint] ?? []
    const oc = layout.some((l: any) =>
      l.i && col < l.x + l.w && col >= l.x && row < l.y + l.h && row >= l.y
    )
    return oc ? null : { x: col, y: row }
  }, [data.layouts, currentBreakpoint, getGridMetrics])

  // ─── 检测鼠标是否在行间隙区域 ────────────────────────────
  const mouseToRowGap = useCallback((clientX: number, clientY: number): number | null => {
    const container = containerRef.current?.parentElement
    if (!container) return null
    const rect = container.getBoundingClientRect()
    const { cols, colWidth, cellHeight, margin } = getGridMetrics()
    const my = clientY - rect.top
    const mx = clientX - rect.left
    if (mx < 0 || mx > rect.width || my < 0) return null
    const row = Math.floor(my / (cellHeight + margin[1]))
    const rowStart = row * (cellHeight + margin[1])
    const rowWidgetEnd = rowStart + cellHeight  // 组件内容区底边
    const rowGapEnd = rowStart + cellHeight + margin[1]  // gap 底边
    // 鼠标落在 gap 区域内（组件底边到行底边之间）
    if (my >= rowWidgetEnd && my < rowGapEnd) return row
    return null
  }, [getGridMetrics])

  // ─── RGL 容器事件 ────────────────────────────────────────
  const handleRglMouseMove = useCallback((e: React.MouseEvent) => {
    if (resizing) return
    // 先检测行间隙悬停
    const gapRow = mouseToRowGap(e.clientX, e.clientY)
    if (gapRow !== null) {
      setRowGapHover(gapRow)
      setGhostCell(null)
      return
    }
    setRowGapHover(null)
    if ((e.target as HTMLElement)?.closest?.('.vdb-widget')) { setGhostCell(null); return }
    const pos = mouseToGrid(e.clientX, e.clientY)
    setGhostCell(pos)
  }, [mouseToGrid, mouseToRowGap, resizing])

  const handleRglMouseLeave = useCallback(() => { setGhostCell(null); setRowGapHover(null) }, [])

  const handleRglContextMenu = useCallback((e: React.MouseEvent) => {
    if ((e.target as HTMLElement)?.closest?.('.vdb-widget')) return
    e.preventDefault()
    // 优先检测行间隙右键
    const gapRow = mouseToRowGap(e.clientX, e.clientY)
    if (gapRow !== null) {
      setRowGapMenu({ row: gapRow, top: e.clientY, left: e.clientX })
      setRowGapHover(null)
      setInsertMenu(null)
      return
    }
    const pos = mouseToGrid(e.clientX, e.clientY)
    if (!pos) return
    setInsertMenu({ ...pos, top: e.clientY, left: e.clientX })
    setGhostCell(null)
  }, [mouseToGrid, mouseToRowGap])

  const handleRglClick = useCallback((e: React.MouseEvent) => {
    if ((e.target as HTMLElement)?.closest?.('.vdb-widget')) return
    setInsertMenu(null)
    setRowGapMenu(null)
  }, [])

  // 点击外部 / Escape 关闭 insertMenu
  useEffect(() => {
    if (!insertMenu) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setInsertMenu(null) }
    const onDown = (e: MouseEvent) => {
      if (insertMenuRef.current && !insertMenuRef.current.contains(e.target as Node)) setInsertMenu(null)
    }
    const t = setTimeout(() => document.addEventListener('mousedown', onDown), 0)
    document.addEventListener('keydown', onKey)
    return () => { clearTimeout(t); document.removeEventListener('mousedown', onDown); document.removeEventListener('keydown', onKey) }
  }, [insertMenu])

  // 点击外部 / Escape 关闭 rowGapMenu
  useEffect(() => {
    if (!rowGapMenu) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setRowGapMenu(null) }
    const onDown = (e: MouseEvent) => {
      if (rowGapMenuRef.current && !rowGapMenuRef.current.contains(e.target as Node)) setRowGapMenu(null)
    }
    const t = setTimeout(() => document.addEventListener('mousedown', onDown), 0)
    document.addEventListener('keydown', onKey)
    return () => { clearTimeout(t); document.removeEventListener('mousedown', onDown); document.removeEventListener('keydown', onKey) }
  }, [rowGapMenu])

  const addWidget = useCallback((type: WidgetType) => {
    const meta = WIDGET_META[type]; const id = `w${Date.now()}`
    const cfg: Record<string, any> = {}; for (const f of CONFIG_FIELDS[type] || []) cfg[f.key] = f.default
    const layout = data.layouts.lg || []
    const maxY = layout.reduce((m: number, l: any) => Math.max(m, l.y + l.h), 0)
    const newLayout = { i: id, x: 0, y: maxY, w: meta.w, h: meta.h }
    setData(d => ({ widgets: [...d.widgets, { id, type, title: meta.label, config: cfg }], layouts: { ...d.layouts, lg: [...layout, newLayout] } }))
    setFormTitle(meta.label); setFormType(type); setFormCfg({ ...cfg }); setEditing(id); setShowPalette(false)
  }, [data.layouts])

  // ─── 在指定网格位置插入组件 ──────────────────────────────
  const addWidgetAt = useCallback((type: WidgetType, x: number, y: number) => {
    const meta = WIDGET_META[type]; const id = `w${Date.now()}`
    const cfg: Record<string, any> = {}; for (const f of CONFIG_FIELDS[type] ?? []) cfg[f.key] = f.default
    const bp = currentBreakpoint
    const layout = (data.layouts[bp] ?? []).map((l: any) => ({ ...l }))
    const newItem = { i: id, x, y, w: 1, h: 1 }
    const newLayout = [...layout, newItem]
    // 碰撞检测：每个格子最多一个组件 — 完整级联推挤
    for (let pass = 0; pass < 50; pass++) {
      let moved = false
      for (let a = 0; a < newLayout.length; a++) {
        for (let b = 0; b < newLayout.length; b++) {
          if (a === b) continue
          const A = newLayout[a], B = newLayout[b]
          if (A.x < B.x + B.w && A.x + A.w > B.x && A.y < B.y + B.h && A.y + A.h > B.y) {
            if (A.y >= B.y) { B.y = A.y + A.h; moved = true }
            else { A.y = B.y + B.h; moved = true }
          }
        }
      }
      if (!moved) break
    }
    setData(d => ({
      widgets: [...d.widgets, { id, type, title: meta.label, config: cfg }],
      layouts: { ...d.layouts, [bp]: newLayout },
    }))
    setGhostCell(null); setInsertMenu(null)
    setFormTitle(meta.label); setFormType(type); setFormCfg({ ...cfg }); setEditing(id)
  }, [data.layouts, currentBreakpoint, setData])

  // ─── 插入空行 ─────────────────────────────────────────────
  const insertEmptyRow = useCallback((y: number) => {
    layoutVersionRef.current++
    setData(d => {
      const bp = currentBreakpoint
      const layout = (d.layouts[bp] ?? []).map((l: any) => ({ ...l }))
      const newLayout = layout.map(l => {
        if (l.y >= y) return { ...l, y: l.y + 1 }
        return l
      })
      return { ...d, layouts: { ...d.layouts, [bp]: newLayout } }
    })
    setInsertMenu(null); setRowGapMenu(null)
  }, [currentBreakpoint, setData])

  // ─── 删除空行 ─────────────────────────────────────────────
  const deleteEmptyRow = useCallback((y: number) => {
    layoutVersionRef.current++
    setData(d => {
      const bp = currentBreakpoint
      const layout = (d.layouts[bp] ?? []).map((l: any) => ({ ...l }))
      const newLayout = layout.map(l => {
        if (l.y > y) return { ...l, y: l.y - 1 }
        return l
      })
      return { ...d, layouts: { ...d.layouts, [bp]: newLayout } }
    })
    setInsertMenu(null); setRowGapMenu(null)
  }, [currentBreakpoint, setData])

  // ─── 检查某行是否为空 ────────────────────────────────────
  const isRowEmpty = useCallback((y: number): boolean => {
    const layout = data.layouts[currentBreakpoint] ?? []
    return !layout.some((l: any) => l.y <= y && l.y + l.h > y)
  }, [data.layouts, currentBreakpoint])

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

  // ─── 标记外部修改 layout（插入空行等），阻止 onLayoutChange 覆盖 ──
  const layoutVersionRef = useRef(0)

  /** 最近一次有效的布局快照（用于拖拽重叠时回退） */
  const prevValidLayoutRef = useRef<Record<string, any[]> | null>(null)
  const revertTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  /** onDrag：拖拽开始时保存当前有效布局 */
  const onDragStart = useCallback(() => {
    prevValidLayoutRef.current = JSON.parse(JSON.stringify(data.layouts))
  }, [data.layouts])

  /** onLayoutChange：检测重叠，如果重叠则 schedule RGL 强制重挂载恢复原位 */
  const onLayoutChange = useCallback((layout: any, allLayouts: any) => {
    setData(d => {
      const bp = currentBreakpoint
      const items = (allLayouts[bp] || []) as any[]
      for (let a = 0; a < items.length; a++) {
        for (let b = a + 1; b < items.length; b++) {
          const A = items[a], B = items[b]
          if (A.x < B.x + B.w && A.x + A.w > B.x && A.y < B.y + B.h && A.y + A.h > B.y) {
            // 有重叠 → 等 RGL 本次回调走完，再强制重挂载弹回原位
            const fallback = prevValidLayoutRef.current
            if (fallback) {
              if (revertTimerRef.current) clearTimeout(revertTimerRef.current)
              revertTimerRef.current = setTimeout(() => {
                layoutVersionRef.current++
                setDataRaw(prev => ({ ...prev, layouts: fallback }))
              }, 0)
            }
            return d  // 本次渲染不接受变更，RGL 展示叠加状态但被迫回退到 setTimeout 触发的重挂载
          }
        }
      }
      // 无重叠 → 接受并缓存
      prevValidLayoutRef.current = allLayouts
      return { ...d, layouts: allLayouts }
    })
  }, [currentBreakpoint])

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
        <Tooltip content="撤销"><button className="btn btn--ghost btn--sm" onClick={undo} style={{ fontSize: 14 }}>↩</button></Tooltip>
        <Tooltip content="重做"><button className="btn btn--ghost btn--sm" onClick={redo} style={{ fontSize: 14 }}>↪</button></Tooltip>
        <Tooltip content="导出 JSON"><button className="btn btn--ghost btn--sm" onClick={exportData}>📤</button></Tooltip>
        <input ref={fileRef} type="file" accept=".json" onChange={importData} style={{ display:'none' }} />
        <Tooltip content="导入 JSON"><button className="btn btn--ghost btn--sm" onClick={() => fileRef.current?.click()}>📥</button></Tooltip>
        <Tooltip content="重新注册所有已导入 DB"><button className="btn btn--ghost btn--sm" onClick={() => reregisterAllDBs()}>↻</button></Tooltip>
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
        <div className="vdb-rgl-wrapper" onMouseMove={handleRglMouseMove} onMouseLeave={handleRglMouseLeave} onContextMenu={handleRglContextMenu} onClick={handleRglClick}>
          <div ref={containerRef} style={{ width: "100%" }}><Responsive key={layoutVersionRef.current} width={containerWidth}
            className="vdb-rgl"
            layouts={data.layouts}
            breakpoints={BREAKPOINTS}
            cols={COLS}
            rowHeight={rowH}
            onLayoutChange={onLayoutChange}
            onDragStart={onDragStart}
            onBreakpointChange={(bp) => setCurrentBreakpoint(bp)}
            onResizeStart={() => setResizing(true)}
            onResizeStop={() => { setResizing(false); setRowGapHover(null) }}
            draggableHandle=".vdb-widget__bar"
            isDraggable
            isResizable
            compactType={null}
            compactor={noCompactor}
            preventCollision={false}
            margin={[10, 10]}
            containerPadding={[0, 0]}
            useCSSTransforms
          >
            {data.widgets.map(w => (
              <div key={w.id} className="vdb-widget" onContextMenu={e => ctx.show(e, [
                { label: '编辑', icon: '✏️', action: () => openEdit(w) },
                { label: '帮助', icon: '❓', action: () => setHelpWidget(w.type) },
                { label: '删除', icon: '✕', action: () => removeWidget(w.id), danger: true },
              ])}>
                <div className="vdb-widget__bar">
                  <span className="vdb-widget__title">{WIDGET_META[w.type]?.icon} {w.title}</span>
                  <div className="vdb-widget__actions">
                    <button className="btn btn--ghost btn--sm" onClick={() => openEdit(w)}>✏️</button>
                    <button className="btn btn--destructive btn--sm" onClick={() => removeWidget(w.id)}>✕</button>
                  </div>
                </div>
                <div className="vdb-widget__body" onMouseDown={e => e.stopPropagation()}><ResizeWrapper>{renderWidget(w.type, w.config, liveData, ioData)}</ResizeWrapper></div>
              </div>
            ))}
          </Responsive>
          {/* Ghost cell */}
          {ghostCell && (() => {
            const { colWidth, cellHeight, margin } = getGridMetrics()
            return <div className="vdb-ghost-cell" style={{
              left: ghostCell.x * (colWidth + margin[0]),
              top: ghostCell.y * (cellHeight + margin[1]),
              width: colWidth,
              height: cellHeight,
            }} />
          })()}
          {/* 行间隙指示线 */}
          {rowGapHover !== null && (() => {
            const { cellHeight, margin } = getGridMetrics()
            const top = (rowGapHover + 1) * (cellHeight + margin[1]) - margin[1] / 2
            return <div className="vdb-rowgap" style={{ top }} />
          })()}
          {/* 行间隙右键菜单 */}
          {rowGapMenu && (
            <div ref={rowGapMenuRef} className="vdb-ctx" style={{ position: 'fixed', zIndex: 99999, left: rowGapMenu.left, top: rowGapMenu.top }}>
              <button className="vdb-ctx__item"
                onClick={() => { insertEmptyRow(rowGapMenu.row + 1); setRowGapMenu(null) }}>
                <span className="vdb-ctx__icon">📏</span>
                在此行下方插入空行
              </button>
            </div>
          )}
          {/* 右键插入菜单 */}
          {insertMenu && (() => {
            const rowEmpty = (() => {
              const layout = data.layouts[currentBreakpoint] ?? []
              return !layout.some((l: any) => l.y <= insertMenu.y && l.y + l.h > insertMenu.y)
            })()
            return (
            <div ref={insertMenuRef} className="vdb-ctx" style={{ position: 'fixed', zIndex: 99999, left: insertMenu.left, top: insertMenu.top }}>
              <div className="vdb-ctx__subtitle">添加组件</div>
              {Object.entries(WIDGET_META).map(([type, meta]) => (
                <button key={type} className="vdb-ctx__item"
                  onClick={() => addWidgetAt(type as WidgetType, insertMenu.x, insertMenu.y)}>
                  <span className="vdb-ctx__icon">{meta.icon}</span>
                  {meta.label}
                </button>
              ))}
              <div className="vdb-ctx__sep" />
              <button className="vdb-ctx__item"
                onClick={() => insertEmptyRow(insertMenu.y)}>
                <span className="vdb-ctx__icon">📏</span>
                在此行插入空行
              </button>
              {rowEmpty && (
                <button className="vdb-ctx__item vdb-ctx__item--danger"
                  onClick={() => deleteEmptyRow(insertMenu.y)}>
                  <span className="vdb-ctx__icon">🗑️</span>
                  删除此空行
                </button>
              )}
            </div>
            )
          })()}
          </div>
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

              {fields.some(f => f.key === 'variableName') && (() => {
                const parsed = parseVarName(formCfg.variableName || '')
                // 按钮可写 Q/M 区，指示灯可读 I/Q/M 区
                const ioAreas: ('I'|'Q'|'M')[] | undefined =
                  formType === 'button' ? ['Q', 'M'] :
                  formType === 'lamp' ? ['I', 'Q', 'M'] :
                  undefined
                return (
                <VariablePicker
                  dbName={parsed.dbName}
                  varName={parsed.varName}
                  importedDBs={importedDBs}
                  ioAreas={ioAreas}
                  onChange={(dbName, varName) => {
                    setFormCfg(c => {
                      const isIO = dbName === 'I' || dbName === 'Q' || dbName === 'M'
                      const fullName = isIO ? `${dbName}${varName}` : dbName ? `${dbName}:${varName}` : varName
                      const labelDefault = CONFIG_FIELDS[formType]?.find(f => f.key === 'label')?.default
                      const isLabelDefault = !c.label || c.label === labelDefault
                      return {
                        ...c,
                        variableName: fullName,
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
                )
              })()}

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
      {helpWidget && (
        <div className="modal-overlay" onMouseDown={e => { if (e.target === e.currentTarget) setHelpWidget(null) }}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ width: 420 }}>
            <h3 className="modal-title">❓ {WIDGET_META[helpWidget]?.icon} {WIDGET_META[helpWidget]?.label} 帮助</h3>
            <div className="modal-form">
              <div style={{ fontSize: 13, lineHeight: 1.8, color: 'var(--foreground)', whiteSpace: 'pre-wrap' }}>{WIDGET_HELP[helpWidget] || '暂无帮助说明'}</div>
            </div>
            <div className="modal-actions">
              <button className="btn btn--primary" onClick={() => setHelpWidget(null)}>知道了</button>
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

/** 解析变量名为 { dbName, varName }，支持 I/Q/M 格式（Q8.6 → {dbName:'Q', varName:'8.6'}）*/
function parseVarName(v: string): { dbName: string; varName: string } {
  const ioMatch = v.match(/^([IQM])(\d+\.\d+)$/)
  if (ioMatch) return { dbName: ioMatch[1], varName: ioMatch[2] }
  const parts = v.split(':')
  return { dbName: parts[0] || '', varName: parts.slice(1).join(':') }
}

/** 带搜索过滤的变量选择器：选 DB → 搜变量名 → 点击选中。支持 I/Q/M 区 */
function VariablePicker({ dbName, varName, importedDBs, onChange, ioAreas }: {
  dbName: string; varName: string; importedDBs: { dbNumber: number; dbName: string }[]
  onChange: (dbName: string, varName: string) => void
  ioAreas?: ('I' | 'Q' | 'M')[]
}) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState(varName)
  const [pos, setPos] = useState({ top: 0, left: 0, width: 0 })
  const inputRef = useRef<HTMLInputElement>(null)
  const wrapRef = useRef<HTMLDivElement>(null)

  const isIOArea = dbName === 'I' || dbName === 'Q' || dbName === 'M'

  // 当前选中的 DB 下的所有变量（I/Q/M 区没有预定义变量列表）
  const allVars = useMemo(() => {
    if (!dbName || isIOArea) return []
    const dbs = loadAllDBData()
    const db = dbs.find(d => d.dbName === dbName)
    return db?.variables ?? []
  }, [dbName, isIOArea])

  // 按搜索词过滤（I/Q/M 区直接显示输入内容）
  const filtered = useMemo(() => {
    if (isIOArea) return []
    if (!search) return allVars
    const q = search.toLowerCase()
    return allVars.filter(v => v.name.toLowerCase().includes(q))
  }, [allVars, search, isIOArea])

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
          {ioAreas && ioAreas.length > 0 && (
            <>
              <option disabled style={{ fontSize: 10, color: 'var(--text-muted)' }}>──────────</option>
              {ioAreas.map(a => <option key={a} value={a}>{a} 区</option>)}
            </>
          )}
        </select>
        <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 4 }}>
          <input ref={inputRef} className="modal-input" style={{ width: '100%' }}
            placeholder={!dbName ? '先选择 DB 或 I/Q/M' : isIOArea ? '输入 字节.位，如 8.6' : '搜索变量名...'}
            value={dbName ? search : ''} disabled={!dbName}
            onFocus={openDropdown}
            onInput={openDropdown}
            onChange={e => { setSearch(e.target.value); openDropdown() }}
            onKeyDown={e => {
              if (e.key === 'Escape') setOpen(false)
              if (e.key === 'Enter') {
                if (isIOArea && /^\d+\.\d+$/.test(search.trim())) {
                  onChange(dbName, search.trim())
                  setOpen(false)
                } else if (filtered.length === 1) {
                  onChange(dbName, filtered[0].name)
                  setSearch(filtered[0].name)
                  setOpen(false)
                }
              }
            }}
            onBlur={() => {
              // I/Q/M 区：输入有效格式后自动确认
              if (isIOArea && /^\d+\.\d+$/.test(search.trim())) {
                onChange(dbName, search.trim())
              }
            }} />
          {search && <button className="modal-clear-btn" onClick={() => { setSearch(''); onChange(dbName, ''); }} title="清空">✕</button>}
        </div>
      </div>
      {open && dbName && !isIOArea && (
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

/** 响应式容器：ResizeObserver 监测尺寸变化，强制子元素撑满 */
function ResizeWrapper({ children }: { children: React.ReactNode }) {
  const ref = useRef<HTMLDivElement>(null)
  const [, setTick] = useState(0)
  useEffect(() => {
    const el = ref.current
    if (!el) return
    let raf: number
    const ro = new ResizeObserver(entries => {
      cancelAnimationFrame(raf)
      raf = requestAnimationFrame(() => {
        // 强制刷新，让子组件的 useEffect 重新计算尺寸
        setTick(t => t + 1)
      })
    })
    ro.observe(el)
    return () => { ro.disconnect(); cancelAnimationFrame(raf) }
  }, [])
  return <div ref={ref} style={{ width: '100%', height: '100%', minHeight: 0, overflow: 'hidden' }}>{children}</div>
}

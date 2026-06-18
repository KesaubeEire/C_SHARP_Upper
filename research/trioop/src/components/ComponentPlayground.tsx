import { useState, useRef, useEffect } from 'react'
import CollapsibleSection from './CollapsibleSection'
import { Gauge, SignalPanel, EventLog, Attitude } from '@altara/core'
import { OEEDashboard, MotorDashboard, PredictiveMaintenanceGauge, AlarmAnnunciatorPanel, TrendRecorder, WaterfallSpectrogram, PIDTuningPanel, PIDNode, ProcessFlowDiagram } from '@altara/industrial'

interface PropDef {
  key: string; label: string; type: 'number' | 'text' | 'select' | 'boolean' | 'range'
  default: any; options?: { label: string; value: any }[]; min?: number; max?: number; step?: number
}

interface CompDef {
  id: string; name: string; desc: string; props: PropDef[]; aspect?: 'wide' | 'tall' | 'square'
}

const COMPONENTS: CompDef[] = [
  {
    id: 'gauge', name: 'Gauge 表盘', desc: '270° SVG 模拟表盘', aspect: 'square',
    props: [
      { key: 'value', label: '手动值', type: 'number', default: 50 },
      { key: 'min', label: '量程下限', type: 'number', default: 0 },
      { key: 'max', label: '量程上限', type: 'number', default: 100 },
      { key: 'unit', label: '单位后缀', type: 'text', default: '%' },
      { key: 'label', label: '表盘标签', type: 'text', default: '负载' },
      { key: 'size', label: '尺寸', type: 'select', default: 'md', options: [{ label: '小 120px', value: 'sm' }, { label: '中 180px', value: 'md' }, { label: '大 240px', value: 'lg' }] },
      { key: 'threshold1', label: '阈值 1(值)', type: 'number', default: 80 },
      { key: 'threshold1Color', label: '阈值 1 色', type: 'select', default: 'warn', options: [{ label: '橙(警告)', value: 'warn' }, { label: '红(危险)', value: 'danger' }, { label: '蓝(信息)', value: 'info' }, { label: '绿(正常)', value: 'active' }] },
      { key: 'threshold2', label: '阈值 2(值)', type: 'number', default: 90 },
      { key: 'threshold2Color', label: '阈值 2 色', type: 'select', default: 'danger', options: [{ label: '橙(警告)', value: 'warn' }, { label: '红(危险)', value: 'danger' }, { label: '蓝(信息)', value: 'info' }, { label: '绿(正常)', value: 'active' }] },
    ],
  },
  {
    id: 'oee', name: 'OEE Dashboard', desc: '设备综合效率', aspect: 'wide',
    props: [
      { key: 'availability', label: '可用率', type: 'range', default: 0.85, min: 0, max: 1, step: 0.01 },
      { key: 'performance', label: '性能率', type: 'range', default: 0.78, min: 0, max: 1, step: 0.01 },
      { key: 'quality', label: '质量率', type: 'range', default: 0.95, min: 0, max: 1, step: 0.01 },
      { key: 'oeeTarget', label: 'OEE 目标线', type: 'range', default: 0.85, min: 0, max: 1, step: 0.01 },
      { key: 'shift', label: '班次标签', type: 'text', default: 'A' },
      { key: 'loss1Cat', label: '损失 1 类别', type: 'text', default: '换型' },
      { key: 'loss1Min', label: '损失 1 分钟', type: 'number', default: 45 },
      { key: 'loss2Cat', label: '损失 2 类别', type: 'text', default: '停机' },
      { key: 'loss2Min', label: '损失 2 分钟', type: 'number', default: 32 },
      { key: 'loss3Cat', label: '损失 3 类别', type: 'text', default: '减速' },
      { key: 'loss3Min', label: '损失 3 分钟', type: 'number', default: 18 },
    ],
  },
  {
    id: 'motor', name: 'Motor Dashboard', desc: '电机仪表盘', aspect: 'wide',
    props: [
      { key: 'rpm', label: '转速 RPM', type: 'number', default: 2850 },
      { key: 'torque', label: '扭矩 Nm', type: 'number', default: 42 },
      { key: 'current', label: '电流 A', type: 'number', default: 38 },
      { key: 'temperature', label: '温度 °C', type: 'number', default: 72 },
      { key: 'ratedRPM', label: '额定转速', type: 'number', default: 3000 },
      { key: 'ratedCurrent', label: '额定电流 A', type: 'number', default: 50 },
      { key: 'fault1', label: '故障 1 代码', type: 'text', default: 'OVT' },
      { key: 'fault1Desc', label: '故障 1 描述', type: 'text', default: '过温' },
      { key: 'fault2', label: '故障 2 代码', type: 'text', default: 'OVL' },
      { key: 'fault2Desc', label: '故障 2 描述', type: 'text', default: '过载' },
    ],
  },
  {
    id: 'predictive', name: 'Predictive Maintenance', desc: '预测维护', aspect: 'square',
    props: [
      { key: 'healthScore', label: '健康指数', type: 'range', default: 74, min: 0, max: 100, step: 1 },
      { key: 'rulDays', label: '剩余寿命(天)', type: 'number', default: 45 },
      { key: 'confidence', label: '置信区间(±天)', type: 'number', default: 12 },
      { key: 'size', label: '尺寸', type: 'select', default: 'md', options: [{ label: '小', value: 'sm' }, { label: '中', value: 'md' }, { label: '大', value: 'lg' }] },
      { key: 'lastMaint', label: '上次维护(ISO)', type: 'text', default: '2026-06-01' },
      { key: 'nextSched', label: '下次维护(ISO)', type: 'text', default: '2026-07-15' },
    ],
  },
  {
    id: 'alarm', name: 'Alarm Annunciator', desc: '报警瓷砖面板', aspect: 'wide',
    props: [
      { key: 'columns', label: '列数', type: 'range', default: 4, min: 2, max: 8, step: 1 },
      { key: 'flashRate', label: '闪烁频率 Hz', type: 'range', default: 2, min: 0.5, max: 5, step: 0.5 },
      { key: 'groupBy', label: '分组字段', type: 'text', default: '' },
    ],
  },
  {
    id: 'trend', name: 'Trend Recorder', desc: '趋势记录仪', aspect: 'wide',
    props: [
      { key: 'timeScale', label: '时间刻度', type: 'select', default: '5m', options: [{ label: '1 分钟', value: '1m' }, { label: '5 分钟', value: '5m' }, { label: '15 分钟', value: '15m' }, { label: '1 小时', value: '1h' }, { label: '4 小时', value: '4h' }, { label: '8 小时', value: '8h' }, { label: '24 小时', value: '24h' }] },
      { key: 'showGrid', label: '显示网格', type: 'boolean', default: true },
      { key: 'showLegend', label: '显示图例', type: 'boolean', default: true },
      { key: 'bgColor', label: '背景色', type: 'text', default: '' },
    ],
  },
  {
    id: 'pid', name: 'PID 调谐面板', desc: 'PID 控制器可视化', aspect: 'wide',
    props: [
      { key: 'kp', label: 'Kp 比例增益', type: 'number', default: 2.5 },
      { key: 'ki', label: 'Ki 积分增益', type: 'number', default: 0.8 },
      { key: 'kd', label: 'Kd 微分增益', type: 'number', default: 0.3 },
      { key: 'errorBand', label: '误差带', type: 'number', default: 5 },
      { key: 'unit', label: '工程单位', type: 'text', default: '°C' },
      { key: 'windowMs', label: '时间窗(ms)', type: 'number', default: 30000 },
    ],
  },
  {
    id: 'spectrogram', name: '频谱瀑布图', desc: 'FFT 实时频谱', aspect: 'wide',
    props: [
      { key: 'fftSize', label: 'FFT 窗口', type: 'select', default: 1024, options: [{ label: '256 点', value: 256 }, { label: '512 点', value: 512 }, { label: '1024 点', value: 1024 }, { label: '2048 点', value: 2048 }] },
      { key: 'sampleRate', label: '采样率 Hz', type: 'number', default: 44100 },
      { key: 'freqMin', label: '最小频率 Hz', type: 'number', default: 0 },
      { key: 'freqMax', label: '最大频率 Hz', type: 'number', default: 5000 },
      { key: 'colorMap', label: '配色方案', type: 'select', default: 'heat', options: [{ label: '热量', value: 'heat' }, { label: '彩云', value: 'viridis' }, { label: '等离子', value: 'plasma' }, { label: '灰度', value: 'grayscale' }] },
      { key: 'scrollRate', label: '滚动速率 fps', type: 'range', default: 30, min: 5, max: 60, step: 1 },
      { key: 'dbRangeLow', label: 'dB 下限', type: 'number', default: -60 },
      { key: 'dbRangeHigh', label: 'dB 上限', type: 'number', default: 0 },
      { key: 'width', label: '画布宽 px', type: 'number', default: 520 },
      { key: 'height', label: '画布高 px', type: 'number', default: 320 },
    ],
  },
  {
    id: 'pidnode', name: 'P&ID 仪表符号', desc: 'ISA 5.1 仪表气泡', aspect: 'square',
    props: [
      { key: 'firstLetter', label: '被测变量', type: 'select', default: 'F', options: [{ label: 'F 流量', value: 'F' }, { label: 'T 温度', value: 'T' }, { label: 'P 压力', value: 'P' }, { label: 'L 液位', value: 'L' }] },
      { key: 'functionLetters', label: '功能字母', type: 'select', default: 'IC', options: [{ label: 'I 指示', value: 'I' }, { label: 'IC 指示控制', value: 'IC' }, { label: 'T 变送', value: 'T' }, { label: 'TR 记录变送', value: 'TR' }] },
      { key: 'location', label: '安装位置', type: 'select', default: 'field', options: [{ label: '现场', value: 'field' }, { label: '面板', value: 'panel' }, { label: 'DCS', value: 'dcs' }] },
      { key: 'value', label: '过程值', type: 'number', default: 72.5 },
      { key: 'unit', label: '工程单位', type: 'text', default: '°C' },
      { key: 'status', label: '状态', type: 'select', default: 'normal', options: [{ label: '正常', value: 'normal' }, { label: '警告', value: 'warning' }, { label: '报警', value: 'alarm' }, { label: '离线', value: 'offline' }] },
      { key: 'size', label: '符号尺寸 px', type: 'range', default: 60, min: 30, max: 120, step: 5 },
    ],
  },
  {
    id: 'signal', name: 'Signal Panel', desc: '信号值面板', aspect: 'square',
    props: [
      { key: 'staleAfterMs', label: '超时判定 ms', type: 'number', default: 5000 },
      { key: 'columns', label: '网格列数', type: 'range', default: 1, min: 1, max: 3, step: 1 },
      { key: 'ch1Label', label: '通道 1 标签', type: 'text', default: '电机电流' },
      { key: 'ch1Val', label: '通道 1 数值', type: 'number', default: 38 },
      { key: 'ch1Unit', label: '通道 1 单位', type: 'text', default: 'A' },
      { key: 'ch2Label', label: '通道 2 标签', type: 'text', default: '绕组温度' },
      { key: 'ch2Val', label: '通道 2 数值', type: 'number', default: 72 },
      { key: 'ch2Unit', label: '通道 2 单位', type: 'text', default: '°C' },
      { key: 'ch2Warn', label: '通道 2 警告值', type: 'number', default: 80 },
      { key: 'ch2Danger', label: '通道 2 危险值', type: 'number', default: 100 },
      { key: 'ch2Dir', label: '通道 2 方向', type: 'select', default: 'above', options: [{ label: '超过阈值触发', value: 'above' }, { label: '低于阈值触发', value: 'below' }] },
      { key: 'ch3Label', label: '通道 3 标签', type: 'text', default: '振动' },
      { key: 'ch3Val', label: '通道 3 数值', type: 'number', default: 4.2 },
      { key: 'ch3Unit', label: '通道 3 单位', type: 'text', default: 'mm/s' },
      { key: 'ch3Warn', label: '通道 3 警告值', type: 'number', default: 5 },
      { key: 'ch3Danger', label: '通道 3 危险值', type: 'number', default: 10 },
      { key: 'ch3Dir', label: '通道 3 方向', type: 'select', default: 'above', options: [{ label: '超过阈值触发', value: 'above' }, { label: '低于阈值触发', value: 'below' }] },
    ],
  },
  {
    id: 'eventlog', name: 'Event Log', desc: '事件日志', aspect: 'tall',
    props: [
      { key: 'maxEntries', label: '最大显示行数', type: 'range', default: 100, min: 10, max: 500, step: 10 },
      { key: 'filter', label: '严重级别过滤', type: 'select', default: 'all', options: [{ label: '全部显示', value: 'all' }, { label: '警告+错误', value: 'warn' }, { label: '仅错误', value: 'error' }] },
    ],
  },
  {
    id: 'attitude', name: '姿态仪', desc: '人工地平仪', aspect: 'square',
    props: [
      { key: 'roll', label: '横滚角 °', type: 'range', default: 5, min: -180, max: 180, step: 1 },
      { key: 'pitch', label: '俯仰角 °', type: 'range', default: 3, min: -90, max: 90, step: 1 },
      { key: 'size', label: '显示尺寸 px', type: 'number', default: 220 },
    ],
  },
  {
    id: 'pfd', name: '工艺流程图', desc: 'P&ID 流程图', aspect: 'wide',
    props: [
      { key: 'width', label: '画布宽度', type: 'number', default: 600 },
      { key: 'height', label: '画布高度', type: 'number', default: 300 },
      { key: 'interactive', label: '交互模式(悬停)', type: 'boolean', default: false },
    ],
  },
]

function PropEditor({ def, value, onChange }: { def: PropDef; value: any; onChange: (v: any) => void }) {
  if (def.type === 'boolean') {
    return (
      <label className="cplay-prop cplay-prop--row">
        <input type="checkbox" checked={!!value} onChange={e => onChange(e.target.checked)} />
        <span>{def.label}</span>
      </label>
    )
  }
  if (def.type === 'select') {
    return (
      <div className="cplay-prop">
        <label className="cplay-prop__label">{def.label}</label>
        <select className="cplay-prop__select" value={value} onChange={e => onChange(e.target.value)}>
          {def.options?.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </div>
    )
  }
  if (def.type === 'range') {
    return (
      <div className="cplay-prop">
        <label className="cplay-prop__label">{def.label}: <strong>{value}</strong></label>
        <input type="range" min={def.min ?? 0} max={def.max ?? 100} step={def.step ?? 1} value={value} onChange={e => onChange(Number(e.target.value))} className="cplay-prop__range" />
      </div>
    )
  }
  return (
    <div className="cplay-prop">
      <label className="cplay-prop__label">{def.label}</label>
      <input className="cplay-prop__input" type={def.type === 'number' ? 'number' : 'text'} value={value} onChange={e => onChange(def.type === 'number' ? Number(e.target.value) : e.target.value)} />
    </div>
  )
}

function renderComponent(id: string, p: Record<string, any>, mockMode: boolean) {
  switch (id) {
    case 'gauge': {
      const thresholds = []
      if (p.threshold1) thresholds.push({ value: p.threshold1, color: p.threshold1Color === 'warn' ? '#EF9F27' : p.threshold1Color === 'danger' ? '#E24B4A' : p.threshold1Color === 'info' ? '#378ADD' : '#1D9E75' })
      if (p.threshold2) thresholds.push({ value: p.threshold2, color: p.threshold2Color === 'warn' ? '#EF9F27' : p.threshold2Color === 'danger' ? '#E24B4A' : p.threshold2Color === 'info' ? '#378ADD' : '#1D9E75' })
      const useManual = p.value !== undefined && p.value !== null
      const ds = useManual ? { subscribe: (cb: any) => { const t = setInterval(() => cb({ timestamp: Date.now(), value: p.value }), 100); return () => clearInterval(t) }, getHistory: () => [{ timestamp: Date.now(), value: p.value }], status: 'connected' as const, destroy: () => {} } : undefined
      return <Gauge min={p.min} max={p.max} unit={p.unit} label={p.label} size={p.size} thresholds={thresholds} dataSource={ds} mockMode={!useManual && mockMode} />
    }
    case 'oee': {
      const lossCategories = []
      if (p.loss1Cat) lossCategories.push({ category: p.loss1Cat, minutes: p.loss1Min })
      if (p.loss2Cat) lossCategories.push({ category: p.loss2Cat, minutes: p.loss2Min })
      if (p.loss3Cat) lossCategories.push({ category: p.loss3Cat, minutes: p.loss3Min })
      return <OEEDashboard availability={p.availability} performance={p.performance} quality={p.quality} oeeTarget={p.oeeTarget} shift={p.shift} lossCategories={lossCategories} mockMode={mockMode} />
    }
    case 'motor': {
      const faults = []
      if (p.fault1) faults.push({ code: p.fault1, description: p.fault1Desc || '', timestamp: Date.now() })
      if (p.fault2) faults.push({ code: p.fault2, description: p.fault2Desc || '', timestamp: Date.now() })
      return <MotorDashboard rpm={p.rpm} torque={p.torque} current={p.current} temperature={p.temperature} ratedRPM={p.ratedRPM} ratedCurrent={p.ratedCurrent} faults={faults} mockMode={mockMode} />
    }
    case 'predictive':
      return <PredictiveMaintenanceGauge healthScore={p.healthScore} rulDays={p.rulDays} confidence={p.confidence} size={p.size} lastMaintenance={p.lastMaint} nextScheduled={p.nextSched} mockMode={mockMode} />
    case 'alarm':
      return <AlarmAnnunciatorPanel columns={p.columns} flashRate={p.flashRate} groupBy={p.groupBy || undefined} mockMode={mockMode} />
    case 'trend':
      return <TrendRecorder timeScale={p.timeScale} showGrid={p.showGrid} showLegend={p.showLegend} backgroundColor={p.bgColor || undefined} mockMode={mockMode} />
    case 'pid':
      return <PIDTuningPanel kp={p.kp} ki={p.ki} kd={p.kd} errorBand={p.errorBand} unit={p.unit} windowMs={p.windowMs} mockMode={mockMode} />
    case 'spectrogram': {
      const dbRange = p.dbRangeLow !== undefined && p.dbRangeHigh !== undefined ? [p.dbRangeLow, p.dbRangeHigh] : undefined
      return <WaterfallSpectrogram fftSize={p.fftSize} sampleRate={p.sampleRate} freqMin={p.freqMin} freqMax={p.freqMax} colorMap={p.colorMap} dbRange={dbRange as [number, number] | undefined} scrollRate={p.scrollRate} width={p.width || 520} height={p.height || 320} mockMode={mockMode} />
    }
    case 'pidnode':
      return <PIDNode firstLetter={p.firstLetter} functionLetters={p.functionLetters} location={p.location} value={p.value} unit={p.unit} status={p.status} size={p.size} />
    case 'signal':
      return <SignalPanel staleAfterMs={p.staleAfterMs} columns={p.columns} signals={[
        { key: 'ch1', label: p.ch1Label || 'CH1', value: p.ch1Val, unit: p.ch1Unit },
        { key: 'ch2', label: p.ch2Label || 'CH2', value: p.ch2Val, unit: p.ch2Unit, warnAt: p.ch2Warn, dangerAt: p.ch2Danger, thresholdDirection: p.ch2Dir || 'above' },
        { key: 'ch3', label: p.ch3Label || 'CH3', value: p.ch3Val, unit: p.ch3Unit, warnAt: p.ch3Warn, dangerAt: p.ch3Danger, thresholdDirection: p.ch3Dir || 'above' },
      ] as any} />
    case 'eventlog':
      return <EventLog entries={[
        { timestamp: Date.now() - 5000, message: '系统启动完成', severity: 'info' },
        { timestamp: Date.now() - 4000, message: '电机温度超过警告线 (82°C)', severity: 'warn' },
        { timestamp: Date.now() - 3000, message: '振动传感器检测到异常', severity: 'warn' },
        { timestamp: Date.now() - 2000, message: '通信超时 - 从站 #3 无响应', severity: 'error' },
        { timestamp: Date.now() - 1000, message: '紧急停止按钮被触发', severity: 'error' },
        { timestamp: Date.now(), message: '系统已恢复正常', severity: 'info' },
      ]} maxEntries={p.maxEntries} filter={p.filter} />
    case 'attitude':
      return <Attitude roll={p.roll} pitch={p.pitch} size={p.size} mockMode={mockMode} />
    case 'pfd':
      return <ProcessFlowDiagram nodes={[
        { id: 't1', type: 'tank', x: 20, y: 80, label: 'TK-101' },
        { id: 'p1', type: 'pump', x: 140, y: 90, label: 'P-101' },
        { id: 'v1', type: 'valve', x: 240, y: 95, label: 'HV-101' },
        { id: 'he1', type: 'heat-exchanger', x: 340, y: 85, label: 'E-101' },
        { id: 't2', type: 'tank', x: 480, y: 80, label: 'TK-102' },
      ]} edges={[
        { from: 't1', to: 'p1' }, { from: 'p1', to: 'v1' },
        { from: 'v1', to: 'he1' }, { from: 'he1', to: 't2' },
      ]} values={{ t1: 85, p1: 1450, v1: 62, t2: 120 }} width={p.width} height={p.height} interactive={p.interactive} />
    default:
      return null
  }
}

export default function ComponentPlayground() {
  const [selected, setSelected] = useState(COMPONENTS[0].id)
  const [isMock, setIsMock] = useState(true)
  const [zoom, setZoom] = useState(1)
  const [pan, setPan] = useState({ x: 0, y: 0 })
  const dragRef = useRef({ startX: 0, startY: 0, origX: 0, origY: 0, dragging: false })
  const previewRef = useRef<HTMLDivElement>(null)
  const [propValues, setPropValues] = useState<Record<string, Record<string, any>>>(() => {
    const init: Record<string, Record<string, any>> = {}
    for (const comp of COMPONENTS) { init[comp.id] = {}; for (const p of comp.props) init[comp.id][p.key] = p.default }
    return init
  })

  useEffect(() => {
    const el = previewRef.current
    if (!el) return
    const handler = (e: WheelEvent) => {
      if (!e.ctrlKey && !e.metaKey) return
      e.preventDefault()
      setZoom(z => Math.max(0.3, Math.min(3, z * (e.deltaY > 0 ? 0.92 : 1.08))))
    }
    el.addEventListener('wheel', handler, { passive: false })
    return () => el.removeEventListener('wheel', handler)
  }, [])

  const current = COMPONENTS.find(c => c.id === selected)!
  const vals = propValues[selected] || {}

  const handleMouseDown = (e: React.MouseEvent) => {
    if (e.button !== 0) return
    dragRef.current = { startX: e.clientX, startY: e.clientY, origX: pan.x, origY: pan.y, dragging: true }
  }
  const handleMouseMove = (e: React.MouseEvent) => {
    if (!dragRef.current.dragging) return
    setPan({ x: dragRef.current.origX + (e.clientX - dragRef.current.startX) / zoom, y: dragRef.current.origY + (e.clientY - dragRef.current.startY) / zoom })
  }
  const handleMouseUp = () => { dragRef.current.dragging = false }

  return (
    <CollapsibleSection title="🧪 组件实验室" storageKey="component-lab" style={{ marginBottom: 24 }}>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12 }}>
        <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>共 {COMPONENTS.length} 个组件 — 点击左侧选择，右侧调整参数</span>
        <button className={`btn btn--sm ${isMock ? 'btn--primary' : 'btn--ghost'}`} onClick={() => setIsMock(!isMock)}>
          {isMock ? '🎭 模拟数据' : '📡 实时数据'}
        </button>
        <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>缩放: {Math.round(zoom * 100)}%</span>
        <button className="btn btn--ghost btn--sm" onClick={() => { setZoom(1); setPan({ x: 0, y: 0 }) }}>重置</button>
      </div>
      <div className="cplay-layout">
        <div className="cplay-sidebar" style={{ width: 200 }}>
          {COMPONENTS.map(c => (
            <button key={c.id} className={`cplay-nav ${selected === c.id ? 'cplay-nav--active' : ''}`} onClick={() => setSelected(c.id)}>
              <span className="cplay-nav__name">{c.name}</span>
              <span className="cplay-nav__desc">{c.desc}</span>
            </button>
          ))}
        </div>
        <div ref={previewRef} className="cplay-preview" style={{ minHeight: current.aspect === 'tall' ? 420 : current.aspect === 'wide' ? 380 : 320, overflow: 'hidden', cursor: dragRef.current.dragging ? 'grabbing' : 'grab', position: 'relative' }}
          onMouseDown={handleMouseDown} onMouseMove={handleMouseMove} onMouseUp={handleMouseUp} onMouseLeave={handleMouseUp}>
          <div className="cplay-preview__inner" style={{ maxWidth: current.aspect === 'square' ? 360 : '100%', transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})`, transformOrigin: 'center center' }}>
            {renderComponent(selected, vals, isMock)}
          </div>
          <div style={{ position: 'absolute', bottom: 8, left: 8, fontSize: 11, color: 'var(--text-muted)', pointerEvents: 'none' }}>Ctrl+滚轮缩放 · 拖拽平移</div>
        </div>
        <div className="cplay-props" style={{ width: 240, maxHeight: 500, overflowY: 'auto' }}>
          <div className="cplay-props__title">属性配置</div>
          {current.props.map(p => (
            <PropEditor key={p.key} def={p} value={vals[p.key] ?? p.default} onChange={v => setPropValues(pv => ({ ...pv, [selected]: { ...pv[selected], [p.key]: v } }))} />
          ))}
        </div>
      </div>
    </CollapsibleSection>
  )
}

import { useEffect, useState, useCallback } from 'react'
import { usePLCData } from './hooks/usePLCData'
import { usePLCWrite } from './hooks/usePLCWrite'
import StatusBar from './components/StatusBar'
import PLCGrid from './components/PLCGrid'
import ConnectionPanel from './components/ConnectionPanel'
import IOGrid from './components/IOGrid'
import DBBlockPanel from './components/DBBlockPanel'
import DBImportPanel from './components/DBImportPanel'
import TrendChart from './components/TrendChart'
import AlarmPanel from './components/AlarmPanel'
import Dashboard from './components/Dashboard'
import VisualDashboard from './components/VisualDashboard'
import ComponentPlayground from './components/ComponentPlayground'
import DiagnosticsPanel from './components/DiagnosticsPanel'
import RecipePanel from './components/RecipePanel'
import AlarmAnnunciator from './components/AlarmAnnunciator'
import { OEEDashboard, MotorDashboard, PredictiveMaintenanceGauge, AlarmAnnunciatorPanel, TrendRecorder } from '@altara/industrial'
import { Gauge } from '@altara/core'
import type { PLCConfig } from '../shared/types'

interface DBBlockConfig {
  label: string
  dbNumber: number
  startOffset: number
  byteCount: number
}

/** 将后端 {start,end}[] 范围展开为 flat 字节数组 */
function rangesToBytes(ranges?: { start: number; end: number }[]): number[] {
  if (!ranges || ranges.length === 0) return [0, 1, 8]
  const bytes = new Set<number>()
  for (const r of ranges) {
    for (let b = r.start; b <= r.end; b++) bytes.add(b)
  }
  return [...bytes].sort((a, b) => a - b)
}

export default function App() {
  const { db, io, setIo, dbBlocks, connected, lastDataTime } = usePLCData()
  const { write, states, dismissError } = usePLCWrite()
  const [config, setConfig] = useState<PLCConfig | null>(null)
  const [blocks, setBlocks] = useState<DBBlockConfig[]>([])
  const [showAltara, setShowAltara] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth > 768)
  useEffect(() => {
    const handler = () => setSidebarOpen(false)
    window.addEventListener('close-sidebar', handler)
    return () => window.removeEventListener('close-sidebar', handler)
  }, [])

  // 启动时加载配置
  useEffect(() => {
    fetch('/api/plc/config').then(r => r.json()).then(setConfig).catch(() => {})
    fetch('/api/plc/db-blocks').then(r => r.json()).then(setBlocks).catch(() => {})
  }, [])

  const handleQToggle = useCallback(async (byteAddr: number, bit: number, value: boolean) => {
    // 乐观更新：先改界面
    setIo(prev => {
      const q = { ...prev.q }
      const oldByte = q[byteAddr] ?? 0
      q[byteAddr] = value ? (oldByte | (1 << bit)) : (oldByte & ~(1 << bit))
      return { ...prev, q }
    })
    // 后台写 PLC（带当前字节值，直接写整字节）
    const currentByte = io.q[byteAddr] ?? 0
    try {
      await fetch('/api/plc/write-io', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ area: 'q', byte: byteAddr, bit, value, currentByte }),
      })
    } catch {}
  }, [setIo, io.q])

  const addBlock = useCallback(async (block: DBBlockConfig) => {
    try {
      const res = await fetch('/api/plc/db-blocks', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(block),
      })
      setBlocks(await res.json())
    } catch {}
  }, [])

  const removeBlock = useCallback(async (label: string) => {
    try {
      const res = await fetch(`/api/plc/db-blocks/${encodeURIComponent(label)}`, { method: 'DELETE' })
      setBlocks(await res.json())
    } catch {}
  }, [])

  // 键盘快捷键
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'F11') { e.preventDefault(); document.documentElement.requestFullscreen?.() }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [])

  const variables = config?.variables ?? []
  const pointCount = Object.keys(db).length

  // I/Q 字节地址：localStorage 最优先（用户在 ConnectionPanel 改了即时生效），无则 fallback 到后端 config
  const ioRanges = (() => {
    try {
      const raw = localStorage.getItem('trioop_connection')
      if (raw) {
        const s = JSON.parse(raw)
        if (s.ioIBytes || s.ioQBytes) {
          const toRanges = (v: string) => {
            if (!v) return undefined
            const nums = v.split(',').map(n => parseInt(n.trim())).filter(n => !isNaN(n))
            if (nums.length === 0) return undefined
            const sorted = [...new Set(nums)].sort((a, b) => a - b)
            const r: { start: number; end: number }[] = []
            let st = sorted[0], en = sorted[0]
            for (let i = 1; i < sorted.length; i++) {
              if (sorted[i] === en + 1) { en = sorted[i] }
              else { r.push({ start: st, end: en }); st = en = sorted[i] }
            }
            r.push({ start: st, end: en })
            return r
          }
          return { i: toRanges(s.ioIBytes), q: toRanges(s.ioQBytes) }
        }
      }
    } catch { /* ignore */ }
    return config?.ioRanges
  })()
  const ioBytes = { i: rangesToBytes(ioRanges?.i), q: rangesToBytes(ioRanges?.q) }

  return (
    <div className="app app--with-sidebar">
      <div className={`sidebar-wrapper${sidebarOpen ? '' : ' sidebar-wrapper--closed'}`}>
        <ConnectionPanel />
      </div>
      <div className="app__main">
        <StatusBar config={config} connected={connected} pointCount={pointCount} lastDataTime={lastDataTime} sidebarOpen={sidebarOpen} onToggleSidebar={() => setSidebarOpen(!sidebarOpen)} />
        <button className="btn btn--ghost btn--sm" style={{ position: 'fixed', bottom: 8, right: 8, zIndex: 100, fontSize: 11 }} onClick={() => setShowAltara(!showAltara)}>
          {showAltara ? '关闭演示' : '组件演示'}
        </button>
        <main className="main">
          {variables.length > 0 && (
            <section className="section">
              <h2 className="section__title">📊 状态变量</h2>
              <PLCGrid
                variables={variables}
                data={db}
                writeStates={states}
                onWrite={write}
                onDismissError={dismissError}
              />
            </section>
          )}

          <section className="section">
            <IOGrid label="🟡 输入点 (I 区)" data={io.i} prefix="I" bytes={ioBytes.i} />
          </section>

          <section className="section">
            <IOGrid label="🔵 输出点 (Q 区)" data={io.q} prefix="Q" bytes={ioBytes.q} onToggle={handleQToggle} />
          </section>

          {/* 实时趋势 */}
          <section className="section">
            <TrendChart variables={variables.map(v => v.name)} liveData={db} timeRange={300} />
          </section>

          {/* 报警面板 */}
          <AlarmPanel />

          {/* 报警面板（瓷砖式） */}
          <AlarmAnnunciator
            alarms={[
              { id: 'fault', label: '设备故障', priority: 1 },
              { id: 'overtemp', label: '超温', priority: 1 },
              { id: 'overload', label: '过载', priority: 2 },
              { id: 'lowflow', label: '流量低', priority: 2 },
              { id: 'maintenance', label: '维护提醒', priority: 3 },
            ]}
            states={{}}
            columns={5}
          />

          {/* 可视化仪表盘 */}
          <VisualDashboard liveData={db} />

          {/* 组件实验室 */}
          <div style={{ textAlign: 'right', marginBottom: 8 }}>
            <button className="btn btn--ghost btn--sm" onClick={() => setShowAltara(!showAltara)}>
              {showAltara ? '✕ 关闭实验室' : '🧪 组件实验室'}
            </button>
          </div>
          {showAltara && <ComponentPlayground />}

          {/* 系统诊断 */}
          <DiagnosticsPanel />

          {/* 配方管理 */}
          <RecipePanel liveData={db} />

          <DBImportPanel onImport={() => {}} liveData={db} />
          <DBBlockPanel blocks={blocks} data={dbBlocks} onAdd={addBlock} onRemove={removeBlock} />
        </main>
      </div>
    </div>
  )
}

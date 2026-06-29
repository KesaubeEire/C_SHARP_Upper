import { useEffect, useState, useCallback, useRef } from 'react'
import { usePLCData } from './hooks/usePLCData'
import { usePLCWrite } from './hooks/usePLCWrite'
import { reregisterAllDBs } from './hooks/useDBMapping'
import StatusBar from './components/StatusBar'
import ConnectionPanel from './components/ConnectionPanel'
import IOGrid from './components/IOGrid'
import DBImportPanel from './components/DBImportPanel'
import Dashboard from './components/Dashboard'
import VisualDashboard from './components/VisualDashboard'
import ComponentPlayground from './components/ComponentPlayground'
import DiagnosticsPanel from './components/DiagnosticsPanel'
import EventLogPanel from './components/EventLogPanel'
import AlarmPanel from './components/AlarmPanel'
import CollapsibleSection from './components/CollapsibleSection'
import RecipePanel from './components/RecipePanel'
import { ConfirmDialog } from './components/ConfirmDialog'
// @altara 组件已全部本地化，不再直接引用第三方包
import type { PLCConfig } from '../shared/types'


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
  const { db, io, setIo, connected, lastDataTime, ioLatency } = usePLCData()
  const { write, states, dismissError } = usePLCWrite()
  const [config, setConfig] = useState<PLCConfig | null>(null)
  const [showAltara, setShowAltara] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(true)
  useEffect(() => {
    const mq = window.matchMedia('(min-width: 769px)')
    setSidebarOpen(mq.matches)
    const onChange = (e: MediaQueryListEvent) => setSidebarOpen(e.matches)
    mq.addEventListener('change', onChange)
    const closeHandler = () => setSidebarOpen(false)
    window.addEventListener('close-sidebar', closeHandler)
    return () => { mq.removeEventListener('change', onChange); window.removeEventListener('close-sidebar', closeHandler) }
  }, [])

  // 启动时加载配置
  useEffect(() => {
    fetch('/api/plc/config').then(r => r.json()).then(setConfig).catch(() => {})
  }, [])

  // ─── 断线重连 → 自动重新注册 DB ──────────────────────────
  const wasConnectedRef = useRef(false)
  useEffect(() => {
    if (connected && !wasConnectedRef.current) {
      wasConnectedRef.current = true
      // 先确认 PLC 确实连上了（connected 只是 SSE 通了，不表示 PLC 已连接）
      fetch('/api/plc/status').then(r => r.json()).then(status => {
        if (status.connected) {
          reregisterAllDBs().then(({ success, fail }) => {
            if (success > 0) console.log(`自动注册 ${success} 个 DB${fail > 0 ? `, ${fail} 个失败` : ''}`)
          })
        }
      }).catch(() => {})
    }
    if (!connected) wasConnectedRef.current = false
  }, [connected])

  const handleIoToggle = useCallback(async (area: 'q' | 'm', byteAddr: number, bit: number, value: boolean) => {
    let currentByte = 0
    // 乐观更新：先改界面，同时 capture 当前字节值用于后端写入
    setIo(prev => {
      const copy = { ...prev[area] }
      currentByte = copy[byteAddr] ?? 0
      copy[byteAddr] = value ? (currentByte | (1 << bit)) : (currentByte & ~(1 << bit))
      return { ...prev, [area]: copy }
    })
    try {
      await fetch('/api/plc/write-io', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ area, byte: byteAddr, bit, value, currentByte }),
      })
    } catch {}
  }, [setIo])

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

  // I/Q/M 字节地址：localStorage 最优先（用户在 ConnectionPanel 改了即时生效），无则 fallback 到后端 config
  const ioRanges = (() => {
    try {
      const raw = localStorage.getItem('trioop_connection')
      if (raw) {
        const s = JSON.parse(raw)
        if (s.ioIBytes || s.ioQBytes || s.ioMBytes) {
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
          const r: { i?: { start: number; end: number }[]; q?: { start: number; end: number }[]; m?: { start: number; end: number }[] } = { i: toRanges(s.ioIBytes), q: toRanges(s.ioQBytes), m: toRanges(s.ioMBytes) }
          return r
        }
      }
    } catch { /* ignore */ }
    const cfg = config?.ioRanges
    if (cfg) return { i: cfg.i, q: cfg.q, m: cfg.m }
    return undefined
  })()
  const ioBytes = { i: rangesToBytes(ioRanges?.i), q: rangesToBytes(ioRanges?.q), m: rangesToBytes(ioRanges?.m) }

  return (
    <div className="app app--with-sidebar">
      <div className={`sidebar-wrapper${sidebarOpen ? '' : ' sidebar-wrapper--closed'}`}>
        <ConnectionPanel />
      </div>
      <div className="app__main">
        <StatusBar config={config} connected={connected} pointCount={pointCount} lastDataTime={lastDataTime} ioLatency={ioLatency} sidebarOpen={sidebarOpen} onToggleSidebar={() => setSidebarOpen(!sidebarOpen)} />
        <button className="btn btn--ghost btn--sm" style={{ position: 'fixed', bottom: 8, right: 8, zIndex: 100, fontSize: 11 }} onClick={() => setShowAltara(!showAltara)}>
          {showAltara ? '关闭演示' : '组件演示'}
        </button>
        <main className="main">

          <CollapsibleSection title="🟡 输入点 (I 区)" storageKey="io-input" keepMounted>
            <IOGrid label="" data={io.i} prefix="I" bytes={ioBytes.i} />
          </CollapsibleSection>

          <CollapsibleSection title="🔵 输出点 (Q 区)" storageKey="io-output" keepMounted>
            <IOGrid label="" data={io.q} prefix="Q" bytes={ioBytes.q} onToggle={(addr, bit, val) => handleIoToggle('q', addr, bit, val)} />
          </CollapsibleSection>

          <CollapsibleSection title="🟣 M 区" storageKey="io-m" keepMounted>
            <IOGrid label="" data={io.m} prefix="M" bytes={ioBytes.m} onToggle={(addr, bit, val) => handleIoToggle('m', addr, bit, val)} />
          </CollapsibleSection>

          {/* 报警面板 */}
          <AlarmPanel />

          {/* 可视化仪表盘 */}
          <VisualDashboard liveData={db} />

          {/* 组件实验室 */}
          <div style={{ textAlign: 'right', marginBottom: 8 }}>
            <button className="btn btn--ghost btn--sm" onClick={() => setShowAltara(!showAltara)}>
              {showAltara ? '✕ 关闭实验室' : '🧪 组件实验室'}
            </button>
          </div>
          {showAltara && <ComponentPlayground />}

          {/* 操作事件日志 */}
          <CollapsibleSection title="📝 操作日志" storageKey="event-log" keepMounted>
            <EventLogPanel />
          </CollapsibleSection>

          {/* 系统诊断 */}
          <DiagnosticsPanel />

          {/* 配方管理 */}
          <RecipePanel />

          <DBImportPanel onImport={() => {}} liveData={db} />
        </main>
      </div>
      <ConfirmDialog />
    </div>
  )
}

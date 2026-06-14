import { useEffect, useState, useCallback } from 'react'
import { usePLCData } from './hooks/usePLCData'
import { usePLCWrite } from './hooks/usePLCWrite'
import StatusBar from './components/StatusBar'
import PLCGrid from './components/PLCGrid'
import ConnectionPanel from './components/ConnectionPanel'
import IOGrid from './components/IOGrid'
import DBBlockPanel from './components/DBBlockPanel'
import type { PLCConfig } from '../shared/types'

interface DBBlockConfig {
  label: string
  dbNumber: number
  startOffset: number
  byteCount: number
}

export default function App() {
  const { db, io, setIo, dbBlocks, connected } = usePLCData()
  const { write, states, dismissError } = usePLCWrite()
  const [config, setConfig] = useState<PLCConfig | null>(null)
  const [blocks, setBlocks] = useState<DBBlockConfig[]>([])

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

  const variables = config?.variables ?? []
  const pointCount = Object.keys(db).length

  return (
    <div className="app app--with-sidebar">
      <ConnectionPanel />
      <div className="app__main">
        <StatusBar config={config} connected={connected} pointCount={pointCount} />
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
            <IOGrid label="🟡 输入点 (I 区)" data={io.i} prefix="I" bytes={[0, 1, 8]} />
          </section>

          <section className="section">
            <IOGrid label="🔵 输出点 (Q 区)" data={io.q} prefix="Q" bytes={[0, 1, 8]} onToggle={handleQToggle} />
          </section>

          <DBBlockPanel blocks={blocks} data={dbBlocks} onAdd={addBlock} onRemove={removeBlock} />
        </main>
      </div>
    </div>
  )
}

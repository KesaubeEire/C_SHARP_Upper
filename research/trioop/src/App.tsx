import { useEffect, useState } from 'react'
import { usePLCData } from './hooks/usePLCData'
import { usePLCWrite } from './hooks/usePLCWrite'
import StatusBar from './components/StatusBar'
import PLCGrid from './components/PLCGrid'
import type { PLCConfig } from '../shared/types'

export default function App() {
  const { data, connected } = usePLCData()
  const { write, states, dismissError } = usePLCWrite()
  const [config, setConfig] = useState<PLCConfig | null>(null)

  // 启动时加载 PLC 配置
  useEffect(() => {
    fetch('/api/plc/config')
      .then(res => res.json())
      .then(setConfig)
      .catch(() => { /* 开发模式下后端可能还没起来 */ })
  }, [])

  const variables = config?.variables ?? []
  const pointCount = Object.keys(data).length

  return (
    <div className="app">
      <StatusBar
        config={config}
        connected={connected}
        pointCount={pointCount}
      />
      <main className="main">
        <PLCGrid
          variables={variables}
          data={data}
          writeStates={states}
          onWrite={write}
          onDismissError={dismissError}
        />
      </main>
    </div>
  )
}

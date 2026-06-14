import type { PLCConfig } from '../../shared/types'

interface StatusBarProps {
  config: PLCConfig | null
  connected: boolean
  pointCount: number
}

export default function StatusBar({ config, connected, pointCount }: StatusBarProps) {
  const writableCount = config?.variables.filter(v => v.writable).length ?? 0

  return (
    <header className="status-bar">
      <h1 className="status-bar__title">🔌 PLC 实时监控</h1>

      <div className="status-bar__right">
        <span className="stat">
          <span className="stat__label">变量</span>
          <span className="stat__value">{pointCount}</span>
        </span>

        <span className="stat">
          <span className="stat__label">可写</span>
          <span className="stat__value">{writableCount}</span>
        </span>

        <span className={`status-dot ${connected ? 'connected' : ''}`} />
        <span className="status-text">{connected ? '已连接' : '未连接'}</span>
      </div>
    </header>
  )
}

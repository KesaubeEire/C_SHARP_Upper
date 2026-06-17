import type { PLCConfig } from '../../shared/types'
import { useAuth } from '../hooks/useAuth'
import { useTheme } from '../hooks/useTheme'

interface StatusBarProps {
  config: PLCConfig | null
  connected: boolean
  pointCount: number
  lastDataTime?: number
  sidebarOpen?: boolean
  onToggleSidebar?: () => void
}

export default function StatusBar({ config, connected, pointCount, lastDataTime, sidebarOpen, onToggleSidebar }: StatusBarProps) {
  const writableCount = config?.variables.filter(v => v.writable).length ?? 0
  const { username, role, logout } = useAuth()
  const { theme, toggle: toggleTheme } = useTheme()
  const dataStale = lastDataTime && (Date.now() - lastDataTime) > 5000

  const toggleFullscreen = () => {
    if (document.fullscreenElement) document.exitFullscreen()
    else document.documentElement.requestFullscreen()
  }

  return (
    <header className="status-bar">
      <div className="status-bar__left">
        {onToggleSidebar && (
          <button className="status-bar__menu-btn" onClick={onToggleSidebar} title={sidebarOpen ? '收起侧栏' : '展开侧栏'}>
            ☰
          </button>
        )}
        <h1 className="status-bar__title">🔌 PLC 实时监控</h1>
      </div>

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
        {dataStale && <span className="status-text" style={{ color: '#ffa726' }}>数据延迟</span>}
        {username && <span className="stat"><span className="stat__label">{username}</span><span className="stat__value" style={{ fontSize: 11, color: '#888' }}>({role})</span></span>}
        <button className="btn btn--ghost btn--sm" onClick={logout} title="退出登录">🚪</button>
        <button className="btn btn--ghost btn--sm" onClick={toggleTheme} title="切换主题">{theme === 'dark' ? '☀️' : '🌙'}</button>
        <button className="btn btn--ghost btn--sm" onClick={toggleFullscreen} title="全屏切换">⛶</button>
      </div>
    </header>
  )
}

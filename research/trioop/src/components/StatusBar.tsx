import { useState, useRef, useEffect } from 'react'
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
  const [menuOpen, setMenuOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  const toggleFullscreen = () => {
    if (document.fullscreenElement) document.exitFullscreen()
    else document.documentElement.requestFullscreen()
  }

  // 点外部关闭菜单
  useEffect(() => {
    if (!menuOpen) return
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false)
    }
    const t = setTimeout(() => document.addEventListener('mousedown', handler), 0)
    return () => { clearTimeout(t); document.removeEventListener('mousedown', handler) }
  }, [menuOpen])

  const avatar = username ? username[0].toUpperCase() : '?'

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
        <span className="stat status-bar__stat--writable">
          <span className="stat__label">可写</span>
          <span className="stat__value">{writableCount}</span>
        </span>

        <span className={`status-dot ${connected ? 'connected' : ''}`} />
        <span className="status-text status-bar__text--connected">{connected ? '已连接' : '未连接'}</span>
        {dataStale && <span className="status-text" style={{ color: '#ffa726' }}>数据延迟</span>}

        {/* 用户头像 + 下拉菜单 */}
        <div ref={menuRef} className="user-menu" style={{ position: 'relative' }}>
          <button className="user-avatar" onClick={() => setMenuOpen(p => !p)} title="用户菜单">
            {avatar}
          </button>
          {menuOpen && (
            <div className="user-menu__dropdown">
              {username && (
                <div className="user-menu__header">
                  <span className="user-menu__name">{username}</span>
                  {role && <span className="user-menu__role">{role}</span>}
                </div>
              )}
              <div className="user-menu__divider" />
              <button className="user-menu__item" onClick={toggleTheme}>
                <span className="user-menu__icon">{theme === 'dark' ? '☀️' : '🌙'}</span>
                <span className="user-menu__label">{theme === 'dark' ? '浅色主题' : '深色主题'}</span>
              </button>
              <button className="user-menu__item" onClick={toggleFullscreen}>
                <span className="user-menu__icon">⛶</span>
                <span className="user-menu__label">全屏</span>
              </button>
              <div className="user-menu__divider" />
              <button className="user-menu__item user-menu__item--danger" onClick={logout}>
                <span className="user-menu__icon">🚪</span>
                <span className="user-menu__label">退出登录</span>
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}

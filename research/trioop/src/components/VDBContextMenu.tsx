import { useState, useEffect, useRef, useCallback } from 'react'

interface CtxItem {
  label: string; icon?: string; action: () => void; danger?: boolean; separator?: boolean
}

/** 全局唯一的右键菜单（一次只开一个） */
let closeGlobal: (() => void) | null = null

export function useContextMenu() {
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null)
  const [items, setItems] = useState<CtxItem[]>([])
  const menuRef = useRef<HTMLDivElement>(null)
  const idxRef = useRef(-1)

  const show = useCallback((e: React.MouseEvent, menuItems: CtxItem[]) => {
    e.preventDefault()
    e.stopPropagation()
    // 关掉其他菜单
    if (closeGlobal) closeGlobal()
    setPos({ x: e.clientX, y: e.clientY })
    setItems(menuItems)
    idxRef.current = -1
  }, [])

  const hide = useCallback(() => {
    setPos(null)
    setItems([])
    idxRef.current = -1
  }, [])

  // 注册全局关闭
  useEffect(() => {
    if (!pos) return
    closeGlobal = hide
    return () => { if (closeGlobal === hide) closeGlobal = null }
  }, [pos, hide])

  // 点击外部关闭
  useEffect(() => {
    if (!pos) return
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) hide()
    }
    // 延迟一帧注册，避免点击触发的 immediate close
    const t = setTimeout(() => document.addEventListener('mousedown', handler), 0)
    return () => { clearTimeout(t); document.removeEventListener('mousedown', handler) }
  }, [pos, hide])

  // 键盘导航
  useEffect(() => {
    if (!pos) return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { hide(); return }
      if (e.key === 'ArrowDown') {
        e.preventDefault()
        idxRef.current = (idxRef.current + 1) % items.length
        const el = menuRef.current?.children[idxRef.current] as HTMLElement
        el?.focus()
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault()
        idxRef.current = (idxRef.current - 1 + items.length) % items.length
        const el = menuRef.current?.children[idxRef.current] as HTMLElement
        el?.focus()
      }
      if (e.key === 'Enter' && idxRef.current >= 0) {
        items[idxRef.current].action()
        hide()
      }
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [pos, items, hide])

  const menu = pos ? (
    <div className="vdb-ctx" ref={menuRef} style={{ left: pos.x, top: pos.y }} role="menu">
      {items.map((item, i) => (
        <button key={i} className={`vdb-ctx__item ${item.danger ? 'vdb-ctx__item--danger' : ''}`} role="menuitem"
          onClick={() => { item.action(); hide() }}
          onMouseEnter={() => { idxRef.current = i; (menuRef.current?.children[i] as HTMLElement)?.focus() }}>
          {item.icon && <span className="vdb-ctx__icon">{item.icon}</span>}
          {item.label}
        </button>
      ))}
    </div>
  ) : null

  return { show, hide, menu }
}

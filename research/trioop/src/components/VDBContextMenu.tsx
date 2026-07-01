import { useState, useEffect, useRef, useCallback, useLayoutEffect } from 'react'

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
  const posRef = useRef<{ x: number; y: number } | null>(null)

  const show = useCallback((e: React.MouseEvent, menuItems: CtxItem[]) => {
    e.preventDefault()
    e.stopPropagation()
    if (closeGlobal) closeGlobal()
    posRef.current = { x: e.clientX, y: e.clientY }
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

  // 菜单渲染后自动校正位置：不超出视口，超出高度则滚动
  useLayoutEffect(() => {
    const el = menuRef.current
    const p = posRef.current
    if (!el || !p) return
    const rect = el.getBoundingClientRect()
    const vw = window.innerWidth
    const vh = window.innerHeight
    const gap = 8

    let x = p.x
    let y = p.y

    // 水平：右溢出时靠左弹出
    if (x + rect.width + gap > vw) x = vw - rect.width - gap
    if (x < gap) x = gap

    // 垂直：下溢出时向上弹出
    if (y + rect.height + gap > vh) y = vh - rect.height - gap
    if (y < gap) y = gap

    // 高度超出视口：允许滚动
    if (rect.height > vh - gap * 2) {
      el.style.maxHeight = `${vh - gap * 2}px`
      el.style.overflowY = 'auto'
    } else {
      el.style.maxHeight = ''
      el.style.overflowY = ''
    }

    el.style.left = `${x}px`
    el.style.top = `${y}px`
  }, [pos])

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

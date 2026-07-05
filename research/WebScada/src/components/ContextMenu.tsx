import { useState, useEffect, useRef } from 'react'

interface MenuItem { label: string; icon?: string; action: () => void; danger?: boolean }

interface ContextMenuProps {
  items: MenuItem[]
  trigger: 'contextmenu' | 'click'
  children: React.ReactNode
}

export default function ContextMenu({ items, trigger, children }: ContextMenuProps) {
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!pos) return
    const handler = () => setPos(null)
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [pos])

  const handleContext = (e: React.MouseEvent) => {
    if (trigger === 'contextmenu') {
      e.preventDefault()
      setPos({ x: e.clientX, y: e.clientY })
    }
  }

  const handleClick = (e: React.MouseEvent) => {
    if (trigger === 'click') {
      e.stopPropagation()
      setPos(prev => prev ? null : { x: e.clientX, y: e.clientY })
    }
  }

  return (
    <div ref={ref} onContextMenu={handleContext} onClick={handleClick} style={{ width: '100%', height: '100%' }}>
      {children}
      {pos && (
        <div className="ctx-menu" style={{ left: pos.x, top: pos.y }} onMouseDown={e => e.stopPropagation()}>
          {items.map((item, i) => (
            <button key={i} className={`ctx-menu__item ${item.danger ? 'ctx-menu__item--danger' : ''}`}
              onClick={() => { item.action(); setPos(null) }}>
              {item.icon && <span className="ctx-menu__icon">{item.icon}</span>}
              {item.label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

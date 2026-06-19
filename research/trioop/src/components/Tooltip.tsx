import { useState, useRef, useCallback, type ReactNode } from 'react'

interface TooltipProps {
  children: ReactNode
  content: string
  side?: 'top' | 'bottom' | 'left' | 'right'
}

export default function Tooltip({ children, content, side = 'top' }: TooltipProps) {
  const [show, setShow] = useState(false)
  const timerRef = useRef<any>(null)
  const [pos, setPos] = useState({ top: 0, left: 0 })
  const triggerRef = useRef<HTMLSpanElement>(null)

  const calcPos = useCallback(() => {
    const el = triggerRef.current
    if (!el) return
    const r = el.getBoundingClientRect()
    const gap = 6
    switch (side) {
      case 'top': setPos({ top: r.top - gap, left: r.left + r.width / 2 }); break
      case 'bottom': setPos({ top: r.bottom + gap, left: r.left + r.width / 2 }); break
      case 'left': setPos({ top: r.top + r.height / 2, left: r.left - gap }); break
      case 'right': setPos({ top: r.top + r.height / 2, left: r.right + gap }); break
    }
  }, [side])

  const handleMouseEnter = useCallback(() => {
    calcPos()
    timerRef.current = setTimeout(() => setShow(true), 400)
  }, [calcPos])

  const handleMouseLeave = useCallback(() => {
    clearTimeout(timerRef.current)
    setShow(false)
  }, [])

  return (
    <span ref={triggerRef} style={{ display: 'inline-flex' }}
      onMouseEnter={handleMouseEnter} onMouseLeave={handleMouseLeave} onFocus={handleMouseEnter} onBlur={handleMouseLeave}>
      {children}
      {show && (
        <span className="tooltip-content" style={{
          position: 'fixed', zIndex: 99999, pointerEvents: 'none',
          top: side === 'top' || side === 'bottom' ? pos.top : undefined,
          bottom: side === 'top' ? undefined : undefined,
          left: side === 'left' || side === 'right' ? undefined : pos.left,
          right: side === 'left' || side === 'right' ? undefined : undefined,
          transform: side === 'top' ? 'translate(-50%, -100%)' : side === 'bottom' ? 'translate(-50%, 0)' : side === 'left' ? 'translate(-100%, -50%)' : 'translate(0, -50%)',
        }}>
          {content}
        </span>
      )}
    </span>
  )
}

import { useState, useRef, useCallback, type ReactNode } from 'react'

interface TooltipProps {
  children: ReactNode
  content: string
  side?: 'top' | 'bottom' | 'left' | 'right'
}

export default function Tooltip({ children, content, side = 'top' }: TooltipProps) {
  const [show, setShow] = useState(false)
  const [pos, setPos] = useState({ top: 0, left: 0 })
  const timerRef = useRef<any>(null)
  const wrapRef = useRef<HTMLSpanElement>(null)

  const calc = useCallback(() => {
    const el = wrapRef.current?.firstElementChild as HTMLElement | null
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

  return (
    <span ref={wrapRef} style={{ display: 'inline-flex', alignItems: 'center' }}
      onMouseEnter={() => { calc(); timerRef.current = setTimeout(() => setShow(true), 400) }}
      onMouseLeave={() => { clearTimeout(timerRef.current); setShow(false) }}
      onFocus={() => { calc(); timerRef.current = setTimeout(() => setShow(true), 400) }}
      onBlur={() => { clearTimeout(timerRef.current); setShow(false) }}>
      {children}
      {show && (
        <span style={{
          position: 'fixed', zIndex: 99999, pointerEvents: 'none',
          top: pos.top, left: pos.left,
          transform: side === 'top' ? 'translate(-50%, -100%)' : side === 'bottom' ? 'translate(-50%, 0)' : side === 'left' ? 'translate(-100%, -50%)' : 'translate(0, -50%)',
        }}>
          <span className="tooltip-content__inner">{content}</span>
        </span>
      )}
    </span>
  )
}

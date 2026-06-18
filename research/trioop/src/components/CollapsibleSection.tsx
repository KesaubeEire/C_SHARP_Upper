import { useState, useCallback, type ReactNode, type CSSProperties } from 'react'

interface Props {
  title: string
  storageKey: string
  defaultOpen?: boolean
  children: ReactNode
  actions?: ReactNode
  className?: string
  style?: CSSProperties
  /** 折叠时是否保留 DOM（用 CSS 隐藏），适合 react-grid-layout 等组件 */
  keepMounted?: boolean
}

/**
 * 可折叠 Section — 点击标题栏展开/收起，状态记到 localStorage
 */
export default function CollapsibleSection({
  title,
  storageKey,
  defaultOpen = true,
  children,
  actions,
  className,
  style,
  keepMounted,
}: Props) {
  const [open, setOpen] = useState<boolean>(() => {
    try {
      const saved = localStorage.getItem(`section_expand_${storageKey}`)
      return saved !== null ? saved === 'true' : defaultOpen
    } catch {
      return defaultOpen
    }
  })

  const toggle = useCallback(() => {
    setOpen(prev => {
      const next = !prev
      try { localStorage.setItem(`section_expand_${storageKey}`, String(next)) } catch {}
      return next
    })
  }, [storageKey])

  return (
    <section className={`section${className ? ` ${className}` : ''}`} style={style}>
      <div className="section__title-row collapsible-header" onClick={toggle}>
        <h2 className="section__title" style={{ margin: 0, cursor: 'pointer', userSelect: 'none' }}>
          <span className={`collapsible-chevron${open ? '' : ' collapsed'}`}>▼</span>
          {title}
        </h2>
        {actions && (
          <div className="collapsible-actions" onClick={e => e.stopPropagation()}>
            {actions}
          </div>
        )}
      </div>
      {keepMounted ? (
        <div className="section__body" style={{ display: open ? '' : 'none' }}>{children}</div>
      ) : (
        open && <div className="section__body">{children}</div>
      )}
    </section>
  )
}

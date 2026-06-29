import { useState, useCallback, useEffect, useRef } from 'react'

interface ConfirmOptions {
  title: string
  message: string
  confirmText?: string
  cancelText?: string
  danger?: boolean
}

let confirmRef: ((opts: ConfirmOptions) => Promise<boolean>) | null = null

export function confirm(opts: ConfirmOptions): Promise<boolean> {
  if (!confirmRef) return Promise.resolve(false)
  return confirmRef(opts)
}

export function ConfirmDialog() {
  const [state, setState] = useState<{ opts: ConfirmOptions; resolve: (v: boolean) => void } | null>(null)
  const okRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    confirmRef = (opts: ConfirmOptions) => new Promise<boolean>(resolve => {
      setState({ opts, resolve })
    })
    return () => { confirmRef = null }
  }, [])

  const handle = useCallback((value: boolean) => {
    state?.resolve(value)
    setState(null)
  }, [state])

  useEffect(() => {
    if (state) okRef.current?.focus()
  }, [state])

  useEffect(() => {
    if (!state) return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') handle(false)
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [state, handle])

  if (!state) return null

  const { opts } = state

  return (
    <div className="modal-overlay" onMouseDown={e => { if (e.target === e.currentTarget) handle(false) }}>
      <div className="modal-content" style={{ width: 380 }} onClick={e => e.stopPropagation()}>
        <h3 className="modal-title">{opts.title}</h3>
        <p style={{ color: 'var(--muted-foreground)', lineHeight: 1.6, margin: 0 }}>{opts.message}</p>
        <div className="modal-actions">
          <button className="btn btn--ghost" onClick={() => handle(false)}>{opts.cancelText || '取消'}</button>
          <button ref={okRef} className={`btn ${opts.danger ? 'btn--danger' : 'btn--primary'}`} onClick={() => handle(true)}>
            {opts.confirmText || '确定'}
          </button>
        </div>
      </div>
    </div>
  )
}

import { useState, useCallback, useEffect } from 'react'

interface Props {
  title: string
  /** 导出还是导入 */
  mode: 'export' | 'import'
  /** 导出时调用：传 format → 执行下载 */
  onExport?: (format: 'csv' | 'xlsx') => void
  /** 导入时调用：传 format → 用户选文件 → 执行上传 */
  onImport?: (format: 'csv' | 'xlsx', file: File) => Promise<void>
  onClose: () => void
}

export function TransferDialog({ title, mode, onExport, onImport, onClose }: Props) {
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [onClose])

  const handleFormat = (fmt: 'csv' | 'xlsx') => {
    if (mode === 'export') {
      onExport?.(fmt)
      onClose()
    } else {
      const input = document.createElement('input')
      input.type = 'file'
      input.accept = fmt === 'csv' ? '.csv' : '.xlsx,.xls'
      input.onchange = async () => {
        const file = input.files?.[0]
        if (!file) return
        setBusy(true)
        try {
          await onImport?.(fmt, file)
        } finally {
          setBusy(false)
          onClose()
        }
      }
      input.click()
    }
  }

  return (
    <div className="modal-overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose() }}>
      <div className="modal-content" onClick={e => e.stopPropagation()} style={{ width: 360 }}>
        <h3 className="modal-title">{title}</h3>
        <p style={{ color: 'var(--muted-foreground)', margin: '0 0 16px 0' }}>
          {mode === 'export' ? '选择要导出的格式' : '选择要导入的格式'}
        </p>
        <div className="modal-actions" style={{ justifyContent: 'center', gap: 12 }}>
          <button className="btn btn--ghost" disabled={busy} onClick={() => handleFormat('csv')}>
            📄 CSV
          </button>
          <button className="btn btn--primary" disabled={busy} onClick={() => handleFormat('xlsx')}>
            📊 Excel
          </button>
        </div>
        {busy && <p style={{ textAlign: 'center', color: 'var(--muted-foreground)', margin: '8px 0 0' }}>处理中...</p>}
      </div>
    </div>
  )
}

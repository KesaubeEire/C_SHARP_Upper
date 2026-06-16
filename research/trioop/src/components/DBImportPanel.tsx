import { useState, useRef, useCallback } from 'react'
import { useToast } from '../hooks/useToast'

interface ParsedVar {
  name: string
  type: string
  offset: number
  bit?: number
  comment?: string
}

interface ImportedDB {
  dbNumber: number
  dbName: string
  variableCount: number
  variables: ParsedVar[]
}

interface DBImportPanelProps {
  onImport: () => void
  liveData?: Record<string, { value: number | boolean; type: string }>
}

export default function DBImportPanel({ onImport, liveData }: DBImportPanelProps) {
  const toast = useToast()
  const [importedDBs, setImportedDBs] = useState<ImportedDB[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<string | null>(null)
  const [editVal, setEditVal] = useState('')
  const fileRef = useRef<HTMLInputElement>(null)

  const loadImported = async () => {
    try { const res = await fetch('/api/plc/imported-dbs'); setImportedDBs(await res.json()) } catch {}
  }

  useState(() => { loadImported() })

  async function handleFileUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setLoading(true); setError('')
    try {
      // 直接发原始文件内容，避免 file.text() + JSON.stringify 阻塞主线程
      const res = await fetch('/api/plc/import-db', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: file })
      const data = await res.json()
      if (data.success) { await loadImported(); onImport() }
      else setError(data.error || '导入失败')
    } catch (err) { setError(`文件读取失败: ${(err as Error).message}`) }
    finally { setLoading(false); if (fileRef.current) fileRef.current.value = '' }
  }

  /** 点按钮前先清空 input 值，防止重复触发或 Esc 后残留 */
  const handleClickFile = useCallback(() => {
    if (fileRef.current) { fileRef.current.value = ''; fileRef.current.click() }
  }, [])

  /** 刷新单个 DB 块：重新注册到当前连接（切换模式/断连后恢复用） */
  async function handleRefresh(key: string) {
    try {
      const res = await fetch(`/api/plc/imported-dbs/${encodeURIComponent(key)}/refresh`, { method: 'POST' })
      const data = await res.json()
      if (data.success) { await loadImported(); onImport() }
      else setError(data.error || '刷新失败')
    } catch { setError('刷新失败') }
  }

  async function handleRemove(key: string) {
    try { await fetch(`/api/plc/imported-dbs/${encodeURIComponent(key)}`, { method: 'DELETE' }); await loadImported(); onImport() } catch {}
  }

  /** 写值（fire & forget，不 await） */
  const writeVal = useCallback((dbNumber: number, name: string, value: number) => {
    fetch('/api/plc/imported-db-write', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ dbNumber, name, value }),
    }).then(r => { if (r.ok) toast(`${name}=${value}`, 'success') })
      .catch(() => toast(`写入 ${name} 失败`, 'error'))
  }, [toast])

  const writeValue = async (dbNumber: number, name: string) => {
    const val = Number(editVal)
    if (isNaN(val)) return
    writeVal(dbNumber, name, val)
    setEditing(null)
  }

  const typeColor = (t: string) => {
    const colors: Record<string, string> = { bool: '#4caf50', int: '#2196f3', real: '#ff9800', dint: '#9c27b0', word: '#00bcd4', dword: '#e91e63', byte: '#607d8b' }
    return colors[t] || '#888'
  }

  return (
    <section className="section">
      <h2 className="section__title">📥 导入 DB 文件</h2>

      <div className="db-import__bar">
        <input ref={fileRef} type="file" accept=".db" onChange={handleFileUpload} className="db-import__file" />
        <button className="btn btn--primary" onClick={handleClickFile} disabled={loading}>{loading ? '解析中...' : '选择 .db 文件'}</button>
      </div>
      {error && <div className="db-import__error">{error}</div>}

      {importedDBs.length === 0 ? (
        <div className="db-empty">尚未导入 DB 文件</div>
      ) : (
        <div className="db-import__list">
          {importedDBs.map(db => {
            const key = `${db.dbNumber}_${db.dbName}`
            return (
              <div key={key} className="db-import__card">
                <div className="db-card__header">
                  <span className="db-card__label">DB{db.dbNumber} · {db.dbName}</span>
                  <span className="db-card__info">{db.variableCount} 个变量</span>
                  <button className="btn btn--primary db-card__refresh" onClick={() => handleRefresh(key)} title="重新注册到当前连接">↻</button>
                  <button className="btn btn--danger db-card__del" onClick={() => handleRemove(key)}>✕</button>
                </div>
                <div className="db-import__vars">
                  {db.variables.map(v => {
                    const live = liveData?.[v.name]
                    const showVal = live?.value !== undefined && live?.value !== null
                    const isEditing = editing === v.name

                    return (
                    <div key={v.name} className="db-import__var">
                      <span className="db-import__var-name">{v.name}</span>

                      {/* Bool 双按钮 */}
                      {v.type === 'bool' ? (
                        <span className="db-import__var-btns">
                          <button className="db-import__momentary"
                            onMouseDown={() => writeVal(db.dbNumber, v.name, 1)}
                            onMouseUp={() => writeVal(db.dbNumber, v.name, 0)}
                            onMouseLeave={() => writeVal(db.dbNumber, v.name, 0)}
                          >按1松0</button>
                          <button className="db-import__toggle"
                            onClick={() => writeVal(db.dbNumber, v.name, showVal && live.value ? 0 : 1)}
                          >取反</button>
                          <span className={`db-import__var-value ${showVal && live.value ? 'db-import__var-value--on' : ''}`}>
                            {showVal ? (live.value ? '1' : '0') : '--'}
                          </span>
                        </span>
                      ) : isEditing ? (
                        <span className="db-import__var-edit">
                          <input className="db-import__edit-input" type="text" value={editVal} onChange={e => setEditVal(e.target.value)}
                            onKeyDown={e => { if (e.key === 'Enter') writeValue(db.dbNumber, v.name); if (e.key === 'Escape') setEditing(null) }}
                            onBlur={() => writeValue(db.dbNumber, v.name)} autoFocus />
                        </span>
                      ) : (
                        <span className="db-import__var-value db-import__var-edit-btn"
                          onClick={() => { setEditing(v.name); setEditVal(showVal ? String(live.value) : '0') }}>
                          {showVal ? String(live.value) : '--'}
                        </span>
                      )}

                      <span className="db-import__var-type" style={{ background: typeColor(v.type) + '33', color: typeColor(v.type) }}>{v.type.toUpperCase()}</span>
                      <span className="db-import__var-offset">@{v.offset}{v.bit !== undefined ? `.${v.bit}` : ''}</span>
                      {v.comment && <span className="db-import__var-comment">// {v.comment}</span>}
                    </div>
                  )})}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </section>
  )
}

import { useState, useRef, useCallback, useEffect } from 'react'
import { useToast } from '../hooks/useToast'
import { loadMapping, saveMapping, saveDBData, writePLC } from '../hooks/useDBMapping'

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
    try {
      const res = await fetch('/api/plc/imported-dbs')
      const dbs: ImportedDB[] = await res.json()
      const m = loadMapping()
      for (const db of dbs) { if (m[db.dbName] !== undefined) db.dbNumber = m[db.dbName] }
      setImportedDBs(dbs)
    } catch {}
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
      if (data.success) {
        saveDBData({ dbNumber: data.dbNumber, dbName: data.dbName, variables: data.variables })
        await loadImported(); onImport()
      }
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
      const m = loadMapping()
      const dbName = key.split('_').slice(1).join('_')
      const mappedDb = m[dbName]
      const res = await fetch(`/api/plc/imported-dbs/${encodeURIComponent(key)}/refresh`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dbNumber: mappedDb }),
      })
      const data = await res.json()
      if (data.success) { await loadImported(); onImport() }
      else setError(data.error || '刷新失败')
    } catch { setError('刷新失败') }
  }

  async function handleRemove(key: string) {
    try { await fetch(`/api/plc/imported-dbs/${encodeURIComponent(key)}`, { method: 'DELETE' }); await loadImported(); onImport() } catch {}
  }

  /** 写值（fire & forget，不 await） */
  const writeVal = useCallback((dbName: string, _dbNumber: number, vname: string, value: number) => {
    writePLC(`${dbName}:${vname}`, value)
      .then(() => toast(`${vname}=${value}`, 'success'))
      .catch((err: Error) => toast(`写入 ${vname} 失败: ${err.message}`, 'error'))
  }, [toast])

  const writeValue = async (dbName: string, _dbNumber: number, vname: string) => {
    const val = Number(editVal)
    if (isNaN(val)) return
    writePLC(`${dbName}:${vname}`, val)
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
                  <span className="db-card__label">{db.dbName}</span>
                  <span className="db-card__info">{db.variableCount} 个变量</span>
                  <DBNumberInput dbName={db.dbName} />
                  <button className="btn btn--primary db-card__refresh" onClick={() => handleRefresh(key)} title="重新注册到当前连接">↻</button>
                  <button className="btn btn--danger db-card__del" onClick={() => handleRemove(key)}>✕</button>
                </div>
                <div className="db-import__vars">
                  {/* 表头 */}
                  <div className="db-import__header">
                    <span className="db-import__h-name">名称</span>
                    <span className="db-import__h-ctrl">操作</span>
                    <span className="db-import__h-val">值</span>
                    <span className="db-import__h-type">类型</span>
                    <span className="db-import__h-off">偏移</span>
                  </div>
                  {db.variables.map(v => {
                    const live = liveData?.[v.name]
                    const showVal = live?.value !== undefined && live?.value !== null
                    const isEditing = editing === v.name

                    return (
                    <div key={v.name} className="db-import__row">
                      <span className="db-import__r-name" title={v.comment}>{v.name}</span>

                      <span className="db-import__r-ctrl">
                        {v.type === 'bool' ? (<>
                          <button className="db-import__momentary"
                            onMouseDown={() => writeVal(db.dbName, db.dbNumber, v.name, 1)}
                            onMouseUp={() => writeVal(db.dbName, db.dbNumber, v.name, 0)}
                            onMouseLeave={() => writeVal(db.dbName, db.dbNumber, v.name, 0)}
                          >按1松0</button>
                          <button className="db-import__toggle"
                            onClick={() => writeVal(db.dbName, db.dbNumber, v.name, showVal && live.value ? 0 : 1)}
                          >取反</button>
                        </>) : null}
                      </span>

                      <span className="db-import__r-val">
                        {v.type === 'bool' ? (
                          <span className={`db-import__cell-val ${showVal && live.value ? 'db-import__cell-val--on' : ''}`}>
                            {showVal ? (live.value ? '1' : '0') : '--'}
                          </span>
                        ) : isEditing ? (
                          <input className="db-import__edit-input" type="text" value={editVal}
                            onChange={e => setEditVal(e.target.value)}
                            onKeyDown={e => { if (e.key === 'Enter') writeValue(db.dbName, db.dbNumber, v.name); if (e.key === 'Escape') setEditing(null) }}
                            onBlur={() => writeValue(db.dbName, db.dbNumber, v.name)} autoFocus />
                        ) : (
                          <span className="db-import__cell-val db-import__cell-edit"
                            onClick={() => { setEditing(v.name); setEditVal(showVal ? String(live.value) : '0') }}>
                            {showVal ? (typeof live.value === 'number' ? parseFloat(Number(live.value).toFixed(4)) : String(live.value)) : '--'}
                          </span>
                        )}
                      </span>

                      <span className="db-import__r-type" style={{ color: typeColor(v.type) }}>{v.type.toUpperCase()}</span>
                      <span className="db-import__r-off">@{v.offset}{v.bit !== undefined ? `.${v.bit}` : ''}</span>
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

function DBNumberInput({ dbName }: { dbName: string }) {
  const map = loadMapping()
  const [val, setVal] = useState(() => map[dbName] ?? (parseInt(dbName.replace(/^DB/i, '')) || 1))
  useEffect(() => {
    const m = loadMapping(); m[dbName] = val; saveMapping(m)
    fetch('/api/plc/update-tag-addr', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({ dbName, dbNumber: val }) }).catch(() => {})
  }, [dbName, val])
  return <input id={`dbnum-${dbName}`} className="db-card__dbnum" type="number" value={val} min={1} max={999}
    onChange={e => setVal(Math.max(1, Number(e.target.value)))}
    title={`${dbName} 对应的 PLC DB 块号`} />
}

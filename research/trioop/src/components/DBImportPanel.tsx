import React, { useState, useRef, useCallback, useEffect } from 'react'
import CollapsibleSection from './CollapsibleSection'
import Tooltip from './Tooltip'
import { useToast } from '../hooks/useToast'
import { loadMapping, saveMapping, saveDBData, saveUDTContent, loadAllUDTContent, clearUDTCache, loadAllDBData, writePLC } from '../hooks/useDBMapping'

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
  const udtFileRef = useRef<HTMLInputElement>(null)
  const [udtNames, setUdtNames] = useState<string[]>([])
  const [udtLoading, setUdtLoading] = useState(false)
  const [udtDetail, setUdtDetail] = useState<{ name: string; fields: { name: string; type: string; bit?: number }[] } | null>(null)
  const [udtDetailCache, setUdtDetailCache] = useState<Record<string, { name: string; fields: { name: string; type: string; bit?: number }[] }>>({})

  const loadImported = async (): Promise<ImportedDB[]> => {
    try {
      const res = await fetch('/api/plc/imported-dbs')
      const dbs: ImportedDB[] = await res.json()
      const m = loadMapping()
      for (const db of dbs) { if (m[db.dbName] !== undefined) db.dbNumber = m[db.dbName] }
      setImportedDBs(dbs)
      return dbs
    } catch { return [] }
  }

  const loadUdts = async (): Promise<string[]> => {
    try {
      const res = await fetch('/api/plc/imported-udts')
      if (res.ok) { const names = await res.json(); setUdtNames(names); return names }
    } catch {}
    return []
  }

  // 启动时从后端加载，若后端无数据则从 localStorage 自动恢复
  useEffect(() => {
    (async () => {
      const dbs = await loadImported()
      if (dbs.length > 0) return
      // 后端无 UDT → 从 localStorage 恢复
      const udtContent = loadAllUDTContent()
      if (udtContent) {
        try { await fetch('/api/plc/import-udt', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: udtContent }) } catch {}
      }
      // 后端无 DB → 从 localStorage 恢复
      const localDBs = loadAllDBData()
      for (const db of localDBs) {
        try {
          const content = localStorage.getItem(`trioop_db_raw_${db.dbName}`)
          if (content) await fetch('/api/plc/import-db', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: content })
        } catch {}
      }
      await loadImported()
      await loadUdts()
    })()
  }, [])

  async function handleFileUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setLoading(true); setError('')
    try {
      const content = await file.text()
      const res = await fetch('/api/plc/import-db', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: content })
      const data = await res.json()
      if (data.success) {
        saveDBData({ dbNumber: data.dbNumber, dbName: data.dbName, variables: data.variables })
        // 存原始内容到 localStorage
        try { localStorage.setItem(`trioop_db_raw_${data.dbName}`, content) } catch {}
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

  /** UDT 文件上传 */
  async function handleUdtUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setUdtLoading(true); setError('')
    try {
      const content = await file.text()
      const res = await fetch('/api/plc/import-udt', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: content })
      const data = await res.json()
      if (data.success) {
        saveUDTContent(content)
        toast(`已导入 ${data.count} 个 UDT: ${data.names.join(', ')}`, 'success')
        await loadUdts()
      } else {
        setError(data.error || 'UDT 导入失败')
      }
    } catch (err) { setError(`UDT 文件读取失败: ${(err as Error).message}`) }
    finally { setUdtLoading(false); if (udtFileRef.current) udtFileRef.current.value = '' }
  }

  const handleClickUdt = useCallback(() => {
    if (udtFileRef.current) { udtFileRef.current.value = ''; udtFileRef.current.click() }
  }, [])

  /** 查看 UDT 字段详情 */
  const handleUdtDetail = useCallback(async (name: string) => {
    // 先查缓存
    if (udtDetailCache[name]) { setUdtDetail(udtDetailCache[name]); return }
    try {
      const res = await fetch(`/api/plc/imported-udts/${encodeURIComponent(name)}`)
      if (res.ok) {
        const data = await res.json()
        setUdtDetailCache(c => ({ ...c, [name]: data }))
        // 持久化到 localStorage
        try { localStorage.setItem(`trioop_udt_detail_${name}`, JSON.stringify(data)) } catch {}
        setUdtDetail(data)
      }
    } catch {}
  }, [udtDetailCache])

  // 启动时尝试从 localStorage 恢复 UDT 详情缓存
  useEffect(() => {
    const names = udtNames
    const restored: Record<string, any> = {}
    for (const n of names) {
      try {
        const raw = localStorage.getItem(`trioop_udt_detail_${n}`)
        if (raw) restored[n] = JSON.parse(raw)
      } catch {}
    }
    if (Object.keys(restored).length > 0) setUdtDetailCache(restored)
  }, [udtNames])

  /** 删除单个 UDT */
  async function handleRemoveUdt(name: string) {
    try {
      await fetch(`/api/plc/imported-udts/${encodeURIComponent(name)}`, { method: 'DELETE' })
      // localStorage 里的 UDT 内容无法按名称删除，只能全清
      clearUDTCache()
      await loadUdts()
    } catch {}
  }

  /** 刷新单个 DB 块：重新注册到当前连接（切换模式/断连后恢复用） */
  async function handleRefresh(key: string) {
    try {
      const dbName = key.split('_').slice(1).join('_')
      // 从 loadMapping() 取映射号（DBNumberInput 每次改动都会 saveMapping）
      const m = loadMapping()
      const mappedDb = m[dbName] ?? 1
      const res = await fetch(`/api/plc/imported-dbs/${encodeURIComponent(key)}/refresh`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dbNumber: mappedDb, dbName }),
      })
      const data = await res.json()
      if (!data.success) { setError(data.error || '刷新失败'); return }
      // 只更新当前 DB 的变量数（不调 loadImported 以免 key 变化导致 DBNumberInput 重挂）
      setImportedDBs(prev => prev.map(db =>
        db.dbName === dbName ? { ...db, variableCount: data.registered ?? data.matched ?? db.variableCount } : db
      ))
      onImport()
    } catch { setError('刷新失败') }
  }

  async function handleRemove(key: string) {
    try {
      const dbName = key.split('_').slice(1).join('_')
      await fetch(`/api/plc/imported-dbs/${encodeURIComponent(key)}?dbName=${encodeURIComponent(dbName)}`, { method: 'DELETE' })
      try { localStorage.removeItem(`trioop_db_raw_${dbName}`) } catch {}
      await loadImported(); onImport()
    } catch {}
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
    <React.Fragment>

      <CollapsibleSection title="📥 导入 DB 文件" storageKey="db-import">

      {/* ─── UDT 定义文件导入 ─────────────────────────────── */}
      <div className="udt-import">
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 4 }}>
          <span style={{ fontSize: '0.85em', color: '#888' }}>🔷 数据类型 (UDT)</span>
          <input ref={udtFileRef} type="file" accept=".db,.udt" onChange={handleUdtUpload} style={{ display: 'none' }} />
          <button className="btn btn--sm" onClick={handleClickUdt} disabled={udtLoading}>
            {udtLoading ? '解析中...' : '选择 UDT 文件'}
          </button>
        </div>
        {udtNames.length > 0 && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: 8 }}>
            {udtNames.map(name => (
              <span key={name} className="udt-tag" style={{ cursor: 'pointer' }} onClick={() => handleUdtDetail(name)} title="查看字段详情">
                {name}
                <Tooltip content="删除此 UDT"><button className="udt-tag__del" onClick={e => { e.stopPropagation(); handleRemoveUdt(name) }}>✕</button></Tooltip>
              </span>
            ))}
          </div>
        )}
      </div>

      {/* ─── DB 文件导入 ──────────────────────────────── */}
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
                  <Tooltip content="重新注册到当前连接"><button className="btn btn--primary db-card__refresh" onClick={() => handleRefresh(key)}>↻</button></Tooltip>
                  <Tooltip content="删除此 DB"><button className="btn btn--danger db-card__del" onClick={() => handleRemove(key)}>✕</button></Tooltip>
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
                    const liveKey = `${db.dbName}:${v.name}`
                    const live = liveData?.[liveKey]
                    const showVal = live?.value !== undefined && live?.value !== null
                    const isEditing = editing === v.name

                    return (
                    <div key={v.name} className="db-import__row">
                      <span className="db-import__r-name" title={v.comment}>{v.name}</span>

                      <span className="db-import__r-ctrl">
                        {v.type === 'bool' ? (<>
                          <button className="db-import__momentary"
                            onMouseDown={e => { (e.currentTarget as HTMLElement).dataset.pressed = '1'; writeVal(db.dbName, db.dbNumber, v.name, 1) }}
                            onMouseUp={e => { (e.currentTarget as HTMLElement).dataset.pressed = ''; writeVal(db.dbName, db.dbNumber, v.name, 0) }}
                            onMouseLeave={e => { if ((e.currentTarget as HTMLElement).dataset.pressed) { (e.currentTarget as HTMLElement).dataset.pressed = ''; writeVal(db.dbName, db.dbNumber, v.name, 0) } }}
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
      </CollapsibleSection>
      {udtDetail && (
        <div className="modal-overlay" onMouseDown={e => { if (e.target === e.currentTarget) setUdtDetail(null) }}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ width: 480 }}>
            <h3 className="modal-title">🔷 {udtDetail.name} 字段列表</h3>
            <div className="modal-form">
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--muted-foreground)' }}>
                    <th style={{ padding: '6px 8px' }}>字段名</th>
                    <th style={{ padding: '6px 8px' }}>类型</th>
                    <th style={{ padding: '6px 8px' }}>位</th>
                  </tr>
                </thead>
                <tbody>
                  {udtDetail.fields.map(f => (
                    <tr key={f.name} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '6px 8px', fontFamily: 'Consolas, monospace' }}>{f.name}</td>
                      <td style={{ padding: '6px 8px' }}>
                        <span style={{ color: typeColor(f.type), fontWeight: 500, fontSize: 12 }}>{f.type.toUpperCase()}</span>
                      </td>
                      <td style={{ padding: '6px 8px', color: 'var(--muted-foreground)' }}>{f.bit !== undefined ? `${f.bit}` : '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="modal-actions">
              <button className="btn btn--primary" onClick={() => setUdtDetail(null)}>关闭</button>
            </div>
          </div>
        </div>
      )}
    </React.Fragment>
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

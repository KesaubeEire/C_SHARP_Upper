import { useState, useEffect, useCallback, useRef } from 'react'
import {
  fetchSnapshot, writeValue, toggleBit, importDB, importUDT, fetchTriggers, createTrigger, deleteTrigger,
  fetchImportedDBs, deleteImportedDB, refreshImportedDB, writeImportedField, randomizeImportedField,
  fetchImportedUDTs, fetchImportedUDTDetail, deleteImportedUDT,
  fetchDbs, upsertDb, deleteDb,
  fetchScripts, saveScripts,
} from './api'
import type { VPLCSnapshot, Trigger, ImportedDB, UDTDetail, ScriptConfig } from './api'
import './App.css'

type Tab = 'monitor' | 'import' | 'triggers' | 'scripts'

const START_TIME = Date.now()

// ── 独立地址存储 I/Q/M ──
const KEYS = { I: 'vplc_addrs_i', Q: 'vplc_addrs_q', M: 'vplc_addrs_m' }
function loadAddrs(area: string): string { return localStorage.getItem(KEYS[area as keyof typeof KEYS]) || '0,1,8' }
function saveAddrs(area: string, v: string) { localStorage.setItem(KEYS[area as keyof typeof KEYS], v) }
function parseAddrs(s: string): number[] {
  return [...new Set(s.split(',').map(n => parseInt(n.trim())).filter(n => !isNaN(n) && n >= 0))].sort((a, b) => a - b)
}

function rndByte() { return Math.floor(Math.random() * 256) }
function rndWord() { return Math.floor(Math.random() * 65536) }
function rndFloat() { return parseFloat((Math.random() * 200 - 100).toFixed(4)) }

async function randomByte(area: string, addr: number) {
  const val = rndByte()
  await writeValue(area, 0, addr, 'byte', val)
  return val
}
async function randomWord(area: string, addr: number) {
  const val = rndWord()
  for (let i = 0; i < 2; i++) await writeValue(area, 0, addr + i, 'byte', (val >> (8 * (1 - i))) & 0xFF)
  return val
}
async function randomFloat(area: string, addr: number) {
  const val = rndFloat()
  const buf = new ArrayBuffer(4)
  new DataView(buf).setFloat32(0, val, false)
  for (let i = 0; i < 4; i++) await writeValue(area, 0, addr + i, 'byte', new Uint8Array(buf)[i])
  return val
}

export default function App() {
  const [tab, setTab] = useState<Tab>('monitor')
  const [snap, setSnap] = useState<VPLCSnapshot | null>(null)
  const [iAddrs, setIAddrs] = useState(() => loadAddrs('I'))
  const [qAddrs, setQAddrs] = useState(() => loadAddrs('Q'))
  const [mAddrs, setMAddrs] = useState(() => loadAddrs('M'))
  const [triggers, setTriggers] = useState<Trigger[]>([])
  const [importedDBs, setImportedDBs] = useState<ImportedDB[]>([])
  const [udtNames, setUdtNames] = useState<string[]>([])
  const [udtDetail, setUdtDetail] = useState<UDTDetail | null>(null)
  const [toast, setToast] = useState('')
  const [uptime, setUptime] = useState(0)

  const showToast = useCallback((msg: string) => { setToast(msg); setTimeout(() => setToast(''), 2000) }, [])

  const refresh = useCallback(async () => {
    const d = await fetchSnapshot()
    setSnap(d)
    setUptime(Math.floor((Date.now() - START_TIME) / 1000))
  }, [])

  useEffect(() => { refresh(); const t = setInterval(refresh, 300); return () => clearInterval(t) }, [refresh])
  useEffect(() => { if (tab === 'triggers') fetchTriggers().then(setTriggers).catch(() => {}) }, [tab])
  useEffect(() => {
    if (tab !== 'import') return
    fetchImportedDBs().then(setImportedDBs).catch(() => {})
    fetchImportedUDTs().then(setUdtNames).catch(() => {})
  }, [tab, snap])

  const handleToggleBit = useCallback(async (area: string, addr: number, bit: number) => {
    await toggleBit(area, addr, bit)
    refresh()
  }, [refresh])

  const handleRandom = useCallback(async (prefix: string, addr: number) => {
    const val = await randomByte(prefix, addr)
    showToast(`${prefix}${addr} = 0x${val.toString(16).padStart(2, '0')} (${val})`)
    refresh()
  }, [refresh, showToast])

  return (
    <div className="app">
      <div className={'toast' + (toast ? ' show' : '')}>{toast}</div>
      <h1 className="app-title">🔌 虚拟 S7-1200 PLC</h1>
      <p className="app-subtitle">S7 端口 1200 | Web 端口 1201 | {uptime}s</p>

      <div className="tabs">
        {(['monitor', 'import', 'triggers', 'scripts'] as Tab[]).map(t => (
          <button key={t} className={'tab' + (tab === t ? ' active' : '')} onClick={() => setTab(t)}>
            {t === 'monitor' ? '📊 监视' : t === 'import' ? '📥 导入' : t === 'triggers' ? '⚡ 触发器' : '📜 脚本'}
          </button>
        ))}
      </div>

      {tab === 'monitor' && (
        <MonitorTab
          snap={snap}
          iAddrs={iAddrs} qAddrs={qAddrs} mAddrs={mAddrs}
          setIAddrs={setIAddrs} setQAddrs={setQAddrs} setMAddrs={setMAddrs}
          onToggleBit={handleToggleBit} onRandom={handleRandom}
        />
      )}

      {tab === 'import' && <ImportTab snap={snap} importedDBs={importedDBs} udtNames={udtNames} udtDetail={udtDetail} setUdtDetail={setUdtDetail} onRefresh={refresh} showToast={showToast} />}
      {tab === 'triggers' && (
        <TriggersTab triggers={triggers} setTriggers={setTriggers} snap={snap} onRefresh={refresh} showToast={showToast} />
      )}
      {tab === 'scripts' && <ScriptsTab showToast={showToast} />}
    </div>
  )
}

// ── 监视 Tab ──
function MonitorTab({ snap, iAddrs, qAddrs, mAddrs, setIAddrs, setQAddrs, setMAddrs, onToggleBit, onRandom }: {
  snap: VPLCSnapshot | null
  iAddrs: string; qAddrs: string; mAddrs: string
  setIAddrs: (v: string) => void; setQAddrs: (v: string) => void; setMAddrs: (v: string) => void
  onToggleBit: (area: string, addr: number, bit: number) => void
  onRandom: (prefix: string, addr: number) => void
}) {
  const [dbView, setDbView] = useState<'card' | 'table'>('card')
  const parsed = snap?._parsed
  const st = parsed?.state
  const isRun = st?.mode === 'RUN'
  const isStop = st?.mode === 'STOP'
  const leds = parsed?.leds || {}
  const areas = [
    { prefix: 'I', label: '🟡 输入点 (I 区)', data: snap?.PE, addrs: iAddrs, setAddrs: setIAddrs },
    { prefix: 'Q', label: '🔵 输出点 (Q 区)', data: snap?.PA, addrs: qAddrs, setAddrs: setQAddrs },
    { prefix: 'M', label: '🟣 M 区', data: snap?.MK, addrs: mAddrs, setAddrs: setMAddrs },
  ]

  return (
    <div>
      {/* PLC 状态栏 */}
      <div className="status-bar">
        <div className="status-item">
          <span className={'status-led ' + (isRun ? 'led-on' : '')} style={{background: leds.run?.state === 'on' ? '#1D9E75' : leds.run?.state === 'blink' ? '#5F5E5A' : '#2E3133'}} />
          <span className="status-label">RUN</span>
        </div>
        <div className="status-item">
          <span className={'status-led ' + (isStop ? 'led-on' : '')} style={{background: leds.stop?.state === 'on' ? '#E8A838' : '#2E3133'}} />
          <span className="status-label">STOP</span>
        </div>
        <div className="status-item">
          <span className={'status-led'} style={{background: leds.error?.state === 'on' ? '#E24B4A' : '#2E3133'}} />
          <span className="status-label">ERROR</span>
        </div>
        <div className="status-item" style={{marginLeft:16, color: isRun ? '#1D9E75' : '#E8A838', fontWeight:600, fontSize:13}}>
          {st?.mode || '--'}
        </div>
        <div className="status-item" style={{marginLeft:'auto', color:'#7A7872', fontSize:11}}>
          🕒 {parsed?.rtc ? new Date(parsed.rtc).toLocaleTimeString() : '--'}
        </div>
      </div>
      {areas.map(({ prefix, label, data, addrs, setAddrs }) => (
        <div key={prefix} className="io-panel">
          <div className="io-title">{label}</div>
          <div className="addr-input">
            <span className="addr-label">显示地址</span>
            <input className="addr-field" value={addrs} onChange={e => { setAddrs(e.target.value); saveAddrs(prefix, e.target.value) }} placeholder="0,1,8" />
          </div>
          {data ? (
            <div className="io-table">
              <div className="io-row io-header">
                <span className="io-addr">地址</span>
                {[0, 1, 2, 3, 4, 5, 6, 7].map(b => <span key={b} className="io-bit-label">{b}</span>)}
                <span className="io-hex">HEX</span>
                <span className="io-rnd">随机</span>
              </div>
              {parseAddrs(addrs).map(addr => {
                const v = data[addr] ?? 0
                return (
                  <div key={addr} className="io-row">
                    <span className="io-addr">{prefix}{addr}</span>
                    {[0, 1, 2, 3, 4, 5, 6, 7].map(b => (
                      <span key={b} className={'io-bit' + ((v >> b) & 1 ? ' on' : ' off')}
                        onClick={() => onToggleBit(prefix, addr, b)} />
                    ))}
                    <span className="io-hex">0x{v.toString(16).padStart(2, '0').toUpperCase()}</span>
                    <button className="btn btn-sm" onClick={() => onRandom(prefix, addr)}>随机</button>
                  </div>
                )
              })}
            </div>
          ) : <div className="empty">加载中...</div>}
        </div>
      ))}
      {snap?._imported && snap._imported.length > 0 && (
        <>
          <div className="section-header">
            <h2 className="section-title">📦 已导入 DB</h2>
            <div className="view-toggle">
              <button className={'toggle-btn' + (dbView === 'card' ? ' active' : '')} onClick={() => setDbView('card')}>▦ 卡片</button>
              <button className={'toggle-btn' + (dbView === 'table' ? ' active' : '')} onClick={() => setDbView('table')}>⊞ 表格</button>
            </div>
          </div>
          {dbView === 'card' && snap._imported.map(imp => {
            const fs = snap.fields?.[imp.dbName]
            return (
              <div key={imp.dbName} className="db-card">
                <div className="db-card-title">{imp.dbName} (DB{imp.dbNumber})</div>
                {fs && <div className="db-fields">
                  {Object.entries(fs.values).map(([k, v]) => (
                    <span key={k} className="db-field">
                      <span className="db-field-name">{k}:</span>
                      <span className="db-field-val">{v !== null && v !== undefined ? String(v) : '--'}</span>
                    </span>
                  ))}
                </div>}
              </div>
            )
          })}
          {dbView === 'table' && snap._imported.map(imp => {
            const fs = snap.fields?.[imp.dbName]
            return (
              <div key={imp.dbName} className="io-panel" style={{marginBottom:8}}>
                <div className="io-title">{imp.dbName} (DB{imp.dbNumber})</div>
                {fs && <div className="io-table">
                  <div className="io-row io-header">
                    <span className="io-addr" style={{width:120}}>字段</span>
                    <span className="io-hex" style={{textAlign:'left',width:80}}>类型</span>
                    <span className="io-hex" style={{textAlign:'left',flex:1}}>值</span>
                  </div>
                  {Object.entries(fs.values).map(([k, v]) => {
                    const meta = fs.fieldMeta?.[k]
                    return (
                      <div key={k} className="io-row">
                        <span className="io-addr" style={{width:120,color:'#378ADD'}} title={meta?.comment}>{k}</span>
                        <span className="io-hex" style={{textAlign:'left',width:80,color:'#7A7872'}}>{(meta?.type || '').toUpperCase()}</span>
                        <span className="io-hex" style={{textAlign:'left',flex:1}}>{v !== null && v !== undefined ? String(v) : '--'}</span>
                      </div>
                    )
                  })}
                </div>}
              </div>
            )
          })}
        </>
      )}
    </div>
  )
}

// ── 导入 Tab ──
function ImportTab({ snap, importedDBs, udtNames, udtDetail, setUdtDetail, onRefresh, showToast }: {
  snap: VPLCSnapshot | null
  importedDBs: ImportedDB[]
  udtNames: string[]
  udtDetail: UDTDetail | null
  setUdtDetail: (detail: UDTDetail | null) => void
  onRefresh: () => void
  showToast: (msg: string) => void
}) {
  const [editing, setEditing] = useState<string | null>(null)
  const [editVal, setEditVal] = useState('')
  const [dbsCfg, setDbsCfg] = useState<Record<string, number>>({})
  const [newDbNum, setNewDbNum] = useState('')
  const [newDbSize, setNewDbSize] = useState('64')
  const [pendingDbImport, setPendingDbImport] = useState<{content: string; dbNum: number} | null>(null)
  const udtInputRef = useRef<HTMLInputElement>(null)
  const dbInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => { fetchDbs().then(setDbsCfg).catch(() => {}) }, [])

  const handleDbFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const input = prompt(`映射到哪个 DB 块？\n（文件本身有块号，但你可以改成其他号）`, '')
    if (input === null) { e.target.value = ''; return }
    const dbNum = parseInt(input)
    if (isNaN(dbNum) || dbNum < 1) { showToast('❌ 无效 DB 号'); e.target.value = ''; return }
    const content = await file.text()
    try {
      const r = await importDB(content, dbNum)
      if (r.success) {
        showToast(`✅ 已导入 DB${dbNum}（${r.variableCount} 字段）`)
        setPendingDbImport(null)
      } else if (r.missingUdt && r.missingUdt.length > 0) {
        showToast(`❌ 缺少 UDT: ${r.missingUdt.join(', ')}，请先上传 .udt 文件`)
        setPendingDbImport({ content, dbNum })
      } else {
        showToast(`❌ ${r.error}`)
      }
    } catch { showToast('❌ 导入失败') }
    onRefresh()
    e.target.value = ''
    ;(document.activeElement as HTMLElement)?.blur()
  }

  const handleUdtFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const content = await file.text()
    try {
      const r = await importUDT(content)
      if (r.success) {
        showToast(`✅ 已导入 ${r.count} 个 UDT`)
        // 如果有待处理的 DB 导入，自动重试
        if (pendingDbImport) {
          const retry = await importDB(pendingDbImport.content, pendingDbImport.dbNum)
          if (retry.success) {
            showToast(`✅ DB${pendingDbImport.dbNum} 导入成功（${retry.variableCount} 字段）`)
            setPendingDbImport(null)
          } else if (retry.missingUdt) {
            showToast(`❌ 还缺 UDT: ${retry.missingUdt.join(', ')}`)
          } else {
            showToast(`❌ ${retry.error}`)
          }
        }
      } else showToast(`❌ ${r.error}`)
    } catch { showToast('❌ UDT 导入失败') }
    onRefresh()
    e.target.value = ''
    ;(document.activeElement as HTMLElement)?.blur()
  }

  const writeField = async (key: string, fieldName: string, value: number | boolean) => {
    const r = await writeImportedField(key, fieldName, value)
    if (r.success) onRefresh()
    else showToast(`❌ ${r.error || '写入失败'}`)
  }

  const randomizeField = async (key: string, fieldName: string) => {
    const r = await randomizeImportedField(key, fieldName)
    if (r.success) {
      showToast(`✅ 随机值 = ${String(r.value)}`)
      onRefresh()
    } else showToast(`❌ ${r.error || '随机失败'}`)
  }

  return (
    <div>
      <div className="udt-import">
        <div className="db-import__bar">
          <button className="btn btn-sm" onClick={() => udtInputRef.current?.click()}>选择 UDT 文件</button>
          <input ref={udtInputRef} type="file" accept=".db,.udt" style={{ display: 'none' }} onChange={handleUdtFile} />
          {udtNames.length > 0 && <div className="udt-tags">{udtNames.map(name => (
            <span key={name} className="udt-tag" onClick={async () => setUdtDetail(await fetchImportedUDTDetail(name))}>
              {name}
              <button className="udt-tag__del" onClick={async e => { e.stopPropagation(); await deleteImportedUDT(name); onRefresh() }}>✕</button>
            </span>
          ))}</div>}
        </div>
      </div>

      <div className="db-import__bar">
        <button className="btn btn-primary" onClick={() => dbInputRef.current?.click()}>选择 .db 文件</button>
        <input ref={dbInputRef} type="file" accept=".db" style={{ display: 'none' }} onChange={handleDbFile} />
      </div>

      <div className="dbs-config">
        <h3 className="section-title">🧱 DB 块管理</h3>
        <div className="dbs-config__list">
          {Object.entries(dbsCfg).sort(([a], [b]) => Number(a) - Number(b)).map(([num, size]) => (
            <div key={num} className="dbs-config__item">
              <span className="dbs-config__label">DB{num}</span>
              <span className="dbs-config__size">{size} 字节</span>
              <button className="btn btn-sm" onClick={async () => {
                const s = prompt(`DB${num} 新大小（字节）：`, String(size))
                if (!s) return
                await upsertDb(Number(num), parseInt(s) || 64)
                fetchDbs().then(setDbsCfg)
              }}>✎</button>
              <button className="btn btn-sm" onClick={async () => {
                await deleteDb(Number(num))
                fetchDbs().then(setDbsCfg)
              }}>✕</button>
            </div>
          ))}
        </div>
        <div className="dbs-config__add">
          <input className="addr-field" placeholder="DB 号" value={newDbNum} onChange={e => setNewDbNum(e.target.value)} style={{ width: 60 }} />
          <input className="addr-field" placeholder="字节" value={newDbSize} onChange={e => setNewDbSize(e.target.value)} style={{ width: 60 }} />
          <button className="btn btn-primary btn-sm" onClick={async () => {
            const n = parseInt(newDbNum)
            if (isNaN(n) || n < 1) return
            await upsertDb(n, parseInt(newDbSize) || 64)
            setNewDbNum(''); setNewDbSize('64')
            fetchDbs().then(setDbsCfg)
          }}>+ 添加 DB</button>
        </div>
      </div>

      {importedDBs.length === 0 ? <div className="db-empty">尚未导入 DB 文件</div> : (
        <div className="db-import__list">
          {importedDBs.map(db => {
            const key = `${db.dbNumber}_${db.dbName}`
            const live = snap?.fields?.[db.dbName]
            return (
              <div key={key} className="db-import__card">
                <div className="db-card__header">
                  <span className="db-card__label">{db.dbName}</span>
                  <span className="db-card__info">{db.variableCount} 个变量</span>
                  <button className="btn btn-primary db-card__refresh" onClick={async () => { await refreshImportedDB(key); onRefresh() }}>↻</button>
                  <button className="btn db-card__del" onClick={async () => { await deleteImportedDB(key); onRefresh() }}>✕</button>
                </div>
                <div className="db-import__vars">
                  <div className="db-import__header">
                    <span className="db-import__h-name">名称</span>
                    <span className="db-import__h-ctrl">操作</span>
                    <span className="db-import__h-val">值</span>
                    <span className="db-import__h-type">类型</span>
                    <span className="db-import__h-off">偏移</span>
                  </div>
                  {db.variables.map(v => {
                    const rowKey = `${key}:${v.name}`
                    const value = live?.values?.[v.name]
                    const isEditing = editing === rowKey
                    return (
                      <div key={rowKey} className="db-import__row">
                        <span className="db-import__r-name" title={v.comment}>{v.name}</span>
                        <span className="db-import__r-ctrl">
                          {v.type === 'bool' ? (
                            <>
                              <button className="db-import__momentary" onMouseDown={() => writeField(key, v.name, true)} onMouseUp={() => writeField(key, v.name, false)} onMouseLeave={() => writeField(key, v.name, false)}>按1松0</button>
                              <button className="db-import__toggle" onClick={() => writeField(key, v.name, !value)}>取反</button>
                            </>
                          ) : null}
                          <button className="db-import__toggle" onClick={() => randomizeField(key, v.name)}>随机</button>
                        </span>
                        <span className="db-import__r-val">
                          {v.type === 'bool' ? (
                            <span className={`db-import__cell-val ${value ? 'db-import__cell-val--on' : ''}`}>{value ? '1' : '0'}</span>
                          ) : isEditing ? (
                            <input className="db-import__edit-input" value={editVal} onChange={e => setEditVal(e.target.value)} onBlur={async () => { await writeField(key, v.name, Number(editVal)); setEditing(null) }} onKeyDown={async e => {
                              if (e.key === 'Enter') { await writeField(key, v.name, Number(editVal)); setEditing(null) }
                              if (e.key === 'Escape') setEditing(null)
                            }} autoFocus />
                          ) : (
                            <span className="db-import__cell-val db-import__cell-edit" onClick={() => { setEditing(rowKey); setEditVal(value !== undefined ? String(value) : '0') }}>{value !== undefined ? String(value) : '--'}</span>
                          )}
                        </span>
                        <span className="db-import__r-type">{v.type.toUpperCase()}</span>
                        <span className="db-import__r-off">@{v.offset}{v.bit !== undefined ? `.${v.bit}` : ''}</span>
                      </div>
                    )
                  })}
                </div>
              </div>
            )
          })}
        </div>
      )}

      {udtDetail && (
        <div className="modal-overlay" onMouseDown={e => { if (e.target === e.currentTarget) setUdtDetail(null) }}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h3 className="modal-title">{udtDetail.name} 字段列表</h3>
            <div className="modal-form">
              <table className="db-table">
                <thead><tr><th>字段名</th><th>类型</th><th>位</th></tr></thead>
                <tbody>{udtDetail.fields.map(f => <tr key={f.name}><td>{f.name}</td><td>{f.type}</td><td>{f.bit ?? '-'}</td></tr>)}</tbody>
              </table>
            </div>
            <div className="modal-actions"><button className="btn btn-primary" onClick={() => setUdtDetail(null)}>关闭</button></div>
          </div>
        </div>
      )}
    </div>
  )
}

// ── 触发器 Tab ──
function TriggersTab({ triggers, setTriggers, snap, onRefresh, showToast }: {
  triggers: Trigger[]; setTriggers: (t: Trigger[]) => void
  snap: VPLCSnapshot | null; onRefresh: () => void; showToast: (msg: string) => void
}) {
  const [showForm, setShowForm] = useState(false)
  const dbKeys = snap ? Object.keys(snap.DB || {}).map(k => parseInt(k.replace('DB', ''))).sort((a, b) => a - b) : []

  const handleCreate = async () => {
    const name = (document.getElementById('trg-name') as HTMLInputElement)?.value || 'trigger'
    const body = {
      name, enabled: true,
      sourceDb: parseInt((document.getElementById('trg-src-db') as HTMLSelectElement)?.value || '1'),
      sourceOffset: parseInt((document.getElementById('trg-src-off') as HTMLInputElement)?.value || '0'),
      sourceType: 'bit',
      sourceBit: parseInt((document.getElementById('trg-src-bit') as HTMLInputElement)?.value) || undefined,
      condition: (document.getElementById('trg-cond') as HTMLSelectElement)?.value || 'eq',
      threshold: parseFloat((document.getElementById('trg-threshold') as HTMLInputElement)?.value || '0'),
      targetDb: parseInt((document.getElementById('trg-tgt-db') as HTMLSelectElement)?.value || '1'),
      targetOffset: parseInt((document.getElementById('trg-tgt-off') as HTMLInputElement)?.value || '0'),
      targetType: (document.getElementById('trg-type') as HTMLSelectElement)?.value || 'real',
      targetBit: parseInt((document.getElementById('trg-tgt-bit') as HTMLInputElement)?.value) || undefined,
      targetValue: parseFloat((document.getElementById('trg-value') as HTMLInputElement)?.value || '0'),
    }
    await createTrigger(body)
    showToast('✅ 已创建触发器')
    setShowForm(false)
    fetchTriggers().then(setTriggers).catch(() => {})
  }

  return (
    <div>
      <div className="flex">
        <button className="btn btn-primary btn-sm" onClick={() => setShowForm(!showForm)}>+ 新建触发器</button>
      </div>
      {showForm && (
        <div className="trigger-form">
          <div className="form-row"><span className="form-label">名称</span><input id="trg-name" className="addr-field" defaultValue={`trigger_${Date.now() % 1000}`} /></div>
          <div className="form-row"><span className="form-label">源 DB</span><select id="trg-src-db">{dbKeys.map(n => <option key={n} value={n}>DB{n}</option>)}</select>
            <span className="form-label">偏移</span><input id="trg-src-off" className="addr-field" type="number" defaultValue="0" style={{ width: 60 }} />
            <span className="form-label">位</span><input id="trg-src-bit" className="addr-field" type="number" style={{ width: 50 }} placeholder="-" /></div>
          <div className="form-row">
            <span className="form-label">条件</span>
            <select id="trg-cond"><option value="eq">=</option><option value="ne">≠</option><option value="gt">&gt;</option><option value="lt">&lt;</option><option value="ge">≥</option><option value="le">≤</option></select>
            <span className="form-label">阈值</span><input id="trg-threshold" className="addr-field" type="number" defaultValue="1" style={{ width: 60 }} />
          </div>
          <div className="form-row"><span className="form-label">目标 DB</span><select id="trg-tgt-db">{dbKeys.map(n => <option key={n} value={n}>DB{n}</option>)}</select>
            <span className="form-label">偏移</span><input id="trg-tgt-off" className="addr-field" type="number" defaultValue="0" style={{ width: 60 }} />
            <span className="form-label">位</span><input id="trg-tgt-bit" className="addr-field" type="number" style={{ width: 50 }} placeholder="-" /></div>
          <div className="form-row">
            <span className="form-label">类型</span><select id="trg-type"><option value="real">REAL</option><option value="bit">位</option><option value="byte">字节</option><option value="word">字</option></select>
            <span className="form-label">值</span><input id="trg-value" className="addr-field" type="number" defaultValue="0" style={{ width: 80 }} />
            <button className="btn btn-primary btn-sm" onClick={handleCreate}>保存</button>
            <button className="btn btn-sm" onClick={() => setShowForm(false)}>取消</button>
          </div>
        </div>
      )}
      {triggers.length === 0 ? <div className="empty">暂无触发器</div> : (
        triggers.map(t => {
          const condLabels: Record<string, string> = { eq: '=', ne: '≠', gt: '>', lt: '<', ge: '≥', le: '≤' }
          const sl = `DB${t.sourceDb}.${t.sourceOffset}${t.sourceBit !== undefined ? '.' + t.sourceBit : ''}`
          const tl = `DB${t.targetDb}.${t.targetOffset}${t.targetBit !== undefined ? '.' + t.targetBit : ''}`
          return (
            <div key={t.id} className="trigger-card" style={{ opacity: t.enabled ? 1 : 0.5 }}>
              <div className="flex">
                <span className={'tag ' + (t.active ? 'tag-on' : 'tag-off')}>{t.active ? '触发' : '待命'}</span>
                <span style={{ flex: 1, fontWeight: 500 }}>{t.name}</span>
                <span className="tag tag-cond">{condLabels[t.condition] || t.condition} {t.threshold}</span>
                <span style={{ color: '#7A7872' }}>→</span>
                <span className="tag tag-action">{tl} = {t.targetValue}</span>
                <span className="del" onClick={async () => { await deleteTrigger(t.id); fetchTriggers().then(setTriggers) }}>✕</span>
              </div>
              <div className="trigger-desc">{sl} {condLabels[t.condition] || t.condition} {t.threshold} → {tl} = {t.targetValue}</div>
            </div>
          )
        })
      )}
    </div>
  )
}

// ── 脚本 Tab ──

const SCRIPT_TEMPLATE = `// 在 OB 周期执行的 JavaScript
// API: readByte/writeByte/readBit/writeBit/readReal/writeReal/readInt/writeInt/log/now
// area: 'I' | 'Q' | 'M' | 'DB'
if (readBit('I', 0, 0, 0)) {
  const pos = readReal('DB', 6, 38)
  writeReal('DB', 6, 38, pos + 0.5)
  log('position:', pos + 0.5)
}`

function ScriptsTab({ showToast }: { showToast: (msg: string) => void }) {
  const [scripts, setScripts] = useState<ScriptConfig[]>([])
  const [loading, setLoading] = useState(true)
  const [editingIdx, setEditingIdx] = useState<number | null>(null)
  const [editSource, setEditSource] = useState('')
  const [dirty, setDirty] = useState(false)

  const load = useCallback(async () => {
    try { setScripts(await fetchScripts()) } catch {} finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  const handleSave = async () => {
    await saveScripts(scripts)
    setDirty(false)
    showToast('✅ 脚本已保存')
  }

  const handleAdd = () => {
    setScripts(prev => [...prev, { name: `script_${prev.length + 1}`, source: SCRIPT_TEMPLATE, obNumber: 1, enabled: true }])
    setDirty(true)
  }

  const handleDelete = (idx: number) => {
    setScripts(prev => prev.filter((_, i) => i !== idx))
    if (editingIdx === idx) setEditingIdx(null)
    setDirty(true)
  }

  const updateScript = (idx: number, patch: Partial<ScriptConfig>) => {
    setScripts(prev => prev.map((s, i) => i === idx ? { ...s, ...patch } : s))
    setDirty(true)
  }

  if (loading) return <div className="empty">加载中...</div>

  return (
    <div>
      <div className="flex" style={{ marginBottom: 10 }}>
        <button className="btn btn-primary btn-sm" onClick={handleAdd}>+ 新建脚本</button>
        {dirty && <button className="btn btn-primary btn-sm" onClick={handleSave}>💾 保存</button>}
        <span style={{ color: '#7A7872', fontSize: 11, marginLeft: 'auto' }}>脚本在 OB 周期执行，超时 100ms</span>
      </div>

      {scripts.length === 0 ? (
        <div className="empty">暂无脚本 — 点击「+ 新建脚本」添加</div>
      ) : (
        <div className="scripts-list">
          {scripts.map((script, idx) => (
            <div key={idx} className="script-card">
              <div className="script-card__header">
                <input className="script-name-input" value={script.name}
                  onChange={e => updateScript(idx, { name: e.target.value })} placeholder="脚本名称" />
                <select className="script-ob-select" value={script.obNumber}
                  onChange={e => updateScript(idx, { obNumber: Number(e.target.value) })}>
                  <option value={1}>OB1 (自由循环)</option>
                  <option value={35}>OB35 (500ms)</option>
                  <option value={100}>OB100 (启动)</option>
                </select>
                <label className="script-toggle-label">
                  <input type="checkbox" checked={script.enabled}
                    onChange={e => updateScript(idx, { enabled: e.target.checked })} />
                  <span>{script.enabled ? '已启用' : '已禁用'}</span>
                </label>
                <button className="btn btn-sm" onClick={() => {
                  if (editingIdx === idx) { setEditingIdx(null); return }
                  setEditingIdx(idx); setEditSource(script.source)
                }}>{editingIdx === idx ? '收起' : '编辑'}</button>
                <span className="del" onClick={() => handleDelete(idx)}>✕</span>
              </div>

              {editingIdx === idx && (
                <div className="script-editor">
                  <textarea className="script-source" value={editSource}
                    onChange={e => setEditSource(e.target.value)}
                    onBlur={() => updateScript(idx, { source: editSource })}
                    spellCheck={false} rows={12} />
                  <div className="script-editor__hint">
                    API: readByte/writeByte/readBit/writeBit/readReal/writeReal/readInt/writeInt/log/now
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

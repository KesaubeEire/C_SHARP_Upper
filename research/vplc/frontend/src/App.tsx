import { useState, useEffect, useCallback, useRef, createElement } from 'react'
import {
  fetchSnapshot, writeValue, toggleBit, fetchTriggers, createTrigger, deleteTrigger,
  fetchDbs, upsertDb, deleteDb,
  fetchScripts, saveScripts,
  fetchDBEditors, saveDBEditor, deleteDBEditor, writeDBEditorField, importDBEditorDB, importDBEditorUDT,
  exportDBEditorDB, randomizeDBEditorField,
} from './api'
import type { VPLCSnapshot, Trigger, ScriptConfig, DBEditorDef, DBEditorField } from './api'
import './App.css'

type Tab = 'monitor' | 'db-editor' | 'triggers' | 'scripts'

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
        {(['monitor', 'db-editor', 'triggers', 'scripts'] as Tab[]).map(t => (
          <button key={t} className={'tab' + (tab === t ? ' active' : '')} onClick={() => setTab(t)}>
            {t === 'monitor' ? '📊 监视' : t === 'db-editor' ? '📝 DB 编辑' : t === 'triggers' ? '⚡ 触发器' : '📜 脚本'}
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

      {tab === 'db-editor' && <DBEditorTab showToast={showToast} onRefresh={refresh} />}
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

// ── DB Editor Tab（博图风格 DB 编辑）──

const DB_EDITOR_TYPES = ['Bool', 'Byte', 'Word', 'Int', 'DWord', 'DInt', 'Real', 'Char', 'SInt', 'USInt', 'UInt', 'UDInt', 'Time', 'Date', 'TOD', 'LReal', 'LWord', 'LInt']

function DBEditorTab({ showToast, onRefresh }: { showToast: (msg: string) => void; onRefresh: () => void }) {
  const [editors, setEditors] = useState<DBEditorDef[]>([])
  const [udtNames, setUdtNames] = useState<string[]>([])
  const [loading, setLoading] = useState(true)

  // 新建 DB 表单
  const [showNewForm, setShowNewForm] = useState(false)
  const [newDbNum, setNewDbNum] = useState('')
  const [newDbName, setNewDbName] = useState('')

  // 当前编辑中的 DB
  const [editingKey, setEditingKey] = useState<string | null>(null)
  const [editingFields, setEditingFields] = useState<DBEditorField[]>([])
  const [editingName, setEditingName] = useState('')
  const [editingNum, setEditingNum] = useState(0)
  const [dirty, setDirty] = useState(false)

  // 导入待处理
  const [pendingImport, setPendingImport] = useState<{ content: string; dbNum: number } | null>(null)

  // 编辑行焦点
  const [focusIdx, setFocusIdx] = useState<number | null>(null)

  // 监视合一：实时值
  const [liveValues, setLiveValues] = useState<Record<string, any>>({})
  // 批量添加弹窗
  const [showBatch, setShowBatch] = useState(false)
  const [batchText, setBatchText] = useState('')

  // 实时值轮询
  const livePollRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const startLivePoll = useCallback((key: string) => {
    if (livePollRef.current) clearInterval(livePollRef.current)
    const poll = async () => {
      try {
        const r = await (await fetch('/api/vplc/db-editor/' + encodeURIComponent(key) + '/values')).json()
        if (r.values) setLiveValues(r.values)
      } catch {}
    }
    poll()
    livePollRef.current = setInterval(poll, 500)
  }, [])
  const stopLivePoll = useCallback(() => {
    if (livePollRef.current) { clearInterval(livePollRef.current); livePollRef.current = null }
  }, [])

  const [toastMsg, setToastMsg] = useState('')
  const localToast = (msg: string) => { setToastMsg(msg); setTimeout(() => setToastMsg(''), 2000) }

  const udtInputRef = useRef<HTMLInputElement>(null)
  const dbInputRef = useRef<HTMLInputElement>(null)

  const load = useCallback(async () => {
    try { setEditors(await fetchDBEditors()) } catch {} finally { setLoading(false) }
    try {
      const udts = await (await fetch('/api/vplc/imported-udts')).json()
      setUdtNames(udts)
    } catch {}
  }, [])

  useEffect(() => { load() }, [load])

  // ── 导入 .db 文件 ──
  const handleImportDbFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const input = prompt('映射到哪个 DB 块？（文件本身有块号，但你可以改成其他号）', '')
    if (input === null) { e.target.value = ''; return }
    const dbNum = parseInt(input)
    if (isNaN(dbNum) || dbNum < 1) { showToast('❌ 无效 DB 号'); e.target.value = ''; return }
    const content = await file.text()
    try {
      const r = await importDBEditorDB(content, dbNum)
      if (r.success) {
        const saveR = await saveDBEditor(r.dbNumber, r.dbName, r.fields)
        if (saveR.success) {
          showToast(`✅ 已导入 DB${r.dbNumber}（${r.variableCount} 字段），可在编辑器中修改`)
          setPendingImport(null)
          load()
        } else showToast(`❌ ${saveR.error || '保存失败'}`)
      } else if (r.missingUdt && r.missingUdt.length > 0) {
        showToast(`❌ 缺少 UDT: ${r.missingUdt.join(', ')}，请先导入 .udt 文件`)
        setPendingImport({ content, dbNum })
      } else {
        showToast(`❌ ${r.error}`)
      }
    } catch { showToast('❌ 导入失败') }
    e.target.value = ''
  }

  // ── 导入 .udt 文件 ──
  const handleImportUdtFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const content = await file.text()
    try {
      const r = await importDBEditorUDT(content)
      if (r.success) {
        showToast(`✅ 已导入 ${r.count} 个 UDT`)
        load()
        if (pendingImport) {
          const retry = await importDBEditorDB(pendingImport.content, pendingImport.dbNum)
          if (retry.success) {
            const saveR = await saveDBEditor(retry.dbNumber, retry.dbName, retry.fields)
            if (saveR.success) {
              showToast(`✅ DB${pendingImport.dbNum} 导入成功（${retry.variableCount} 字段）`)
              setPendingImport(null)
              load()
            }
          } else if (retry.missingUdt) {
            showToast(`❌ 还缺 UDT: ${retry.missingUdt.join(', ')}`)
          } else showToast(`❌ ${retry.error}`)
        }
      } else showToast(`❌ ${r.error}`)
    } catch { showToast('❌ UDT 导入失败') }
    e.target.value = ''
  }

  // ── 删除 UDT ──
  const handleDeleteUdt = async (name: string) => {
    try {
      await (await fetch('/api/vplc/imported-udts/' + encodeURIComponent(name), { method: 'DELETE' })).json()
      showToast(`✅ 已删除 UDT: ${name}`)
      load()
    } catch { showToast('❌ 删除 UDT 失败') }
  }

  // ── 编辑状态 ──
  const startEditing = (def: DBEditorDef) => {
    let fields: DBEditorField[]
    if (def.fields.length > 0 && def.fields[0].offset !== undefined) {
      fields = def.fields.map(f => ({ name: f.name, type: f.type, startValue: f.startValue, comment: f.comment }))
    } else {
      fields = [...def.fields]
    }
    setEditingKey(def.key)
    setEditingFields(fields)
    setEditingName(def.dbName)
    setEditingNum(def.dbNumber)
    setDirty(false)
    setFocusIdx(null)
    setLiveValues({})
    setShowBatch(false)
    startLivePoll(def.key)
  }

  const handleBackToList = () => {
    if (dirty && !confirm('有未保存的修改，确定返回吗？')) return
    stopLivePoll()
    setEditingKey(null)
    setEditingFields([])
    setDirty(false)
    setShowBatch(false)
  }

  const saveEditor = async () => {
    if (!editingKey) return
    if (editingFields.length === 0) { localToast('❌ 至少需要一个字段'); return }
    const r = await saveDBEditor(editingNum, editingName, editingFields)
    if (r.success) {
      localToast('✅ 已保存')
      setDirty(false)
      setEditingKey(r.key)
      load()
    } else localToast('❌ ' + (r.error || '保存失败'))
  }

  const addField = () => {
    setEditingFields(prev => [...prev, { name: `var_${prev.length + 1}`, type: 'Bool', startValue: '', comment: '' }])
    setDirty(true)
    setFocusIdx(editingFields.length)
  }

  const insertField = () => {
    const insertAt = focusIdx !== null ? focusIdx : editingFields.length
    const newFields = [...editingFields]
    newFields.splice(insertAt, 0, { name: `var_${Date.now() % 1000}`, type: 'Bool', startValue: '', comment: '' })
    setEditingFields(newFields)
    setDirty(true)
    setFocusIdx(insertAt)
  }

  const copyField = () => {
    if (focusIdx === null || !editingFields[focusIdx]) return
    const src = editingFields[focusIdx]
    setEditingFields(prev => [...prev, { ...src, name: src.name + '_copy' }])
    setDirty(true)
  }

  const removeField = (idx: number) => {
    setEditingFields(prev => prev.filter((_, i) => i !== idx))
    setDirty(true)
    if (focusIdx !== null && focusIdx >= idx) setFocusIdx(Math.max(0, focusIdx - 1))
  }

  const moveField = (idx: number, direction: -1 | 1) => {
    const newIdx = idx + direction
    if (newIdx < 0 || newIdx >= editingFields.length) return
    const newFields = [...editingFields]
    const tmp = newFields[idx]
    newFields[idx] = newFields[newIdx]
    newFields[newIdx] = tmp
    setEditingFields(newFields)
    setDirty(true)
    setFocusIdx(newIdx)
  }

  const updateField = (idx: number, patch: Partial<DBEditorField>) => {
    setEditingFields(prev => prev.map((f, i) => i === idx ? { ...f, ...patch } : f))
    setDirty(true)
  }

  // 实时值写入（监视合一）
  const handleWriteLive = async (key: string, fieldName: string, value: number | boolean) => {
    await writeDBEditorField(key, fieldName, value)
    setLiveValues(prev => ({ ...prev, [fieldName]: value }))
  }

  const handleToggleBitLive = async (key: string, fieldName: string, currentVal: any) => {
    const newVal = currentVal ? false : true
    await writeDBEditorField(key, fieldName, newVal)
    setLiveValues(prev => ({ ...prev, [fieldName]: newVal }))
  }

  const handleEditLiveValue = (fieldName: string, prevVal: any) => {
    const input = prompt(`为 ${fieldName} 输入新值：`, String(prevVal ?? 0))
    if (input === null) return
    const num = parseFloat(input)
    if (isNaN(num)) { showToast('❌ 无效数值'); return }
    handleWriteLive(editingKey!, fieldName, num)
  }

  // 批量添加
  const handleBatchAdd = () => {
    if (!batchText.trim()) { localToast('❌ 请粘贴变量定义'); return }
    const lines = batchText.trim().split('\n')
    const newFields: DBEditorField[] = []
    for (const raw of lines) {
      const trimmed = raw.trim()
      if (!trimmed || trimmed.startsWith('//') || trimmed.startsWith('#')) continue
      const parts = trimmed.split(/[,，\t]/).map(s => s.trim()).filter(Boolean)
      if (parts.length < 2) continue
      const name = parts[0].replace(/^["']|["']$/g, '')
      let type = parts[1]
      type = type.charAt(0).toUpperCase() + type.slice(1).toLowerCase()
      const matchedType = DB_EDITOR_TYPES.find(t => t.toLowerCase() === type.toLowerCase())
      if (!matchedType) continue
      newFields.push({
        name,
        type: matchedType,
        startValue: parts[2] || '',
        comment: parts[3] || '',
      })
    }
    if (newFields.length === 0) { localToast('❌ 没有有效的变量行'); return }
    setEditingFields(prev => [...prev, ...newFields])
    setDirty(true)
    setShowBatch(false)
    setBatchText('')
    localToast(`✅ 已添加 ${newFields.length} 个变量`)
  }

  const handleDeleteEditor = async (key: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (!confirm(`确定删除此 DB 块定义？`)) return
    await deleteDBEditor(key)
    showToast('✅ 已删除')
    load()
  }

  const handleCreateNew = async () => {
    const num = parseInt(newDbNum)
    if (isNaN(num) || num < 1) { localToast('❌ 请输入有效的 DB 号'); return }
    const name = newDbName.trim() || `DB${num}`
    const r = await saveDBEditor(num, name, [])
    if (r.success) {
      localToast(`✅ 已创建 ${name}`)
      setShowNewForm(false)
      setNewDbNum('')
      setNewDbName('')
      load()
    } else localToast('❌ ' + (r.error || '创建失败'))
  }

  const handleExportDb = async (key: string, e: React.MouseEvent) => {
    e.stopPropagation()
    const r = await exportDBEditorDB(key)
    if (!r.success) { showToast('❌ 导出失败'); return }
    const blob = new Blob([r.content], { type: 'text/plain;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${r.dbName}.db`
    a.click()
    URL.revokeObjectURL(url)
    showToast(`✅ 已导出 ${r.dbName}.db`)
  }

  const handleExportAll = async () => {
    for (const def of editors) {
      const r = await exportDBEditorDB(def.key)
      if (!r.success) continue
      const blob = new Blob([r.content], { type: 'text/plain;charset=utf-8' })
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `${r.dbName}.db`
      a.click()
      URL.revokeObjectURL(url)
    }
    showToast(`✅ 已导出全部 ${editors.length} 个 DB`)
  }

  const handleCopyDb = async (def: DBEditorDef, e: React.MouseEvent) => {
    e.stopPropagation()
    const newNum = prompt(`复制 ${def.dbName} 为新的 DB 块，请输入新 DB 号：`)
    if (!newNum) return
    const num = parseInt(newNum)
    if (isNaN(num) || num < 1) { showToast('❌ 无效 DB 号'); return }
    const r = await saveDBEditor(num, def.dbName + '_copy', def.fields)
    if (r.success) { showToast('✅ 已复制'); load() }
    else showToast('❌ ' + (r.error || '复制失败'))
  }

  if (loading) return <div className="empty">加载中...</div>

  // ── 正在编辑一个 DB ──
  if (editingKey) {
    let byteOff = 0, nextBit = 0
    const computedFields = editingFields.map(f => {
      const rawType = f.type.toLowerCase()
      let offset = 0, bit: number | undefined
      if (rawType === 'bool') {
        if (nextBit >= 8) { byteOff++; nextBit = 0 }
        offset = byteOff; bit = nextBit; nextBit++
      } else {
        if (nextBit > 0) { byteOff++; nextBit = 0 }
        if (byteOff % 2 !== 0) byteOff++
        offset = byteOff
        const size = ['byte', 'sint', 'usint', 'char'].includes(rawType) ? 1 :
                     ['word', 'int', 'uint', 'date', 'wchar'].includes(rawType) ? 2 :
                     rawType === 'bool' ? 1 : 4
        byteOff += size
      }
      return { ...f, offset, bit }
    })
    const totalSize = byteOff % 2 !== 0 ? byteOff + 1 : byteOff || 1

    return (
      <div>
        <div className={'toast' + (toastMsg ? ' show' : '')}>{toastMsg}</div>
        <div className="ed-header">
          <button className="btn btn-sm" onClick={handleBackToList}>← 返回列表</button>
          <span className="ed-title">{editingName} (DB{editingNum})</span>
          <span className="ed-info">{editingFields.length} 个变量 · {totalSize} 字节</span>
          <div style={{ marginLeft: 'auto', display: 'flex', gap: 6, alignItems: 'center' }}>
            <input className="addr-field" value={editingName} onChange={e => { setEditingName(e.target.value); setDirty(true) }} placeholder="DB 名称" style={{ width: 120 }} />
            <input className="addr-field" type="number" value={editingNum} onChange={e => { setEditingNum(parseInt(e.target.value) || editingNum); setDirty(true) }} style={{ width: 60 }} />
            <button className="btn btn-primary btn-sm" onClick={saveEditor}>💾 保存</button>
          </div>
        </div>

        <div className="ed-toolbar">
          <button className="btn btn-primary btn-sm" onClick={addField}>+ 添加变量</button>
          <button className="btn btn-sm" onClick={insertField}>插入行</button>
          <button className="btn btn-sm" onClick={copyField}>复制行</button>
          <button className="btn btn-sm" onClick={() => setShowBatch(true)}>批量添加</button>
          {focusIdx !== null && editingFields[focusIdx] && (
            <span style={{ color: '#378ADD', fontSize: 11, marginLeft: 8 }}>➔ {editingFields[focusIdx].name}</span>
          )}
        </div>

        <div className="ed-table-wrap">
          <table className="ed-table">
            <thead>
              <tr>
                <th style={{ width: 24 }}>#</th>
                <th style={{ width: 44 }}>排序</th>
                <th style={{ width: 140 }}>名称</th>
                <th style={{ width: 90 }}>数据类型</th>
                <th style={{ width: 40 }}>偏移量</th>
                <th style={{ width: 50 }}>位</th>
                <th style={{ width: 60 }}>起始值</th>
                <th style={{ width: 120 }}>当前值</th>
                <th style={{ width: 110 }}>操作</th>
                <th style={{ flex: 1 }}>注释</th>
                <th style={{ width: 40 }}></th>
              </tr>
            </thead>
            <tbody>
              {computedFields.map((f, idx) => (
                <tr key={idx} className={focusIdx === idx ? 'ed-row-active' : ''}>
                  <td className="ed-cell-idx">{idx + 1}</td>
                  <td style={{ display: 'flex', gap: 2 }}>
                    <button className="ed-move-btn" onClick={() => moveField(idx, -1)} disabled={idx === 0} title="上移">▲</button>
                    <button className="ed-move-btn" onClick={() => moveField(idx, 1)} disabled={idx === editingFields.length - 1} title="下移">▼</button>
                  </td>
                  <td>
                    <input className="ed-input" value={f.name} onChange={e => updateField(idx, { name: e.target.value })}
                      onFocus={() => setFocusIdx(idx)} placeholder="变量名" />
                  </td>
                  <td>
                    <select className="ed-select" value={f.type} onChange={e => updateField(idx, { type: e.target.value })}>
                      {DB_EDITOR_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
                    </select>
                  </td>
                  <td className="ed-cell-off">{(f.offset ?? 0).toString(16).toUpperCase()}H</td>
                  <td className="ed-cell-bit">{f.bit !== undefined ? String(f.bit) : '-'}</td>
                  <td>
                    <input className="ed-input ed-input-val" value={f.startValue ?? ''} onChange={e => updateField(idx, { startValue: e.target.value })} placeholder="-" />
                  </td>
                  <td>
                    {f.type === 'Bool' ? (
                      <span className="db-import__cell-val" style={{ cursor: 'pointer', color: editingKey && liveValues[f.name] ? '#1D9E75' : '#7A7872' }}
                        onClick={() => editingKey && handleToggleBitLive(editingKey, f.name, liveValues[f.name])}>
                        {liveValues[f.name] !== undefined ? (liveValues[f.name] ? '1' : '0') : '--'}
                      </span>
                    ) : (
                      <span className="db-import__cell-val db-import__cell-edit" onClick={() => editingKey && handleEditLiveValue(f.name, liveValues[f.name])}>
                        {liveValues[f.name] !== undefined ? String(liveValues[f.name]) : '--'}
                      </span>
                    )}
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                      <button className="ed-op-btn" onClick={async () => { if (!editingKey) return; await randomizeDBEditorField(editingKey, f.name); stopLivePoll(); startLivePoll(editingKey); }} title="随机">🎲</button>
                      {f.type === 'Bool' && (
                        <button className="ed-op-btn" onClick={async () => { if (!editingKey) return; await handleToggleBitLive(editingKey, f.name, liveValues[f.name]); }} title="取反">↺</button>
                      )}
                    </div>
                  </td>
                  <td>
                    <input className="ed-input" value={f.comment ?? ''} onChange={e => updateField(idx, { comment: e.target.value })} placeholder="注释..." />
                  </td>
                  <td>
                    <button className="ed-del-btn" onClick={() => removeField(idx)} title="删除">✕</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="ed-footer">
          <span className="ed-footer-info">总大小: <strong>{totalSize}</strong> 字节（非优化 DB, 2 字节对齐）</span>
          <span className="ed-footer-info">地址范围: 0x0 ~ 0x{(totalSize - 1).toString(16).toUpperCase()}</span>
        </div>

        {/* 批量添加弹窗 */}
        {showBatch && (
          <div className="modal-overlay" onMouseDown={e => { if (e.target === e.currentTarget) setShowBatch(false) }}>
            <div className="modal-content" onClick={e => e.stopPropagation()}>
              <h3 className="modal-title">批量添加变量</h3>
              <p style={{ color: '#7A7872', fontSize: 12, marginBottom: 8 }}>每行一个变量，格式：<code>名称,类型</code> 或 <code>名称,类型,起始值,注释</code></p>
              <textarea className="batch-textarea" value={batchText} onChange={e => setBatchText(e.target.value)}
                placeholder={'Enable,Bool\nSpeedSP,Real,0.0,速度设定\nCounter,Int\nTemp,Byte,,温度传感器\nAlarm,Bool,,报警'}
                rows={8} spellCheck={false} />
              <div className="modal-actions" style={{ gap: 6 }}>
                <span style={{ color: '#5F5E5A', fontSize: 11 }}>支持分隔符: 逗号 / Tab</span>
                <button className="btn btn-sm" onClick={() => { setShowBatch(false); setBatchText('') }}>取消</button>
                <button className="btn btn-primary btn-sm" onClick={handleBatchAdd}>✅ 添加</button>
              </div>
            </div>
          </div>
        )}
      </div>
    )
  }

  // ── DB 列表视图 ──
  return (
    <div>
      <div className={'toast' + (toastMsg ? ' show' : '')}>{toastMsg}</div>

      {/* 导入工具栏 */}
      <div className="ed-import-bar">
        <button className="btn btn-primary btn-sm" onClick={() => dbInputRef.current?.click()}>📥 导入 .db 文件</button>
        <input ref={dbInputRef} type="file" accept=".db" style={{ display: 'none' }} onChange={handleImportDbFile} />
        <button className="btn btn-sm" onClick={() => udtInputRef.current?.click()}>📥 导入 .udt 文件</button>
        <input ref={udtInputRef} type="file" accept=".db,.udt" style={{ display: 'none' }} onChange={handleImportUdtFile} />
        {udtNames.length > 0 && (
          <div className="ed-udt-tags">
            <span style={{ color: '#7A7872', fontSize: 11, marginRight: 4 }}>UDT:</span>
            {udtNames.map(name => (
              <span key={name} className="udt-tag" style={{ fontSize: 11, padding: '2px 8px' }}>
                {name}
                <button className="udt-tag__del" onClick={() => handleDeleteUdt(name)}>✕</button>
              </span>
            ))}
          </div>
        )}
      </div>

      {/* 新建/列表 */}
      <div className="ed-list-header">
        <h2 className="section-title" style={{ margin: 0, border: 'none', padding: 0 }}>📝 DB 块编辑器</h2>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          {editors.length > 0 && <button className="btn btn-sm" onClick={handleExportAll}>📤 导出全部</button>}
          <button className="btn btn-primary btn-sm" onClick={() => setShowNewForm(!showNewForm)}>
            {showNewForm ? '取消' : '+ 新建 DB'}
          </button>
        </div>
      </div>

      {showNewForm && (
        <div className="ed-new-form">
          <span className="form-label">DB 号</span>
          <input className="addr-field" type="number" value={newDbNum} onChange={e => setNewDbNum(e.target.value)} placeholder="1" style={{ width: 60 }} />
          <span className="form-label">名称</span>
          <input className="addr-field" value={newDbName} onChange={e => setNewDbName(e.target.value)} placeholder={`DB${newDbNum || ''}`} style={{ width: 120 }} />
          <button className="btn btn-primary btn-sm" onClick={handleCreateNew}>创建</button>
        </div>
      )}

      {editors.length === 0 ? (
        <div className="empty">暂无 DB 定义 — 点击「📥 导入 .db 文件」导入博图导出文件，或点「+ 新建 DB」手动创建</div>
      ) : (
        <div className="ed-list">
          {editors.map(def => (
            <div key={def.key} className="ed-list-item" onClick={() => startEditing(def)}>
              <div className="ed-list-main">
                <span className="ed-list-name">{def.dbName}</span>
                <span className="ed-list-num">DB{def.dbNumber}</span>
                <span className="ed-list-count">{def.fields.length} 个变量</span>
                {def.totalSize && <span className="ed-list-size">{def.totalSize} 字节</span>}
              </div>
              <div className="ed-list-actions">
                <button className="ed-list-export-btn" onClick={e => handleCopyDb(def, e)} title="复制 DB">📋</button>
                <button className="ed-list-export-btn" onClick={e => handleExportDb(def.key, e)} title="导出 .db">📤</button>
                <span className="ed-list-edit">编辑 →</span>
                <button className="ed-del-btn" onClick={e => handleDeleteEditor(def.key, e)} title="删除此 DB">✕</button>
              </div>
            </div>
          ))}
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

const SCRIPT_HELP = `# vPLC 脚本参考手册

## 概述

脚本在 PLC 的 OB（组织块）周期中执行，使用 JavaScript 语法。
每个脚本可以关联到 OB1（自由循环）、OB35（500ms 定时）或 OB100（启动时）。
超时限制 100ms，超时自动中断。

---

## 区域说明

| 区域 | 写法 | 说明 |
|------|------|------|
| 输入点 | I | 物理输入，外设输入区 |
| 输出点 | Q | 物理输出，外设输出区 |
| 存储区 | M | 中间变量区 |
| 数据块 | DB | 数据块，需指定块号 |

---

## API 函数速查

### 字节读写
\`\`\`js
readByte(area, dbNumber, offset)  // 读取一个字节 (0-255)
writeByte(area, dbNumber, offset, value)  // 写入一个字节 (0-255)
\`\`\`

### 位读写
\`\`\`js
readBit(area, dbNumber, offset, bit)   // 读取某一位 (true/false)
writeBit(area, dbNumber, offset, bit, value)  // 写入某一位
\`\`\`

### 16 位整数 (INT)
\`\`\`js
readInt(area, dbNumber, offset)   // 读取 INT (-32768 ~ 32767)
writeInt(area, dbNumber, offset, value)
\`\`\`

### 32 位浮点数 (REAL)
\`\`\`js
readReal(area, dbNumber, offset)   // 读取 REAL，仅支持 DB 区域
writeReal(area, dbNumber, offset, value)
\`\`\`

### 工具
\`\`\`js
log(...args)    // 打印日志到控制台
now()           // 当前时间戳 (ms)
tick()          // 当前仿真节拍 (ms)
\`\`\`

---

## 常用模式示例

### 1. 条件触发 — 位上升沿
\`\`\`js
if (readBit('I', 0, 0, 0)) {
  writeByte('Q', 0, 0, 1)
}
\`\`\`

### 2. 值比较触发 — REAL 大于阈值
\`\`\`js
if (readReal('DB', 7, 38) > 30.0) {
  writeBit('Q', 0, 0, 0, true)
}
\`\`\`

### 3. 值比较触发 — INT 等于某值
\`\`\`js
if (readInt('DB', 6, 0) === 100) {
  writeByte('Q', 0, 1, 0xFF)
}
\`\`\`

### 4. 值范围触发
\`\`\`js
const temp = readReal('DB', 7, 38)
if (temp >= 20.0 && temp <= 30.0) {
  writeBit('Q', 0, 2, 0, true)  // 正常指示
} else {
  writeBit('Q', 0, 2, 0, false) // 报警
}
\`\`\`

### 5. 位与运算 — 多条件与
\`\`\`js
const start = readBit('I', 0, 0, 0)
const ok = readBit('I', 0, 0, 1)
if (start && ok) {
  writeBit('Q', 0, 0, 0, true)  // 启动输出
}
\`\`\`

### 6. 位或运算 — 多条件或
\`\`\`js
const alarm1 = readBit('DB', 7, 0, 0)
const alarm2 = readBit('DB', 7, 0, 1)
if (alarm1 || alarm2) {
  writeBit('Q', 0, 7, 0, true)   // 总报警
}
\`\`\`

### 7. 值累加 — 脉冲计数器
\`\`\`js
if (readBit('I', 0, 0, 0)) {
  var count = readInt('DB', 6, 0)
  writeInt('DB', 6, 0, count + 1)
  log('count:', count + 1)
}
\`\`\`

### 8. 值累积 — 积分运算
\`\`\`js
var pos = readReal('DB', 6, 38)
writeReal('DB', 6, 38, pos + 0.1)
log('position:', pos + 0.1)
\`\`\`

### 9. 互锁 — 两个输出互斥
\`\`\`js
if (readBit('I', 0, 0, 0)) {
  writeBit('Q', 0, 0, 0, true)   // 正转
  writeBit('Q', 0, 0, 1, false)  // 反转关
} else if (readBit('I', 0, 0, 1)) {
  writeBit('Q', 0, 0, 0, false)  // 正转关
  writeBit('Q', 0, 0, 1, true)   // 反转
}
\`\`\`

### 10. 电机启动/停止
\`\`\`js
if (readBit('I', 0, 0, 0)) {      // 启动按钮
  writeBit('Q', 0, 0, 0, true)     // 电机运行
}
if (readBit('I', 0, 0, 1)) {      // 停止按钮
  writeBit('Q', 0, 0, 0, false)    // 电机停止
}
\`\`\`

### 11. 延时翻转 — 闪烁灯
\`\`\`js
var tick = now()
var blink = Math.floor(tick / 1000) % 2 === 0
writeBit('Q', 0, 0, 7, blink)
\`\`\`

### 12. 数值映射 — 模拟量缩放
\`\`\`js
var raw = readReal('DB', 7, 38)          // 0-50°C 温度
var scaled = (raw / 50.0) * 27648        // 映射到 0-27648
writeReal('DB', 6, 46, scaled)
\`\`\`

### 13. 自保持电路 (Self-holding)
\`\`\`js
var start = readBit('I', 0, 0, 0)
var stop = readBit('I', 0, 0, 1)
var running = readBit('Q', 0, 0, 0)
if (start || running) {
  if (!stop) writeBit('Q', 0, 0, 0, true)
  else writeBit('Q', 0, 0, 0, false)
}
\`\`\`

---

## 数据类型对应

| PLC 类型 | API | 字节数 | 范围 |
|----------|-----|--------|------|
| BOOL | readBit/writeBit | 1 bit | true/false |
| BYTE | readByte/writeByte | 1 | 0~255 |
| INT | readInt/writeInt | 2 | -32768~32767 |
| WORD | readInt | 2 | 0~65535（用 readInt 后自行 & 0xFFFF） |
| DINT | readByte x4 | 4 | 手动拼 DataView |
| REAL | readReal/writeReal | 4 | ±1.18e-38 ~ ±3.4e38 |
| DWORD | readByte x4 | 4 | 手动拼 DataView |

---

## 注意事项

1. **超时限制 100ms**，死循环会被自动终止
2. OB1 每个仿真周期（~500ms）执行一次
3. OB35 每 500ms 执行一次
4. OB100 仅在启动时执行一次
5. 脚本中可以使用 \`var\` / \`let\` / \`const\`，但不支持 import/export
6. 多个脚本按配置顺序执行，互不干扰
7. 建议在脚本开头加注释说明用途
8. 写入 DB 区域前确保 DB 块已通过 DB Editor 分配好大小
`

function ScriptsTab({ showToast }: { showToast: (msg: string) => void }) {
  const [scripts, setScripts] = useState<ScriptConfig[]>([])
  const [loading, setLoading] = useState(true)
  const [editingIdx, setEditingIdx] = useState<number | null>(null)
  const [editSource, setEditSource] = useState('')
  const [dirty, setDirty] = useState(false)
  const [showHelp, setShowHelp] = useState(false)

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
        <button className="btn btn-sm" onClick={() => setShowHelp(true)}>📖 脚本手册</button>
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

      {/* 脚本手册弹窗 */}
      {showHelp && (
        <div className="modal-overlay" onMouseDown={e => { if (e.target === e.currentTarget) setShowHelp(false) }}>
          <div className="modal-content modal-content--wide" onClick={e => e.stopPropagation()}>
            <h3 className="modal-title">📖 vPLC 脚本参考手册</h3>
            <div className="modal-form" style={{ maxHeight: '60vh', overflowY: 'auto' }}>
              <MarkdownRenderer text={SCRIPT_HELP} />
            </div>
            <div className="modal-actions"><button className="btn btn-primary" onClick={() => setShowHelp(false)}>关闭</button></div>
          </div>
        </div>
      )}
    </div>
  )
}

/** 轻量 Markdown 渲染器（支持标题/代码块/行内代码/粗体/表格/列表） */
function MarkdownRenderer({ text }: { text: string }) {
  const lines = text.split('\n')
  const elements: React.ReactNode[] = []
  let inCodeBlock = false
  let codeLines: string[] = []
  let codeLang = ''
  let inTable = false
  let tableHeaders: string[] = []
  let tableRows: string[][] = []
  const tableAligns: string[] = []
  // 代码块缩进去除
  let codeBlockStartIndent = 0

  function flushCodeBlock() {
    if (codeLines.length === 0) return
    // 去掉公共缩进
    const indent = codeLines.reduce((acc, line) => {
      if (!line.trim()) return acc
      const m = line.match(/^(\s*)/)
      return Math.min(acc, m ? m[1].length : 0)
    }, codeBlockStartIndent)
    const unindented = codeLines.map(l => l.slice(indent))
    const highlighted = highlightJS(unindented.join('\n'))
    elements.push(<pre key={'code-'+elements.length} className="md-code-block"><code dangerouslySetInnerHTML={{__html: highlighted}} /></pre>)
    codeLines = []; codeBlockStartIndent = 0
  }

  function flushTable() {
    if (tableHeaders.length === 0) return
    elements.push(
      <table key={'tbl-'+elements.length} className="md-table">
        <thead><tr>{tableHeaders.map((h, i) => <th key={i} style={{textAlign: tableAligns[i] || 'left'}}>{renderInline(h)}</th>)}</tr></thead>
        <tbody>{tableRows.map((row, ri) => <tr key={ri}>{row.map((c, ci) => <td key={ci} style={{textAlign: (tableAligns[ci] || 'left')}}>{renderInline(c)}</td>)}</tr>)}</tbody>
      </table>
    )
    tableHeaders = []; tableRows = []; inTable = false
  }

  function renderInline(s: string): React.ReactNode {
    const parts = s.split(/(`[^`]+`)/g)
    return parts.map((part, i) => {
      if (part.startsWith('`') && part.endsWith('`')) {
        return <code key={i} className="md-inline-code">{part.slice(1, -1)}</code>
      }
      const boldParts = part.split(/(\*\*[^*]+\*\*)/g)
      return boldParts.map((bp, j) => {
        if (bp.startsWith('**') && bp.endsWith('**')) {
          return <strong key={`${i}-${j}`}>{bp.slice(2, -2)}</strong>
        }
        return bp
      })
    })
  }

  for (let i = 0; i < lines.length; i++) {
    const raw = lines[i]
    if (raw.trimStart().startsWith('```')) {
      if (inCodeBlock) { flushCodeBlock(); inCodeBlock = false; codeLang = '' }
      else { flushTable(); inCodeBlock = true; codeLang = raw.trim().slice(3).trim(); codeBlockStartIndent = raw.match(/^(\s*)/)?.[1].length ?? 0 }
      continue
    }
    if (inCodeBlock) { codeLines.push(raw); continue }

    const trimmed = raw.trim()
    if (!trimmed) { flushTable(); elements.push(<div key={'br-'+i} style={{height:8}} />); continue }
    if (/^---+$/.test(trimmed)) { flushTable(); elements.push(<hr key={'hr-'+i} className="md-hr" />); continue }

    // 表格
    if (trimmed.startsWith('|')) {
      const cells = trimmed.split('|').slice(1, -1).map(c => c.trim())
      if (!inTable && tableHeaders.length === 0) { inTable = true; tableHeaders = cells }
      else if (inTable && /^[\s:|:-]+$/.test(trimmed.replace(/\|/g, ''))) {
        tableAligns.length = 0
        for (const cell of trimmed.split('|').slice(1, -1)) {
          if (cell.trim().startsWith(':') && cell.trim().endsWith(':')) tableAligns.push('center')
          else if (cell.trim().endsWith(':')) tableAligns.push('right')
          else tableAligns.push('left')
        }
      } else if (inTable) { tableRows.push(cells) }
      continue
    }

    // 标题
    const hMatch = trimmed.match(/^(#{1,4})\s+(.+)/)
    if (hMatch) {
      flushTable(); const level = hMatch[1].length
      const tag = ['h1','h2','h3','h4'][level-1] as keyof JSX.IntrinsicElements
      elements.push(createElement(tag, {key:'h-'+i, className:'md-h md-h'+level}, renderInline(hMatch[2])))
      continue
    }

    // 列表
    const ulMatch = trimmed.match(/^(\s*)[*-]\s+(.+)/)
    if (ulMatch) {
      flushTable()
      elements.push(<div key={'li-'+i} className="md-li" style={{paddingLeft:Math.min(ulMatch[1].length+16,40)}}>• {renderInline(ulMatch[2])}</div>)
      continue
    }
    const olMatch = trimmed.match(/^(\s*)\d+\.\s+(.+)/)
    if (olMatch) {
      flushTable()
      elements.push(<div key={'li-'+i} className="md-li" style={{paddingLeft:Math.min(olMatch[1].length+16,40)}}>{olMatch[2]}</div>)
      continue
    }

    flushTable()
    elements.push(<div key={'p-'+i} className="md-p">{renderInline(trimmed)}</div>)
  }

  if (inCodeBlock) flushCodeBlock()
  if (inTable) flushTable()

  return <div className="md-body">{elements}</div>
}

/** 极简 JS 语法高亮（词法着色，零依赖） */
function highlightJS(code: string): string {
  // 1) 运算符（必须在 HTML 转义之前，因为要用原始 < > &）
  let h = code.replace(/(===?|!==?|<=?|>=?|&&|\|\||[+\-*/%]=?)/g, '\x01op:$1\x02')
  // 2) HTML 转义（跳过 \x01...\x02 占位符）
  h = h.replace(/&(?!amp;)/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
  h = h.replace(/\x01op:/g, '<span class="op">').replace(/\x02/g, '</span>')
  // 3) 字符串字面量（单/双/反引号）
  h = h.replace(/('(?:[^'\\]|\\.)*')/g, '<span class="str">$1</span>')
  h = h.replace(/("(?:[^"\\]|\\.)*")/g, '<span class="str">$1</span>')
  h = h.replace(/(`(?:[^`\\]|\\.)*`)/g, '<span class="str">$1</span>')
  // 4) 注释
  h = h.replace(/(\/\/.*)/g, '<span class="cm">$1</span>')
  h = h.replace(/(\/\*[\s\S]*?\*\/)/g, '<span class="cm">$1</span>')
  // 5) 数字
  h = h.replace(/\b(\d+\.?\d*)\b/g, '<span class="num">$1</span>')
  // 6) 关键字
  const kws = 'var|let|const|if|else|for|while|do|switch|case|break|continue|return|function|true|false|null|undefined|typeof|new|delete|try|catch|finally|throw|in|of|this|class|import|export|default'
  h = h.replace(new RegExp('\\b(' + kws + ')\\b', 'g'), '<span class="kw">$1</span>')
  // 7) 函数调用
  h = h.replace(/\b([a-zA-Z_$][\w$]*)\(/g, '<span class="fn">$1</span>(')
  return h
}


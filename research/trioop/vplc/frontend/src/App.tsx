import { useState, useEffect, useCallback, useRef } from 'react'
import { fetchSnapshot, writeValue, toggleBit, importDB, createDB, fetchTriggers, createTrigger, deleteTrigger } from './api'
import type { VPLCSnapshot, Trigger } from './api'
import './App.css'

type Tab = 'monitor' | 'editor' | 'import' | 'triggers'

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
        {(['monitor', 'editor', 'import', 'triggers'] as Tab[]).map(t => (
          <button key={t} className={'tab' + (tab === t ? ' active' : '')} onClick={() => setTab(t)}>
            {t === 'monitor' ? '📊 监视' : t === 'editor' ? '✏️ DB 编辑' : t === 'import' ? '📥 导入' : '⚡ 触发器'}
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

      {tab === 'editor' && <EditorTab snap={snap} onRefresh={refresh} showToast={showToast} />}
      {tab === 'import' && <ImportTab onRefresh={refresh} showToast={showToast} />}
      {tab === 'triggers' && (
        <TriggersTab triggers={triggers} setTriggers={setTriggers} snap={snap} onRefresh={refresh} showToast={showToast} />
      )}
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
  const areas = [
    { prefix: 'I', label: '🟡 输入点 (I 区)', data: snap?.PE, addrs: iAddrs, setAddrs: setIAddrs },
    { prefix: 'Q', label: '🔵 输出点 (Q 区)', data: snap?.PA, addrs: qAddrs, setAddrs: setQAddrs },
    { prefix: 'M', label: '🟣 M 区', data: snap?.MK, addrs: mAddrs, setAddrs: setMAddrs },
  ]

  return (
    <div>
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
          <h2 className="section-title">📦 已导入 DB</h2>
          {snap._imported.map(imp => {
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
        </>
      )}
    </div>
  )
}

// ── DB 编辑 Tab ──
function EditorTab({ snap, onRefresh, showToast }: { snap: VPLCSnapshot | null; onRefresh: () => void; showToast: (msg: string) => void }) {
  const [dbNum, setDbNum] = useState<number>(6)
  const dbKeys = snap ? Object.keys(snap.DB || {}).map(k => parseInt(k.replace('DB', ''))).sort((a, b) => a - b) : []

  const handleEdit = async (offset: number) => {
    const val = prompt(`DB${dbNum}[${offset}] 输入新值（浮点数）：`)
    if (val === null) return
    const num = parseFloat(val)
    if (isNaN(num)) { showToast('无效数字'); return }
    await writeValue('DB', dbNum, offset, 'real', num)
    showToast(`✅ DB${dbNum}[${offset}] = ${num}`)
    onRefresh()
  }

  const handleCreate = async () => {
    const n = prompt('新 DB 块号：')
    if (!n) return
    const num = parseInt(n)
    if (isNaN(num) || num < 1) { showToast('无效 DB 号'); return }
    const size = prompt('字节数（默认 64）：', '64')
    await createDB(num, parseInt(size || '64'))
    showToast(`✅ 创建 DB${num}`)
    onRefresh()
  }

  const raw = snap?.DB?.[`DB${dbNum}`]
  return (
    <div>
      <div className="flex">
        <span className="addr-label">选择 DB：</span>
        <select className="addr-field" value={dbNum} onChange={e => setDbNum(parseInt(e.target.value))}>
          {dbKeys.map(n => <option key={n} value={n}>DB{n}</option>)}
        </select>
        <button className="btn btn-primary btn-sm" onClick={handleCreate}>+ 新建</button>
      </div>
      {raw ? (
        <table className="db-table">
          <thead><tr><th>偏移</th><th>HEX</th><th>十进制</th><th>REAL</th><th>操作</th></tr></thead>
          <tbody>
            {Array.from({ length: Math.min(raw.length, 128) }, (_, i) => i).filter(i => i % 4 === 0).map(off => {
              const b0 = raw[off], b1 = raw[off + 1] ?? 0, b2 = raw[off + 2] ?? 0, b3 = raw[off + 3] ?? 0
              const hex = [b0, b1, b2, b3].map(b => b.toString(16).padStart(2, '0')).join(' ')
              const dec = b0 + (b1 << 8) + (b2 << 16) + (b3 << 24)
              const dv = new DataView(new Uint8Array(raw.slice(off, off + 4)).buffer)
              const real = dv.getFloat32(0, false).toFixed(4)
              return (
                <tr key={off}>
                  <td>{off}</td>
                  <td className="hex">{hex}</td>
                  <td>{dec}</td>
                  <td>{real}</td>
                  <td><button className="btn btn-sm btn-primary" onClick={() => handleEdit(off)}>编辑</button></td>
                </tr>
              )
            })}
          </tbody>
        </table>
      ) : <div className="empty">选择要查看的 DB 块</div>}
    </div>
  )
}

// ── 导入 Tab ──
function ImportTab({ onRefresh, showToast }: { onRefresh: () => void; showToast: (msg: string) => void }) {
  const handleFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const content = await file.text()
    try {
      const r = await importDB(content)
      if (r.success) showToast(`✅ 已导入 ${r.dbName}（${r.fields} 字段）`)
      else showToast(`❌ ${r.error}`)
    } catch { showToast('❌ 导入失败') }
    onRefresh()
  }
  return (
    <div>
      <label className="file-upload" onClick={() => document.getElementById('db-file')?.click()}>
        📄 点击选择 .db 文件（TIA Portal 导出）
      </label>
      <input id="db-file" type="file" accept=".db" style={{ display: 'none' }} onChange={handleFile} />
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

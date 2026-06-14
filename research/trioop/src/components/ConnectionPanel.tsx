import { useState, useEffect } from 'react'

const STORAGE_KEY = 'trioop_connection'

const CONN_TYPES = [
  { value: 'PG', label: 'PG (编程器)' },
  { value: 'OP', label: 'OP (HMI/触摸屏)' },
  { value: 'BASIC', label: 'BASIC (通用)' },
]

interface NetworkAdapter {
  name: string
  ip: string
  family: string
}

function loadSaved() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return JSON.parse(raw)
  } catch { /* ignore */ }
  return { plcIp: '192.168.0.1', adapterIp: '', connType: 'BASIC', pollInterval: 1000, ioSource: 'io', ioDb: '5', ioStart: '0', ioLen: '8' }
}

export default function ConnectionPanel() {
  const saved = loadSaved()
  const [adapters, setAdapters] = useState<NetworkAdapter[]>([])
  const [selectedAdapter, setSelectedAdapter] = useState(saved.adapterIp)
  const [plcIp, setPlcIp] = useState(saved.plcIp)
  const [connType, setConnType] = useState(saved.connType)
  const [pollInterval, setPollInterval] = useState(String(saved.pollInterval))
  const [ioSource, setIoSource] = useState(saved.ioSource)
  const [ioDb, setIoDb] = useState(saved.ioDb)
  const [ioStart, setIoStart] = useState(saved.ioStart)
  const [ioLen, setIoLen] = useState(saved.ioLen)
  const [connecting, setConnecting] = useState(false)
  const [connected, setConnected] = useState(false)
  const [statusMsg, setStatusMsg] = useState('')

  // 保存到 localStorage
  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({
      plcIp, adapterIp: selectedAdapter, connType, pollInterval: Number(pollInterval) || 1000,
      ioSource, ioDb, ioStart, ioLen,
    }))
  }, [plcIp, selectedAdapter, connType, pollInterval, ioSource, ioDb, ioStart, ioLen])

  // 加载网卡列表
  useEffect(() => {
    fetch('/api/network/adapters')
      .then(res => res.json())
      .then((list: NetworkAdapter[]) => {
        setAdapters(list)
        if (list.length > 0) setStatusMsg(`找到 ${list.length} 个网卡`)
      })
      .catch(() => setStatusMsg('无法获取网卡列表'))
  }, [])

  // 轮询连接状态
  useEffect(() => {
    const timer = setInterval(async () => {
      try {
        const res = await fetch('/api/plc/status')
        const st = await res.json()
        setConnected(st.connected)
        if (st.connected) setStatusMsg(`已连接到 ${st.plcIp}`)
        else if (statusMsg.includes('已连接到')) setStatusMsg('连接已断开，等待重连...')
      } catch { /* ignore */ }
    }, 1000)
    return () => clearInterval(timer)
  }, [statusMsg])

  // 自动重连
  useEffect(() => {
    const saved = loadSaved()
    if (!saved.plcIp || saved.plcIp === '192.168.0.1') return
    const timer = setTimeout(async () => {
      try {
        const res = await fetch('/api/plc/status')
        const st = await res.json()
        if (!st.connected) {
          fetch('/api/plc/connect', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              plcIp: saved.plcIp,
              localAddress: saved.adapterIp || undefined,
              connType: saved.connType,
              pollInterval: saved.pollInterval,
              ioSource: saved.ioSource,
              ioDbConfig: { dbNumber: Number(saved.ioDb) || 5, startOffset: Number(saved.ioStart) || 0, byteCount: Number(saved.ioLen) || 8 },
            }),
          }).then(r => r.json()).then(d => {
            if (d.success) setStatusMsg(`已重连到 ${saved.plcIp}`)
          }).catch(() => {})
        }
      } catch {}
    }, 3000)
    return () => clearTimeout(timer)
  }, [])

  async function handleConnect() {
    setConnecting(true)
    setStatusMsg('正在连接...')
    try {
      const res = await fetch('/api/plc/connect', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          plcIp: plcIp.trim(),
          localAddress: selectedAdapter || undefined,
          connType,
          pollInterval: Number(pollInterval) || 1000,
          ioSource,
          ioDbConfig: { dbNumber: Number(ioDb) || 5, startOffset: Number(ioStart) || 0, byteCount: Number(ioLen) || 8 },
        }),
      })
      const data = await res.json()
      if (data.success) {
        setConnected(true)
        setStatusMsg(`已连接到 ${plcIp}`)
      } else {
        setStatusMsg(`连接失败: ${data.error}`)
      }
    } catch (err) {
      setStatusMsg(`连接失败: ${(err as Error).message}`)
    } finally {
      setConnecting(false)
    }
  }

  async function handleDisconnect() {
    try {
      await fetch('/api/plc/disconnect', { method: 'POST' })
      setConnected(false)
      setStatusMsg('已断开')
    } catch { /* ignore */ }
  }

  return (
    <aside className="sidebar">
      <h2 className="sidebar__title">🔌 PLC 连接</h2>

      <div className="sidebar__group">
        <label className="sidebar__label">本机网卡</label>
        <select className="sidebar__select" value={selectedAdapter} onChange={e => setSelectedAdapter(e.target.value)}>
          <option value="">自动选择</option>
          {adapters.map(a => (
            <option key={a.ip} value={a.ip}>{a.name} ({a.ip})</option>
          ))}
        </select>
      </div>

      <div className="sidebar__group">
        <label className="sidebar__label">PLC IP 地址</label>
        <input className="sidebar__input" type="text" value={plcIp} onChange={e => setPlcIp(e.target.value)} placeholder="192.168.0.1" />
      </div>

      <div className="sidebar__group">
        <label className="sidebar__label">连接通道</label>
        <select className="sidebar__select" value={connType} onChange={e => setConnType(e.target.value)}>
          {CONN_TYPES.map(t => (
            <option key={t.value} value={t.value}>{t.label}</option>
          ))}
        </select>
      </div>

      <div className="sidebar__group">
        <label className="sidebar__label">轮询间隔 (ms)</label>
        <input className="sidebar__input" type="number" value={pollInterval} onChange={e => { const v = Math.max(50, Number(e.target.value) || 50); setPollInterval(String(v)) }} min={50} max={10000} step={50} />
      </div>

      <div className="sidebar__group">
        <label className="sidebar__label">I/O 数据源</label>
        <select className="sidebar__select" value={ioSource} onChange={e => setIoSource(e.target.value)}>
          <option value="io">直读 I/Q 区</option>
          <option value="db">从 DB 读取</option>
        </select>
      </div>

      {ioSource === 'db' && (
        <div className="sidebar__group" style={{ display: 'flex', gap: 6, flexDirection: 'row', flexWrap: 'wrap' }}>
          <input className="sidebar__input" style={{ width: 52 }} value={ioDb} onChange={e => setIoDb(e.target.value)} placeholder="DB" />
          <input className="sidebar__input" style={{ width: 52 }} value={ioStart} onChange={e => setIoStart(e.target.value)} placeholder="起始" />
          <input className="sidebar__input" style={{ width: 52 }} value={ioLen} onChange={e => setIoLen(e.target.value)} placeholder="长度" />
          <span style={{ fontSize: 11, color: '#666', lineHeight: '32px' }}>DB{ioDb} @{ioStart} · {ioLen}B</span>
        </div>
      )}

      <div className="sidebar__actions">
        {connected ? (
          <button className="btn btn--danger sidebar__btn" onClick={handleDisconnect}>断开连接</button>
        ) : (
          <button className="btn btn--primary sidebar__btn" onClick={handleConnect} disabled={connecting || !plcIp.trim()}>
            {connecting ? '连接中...' : '连接 PLC'}
          </button>
        )}
      </div>

      <div className={`sidebar__status ${connected ? 'sidebar__status--ok' : ''}`}>
        <span className={`status-dot ${connected ? 'connected' : ''}`} />
        <span>{statusMsg || (connected ? '已连接' : '未连接')}</span>
      </div>
    </aside>
  )
}

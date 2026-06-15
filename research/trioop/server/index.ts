/**
 * Trioop PLC Monitor — 服务端入口
 *
 * 开发：pnpm dev       (Vite HMR :5173 + API :3001)
 * 生产：pnpm start     (Express :3000 提供 API + 前端静态文件)
 */

import express from 'express'
import path from 'path'
import { fileURLToPath } from 'url'
import config from './config.js'
import * as plc from './plc.js'

// ─── 路径 ─────────────────────────────────────────────────
const __dirname = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(__dirname, '..')
const isDev = process.env.NODE_ENV !== 'production'

const app = express()
app.use(express.json())

// ─── 判断 Vite 是否需要加载前端 ────────────────────────────
if (isDev) {
  console.log('[Server] 开发模式：API 只监听，前端由 Vite HMR 提供')
} else {
  const dist = path.resolve(root, 'dist')
  app.use(express.static(dist))
  app.get('*', (req, res, next) => {
    if (req.path.startsWith('/api')) return next()
    res.sendFile(path.join(dist, 'index.html'))
  })
}

// ─── 运行时连接参数 ───────────────────────────────────────
let runtimePlcIp: string = config.plc.ip
let runtimeLocalAddr: string | undefined
let pollingTimer: ReturnType<typeof setInterval> | null = null
let plcDataCache: Record<string, unknown> = {}
let ioDataCache: { i: Record<number, number>; q: Record<number, number> } = { i: {}, q: {} }
let runtimeConnType = 3
let runtimePollInterval = 1000
let runtimeIOSource: 'io' | 'db' = 'io'    // 'io'=直读I/Q, 'db'=从DB读
let runtimeIODbConfig = { dbNumber: 5, startOffset: 0, byteCount: 8 }

/** DB 块列表：用户在前端配置的要读取的 DB 块 */
interface DBBlockConfig {
  label: string
  dbNumber: number
  startOffset: number
  byteCount: number
}
let dbBlocks: DBBlockConfig[] = []
let dbBlockCache: Record<string, number[] | null> = {}

// ─── API: 获取本机网卡列表 ────────────────────────────────
app.get('/api/network/adapters', (_req, res) => {
  const adapters = plc.listNetworkAdapters()
  res.json(adapters)
})

// ─── 连接类型映射 ────────────────────────────────────────
const CONN_TYPE_MAP: Record<string, number> = { PG: 1, OP: 2, BASIC: 3 }

// ─── API: 连接 PLC ───────────────────────────────────────
app.post('/api/plc/connect', async (req, res) => {
  const { plcIp, localAddress, connType, pollInterval } = req.body
  if (!plcIp) return res.status(400).json({ error: '请提供 PLC IP' })

  runtimePlcIp = plcIp
  runtimeLocalAddr = localAddress || undefined
  runtimeConnType = CONN_TYPE_MAP[connType as string] ?? 3
  runtimePollInterval = Math.max(50, Math.min(10000, (pollInterval as number) || 1000))
  if (req.body.ioSource === 'io' || req.body.ioSource === 'db') runtimeIOSource = req.body.ioSource
  if (req.body.ioDbConfig) runtimeIODbConfig = { ...runtimeIODbConfig, ...req.body.ioDbConfig }

  plc.disconnect()
  if (pollingTimer) clearInterval(pollingTimer)

  try {
    await plc.connect(runtimePlcIp, config.plc.rack, config.plc.slot, runtimeLocalAddr, runtimeConnType, config.variables, dbBlocks)
    plcDataCache = {}
    pollingTimer = setInterval(poll, runtimePollInterval)
    poll()
    res.json({ success: true, message: `已连接到 ${runtimePlcIp}` })
  } catch (err) {
    res.status(502).json({ error: (err as Error).message })
  }
})

// ─── API: 断开 PLC ───────────────────────────────────────
app.post('/api/plc/disconnect', (_req, res) => {
  plc.disconnect()
  if (pollingTimer) {
    clearInterval(pollingTimer)
    pollingTimer = null
  }
  res.json({ success: true })
})

// ─── API: 获取连接状态 ────────────────────────────────────
app.get('/api/plc/status', (_req, res) => {
  res.json({ connected: plc.isConnected(), plcIp: runtimePlcIp, localAddress: runtimeLocalAddr ?? null, connType: runtimeConnType, pollInterval: runtimePollInterval, ioSource: runtimeIOSource })
})

// ─── API: 获取最新数据 ──────────────────────────────────
app.get('/api/plc/data', (_req, res) => {
  res.json({ db: plcDataCache, io: ioDataCache, dbBlocks: dbBlockCache })
})

// ─── API: 获取配置 ──────────────────────────────────────
app.get('/api/plc/config', (_req, res) => {
  res.json({
    pollInterval: config.pollInterval,
    variables: config.variables.map(v => ({
      name: v.name,
      area: v.area,
      dbNumber: v.dbNumber,
      offset: v.offset,
      type: v.type,
      bit: v.bit,
      writable: !!v.writable,
    })),
  })
})

// ─── API: DB 块配置 ────────────────────────────────────
app.get('/api/plc/db-blocks', (_req, res) => {
  res.json(dbBlocks)
})

app.post('/api/plc/db-blocks', (req, res) => {
  const { label, dbNumber, startOffset, byteCount } = req.body
  if (!label || !dbNumber) return res.status(400).json({ error: '请提供 label 和 dbNumber' })
  const idx = dbBlocks.findIndex(b => b.label === label)
  const block: DBBlockConfig = { label, dbNumber, startOffset: startOffset ?? 0, byteCount: byteCount ?? 4 }
  if (idx >= 0) {
    dbBlocks[idx] = block
    plc.removeDBBlock(label)   // 先移除旧注册
    plc.addDBBlock(label, block.dbNumber, block.startOffset, block.byteCount)
  } else {
    dbBlocks.push(block)
    if (plc.isConnected()) {
      plc.addDBBlock(label, block.dbNumber, block.startOffset, block.byteCount)
    }
  }
  res.json(dbBlocks)
})

app.delete('/api/plc/db-blocks/:label', (req, res) => {
  dbBlocks = dbBlocks.filter(b => b.label !== req.params.label)
  delete dbBlockCache[req.params.label]
  plc.removeDBBlock(req.params.label)
  res.json(dbBlocks)
})

// ─── API: 写入 PLC ──────────────────────────────────────
app.post('/api/plc/write', async (req, res) => {
  const { name, value } = req.body
  if (!name || value === undefined) {
    return res.status(400).json({ error: '请提供 name 和 value' })
  }

  const varCfg = config.variables.find(v => v.name === name)
  if (!varCfg)        return res.status(404).json({ error: `未找到变量: ${name}` })
  if (!varCfg.writable) return res.status(403).json({ error: `${name} 不可写` })

  try {
    await plc.writeVariable(varCfg, Number(value))
    plcDataCache[name] = {
      value: Number(value),
      type: varCfg.type,
      writable: true,
      dbNumber: varCfg.dbNumber,
      offset: varCfg.offset,
    }
    res.json({ success: true, name, value: Number(value) })
  } catch (err) {
    const msg = (err as Error).message
    console.error(`[PLC] 写入 ${name} 失败:`, msg)
    res.status(502).json({ error: msg })
  }
})

// ─── API: 写入 I/O 点 ──────────────────────────────────
app.post('/api/plc/write-io', async (req, res) => {
  const { area, byte: byteAddr, bit, value, currentByte } = req.body
  if (area !== 'q') return res.status(400).json({ error: '仅支持 Q 区写入' })
  if (byteAddr === undefined || bit === undefined || value === undefined) {
    return res.status(400).json({ error: '请提供 byte、bit 和 value' })
  }

  try {
    // 前端传 currentByte 则直接算新值写整个字节，避免读-改-写冲突
    if (typeof currentByte === 'number') {
      const newByte = value ? (currentByte | (1 << bit)) : (currentByte & ~(1 << bit))
      await plc.writeByte(Number(byteAddr), newByte)
    } else {
      await plc.writeIOBit(Number(byteAddr), Number(bit), !!value)
    }
    res.json({ success: true })
  } catch (err) {
    const msg = (err as Error).message
    res.status(502).json({ error: msg })
  }
})

// ─── API: SSE 实时推送 ──────────────────────────────────
import { addClient, broadcast } from './sse.js'

app.get('/api/plc/stream', (req, res) => {
  res.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    Connection: 'keep-alive',
  })
  res.write(`data: ${JSON.stringify({ db: plcDataCache, io: ioDataCache, dbBlocks: dbBlockCache })}\n\n`)
  addClient(res)
})

// ─── 轮询 ────────────────────────────────────────────────
async function poll() {
  try {
    if (!plc.isConnected()) {
      try { await plc.connect(runtimePlcIp, config.plc.rack, config.plc.slot, runtimeLocalAddr, runtimeConnType, config.variables, dbBlocks) } catch {}
      if (!plc.isConnected()) return
    }

    // 一次读取所有已注册项（nodes7 自动合并为最优 S7 请求包）
    const result = await plc.readOnce()

    plcDataCache = result.db
    ioDataCache.i = result.io.i
    ioDataCache.q = result.io.q
    dbBlockCache = result.dbBlocks

    broadcast({ db: plcDataCache, io: ioDataCache, dbBlocks: dbBlockCache })
  } catch (err) {
    console.warn(`[PLC] 轮询异常:`, (err as Error).message)
  }
}

// ─── 启动 ────────────────────────────────────────────────
const PORT = isDev ? 3001 : 3000

function start() {
  app.listen(PORT, () => {
    console.log(`\n========================================`)
    console.log(`  Trioop PLC Monitor`)
    console.log(`  环境: ${isDev ? '开发' : '生产'}`)
    console.log(`  API:  http://localhost:${PORT}/api/plc`)
    console.log(`  推流: http://localhost:${PORT}/api/plc/stream`)
    console.log(`========================================\n`)
  })
}

// 优雅退出
process.on('SIGINT', () => {
  console.log('\n正在关闭...')
  if (pollingTimer) clearInterval(pollingTimer)
  plc.disconnect()
  process.exit(0)
})

start()

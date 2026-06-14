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
import { addClient, broadcast, getClientCount } from './sse.js'

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
  // 生产模式：挂载 Vite 构建产物
  const dist = path.resolve(root, 'dist')
  app.use(express.static(dist))

  // SPA fallback：非 API 路由返回 index.html
  app.get('*', (req, res, next) => {
    if (req.path.startsWith('/api')) return next()
    res.sendFile(path.join(dist, 'index.html'))
  })
}

// ─── API: 获取最新数据 ──────────────────────────────────
app.get('/api/plc/data', (_req, res) => {
  res.json(plcDataCache)
})

// ─── API: 获取配置 ──────────────────────────────────────
app.get('/api/plc/config', (_req, res) => {
  res.json({
    pollInterval: config.pollInterval,
    variables: config.variables.map(v => ({
      name: v.name,
      type: v.type,
      writable: !!v.writable,
    })),
  })
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
    // 更新缓存
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

// ─── API: SSE 实时推送 ──────────────────────────────────
app.get('/api/plc/stream', (req, res) => {
  res.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    Connection: 'keep-alive',
  })
  // 立即推送一次当前数据
  res.write(`data: ${JSON.stringify(plcDataCache)}\n\n`)
  addClient(res)
})

// ─── 数据缓存与轮询 ─────────────────────────────────────
let plcDataCache: Record<string, unknown> = {}
let pollingTimer: ReturnType<typeof setInterval> | null = null

async function poll() {
  try {
    if (!plc.isConnected()) {
      throw new Error('未连接')
    }
    const data = await plc.readAll(config.variables)
    plcDataCache = data
    broadcast(data as any)
  } catch (err) {
    console.warn(`[PLC] 轮询异常:`, (err as Error).message)
    // 尝试重连
    try {
      await tryConnect()
    } catch {
      // 连不上就等下一轮
    }
  }
}

async function tryConnect(): Promise<boolean> {
  try {
    await plc.connect(config.plc.ip, config.plc.rack, config.plc.slot)
    console.log('[PLC] ✅ 连接成功')
    return true
  } catch (err) {
    console.warn(`[PLC] ❌ 连接失败，30 秒后重试:`, (err as Error).message)
    return false
  }
}

// ─── 启动 ────────────────────────────────────────────────
const PORT = isDev ? 3001 : 3000

async function start() {
  // 启动时尝试连接 PLC（异步，不阻塞 HTTP）
  tryConnect().then(ok => {
    if (ok) {
      // 连接成功，立即读一次
      poll()
    }
  })

  // 启动轮询
  pollingTimer = setInterval(poll, config.pollInterval)

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

start().catch(err => {
  console.error('启动失败:', err)
  process.exit(1)
})

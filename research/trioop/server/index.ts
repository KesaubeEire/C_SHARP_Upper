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
import * as opcua from './opcua.js'
import { parseDBFile, parsedVarsToNodes7Tags } from './dbParser.js'
import { trendBuffer } from './ringBuffer.js'
import { checkAlarms, getRules, setRule, deleteRule, getActiveAlarms, getAlarmHistory, acknowledgeAlarm, acknowledgeAll } from './alarmEngine.js'
import { writePoints, queryHistory, exportCSV, stopFlush } from './historyStore.js'
import { recordPoll, recordError, getDiagnostics, resetDiagnostics } from './diagnostics.js'
import { getRecipes, createRecipe, updateRecipe, deleteRecipe, getRecipe } from './recipeManager.js'
import { authenticate, validateToken, logout, getUsers, addUser, removeUser, changePassword, extractToken } from './auth.js'
import { updateTagAddressByDB } from './plc.js'
import { getMapping, setMapping, deleteMapping, setMappings } from './dbMapping.js'

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

// ─── OPC UA 相关 ──────────────────────────────────────────
/** OPC UA 订阅的数据缓存（变量名 → { value }，供 SSE 推给前端） */
let opcuaDataCache: Record<string, { value: any }> = {}

/** OPC UA 变量名 ↔ nodeId 映射表 */
interface OPCUAVarMapping {
  name: string
  nodeId: string
}
let opcuaVarMap: OPCUAVarMapping[] = []

// ─── API: 获取本机网卡列表 ────────────────────────────────
app.get('/api/network/adapters', (_req, res) => {
  const adapters = plc.listNetworkAdapters()
  res.json(adapters)
})

// ─── 连接类型映射 ────────────────────────────────────────
const CONN_TYPE_MAP: Record<string, number> = { PG: 1, OP: 2, BASIC: 3 }

// ─── API: 连接 PLC ───────────────────────────────────────
app.post('/api/plc/connect', async (req, res) => {
  const { plcIp, localAddress, connType, pollInterval, ioRanges } = req.body
  if (!plcIp) return res.status(400).json({ error: '请提供 PLC IP' })

  runtimePlcIp = plcIp
  runtimeLocalAddr = localAddress || undefined
  runtimeConnType = CONN_TYPE_MAP[connType as string] ?? 3
  runtimePollInterval = Math.max(50, Math.min(10000, (pollInterval as number) || 1000))
  runtimeMode = 's7'
  if (req.body.ioSource === 'io' || req.body.ioSource === 'db') runtimeIOSource = req.body.ioSource
  if (req.body.ioDbConfig) runtimeIODbConfig = { ...runtimeIODbConfig, ...req.body.ioDbConfig }
  if (ioRanges) runtimeIORanges = ioRanges

  plc.disconnect()
  if (pollingTimer) clearInterval(pollingTimer)

  try {
    await plc.connect(runtimePlcIp, config.plc.rack, config.plc.slot, runtimeLocalAddr, runtimeConnType, config.variables, dbBlocks, ioRanges)
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
  res.json({
    mode: runtimeMode,
    connected: runtimeMode === 's7' ? plc.isConnected() : opcua.isConnected(),
    plcIp: runtimePlcIp,
    localAddress: runtimeLocalAddr ?? null,
    connType: runtimeConnType,
    pollInterval: runtimePollInterval,
    ioSource: runtimeIOSource,
    ioRanges: runtimeIORanges ?? config.ioRanges ?? null,
  })
})

// ─── OPC UA 连接 ────────────────────────────────────────
let runtimeMode: 's7' | 'opcua' = 's7'
/** 前端传过来的 I/Q 字节范围（做实时显示用，OPC UA 模式也存着） */
let runtimeIORanges: { i?: { start: number; end: number }[]; q?: { start: number; end: number }[] } | null = null

app.post('/api/opcua/connect', async (req, res) => {
  const { plcIp, port, username, password, ioRanges } = req.body
  if (!plcIp) return res.status(400).json({ error: '请提供 PLC IP' })

  runtimeMode = 'opcua'
  runtimePlcIp = plcIp
  if (ioRanges) runtimeIORanges = ioRanges

  try {
    await opcua.connect(plcIp, port, username, password)
    res.json({ success: true, message: `OPC UA 已连接到 ${plcIp}` })
  } catch (err) {
    res.status(502).json({ error: (err as Error).message })
  }
})

app.post('/api/opcua/disconnect', async (_req, res) => {
  await opcua.unsubscribeAll()
  await opcua.disconnect()
  opcuaDataCache = {}
  broadcast({ db: {}, io: { i: {}, q: {} }, dbBlocks: {} })
  res.json({ success: true })
})

app.get('/api/opcua/browse', async (req, res) => {
  const nodeId = (req.query.nodeId as string) || 'i=85'
  try {
    const nodes = await opcua.browse(nodeId)
    res.json(nodes)
  } catch (err) {
    res.status(502).json({ error: (err as Error).message })
  }
})

app.post('/api/opcua/read', async (req, res) => {
  const { nodeIds } = req.body
  if (!nodeIds || !Array.isArray(nodeIds)) return res.status(400).json({ error: '请提供 nodeIds 数组' })
  try {
    const data = await opcua.readNodes(nodeIds)
    res.json({ data })
  } catch (err) {
    res.status(502).json({ error: (err as Error).message })
  }
})

app.post('/api/opcua/write', async (req, res) => {
  const { nodeId, value } = req.body
  if (!nodeId) return res.status(400).json({ error: '请提供 nodeId' })
  try {
    await opcua.writeNode(nodeId, value)
    res.json({ success: true })
  } catch (err) {
    res.status(502).json({ error: (err as Error).message })
  }
})

// ─── OPC UA 变量映射 ────────────────────────────────────
app.get('/api/opcua/variable-map', (_req, res) => {
  res.json(opcuaVarMap)
})

app.post('/api/opcua/variable-map', (req, res) => {
  const { name, nodeId } = req.body
  if (!name || !nodeId) return res.status(400).json({ error: '请提供 name 和 nodeId' })
  const idx = opcuaVarMap.findIndex(m => m.name === name)
  if (idx >= 0) opcuaVarMap[idx] = { name, nodeId }
  else opcuaVarMap.push({ name, nodeId })
  res.json({ success: true, map: opcuaVarMap })
})

app.delete('/api/opcua/variable-map/:name', (req, res) => {
  opcuaVarMap = opcuaVarMap.filter(m => m.name !== req.params.name)
  res.json({ success: true, map: opcuaVarMap })
})

// ─── OPC UA 订阅管理 ────────────────────────────────────
app.post('/api/opcua/subscribe', async (req, res) => {
  const { items, publishingInterval } = req.body
  const subItems = items ?? opcuaVarMap
  if (!subItems || subItems.length === 0) return res.status(400).json({ error: '没有要订阅的变量' })

  try {
    opcuaDataCache = {}
    await opcua.subscribeWithCache(subItems, publishingInterval ?? 200)
    startOpcuaBroadcast()
    res.json({ success: true, count: subItems.length, message: `已订阅 ${subItems.length} 个变量` })
  } catch (err) {
    res.status(502).json({ error: `订阅失败: ${(err as Error).message}` })
  }
})

app.post('/api/opcua/unsubscribe', async (_req, res) => {
  await opcua.unsubscribeAll()
  stopOpcuaBroadcast()
  opcuaDataCache = {}
  broadcast({ db: {}, io: { i: {}, q: {} }, dbBlocks: {} })
  res.json({ success: true })
})

// ─── OPC UA 调试：列出地址空间所有变量 ─────────────────
app.get('/api/opcua/all-variables', async (_req, res) => {
  try {
    const vars = await opcua.getAllVariables()
    res.json({ count: vars.length, variables: vars })
  } catch (err) {
    res.status(502).json({ error: (err as Error).message })
  }
})

/** OPC UA 订阅缓存 → SSE 广播（每 200ms 推一次） */
let opcuaBroadcastTimer: ReturnType<typeof setInterval> | null = null

function startOpcuaBroadcast() {
  stopOpcuaBroadcast()
  opcuaBroadcastTimer = setInterval(() => {
    const cache = opcua.getValueCache()
    const db: Record<string, { value: any }> = {}
    for (const [name, value] of Object.entries(cache)) {
      db[name] = { value, type: typeof value, writable: true, dbNumber: 0, offset: 0 }
    }
    opcuaDataCache = db
    trendBuffer.push(cache as Record<string, number | boolean>)
    writePoints(cache as Record<string, number | boolean>)
    checkAlarms(cache as Record<string, number | boolean>)
    broadcast({ db, io: { i: {}, q: {} }, dbBlocks: {} })
  }, 200)
}

function stopOpcuaBroadcast() {
  if (opcuaBroadcastTimer) {
    clearInterval(opcuaBroadcastTimer)
    opcuaBroadcastTimer = null
  }
}

// ─── API: 趋势数据查询 ──────────────────────────────────
app.get('/api/trend/:name', (req, res) => {
  const name = req.params.name
  const from = req.query.from ? Number(req.query.from) : undefined
  const to = req.query.to ? Number(req.query.to) : undefined
  const data = trendBuffer.query(name, from, to)
  res.json({ name, count: data.length, data })
})

app.get('/api/trend', (req, res) => {
  const namesParam = req.query.names as string
  const count = req.query.count ? Number(req.query.count) : 100
  const names = namesParam ? namesParam.split(',') : []
  if (names.length === 0) return res.json({ data: {} })
  const data = trendBuffer.queryLatest(names, count)
  res.json({ count: names.length, points: count, data })
})

// ─── API: 获取最新数据 ──────────────────────────────────
app.get('/api/plc/data', (_req, res) => {
  if (runtimeMode === 'opcua') {
    res.json({ db: opcuaDataCache, io: { i: {}, q: {} }, dbBlocks: {} })
  } else {
    res.json({ db: plcDataCache, io: ioDataCache, dbBlocks: dbBlockCache })
  }
})

// ─── API: 获取配置 ──────────────────────────────────────
app.get('/api/plc/config', (_req, res) => {
  res.json({
    pollInterval: config.pollInterval,
    ioRanges: config.ioRanges ?? { i: [{ start: 0, end: 1 }, { start: 8, end: 8 }], q: [{ start: 0, end: 1 }, { start: 8, end: 8 }] },
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

// ─── API: 导入 DB 文件 ─────────────────────────────────
let importedDBs: Record<string, { dbNumber: number; dbName: string; variables: import('./dbParser.js').ParsedDBVariable[] }> = {}

app.post('/api/plc/import-db', async (req, res) => {
  // 同时支持：前端直接发原始文件 (octet-stream) 或 JSON (旧格式)
  let content: string
  let dbNumber: number | undefined
  try {
    if (req.is('application/octet-stream')) {
      const chunks: Buffer[] = []
      for await (const chunk of req) chunks.push(chunk)
      content = Buffer.concat(chunks).toString('utf-8')
    } else {
      content = req.body?.content
      dbNumber = req.body?.dbNumber
    }
  } catch (err) {
    return res.status(400).json({ error: `读取文件失败: ${(err as Error).message}` })
  }
  if (!content) return res.status(400).json({ error: '请提供 DB 文件内容' })

  try {
    const parsed = parseDBFile(content, dbNumber)
    if (parsed.optimized) {
      return res.status(400).json({ error: `DB"${parsed.dbName}" 开启了优化块访问，无法通过绝对地址读取` })
    }
    if (parsed.variables.length === 0) {
      return res.status(400).json({ error: '未解析到任何变量，请确认文件格式是否正确' })
    }

    const key = `${parsed.dbNumber}_${parsed.dbName}`
    importedDBs[key] = { dbNumber: parsed.dbNumber, dbName: parsed.dbName, variables: parsed.variables }

    // 转换为 nodes7 标签并批量注册（带变量名，用于回传实时值）
    const tags = parsedVarsToNodes7Tags(parsed.variables, parsed.dbNumber)
    if (plc.isConnected()) {
      plc.addDynamicTags(tags.map(t => ({ tag: t.tag, s7addr: t.s7addr, varName: `${parsed.dbName}:${t.name}` })))
    }

    // OPC UA 模式：自动搜索匹配的 nodeId 并订阅
    let opcuaMatched: { name: string; nodeId: string }[] = []
    if (runtimeMode === 'opcua' && opcua.isConnected()) {
      const varNames = parsed.variables.map(v => v.name)
      opcuaMatched = await opcua.findVariablesByName(varNames)
      if (opcuaMatched.length > 0) {
        for (const m of opcuaMatched) {
          const idx = opcuaVarMap.findIndex(x => x.name === m.name)
          if (idx >= 0) opcuaVarMap[idx] = m
          else opcuaVarMap.push(m)
        }
        opcuaDataCache = {}
        await opcua.subscribeWithCache(opcuaMatched, 200)
        startOpcuaBroadcast()
      }
    }

    res.json({
      success: true,
      dbNumber: parsed.dbNumber,
      dbName: parsed.dbName,
      variableCount: parsed.variables.length,
      variables: parsed.variables,
      opcuaMatched: opcuaMatched.length > 0 ? opcuaMatched : undefined,
    })
  } catch (err) {
    res.status(400).json({ error: `解析失败: ${(err as Error).message}` })
  }
})

app.get('/api/plc/imported-dbs', (_req, res) => {
  res.json(Object.values(importedDBs))
})

// ─── 调试：查看动态标签状态 ─────────────────────────
app.get('/api/plc/debug-tags', (_req, res) => {
  res.json({
    importedDBs: Object.keys(importedDBs),
    tags: plc.getDebugTags(),
  })
})

app.delete('/api/plc/imported-dbs/:key', (req, res) => {
  const key = req.params.key
  const db = importedDBs[key]
  if (db) {
    const tags = parsedVarsToNodes7Tags(db.variables, db.dbNumber)
    for (const t of tags) {
      plc.removeDynamicTag(t.tag)
    }
    delete importedDBs[key]
  }
  res.json({ success: true })
})

// ─── API: 刷新导入的 DB 块（切换模式/断连后重新注册） ──
app.post('/api/plc/imported-dbs/:key/refresh', async (req, res) => {
  const key = req.params.key
  const db = importedDBs[key]
  if (!db) return res.status(404).json({ error: `未找到 DB: ${key}` })

  try {
    if (runtimeMode === 'opcua') {
      if (opcua.isConnected()) {
        const varNames = db.variables.map(v => v.name)
        const matched = await opcua.findVariablesByName(varNames)
        if (matched.length > 0) {
          for (const m of matched) {
            const idx = opcuaVarMap.findIndex(x => x.name === m.name)
            if (idx >= 0) opcuaVarMap[idx] = m
            else opcuaVarMap.push(m)
          }
          await opcua.subscribeWithCache(matched, 200)
          startOpcuaBroadcast()
        }
        res.json({ success: true, matched: matched.length })
      } else {
        res.status(400).json({ error: 'OPC UA 未连接' })
      }
    } else {
      if (plc.isConnected()) {
        const tags = parsedVarsToNodes7Tags(db.variables, db.dbNumber)
        plc.addDynamicTags(tags.map(t => ({ tag: t.tag, s7addr: t.s7addr, varName: `${db.dbName}:${t.name}` })))
        res.json({ success: true, registered: tags.length })
      } else {
        res.status(400).json({ error: 'PLC 未连接' })
      }
    }
  } catch (err) {
    res.status(502).json({ error: `刷新失败: ${(err as Error).message}` })
  }
})

// ─── API: 导入 DB 变量写入 ────────────────────────────
app.post('/api/plc/imported-db-write', async (req, res) => {
  const { dbNumber, name, value } = req.body
  if (!dbNumber || !name || value === undefined) {
    return res.status(400).json({ error: '请提供 dbNumber、name 和 value' })
  }

  const key = Object.keys(importedDBs).find(k => importedDBs[k].dbNumber === dbNumber)
  if (!key) return res.status(404).json({ error: `未找到 DB${dbNumber}` })
  const db = importedDBs[key]
  const varDef = db.variables.find(v => v.name === name)
  if (!varDef) return res.status(404).json({ error: `未找到变量 ${name}` })

  try {
    if (runtimeMode === 'opcua') {
      // OPC UA 模式：查映射表，走 OPC UA 写入
      const mapping = opcuaVarMap.find(m => m.name === name)
      if (!mapping) throw new Error(`OPC UA 映射中未找到变量 "${name}"，请先配映射`)
      await opcua.writeNode(mapping.nodeId, Number(value))
    } else if (varDef.type === 'bool') {
      await plc.modifyBit(`DB${dbNumber},B${varDef.offset}.1`, varDef.bit ?? 0, !!value)
    } else {
      const tags = parsedVarsToNodes7Tags([varDef], dbNumber)
      if (tags.length === 0) throw new Error('无法生成节点地址')
      await plc.writeRaw(tags[0].s7addr, Number(value))
    }
    res.json({ success: true })
  } catch (err) {
    res.status(502).json({ error: (err as Error).message })
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


// ─── API: 直写 PLC ────────────────────────────────────
app.post("/api/plc/write-raw", async (req, res) => {
  const { address, value, bit } = req.body
  if (!address || value === undefined) return res.status(400).json({ error: "请提供 address 和 value" })
  try {
    if (bit !== undefined) {
      await plc.modifyBit(address, bit, !!value)
    } else {
      await plc.writeRaw(address, Number(value))
    }
    res.json({ success: true })
  } catch (err) { res.status(502).json({ error: (err as Error).message }) }
})

// ─── API: 更新标签 DB 号 ────────────────────────────
app.post("/api/plc/update-tag-addr", async (req, res) => {
  const { dbName, dbNumber } = req.body
  if (!dbName || !dbNumber) return res.status(400).json({ error: "请提供 dbName 和 dbNumber" })
  try { const count = plc.updateTagAddressByDB(dbName, dbNumber); res.json({ success: true, updated: count }) }
  catch (err) { res.status(502).json({ error: (err as Error).message }) }
})

// ─── API: 报警管理 ──────────────────────────────────────
app.get('/api/alarm/rules', (_req, res) => {
  res.json(getRules())
})

app.post('/api/alarm/rules', (req, res) => {
  const { name, variableName, condition, threshold, message, enabled } = req.body
  if (!name || !variableName || !condition || threshold === undefined) {
    return res.status(400).json({ error: '请提供 name, variableName, condition, threshold' })
  }
  setRule({ name, variableName, condition, threshold, message: message || name, enabled: enabled ?? true })
  res.json({ success: true, rules: getRules() })
})

app.delete('/api/alarm/rules/:name', (req, res) => {
  deleteRule(req.params.name)
  res.json({ success: true, rules: getRules() })
})

app.get('/api/alarm/active', (_req, res) => {
  res.json(getActiveAlarms())
})

app.get('/api/alarm/history', (_req, res) => {
  res.json(getAlarmHistory())
})

app.post('/api/alarm/ack', (req, res) => {
  const { name } = req.body
  if (name) acknowledgeAlarm(name)
  else acknowledgeAll()
  res.json({ success: true, active: getActiveAlarms() })
})

// ─── API: 历史数据 ──────────────────────────────────────
app.get('/api/history', (req, res) => {
  const name = req.query.name as string
  const from = req.query.from ? Number(req.query.from) : undefined
  const to = req.query.to ? Number(req.query.to) : undefined
  const limit = req.query.limit ? Number(req.query.limit) : 10000
  if (!name) return res.status(400).json({ error: '请提供 name' })
  const data = queryHistory(name, from, to, limit)
  res.json({ name, count: data.length, data })
})

app.get('/api/history/export', (req, res) => {
  const name = req.query.name as string
  const from = req.query.from ? Number(req.query.from) : undefined
  const to = req.query.to ? Number(req.query.to) : undefined
  if (!name) return res.status(400).json({ error: '请提供 name' })
  const csv = exportCSV(name, from, to)
  res.setHeader('Content-Type', 'text/csv')
  res.setHeader('Content-Disposition', `attachment; filename="${name}-history.csv"`)
  res.send(csv)
})

// ─── API: 系统诊断 ──────────────────────────────────────
app.get('/api/diagnostics', (_req, res) => {
  res.json(getDiagnostics())
})

app.post('/api/diagnostics/reset', (_req, res) => {
  resetDiagnostics()
  res.json({ success: true })
})

// ─── API: 配方管理 ──────────────────────────────────────
app.get('/api/recipe', (_req, res) => {
  res.json(getRecipes())
})

app.get('/api/recipe/:name', (req, res) => {
  const recipe = getRecipe(req.params.name)
  if (!recipe) return res.status(404).json({ error: '配方不存在' })
  res.json(recipe)
})

app.post('/api/recipe', async (req, res) => {
  const { name, values, description } = req.body
  if (!name || !values) return res.status(400).json({ error: '请提供 name 和 values' })
  try {
    const recipe = await createRecipe(name, values, description)
    res.json({ success: true, recipe })
  } catch (err) {
    res.status(400).json({ error: (err as Error).message })
  }
})

app.put('/api/recipe/:name', async (req, res) => {
  const { values, description } = req.body
  if (!values) return res.status(400).json({ error: '请提供 values' })
  try {
    const recipe = await updateRecipe(req.params.name, values, description)
    res.json({ success: true, recipe })
  } catch (err) {
    res.status(404).json({ error: (err as Error).message })
  }
})

app.delete('/api/recipe/:name', async (req, res) => {
  await deleteRecipe(req.params.name)
  res.json({ success: true })
})

app.post('/api/recipe/:name/apply', async (req, res) => {
  const recipe = getRecipe(req.params.name)
  if (!recipe) return res.status(404).json({ error: '配方不存在' })
  const results: { name: string; success: boolean; error?: string }[] = []
  for (const [varName, val] of Object.entries(recipe.values)) {
    try {
      if (runtimeMode === 'opcua') {
        const mapping = opcuaVarMap.find(m => m.name === varName)
        if (!mapping) { results.push({ name: varName, success: false, error: '未找到 OPC UA 映射' }); continue }
        await opcua.writeNode(mapping.nodeId, val)
      } else {
        const varCfg = config.variables.find(v => v.name === varName)
        if (!varCfg) { results.push({ name: varName, success: false, error: '未找到变量配置' }); continue }
        await plc.writeVariable(varCfg, val)
      }
      results.push({ name: varName, success: true })
    } catch (err) {
      results.push({ name: varName, success: false, error: (err as Error).message })
    }
  }
  res.json({ success: results.every(r => r.success), results })
})

app.post('/api/recipe/:name/snapshot', async (req, res) => {
  const { name, description } = req.body
  if (!name) return res.status(400).json({ error: '请提供配方名' })
  const values: Record<string, number> = {}
  if (runtimeMode === 'opcua') {
    const cache = opcua.getValueCache()
    for (const [n, v] of Object.entries(cache)) {
      values[n] = typeof v === 'number' ? v : (v ? 1 : 0)
    }
  } else {
    for (const [n, pt] of Object.entries(plcDataCache)) {
      values[n] = typeof (pt as any).value === 'number' ? (pt as any).value : ((pt as any).value ? 1 : 0)
    }
  }
  try { const recipe = await createRecipe(name, values, description); res.json({ success: true, recipe }) }
  catch (err) { res.status(400).json({ error: (err as Error).message }) }
})

// ─── API: 用户认证 ──────────────────────────────────────
app.post('/api/auth/login', (req, res) => {
  const { username, password } = req.body
  if (!username || !password) return res.status(400).json({ error: '请提供用户名和密码' })
  const result = authenticate(username, password)
  if (!result) return res.status(401).json({ error: '用户名或密码错误' })
  res.json({ success: true, ...result })
})

app.post('/api/auth/logout', (req, res) => {
  const token = extractToken(req)
  if (token) logout(token)
  res.json({ success: true })
})

app.get('/api/auth/me', (req, res) => {
  const token = extractToken(req)
  if (!token) return res.status(401).json({ error: '未登录' })
  const session = validateToken(token)
  if (!session) return res.status(401).json({ error: '会话已过期' })
  res.json({ username: session.username, role: session.role })
})

app.get('/api/auth/users', (_req, res) => {
  res.json(getUsers())
})

app.post('/api/auth/users', async (req, res) => {
  const { username, password, role } = req.body
  if (!username || !password || !role) return res.status(400).json({ error: '请提供 username, password, role' })
  try { await addUser(username, password, role); res.json({ success: true, users: getUsers() }) }
  catch (err) { res.status(400).json({ error: (err as Error).message }) }
})

app.delete('/api/auth/users/:username', async (req, res) => {
  await removeUser(req.params.username)
  res.json({ success: true, users: getUsers() })
})

app.post('/api/auth/change-password', async (req, res) => {
  const { oldPassword, newPassword } = req.body
  const token = extractToken(req)
  if (!token) return res.status(401).json({ error: '未登录' })
  const session = validateToken(token)
  if (!session) return res.status(401).json({ error: '会话已过期' })
  const ok = await changePassword(session.username, oldPassword, newPassword)
  if (!ok) return res.status(400).json({ error: '旧密码错误' })
  res.json({ success: true })
})

// ─── API: SSE 实时推送 ──────────────────────────────────
import { addClient, broadcast } from './sse.js'

app.get('/api/plc/stream', (req, res) => {
  res.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    Connection: 'keep-alive',
  })
  // OPC UA 模式下推送 OPC UA 数据缓存的快照
  const payload = runtimeMode === 'opcua'
    ? { db: opcuaDataCache, io: { i: {}, q: {} }, dbBlocks: {} }
    : { db: plcDataCache, io: ioDataCache, dbBlocks: dbBlockCache }
  res.write(`data: ${JSON.stringify(payload)}\n\n`)
  addClient(res)
})

// ─── 轮询 ────────────────────────────────────────────────
async function poll() {
  try {
    if (!plc.isConnected()) {
      try { await plc.connect(runtimePlcIp, config.plc.rack, config.plc.slot, runtimeLocalAddr, runtimeConnType, config.variables, dbBlocks, runtimeIORanges ?? undefined) } catch {}
      if (!plc.isConnected()) return
    }

    // 写入队列忙时跳过本次轮询（避免和 modifyBit 冲突）
    if (!plc.isQueueIdle()) return

    const t0 = performance.now()
    const result = await plc.readOnce()
    recordPoll(performance.now() - t0)

    plcDataCache = result.db
    ioDataCache.i = result.io.i
    ioDataCache.q = result.io.q
    dbBlockCache = result.dbBlocks

    // 写入趋势缓冲区
    const trendVals: Record<string, number | boolean> = {}
    for (const [name, pt] of Object.entries(result.db)) {
      trendVals[name] = pt.value
    }
    trendBuffer.push(trendVals)
    writePoints(trendVals)

    checkAlarms(trendVals)

    broadcast({ db: plcDataCache, io: ioDataCache, dbBlocks: dbBlockCache })
  } catch (err) {
    recordError((err as Error).message)
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
  stopOpcuaBroadcast()
  stopFlush()
  plc.disconnect()
  opcua.disconnect()
  process.exit(0)
})

start()

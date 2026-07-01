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
import { parseDBFile, parseUDTFile, parsedVarsToNodes7Tags } from './dbParser.js'
import { trendBuffer } from './ringBuffer.js'
import {
  checkAlarms, getRules, addRule, removeRule, updateRule,
  getActiveAlarms, getAlarmHistory, acknowledgeAlarm, acknowledgeAll,
  shelveAlarm, unshelveAlarm, addComment, clearAll,
  getStatistics, exportAlarmsCsv, exportRulesCsv, importRulesCsv,
  getAlarms, getShelvedAlarms,
} from './alarmEngine.js'
import { writePoints, queryHistory, exportCSV, stopFlush } from './historyStore.js'
import { recordPoll, recordError, getDiagnostics, resetDiagnostics } from './diagnostics.js'
import { getAllRecipes, loadRecipe, saveRecipe as saveRecipeSvc, deleteRecipe as deleteRecipeSvc, copyRecipe, getVersionHistory, loadRecipeVersion, restoreVersion, exportToCsv, importFromCsv, readCsvFileWithAutoDetect } from './recipeManager.js'
import { authenticate, validateToken, logout, getUsers, addUser, removeUser, changePassword, extractToken } from './auth.js'
import { logEvent, getEvents, getEventCount, getEventStats } from './eventLog.js'

/** 从请求中提取当前用户名，未登录则返回 'anonymous' */
function currentUser(req: any): string {
  try {
    const token = extractToken(req)
    if (!token) return 'anonymous'
    const session = validateToken(token)
    return session?.username || 'anonymous'
  } catch { return 'anonymous' }
}
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
let ioDataCache: { i: Record<number, number>; q: Record<number, number>; m: Record<number, number> } = { i: {}, q: {}, m: {} }
let runtimeConnType = 3
let runtimePollInterval = 1000
/** DB 块列表：用户在前端配置的要读取的 DB 块 */
interface DBBlockConfig {
  label: string
  dbNumber: number
  startOffset: number
  byteCount: number
}
let dbBlocks: DBBlockConfig[] = []
let dbBlockCache: Record<string, number[] | null> = {}

/** 暂停轮询（批量写入/读取时暂停，避免和 nodes7 内部操作冲突） */
function pausePolling() {
  if (pollingTimer) {
    clearInterval(pollingTimer)
    pollingTimer = null
  }
}

/** 恢复轮询 */
function resumePolling() {
  if (!pollingTimer && plc.isConnected()) {
    pollingTimer = setInterval(poll, runtimePollInterval)
    poll()
  }
}

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
  if (ioRanges) runtimeIORanges = ioRanges

  plc.disconnect()
  if (pollingTimer) clearInterval(pollingTimer)

  try {
    await plc.connect(runtimePlcIp, config.plc.rack, config.plc.slot, runtimeLocalAddr, runtimeConnType, config.variables, dbBlocks, ioRanges)
    plcDataCache = {}
    pollingTimer = setInterval(poll, runtimePollInterval)
    poll()
    logEvent('plc.connect', `已连接到 ${runtimePlcIp}（S7）`, currentUser(req))
    res.json({ success: true, message: `已连接到 ${runtimePlcIp}` })
  } catch (err) {
    res.status(502).json({ error: (err as Error).message })
  }
})

// ─── API: 断开 PLC ───────────────────────────────────────
app.post('/api/plc/disconnect', (req, res) => {
  logEvent('plc.disconnect', '断开 PLC 连接', currentUser(req))
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
    ioRanges: runtimeIORanges ?? config.ioRanges ?? null,
  })
})

// ─── OPC UA 连接 ────────────────────────────────────────
let runtimeMode: 's7' | 'opcua' = 's7'
/** 前端传过来的 I/Q 字节范围（做实时显示用，OPC UA 模式也存着） */
let runtimeIORanges: { i?: { start: number; end: number }[]; q?: { start: number; end: number }[]; m?: { start: number; end: number }[] } | null = null

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
  broadcast({ db: {}, io: { i: {}, q: {}, m: {} }, dbBlocks: {} })
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
  broadcast({ db: {}, io: { i: {}, q: {}, m: {} }, dbBlocks: {} })
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
    broadcast({ db, io: { i: {}, q: {}, m: {} }, dbBlocks: {} })
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
    res.json({ db: opcuaDataCache, io: { i: {}, q: {}, m: {} }, dbBlocks: {} })
  } else {
    res.json({ db: plcDataCache, io: ioDataCache, dbBlocks: dbBlockCache })
  }
})

// ─── API: 获取配置 ──────────────────────────────────────
app.get('/api/plc/config', (_req, res) => {
  res.json({
    pollInterval: config.pollInterval,
    ioRanges: config.ioRanges ?? { i: [{ start: 0, end: 1 }, { start: 8, end: 8 }], q: [{ start: 0, end: 1 }, { start: 8, end: 8 }], m: [{ start: 0, end: 8 }] },
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
    logEvent('plc.write', `写入 ${name} = ${value}`, currentUser(req), `DB${varCfg.dbNumber},${varCfg.offset}`)
    res.json({ success: true, name, value: Number(value) })
  } catch (err) {
    const msg = (err as Error).message
    logEvent('plc.write', `写入 ${name} 失败: ${msg}`, currentUser(req))
    console.error(`[PLC] 写入 ${name} 失败:`, msg)
    res.status(502).json({ error: msg })
  }
})

// ─── API: 导入 DB 文件 ─────────────────────────────────
let importedDBs: Record<string, { dbNumber: number; dbName: string; variables: import('./dbParser.js').ParsedDBVariable[] }> = {}
let udtDefs: import('./dbParser.js').UDTMap = {}

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
    const parsed = parseDBFile(content, dbNumber, udtDefs)
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
    udtDefs: Object.keys(udtDefs),
    tags: plc.getDebugTags(),
  })
})

// ─── API: 导入 UDT 定义文件 ─────────────────────────
app.post('/api/plc/import-udt', async (req, res) => {
  let content: string
  try {
    if (req.is('application/octet-stream')) {
      const chunks: Buffer[] = []
      for await (const chunk of req) chunks.push(chunk)
      content = Buffer.concat(chunks).toString('utf-8')
    } else {
      content = req.body?.content
    }
  } catch (err) {
    return res.status(400).json({ error: `读取 UDT 文件失败: ${(err as Error).message}` })
  }
  if (!content) return res.status(400).json({ error: '请提供 UDT 文件内容' })

  try {
    const parsed = parseUDTFile(content)
    const count = Object.keys(parsed).length
    if (count === 0) return res.status(400).json({ error: '未解析到任何 UDT 定义，请确认文件包含 TYPE 块' })
    // 合并到全局 udtDefs
    Object.assign(udtDefs, parsed)
    res.json({ success: true, count, names: Object.keys(parsed) })
  } catch (err) {
    res.status(400).json({ error: `UDT 解析失败: ${(err as Error).message}` })
  }
})

app.get('/api/plc/imported-udts', (_req, res) => {
  res.json(Object.keys(udtDefs))
})

app.get('/api/plc/imported-udts/:name', (req, res) => {
  const fields = udtDefs[req.params.name]
  if (!fields) return res.status(404).json({ error: '未找到 UDT' })
  res.json({ name: req.params.name, fields })
})

app.delete('/api/plc/imported-udts/:name', (req, res) => {
  delete udtDefs[req.params.name]
  res.json({ success: true })
})

app.delete('/api/plc/imported-dbs/:key', (req, res) => {
  const key = req.params.key
  let actualKey = key
  let db = importedDBs[actualKey]
  if (!db && req.query.dbName) {
    const entry = Object.entries(importedDBs).find(([, v]) => v.dbName === req.query.dbName)
    if (entry) { actualKey = entry[0]; db = entry[1] }
  }
  if (db) {
    const tags = parsedVarsToNodes7Tags(db.variables, db.dbNumber)
    for (const t of tags) plc.removeDynamicTag(t.tag)
    delete importedDBs[actualKey]
  }
  res.json({ success: true })
})

// ─── API: 刷新导入的 DB 块（切换模式/断连后重新注册） ──
app.post('/api/plc/imported-dbs/:key/refresh', async (req, res) => {
  let db = importedDBs[req.params.key]
  // 如果 key 不匹配（例如前端映射改了 dbNumber），按 dbName 回退查找
  if (!db && req.body?.dbName) {
    db = Object.values(importedDBs).find(d => d.dbName === req.body.dbName)
  }
  if (!db) return res.status(404).json({ error: `未找到 DB: ${req.params.key}` })

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
        const effectiveDbNumber = req.body?.dbNumber ?? db.dbNumber
        const tags = parsedVarsToNodes7Tags(db.variables, effectiveDbNumber)
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
  const { area, byte: byteAddr, bit, value } = req.body
  if (area !== 'q' && area !== 'm') return res.status(400).json({ error: '仅支持 Q/M 区写入' })
  if (byteAddr === undefined || bit === undefined || value === undefined) {
    return res.status(400).json({ error: '请提供 byte、bit 和 value' })
  }

  try {
    // 构造 S7 地址（如 QB8）→ modifyBit 会从 PLC 读当前字节 → 改位 → 写回
    const s7addr = `${area.toUpperCase()}B${byteAddr}`
    await plc.modifyBit(s7addr, Number(bit), !!value)
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
  const { variableKey, dataType, description, severity, conditionType, condition, threshold, deadband, onDelayMs, offDelayMs, area, isEnabled, name } = req.body
  const key = variableKey || name
  if (!key) return res.status(400).json({ error: '请提供 variableKey' })
  addRule({
    name: key,
    variableKey: key,
    dataType: dataType || 'BYTE',
    description: description || '',
    severity: severity ?? 0,
    conditionType: conditionType ?? 0,
    condition: condition || 'gt',
    threshold: threshold ?? 0,
    deadband: deadband ?? 0,
    onDelayMs: onDelayMs ?? 0,
    offDelayMs: offDelayMs ?? 0,
    area: area || '',
    isEnabled: isEnabled ?? true,
  })
  logEvent('alarm.rule_add', `添加报警规则: ${key}`, currentUser(req))
  res.json({ success: true, rules: getRules() })
})

app.put('/api/alarm/rules/:variableKey', (req, res) => {
  const { variableKey, dataType, description, severity, conditionType, condition, threshold, deadband, onDelayMs, offDelayMs, area, isEnabled } = req.body
  const key = variableKey || req.params.variableKey
  if (!key) return res.status(400).json({ error: '请提供 variableKey' })
  updateRule(req.params.variableKey, {
    name: key,
    variableKey: key,
    dataType: dataType || 'BYTE',
    description: description || '',
    severity: severity ?? 0,
    conditionType: conditionType ?? 0,
    condition: condition || 'gt',
    threshold: threshold ?? 0,
    deadband: deadband ?? 0,
    onDelayMs: onDelayMs ?? 0,
    offDelayMs: offDelayMs ?? 0,
    area: area || '',
    isEnabled: isEnabled ?? true,
  })
  logEvent('alarm.rule_update', `更新报警规则: ${req.params.variableKey} → ${key}`, currentUser(req))
  res.json({ success: true, rules: getRules() })
})

app.delete('/api/alarm/rules/:variableKey', (req, res) => {
  removeRule(req.params.variableKey)
  logEvent('alarm.rule_delete', `删除报警规则: ${req.params.variableKey}`, currentUser(req))
  res.json({ success: true, rules: getRules() })
})

app.get('/api/alarm/active', (_req, res) => {
  res.json(getActiveAlarms())
})

app.get('/api/alarm/shelved', (_req, res) => {
  res.json(getShelvedAlarms())
})

app.get('/api/alarm/history', (_req, res) => {
  res.json(getAlarmHistory())
})

app.get('/api/alarm/statistics', (_req, res) => {
  res.json(getStatistics())
})

app.get('/api/alarm/export', (req, res) => {
  const csv = exportAlarmsCsv()
  logEvent('alarm.export', `导出报警 CSV`, currentUser(req))
  res.setHeader('Content-Type', 'text/csv')
  res.setHeader('Content-Disposition', `attachment; filename="alarms-${new Date().toISOString().slice(0, 10)}.csv"`)
  res.send(csv)
})

app.get('/api/alarm/rules/export', (req, res) => {
  const csv = exportRulesCsv()
  logEvent('alarm.rules_export', `导出报警规则 CSV`, currentUser(req))
  res.setHeader('Content-Type', 'text/csv')
  res.setHeader('Content-Disposition', `attachment; filename="alarm-rules.csv"`)
  res.send(csv)
})

app.post('/api/alarm/rules/import', (req, res) => {
  const { csv } = req.body
  if (!csv) return res.status(400).json({ error: '请提供 csv 内容' })
  const count = importRulesCsv(csv)
  logEvent('alarm.rules_import', `导入报警规则: ${count} 条`, currentUser(req))
  res.json({ success: true, imported: count, rules: getRules() })
})

app.post('/api/alarm/ack', (req, res) => {
  const { id, by } = req.body
  const user = by || currentUser(req)
  if (id) { acknowledgeAlarm(id, user); logEvent('alarm.ack', `确认报警: ${id}`, user) }
  else { const n = acknowledgeAll(user); logEvent('alarm.ack', `全部确认: ${n} 条`, user) }
  res.json({ success: true })
})

app.post('/api/alarm/ack/:id', (req, res) => {
  const user = req.body.by || currentUser(req)
  acknowledgeAlarm(req.params.id, user)
  logEvent('alarm.ack', `确认报警: ${req.params.id}`, user)
  res.json({ success: true })
})

app.post('/api/alarm/shelve/:id', (req, res) => {
  const { durationMs, by } = req.body
  const user = by || currentUser(req)
  shelveAlarm(req.params.id, durationMs, user)
  logEvent('alarm.shelve', `搁置报警: ${req.params.id}${durationMs ? ` (${durationMs}ms)` : ' (永久)'}`, user)
  res.json({ success: true })
})

app.post('/api/alarm/unshelve/:id', (req, res) => {
  unshelveAlarm(req.params.id)
  logEvent('alarm.unshelve', `取消搁置报警: ${req.params.id}`, currentUser(req))
  res.json({ success: true })
})

app.post('/api/alarm/comment/:id', (req, res) => {
  const { comment } = req.body
  addComment(req.params.id, comment || '')
  logEvent('alarm.comment', `报警备注: ${req.params.id}`, currentUser(req), comment || '')
  res.json({ success: true })
})

app.post('/api/alarm/clear', (req, res) => {
  clearAll()
  logEvent('alarm.clear', `清除报警历史`, currentUser(req))
  res.json({ success: true })
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

// ─── API: 操作事件日志 ──────────────────────────────────
app.get('/api/events', (req, res) => {
  const limit = Number(req.query.limit) || 100
  const offset = Number(req.query.offset) || 0
  const type = req.query.type as any || undefined
  res.json({
    events: getEvents(limit, offset, type),
    total: getEventCount(type),
  })
})

app.get('/api/events/stats', (_req, res) => {
  res.json(getEventStats())
})

// ─── API: 配方管理 ──────────────────────────────────────
app.get('/api/recipe', (_req, res) => {
  res.json(getAllRecipes())
})

app.get('/api/recipe/:id', (req, res) => {
  const recipe = loadRecipe(req.params.id)
  if (!recipe) return res.status(404).json({ error: '配方不存在' })
  res.json(recipe)
})

app.post('/api/recipe', async (req, res) => {
  const { id, name, description, productCode, author, status, category, tags, defaultDbNumber, groups } = req.body
  if (!name) return res.status(400).json({ error: '请提供 name' })

  const now = new Date().toISOString()
  const recipe = {
    id: id || `recipe_${Date.now()}`,
    name,
    description: description || '',
    productCode: productCode || '',
    author: author || '',
    status: status ?? 0,
    createdAt: now,
    modifiedAt: now,
    version: 0,
    tags: Array.isArray(tags) ? tags : [],
    category: category || '',
    defaultDbNumber: defaultDbNumber ?? 1,
    groups: Array.isArray(groups) ? groups.map((g: any) => ({
      name: g.name || '参数组',
      description: g.description || '',
      parameterCount: g.parameters?.length ?? 0,
      parameters: g.parameters?.map((p: any) => ({
        name: p.name || '',
        value: p.value ?? 0,
        unit: p.unit || '',
        address: p.address ?? 0,
        scale: p.scale ?? 1.0,
        offset: p.offset ?? 0,
        minValue: p.minValue ?? -Infinity,
        maxValue: p.maxValue ?? Infinity,
        group: p.group || '',
        plcDataType: p.plcDataType || 'REAL',
        dbNumber: p.dbNumber ?? 0,
      })) ?? [],
    })) : [],
  }

  saveRecipeSvc(recipe)
  logEvent('recipe.create', `创建配方: ${recipe.name}`, currentUser(req))
  res.json({ success: true, recipe })
})

app.put('/api/recipe/:id', (req, res) => {
  const existing = loadRecipe(req.params.id)
  if (!existing) return res.status(404).json({ error: '配方不存在' })

  const { name, description, productCode, author, status, category, tags, defaultDbNumber, groups } = req.body
  if (name !== undefined) existing.name = name
  if (description !== undefined) existing.description = description
  if (productCode !== undefined) existing.productCode = productCode
  if (author !== undefined) existing.author = author
  if (status !== undefined) existing.status = status
  if (category !== undefined) existing.category = category
  if (tags !== undefined) existing.tags = Array.isArray(tags) ? tags : existing.tags
  if (defaultDbNumber !== undefined) existing.defaultDbNumber = defaultDbNumber

  if (groups !== undefined && Array.isArray(groups)) {
    existing.groups = groups.map((g: any) => ({
      name: g.name || '参数组',
      description: g.description || '',
      parameterCount: g.parameters?.length ?? 0,
      parameters: g.parameters?.map((p: any) => ({
        name: p.name || '',
        value: p.value ?? 0,
        unit: p.unit || '',
        address: p.address ?? 0,
        scale: p.scale ?? 1.0,
        offset: p.offset ?? 0,
        minValue: p.minValue ?? -Infinity,
        maxValue: p.maxValue ?? Infinity,
        group: p.group || '',
        plcDataType: p.plcDataType || 'REAL',
        dbNumber: p.dbNumber ?? 0,
      })) ?? [],
    }))
  }

  saveRecipeSvc(existing)
  logEvent('recipe.update', `更新配方: ${existing.name}`, currentUser(req))
  res.json({ success: true, recipe: existing })
})

app.delete('/api/recipe/:id', (req, res) => {
  const ok = deleteRecipeSvc(req.params.id)
  logEvent('recipe.delete', `删除配方: ${req.params.id}`, currentUser(req))
  res.json({ success: ok })
})

app.post('/api/recipe/:id/copy', (req, res) => {
  const { name } = req.body
  const newName = name || `副本-${req.params.id}`
  const copy = copyRecipe(req.params.id, newName)
  if (!copy) return res.status(404).json({ error: '源配方不存在' })
  res.json({ success: true, recipe: copy })
})

app.get('/api/recipe/:id/versions', (req, res) => {
  res.json(getVersionHistory(req.params.id))
})

app.get('/api/recipe/:id/versions/:version', (req, res) => {
  const version = parseInt(req.params.version)
  const recipe = loadRecipeVersion(req.params.id, version)
  if (!recipe) return res.status(404).json({ error: '版本不存在' })
  res.json(recipe)
})

app.post('/api/recipe/:id/restore/:version', (req, res) => {
  const version = parseInt(req.params.version)
  const restored = restoreVersion(req.params.id, version)
  if (!restored) return res.status(404).json({ error: '版本不存在' })
  res.json({ success: true, recipe: restored })
})

app.get('/api/recipe/:id/export-csv', (req, res) => {
  const recipe = loadRecipe(req.params.id)
  if (!recipe) return res.status(404).json({ error: '配方不存在' })
  const csv = exportToCsv(recipe)
  res.setHeader('Content-Type', 'text/csv')
  const encodedName = encodeURIComponent(recipe.name).replace(/%20/g, ' ')
  res.setHeader('Content-Disposition', `attachment; filename="${encodedName}.csv"; filename*=UTF-8''${encodeURIComponent(recipe.name)}.csv`)
  res.send(csv)
})

app.post('/api/recipe/:id/import-csv', (req, res) => {
  const { csv, targetGroup } = req.body
  if (!csv) return res.status(400).json({ error: '请提供 csv 内容' })
  const params = importFromCsv(csv, targetGroup)
  res.json({ success: true, imported: params.length, parameters: params })
})

app.post('/api/recipe/:id/apply', async (req, res) => {
  const recipe = loadRecipe(req.params.id)
  if (!recipe) return res.status(404).json({ error: '配方不存在' })
  const results: { name: string; success: boolean; error?: string }[] = []

  if (runtimeMode === 'opcua') {
    // OPC UA 模式：按变量名匹配映射表写入
    for (const group of recipe.groups) {
      for (const param of group.parameters) {
        try {
          const mapping = opcuaVarMap.find(m => m.name === param.name)
          if (!mapping) { results.push({ name: param.name, success: false, error: '未找到 OPC UA 映射' }); continue }
          await opcua.writeNode(mapping.nodeId, param.value)
          results.push({ name: param.name, success: true })
        } catch (err) {
          results.push({ name: param.name, success: false, error: (err as Error).message })
        }
      }
    }
  } else {
    // S7 模式：按 recipe 参数里的 dbNumber + address + plcDataType 构造地址直接写入
    pausePolling()
    try {
      const DATA_TYPE_MAP: Record<string, string> = {
        REAL: 'R', INT: 'I', DINT: 'DI', UINT: 'I', UDINT: 'DI',
        WORD: 'W', DWORD: 'DW', BYTE: 'B', USINT: 'B', SINT: 'B', BOOL: 'X',
      }
      for (const group of recipe.groups) {
        for (const param of group.parameters) {
          try {
            const db = param.dbNumber > 0 ? param.dbNumber : (recipe.defaultDbNumber > 0 ? recipe.defaultDbNumber : 1)
            const addrType = DATA_TYPE_MAP[param.plcDataType] || 'R'
            const bitSuffix = addrType === 'X' ? '.0' : '.1'
            const s7addr = `DB${db},${addrType}${param.address}${bitSuffix}`
            await plc.writeRaw(s7addr, Number(param.value))
            results.push({ name: param.name, success: true })
          } catch (err) {
            results.push({ name: param.name, success: false, error: (err as Error).message })
          }
        }
      }
    } finally { resumePolling() }
  }
  const successCount = results.filter(r => r.success).length
  logEvent('recipe.apply', `下载配方 "${recipe.name}"：${successCount}/${results.length} 成功`, currentUser(req))
  res.json({ success: successCount === results.length, results })
})

app.post('/api/recipe/:id/upload', async (req, res) => {
  const recipe = loadRecipe(req.params.id)
  if (!recipe) return res.status(404).json({ error: '配方不存在' })
  const updated: { name: string; value: number; success: boolean; error?: string }[] = []

  if (runtimeMode === 'opcua') {
    // OPC UA 模式：按变量名匹配映射表读取
    for (const group of recipe.groups) {
      for (const param of group.parameters) {
        try {
          const mapping = opcuaVarMap.find(m => m.name === param.name)
          if (!mapping) { updated.push({ name: param.name, value: param.value, success: false, error: '未找到 OPC UA 映射' }); continue }
          const data = await opcua.readNodes([mapping.nodeId])
          const newVal = data[mapping.nodeId]
          if (typeof newVal === 'number') {
            param.value = newVal
            updated.push({ name: param.name, value: newVal, success: true })
          } else {
            updated.push({ name: param.name, value: param.value, success: false, error: '读取返回非数值' })
          }
        } catch (err) {
          updated.push({ name: param.name, value: param.value, success: false, error: (err as Error).message })
        }
      }
    }
  } else {
    // S7 模式：批量构造地址，一次读取
    const DATA_TYPE_MAP: Record<string, string> = {
      REAL: 'R', INT: 'I', DINT: 'DI', UINT: 'I', UDINT: 'DI',
      WORD: 'W', DWORD: 'DW', BYTE: 'B', USINT: 'B', SINT: 'B', BOOL: 'X',
    }
    const addrs: { tag: string; s7addr: string; param: any }[] = []
    for (const group of recipe.groups) {
      for (const param of group.parameters) {
        const db = param.dbNumber > 0 ? param.dbNumber : (recipe.defaultDbNumber > 0 ? recipe.defaultDbNumber : 1)
        const addrType = DATA_TYPE_MAP[param.plcDataType] || 'R'
        const bitSuffix = addrType === 'X' ? '.0' : '.1'
        const s7addr = `DB${db},${addrType}${param.address}${bitSuffix}`
        const tag = `_up_${param.name.replace(/[^a-zA-Z0-9_]/g, '_')}`
        addrs.push({ tag, s7addr, param })
      }
    }
    try {
      const values = await plc.readMultipleRaw(addrs.map(a => ({ tag: a.tag, s7addr: a.s7addr })))
      for (const a of addrs) {
        const v = values[a.tag]
        if (v !== undefined) {
          a.param.value = v
          updated.push({ name: a.param.name, value: v, success: true })
        } else {
          updated.push({ name: a.param.name, value: a.param.value, success: false, error: '读取无返回值' })
        }
      }
    } catch (err) {
      for (const a of addrs) {
        updated.push({ name: a.param.name, value: a.param.value, success: false, error: (err as Error).message })
      }
    }
  }

  // 上传成功后，把更新后的值保存到配方文件
  if (updated.some(u => u.success)) {
    saveRecipeSvc(recipe)
  }

  const successCount = updated.filter(u => u.success).length
  logEvent('recipe.upload', `上传配方 "${recipe.name}"：${successCount}/${updated.length} 成功`, currentUser(req))
  res.json({ success: successCount > 0, results: updated, recipe })
})

app.post('/api/recipe/snapshot', async (req, res) => {
  const { name, description, defaultDbNumber } = req.body
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

  const now = new Date().toISOString()
  const params = Object.entries(values).map(([k, v], i) => ({
    name: k, value: v, unit: '', address: i * 2, scale: 1.0, offset: 0,
    minValue: -Infinity, maxValue: Infinity, group: '', plcDataType: 'REAL', dbNumber: 0,
  }))
  const recipe = {
    id: `recipe_${Date.now()}`,
    name,
    description: description || '',
    productCode: '', author: '', status: 0,
    createdAt: now, modifiedAt: now, version: 0,
    tags: [], category: '', defaultDbNumber: defaultDbNumber ?? 1,
    groups: [{ name: '参数组1', description: '', parameterCount: params.length, parameters: params }],
  }
  saveRecipeSvc(recipe)
  res.json({ success: true, recipe })
})

// ─── API: 用户认证 ──────────────────────────────────────
app.post('/api/auth/login', (req, res) => {
  const { username, password } = req.body
  if (!username || !password) return res.status(400).json({ error: '请提供用户名和密码' })
  const result = authenticate(username, password)
  if (!result) return res.status(401).json({ error: '用户名或密码错误' })
  logEvent('auth.login', `用户登录: ${username}`, username)
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
    ? { db: opcuaDataCache, io: { i: {}, q: {}, m: {} }, dbBlocks: {} }
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
    ioDataCache.m = result.io.m
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
import { resolveAndSavePort } from './port.js'

const PORT = parseInt(process.env.PORT || '') || 3000

async function start() {
  // 用 PORT 作为基线找空闲端口，写入 .port.json（开发者可随时调用 tsx server/resolve-port.ts 单独分配）
  const apiPort = await resolveAndSavePort(PORT)

  app.listen(apiPort, () => {
    console.log(`\n========================================`)
    console.log(`  Trioop PLC Monitor`)
    console.log(`  环境: ${isDev ? '开发' : '生产'}`)
    console.log(`  API:  http://localhost:${apiPort}/api/plc`)
    console.log(`  推流: http://localhost:${apiPort}/api/plc/stream`)
    console.log(`  Port: ${apiPort} (基线: ${PORT})`)
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

start().catch(err => { console.error('启动失败:', err); process.exit(1) })

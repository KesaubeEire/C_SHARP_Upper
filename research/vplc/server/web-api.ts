/**
 * vPLC Web API — HTTP REST 接口
 * 供 React 前端 /api/vplc 系列 API
 */

import http from 'http'
import { memory, ensureDbSize, markMemDirty, dbsConfig, udtDefs, importedDBs, importedTriggers,
         readTypedValueFromMemory, writeTypedValueToMemory, randomValueForVar, buildImportedSnapshot,
         calcImportedDbSize } from './plc-memory.js'
import { plcState, stateChangedAt, rtcOffset, setRtcOffset, setPlcState, addDiag, getLedsSnapshot, getRtcIso, getDiagBuffer, clearDiagBuffer } from './plc-state.js'
import { obCycles, resetAllOBs, getRuntimeSnapshot, setUserScripts, getUserScripts } from './plc-runtime.js'
import { writeConfig } from './persistence.js'

// ─── 导入解析器 ──
import { parseUDTFile, parseDBFile, extractReferencedUDTs } from './dbParser.js'

// ─── JSON 响应工具 ──

function json(res: http.ServerResponse, code: number, data: any) {
  res.writeHead(code, {
    'Content-Type': 'application/json',
    'Access-Control-Allow-Origin': '*',
  })
  res.end(JSON.stringify(data))
}

function readBody(req: http.IncomingMessage): Promise<Buffer> {
  return new Promise(resolve => {
    const chunks: Buffer[] = []
    req.on('data', (c: Buffer) => chunks.push(c))
    req.on('end', () => resolve(Buffer.concat(chunks)))
  })
}

// ─── 快照 ──

function memorySnapshot() {
  const snap: Record<string, any> = { DB: {}, PE: {}, PA: {}, MK: {} }
  for (const [k, v] of Object.entries(memory.DB)) {
    snap.DB[`DB${k}`] = Array.from(v.subarray(0, Math.min(v.length, 128)))
  }
  snap.PE = Array.from(memory.PE.subarray(0, 32))
  snap.PA = Array.from(memory.PA.subarray(0, 32))
  snap.MK = Array.from(memory.MK.subarray(0, 128))

  const importedSnapshot = buildImportedSnapshot()
  snap.fields = importedSnapshot.fields
  snap._imported = importedSnapshot.imported
  snap._triggers = importedTriggers

  const parsed = getRuntimeSnapshot()
  snap._parsed = {
    ...parsed,
    state: { mode: plcState, since: stateChangedAt },
    rtc: getRtcIso(),
    leds: getLedsSnapshot(),
    ob: obCycles.map(o => ({
      num: o.num, name: o.name, type: o.type,
      runCount: o.runCount, errors: o.errors,
      lastExecuteMs: o.lastExecuteMs, lastRun: o.lastRun, state: o.state,
    })),
  }
  return snap
}

// ─── 路由处理 ──

function extractDbImportKey(url: string): string | null {
  const m = url.match(/\/api\/vplc\/imported-dbs\/([^/]+)/)
  return m ? decodeURIComponent(m[1]) : null
}

export async function handleAPI(req: http.IncomingMessage, res: http.ServerResponse): Promise<void> {
  const url = req.url || ''
  const method = req.method || 'GET'

  const write404 = () => json(res, 404, { error: 'Not Found', api: '/api/vplc' })

  // CORS
  if (method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET,POST,DELETE',
      'Access-Control-Allow-Headers': 'Content-Type',
    })
    res.end()
    return
  }

  // ── 主快照 ──
  if (url === '/api/vplc' && method === 'GET') { json(res, 200, memorySnapshot()); return }

  // ── 写入 ──
  if (url === '/api/vplc/write' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const { area, dbNumber, offset, type, value, bit } = body
    if (type === 'bit' && bit !== undefined) {
      const mem = area === 'I' ? memory.PE : area === 'Q' ? memory.PA : area === 'M' ? memory.MK : null
      if (mem && offset < mem.length) {
        if (value) mem[offset] |= (1 << bit); else mem[offset] &= ~(1 << bit)
      }
    } else {
      const mem = area === 'I' ? memory.PE : area === 'Q' ? memory.PA : area === 'M' ? memory.MK : area === 'DB' ? memory.DB[dbNumber] : null
      if (mem && offset >= 0) {
        if (type === 'byte') mem[offset] = value & 0xFF
        else if (type === 'real' && offset + 4 <= mem.length) {
          new DataView(mem.buffer, mem.byteOffset + offset, 4).setFloat32(0, value, false)
        }
      }
    }
    markMemDirty()
    json(res, 200, { success: true })
    return
  }

  // ── 切换位 ──
  if (url === '/api/vplc/toggle-bit' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const { area, offset, bit } = body
    const mem = area === 'I' ? memory.PE : area === 'Q' ? memory.PA : area === 'M' ? memory.MK : null
    if (mem && offset < mem.length) {
      mem[offset] ^= (1 << bit)
      markMemDirty()
    }
    json(res, 200, { success: true })
    return
  }

  // ── 创建 DB ──
  if (url === '/api/vplc/create-db' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const { dbNumber, size } = body
    if (!memory.DB[dbNumber]) {
      memory.DB[dbNumber] = new Uint8Array(size || 64)
      markMemDirty()
    }
    json(res, 200, { success: true })
    return
  }

  // ── DB 块配置管理 ──
  if (url === '/api/vplc/dbs' && method === 'GET') { json(res, 200, dbsConfig); return }
  if (url === '/api/vplc/dbs' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const { dbNumber, size } = body
    if (!dbNumber || !size) { json(res, 400, { error: '需要 dbNumber 和 size' }); return }
    dbsConfig[String(dbNumber)] = size
    memory.DB[dbNumber] = new Uint8Array(size)
    markMemDirty()
    writeConfig()
    json(res, 200, { success: true, dbs: dbsConfig })
    return
  }
  if (url.match(/^\/api\/vplc\/dbs\/\d+$/) && method === 'DELETE') {
    const key = url.split('/').pop() || ''
    if (!dbsConfig[key]) { json(res, 404, { error: 'DB 不存在' }); return }
    delete dbsConfig[key]
    delete memory.DB[Number(key)]
    markMemDirty()
    writeConfig()
    json(res, 200, { success: true, dbs: dbsConfig })
    return
  }

  // ── UDT 导入 ──
  if (url === '/api/vplc/import-udt' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const content = body.content || ''
    if (!content) { json(res, 400, { error: '请提供 UDT 文件内容' }); return }
    try {
      const parsed = parseUDTFile(content)
      Object.assign(udtDefs, parsed)
      writeConfig()
      json(res, 200, { success: true, count: Object.keys(parsed).length, names: Object.keys(parsed) })
    } catch (err) {
      json(res, 400, { error: `UDT 解析失败: ${(err as Error).message}` })
    }
    return
  }
  if (url === '/api/vplc/imported-udts' && method === 'GET') { json(res, 200, Object.keys(udtDefs)); return }
  if (url.match(/^\/api\/vplc\/imported-udts\//) && method === 'GET') {
    const name = decodeURIComponent(url.split('/').pop() || '')
    const fields = udtDefs[name]
    if (!fields) { json(res, 404, { error: '未找到 UDT' }); return }
    json(res, 200, { name, fields })
    return
  }
  if (url.match(/^\/api\/vplc\/imported-udts\//) && method === 'DELETE') {
    const name = decodeURIComponent(url.split('/').pop() || '')
    delete udtDefs[name]
    writeConfig()
    json(res, 200, { success: true })
    return
  }

  // ── DB 导入 ──
  if (url === '/api/vplc/import-db' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const content = body.content || ''
    const dbNumberOverride = body.dbNumber ? Number(body.dbNumber) : undefined
    if (!content) { json(res, 400, { error: '请提供 DB 文件内容' }); return }
    try {
      const udtCheck = extractReferencedUDTs(content, udtDefs)
      if (udtCheck.missing.length > 0) {
        json(res, 412, { error: '缺少 UDT 数据类型', missingUdt: udtCheck.missing, allUdt: udtCheck.all })
        return
      }
      const parsed = parseDBFile(content, dbNumberOverride, udtDefs)
      if (parsed.optimized) { json(res, 400, { error: `DB"${parsed.dbName}" 开启了优化块访问，无法通过绝对地址读取` }); return }
      const byteSize = calcImportedDbSize(parsed.variables)
      ensureDbSize(parsed.dbNumber, byteSize)
      dbsConfig[String(parsed.dbNumber)] = Math.max(dbsConfig[String(parsed.dbNumber)] || 0, byteSize)
      const key = `${parsed.dbNumber}_${parsed.dbName}`
      const now = Date.now()
      importedDBs[key] = {
        key, dbNumber: parsed.dbNumber, dbName: parsed.dbName,
        variableCount: parsed.variables.length, variables: parsed.variables,
        byteSize, rawContent: content,
        createdAt: importedDBs[key]?.createdAt || now, updatedAt: now,
      }
      writeConfig()
      json(res, 200, { success: true, dbNumber: parsed.dbNumber, dbName: parsed.dbName, variableCount: parsed.variables.length, variables: parsed.variables })
    } catch (err) {
      json(res, 400, { error: `解析失败: ${(err as Error).message}` })
    }
    return
  }
  if (url === '/api/vplc/imported-dbs' && method === 'GET') {
    const result = Object.values(importedDBs).map(db => {
      const mem = ensureDbSize(db.dbNumber, db.byteSize)
      const values: Record<string, any> = {}
      for (const v of db.variables) values[v.name] = readTypedValueFromMemory(mem, v)
      return { dbNumber: db.dbNumber, dbName: db.dbName, variableCount: db.variableCount, variables: db.variables, values }
    })
    json(res, 200, result)
    return
  }
  if (url.match(/^\/api\/vplc\/imported-dbs\//) && method === 'DELETE') {
    const key = extractDbImportKey(url)
    if (!key || !importedDBs[key]) { json(res, 404, { error: '未找到 DB' }); return }
    delete importedDBs[key]
    writeConfig()
    json(res, 200, { success: true })
    return
  }
  // refresh
  const refreshMatch = url.match(/\/api\/vplc\/imported-dbs\/([^/]+)\/refresh/)
  if (refreshMatch && (method === 'POST' || method === 'GET')) {
    const key = decodeURIComponent(refreshMatch[1])
    const db = importedDBs[key]
    if (!db) { json(res, 404, { error: '未找到 DB' }); return }
    ensureDbSize(db.dbNumber, db.byteSize)
    if (db.rawContent) {
      const parsed = parseDBFile(db.rawContent, db.dbNumber, udtDefs)
      db.variables = parsed.variables
      db.variableCount = parsed.variables.length
      db.byteSize = calcImportedDbSize(parsed.variables)
      db.updatedAt = Date.now()
      ensureDbSize(db.dbNumber, db.byteSize)
    }
    const mem = ensureDbSize(db.dbNumber, db.byteSize)
    const values: Record<string, any> = {}
    for (const v of db.variables) values[v.name] = readTypedValueFromMemory(mem, v)
    json(res, 200, { success: true, registered: db.variableCount, values })
    return
  }
  // imported-dbs write
  const writeMatch = url.match(/\/api\/vplc\/imported-dbs\/([^/]+)\/write/)
  if (writeMatch && method === 'POST') {
    const key = decodeURIComponent(writeMatch[1])
    const db = importedDBs[key]
    if (!db) { json(res, 404, { error: '未找到 DB' }); return }
    const body = JSON.parse((await readBody(req)).toString())
    const { fieldName, value } = body
    const variable = db.variables.find(v => v.name === fieldName)
    if (!variable) { json(res, 404, { error: '未找到字段' }); return }
    const mem = ensureDbSize(db.dbNumber, db.byteSize)
    if (!writeTypedValueToMemory(mem, variable, value)) { json(res, 400, { error: '写入失败' }); return }
    markMemDirty()
    json(res, 200, { success: true })
    return
  }
  // randomize
  const rndMatch = url.match(/\/api\/vplc\/imported-dbs\/([^/]+)\/randomize/)
  if (rndMatch && method === 'POST') {
    const key = decodeURIComponent(rndMatch[1])
    const db = importedDBs[key]
    if (!db) { json(res, 404, { error: '未找到 DB' }); return }
    const body = JSON.parse((await readBody(req)).toString())
    const { fieldName } = body
    const variable = db.variables.find(v => v.name === fieldName)
    if (!variable) { json(res, 404, { error: '未找到字段' }); return }
    const mem = ensureDbSize(db.dbNumber, db.byteSize)
    const value = randomValueForVar(variable)
    if (!writeTypedValueToMemory(mem, variable, value)) { json(res, 400, { error: '随机写入失败' }); return }
    markMemDirty()
    json(res, 200, { success: true, value, type: variable.type })
    return
  }

  // ── 触发器 ──
  if (url === '/api/vplc/triggers' && method === 'GET') { json(res, 200, importedTriggers); return }
  if (url === '/api/vplc/triggers' && method === 'POST') {
    json(res, 200, { id: Date.now().toString(), active: false })
    return
  }
  if (url.match(/^\/api\/vplc\/triggers\//) && method === 'DELETE') { json(res, 200, { success: true }); return }

  // ── OB 状态 ──
  if (url === '/api/vplc/ob' && method === 'GET') {
    json(res, 200, obCycles.map(o => ({
      num: o.num, name: o.name, type: o.type,
      intervalMs: o.intervalMs, runCount: o.runCount, errors: o.errors,
      lastExecuteMs: o.lastExecuteMs, lastRun: o.lastRun, state: o.state,
    })))
    return
  }
  if (url.match(/^\/api\/vplc\/ob\/(reset|\d+)$/) && method === 'POST') {
    const target = url.split('/').pop()
    if (target === 'reset') {
      resetAllOBs()
      json(res, 200, { success: true, message: '所有 OB 已重置' })
    } else {
      const obNum = Number(target)
      const ob = obCycles.find(o => o.num === obNum)
      if (!ob) { json(res, 404, { error: `OB${obNum} 不存在` }); return }
      ob.runCount = 0; ob.errors = 0; ob.lastRun = 0; ob.lastExecuteMs = 0; ob.state = 'waiting'
      json(res, 200, { success: true, message: `OB${obNum} 已重置` })
    }
    return
  }
  const obResetMatch = url.match(/^\/api\/vplc\/ob\/(\d+)\/reset$/)
  if (obResetMatch && method === 'POST') {
    const obNum = Number(obResetMatch[1])
    const ob = obCycles.find(o => o.num === obNum)
    if (!ob) { json(res, 404, { error: `OB${obNum} 不存在` }); return }
    ob.runCount = 0; ob.errors = 0; ob.lastRun = 0; ob.lastExecuteMs = 0; ob.state = 'waiting'
    json(res, 200, { success: true, message: `OB${obNum} 已重置` })
    return
  }

  // ── RUN/STOP ──
  if (url === '/api/vplc/state' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    if (body.state !== 'RUN' && body.state !== 'STOP') { json(res, 400, { error: '无效状态' }); return }
    const prev = plcState
    setPlcState(body.state)
    addDiag('info', 'STATE', `PLC 状态: ${prev} → ${plcState}`)
    json(res, 200, { success: true, state: plcState, since: stateChangedAt })
    return
  }

  // ── RTC ──
  if (url === '/api/vplc/rtc' && method === 'GET') {
    json(res, 200, { iso: getRtcIso(), offsetMs: rtcOffset })
    return
  }
  if (url === '/api/vplc/rtc' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    if (body.iso) {
      const t = new Date(body.iso).getTime()
      if (isNaN(t)) { json(res, 400, { error: '无效 ISO 日期' }); return }
      setRtcOffset(t - Date.now())
    } else if (body.offset !== undefined) {
      setRtcOffset(body.offset)
    }
    addDiag('info', 'RTC', `PLC 时间设置为 ${getRtcIso()}`)
    json(res, 200, { success: true, iso: getRtcIso(), offsetMs: rtcOffset })
    return
  }

  // ── 诊断缓冲区 ──
  if (url === '/api/vplc/diag' && method === 'GET') { json(res, 200, getDiagBuffer()); return }
  if (url === '/api/vplc/diag' && method === 'DELETE') { clearDiagBuffer(); json(res, 200, { success: true }); return }

  // ── LED ──
  if (url === '/api/vplc/leds' && method === 'GET') {
    json(res, 200, { state: plcState, leds: getLedsSnapshot() })
    return
  }

  // ── 用户脚本 API (步骤 3) ──
  if (url === '/api/vplc/scripts' && method === 'GET') {
    json(res, 200, getUserScripts())
    return
  }
  if (url === '/api/vplc/scripts' && method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    setUserScripts(body.scripts || [])
    writeConfig()
    json(res, 200, { success: true })
    return
  }

  // ── Modbus 状态 ──
  if (url === '/api/vplc/modbus' && method === 'GET') {
    json(res, 200, { enabled: true, port: (global as any).__modbusPort || 0 })
    return
  }

  write404()
}

// ─── HTTP 服务创建 ──

export function createWebServer(): http.Server {
  return http.createServer((req, res) => {
    handleAPI(req, res).catch(() => {
      res.writeHead(500, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ error: 'Internal Server Error' }))
    })
  })
}

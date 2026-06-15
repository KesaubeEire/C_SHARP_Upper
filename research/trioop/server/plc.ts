/**
 * S7 PLC 通信层
 * 使用 nodes7 库（比 snap7 更稳定，支持 I/Q 区读写）
 */

import net from 'net'
import os from 'os'
import NodeS7 from 'nodes7'
import type { PLCVariable, PLCData, IOAreaData } from '../shared/types.js'

// ─── 连接管理 ─────────────────────────────────────────────

let client: InstanceType<typeof NodeS7> | null = null
let _connected = false
const _addrMap = new Map<string, string>()  // 标签→S7地址 映射（可变，支持动态添加）

// 读取回调缓存
let _latestValues: Record<string, any> = {}
let _readPending = false
let _readResolve: ((v: Record<string, any>) => void) | null = null

export function isConnected(): boolean {
  return _connected
}

/** 列出本机所有活跃网卡 (名称 + IPv4) */
export function listNetworkAdapters(): { name: string; ip: string; family: string }[] {
  const ifaces = os.networkInterfaces()
  const result: { name: string; ip: string; family: string }[] = []
  for (const [name, addrs] of Object.entries(ifaces)) {
    if (!addrs) continue
    for (const addr of addrs) {
      if (addr.family === 'IPv4' && !addr.internal) {
        result.push({ name, ip: addr.address, family: addr.family })
      }
    }
  }
  return result
}

// ─── 地址映射表 ───────────────────────────────────────────

interface AddrEntry {
  tag: string
  s7addr: string
  type: string   // 'i' | 'q' | 'db_var' | 'db_block'
  ref?: PLCVariable | string
}

let addrTable: AddrEntry[] = []
let dbBlockTable: { label: string; dbNumber: number; startOffset: number; byteCount: number }[] = []

/** 注册 I/O 字节 */
function registerIO() {
  for (let b = 0; b <= 8; b++) {
    addrTable.push({ tag: `IB${b}`, s7addr: `IB${b}`, type: 'i' })
    addrTable.push({ tag: `QB${b}`, s7addr: `QB${b}`, type: 'q' })
  }
}

/** 注册配置变量 */
function registerVariables(variables: PLCVariable[]) {
  for (const v of variables) {
    const tag = `cfg_${v.name}`
    let s7addr: string
    if (v.type === 'bool') {
      s7addr = `DB${v.dbNumber},X${v.offset}.${v.bit ?? 0}`
    } else {
      const typeMap: Record<string, string> = {
        real: 'R', int: 'I', dint: 'DI',
        word: 'W', dword: 'DW', byte: 'B',
      }
      const t = typeMap[v.type] || 'B'
      s7addr = `DB${v.dbNumber},${t}${v.offset}.1`
    }
    addrTable.push({ tag, s7addr, type: 'db_var', ref: v })
  }
}

/** 注册 DB 块 */
function registerDBBlock(label: string, dbNumber: number, startOffset: number, byteCount: number) {
  const tag = `db_${label}`
  const s7addr = `DB${dbNumber},B${startOffset}.${byteCount}`
  addrTable.push({ tag, s7addr, type: 'db_block', ref: label })
  dbBlockTable.push({ label, dbNumber, startOffset, byteCount })
}

/** 移除 DB 块注册 */
function unregisterDBBlock(label: string) {
  addrTable = addrTable.filter(a => !(a.type === 'db_block' && a.ref === label))
  dbBlockTable = dbBlockTable.filter(b => b.label !== label)
}

/** 初始化 nodes7 地址表 */
function initAddrTable(variables: PLCVariable[], dbBlocks: { label: string; dbNumber: number; startOffset: number; byteCount: number }[]) {
  addrTable = []
  registerIO()
  registerVariables(variables)
  for (const b of dbBlocks) {
    registerDBBlock(b.label, b.dbNumber, b.startOffset, b.byteCount)
  }
}

// ─── 连接 ─────────────────────────────────────────────────

export async function connect(
  ip: string, rack: number, slot: number,
  localAddress?: string, connType?: number,
  variables?: PLCVariable[],
  dbBlocks?: { label: string; dbNumber: number; startOffset: number; byteCount: number }[],
): Promise<void> {
  return new Promise((resolve, reject) => {
    if (client) {
      client.dropConnection()
    }

    client = new NodeS7()

    // 注册所有地址
    _addrMap.clear()
    initAddrTable(variables ?? [], dbBlocks ?? [])
    addrTable.forEach(a => _addrMap.set(a.tag, a.s7addr))

    // 建立翻译回调：已知标签查表，未知的直通（用于 write IO 点位）
    client.setTranslationCB((tag: string) => {
      const mapped = _addrMap.get(tag)
      if (mapped) return mapped
      if (/^[IQM]\d+\.\d+$/.test(tag)) return tag
      if (/^DB\d+,/.test(tag)) return tag
      return ''
    })

    // 添加所有 tag
    const tags = addrTable.map(a => a.tag)
    client.addItems(tags)

    const connTSAP = connType ?? 1
    const remoteTSAP = (connTSAP << 8) + (rack * 0x20) + slot
    client.initiateConnection({
      host: ip,
      port: 102,
      rack,
      slot,
      timeout: 3000,
      localTSAP: connTSAP << 8,
      remoteTSAP,
    }, (err: any) => {
      if (err) {
        _connected = false
        reject(new Error(`PLC 连接失败: ${err}`))
      } else {
        _connected = true
        resolve()
      }
    })
  })
}

export function disconnect(): void {
  if (client) {
    client.dropConnection()
    client = null
  }
  _connected = false
}

// ─── 批量读取 ─────────────────────────────────────────────

/** 读取所有已注册项，返回 { tag: value } */
async function readAllItems(): Promise<Record<string, any>> {
  if (!client || !_connected) throw new Error('PLC 未连接')

  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error('读取超时')), 5000)

    client!.readAllItems((err: any, values: Record<string, any>) => {
      clearTimeout(timeout)
      if (err) {
        reject(new Error(`读取失败: ${err}`))
      } else {
        _latestValues = values
        resolve(values)
      }
    })
  })
}

// ─── 读取 I/O 区 ─────────────────────────────────────────

export async function readIOBytes(area: 'PE' | 'PA'): Promise<Record<number, number> | null> {
  try {
    const values = await readAllItems()
    const result: Record<number, number> = {}

    const prefix = area === 'PE' ? 'IB' : 'QB'
    for (let b = 0; b <= 8; b++) {
      const key = `${prefix}${b}`
      if (values[key] !== undefined && values[key] !== null) {
        result[b] = values[key]
      }
    }

    return Object.keys(result).length > 0 ? result : null
  } catch {
    return null
  }
}

// ─── 读取配置变量 ─────────────────────────────────────────

export async function readAll(variables: PLCVariable[]): Promise<PLCData> {
  try {
    const values = await readAllItems()
    const data: PLCData = {}

    for (const v of variables) {
      const tag = `cfg_${v.name}`
      const val = values[tag]
      if (val !== undefined && val !== null) {
        data[v.name] = {
          value: val as number | boolean,
          type: v.type,
          writable: !!v.writable,
          dbNumber: v.dbNumber,
          offset: v.offset,
        }
      }
    }

    return data
  } catch {
    return {}
  }
}

/**
 * 一次性读取所有已注册项（DB 变量 + I/Q 字节 + DB 块）
 * 比多次调用 readAll/readIOBytes 更高效（nodes7 合并为一次 S7 请求）
 */
export interface ReadOnceResult {
  db: PLCData
  io: { i: Record<number, number>; q: Record<number, number> }
  dbBlocks: Record<string, number[] | null>
}

export async function readOnce(): Promise<ReadOnceResult> {
  const values = await readAllItems()
  const result: ReadOnceResult = {
    db: {},
    io: { i: {}, q: {} },
    dbBlocks: {},
  }

  // 解析 DB 变量
  for (const entry of addrTable) {
    if (entry.type === 'db_var') {
      const val = values[entry.tag]
      if (val !== undefined && val !== null) {
        // 动态导入的变量 ref 存的是变量名（字符串），配置文件变量 ref 是 PLCVariable 对象
        const name = typeof entry.ref === 'string' ? entry.ref : (entry.ref as PLCVariable)?.name
        if (name) {
          result.db[name] = {
            value: val as number | boolean,
            type: entry.ref && typeof entry.ref !== 'string' ? (entry.ref as PLCVariable).type : 'real',
            writable: entry.ref && typeof entry.ref !== 'string' ? !!(entry.ref as PLCVariable).writable : true,
            dbNumber: 0,
            offset: 0,
          }
        }
      }
    }
    // 解析 I/O 字节
    if (entry.type === 'i') {
      const byteNum = parseInt(entry.tag.replace('IB', ''))
      const val = values[entry.tag]
      if (val !== undefined && val !== null) result.io.i[byteNum] = val
    }
    if (entry.type === 'q') {
      const byteNum = parseInt(entry.tag.replace('QB', ''))
      const val = values[entry.tag]
      if (val !== undefined && val !== null) result.io.q[byteNum] = val
    }
    // 解析 DB 块
    if (entry.type === 'db_block') {
      const buf = values[entry.tag]
      if (buf && Buffer.isBuffer(buf)) {
        result.dbBlocks[entry.ref as string] = Array.from(buf)
      } else if (buf === null || buf === undefined) {
        result.dbBlocks[entry.ref as string] = null
      }
    }
  }

  return result
}

/** 为了兼容性保留的函数名，直接调 readIOBytes */
export async function readIOAreaRanges(
  area: 'PE' | 'PA',
  _ranges: { start: number; end: number }[],
): Promise<IOAreaData | null> {
  const result = await readIOBytes(area)
  if (!result) return null
  return { area, bytes: result }
}

// ─── 读取 DB 块 ──────────────────────────────────────────

export async function readDBRange(dbNumber: number, startOffset: number, byteCount: number): Promise<number[] | null> {
  // nodes7 只能读已注册的项，动态 DB 块需要临时注册
  const tempTag = `_tmp_db${dbNumber}_${startOffset}_${byteCount}`
  const s7addr = `DB${dbNumber},B${startOffset}.${byteCount}`

  if (!client) throw new Error('PLC 未连接')
  const c = client // 捕获非 null 引用

  return new Promise((resolve, reject) => {
    c.addItems([tempTag])
    const origCB = c.translationCB
    c.setTranslationCB((tag: string) => tag === tempTag ? s7addr : origCB(tag))

    // 等一帧让 addItems 生效
    setTimeout(() => {
      c.readAllItems((err: any, values: Record<string, any>) => {
        c.removeItems([tempTag])
        if (err) {
          reject(new Error(`DB 读取失败: ${err}`))
        } else {
          const buf = values[tempTag]
          if (buf && Buffer.isBuffer(buf)) {
            resolve(Array.from(buf))
          } else if (typeof buf === 'number') {
            resolve([buf])
          } else {
            resolve(null)
          }
        }
      })
    }, 100)
  })
}

// ─── 写入 ─────────────────────────────────────────────────

export async function writeVariable(varCfg: PLCVariable, value: number): Promise<void> {
  if (!client || !_connected) throw new Error('PLC 未连接')

  let s7addr: string
  if (varCfg.type === 'bool') {
    s7addr = `DB${varCfg.dbNumber},X${varCfg.offset}.${varCfg.bit ?? 0}`
  } else {
    const typeMap: Record<string, string> = {
      real: 'R', int: 'I', dint: 'DI',
      word: 'W', dword: 'DW', byte: 'B',
    }
    const t = typeMap[varCfg.type] || 'B'
    s7addr = `DB${varCfg.dbNumber},${t}${varCfg.offset}.1`
  }

  return new Promise((resolve, reject) => {
    client!.writeItems(s7addr, value, (err: any) => {
      if (err) reject(new Error(`写入失败: ${err}`))
      else resolve()
    })
  })
}

// ─── I/O 写入队列（串行处理，防止并发冲突） ────────────────

interface WriteJob {
  byteAddr: number
  bit: number
  value: boolean
  resolve: () => void
  reject: (err: Error) => void
}

const writeQueue: WriteJob[] = []
let writeBusy = false

async function processWriteQueue() {
  if (writeBusy || writeQueue.length === 0) return
  writeBusy = true

  const job = writeQueue.shift()!
  try {
    await doWriteIOBit(job.byteAddr, job.bit, job.value)
    job.resolve()
  } catch (err) {
    job.reject(err as Error)
  } finally {
    writeBusy = false
    processWriteQueue() // 处理下一个
  }
}

/** 实际执行写入（读当前字节 → 改位 → 写回整个字节） */
async function doWriteIOBit(byteAddr: number, bit: number, value: boolean): Promise<void> {
  if (!client || !_connected) throw new Error('PLC 未连接')
  return new Promise((resolve, reject) => {
    const tag = `_q_write_${byteAddr}`
    const s7addr = `QB${byteAddr}`
    const origCB = client!.translationCB
    client!.setTranslationCB((t: string) => t === tag ? s7addr : origCB(t))
    client!.addItems([tag])
    setTimeout(() => {
      client!.readAllItems((err: any, values: Record<string, any>) => {
        client!.removeItems([tag])
        client!.setTranslationCB(origCB)
        if (err) { reject(new Error(`读取 Q${byteAddr} 失败: ${err}`)); return }
        const currByte = (values[tag] as number) ?? 0
        const newByte = value ? (currByte | (1 << bit)) : (currByte & ~(1 << bit))
        client!.writeItems(s7addr, newByte, (writeErr: any) => {
          if (writeErr) reject(new Error(`写入 Q${byteAddr}.${bit} 失败: ${writeErr}`))
          else resolve()
        })
      })
    }, 50)
  })
}

/**
 * 写入 Q 区（输出点）的某个位（入队列，串行执行）
 */
export async function writeIOBit(byteAddr: number, bit: number, value: boolean): Promise<void> {
  return new Promise((resolve, reject) => {
    writeQueue.push({ byteAddr, bit, value, resolve, reject })
    processWriteQueue()
  })
}

/** 直接写 Q 区一个字节（前端已算好新值，无需读-改-写） */
export async function writeByte(byteAddr: number, value: number): Promise<void> {
  if (!client || !_connected) throw new Error('PLC 未连接')
  return new Promise((resolve, reject) => {
    client!.writeItems(`QB${byteAddr}`, value, (err: any) => {
      if (err) reject(new Error(`写入 QB${byteAddr} 失败: ${err}`))
      else resolve()
    })
  })
}

// ─── 动态注册/注销 DB 块（给 index.ts 用） ───────────────

export function addDBBlock(label: string, dbNumber: number, startOffset: number, byteCount: number) {
  registerDBBlock(label, dbNumber, startOffset, byteCount)
  if (client) {
    const entry = addrTable.find(a => a.type === 'db_block' && a.ref === label)
    if (entry) client.addItems([entry.tag])
  }
}

export function removeDBBlock(label: string) {
  const entry = addrTable.find(a => a.type === 'db_block' && a.ref === label)
  if (client && entry) client.removeItems([entry.tag])
  unregisterDBBlock(label)
}

// ─── 写入队列（串行处理，防止并发冲突） ──────────────────

interface RawWriteJob {
  type: 'read' | 'write' | 'rmw'
  s7addr: string
  value?: number
  bit?: number
  bitValue?: boolean
  resolve: (val: any) => void
  reject: (err: Error) => void
}

const rawQueue: RawWriteJob[] = []
let rawBusy = false

async function processRawQueue() {
  if (rawBusy || rawQueue.length === 0) return
  rawBusy = true
  const job = rawQueue.shift()!
  try {
    if (job.type === 'read') {
      const val = await doReadRaw(job.s7addr)
      job.resolve(val)
    } else if (job.type === 'write') {
      await doWriteRaw(job.s7addr, job.value!)
      job.resolve(undefined)
    } else if (job.type === 'rmw') {
      const curr = await doReadRaw(job.s7addr)
      const newVal = job.bitValue ? (curr | (1 << job.bit!)) : (curr & ~(1 << job.bit!))
      await doWriteRaw(job.s7addr, newVal)
      job.resolve(undefined)
    }
  } catch (err) {
    job.reject(err as Error)
  } finally {
    rawBusy = false
    processRawQueue()
  }
}

/** 轮询调用此函数检查是否可以安全读（队列空闲时才能读） */
export function isQueueIdle(): boolean {
  return !rawBusy && rawQueue.length === 0
}

/** 实际的读操作 */
async function doReadRaw(s7addr: string): Promise<number> {
  if (!client || !_connected) throw new Error('PLC 未连接')
  const tag = `_raw_${Date.now()}`
  return new Promise((resolve, reject) => {
    const origCB = client!.translationCB
    client!.setTranslationCB((t: string) => t === tag ? s7addr : origCB(t))
    client!.addItems([tag])
    setTimeout(() => {
      client!.readAllItems((err: any, values: Record<string, any>) => {
        client!.removeItems([tag])
        client!.setTranslationCB(origCB)
        const val = values[tag]
        if (err || val === undefined) reject(new Error(`读取 ${s7addr} 失败`))
        else resolve(Number(val))
      })
    }, 50)
  })
}

/** 实际的写操作（带 5s 超时，防止卡死队列） */
async function doWriteRaw(s7addr: string, value: number): Promise<void> {
  if (!client || !_connected) throw new Error('PLC 未连接')
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error(`写入 ${s7addr} 超时`)), 5000)
    client!.writeItems(s7addr, value, (err: any) => {
      clearTimeout(timeout)
      if (err) reject(new Error(`写入 ${s7addr} 失败: ${err}`))
      else resolve()
    })
  })
}

// ─── 外部 API（入队列，串行执行） ────────────────────────

export function readRaw(s7addr: string): Promise<number> {
  return new Promise((resolve, reject) => {
    rawQueue.push({ type: 'read', s7addr, resolve, reject })
    processRawQueue()
  })
}

export function writeRaw(s7addr: string, value: number): Promise<void> {
  return new Promise((resolve, reject) => {
    rawQueue.push({ type: 'write', s7addr, value, resolve, reject })
    processRawQueue()
  })
}

/** 读-改-写（读当前字节 → 改指定位 → 写回） */
export function modifyBit(s7addr: string, bit: number, value: boolean): Promise<void> {
  return new Promise((resolve, reject) => {
    rawQueue.push({ type: 'rmw', s7addr, bit, bitValue: value, resolve, reject })
    processRawQueue()
  })
}

// ─── 动态标签管理（供导入 DB 文件使用） ──────────────────
export function addDynamicTag(tag: string, s7addr: string, varName?: string) {
  if (!client) return
  const exists = addrTable.find(a => a.tag === tag)
  if (exists) return
  addrTable.push({ tag, s7addr, type: 'db_var', ref: varName })
  _addrMap.set(tag, s7addr)
  client.addItems([tag])
}

export function getDebugTags() {
  return {
    addrTable: addrTable.map(a => ({ tag: a.tag, s7addr: a.s7addr, type: a.type, ref: typeof a.ref })),
    addrMap: Object.fromEntries(_addrMap),
  }
}

export function removeDynamicTag(tag: string) {
  if (!client) return
  client.removeItems([tag])
  _addrMap.delete(tag)
  addrTable = addrTable.filter(a => a.tag !== tag)
}

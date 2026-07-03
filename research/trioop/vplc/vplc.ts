/**
 * 虚拟 S7-1200 PLC — 纯 Node.js 实现
 *
 * 实现 ISO-on-TCP (RFC1006) + S7 协议 Read/Write，
 * 无需任何原生依赖，兼容所有 Node.js 版本。
 *
 * 启动：pnpm dev:vplc
 * 连接：PLC IP 127.0.0.1, Rack 0, Slot 1
 */

import net from 'net'
import http from 'http'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import { parseDBFile, parseUDTFile } from '../server/dbParser.js'
import type { ParsedDBVariable, UDTMap, UDTField } from '../server/dbParser.js'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

/** 读取配置文件，不存在则创建默认 */
function loadConfig(): { port: number; host: string } {
  const cfgPath = path.resolve(__dirname, 'vplc-config.json')
  try {
    return JSON.parse(fs.readFileSync(cfgPath, 'utf-8'))
  } catch {
    const defaults = { port: 1102, host: '0.0.0.0' }
    fs.writeFileSync(cfgPath, JSON.stringify(defaults, null, 2), 'utf-8')
    return defaults
  }
}

const cfg = loadConfig()
const PORT = cfg.port

type ImportedDBRuntime = {
  key: string
  dbNumber: number
  dbName: string
  variableCount: number
  variables: ParsedDBVariable[]
  byteSize: number
  rawContent?: string
  createdAt: number
  updatedAt: number
}

type ImportedFieldMeta = {
  dbNumber: number
  name: string
  type: string
  offset: number
  bit?: number
  arrayCount?: number
  opaqueSize?: number
  comment?: string
}

const udtDefs: UDTMap = {}
const importedDBs: Record<string, ImportedDBRuntime> = {}
const importedTriggers: any[] = []

// ─── PLC 内存 ──────────────────────────────────────────────
const memory = {
  DB: {} as Record<number, Uint8Array>,
  PE: new Uint8Array(256),   // I 区
  PA: new Uint8Array(256),   // Q 区
  MK: new Uint8Array(256),   // M 区
  TM: new Uint8Array(256),   // 定时器
  CT: new Uint8Array(256),   // 计数器
}

// 初始化 DB
memory.DB[6] = new Uint8Array(64)
memory.DB[7] = new Uint8Array(100)
memory.DB[1] = new Uint8Array(64)

// ─── 模拟数据初始化 ────────────────────────────────────────
function setDB6() {
  const buf = memory.DB[6]
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength)
  dv.setFloat32(38, 0, false)    // position
  dv.setFloat32(42, 0, false)    // target
  dv.setFloat32(46, 0, false)    // speed
}

function setDB7() {
  const buf = memory.DB[7]
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength)
  dv.setUint8(0, 0b00000000)    // X0.0-X0.7: startBtn, stopBtn, running, alarm...
  dv.setFloat32(38, 25, false)  // temp
  dv.setFloat32(42, 0.5, false) // pressure
}

setDB6()
setDB7()

function typeByteSize(v: ParsedDBVariable) {
  if (v.type === 'bool') return 1
  if (v.opaqueSize) return v.opaqueSize
  if (v.type === 'byte') return 1
  if (v.type === 'int' || v.type === 'word') return 2
  if (v.type === 'dint' || v.type === 'dword' || v.type === 'real') return 4
  return 1
}

function calcImportedDbSize(variables: ParsedDBVariable[]) {
  return Math.max(1, ...variables.map(v => v.offset + typeByteSize(v) * (v.arrayCount ?? 1)))
}

function ensureDbSize(dbNumber: number, minSize: number) {
  const current = memory.DB[dbNumber]
  if (!current) {
    memory.DB[dbNumber] = new Uint8Array(minSize)
    return memory.DB[dbNumber]
  }
  if (current.length >= minSize) return current
  const next = new Uint8Array(minSize)
  next.set(current)
  memory.DB[dbNumber] = next
  return next
}

function readTypedValueFromMemory(mem: Uint8Array, v: ParsedDBVariable) {
  const offset = v.offset
  if (offset >= mem.length) return null
  if (v.type === 'bool') return !!(mem[offset] & (1 << (v.bit ?? 0)))
  if (v.arrayCount && v.arrayCount > 1) {
    return Array.from(mem.slice(offset, offset + typeByteSize(v) * v.arrayCount))
  }
  const dv = new DataView(mem.buffer, mem.byteOffset + offset)
  if (v.type === 'byte') return mem[offset] ?? 0
  if (v.type === 'int') return dv.getInt16(0, false)
  if (v.type === 'word') return dv.getUint16(0, false)
  if (v.type === 'dint') return dv.getInt32(0, false)
  if (v.type === 'dword') return dv.getUint32(0, false)
  if (v.type === 'real') return Number(dv.getFloat32(0, false).toFixed(4))
  return Array.from(mem.slice(offset, offset + typeByteSize(v))).map(b => b.toString(16).padStart(2, '0')).join(' ')
}

function writeTypedValueToMemory(mem: Uint8Array, v: ParsedDBVariable, value: number | boolean) {
  const offset = v.offset
  if (offset >= mem.length) return false
  if (v.type === 'bool') {
    const bit = v.bit ?? 0
    if (value) mem[offset] |= (1 << bit)
    else mem[offset] &= ~(1 << bit)
    return true
  }
  const dv = new DataView(mem.buffer, mem.byteOffset + offset)
  const num = Number(value)
  if (Number.isNaN(num)) return false
  if (v.type === 'byte') mem[offset] = num & 0xFF
  else if (v.type === 'int') dv.setInt16(0, Math.max(-32768, Math.min(32767, Math.trunc(num))), false)
  else if (v.type === 'word') dv.setUint16(0, Math.max(0, Math.min(65535, Math.trunc(num))), false)
  else if (v.type === 'dint') dv.setInt32(0, Math.trunc(num), false)
  else if (v.type === 'dword') dv.setUint32(0, Math.max(0, Math.trunc(num)), false)
  else if (v.type === 'real') dv.setFloat32(0, num, false)
  else return false
  return true
}

function randomValueForVar(v: ParsedDBVariable) {
  if (v.type === 'bool') return Math.random() >= 0.5
  if (v.type === 'byte') return Math.floor(Math.random() * 256)
  if (v.type === 'int') return Math.floor(Math.random() * 65536) - 32768
  if (v.type === 'word') return Math.floor(Math.random() * 65536)
  if (v.type === 'dint') return Math.floor(Math.random() * 2000001) - 1000000
  if (v.type === 'dword') return Math.floor(Math.random() * 1000000)
  if (v.type === 'real') return Number((Math.random() * 2000 - 1000).toFixed(4))
  return Math.floor(Math.random() * 256)
}

function buildImportedSnapshot() {
  const fields: Record<string, { dbNumber: number; values: Record<string, any>; fieldMeta: Record<string, ImportedFieldMeta> }> = {}
  const imported = Object.values(importedDBs).map(db => {
    const mem = ensureDbSize(db.dbNumber, db.byteSize)
    const values: Record<string, any> = {}
    const fieldMeta: Record<string, ImportedFieldMeta> = {}
    for (const v of db.variables) {
      values[v.name] = readTypedValueFromMemory(mem, v)
      fieldMeta[v.name] = {
        dbNumber: db.dbNumber,
        name: v.name,
        type: v.type,
        offset: v.offset,
        bit: v.bit,
        arrayCount: v.arrayCount,
        opaqueSize: v.opaqueSize,
        comment: v.comment,
      }
    }
    fields[db.dbName] = { dbNumber: db.dbNumber, values, fieldMeta }
    return { dbNumber: db.dbNumber, dbName: db.dbName, fieldCount: db.variableCount }
  })
  return { fields, imported }
}

// ─── 模拟数据变化 ──────────────────────────────────────────
function simulate() {
  const now = Date.now()
  const db7 = memory.DB[7]
  const dv7 = new DataView(db7.buffer, db7.byteOffset, db7.byteLength)

  // 温度、压力波动
  dv7.setFloat32(38, 25 + Math.sin(now / 3000) * 3 + Math.random() * 0.5, false)
  dv7.setFloat32(42, 0.5 + Math.sin(now / 5000) * 0.2 + Math.random() * 0.05, false)

  // 位置波动
  const db6 = memory.DB[6]
  const dv6 = new DataView(db6.buffer, db6.byteOffset, db6.byteLength)
  dv6.setFloat32(38, Math.max(0, Math.min(100, (Math.sin(now / 2000) + 1) * 50)), false)

  // I0.0-I0.3 间歇变化（不同频率模拟传感器信号）
  memory.PE[0] = (Math.floor(now / 800) % 2) * 0x01    // I0.0: 0.8s
                | (Math.floor(now / 1500) % 2) * 0x02   // I0.1: 1.5s
                | (Math.floor(now / 2200) % 2) * 0x04   // I0.2: 2.2s
                | (Math.floor(now / 3000) % 2) * 0x08   // I0.3: 3.0s

  // Q8 点位的模拟（如果 Q8.2=1 表示运行，则 Q8.3 周期性变化）
  const qb8 = memory.PA[8]
  if (qb8 & 0b00000100) {
    const cycle = Math.floor(now / 1200) % 4
    memory.PE[8] = (memory.PE[8] & 0xF0) | (cycle === 0 || cycle === 2 ? 0x08 : 0x00)
  }
}

// ─── S7 协议 ───────────────────────────────────────────────
// ISO-on-TCP (RFC1006): TPKT(4) + COTP(可变) + S7(可变)

/** 发送 TPKT + COTP + S7 响应 */
function sendS7(sock: net.Socket, s7payload: Buffer) {
  // COTP DT (Data Transfer) — 带最后数据单元标志
  const cotp = Buffer.alloc(3)
  cotp[0] = 0x02    // LI
  cotp[1] = 0xF0    // DT code
  cotp[2] = 0x80    // Last data unit flag (bit 7)

  const tpktLen = 4 + cotp.length + s7payload.length
  const tpkt = Buffer.alloc(4)
  tpkt[0] = 0x03    // 版本
  tpkt[1] = 0x00
  tpkt.writeUInt16BE(tpktLen, 2)

  sock.write(Buffer.concat([tpkt, cotp, s7payload]))
}

/** 构建 S7 Read 响应 */
function s7ReadResponse(pduRef: number, resultData: Buffer) {
  const paramLen = 2
  const padding = 2  // 填充使 data 对齐到 TCP 字节 21（= S7 偏移 14，预留 2 字节填充）
  const dataLen = resultData.length
  const header = Buffer.alloc(12 + paramLen + padding + dataLen)

  // S7 Header
  header[0] = 0x32          // Protocol ID
  header[1] = 0x03          // Message Type: ACK-Data (nodes7 要求 ROSCTR=0x03)
  header[2] = 0x00          // Reserved
  header[3] = 0x00          // Reserved
  header.writeUInt16BE(pduRef, 4)  // 回显请求的 PDU Ref
  header[6] = 0x00
  header[7] = paramLen + padding  // Param length = 4（nodes7 按此偏移读 data）
  header[8] = dataLen >> 8  // Data length high
  header[9] = dataLen & 0xFF// Data length low

  // S7 Parameter: Read ACK (2 字节 + 2 填充)
  header[10] = 0xFF         // 功能返回码
  header[11] = 0x00         // Reserved
  // 填充字节 [12-13] 自动为 0

  // S7 Data: Returned items
  resultData.copy(header, 14)

  return header
}

/** 构建 S7 Write 响应 */
function s7WriteResponse(pduRef: number, dataLen = 0) {
  const dataByteLen = dataLen > 0 ? dataLen : 1
  const buf = Buffer.alloc(14 + dataByteLen)  // header(10) + params(4) + data
  buf[0] = 0x32
  buf[1] = 0x03         // Message Type: ACK-Data
  buf[2] = 0x00
  buf[3] = 0x00
  buf.writeUInt16BE(pduRef, 4)  // 回显请求的 PDU Ref
  buf[6] = 0x00
  buf[7] = 0x04             // Param length = 4（nodes7 用固定 dataPointer=21）
  buf[8] = dataByteLen >> 8 // Data length high
  buf[9] = dataByteLen & 0xFF // Data length low

  // S7 Parameter: Write Response (4 字节)
  buf[10] = 0x00            // 功能返回码
  buf[11] = 0x00            // Reserved
  buf[12] = 0x00            // Reserved
  buf[13] = 0x00            // Reserved

  // 数据区：每个写入项返回 0xFF (成功)
  for (let i = 0; i < dataByteLen; i++) buf[14 + i] = 0xFF

  return buf
}

/** 构建 S7 默认响应（未知功能码） */
function s7DefaultResponse(req: Buffer) {
  // req 是 s7Req（包含 0x80 前缀），我们需要正确复制 PDU Ref
  const s7Off = req[0] === 0x80 ? 1 : 0
  const pduRef = req.readUInt16BE(s7Off + 4)
  const buf = Buffer.alloc(12)
  buf[0] = 0x32
  buf[1] = 0x03       // ACK-Data (nodes7 要求 ROSCTR=0x03)
  buf[2] = 0x00
  buf[3] = 0x00
  buf.writeUInt16BE(pduRef, 4)
  buf[6] = 0x00
  buf[7] = 0x02       // Param length = 2
  buf[8] = 0x00
  buf[9] = 0x00       // Data length = 0
  buf[10] = 0xFF
  buf[11] = 0x00
  return buf
}

/** 解析 S7 地址并读取 */
function s7ReadArea(area: number, dbNum: number, byteAddr: number, bit: number, count: number, transportSize: number): Buffer | null {
  let mem: Uint8Array | undefined

  // S7 区域码标准: 0x81=I(输入), 0x82=Q(输出), 0x83=M, 0x84=DB
  if (area === 0x81) mem = memory.PE       // I 区 / 外设输入
  else if (area === 0x82) mem = memory.PA  // Q 区 / 外设输出
  else if (area === 0x83) mem = memory.MK  // M 区
  else if (area === 0x84) {                // DB
    mem = memory.DB[dbNum]
    if (!mem) mem = new Uint8Array(count + byteAddr)  // 不存在就动态创建
  }
  else if (area === 0x85) mem = memory.CT  // 计数器
  else if (area === 0x87) mem = memory.TM  // 定时器
  else return null

  // nodes7 用 readTransportCode=0x04，长度字段为位数，且传输码也要匹配 0x04
  const responseTransportCode = transportSize === 0x03 ? 0x03 : 0x04
  const lengthValue = responseTransportCode === 0x04 ? count * 8 : count  // 0x04 → 位数
  // S7 协议要求每个 item 数据区对齐到偶数边界，奇数时补 1 字节填充
  const dataLen = count
  const paddedLen = dataLen + (dataLen % 2)  // 对齐到偶数
  const buf = Buffer.alloc(4 + paddedLen)
  // Return item header
  buf[0] = 0xFF      // Return code: OK
  buf[1] = responseTransportCode
  buf[2] = lengthValue >> 8
  buf[3] = lengthValue & 0xFF

  if (transportSize === 0x03) {
    // BIT: 读单个位
    const byteVal = mem[byteAddr] ?? 0
    buf[4] = (byteVal >> bit) & 1
  } else {
    for (let i = 0; i < dataLen; i++) {
      buf[4 + i] = mem[byteAddr + i] ?? 0
    }
    // 填充字节（对齐到偶数）自动为 0
  }

  return buf
}

/** 解析 S7 地址并写入 */
function s7WriteArea(area: number, dbNum: number, byteAddr: number, bit: number, data: Buffer): boolean {
  let mem: Uint8Array | undefined

  // S7 区域码标准: 0x81=I(输入), 0x82=Q(输出), 0x83=M, 0x84=DB
  if (area === 0x81) mem = memory.PE       // I 区
  else if (area === 0x82) mem = memory.PA  // Q 区
  else if (area === 0x83) mem = memory.MK
  else if (area === 0x84) mem = memory.DB[dbNum]
  else return false

  if (!mem) return false
  if (byteAddr + data.length > mem.length) return false

  for (let i = 0; i < data.length; i++) {
    mem[byteAddr + i] = data[i]
  }
  return true
}

/** 解析 COTP Connection Request，回复 Connection Response */
function handleCOTPConnect(sock: net.Socket, tpktPayload: Buffer): boolean {
  if (tpktPayload.length < 7) return false
  // COTP CR (Connection Request) — tpktPayload 已去掉 TPKT 头
  // 结构: [0]=LI, [1]=PDU_code(CR=0xE0), [2-3]=DST-REF, [4-5]=SRC-REF, [6]=Class, [7+]=params
  if (tpktPayload[1] !== 0xE0) return false

  // 从 CR 参数中提取 TSAP
  // nodes7 发: C1 02 01 00 (SRC-TSAP), C2 02 01 02 (DST-TSAP)
  let srcTSAP: Buffer, dstTSAP: Buffer
  const params = tpktPayload.subarray(7)
  const c1Off = params.indexOf(0xC1)
  const c2Off = params.indexOf(0xC2)
  if (c1Off >= 0 && c1Off + 3 < params.length) srcTSAP = params.subarray(c1Off + 2, c1Off + 4)
  else srcTSAP = Buffer.from([0x01, 0x00])

  if (c2Off >= 0 && c2Off + 3 < params.length) dstTSAP = params.subarray(c2Off + 2, c2Off + 4)
  else dstTSAP = Buffer.from([0x01, 0x02])

  // 构建 ISO-on-TCP CC (Connection Confirm) — 完整响应以通过 nodes7 校验:
  //   onISOConnectReply 检查:
  //     data[5] === 0xD0       (CC code)
  //     data[4] === data.length - 5  (LI 正确)
  //     data.readInt16BE(2) === data.length  (TPKT 长度一致)
  const cc = Buffer.alloc(18)
  cc[0] = 0xD0               // CC code
  cc[1] = 0x00               // DST-REF (echo, 2 bytes)
  cc[2] = 0x00
  cc[3] = 0x00               // SRC-REF (echo, 2 bytes)
  cc[4] = 0x00
  cc[5] = 0x00               // Class
  cc[6] = 0xC0               // TPDU-size = 1024
  cc[7] = 0x01
  cc[8] = 0x0A
  // 交换 TSAP (标准 IBM 实现)
  cc[9]  = 0xC1              // SRC-TSAP ← echo CR 的 DST-TSAP
  cc[10] = 0x02
  cc[11] = dstTSAP[0]
  cc[12] = dstTSAP[1]
  cc[13] = 0xC2              // DST-TSAP ← echo CR 的 SRC-TSAP
  cc[14] = 0x02
  cc[15] = srcTSAP[0]
  cc[16] = srcTSAP[1]

  const totalLen = 4 + 1 + cc.length  // 23 — wait, 4+1+18 = 23

  // 仔细对齐节点:
  // 原始数据(tcp): [TPKT(4)] [LI(1)] [CC(1)] [DST-REF(2)] [SRC-REF(2)] [Class(1)] [TPDU-size(3)] [SRC-TSAP(4)] [DST-TSAP(4)]
  // = [0-3] [4] [5] [6-7] [8-9] [10] [11-13] [14-17] [18-21]
  // 总共 = 22 bytes, LI = total-4-1 = 17 = 0x11

  // 重写为简洁正确的方式:
  const resp = Buffer.alloc(22)
  // TPKT 头
  resp[0] = 0x03
  resp[1] = 0x00
  resp.writeUInt16BE(22, 2)     // TPKT 总长度
  // COTP 固定部分
  resp[4] = 0x11                 // LI = 17
  resp[5] = 0xD0                 // CC
  resp[6] = 0x00                 // DST-REF (2字节)
  resp[7] = 0x00
  resp[8] = 0x00                 // SRC-REF (2字节)
  resp[9] = 0x00
  resp[10] = 0x00                // Class
  // 参数
  resp[11] = 0xC0                // TPDU-size = 1024
  resp[12] = 0x01
  resp[13] = 0x0A
  resp[14] = 0xC1                // SRC-TSAP
  resp[15] = 0x02
  resp[16] = dstTSAP[0]
  resp[17] = dstTSAP[1]
  resp[18] = 0xC2                // DST-TSAP
  resp[19] = 0x02
  resp[20] = srcTSAP[0]
  resp[21] = srcTSAP[1]

  sock.write(resp)
  return true
}

// ─── TCP 服务 ──────────────────────────────────────────────
const server = net.createServer((sock) => {
  let cotpConnected = false

  sock.on('data', (data) => {
    try {
      if (data.length < 4) return
      const tpktLen = data.readUInt16BE(2)
      const payload = data.subarray(4, tpktLen)

      if (!cotpConnected) {
        if (handleCOTPConnect(sock, payload)) {
          cotpConnected = true
        }
        return
      }

      // COPT DT 帧
      let s7Req = payload
      if (payload[0] === 0x02 && payload[1] === 0xF0) {
        s7Req = payload.subarray(2)
      }

      if (s7Req.length < 12) return

      // nodes7 的 S7 PDU 开头有一个 0x80 前缀字节
      const s7Off = s7Req[0] === 0x80 ? 1 : 0

      const rosctr = s7Req[s7Off + 1]
      if (rosctr !== 0x01) return

      const pduRef = s7Req.readUInt16BE(s7Off + 4)
      const paramLen = (s7Req[s7Off + 6] << 8) | s7Req[s7Off + 7]
      const dataLen = (s7Req[s7Off + 8] << 8) | s7Req[s7Off + 9]
      const params = s7Req.subarray(s7Off + 10, s7Off + 10 + paramLen)
      const dataSection = dataLen > 0 ? s7Req.subarray(s7Off + 10 + paramLen, s7Off + 10 + paramLen + dataLen) : Buffer.alloc(0)

      const funcCode = params[0]

      if (funcCode === 0xF0) {
        // S7 PDU 协商 (Setup Communication)
        const resp = Buffer.alloc(20)
        resp[0] = 0x32; resp[1] = 0x03; resp[2] = 0x00; resp[3] = 0x00
        resp.writeUInt16BE(pduRef, 4)
        resp[6] = 0x00; resp[7] = 0x08   // Param length = 8
        resp[8] = 0x00; resp[9] = 0x00   // Data length = 0
        resp[10] = 0xF0; resp[11] = 0x00 // Funct, Reserved
        resp[12] = 0x00; resp[13] = 0x01 // data[19]: MaxAmplifier
        resp[14] = 0x00; resp[15] = 0x01 // data[21]: MaxAmplifier(dup)
        resp[16] = 0x00; resp[17] = 0x01 // data[23]: MaxAmplifier(dup)
        resp[18] = 0x01; resp[19] = 0xE0 // data[25]: MaxPDU = 480
        sendS7(sock, resp)
      }
      else if (funcCode === 0x04) {
        // S7 Read — nodes7 用 12 字节项，不是 10
        const itemCount = params[1]
        const results: Buffer[] = []

        for (let i = 0; i < itemCount; i++) {
          const off = 2 + i * 12
          if (off + 12 > params.length) break

          const transportSize = params[off + 3]
          const count = (params[off + 4] << 8) | params[off + 5]
          const dbNum = (params[off + 6] << 8) | params[off + 7]
          const area = params[off + 8]
          const byteAddr = area === 0x84
            ? (params[off + 9] << 8) | params[off + 10]   // DB
            : (params[off + 10] << 8) | params[off + 11]  // I/Q/M

          if (transportSize === 0x03) {
            const bit = byteAddr & 0x07
            const byteOff = (byteAddr >> 3) & 0xFFFF
            const r = s7ReadArea(area, dbNum, byteOff, bit, 1, 0x03)
            if (r) results.push(r)
          } else {
            const r = s7ReadArea(area, dbNum, byteAddr, 0, count, transportSize)
            if (r) results.push(r)
          }
        }

        const respData = Buffer.concat(results)
        const resp = s7ReadResponse(pduRef, respData)
        if (results.length > 0) {
          const firstItem = results[0]
          console.log(`[vPLC] RESP item0: ${firstItem.toString('hex')} (len=${firstItem.length})`)
        }
        sendS7(sock, resp)
      }
      else if (funcCode === 0x05) {
        // S7 Write
        const itemCount = params[1]
        let dataOff = 0
        for (let i = 0; i < itemCount; i++) {
          const off = 2 + i * 12
          if (off + 12 > params.length) break

          const transportSize = params[off + 3]
          const count = (params[off + 4] << 8) | params[off + 5]
          const dbNum = (params[off + 6] << 8) | params[off + 7]
          const area = params[off + 8]
          const byteAddr = area === 0x84
            ? (params[off + 9] << 8) | params[off + 10]   // DB
            : (params[off + 10] << 8) | params[off + 11]  // I/Q/M

          if (transportSize === 0x03) {
            const bit = byteAddr & 0x07
            const byteOff = (byteAddr >> 3) & 0xFFFF
            const val = dataSection[dataOff] ?? 0
            const mem = area === 0x84 ? memory.DB[dbNum]
              : area === 0x81 ? memory.PE
              : area === 0x82 ? memory.PA
              : area === 0x83 ? memory.MK : undefined
            if (mem && byteOff < mem.length) {
              if (val) mem[byteOff] |= (1 << bit)
              else mem[byteOff] &= ~(1 << bit)
            }
            dataOff += 1
          } else {
            const byteLen = transportSize === 0x04 ? count
              : transportSize === 0x05 ? count * 2
              : transportSize === 0x06 ? count * 4
              : transportSize === 0x07 ? count * 8
              : count
            const writeData = dataSection.subarray(dataOff, dataOff + byteLen)
            s7WriteArea(area, dbNum, byteAddr, 0, writeData)
            dataOff += byteLen
          }
        }

        sendS7(sock, s7WriteResponse(pduRef, itemCount))
      }
      else {
        sendS7(sock, s7DefaultResponse(s7Req))
      }
    } catch (err) {
      // S7 处理异常不崩溃
    }
  })

  sock.on('error', () => {})
  sock.on('close', () => { cotpConnected = false })
})

function startS7Server(port: number) {
  server.listen(port, cfg.host)
  server.on('error', (err: any) => {
    if (err.code === 'EACCES') {
      console.error('')
      console.error('╔══════════════════════════════════════════════════╗')
      console.error(`║  权限不足！端口 ${port} 需要管理员权限或被系统保留。    ║`)
      console.error('║                                               ║')
      console.error('║  修改 server/vplc-config.json 改用其他端口:     ║')
      console.error(`║    { "port": ${port + 1} }                           ║`)
      console.error('╚══════════════════════════════════════════════════╝')
      process.exit(1)
    }
    if (err.code === 'EADDRINUSE' && port < 65535) {
      console.log(`[vPLC] S7 端口 ${port} 被占用，尝试 ${port + 1}...`)
      startS7Server(port + 1)
    } else {
      console.error('[vPLC] S7 服务器启动失败:', err.message)
      process.exit(1)
    }
  })
}
startS7Server(PORT)
const s7PortRef = { current: PORT }

// ─── Web API（HTTP 服务，供 React 前端使用） ──────────────
const WEB_PORT = PORT + 1  // S7端口+1

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

  // 添加解析后的可读值
  const db6 = memory.DB[6]; const dv6 = db6 ? new DataView(db6.buffer, db6.byteOffset, db6.byteLength) : null
  const db7 = memory.DB[7]; const dv7 = db7 ? new DataView(db7.buffer, db7.byteOffset, db7.byteLength) : null
  snap._parsed = {
    DB6: dv6 ? {
      position: dv6.getFloat32(38, false).toFixed(2),
      target: dv6.getFloat32(42, false).toFixed(2),
      speed: dv6.getFloat32(46, false).toFixed(2),
    } : {},
    DB7: dv7 ? {
      startBtn: !!(dv7.getUint8(0) & 0x01),
      stopBtn: !!(dv7.getUint8(0) & 0x02),
      running: !!(dv7.getUint8(0) & 0x04),
      alarm: !!(dv7.getUint8(0) & 0x08),
      sensorA: !!(memory.PE[8] & 0x08),
      sensorB: !!(memory.PE[8] & 0x04),
      valve: !!(memory.PA[8] & 0x20),
      temp: dv7.getFloat32(38, false).toFixed(2),
      pressure: dv7.getFloat32(42, false).toFixed(2),
    } : {},
    Q: {
      QB8: memory.PA[8],
      bits: Array.from({length:8}, (_, i) => !!(memory.PA[8] & (1 << i))),
    },
  }
  return snap
}

function readBody(req: http.IncomingMessage): Promise<Buffer> {
  return new Promise(resolve => {
    const chunks: Buffer[] = []
    req.on('data', (c: Buffer) => chunks.push(c))
    req.on('end', () => resolve(Buffer.concat(chunks)))
  })
}

const webServer = http.createServer(async (req, res) => {
  const writeJson = (code: number, data: any) => {
    res.writeHead(code, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' })
    res.end(JSON.stringify(data))
  }

  if (req.url === '/api/vplc' && req.method === 'GET') {
    writeJson(200, memorySnapshot())
    return
  }

  if (req.url === '/api/vplc/write' && req.method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const { area, dbNumber, offset, type, value, bit } = body
    console.log(`[vPLC] HTTP write:`, body)
    if (type === 'bit' && bit !== undefined) {
      const mem = area === 'I' ? memory.PE : area === 'Q' ? memory.PA : area === 'M' ? memory.MK : null
      if (mem && offset < mem.length) {
        if (value) mem[offset] |= (1 << bit)
        else mem[offset] &= ~(1 << bit)
      }
    } else {
      const mem = area === 'I' ? memory.PE : area === 'Q' ? memory.PA : area === 'M' ? memory.MK : area === 'DB' ? memory.DB[dbNumber] : null
      if (mem && offset >= 0) {
        if (type === 'byte') mem[offset] = value & 0xFF
        else if (type === 'real' && offset + 4 <= mem.length) {
          const dv = new DataView(mem.buffer, mem.byteOffset + offset, 4)
          dv.setFloat32(0, value, false)
        }
      }
    }
    writeJson(200, { success: true })
    return
  }

  if (req.url === '/api/vplc/toggle-bit' && req.method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const { area, offset, bit } = body
    const mem = area === 'I' ? memory.PE : area === 'Q' ? memory.PA : area === 'M' ? memory.MK : null
    if (mem && offset < mem.length) {
      mem[offset] ^= (1 << bit)
    }
    writeJson(200, { success: true })
    return
  }

  if (req.url === '/api/vplc/create-db' && req.method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const { dbNumber, size } = body
    if (!memory.DB[dbNumber]) {
      memory.DB[dbNumber] = new Uint8Array(size || 64)
    }
    writeJson(200, { success: true })
    return
  }

  if (req.url === '/api/vplc/import-udt' && req.method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const content = body.content || ''
    if (!content) { writeJson(400, { error: '请提供 UDT 文件内容' }); return }
    try {
      const parsed = parseUDTFile(content)
      Object.assign(udtDefs, parsed)
      const names = Object.keys(parsed)
      writeJson(200, { success: true, count: names.length, names })
    } catch (err) {
      writeJson(400, { error: `UDT 解析失败: ${(err as Error).message}` })
    }
    return
  }

  if (req.url === '/api/vplc/imported-udts' && req.method === 'GET') {
    writeJson(200, Object.keys(udtDefs))
    return
  }

  if (req.url?.startsWith('/api/vplc/imported-udts/') && req.method === 'GET') {
    const name = decodeURIComponent(req.url.split('/').pop() || '')
    const fields = udtDefs[name]
    if (!fields) { writeJson(404, { error: '未找到 UDT' }); return }
    writeJson(200, { name, fields })
    return
  }

  if (req.url?.startsWith('/api/vplc/imported-udts/') && req.method === 'DELETE') {
    const name = decodeURIComponent(req.url.split('/').pop() || '')
    delete udtDefs[name]
    writeJson(200, { success: true })
    return
  }

  if (req.url === '/api/vplc/import-db' && req.method === 'POST') {
    const body = JSON.parse((await readBody(req)).toString())
    const content = body.content || ''
    const dbNumberOverride = body.dbNumber ? Number(body.dbNumber) : undefined
    if (!content) { writeJson(400, { error: '请提供 DB 文件内容' }); return }
    try {
      const parsed = parseDBFile(content, dbNumberOverride, udtDefs)
      if (parsed.optimized) { writeJson(400, { error: `DB\"${parsed.dbName}\" 开启了优化块访问，无法通过绝对地址读取` }); return }
      const byteSize = calcImportedDbSize(parsed.variables)
      ensureDbSize(parsed.dbNumber, byteSize)
      const key = `${parsed.dbNumber}_${parsed.dbName}`
      const now = Date.now()
      importedDBs[key] = {
        key,
        dbNumber: parsed.dbNumber,
        dbName: parsed.dbName,
        variableCount: parsed.variables.length,
        variables: parsed.variables,
        byteSize,
        rawContent: content,
        createdAt: importedDBs[key]?.createdAt || now,
        updatedAt: now,
      }
      writeJson(200, { success: true, dbNumber: parsed.dbNumber, dbName: parsed.dbName, variableCount: parsed.variables.length, variables: parsed.variables })
    } catch (err) {
      writeJson(400, { error: `解析失败: ${(err as Error).message}` })
    }
    return
  }

  if (req.url === '/api/vplc/imported-dbs' && req.method === 'GET') {
    writeJson(200, Object.values(importedDBs).map(db => ({ dbNumber: db.dbNumber, dbName: db.dbName, variableCount: db.variableCount, variables: db.variables })))
    return
  }

  if (req.url?.startsWith('/api/vplc/imported-dbs/') && req.method === 'DELETE') {
    const key = decodeURIComponent(req.url.split('/').pop() || '')
    delete importedDBs[key]
    writeJson(200, { success: true })
    return
  }

  if (req.url?.endsWith('/refresh') && req.method === 'POST') {
    const parts = req.url.split('/')
    const key = decodeURIComponent(parts[parts.length - 2] || '')
    const db = importedDBs[key]
    if (!db) { writeJson(404, { error: '未找到 DB' }); return }
    ensureDbSize(db.dbNumber, db.byteSize)
    if (db.rawContent) {
      const parsed = parseDBFile(db.rawContent, db.dbNumber, udtDefs)
      db.variables = parsed.variables
      db.variableCount = parsed.variables.length
      db.byteSize = calcImportedDbSize(parsed.variables)
      db.updatedAt = Date.now()
      ensureDbSize(db.dbNumber, db.byteSize)
    }
    writeJson(200, { success: true, registered: db.variableCount })
    return
  }

  if (req.url?.endsWith('/write') && req.method === 'POST' && req.url.includes('/api/vplc/imported-dbs/')) {
    const parts = req.url.split('/')
    const key = decodeURIComponent(parts[parts.length - 2] || '')
    const db = importedDBs[key]
    if (!db) { writeJson(404, { error: '未找到 DB' }); return }
    const body = JSON.parse((await readBody(req)).toString())
    const { fieldName, value } = body
    const variable = db.variables.find(v => v.name === fieldName)
    if (!variable) { writeJson(404, { error: '未找到字段' }); return }
    const mem = ensureDbSize(db.dbNumber, db.byteSize)
    if (!writeTypedValueToMemory(mem, variable, value)) { writeJson(400, { error: '写入失败' }); return }
    writeJson(200, { success: true })
    return
  }

  if (req.url?.endsWith('/randomize') && req.method === 'POST') {
    const parts = req.url.split('/')
    const key = decodeURIComponent(parts[parts.length - 2] || '')
    const db = importedDBs[key]
    if (!db) { writeJson(404, { error: '未找到 DB' }); return }
    const body = JSON.parse((await readBody(req)).toString())
    const { fieldName } = body
    const variable = db.variables.find(v => v.name === fieldName)
    if (!variable) { writeJson(404, { error: '未找到字段' }); return }
    const mem = ensureDbSize(db.dbNumber, db.byteSize)
    const value = randomValueForVar(variable)
    if (!writeTypedValueToMemory(mem, variable, value)) { writeJson(400, { error: '随机写入失败' }); return }
    writeJson(200, { success: true, value, type: variable.type })
    return
  }

  // Trigger stubs（简化版，让前端触发器 Tab 不报错）
  if (req.url === '/api/vplc/triggers' && req.method === 'GET') {
    writeJson(200, [])
    return
  }
  if (req.url === '/api/vplc/triggers' && req.method === 'POST') {
    writeJson(200, { id: Date.now().toString(), active: false })
    return
  }
  if (req.url?.startsWith('/api/vplc/triggers/') && req.method === 'DELETE') {
    writeJson(200, { success: true })
    return
  }

  // CORS preflight
  if (req.method === 'OPTIONS') {
    res.writeHead(204, { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Methods': 'GET,POST,DELETE', 'Access-Control-Allow-Headers': 'Content-Type' })
    res.end()
    return
  }

  // 非 API 路径：返回简单提示（无内联 HTML）
  writeJson(404, { error: 'Not Found', api: '/api/vplc' })
})

function startWebServer(port: number) {
  webServer.listen(port, cfg.host)
  webServer.on('error', (err: any) => {
    if (err.code === 'EADDRINUSE' && port < PORT + 100) {
      startWebServer(port + 1)
    } else {
      console.error(`[vPLC] Web 服务器启动失败 (${port}): ${err.message}`)
    }
  })
}
startWebServer(WEB_PORT)
const webPortRef = { current: WEB_PORT }

// 等两个服务器都就绪后再打完整的启动横幅
let s7Ready = false, webReady = false
function printFinalBanner() {
  if (!s7Ready || !webReady) return
  console.log('')
  console.log('╔══════════════════════════════════════════════╗')
  console.log('║    虚拟 S7-1200 PLC 已启动                   ║')
  console.log(`║    S7:  127.0.0.1:${s7PortRef.current}                   ║`)
  console.log(`║    API: http://localhost:${webPortRef.current}/api/vplc   ║`)
  console.log('║                                              ║')
  console.log('║    上位机连接:                               ║')
  console.log('║      IP: 127.0.0.1  Rack:0  Slot:1          ║')
  console.log('║      Port: ' + s7PortRef.current + '                        ║')
  console.log('║                                              ║')
  console.log('║    模拟区域:  DB1/DB6/DB7  I区 Q区 M区      ║')
  console.log('║    模拟值自动变化: 温度/压力/位置             ║')
  console.log('╚══════════════════════════════════════════════╝')
  console.log('')
}
server.on('listening', () => { s7PortRef.current = server.address()?.port || PORT; s7Ready = true; printFinalBanner() })
webServer.on('listening', () => { webReady = true; printFinalBanner() })

// 模拟定时器
setInterval(simulate, 500)

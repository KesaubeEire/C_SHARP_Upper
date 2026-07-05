/**
 * S7 协议层 — ISO-on-TCP (RFC1006) + S7 通信
 *
 * 功能码：
 *   0xF0 — Setup Communication (PDU 协商)
 *   0x04 — Read
 *   0x05 — Write
 *   0x11 — Read SZL (系统信息)
 *   0x19 — Read Time-of-Day (RTC)
 *   0x1A — Request Diagnostics
 *   0x2D — Protection (密码/访问级别)
 *
 * 新增功能码在 handleS7Function 中添加
 */

import net from 'net'
import { memory, markMemDirty } from './plc-memory.js'
import { isRunning, getRtcMs, addDiag, getDiagBuffer } from './plc-state.js'

// ─── S7 协议常亮 ──

/** S7 区域码 */
const AREA_PE = 0x81  // I / 外设输入
const AREA_PA = 0x82  // Q / 外设输出
const AREA_MK = 0x83  // M 区
const AREA_DB = 0x84  // DB
const AREA_CT = 0x85  // 计数器
const AREA_TM = 0x87  // 定时器

// ─── 发送 S7 响应 ──

/** 发送 TPKT + COTP + S7 响应 */
export function sendS7(sock: net.Socket, s7payload: Buffer) {
  const cotp = Buffer.alloc(3)
  cotp[0] = 0x02    // LI
  cotp[1] = 0xF0    // DT code
  cotp[2] = 0x80    // Last data unit flag

  const tpktLen = 4 + cotp.length + s7payload.length
  const tpkt = Buffer.alloc(4)
  tpkt[0] = 0x03
  tpkt[1] = 0x00
  tpkt.writeUInt16BE(tpktLen, 2)

  sock.write(Buffer.concat([tpkt, cotp, s7payload]))
}

// ─── S7 响应帧构建 ──

/** 构建 S7 Read 响应 */
export function s7ReadResponse(pduRef: number, resultData: Buffer): Buffer {
  const paramLen = 2
  const padding = 2
  const dataLen = resultData.length
  const header = Buffer.alloc(12 + paramLen + padding + dataLen)

  header[0] = 0x32          // Protocol ID
  header[1] = 0x03          // Message Type: ACK-Data
  header[2] = 0x00
  header[3] = 0x00
  header.writeUInt16BE(pduRef, 4)
  header[6] = 0x00
  header[7] = paramLen + padding
  header[8] = dataLen >> 8
  header[9] = dataLen & 0xFF

  header[10] = 0xFF         // 功能返回码
  header[11] = 0x00
  // padding [12-13]

  resultData.copy(header, 14)
  return header
}

/** 构建 S7 Write 响应 */
export function s7WriteResponse(pduRef: number, dataLen = 0): Buffer {
  const dataByteLen = dataLen > 0 ? dataLen : 1
  const buf = Buffer.alloc(14 + dataByteLen)
  buf[0] = 0x32; buf[1] = 0x03; buf[2] = 0x00; buf[3] = 0x00
  buf.writeUInt16BE(pduRef, 4)
  buf[6] = 0x00; buf[7] = 0x02
  buf[8] = dataByteLen >> 8; buf[9] = dataByteLen & 0xFF
  buf[10] = 0xFF; buf[11] = 0x00
  for (let i = 0; i < dataByteLen; i++) buf[14 + i] = 0xFF
  return buf
}

/** 构建 S7 默认响应（未知功能码） */
export function s7DefaultResponse(req: Buffer): Buffer {
  const s7Off = req[0] === 0x80 ? 1 : 0
  const pduRef = req.readUInt16BE(s7Off + 4)
  const buf = Buffer.alloc(12)
  buf[0] = 0x32; buf[1] = 0x03; buf[2] = 0x00; buf[3] = 0x00
  buf.writeUInt16BE(pduRef, 4)
  buf[6] = 0x00; buf[7] = 0x02; buf[8] = 0x00; buf[9] = 0x00
  buf[10] = 0xFF; buf[11] = 0x00
  return buf
}

// ─── S7 读写区域 ──

/** 获取 S7 区域对应的内存缓冲区 */
function getAreaMem(area: number, dbNum: number): Uint8Array | undefined {
  switch (area) {
    case AREA_PE: return memory.PE
    case AREA_PA: return memory.PA
    case AREA_MK: return memory.MK
    case AREA_DB: {
      let mem = memory.DB[dbNum]
      if (!mem) {
        mem = new Uint8Array(256)
        memory.DB[dbNum] = mem
      }
      return mem
    }
    case AREA_CT: return memory.CT
    case AREA_TM: return memory.TM
    default: return undefined
  }
}

/** 解析 S7 地址并读取 */
export function s7ReadArea(area: number, dbNum: number, byteAddr: number, bit: number, count: number, transportSize: number): Buffer | null {
  const mem = getAreaMem(area, dbNum)
  if (!mem) return null

  const responseTransportCode = transportSize === 0x03 ? 0x03 : 0x04
  const lengthValue = responseTransportCode === 0x04 ? count * 8 : count
  const dataLen = count
  const paddedLen = dataLen + (dataLen % 2)
  const buf = Buffer.alloc(4 + paddedLen)

  buf[0] = 0xFF      // Return code: OK
  buf[1] = responseTransportCode
  buf[2] = lengthValue >> 8
  buf[3] = lengthValue & 0xFF

  if (transportSize === 0x03) {
    const byteVal = mem[byteAddr] ?? 0
    buf[4] = (byteVal >> bit) & 1
  } else {
    for (let i = 0; i < dataLen; i++) {
      buf[4 + i] = mem[byteAddr + i] ?? 0
    }
  }
  return buf
}

/** 解析 S7 地址并写入 */
export function s7WriteArea(area: number, dbNum: number, byteAddr: number, bit: number, data: Buffer): boolean {
  const mem = getAreaMem(area, dbNum)
  if (!mem) return false
  if (byteAddr + data.length > mem.length) return false

  for (let i = 0; i < data.length; i++) {
    mem[byteAddr + i] = data[i]
  }
  markMemDirty()
  return true
}

// ─── COTP 连接 ──

/** 解析 COTP Connection Request，回复 Connection Response */
export function handleCOTPConnect(sock: net.Socket, tpktPayload: Buffer): boolean {
  if (tpktPayload.length < 7) return false
  if (tpktPayload[1] !== 0xE0) return false // 必须是 CR

  const params = tpktPayload.subarray(7)
  const c1Off = params.indexOf(0xC1)
  const c2Off = params.indexOf(0xC2)
  const srcTSAP = (c1Off >= 0 && c1Off + 3 < params.length)
    ? params.subarray(c1Off + 2, c1Off + 4)
    : Buffer.from([0x01, 0x00])
  const dstTSAP = (c2Off >= 0 && c2Off + 3 < params.length)
    ? params.subarray(c2Off + 2, c2Off + 4)
    : Buffer.from([0x01, 0x02])

  const resp = Buffer.alloc(22)
  resp[0] = 0x03; resp[1] = 0x00
  resp.writeUInt16BE(22, 2)
  resp[4] = 0x11; resp[5] = 0xD0  // CC
  resp[6] = 0x00; resp[7] = 0x00   // DST-REF
  resp[8] = 0x00; resp[9] = 0x00   // SRC-REF
  resp[10] = 0x00                   // Class
  resp[11] = 0xC0; resp[12] = 0x01; resp[13] = 0x0A  // TPDU-size 1024
  resp[14] = 0xC1; resp[15] = 0x02  // SRC-TSAP
  resp[16] = dstTSAP[0]; resp[17] = dstTSAP[1]
  resp[18] = 0xC2; resp[19] = 0x02  // DST-TSAP
  resp[20] = srcTSAP[0]; resp[21] = srcTSAP[1]

  sock.write(resp)
  return true
}

// ─── S7 PDU 解析 ──

export interface S7Request {
  pduRef: number
  funcCode: number
  paramLen: number
  dataLen: number
  params: Buffer
  dataSection: Buffer
}

/** 解析 S7 PDU 请求 */
export function parseS7Request(s7Req: Buffer): S7Request | null {
  if (s7Req.length < 12) return null

  const s7Off = s7Req[0] === 0x80 ? 1 : 0
  const rosctr = s7Req[s7Off + 1]
  if (rosctr !== 0x01) return null  // 只处理 ROSCTR Request

  const pduRef = s7Req.readUInt16BE(s7Off + 4)
  const paramLen = (s7Req[s7Off + 6] << 8) | s7Req[s7Off + 7]
  const dataLen = (s7Req[s7Off + 8] << 8) | s7Req[s7Off + 9]
  const params = s7Req.subarray(s7Off + 10, s7Off + 10 + paramLen)
  const dataSection = dataLen > 0
    ? s7Req.subarray(s7Off + 10 + paramLen, s7Off + 10 + paramLen + dataLen)
    : Buffer.alloc(0)

  return { pduRef, funcCode: params[0], paramLen, dataLen, params, dataSection }
}

// ─── 功能码处理器 ──

/** S7 PDU 协商 (0xF0) */
function handleSetupComm(req: S7Request): Buffer {
  const resp = Buffer.alloc(20)
  resp[0] = 0x32; resp[1] = 0x03; resp[2] = 0x00; resp[3] = 0x00
  resp.writeUInt16BE(req.pduRef, 4)
  resp[6] = 0x00; resp[7] = 0x08
  resp[8] = 0x00; resp[9] = 0x00
  resp[10] = 0xF0; resp[11] = 0x00
  resp[12] = 0x00; resp[13] = 0x01 // MaxAmplifier
  resp[14] = 0x00; resp[15] = 0x01
  resp[16] = 0x00; resp[17] = 0x01
  resp[18] = 0x01; resp[19] = 0xE0 // MaxPDU = 480
  return resp
}

/** S7 Read (0x04) */
function handleRead(req: S7Request): Buffer {
  const itemCount = req.params[1]
  const results: Buffer[] = []

  for (let i = 0; i < itemCount; i++) {
    const off = 2 + i * 12
    if (off + 12 > req.params.length) break

    const transportSize = req.params[off + 3]
    const count = (req.params[off + 4] << 8) | req.params[off + 5]
    const dbNum = (req.params[off + 6] << 8) | req.params[off + 7]
    const area = req.params[off + 8]
    const byteAddr = area === AREA_DB
      ? (req.params[off + 9] << 8) | req.params[off + 10]
      : (req.params[off + 10] << 8) | req.params[off + 11]

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
  return s7ReadResponse(req.pduRef, respData)
}

/** S7 Write (0x05) */
function handleWrite(req: S7Request): Buffer {
  const itemCount = req.params[1]
  let dataOff = 0

  for (let i = 0; i < itemCount; i++) {
    const off = 2 + i * 12
    if (off + 12 > req.params.length) break

    const transportSize = req.params[off + 3]
    const count = (req.params[off + 4] << 8) | req.params[off + 5]
    const dbNum = (req.params[off + 6] << 8) | req.params[off + 7]
    const area = req.params[off + 8]
    const byteAddr = area === AREA_DB
      ? (req.params[off + 9] << 8) | req.params[off + 10]
      : (req.params[off + 10] << 8) | req.params[off + 11]

    if (transportSize === 0x03) {
      const bit = byteAddr & 0x07
      const byteOff = (byteAddr >> 3) & 0xFFFF
      const val = req.dataSection[dataOff] ?? 0
      const mem = getAreaMem(area, dbNum)
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
      const writeData = req.dataSection.subarray(dataOff, dataOff + byteLen)
      s7WriteArea(area, dbNum, byteAddr, 0, writeData)
      dataOff += byteLen
    }
  }
  markMemDirty()
  return s7WriteResponse(req.pduRef, itemCount)
}

// ─── 新增功能码 ↓ ──

/**
 * S7 Read SZL (0x11) — 读取系统信息
 *
 * 返回基本的 PLC 标识信息，包括：
 * - 模块类型 (S7-1200)
 * - 固件版本 (V4.6)
 * - 序列号
 * - 基本硬件信息
 */
function handleReadSZL(req: S7Request): Buffer | null {
  const s7Off = req.params[1] // sub-function (通常为 0x01)
  const szlId = (req.params[2] << 8) | req.params[3]  // SZL ID
  const szlIndex = (req.params[4] << 8) | req.params[5] // SZL Index

  // 只实现最基本的 SZL ID: 0x0011 (模块标识)
  if (szlId === 0x0011) {
    // 模块标识: 名称(12) + 序列号(12) + 版本号(2) = 26 字节
    const dataBuf = Buffer.alloc(26)
    dataBuf.write('S7-1200', 0, 8, 'ascii')    // 模块类型
    dataBuf.write('6ES7214-1HG40-0XB0', 8, 18, 'ascii') // 订货号
    dataBuf[26 - 2] = 4   // 主版本
    dataBuf[26 - 1] = 6   // 次版本

    // SZL 响应包装
    const resp = Buffer.alloc(4 + dataBuf.length)
    resp[0] = 0xFF   // 返回码 OK
    resp[1] = 0x09   // 传输码: SZL
    resp[2] = (dataBuf.length * 8) >> 8
    resp[3] = (dataBuf.length * 8) & 0xFF
    dataBuf.copy(resp, 4)

    // 构建整体响应
    // S7 Header + param(return code + reserved) + data
    const paramLen = 2
    const padding = 2
    const header = Buffer.alloc(12 + paramLen + padding + resp.length)
    header[0] = 0x32; header[1] = 0x03
    header[2] = 0x00; header[3] = 0x00
    header.writeUInt16BE(req.pduRef, 4)
    header[6] = 0x00; header[7] = paramLen + padding
    header[8] = resp.length >> 8; header[9] = resp.length & 0xFF
    header[10] = 0xFF; header[11] = 0x00
    resp.copy(header, 14)
    return header
  }

  // 未知 SZL ID — 返回空响应
  const resp = Buffer.alloc(4)
  resp[0] = 0x05  // 无可用数据
  resp[1] = 0x00; resp[2] = 0x00; resp[3] = 0x00

  const paramLen = 2
  const padding = 2
  const header = Buffer.alloc(12 + paramLen + padding + resp.length)
  header[0] = 0x32; header[1] = 0x03
  header[2] = 0x00; header[3] = 0x00
  header.writeUInt16BE(req.pduRef, 4)
  header[6] = 0x00; header[7] = paramLen + padding
  header[8] = resp.length >> 8; header[9] = resp.length & 0xFF
  header[10] = 0xFF; header[11] = 0x00
  resp.copy(header, 14)
  return header
}

/**
 * S7 Read Time-of-Day (0x19) — 读取 PLC 日期时间
 */
function handleReadTOD(req: S7Request): Buffer {
  const now = new Date(getRtcMs())

  // BCD 编码的日期时间 (S7 DATE_AND_TIME 格式)
  // 10 字节: YY-MM-DD-HH-MM-SS-msw-lsw (BCD, 毫秒位)
  const data = Buffer.alloc(8)
  data[0] = bcd(now.getFullYear() % 100)
  data[1] = bcd(now.getMonth() + 1)
  data[2] = bcd(now.getDate())
  data[3] = bcd(now.getHours())
  data[4] = bcd(now.getMinutes())
  data[5] = bcd(now.getSeconds())
  data[6] = 0x00 // 毫秒高字节 (BCD)
  data[7] = 0x00 // 毫秒低字节

  // 包装 item
  const item = Buffer.alloc(4 + data.length)
  item[0] = 0xFF        // 返回码
  item[1] = 0x09        // 传输码
  item[2] = (data.length * 8) >> 8
  item[3] = (data.length * 8) & 0xFF
  data.copy(item, 4)

  const header = Buffer.alloc(12 + 2 + 2 + item.length)
  header[0] = 0x32; header[1] = 0x03
  header[2] = 0x00; header[3] = 0x00
  header.writeUInt16BE(req.pduRef, 4)
  header[6] = 0x00; header[7] = 0x04   // param len = 4 (2 + 2 padding)
  header[8] = item.length >> 8; header[9] = item.length & 0xFF
  header[10] = 0xFF; header[11] = 0x00
  // padding [12-13]
  item.copy(header, 14)
  return header
}

/**
 * S7 Request Diagnostics (0x1A) — 请求诊断信息
 */
function handleDiagnostics(req: S7Request): Buffer {
  // 构建诊断数据：返回诊断缓冲区前 N 条 + 状态
  const diag = getDiagBuffer()
  const statusData = Buffer.alloc(8)
  statusData[0] = isRunning() ? 0x01 : 0x00  // 运行/停止
  statusData[1] = 0x00                        // 诊断状态
  statusData[2] = Math.min(diag.length, 0xFF) // 诊断条目数
  statusData[3] = 0x00

  const item = Buffer.alloc(4 + statusData.length)
  item[0] = 0xFF
  item[1] = 0x09
  item[2] = (statusData.length * 8) >> 8
  item[3] = (statusData.length * 8) & 0xFF
  statusData.copy(item, 4)

  const header = Buffer.alloc(12 + 2 + 2 + item.length)
  header[0] = 0x32; header[1] = 0x03
  header[2] = 0x00; header[3] = 0x00
  header.writeUInt16BE(req.pduRef, 4)
  header[6] = 0x00; header[7] = 0x04
  header[8] = item.length >> 8; header[9] = item.length & 0xFF
  header[10] = 0xFF; header[11] = 0x00
  item.copy(header, 14)
  return header
}

/**
 * S7 Protection (0x2D) — 读取访问保护级别
 * 返回"无密码、完全访问"，这样客户端不会弹密码框
 */
function handleProtection(req: S7Request): Buffer {
  const resp = Buffer.alloc(12 + 2 + 2 + 4)
  resp[0] = 0x32; resp[1] = 0x03
  resp[2] = 0x00; resp[3] = 0x00
  resp.writeUInt16BE(req.pduRef, 4)
  resp[6] = 0x00; resp[7] = 0x04     // param len = 4
  resp[8] = 0x00; resp[9] = 0x04     // data len
  resp[10] = 0xFF; resp[11] = 0x00   // return code + reserved
  // data: 4 bytes
  resp[14] = 0x00    // Protection level: 0 = 无保护
  resp[15] = 0x00    // Mode: 0 = 完全访问
  resp[16] = 0x00    // Reserved
  resp[17] = 0x00    // Reserved
  return resp
}

// ─── BCD 编码工具 ──
function bcd(n: number): number {
  return ((Math.floor(n / 10) << 4) | (n % 10))
}

// ─── 主分发函数 ──

/** S7 功能码分发 */
export function handleS7Function(req: S7Request): Buffer {
  switch (req.funcCode) {
    case 0xF0: return handleSetupComm(req)
    case 0x04: return handleRead(req)
    case 0x05: return handleWrite(req)
    case 0x11: return handleReadSZL(req) ?? s7DefaultResponse(Buffer.alloc(0))
    case 0x19: return handleReadTOD(req)
    case 0x1A: return handleDiagnostics(req)
    case 0x2D: return handleProtection(req)
    default:   return s7DefaultResponse(Buffer.alloc(0))
  }
}

// ─── COTP 数据入口 ──

/**
 * 处理 COTP 帧数据（在 COTP 连接建立后调用）
 * payload 已经是去掉 TPKT 头的 COTP DT 数据
 */
export function handleCOTPData(sock: net.Socket, payload: Buffer) {
  let s7Req = payload
  if (payload[0] === 0x02 && payload[1] === 0xF0) {
    s7Req = payload.subarray(2)
  }

  const parsed = parseS7Request(s7Req)
  if (!parsed) return

  const resp = handleS7Function(parsed)
  sendS7(sock, resp)
}

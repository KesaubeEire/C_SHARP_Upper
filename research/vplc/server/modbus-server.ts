/**
 * Modbus TCP Server
 *
 * 标准 Modbus TCP 协议实现，地址映射到 vPLC 内存。
 * 默认端口 502（仿真实例回退到 5020+）
 *
 * 功能码支持：
 *   0x01 — Read Coils           → Q 区位
 *   0x02 — Read Discrete Inputs → I 区位
 *   0x03 — Read Holding Regs    → DB 区字
 *   0x04 — Read Input Regs      → M 区字
 *   0x05 — Write Single Coil    → Q 区位
 *   0x06 — Write Single Register → DB 区字
 *   0x0F — Write Multiple Coils → Q 区
 *   0x10 — Write Multiple Regs  → DB 区
 *
 * 地址映射：
 *   Modbus 地址 0x0000-0xFFFF 直接对应字节偏移，
 *   Coil/Discrete = 位操作，Register = 字(2字节)操作
 */

import net from 'net'
import { memory, ensureDbSize, markMemDirty } from './plc-memory.js'
import { addDiag } from './plc-state.js'

export interface ModbusServerOptions {
  preferredPort: number
  /** DB 块号映射（holding register → 目标 DB 号） */
  holdingRegisterDb: number
  /** 输入寄存器映射的 M 区起始偏移 */
  inputRegisterStart: number
}

const DEFAULTS: ModbusServerOptions = {
  preferredPort: 5020,
  holdingRegisterDb: 1,
  inputRegisterStart: 0,
}

// ─── 工具函数 ──

function writeUint16BE(buf: Buffer, off: number, val: number) {
  buf[off] = (val >> 8) & 0xFF
  buf[off + 1] = val & 0xFF
}

function readUint16BE(buf: Buffer, off: number): number {
  return (buf[off] << 8) | buf[off + 1]
}

// ─── Modbus TCP 帧 ──

/**
 * Modbus TCP 帧格式 (MBAP Header 7 字节):
 *   0-1: Transaction ID
 *   2-3: Protocol ID (0x0000)
 *   4-5: Length (后续字节数 = unitId + funcCode + data)
 *   6:   Unit ID
 *   7:   Function code
 *   8+:  Data
 */

/** 构建 Modbus TCP 异常响应 */
function exceptionResponse(transId: number, unitId: number, funcCode: number, exceptionCode: number): Buffer {
  const resp = Buffer.alloc(9)
  writeUint16BE(resp, 0, transId)
  writeUint16BE(resp, 2, 0x0000)
  writeUint16BE(resp, 4, 0x0003)   // 长度 = unitId(1) + func(1) + exception(1)
  resp[6] = unitId
  resp[7] = funcCode | 0x80
  resp[8] = exceptionCode
  return resp
}

/** 构建正常响应头（数据部分需后续追加）*/
function responseHeader(transId: number, unitId: number, funcCode: number, dataLen: number): Buffer {
  const resp = Buffer.alloc(8 + dataLen)
  writeUint16BE(resp, 0, transId)
  writeUint16BE(resp, 2, 0x0000)
  writeUint16BE(resp, 4, dataLen + 2)  // 长度 = unitId(1) + func(1) + data
  resp[6] = unitId
  resp[7] = funcCode
  return resp
}

// ─── 功能码处理器 ──

/**
 * 读取线圈/离散输入（按位打包）
 */
function handleReadBits(
  startAddr: number, quantity: number, mem: Uint8Array,
  transId: number, unitId: number, funcCode: number
): Buffer {
  const byteCount = Math.ceil(quantity / 8)
  const resp = responseHeader(transId, unitId, funcCode, 1 + byteCount)
  resp[8] = byteCount

  for (let i = 0; i < quantity; i++) {
    const byteAddr = startAddr + i
    const bit = byteAddr & 0x07
    const byteOff = byteAddr >> 3
    if (byteOff < mem.length && (mem[byteOff] & (1 << bit))) {
      resp[9 + Math.floor(i / 8)] |= (1 << (i % 8))
    }
  }
  return resp
}

/**
 * 写单个线圈
 */
function handleWriteSingleCoil(
  addr: number, value: number, mem: Uint8Array,
  transId: number, unitId: number
): Buffer {
  const byteAddr = addr >> 3
  const bit = addr & 0x07
  if (byteAddr < mem.length) {
    if (value !== 0) mem[byteAddr] |= (1 << bit)
    else mem[byteAddr] &= ~(1 << bit)
    markMemDirty()
  }

  // 回显请求帧
  const resp = Buffer.alloc(12)
  writeUint16BE(resp, 0, transId)
  writeUint16BE(resp, 2, 0x0000)
  writeUint16BE(resp, 4, 0x0006)   // 长度
  resp[6] = unitId
  resp[7] = 0x05
  writeUint16BE(resp, 8, addr)
  writeUint16BE(resp, 10, value !== 0 ? 0xFF00 : 0x0000)
  return resp
}

/**
 * 写多个线圈
 */
function handleWriteMultipleCoils(
  startAddr: number, quantity: number, data: Buffer, mem: Uint8Array,
  transId: number, unitId: number
): Buffer {
  for (let i = 0; i < quantity && i < data.length * 8; i++) {
    const byteAddr = (startAddr + i) >> 3
    const bit = (startAddr + i) & 0x07
    if (byteAddr < mem.length) {
      if (data[Math.floor(i / 8)] & (1 << (i % 8))) mem[byteAddr] |= (1 << bit)
      else mem[byteAddr] &= ~(1 << bit)
    }
  }
  markMemDirty()

  const resp = Buffer.alloc(12)
  writeUint16BE(resp, 0, transId)
  writeUint16BE(resp, 2, 0x0000)
  writeUint16BE(resp, 4, 0x0006)
  resp[6] = unitId
  resp[7] = 0x0F
  writeUint16BE(resp, 8, startAddr)
  writeUint16BE(resp, 10, quantity)
  return resp
}

/**
 * 读保持寄存器（DB 区, 字为单位）
 */
function handleReadHoldingRegisters(
  startAddr: number, quantity: number, mem: Uint8Array,
  transId: number, unitId: number
): Buffer {
  const byteCount = quantity * 2
  const resp = responseHeader(transId, unitId, 0x03, 1 + byteCount)
  resp[8] = byteCount

  for (let i = 0; i < quantity; i++) {
    const off = (startAddr + i) * 2
    if (off + 2 <= mem.length) {
      resp[9 + i * 2] = mem[off] ?? 0
      resp[9 + i * 2 + 1] = mem[off + 1] ?? 0
    }
  }
  return resp
}

/**
 * 读输入寄存器（M 区, 字为单位）
 */
function handleReadInputRegisters(
  startAddr: number, quantity: number, mem: Uint8Array,
  transId: number, unitId: number
): Buffer {
  const byteCount = quantity * 2
  const resp = responseHeader(transId, unitId, 0x04, 1 + byteCount)
  resp[8] = byteCount

  for (let i = 0; i < quantity; i++) {
    const off = (startAddr + i) * 2
    if (off + 2 <= mem.length) {
      resp[9 + i * 2] = mem[off] ?? 0
      resp[9 + i * 2 + 1] = mem[off + 1] ?? 0
    }
  }
  return resp
}

/**
 * 写单个寄存器（保持寄存器 → DB 区）
 */
function handleWriteSingleRegister(
  addr: number, value: number, mem: Uint8Array,
  transId: number, unitId: number
): Buffer {
  const off = addr * 2
  if (off + 2 <= mem.length) {
    mem[off] = (value >> 8) & 0xFF
    mem[off + 1] = value & 0xFF
    markMemDirty()
  }

  const resp = Buffer.alloc(12)
  writeUint16BE(resp, 0, transId)
  writeUint16BE(resp, 2, 0x0000)
  writeUint16BE(resp, 4, 0x0006)
  resp[6] = unitId
  resp[7] = 0x06
  writeUint16BE(resp, 8, addr)
  writeUint16BE(resp, 10, value & 0xFFFF)
  return resp
}

/**
 * 写多个寄存器（保持寄存器 → DB 区）
 */
function handleWriteMultipleRegisters(
  startAddr: number, quantity: number, data: Buffer,
  mem: Uint8Array, transId: number, unitId: number
): Buffer {
  for (let i = 0; i < quantity; i++) {
    const off = (startAddr + i) * 2
    if (off + 2 <= mem.length && i * 2 + 2 <= data.length) {
      mem[off] = data[i * 2]
      mem[off + 1] = data[i * 2 + 1]
    }
  }
  markMemDirty()

  const resp = Buffer.alloc(12)
  writeUint16BE(resp, 0, transId)
  writeUint16BE(resp, 2, 0x0000)
  writeUint16BE(resp, 4, 0x0006)
  resp[6] = unitId
  resp[7] = 0x10
  writeUint16BE(resp, 8, startAddr)
  writeUint16BE(resp, 10, quantity)
  return resp
}

// ─── Modbus 帧解析 ──

function handleModbusFrame(data: Buffer, opts: ModbusServerOptions): Buffer {
  if (data.length < 8) {
    return exceptionResponse(0, 0, 0, 0x01)
  }

  const transId = readUint16BE(data, 0)
  const unitId = data[6]      // Modbus TCP: byte 6 = Unit ID
  const funcCode = data[7]    // byte 7 = Function code

  const qMem = memory.PA
  const iMem = memory.PE
  const dbMem = ensureDbSize(opts.holdingRegisterDb, 256)
  const mMem = memory.MK

  try {
    switch (funcCode) {
      // ── 位操作 ──
      case 0x01: { // Read Coils
        const startAddr = readUint16BE(data, 8)
        const quantity = readUint16BE(data, 10)
        if (quantity < 1 || quantity > 2000) throw new Error('Invalid quantity')
        return handleReadBits(startAddr, quantity, qMem, transId, unitId, funcCode)
      }
      case 0x02: { // Read Discrete Inputs
        const startAddr = readUint16BE(data, 8)
        const quantity = readUint16BE(data, 10)
        if (quantity < 1 || quantity > 2000) throw new Error('Invalid quantity')
        return handleReadBits(startAddr, quantity, iMem, transId, unitId, funcCode)
      }
      case 0x05: { // Write Single Coil
        const addr = readUint16BE(data, 8)
        const value = readUint16BE(data, 10)
        return handleWriteSingleCoil(addr, value, qMem, transId, unitId)
      }
      case 0x0F: { // Write Multiple Coils
        const startAddr = readUint16BE(data, 8)
        const quantity = readUint16BE(data, 10)
        const byteCount = data[12]
        const coilData = data.subarray(13, 13 + byteCount)
        return handleWriteMultipleCoils(startAddr, quantity, coilData, qMem, transId, unitId)
      }

      // ── 字操作 ──
      case 0x03: { // Read Holding Registers
        const startAddr = readUint16BE(data, 8)
        const quantity = readUint16BE(data, 10)
        if (quantity < 1 || quantity > 125) throw new Error('Invalid quantity')
        return handleReadHoldingRegisters(startAddr, quantity, dbMem, transId, unitId)
      }
      case 0x04: { // Read Input Registers
        const startAddr = readUint16BE(data, 8)
        const quantity = readUint16BE(data, 10)
        if (quantity < 1 || quantity > 125) throw new Error('Invalid quantity')
        return handleReadInputRegisters(startAddr, quantity, mMem, transId, unitId)
      }
      case 0x06: { // Write Single Register
        const addr = readUint16BE(data, 8)
        const value = readUint16BE(data, 10)
        return handleWriteSingleRegister(addr, value, dbMem, transId, unitId)
      }
      case 0x10: { // Write Multiple Registers
        const startAddr = readUint16BE(data, 8)
        const quantity = readUint16BE(data, 10)
        const byteCount = data[12]
        const regData = data.subarray(13, 13 + byteCount)
        return handleWriteMultipleRegisters(startAddr, quantity, regData, dbMem, transId, unitId)
      }

      default:
        return exceptionResponse(transId, unitId, funcCode, 0x01)
    }
  } catch {
    return exceptionResponse(transId, unitId, funcCode, 0x02)
  }
}

// ─── 服务器 ──

export function createModbusServer(options: Partial<ModbusServerOptions> = {}): Promise<net.Server> {
  const opts = { ...DEFAULTS, ...options }
  let port = opts.preferredPort

  return new Promise((resolve) => {
    const server = net.createServer((sock) => {
      sock.on('data', (data: Buffer) => {
        try {
          const resp = handleModbusFrame(data, opts)
          sock.write(resp)
        } catch { /* 忽略 */ }
      })
      sock.on('error', () => {})
    })

    server.on('error', (err: any) => {
      if (err.code === 'EADDRINUSE' && port < opts.preferredPort + 100) {
        port++
        server.listen(port, '0.0.0.0')
      } else {
        console.error(`[Modbus] 启动失败: ${err.message}`)
      }
    })

    server.listen(port, '0.0.0.0', () => {
      addDiag('info', 'MODBUS', `Modbus TCP 服务启动:端口${port}`)
      resolve(server)
    })
  })
}

/** 获取当前生效的端口（用于 banner 显示） */
export function getModbusPort(server: net.Server): number {
  const addr = server.address()
  return addr ? (addr as net.AddressInfo).port : 0
}

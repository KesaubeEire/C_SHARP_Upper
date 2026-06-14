/**
 * S7 PLC 通信层
 * 封装 node-snap7js，提供类型安全读写
 */

import { S7Client, S7Consts } from 'node-snap7js'
import type { PLCVariable, PLCData } from '../shared/types.js'

// ─── 类型映射 ─────────────────────────────────────────────
const TYPE_MAP = {
  real:  { wordLen: S7Consts.S7WLReal,  bytes: 4 },
  int:   { wordLen: S7Consts.S7WLWord,  bytes: 2 },
  dint:  { wordLen: S7Consts.S7WLDWord, bytes: 4 },
  word:  { wordLen: S7Consts.S7WLWord,  bytes: 2 },
  dword: { wordLen: S7Consts.S7WLDWord, bytes: 4 },
  byte:  { wordLen: S7Consts.S7WLByte,  bytes: 1 },
  bool:  { wordLen: S7Consts.S7WLByte,  bytes: 1 },
} as const

// ─── 连接管理 ─────────────────────────────────────────────

let client: InstanceType<typeof S7Client> | null = null
let _connected = false

export function isConnected(): boolean {
  return _connected
}

export async function connect(ip: string, rack: number, slot: number): Promise<void> {
  client = new S7Client()
  const result = await client.ConnectTo(ip, rack, slot)
  if (result !== 0) {
    _connected = false
    throw new Error(`PLC 连接失败, 错误码: ${result}`)
  }
  _connected = true
}

export function disconnect(): void {
  if (client && _connected) {
    client.Disconnect()
  }
  _connected = false
}

// ─── 读取 ─────────────────────────────────────────────────

function parseBuffer(buf: Buffer, type: string, bit?: number): number | boolean {
  switch (type) {
    case 'real':   return buf.readFloatBE(0)
    case 'int':    return buf.readInt16BE(0)
    case 'dint':   return buf.readInt32BE(0)
    case 'word':   return buf.readUInt16BE(0)
    case 'dword':  return buf.readUInt32BE(0)
    case 'byte':   return buf.readUInt8(0)
    case 'bool': {
      if (bit === undefined) throw new Error('bool 类型需要指定 bit')
      return (buf.readUInt8(0) & (1 << bit)) !== 0
    }
    default:
      throw new Error(`不支持的类型: ${type}`)
  }
}

export async function readVariable(varCfg: PLCVariable): Promise<number | boolean | null> {
  if (!client || !_connected) throw new Error('PLC 未连接')

  const meta = TYPE_MAP[varCfg.type]
  const buffer = Buffer.alloc(meta.bytes)

  const result = await client.ReadArea(
    S7Consts.S7AreaDB,
    varCfg.dbNumber,
    varCfg.offset,
    1,
    meta.wordLen,
    buffer,
  )

  if (result !== 0) {
    console.warn(`[PLC] 读取失败: ${varCfg.name}: 错误码 ${result}`)
    return null
  }

  return parseBuffer(buffer, varCfg.type, varCfg.bit)
}

export async function readAll(variables: PLCVariable[]): Promise<PLCData> {
  if (!client || !_connected) {
    throw new Error('PLC 未连接')
  }

  const data: PLCData = {}
  for (const v of variables) {
    try {
      const value = await readVariable(v)
      if (value !== null) {
        data[v.name] = {
          value,
          type: v.type,
          writable: !!v.writable,
          dbNumber: v.dbNumber,
          offset: v.offset,
        }
      }
    } catch (err) {
      console.warn(`[PLC] 读取 ${v.name} 失败:`, (err as Error).message)
    }
  }
  return data
}

// ─── 写入 ─────────────────────────────────────────────────

export async function writeVariable(
  varCfg: PLCVariable,
  value: number,
): Promise<void> {
  if (!client || !_connected) throw new Error('PLC 未连接')

  if (varCfg.type === 'bool') {
    // 读出当前字节 → 修改位 → 写回
    const buf = Buffer.alloc(1)
    await client.ReadArea(S7Consts.S7AreaDB, varCfg.dbNumber, varCfg.offset, 1, S7Consts.S7WLByte, buf)
    if (value) {
      buf[0] |= (1 << (varCfg.bit ?? 0))
    } else {
      buf[0] &= ~(1 << (varCfg.bit ?? 0))
    }
    await client.WriteArea(S7Consts.S7AreaDB, varCfg.dbNumber, varCfg.offset, 1, S7Consts.S7WLByte, buf)
    return
  }

  const meta = TYPE_MAP[varCfg.type]
  const buf = Buffer.alloc(meta.bytes)

  switch (varCfg.type) {
    case 'real':  buf.writeFloatBE(value, 0); break
    case 'int':   buf.writeInt16BE(value, 0); break
    case 'dint':  buf.writeInt32BE(value, 0); break
    case 'word':  buf.writeUInt16BE(value, 0); break
    case 'dword': buf.writeUInt32BE(value, 0); break
    case 'byte':  buf.writeUInt8(value, 0); break
    default:      throw new Error(`不支持的写入类型: ${varCfg.type}`)
  }

  const result = await client.WriteArea(
    S7Consts.S7AreaDB,
    varCfg.dbNumber,
    varCfg.offset,
    1,
    meta.wordLen,
    buf,
  )
  if (result !== 0) {
    throw new Error(`写入失败, 错误码: ${result}`)
  }
}

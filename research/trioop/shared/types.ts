/** 前后端共享类型 */

export type PLCArea = 'DB' | 'PE' | 'PA'

export interface PLCVariable {
  name: string
  area?: PLCArea        // 默认 'DB'
  dbNumber: number
  offset: number
  type: 'real' | 'int' | 'dint' | 'word' | 'dword' | 'bool' | 'byte'
  bit?: number
  writable?: boolean
}

export interface PLCConnection {
  ip: string
  rack: number
  slot: number
  /** 本地网卡 IP（多网卡时指定用哪块网卡连 PLC） */
  localAddress?: string
}

export interface PLCConfig {
  plc: PLCConnection
  pollInterval: number
  variables: PLCVariable[]
  /** I/Q 区读取的字节段列表 */
  ioRanges?: {
    /** I 区字节段，如 [{ start: 0, end: 1 }, { start: 8, end: 8 }] */
    i?: { start: number; end: number }[]
    /** Q 区字节段 */
    q?: { start: number; end: number }[]
  }
}

export interface PLCDataPoint {
  value: number | boolean
  type: string
  writable: boolean
  dbNumber: number
  offset: number
}

export type PLCData = Record<string, PLCDataPoint>

/** I 区 / Q 区字节数据 */
export interface IOAreaData {
  area: 'PE' | 'PA'
  /** 字节地址 → 值 的映射 */
  bytes: Record<number, number>
}

export interface WriteRequest {
  name: string
  value: number
}

export interface WriteResponse {
  success: boolean
  name: string
  value: number
  error?: string
}

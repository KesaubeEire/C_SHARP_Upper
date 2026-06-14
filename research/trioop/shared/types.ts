/** 前后端共享类型 */

export interface PLCVariable {
  name: string
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
}

export interface PLCConfig {
  plc: PLCConnection
  pollInterval: number
  variables: PLCVariable[]
}

export interface PLCDataPoint {
  value: number | boolean
  type: string
  writable: boolean
  dbNumber: number
  offset: number
}

export type PLCData = Record<string, PLCDataPoint>

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

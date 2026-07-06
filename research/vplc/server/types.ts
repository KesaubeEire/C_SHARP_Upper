/** vPLC 共享类型定义 */

import type { ParsedDBVariable, UDTMap } from './dbParser.js'

export type { ParsedDBVariable, UDTMap }

/** PLC 运行状态 */
export type PlcStateType = 'RUN' | 'STOP' | 'STARTUP'

/** DB 配置：DB号 → 字节数 */
export type DbsConfig = Record<string, number>

export interface ImportedDBRuntime {
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

export interface ImportedFieldMeta {
  dbNumber: number
  name: string
  type: string
  offset: number
  bit?: number
  arrayCount?: number
  opaqueSize?: number
  comment?: string
}

/** 内存区域（所有区都是 Uint8Array 字节数组） */
export interface PlcMemory {
  DB: Record<number, Uint8Array>
  PE: Uint8Array   // I 区
  PA: Uint8Array   // Q 区
  MK: Uint8Array   // M 区
  TM: Uint8Array   // 定时器
  CT: Uint8Array   // 计数器
}

/** LED 状态 */
export interface PLCLED {
  color: string
  state: 'on' | 'off' | 'blink'
}

/** OB 周期定义 */
export interface OBCycle {
  num: number
  name: string
  type: 'startup' | 'freecycle' | 'cyclic'
  intervalMs: number
  runCount: number
  lastRun: number
  errors: number
  lastExecuteMs: number
  state: 'waiting' | 'running' | 'finished' | 'error'
}

/** 诊断条目 */
export interface DiagEntry {
  id: number
  timestamp: number
  category: string
  source: string
  message: string
  detail?: string
}

/** 触发器定义 */
export interface Trigger {
  id: string
  name: string
  enabled: boolean
  sourceDb: number
  sourceOffset: number
  sourceType: string
  sourceBit?: number
  condition: string
  threshold: number
  targetDb: number
  targetOffset: number
  targetType: string
  targetBit?: number
  targetValue: number
  active?: boolean
}

/** DB Editor：一行变量（类似博图 DB 编辑视图） */
export interface DBEditorField {
  name: string
  type: string       // bool, byte, word, int, dint, real, dword 等
  startValue?: string
  comment?: string
  // computed（非持久化）
  offset?: number
  bit?: number
  arrayCount?: number
}

/** DB Editor：一个 DB 块的定义 */
export interface DBEditorDef {
  key: string        // `${dbNumber}_${dbName}`
  dbNumber: number
  dbName: string
  fields: DBEditorField[]
  createdAt: number
  updatedAt: number
}

/** S7 内存区域码 */
export const S7_AREA = {
  PE: 0x81,   // I / 外设输入
  PA: 0x82,   // Q / 外设输出
  MK: 0x83,   // M 区
  DB: 0x84,   // 数据块
  CT: 0x85,   // 计数器
  TM: 0x87,   // 定时器
} as const

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
  /** I/Q/M 区读取的字节段列表 */
  ioRanges?: {
    /** I 区字节段，如 [{ start: 0, end: 1 }, { start: 8, end: 8 }] */
    i?: { start: number; end: number }[]
    /** Q 区字节段 */
    q?: { start: number; end: number }[]
    /** M 区字节段 */
    m?: { start: number; end: number }[]
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

// ========================================================================
// Alarm 报警系统类型
// ========================================================================

/** 报警严重度级别 (ISA 18.2 / EEMUA 191) */
export enum AlarmSeverity {
  Info = 0,
  Warning = 1,
  Critical = 2,
  Emergency = 3,
}

/** 报警条件/限值类型 */
export enum AlarmConditionType {
  High = 0,
  HighHigh = 1,
  Low = 2,
  LowLow = 3,
  NotEqual = 4,
  RateOfChange = 5,
  Digital = 6,
}

/** 单条报警事件 */
export interface AlarmItem {
  id: string
  timestamp: number           // Unix ms
  severity: AlarmSeverity
  alarmType: AlarmConditionType
  variableName: string
  description: string
  area: string
  currentValue: number
  threshold?: number
  deadband: number
  // 生命周期状态
  isActive: boolean
  isAcknowledged: boolean
  isShelved: boolean
  acknowledgedBy?: string
  shelvedBy?: string
  acknowledgedAt?: number      // Unix ms
  shelvedUntil?: number        // Unix ms, null = 永久搁置
  comment?: string
}

/** 报警规则 */
export interface AlarmRule {
  name: string                  // 规则唯一名（兼容旧版）
  variableKey: string
  dataType: string              // "BYTE" | "WORD" | "INT" | "DINT" | "REAL" | "LREAL"
  description: string
  severity: AlarmSeverity
  conditionType: AlarmConditionType
  condition: string             // 向后兼容: "eq" | "ne" | "gt" | "lt" | "ge" | "le"
  threshold: number
  deadband: number
  onDelayMs: number             // 触发延时 (ms)
  offDelayMs: number            // 恢复延时 (ms)
  area: string
  isEnabled: boolean
  // 内部状态 (不序列化)
  lastTriggered?: boolean
  conditionStartTime?: number
  normalStartTime?: number
}

/** 报警统计快照 */
export interface AlarmStatistics {
  totalActive: number
  totalUnacknowledged: number
  totalShelved: number
  totalToday: number
  totalThisHour: number
  totalEmergency: number
  totalCritical: number
}

// ========================================================================
// Recipe 配方系统类型
// ========================================================================

/** 配方生命周期状态 */
export enum RecipeStatus {
  Draft = 0,
  Active = 1,
  Archived = 2,
}

/** 配方单个参数 */
export interface RecipeParameter {
  name: string
  value: number
  unit: string
  address: number               // ushort
  scale: number
  offset: number
  minValue: number
  maxValue: number
  group: string
  plcDataType: string           // "REAL" | "INT" | "DINT" | "UINT" | ...
  dbNumber: number
}

/** 配方参数组 */
export interface RecipeGroup {
  name: string
  description: string
  parameters: RecipeParameter[]
  parameterCount: number
}

/** 配方完整记录 */
export interface RecipeRecord {
  id: string
  name: string
  description: string
  productCode: string
  author: string
  status: RecipeStatus
  createdAt: string             // ISO
  modifiedAt: string            // ISO
  version: number
  tags: string[]
  category: string
  defaultDbNumber: number
  groups: RecipeGroup[]
}

/** 配方列表元数据 */
export interface RecipeMeta {
  id: string
  name: string
  description: string
  productCode: string
  author: string
  status: RecipeStatus
  version: number
  category: string
  tags: string[]
  createdAt: string
  modifiedAt: string
  parameterCount: number
}

/** 配方版本快照元数据 */
export interface RecipeVersionSnapshot {
  recipeId: string
  version: number
  snapshotAt: string            // ISO
}

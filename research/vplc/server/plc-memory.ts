/**
 * vPLC 内存管理
 * 所有 PLC 内存区域的读写操作
 */

import type { ParsedDBVariable, UDTMap } from './dbParser.js'
import type { PlcMemory, ImportedDBRuntime, ImportedFieldMeta, DBEditorDef, DBEditorField } from './types.js'

// ─── 内存区域 ──
export const memory: PlcMemory = {
  DB: {} as Record<number, Uint8Array>,
  PE: new Uint8Array(256),   // I 区
  PA: new Uint8Array(256),   // Q 区
  MK: new Uint8Array(256),   // M 区
  TM: new Uint8Array(256),   // 定时器
  CT: new Uint8Array(256),   // 计数器
}

// ─── DB 配置 ──
/** DB 号 → 字节数（仅无导入文件的简易配置） */
export const dbsConfig: Record<string, number> = {}

// ─── UDT / 导入 DB 管理 ──
export const udtDefs: UDTMap = {}
export const importedDBs: Record<string, ImportedDBRuntime> = {}
export const importedTriggers: any[] = []

// ─── DB Editor 存储 ──
export const dbEditorDefs: Record<string, DBEditorDef> = {}

// ─── 内存脏标记（供 persistence 模块使用） ──
let _memDirty = false
const _dirtyListeners: Array<() => void> = []

export function onMemDirty(fn: () => void) { _dirtyListeners.push(fn) }

export function markMemDirty() {
  if (!_memDirty) { _memDirty = true; for (const fn of _dirtyListeners) fn() }
}

export function clearMemDirty() { _memDirty = false }

export function isMemDirty(): boolean { return _memDirty }

// ─── 类型工具 ──

export function typeByteSize(v: ParsedDBVariable): number {
  if (v.type === 'bool') return 1
  if (v.opaqueSize) return v.opaqueSize
  if (v.type === 'byte') return 1
  if (v.type === 'int' || v.type === 'word') return 2
  if (v.type === 'dint' || v.type === 'dword' || v.type === 'real') return 4
  return 1
}

export function calcImportedDbSize(variables: ParsedDBVariable[]): number {
  return Math.max(1, ...variables.map(v => v.offset + typeByteSize(v) * (v.arrayCount ?? 1)))
}

export function ensureDbSize(dbNumber: number, minSize: number): Uint8Array {
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

// ─── 类型化读写 ──

export function readTypedValueFromMemory(mem: Uint8Array, v: ParsedDBVariable): number | boolean | number[] | null {
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

export function writeTypedValueToMemory(mem: Uint8Array, v: ParsedDBVariable, value: number | boolean): boolean {
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

// ─── 随机值生成 ──

export function randomValueForVar(v: ParsedDBVariable): number | boolean {
  if (v.type === 'bool') return Math.random() >= 0.5
  if (v.type === 'byte') return Math.floor(Math.random() * 256)
  if (v.type === 'int') return Math.floor(Math.random() * 65536) - 32768
  if (v.type === 'word') return Math.floor(Math.random() * 65536)
  if (v.type === 'dint') return Math.floor(Math.random() * 2000001) - 1000000
  if (v.type === 'dword') return Math.floor(Math.random() * 1000000)
  if (v.type === 'real') return Number((Math.random() * 2000 - 1000).toFixed(4))
  return Math.floor(Math.random() * 256)
}

// ─── 导入 DB 快照 ──

export function buildImportedSnapshot() {
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

// ─── DB Editor 偏移量计算（博图方式）──

/** S7-1200 基本类型字节数（非优化 DB，2 字节对齐） */
const EDITOR_TYPE_SIZES: Record<string, number> = {
  bool: 1, byte: 1, char: 1, sint: 1, usint: 1,
  word: 2, int: 2, uint: 2, wchar: 2,
  dword: 4, dint: 4, udint: 4, real: 4, time: 4, tod: 4,
  lword: 8, lint: 8, ulint: 8, lreal: 8,
}

/** 根据编辑器字段列表计算每个字段的偏移量（博图式累加+对齐） */
export function calculateDBEditorOffsets(fields: DBEditorField[]): DBEditorField[] {
  let byteOff = 0
  let nextBit = 0
  return fields.map(f => {
    const f2 = { ...f }
    const rawType = f.type.toLowerCase()
    if (rawType === 'bool') {
      if (nextBit >= 8) { byteOff++; nextBit = 0 }
      f2.offset = byteOff
      f2.bit = nextBit
      nextBit++
    } else {
      if (nextBit > 0) { byteOff++; nextBit = 0 }
      if (byteOff % 2 !== 0) byteOff++
      const size = EDITOR_TYPE_SIZES[rawType] ?? 2
      f2.offset = byteOff
      byteOff += size
    }
    return f2
  })
}

/** 根据 DB Editor 定义计算总字节数 */
export function calcEditorTotalSize(fields: DBEditorField[]): number {
  const withOffsets = calculateDBEditorOffsets(fields)
  let maxEnd = 0
  for (const f of withOffsets) {
    const size = EDITOR_TYPE_SIZES[f.type.toLowerCase()] ?? 2
    const end = (f.offset ?? 0) + (f.arrayCount ?? 1) * size
    if (end > maxEnd) maxEnd = end
  }
  if (maxEnd % 2 !== 0) maxEnd++
  return Math.max(maxEnd, 1)
}

/** 将 dbEditorDefs 同步到 dbsConfig 和 memory.DB */
export function syncEditorDefToMemory(def: DBEditorDef) {
  const totalSize = calcEditorTotalSize(def.fields)
  dbsConfig[String(def.dbNumber)] = Math.max(dbsConfig[String(def.dbNumber)] || 0, totalSize)
  ensureDbSize(def.dbNumber, totalSize)
}

/** 读取 DB Editor 字段的实时值 */
export function readEditorValues(def: DBEditorDef): Record<string, any> {
  const withOffsets = calculateDBEditorOffsets(def.fields)
  const mem = ensureDbSize(def.dbNumber, calcEditorTotalSize(def.fields))
  const result: Record<string, any> = {}
  for (const f of withOffsets) {
    const pv: ParsedDBVariable = {
      name: f.name,
      type: f.type.toLowerCase(),
      offset: f.offset ?? 0,
      bit: f.bit,
      arrayCount: f.arrayCount,
    }
    result[f.name] = readTypedValueFromMemory(mem, pv)
  }
  return result
}

/** 写入 DB Editor 字段的值 */
export function writeEditorValue(def: DBEditorDef, fieldName: string, value: number | boolean): boolean {
  const withOffsets = calculateDBEditorOffsets(def.fields)
  const f = withOffsets.find(x => x.name === fieldName)
  if (!f) return false
  const mem = ensureDbSize(def.dbNumber, calcEditorTotalSize(def.fields))
  const pv: ParsedDBVariable = {
    name: f.name,
    type: f.type.toLowerCase(),
    offset: f.offset ?? 0,
    bit: f.bit,
  }
  return writeTypedValueToMemory(mem, pv, value)
}

/**
 * vPLC 内存管理
 * 所有 PLC 内存区域的读写操作
 */

import type { ParsedDBVariable, UDTMap } from '../../server/dbParser.js'
import type { PlcMemory, ImportedDBRuntime, ImportedFieldMeta } from './types.js'

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

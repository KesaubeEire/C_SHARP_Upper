/**
 * TIA Portal DB 导出文件 (.db) 解析器
 *
 * 解析非优化 DB 块的 STRUCT 定义，提取变量名、类型、偏移量
 *
 * 支持格式（TIA Portal 导出）：
 *   DATA_BLOCK "DB1"
 *     STRUCT
 *       VarName : Bool;       // 注释
 *       VarName : Int;        // 注释
 *       VarName : Real;       // 注释
 *       VarName : Array[0..9] of Byte;  // 数组
 *     END_STRUCT;
 *   BEGIN
 *   END_DATA_BLOCK
 */

/** UDT 定义中的单个字段 */
export interface UDTField {
  name: string
  type: string       // bool, int, real, 等基本类型（已展开）
  bit?: number       // bool 位号
}

/** UDT 定义映射：UDT名 → 字段列表 */
export type UDTMap = Record<string, UDTField[]>

export interface ParsedDBVariable {
  name: string
  type: string       // bool, int, real, dint, word, dword, byte
  offset: number     // 计算出的字节偏移
  bit?: number       // bool 类型的位号
  arrayCount?: number // 数组元素个数
  comment?: string   // 注释
  /** 原始类型字节数（用于 nodes7 无法直接寻址的类型：LReal=8, DTL=12 等） */
  opaqueSize?: number
}

export interface ParsedDB {
  dbNumber: number
  dbName: string
  optimized: boolean
  variables: ParsedDBVariable[]
}

// 类型占用的字节数（非优化 DB）
/**
 * S7-1200 基本数据类型字节数（非优化 DB，2 字节对齐）
 * 参考：Siemens S7-1200 系统手册 V4.6 ch.5.4
 */
const TYPE_SIZES: Record<string, number> = {
  // 位/字节
  bool: 1,
  byte: 1,  char: 1,
  sint: 1,  usint: 1,
  // 2 字节
  word: 2,  int: 2,  uint: 2,  wchar: 2,
  // 4 字节
  dword: 4,  dint: 4,  udint: 4,  real: 4,
  time: 4,  tod: 4,  s5time: 4,
  // 8 字节
  lword: 8,  lint: 8,  ulint: 8,  lreal: 8,
  // 日期
  date: 2,
  dt: 8,         // DATE_AND_TIME
  dtl: 12,       // DTL 结构体
  // 西门子系统类型
  iec_timer: 16,  iec_ltimer: 20,
  iec_scounter: 16, iec_counter: 16, iec_dcounter: 16,
  iec_lcounter: 24, iec_sscounter: 22,
}

// 类型映射到我们的类型系统（用于 nodes7 地址生成）
const TYPE_ALIAS: Record<string, string> = {
  bool: 'bool',
  byte: 'byte',  char: 'byte',  sint: 'byte',  usint: 'byte',
  word: 'word',  int: 'int',  uint: 'int',  wchar: 'word',
  dword: 'dword',  dint: 'dint',  udint: 'dint',  real: 'real',
  time: 'dword',  tod: 'dword',  s5time: 'dword',
  lword: 'byte',  lint: 'byte',  ulint: 'byte',  lreal: 'byte',
  date: 'int',
  dt: 'byte',  dtl: 'byte',
  iec_timer: 'byte',  iec_ltimer: 'byte',
  iec_scounter: 'byte', iec_counter: 'byte', iec_dcounter: 'byte',
  iec_lcounter: 'byte', iec_sscounter: 'byte',
}

// UDT 类型关键字（这些关键字不会出现在 TYPE 定义中）
const UDT_KEYWORDS = new Set(['struct', 'end_struct', 'type', 'end_type', 'version', 'name_space'])

/**
 * 解析 UDT 定义文件内容（TIA Portal 导出的 TYPE 块）
 * 一个文件可能包含多个 TYPE 定义
 */
export function parseUDTFile(content: string): UDTMap {
  const lines = content.split(/\r?\n/)
  const udtMap: UDTMap = {}

  let currentUdt: string | null = null
  let inStruct = false
  const fields: { raw: string; comment?: string }[] = []

  function commitUdt() {
    if (!currentUdt || fields.length === 0) return
    const parsedFields: UDTField[] = []
    // 计算 UDT 内部偏移（跟 DB 一样的对齐规则）
    let byteOff = 0
    let nextBit = 0
    for (const f of fields) {
      const pArr = parseVariableLine(f.raw)
      if (!pArr) continue
      for (const p of pArr) {
        if (p.type === 'bool') {
          if (nextBit >= 8) { byteOff++; nextBit = 0 }
          parsedFields.push({ name: p.name, type: 'bool', bit: nextBit })
          nextBit++
        } else {
          if (nextBit > 0) { byteOff++; nextBit = 0 }
          if (byteOff % 2 !== 0) byteOff++
          const size = p.opaqueSize ?? (TYPE_SIZES[p.type] ?? 2)
          parsedFields.push({ name: p.name, type: p.type })
          byteOff += size * (p.arrayCount ?? 1)
        }
      }
    }
    udtMap[currentUdt] = parsedFields
  }

  for (const line of lines) {
    const trimmed = line.trim()

    // TYPE "UDT_Name"
    const typeMatch = trimmed.match(/^TYPE\s+"([^"]+)"\s*$/i)
    if (typeMatch) {
      commitUdt()
      currentUdt = typeMatch[1]
      inStruct = false
      fields.length = 0
      continue
    }

    if (currentUdt) {
      if (/^\s*STRUCT\s*$/i.test(trimmed)) {
        inStruct = true
        continue
      }
      if (inStruct && /^\s*END_STRUCT\s*;/i.test(trimmed)) {
        inStruct = false
        continue
      }
      if (inStruct && /^\s*END_TYPE\s*;/i.test(trimmed)) {
        inStruct = false
        continue
      }
      if (inStruct && !/^\s*(VERSION|NAME_SPACE|BEGIN|END_)/i.test(trimmed)) {
        fields.push({ raw: trimmed })
      }
    }
  }
  commitUdt()

  return udtMap
}

/** 展开 UDT 字段为多个独立变量（带前缀），并返回总字节数 */
export function flattenUDT(prefix: string, fields: UDTField[], comment?: string): { vars: ParsedDBVariable[]; totalBytes: number } {
  const vars: ParsedDBVariable[] = []
  let byteOff = 0
  let nextBit = 0

  for (const f of fields) {
    if (f.type === 'bool') {
      if (nextBit >= 8) { byteOff++; nextBit = 0 }
      vars.push({ name: `${prefix}_${f.name}`, type: 'bool', offset: byteOff, bit: nextBit, comment })
      nextBit++
    } else {
      if (nextBit > 0) { byteOff++; nextBit = 0 }
      if (byteOff % 2 !== 0) byteOff++
      const size = TYPE_SIZES[f.type] ?? 2
      vars.push({ name: `${prefix}_${f.name}`, type: f.type, offset: byteOff, comment })
      byteOff += size
    }
  }
  // bool 末尾补齐
  if (nextBit > 0) { byteOff++ }
  if (byteOff % 2 !== 0) byteOff++

  return { vars, totalBytes: byteOff }
}

/** 展开 UDT 数组：每个元素展开一次，加元素索引前缀 */
export function flattenUDTArray(prefix: string, fields: UDTField[], arrayCount: number, comment?: string): { vars: ParsedDBVariable[]; totalBytes: number } {
  const allVars: ParsedDBVariable[] = []
  let totalBytes = 0
  for (let i = 0; i < arrayCount; i++) {
    const { vars, totalBytes: sz } = flattenUDT(`${prefix}_${i}`, fields, comment)
    allVars.push(...vars)
    totalBytes += sz
  }
  return { vars: allVars, totalBytes }
}

/**
 * 扫描 DB 文件内容，提取所有引用的 UDT 名称
 * 返回 { found, missing } 基于已加载的 udtMap
 */
export function extractReferencedUDTs(content: string, udtMap?: UDTMap): { all: string[]; found: string[]; missing: string[] } {
  const lines = content.split(/\r?\n/)
  const referenced: string[] = []
  let inDataBlock = false

  for (const line of lines) {
    const trimmed = line.trim()

    // 跳过 DATA_BLOCK 行（DB 名称会被误判为 UDT）
    if (/^DATA_BLOCK\s+"/i.test(trimmed)) {
      inDataBlock = true
      continue
    }
    // 跳过 { S7_Optimized_Access := ... } 属性块
    if (/^\s*\{.*\}\s*$/.test(trimmed)) continue
    // 跳过 BEGIN / END_DATA_BLOCK
    if (/^(BEGIN|END_DATA_BLOCK)/i.test(trimmed)) continue
    if (!inDataBlock) continue

    const quoted = trimmed.match(/"([^"]+)"/g)
    if (quoted) {
      for (const q of quoted) {
        const name = q.replace(/"/g, '')
        if (/^\d/.test(name) || /^(DATA_BLOCK|TYPE|STRUCT|END_STRUCT|BEGIN|END_DATA|VERSION|AUTHOR|NAME|FAMILY)$/i.test(name)) continue
        if (['BOOL','BYTE','WORD','DWORD','INT','DINT','REAL','LREAL','SINT','USINT','UINT','UDINT','CHAR','WCHAR','TIME','DATE','TOD','DTL','STRING','ARRAY','STRUCT','VARIANT','TRUE','FALSE','TIMER','COUNTER','BLOCK_DB','BLOCK_FC','BLOCK_FB','IEC_TIMER','IEC_LTIMER','IEC_SCOUNTER','IEC_COUNTER','IEC_DCOUNTER','IEC_LCOUNTER','IEC_SCOUNTER'].includes(name.toUpperCase())) continue
        if (!referenced.includes(name)) referenced.push(name)
      }
    }
  }

  const found: string[] = []
  const missing: string[] = []
  for (const name of referenced) {
    if (udtMap?.[name]) found.push(name)
    else missing.push(name)
  }
  return { all: referenced, found, missing }
}

/**
 * 解析 .db 文件内容
 */
export function parseDBFile(content: string, defaultDbNumber?: number, udtMap?: UDTMap): ParsedDB {
  // 去掉 UTF-8 BOM (﻿)
  if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1)
  const lines = content.split(/\r?\n/)
  const result: ParsedDB = {
    dbNumber: defaultDbNumber ?? 1,
    dbName: '',
    optimized: false,
    variables: [],
  }

  let inStruct = false
  let structEndIndex = -1
  let byteOff = 0
  let nextBit = 0

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const trimmed = line.trim()

    // 提取 DB 块号与名称: DATA_BLOCK "DB1" 或 DATA_BLOCK "Name" { DB_Number := 1 }
    const dbMatch = trimmed.match(/^DATA_BLOCK\s+"([^"]+)"(?:\s*\{[^}]*\})?/i)
    if (dbMatch) {
      const name = dbMatch[1]
      // 尝试从名称中提取数字：DB1 → 1, DB_Test → 非数字
      const numMatch = name.match(/^DB(\d+)$/i)
      if (numMatch) {
        result.dbNumber = parseInt(numMatch[1])
      }
      result.dbName = name
      continue
    }

    // 检查是否优化
    if (/S7_Optimized_Access\s*:=\s*'TRUE'/i.test(trimmed)) {
      result.optimized = true
    }

    // 进入 STRUCT
    if (/^\s*STRUCT\s*$/i.test(trimmed)) {
      inStruct = true
      continue
    }

    // 离开 STRUCT
    if (inStruct && /^\s*END_STRUCT\s*;/i.test(trimmed)) {
      inStruct = false
      break
    }

    // 解析 STRUCT 内的变量行（支持 UDT 展开为多个变量）
    if (inStruct) {
      const vars = parseVariableLine(trimmed, udtMap)
      if (!vars) continue

      for (const v of vars) {
        if (v.type === 'bool') {
          if (nextBit >= 8) { byteOff++; nextBit = 0 }
          v.offset = byteOff
          v.bit = nextBit
          nextBit++
        } else {
          if (nextBit > 0) { byteOff++; nextBit = 0 }
          if (byteOff % 2 !== 0) byteOff++
          const size = v.opaqueSize ?? (TYPE_SIZES[v.type] ?? 2)
          v.offset = byteOff
          byteOff += size * (v.arrayCount ?? 1)
        }
        result.variables.push(v)
      }
    }
  }

  return result
}

/** 去掉 TIA Portal 属性块 { S7_SetPoint := 'False' } */
function stripAttrs(s: string): string {
  return s.replace(/\s*\{[^}]*\}\s*/g, ' ').replace(/\s+/g, ' ').trim()
}

/**
 * 解析一行变量定义，返回展开后的变量列表
 * 普通类型 → [单变量]
 * UDT 类型 → [展开的多个变量]
 * 不支持  → null
 */
function parseVariableLine(line: string, udtMap?: UDTMap): ParsedDBVariable[] | null {
  // 去掉行尾注释
  const commentMatch = line.match(/\/\/\s*(.*)$/)
  const comment = commentMatch ? commentMatch[1].trim() : undefined
  let code = commentMatch ? line.substring(0, commentMatch.index).trim() : line

  // 去掉末尾的分号
  const clean = code.replace(/;\s*$/, '').trim()

  if (!clean || clean.startsWith('//') || clean.startsWith('END_STRUCT') || clean.startsWith('END_DATA')) {
    return null
  }

  // 去掉 { S7_SetPoint := 'False' } 属性块，统一格式
  const stripped = stripAttrs(clean)

  /** 创建变量，对 >1 字节的透明类型设置 opaqueSize 以便生成正确 S7 地址 */
  function mkVar(name: string, type: string, rawName: string, extra: Partial<ParsedDBVariable>): ParsedDBVariable {
    const rawSize = TYPE_SIZES[rawName]
    return { name, type, offset: 0, ...extra, opaqueSize: (rawSize && rawSize > 1 && type === 'byte') ? rawSize : undefined }
  }

  // 匹配 Array[0..N] of 类型: VarName : Array[0..N] of Type;
  // 也处理 Array[0..N] of "UDT_Name";
  const arrayMatch = stripped.match(/^([^\s:;]+)\s*:\s*Array\s*\[\s*\d+\s*\.\.\s*(\d+)\s*\]\s+of\s+(.+)/i)
  if (arrayMatch) {
    const name = arrayMatch[1]
    const rawName = arrayMatch[3].replace(/"/g, '').trim().split(/\s+/)[0].toLowerCase()
    const arrayCount = parseInt(arrayMatch[2]) + 1
    const type = TYPE_ALIAS[rawName]
    if (type) return [mkVar(name, type, rawName, { arrayCount, comment })]
    // 可能是 UDT 数组
    const udtName = arrayMatch[3].replace(/"/g, '').trim()
    if (udtMap?.[udtName]) {
      const { vars } = flattenUDTArray(name, udtMap[udtName], arrayCount, comment)
      return vars
    }
    return [{ name, type: 'byte', offset: 0, arrayCount: arrayCount * 4, comment }]
  }

  // 匹配普通类型: VarName : Type; 或 VarName : UDT_Name;
  const varMatch = stripped.match(/^([^\s:;]+)\s*:\s*(\w+)/)
  if (varMatch) {
    const name = varMatch[1]
    const rawName = varMatch[2].toLowerCase()
    if (rawName === 'string' || rawName === 'wstring') return null
    const type = TYPE_ALIAS[rawName]
    if (type) return [mkVar(name, type, rawName, { comment })]
    // 可能是 UDT 引用（无引号）
    if (udtMap?.[varMatch[2]]) {
      const { vars } = flattenUDT(name, udtMap[varMatch[2]], comment)
      return vars
    }
    return null
  }

  // 匹配 UDT 类型: VarName : "UDT名称";
  const udtMatch = stripped.match(/^([^\s:;]+)\s*:\s*"([^"]+)"/)
  if (udtMatch) {
    const name = udtMatch[1]
    const udtName = udtMatch[2]
    if (udtMap?.[udtName]) {
      const { vars } = flattenUDT(name, udtMap[udtName], comment)
      return vars
    }
    return [mkVar(name, 'byte', udtName, { comment })]
  }

  return null
}

/**
 * 将解析后的 DB 变量转换为 nodes7 标签列表
 */
export function parsedVarsToNodes7Tags(variables: ParsedDBVariable[], dbNumber: number): { tag: string; s7addr: string; name: string }[] {
  const tags: { tag: string; s7addr: string; name: string }[] = []

  for (const v of variables) {
    const tag = `cfg_${v.name}_${dbNumber}`
    if (v.type === 'bool') {
      tags.push({ tag, s7addr: `DB${dbNumber},X${v.offset}.${v.bit ?? 0}`, name: v.name })
    } else {
      const typeMap: Record<string, string> = {
        real: 'R', int: 'I', dint: 'DI', word: 'W', dword: 'DW', byte: 'B',
        uint: 'W', udint: 'DW', sint: 'B', usint: 'B', date: 'I',
      }
      const t = typeMap[v.type] || 'B'
      // opaqueSize 覆盖 arrayCount：大类型读整块字节
      const count = v.opaqueSize ?? v.arrayCount ?? 1
      tags.push({ tag, s7addr: `DB${dbNumber},${t}${v.offset}.${count}`, name: v.name })
    }
  }

  return tags
}

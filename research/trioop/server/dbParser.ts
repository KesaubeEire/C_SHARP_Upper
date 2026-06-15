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

export interface ParsedDBVariable {
  name: string
  type: string       // bool, int, real, dint, word, dword, byte
  offset: number     // 计算出的字节偏移
  bit?: number       // bool 类型的位号
  arrayCount?: number // 数组元素个数
  comment?: string   // 注释
}

export interface ParsedDB {
  dbNumber: number
  dbName: string
  optimized: boolean
  variables: ParsedDBVariable[]
}

// 类型占用的字节数（非优化 DB）
const TYPE_SIZES: Record<string, number> = {
  bool: 1,
  byte: 1,
  char: 1,
  word: 2,
  int: 2,
  dword: 4,
  dint: 4,
  real: 4,
  time: 4,
  date: 4,
  tod: 4,
  s5time: 4,
}

// 类型映射到我们的类型系统
const TYPE_ALIAS: Record<string, string> = {
  bool: 'bool',
  byte: 'byte',
  char: 'byte',
  word: 'word',
  int: 'int',
  dword: 'dword',
  dint: 'dint',
  real: 'real',
  time: 'dword',
  date: 'dword',
  tod: 'dword',
  s5time: 'dword',
}

/**
 * 解析 .db 文件内容
 */
export function parseDBFile(content: string, defaultDbNumber?: number): ParsedDB {
  const lines = content.split(/\r?\n/)
  const result: ParsedDB = {
    dbNumber: defaultDbNumber ?? 1,
    dbName: '',
    optimized: false,
    variables: [],
  }

  let inStruct = false
  let structEndIndex = -1

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
      structEndIndex = i
      break
    }

    // 解析 STRUCT 内的变量行
    if (inStruct) {
      const parsed = parseVariableLine(trimmed)
      if (parsed) {
        result.variables.push(parsed)
      }
    }
  }

  // 计算偏移量（S7-1200 非优化 DB）
  //   - Bool 按位打包，8 个/字节
  //   - 遇到非 Bool 时，当前 Bool 字节剩余位废弃，跳到 2 字节对齐
  //   - 所有非 Bool 类型统一 2 字节对齐（不是按类型自然对齐）
  let byteOff = 0
  let nextBit = 0        // 0-7，下一个 Bool 可用的位

  for (const v of result.variables) {
    if (v.type === 'bool') {
      if (nextBit >= 8) { byteOff++; nextBit = 0 }
      v.offset = byteOff
      v.bit = nextBit
      nextBit++
    } else {
      if (nextBit > 0) { byteOff++; nextBit = 0 }  // 废弃剩余 bool 位
      if (byteOff % 2 !== 0) byteOff++              // 2 字节对齐
      const size = TYPE_SIZES[v.type] ?? 2
      v.offset = byteOff
      byteOff += size * (v.arrayCount ?? 1)
    }
  }

  return result
}

/**
 * 解析一行变量定义
 * 格式: VarName : Type; 或 VarName : Array[0..N] of Type;
 */
function parseVariableLine(line: string): ParsedDBVariable | null {
  // 去掉行尾注释
  const commentMatch = line.match(/\/\/\s*(.*)$/)
  const comment = commentMatch ? commentMatch[1].trim() : undefined
  const code = commentMatch ? line.substring(0, commentMatch.index).trim() : line

  // 去掉末尾的分号
  const clean = code.replace(/;\s*$/, '').trim()

  if (!clean || clean.startsWith('//') || clean.startsWith('END_STRUCT') || clean.startsWith('END_DATA')) {
    return null
  }

  // 匹配 Array 类型: VarName : Array[0..N] of Type;
  const arrayMatch = clean.match(/^([^\s:;]+)\s*:\s*Array\s*\[\s*\d+\s*\.\.\s*(\d+)\s*\]\s+of\s+(\w+)/i)
  if (arrayMatch) {
    const name = arrayMatch[1]
    const rawType = arrayMatch[3].toLowerCase()
    const arrayCount = parseInt(arrayMatch[2]) + 1  // 0..9 → 10 个
    const type = TYPE_ALIAS[rawType]
    if (!type) return null
    return { name, type, offset: 0, arrayCount, comment }
  }

  // 匹配普通类型: VarName : Type;（变量名支持中文）
  const varMatch = clean.match(/^([^\s:;]+)\s*:\s*(\w+)/)
  if (varMatch) {
    const name = varMatch[1]
    const rawType = varMatch[2].toLowerCase()
    const type = TYPE_ALIAS[rawType]
    if (!type) return null

    // 字符串类型特殊处理
    if (rawType === 'string' || rawType === 'wstring') {
      return null  // 暂不支持字符串
    }

    return { name, type, offset: 0, comment }
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
      }
      const t = typeMap[v.type] || 'B'
      if (v.arrayCount && v.arrayCount > 1) {
        tags.push({ tag, s7addr: `DB${dbNumber},${t}${v.offset}.${v.arrayCount}`, name: v.name })
      } else {
        tags.push({ tag, s7addr: `DB${dbNumber},${t}${v.offset}.1`, name: v.name })
      }
    }
  }

  return tags
}

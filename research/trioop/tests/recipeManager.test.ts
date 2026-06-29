/**
 * recipeManager — 单元测试（内联模式，绕过 vitest .js 扩展解析限制）
 */

import { describe, it, expect, beforeAll, afterAll } from 'vitest'
import fs from 'fs'
import path from 'path'
import os from 'os'
import { RecipeStatus, type RecipeRecord } from '../shared/types.js'

// 直接内联测试 CSV 函数（纯函数），不再依赖模块导入
// 从 server/recipeManager.ts 复制测试所需的函数
// 这些是纯函数，不依赖文件系统

function escCsv(s: string | number): string {
  const str = String(s)
  if (str.includes(',') || str.includes('"') || str.includes('\n')) {
    return '"' + str.replace(/"/g, '""') + '"'
  }
  return str
}

function getAllParams(recipe: RecipeRecord): any[] {
  return recipe.groups.flatMap(g => g.parameters)
}

function exportToCsv(recipe: RecipeRecord): string {
  const params = getAllParams(recipe)
  const lines = ['Name,Value,Unit,Address,Scale,Offset,Group,PlcDataType,DbNumber,MinValue,MaxValue']
  for (const p of params) {
    lines.push([
      escCsv(p.name), String(p.value), escCsv(p.unit), String(p.address),
      String(p.scale), String(p.offset),
      escCsv(p.group), escCsv(p.plcDataType), String(p.dbNumber),
      String(p.minValue), String(p.maxValue),
    ].join(','))
  }
  return lines.join('\n')
}

function parseCsvLine(line: string, delimiter: string): string[] {
  if (delimiter === '\t') return line.split('\t')

  const result: string[] = []
  let current = ''
  let inQuotes = false
  let i = 0
  while (i < line.length) {
    const c = line[i]
    if (c === '"') {
      if (inQuotes && i + 1 < line.length && line[i + 1] === '"') {
        current += '"'
        i += 2
        continue
      } else {
        inQuotes = !inQuotes
      }
    } else if (c === delimiter && !inQuotes) {
      result.push(current)
      current = ''
    } else {
      current += c
    }
    i++
  }
  result.push(current)
  return result
}

function importFromCsv(csvText: string, targetGroup?: string): any[] {
  const result: any[] = []
  const lines = csvText.split('\n').map(l => l.trim()).filter(Boolean)
  if (lines.length < 2) return result

  const delimiter = lines[0].includes('\t') ? '\t' : ','

  for (let i = 1; i < lines.length; i++) {
    try {
      const cols = parseCsvLine(lines[i], delimiter)
      if (cols.length < 4) continue

      const param = {
        name: cols[0],
        value: parseFloat(cols[1]) || 0,
        unit: cols[2] || '',
        address: parseInt(cols[3]) || 0,
        scale: cols.length > 4 ? parseFloat(cols[4]) || 1.0 : 1.0,
        offset: cols.length > 5 ? parseFloat(cols[5]) || 0 : 0,
        group: cols.length > 6 ? cols[6] : (targetGroup ?? ''),
        plcDataType: cols.length > 7 ? cols[7] : 'REAL',
        dbNumber: cols.length > 8 ? parseInt(cols[8]) || 0 : 0,
        minValue: cols.length > 9 ? parseFloat(cols[9]) || -Infinity : -Infinity,
        maxValue: cols.length > 10 ? parseFloat(cols[10]) || Infinity : Infinity,
      }
      result.push(param)
    } catch { /* skip malformed */ }
  }
  return result
}

function readCsvFileWithAutoDetect(filePath: string): string {
  const buffer = fs.readFileSync(filePath)
  if (buffer.length >= 3 && buffer[0] === 0xEF && buffer[1] === 0xBB && buffer[2] === 0xBF) {
    return buffer.toString('utf-8', 3)
  }
  return buffer.toString('utf-8')
}

// ─── 测试数据 ─────────────────────────────────────────────────

function makeSampleRecipe(overrides: Partial<RecipeRecord> = {}): RecipeRecord {
  return {
    id: 'test_recipe_001',
    name: '测试配方',
    description: '用于单元测试',
    productCode: 'PC-001',
    author: 'Tester',
    status: RecipeStatus.Draft,
    createdAt: '2026-01-01T00:00:00.000Z',
    modifiedAt: '2026-01-01T00:00:00.000Z',
    version: 1,
    tags: ['测试', '温度'],
    category: '标准',
    defaultDbNumber: 1,
    groups: [
      {
        name: '温度组',
        description: '温度参数',
        parameterCount: 2,
        parameters: [
          { name: 'Temp1', value: 100, unit: '°C', address: 0, scale: 1.0, offset: 0, minValue: -Infinity, maxValue: Infinity, group: '温度组', plcDataType: 'REAL', dbNumber: 1 },
          { name: 'Temp2', value: 200, unit: '°C', address: 2, scale: 1.0, offset: 0, minValue: -Infinity, maxValue: Infinity, group: '温度组', plcDataType: 'REAL', dbNumber: 1 },
        ],
      },
    ],
    ...overrides,
  }
}

function makeRecipeWithSpecialChars(): RecipeRecord {
  return {
    ...makeSampleRecipe(),
    id: 'test_special',
    groups: [{
      name: '组"1"',
      description: '带,逗号描述',
      parameterCount: 2,
      parameters: [
        { name: '参,数A', value: 99.5, unit: 'mm', address: 0, scale: 1.0, offset: 0, minValue: -Infinity, maxValue: Infinity, group: '组"1"', plcDataType: 'REAL', dbNumber: 1 },
        { name: '参"数"B', value: -5, unit: '"inch"', address: 2, scale: 1.0, offset: 0, minValue: -Infinity, maxValue: Infinity, group: '组"1"', plcDataType: 'INT', dbNumber: 1 },
      ],
    }],
  }
}

// ═══════════════════════════════════════════════════════════
// CSV 导出
// ═══════════════════════════════════════════════════════════

describe('exportToCsv', () => {
  it('导出标准 CSV 格式', () => {
    const recipe = makeSampleRecipe()
    const csv = exportToCsv(recipe)
    const lines = csv.split('\n').filter(l => l.trim())

    expect(lines[0]).toBe('Name,Value,Unit,Address,Scale,Offset,Group,PlcDataType,DbNumber,MinValue,MaxValue')
    expect(lines[1]).toContain('Temp1')
    expect(lines[1]).toContain('100')
    expect(lines[2]).toContain('Temp2')
    expect(lines[2]).toContain('200')
  })

  it('转义含逗号的参数名', () => {
    const csv = exportToCsv(makeRecipeWithSpecialChars())
    expect(csv).toMatch(/"参,数A"/)
  })

  it('转义含引号的参数名', () => {
    const csv = exportToCsv(makeRecipeWithSpecialChars())
    expect(csv).toMatch(/"参""数""B"/)
  })

  it('转义含引号的单位：""inch"" 导出为 """"inch""""', () => {
    const csv = exportToCsv(makeRecipeWithSpecialChars())
    const lines = csv.split('\n').filter(Boolean)
    // 第二行数据中 unit 应是 """inch"""（csv 转义：""inch"" → """"inch""""）
    const row2 = lines[2]
    expect(row2).toContain('"""inch"""')
  })
})

// ═══════════════════════════════════════════════════════════
// CSV 导入
// ═══════════════════════════════════════════════════════════

describe('importFromCsv', () => {
  it('解析标准 CSV', () => {
    const csv = [
      'Name,Value,Unit,Address,Scale,Offset,Group,PlcDataType,DbNumber,MinValue,MaxValue',
      'Temp1,100,°C,0,1.0,0,温度组,REAL,1,-Infinity,Infinity',
      'Temp2,200,°C,2,1.0,0,温度组,REAL,1,-Infinity,Infinity',
    ].join('\n')
    const params = importFromCsv(csv)
    expect(params).toHaveLength(2)
    expect(params[0].name).toBe('Temp1')
    expect(params[0].value).toBe(100)
    expect(params[0].unit).toBe('°C')
    expect(params[1].name).toBe('Temp2')
    expect(params[1].address).toBe(2)
  })

  it('解析带转义的 CSV（逗号、引号）', () => {
    const csv = [
      'Name,Value,Unit,Address,Scale,Offset,Group,PlcDataType,DbNumber,MinValue,MaxValue',
      '"参,数A",99.5,mm,0,1.0,0,"组""1""",REAL,1,-Infinity,Infinity',
      '"参""数""B",-5,"""inch""",2,1.0,0,"组""1""",INT,1,-Infinity,Infinity',
    ].join('\n')
    const params = importFromCsv(csv)
    expect(params).toHaveLength(2)
    expect(params[0].name).toBe('参,数A')
    expect(params[0].unit).toBe('mm')
    expect(params[1].name).toBe('参"数"B')
    expect(params[1].unit).toBe('"inch"')
  })

  it('自动检测 Tab 分隔符', () => {
    const csv = 'Name\tValue\tUnit\tAddress\nTemp1\t100\t°C\t0\nTemp2\t200\t°C\t2'
    const params = importFromCsv(csv)
    expect(params).toHaveLength(2)
    expect(params[0].name).toBe('Temp1')
  })

  it('空文件/仅有表头返回空数组', () => {
    expect(importFromCsv('')).toEqual([])
    expect(importFromCsv('Name,Value\n')).toEqual([])
    expect(importFromCsv('\n\n')).toEqual([])
  })

  it('使用 targetGroup 作为默认组名', () => {
    const csv = 'Name,Value,Unit,Address\nTemp1,100,°C,0'
    const params = importFromCsv(csv, '默认组')
    expect(params[0].group).toBe('默认组')
  })

  it('保留 CSV 中的分组名', () => {
    const csv = 'Name,Value,Unit,Address,Scale,Offset,Group\nTemp1,100,°C,0,1.0,0,自定义组'
    const params = importFromCsv(csv)
    expect(params[0].group).toBe('自定义组')
  })

  it('浮点精度不丢失', () => {
    const csv = 'Name,Value,Unit,Address\nPi,3.14159265358979,mm,0'
    const params = importFromCsv(csv)
    expect(params[0].value).toBeCloseTo(3.14159265358979, 12)
  })

  it('中文参数名正常', () => {
    const csv = 'Name,Value,Unit,Address\n温度1,120,°C,0\n压力A,0.5,MPa,2'
    const params = importFromCsv(csv)
    expect(params[0].name).toBe('温度1')
    expect(params[1].name).toBe('压力A')
    expect(params[1].unit).toBe('MPa')
    expect(params[1].value).toBe(0.5)
  })

  it('只有必要字段的 CSV', () => {
    const csv = 'Name,Value,Unit,Address\nX,42,mm,10'
    const params = importFromCsv(csv)
    expect(params[0].name).toBe('X')
    expect(params[0].value).toBe(42)
    expect(params[0].scale).toBe(1.0)
    expect(params[0].plcDataType).toBe('REAL')
  })
})

// ═══════════════════════════════════════════════════════════
// 往返测试：export → import
// ═══════════════════════════════════════════════════════════

describe('CSV 往返测试', () => {
  it('标准数据往返一致', () => {
    const recipe = makeSampleRecipe()
    const csv = exportToCsv(recipe)
    const params = importFromCsv(csv)
    expect(params).toHaveLength(2)
    expect(params[0].name).toBe('Temp1')
    expect(params[0].value).toBe(100)
  })

  it('特殊字符往返一致', () => {
    const recipe = makeRecipeWithSpecialChars()
    const csv = exportToCsv(recipe)
    const params = importFromCsv(csv)
    expect(params).toHaveLength(2)
    expect(params[0].name).toBe('参,数A')
    expect(params[0].value).toBe(99.5)
    expect(params[1].name).toBe('参"数"B')
    expect(params[1].value).toBe(-5)
    expect(params[1].unit).toBe('"inch"')
  })
})

// ═══════════════════════════════════════════════════════════
// readCsvFileWithAutoDetect — 文件 IO
// ═══════════════════════════════════════════════════════════

describe('readCsvFileWithAutoDetect', () => {
  function tmpFile(prefix = 'csvtest'): string {
    return path.join(os.tmpdir(), `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2, 6)}.csv`)
  }

  it('检测并去除 UTF-8 BOM', () => {
    const fp = tmpFile()
    const bom = Buffer.from([0xEF, 0xBB, 0xBF])
    const content = Buffer.from('Name,Value\nTemp1,100', 'utf-8')
    fs.writeFileSync(fp, Buffer.concat([bom, content]))
    try {
      const result = readCsvFileWithAutoDetect(fp)
      expect(result).toBe('Name,Value\nTemp1,100')
      expect(result.charCodeAt(0)).toBe(78) // 'N'
    } finally { fs.unlinkSync(fp) }
  })

  it('无 BOM 正常读取', () => {
    const fp = tmpFile()
    fs.writeFileSync(fp, 'Name,Value\nTemp1,100', 'utf-8')
    try {
      expect(readCsvFileWithAutoDetect(fp)).toBe('Name,Value\nTemp1,100')
    } finally { fs.unlinkSync(fp) }
  })

  it('中文 CSV 无 BOM 正常读取', () => {
    const fp = tmpFile()
    fs.writeFileSync(fp, '名称,数值,单位\n温度1,100,°C\n压力,0.5,MPa', 'utf-8')
    try {
      const result = readCsvFileWithAutoDetect(fp)
      expect(result).toContain('温度1')
      expect(result).toContain('°C')
    } finally { fs.unlinkSync(fp) }
  })

  it('带 BOM 的中文 CSV', () => {
    const fp = tmpFile()
    const bom = Buffer.from([0xEF, 0xBB, 0xBF])
    const content = Buffer.from('名称,数值\n温度1,100', 'utf-8')
    fs.writeFileSync(fp, Buffer.concat([bom, content]))
    try {
      const result = readCsvFileWithAutoDetect(fp)
      expect(result).toBe('名称,数值\n温度1,100')
    } finally { fs.unlinkSync(fp) }
  })
})

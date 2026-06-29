/**
 * 配方管理 — Recipe CRUD + 分组参数 + 版本管理 + CSV 导入导出
 *
 * 数据存储: data/recipes/<id>.json
 * 版本快照: data/recipes/_versions/<id>/v{N}.json
 */

import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import type { RecipeRecord, RecipeMeta, RecipeGroup, RecipeParameter, RecipeVersionSnapshot } from '../shared/types.js'
import { RecipeStatus } from '../shared/types.js'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const DATA_DIR = path.resolve(__dirname, '..', 'data')
const RECIPES_DIR = path.join(DATA_DIR, 'recipes')
const VERSIONS_DIR = path.join(RECIPES_DIR, '_versions')

function ensureDirs() {
  if (!fs.existsSync(RECIPES_DIR)) fs.mkdirSync(RECIPES_DIR, { recursive: true })
  if (!fs.existsSync(VERSIONS_DIR)) fs.mkdirSync(VERSIONS_DIR, { recursive: true })
}

function filePath(id: string) { return path.join(RECIPES_DIR, `${id}.json`) }
function versionDir(id: string) { return path.join(VERSIONS_DIR, id) }
function versionFilePath(id: string, version: number) { return path.join(versionDir(id), `v${version}.json`) }

// ===================== CRUD =====================

/** 快速读取所有配方的元数据（不完整反序列化，只取 head 字段） */
export function getAllRecipes(): RecipeMeta[] {
  ensureDirs()
  const files = fs.readdirSync(RECIPES_DIR).filter(f => f.endsWith('.json') && !f.startsWith('_'))
  const result: RecipeMeta[] = []

  for (const file of files) {
    try {
      const raw = fs.readFileSync(path.join(RECIPES_DIR, file), 'utf-8')
      const doc = JSON.parse(raw)

      // 统计参数总数
      let paramCount = 0
      if (Array.isArray(doc.groups)) {
        for (const g of doc.groups) {
          if (Array.isArray(g.parameters)) paramCount += g.parameters.length
        }
      }

      result.push({
        id: doc.id ?? file.replace('.json', ''),
        name: doc.name ?? file.replace('.json', ''),
        description: doc.description ?? '',
        productCode: doc.productCode ?? '',
        author: doc.author ?? '',
        status: doc.status ?? RecipeStatus.Draft,
        version: doc.version ?? 1,
        category: doc.category ?? '',
        tags: Array.isArray(doc.tags) ? doc.tags : [],
        createdAt: doc.createdAt ?? new Date(0).toISOString(),
        modifiedAt: doc.modifiedAt ?? new Date(0).toISOString(),
        parameterCount: paramCount,
      })
    } catch { /* skip corrupt files */ }
  }

  return result.sort((a, b) => new Date(b.modifiedAt).getTime() - new Date(a.modifiedAt).getTime())
}

export function loadRecipe(id: string): RecipeRecord | null {
  const fp = filePath(id)
  if (!fs.existsSync(fp)) return null
  try {
    const raw = fs.readFileSync(fp, 'utf-8')
    return JSON.parse(raw) as RecipeRecord
  } catch { return null }
}

export function saveRecipe(recipe: RecipeRecord): void {
  ensureDirs()
  recipe.modifiedAt = new Date().toISOString()
  recipe.version = (recipe.version || 0) + 1

  const fp = filePath(recipe.id)
  fs.writeFileSync(fp, JSON.stringify(recipe, null, 2), 'utf-8')

  // 自动创建版本快照
  saveVersionSnapshot(recipe)
}

export function deleteRecipe(id: string): boolean {
  const fp = filePath(id)
  if (!fs.existsSync(fp)) return false
  fs.unlinkSync(fp)

  // 删除版本历史
  const vd = versionDir(id)
  if (fs.existsSync(vd)) {
    fs.rmSync(vd, { recursive: true, force: true })
  }
  return true
}

export function copyRecipe(sourceId: string, newName: string): RecipeRecord | null {
  const source = loadRecipe(sourceId)
  if (!source) return null

  const copy: RecipeRecord = {
    id: generateId(),
    name: newName,
    description: source.description,
    productCode: source.productCode,
    author: source.author,
    status: source.status,
    createdAt: new Date().toISOString(),
    modifiedAt: new Date().toISOString(),
    version: 1,
    tags: [...source.tags],
    category: source.category,
    defaultDbNumber: source.defaultDbNumber,
    groups: source.groups.map(g => ({
      name: g.name,
      description: g.description,
      parameterCount: g.parameterCount,
      parameters: g.parameters.map(p => deepCopyParam(p)),
    })),
  }

  saveRecipe(copy)
  return copy
}

// ===================== 版本管理 =====================

export function getVersionHistory(recipeId: string): RecipeVersionSnapshot[] {
  const vd = versionDir(recipeId)
  if (!fs.existsSync(vd)) return []

  const files = fs.readdirSync(vd).filter(f => f.endsWith('.json'))
  const result: RecipeVersionSnapshot[] = []

  for (const file of files) {
    try {
      const raw = fs.readFileSync(path.join(vd, file), 'utf-8')
      const doc = JSON.parse(raw)
      result.push({
        recipeId,
        version: doc.version ?? 0,
        snapshotAt: doc.modifiedAt ?? new Date(0).toISOString(),
      })
    } catch { /* skip */ }
  }

  return result.sort((a, b) => b.version - a.version)
}

export function loadRecipeVersion(recipeId: string, version: number): RecipeRecord | null {
  const fp = versionFilePath(recipeId, version)
  if (!fs.existsSync(fp)) return null
  try {
    const raw = fs.readFileSync(fp, 'utf-8')
    return JSON.parse(raw) as RecipeRecord
  } catch { return null }
}

export function restoreVersion(recipeId: string, version: number): RecipeRecord | null {
  const snapshot = loadRecipeVersion(recipeId, version)
  if (!snapshot) return null

  const current = loadRecipe(recipeId)
  const restored: RecipeRecord = {
    id: recipeId,
    name: snapshot.name,
    description: snapshot.description,
    productCode: snapshot.productCode,
    author: snapshot.author,
    status: snapshot.status,
    createdAt: current?.createdAt ?? new Date().toISOString(),
    modifiedAt: new Date().toISOString(),
    version: current?.version ?? 1,
    tags: [...snapshot.tags],
    category: snapshot.category,
    defaultDbNumber: snapshot.defaultDbNumber,
    groups: snapshot.groups.map(g => ({
      name: g.name,
      description: g.description,
      parameterCount: g.parameterCount,
      parameters: g.parameters.map(p => deepCopyParam(p)),
    })),
  }

  saveRecipe(restored)
  return restored
}

function saveVersionSnapshot(recipe: RecipeRecord) {
  const vd = versionDir(recipe.id)
  if (!fs.existsSync(vd)) fs.mkdirSync(vd, { recursive: true })
  const fp = versionFilePath(recipe.id, recipe.version)
  fs.writeFileSync(fp, JSON.stringify(recipe, null, 2), 'utf-8')
}

// ===================== CSV 导出/导入 =====================

/** 获取所有参数的扁平列表 */
function getAllParams(recipe: RecipeRecord): RecipeParameter[] {
  return recipe.groups.flatMap(g => g.parameters)
}

export function exportToCsv(recipe: RecipeRecord): string {
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

export function importFromCsv(csvText: string, targetGroup?: string): RecipeParameter[] {
  const result: RecipeParameter[] = []
  const lines = csvText.split('\n').map(l => l.trim()).filter(Boolean)
  if (lines.length < 2) return result

  // 检测分隔符
  const delimiter = lines[0].includes('\t') ? '\t' : ','

  for (let i = 1; i < lines.length; i++) {
    try {
      const cols = parseCsvLine(lines[i], delimiter)
      if (cols.length < 4) continue

      const param: RecipeParameter = {
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

export function readCsvFileWithAutoDetect(filePath: string): string {
  const buffer = fs.readFileSync(filePath)
  // 检查 UTF-8 BOM
  if (buffer.length >= 3 && buffer[0] === 0xEF && buffer[1] === 0xBB && buffer[2] === 0xBF) {
    return buffer.toString('utf-8', 3)
  }
  // 无 BOM → UTF-8 (Node 默认)
  return buffer.toString('utf-8')
}

// ===================== 工具函数 =====================

function generateId(): string {
  return `recipe_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`
}

function deepCopyParam(p: RecipeParameter): RecipeParameter {
  return { ...p }
}

function escCsv(s: string | number): string {
  const str = String(s)
  if (str.includes(',') || str.includes('"') || str.includes('\n')) {
    return '"' + str.replace(/"/g, '""') + '"'
  }
  return str
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

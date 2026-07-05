/**
 * vPLC 持久化
 * 配置加载/保存、内存数据持久化、PID 管理
 */

import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

import { memory, dbsConfig, udtDefs, importedDBs, ensureDbSize, isMemDirty, clearMemDirty, onMemDirty } from './plc-memory.js'
import { getUserScripts, setUserScripts } from './plc-runtime.js'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

// ─── 配置路径 ──
export const cfgPath = path.resolve(__dirname, '..', 'vplc-config.json')
export const memPath = path.resolve(__dirname, '..', 'vplc-memory.json')
export const pidPath = path.resolve(__dirname, '..', 'vplc.pid')

// ─── 配置文件 ──

export interface VplcConfig {
  port: number
  host: string
  dbs: Record<string, number>
  udts?: any
  imported?: any
  scripts?: string[]
}

export function loadConfig(): VplcConfig {
  try {
    const raw = JSON.parse(fs.readFileSync(cfgPath, 'utf-8'))
    return { port: 1102, host: '0.0.0.0', dbs: {}, ...raw }
  } catch {
    const defaults: VplcConfig = { port: 1102, host: '0.0.0.0', dbs: { '1': 64, '6': 64, '7': 100 } }
    fs.writeFileSync(cfgPath, JSON.stringify(defaults, null, 2), 'utf-8')
    return defaults
  }
}

export function writeConfig() {
  try {
    const raw = JSON.parse(fs.readFileSync(cfgPath, 'utf-8'))
    raw.dbs = dbsConfig
    raw.udts = Object.fromEntries(Object.entries(udtDefs).map(([k, v]) => [k, v]))
    raw.imported = Object.fromEntries(
      Object.entries(importedDBs).map(([k, v]) => [k, { ...v, variables: v.variables }])
    )
    raw.scripts = getUserScripts().map(s => ({ name: s.name, source: s.source, obNumber: s.obNumber, enabled: s.enabled }))
    fs.writeFileSync(cfgPath, JSON.stringify(raw, null, 2), 'utf-8')
  } catch { /* 忽略 */ }
}

/** 从配置恢复 UDT、导入 DB 和脚本 */
export function restoreImports(raw: any) {
  if (raw.udts) Object.assign(udtDefs, raw.udts)
  if (raw.imported) {
    for (const [key, val] of Object.entries(raw.imported)) {
      const v = val as any
      importedDBs[key] = {
        key, dbNumber: v.dbNumber, dbName: v.dbName,
        variableCount: v.variableCount, variables: v.variables,
        byteSize: v.byteSize,
        rawContent: v.rawContent, createdAt: v.createdAt, updatedAt: v.updatedAt,
      }
      if (v.dbNumber && v.byteSize) ensureDbSize(v.dbNumber, v.byteSize)
    }
  }
  // 恢复用户脚本
  if (raw.scripts && Array.isArray(raw.scripts)) {
    setUserScripts(raw.scripts.map((s: any) => ({
      name: s.name || 'script',
      source: s.source || '',
      obNumber: s.obNumber ?? 1,
      enabled: s.enabled !== false,
    })))
  }
}

// ─── PID 文件 ──

export function killPreviousInstance() {
  try {
    const oldPid = Number(fs.readFileSync(pidPath, 'utf-8').trim())
    if (oldPid && oldPid !== process.pid) {
      try { process.kill(oldPid, 'SIGTERM') } catch {}
    }
  } catch {}
  fs.writeFileSync(pidPath, String(process.pid), 'utf-8')
}

export function removePidFile() {
  try { fs.unlinkSync(pidPath) } catch {}
}

// ─── 内存数据持久化 ──

let _memTimer: any = null

export function saveMemory() {
  try {
    const data: Record<string, number[]> = {}
    for (const [k, v] of Object.entries(memory.DB)) {
      data[String(k)] = Array.from(v)
    }
    fs.writeFileSync(memPath, JSON.stringify(data), 'utf-8')
    clearMemDirty()
  } catch { /* 忽略 */ }
}

export function loadMemory() {
  try {
    const data = JSON.parse(fs.readFileSync(memPath, 'utf-8'))
    for (const [arr, bytes] of Object.entries(data)) {
      const dbNum = Number(arr)
      const byteArr = bytes as number[]
      if (memory.DB[dbNum] && memory.DB[dbNum].length === byteArr.length) {
        memory.DB[dbNum].set(byteArr)
      } else if (byteArr.length > 0) {
        memory.DB[dbNum] = new Uint8Array(byteArr)
      }
    }
  } catch { /* 忽略 */ }
}

export function markMemDirtyDebounced() {
  if (_memTimer) clearTimeout(_memTimer)
  _memTimer = setTimeout(saveMemory, 2000)
}

/** 开始自动保存（每 30 秒） */
export function startAutoSave() {
  setInterval(() => { if (isMemDirty()) saveMemory() }, 30000)
  onMemDirty(() => markMemDirtyDebounced())
}

/**
 * 历史数据存储
 *
 * 纯文件实现，零外部依赖。
 * 按天分文件，每 5 秒批量写入。
 * 格式: { t: timestamp, n: name, v: value }
 */

import fs from 'fs'
import path from 'path'

const DATA_DIR = path.resolve(import.meta.dirname, '..', 'data', 'history')
const FLUSH_INTERVAL = 5000 // 5s 刷一次盘

// 确保目录存在
try { fs.mkdirSync(DATA_DIR, { recursive: true }) } catch {}

// 写缓冲 { fileName: entries[] }
const buffer: Map<string, { t: number; n: string; v: number }[]> = new Map()

let flushTimer: ReturnType<typeof setInterval> | null = null

function getFileName(): string {
  const d = new Date()
  return `history-${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}.jsonl`
}

/** 写入一条记录（内存缓冲） */
export function writePoint(name: string, value: number | boolean): void {
  const val = typeof value === 'number' ? value : (value ? 1 : 0)
  const fileName = getFileName()
  const entry = { t: Date.now(), n: name, v: val }
  const arr = buffer.get(fileName) || []
  arr.push(entry)
  buffer.set(fileName, arr)
}

/** 批量写多条 */
export function writePoints(data: Record<string, number | boolean>): void {
  const fileName = getFileName()
  const now = Date.now()
  const arr = buffer.get(fileName) || []
  for (const [name, value] of Object.entries(data)) {
    const val = typeof value === 'number' ? value : (value ? 1 : 0)
    arr.push({ t: now, n: name, v: val })
  }
  buffer.set(fileName, arr)
}

/** 刷盘：将缓冲区写入文件 */
function flush(): void {
  for (const [fileName, entries] of buffer) {
    if (entries.length === 0) continue
    const filePath = path.join(DATA_DIR, fileName)
    try {
      const lines = entries.map(e => JSON.stringify(e)).join('\n') + '\n'
      fs.appendFileSync(filePath, lines, 'utf-8')
      buffer.set(fileName, [])
    } catch (err) {
      console.error(`[History] 写入失败 ${filePath}:`, err)
    }
  }
}

/** 启动定时刷盘 */
export function startFlush(): void {
  if (flushTimer) return
  flushTimer = setInterval(flush, FLUSH_INTERVAL)
}

/** 停止刷盘 */
export function stopFlush(): void {
  if (flushTimer) {
    clearInterval(flushTimer)
    flushTimer = null
  }
  // 最后刷一次
  flush()
}

/**
 * 查询历史数据
 * @param name  变量名
 * @param from  起始时间戳 (ms)
 * @param to    结束时间戳 (ms)
 * @param limit 最大返回条数
 */
export function queryHistory(name: string, from?: number, to?: number, limit: number = 10000): { timestamp: number; value: number }[] {
  const result: { timestamp: number; value: number }[] = []
  const files = fs.readdirSync(DATA_DIR).filter(f => f.startsWith('history-') && f.endsWith('.jsonl')).sort()

  for (const file of files) {
    if (result.length >= limit) break
    const filePath = path.join(DATA_DIR, file)
    try {
      const content = fs.readFileSync(filePath, 'utf-8')
      const lines = content.split('\n').filter(Boolean)
      for (const line of lines) {
        const entry = JSON.parse(line)
        if (entry.n !== name) continue
        if (from !== undefined && entry.t < from) continue
        if (to !== undefined && entry.t > to) continue
        result.push({ timestamp: entry.t, value: entry.v })
        if (result.length >= limit) break
      }
    } catch {}
  }

  // 加上缓冲区中未刷盘的
  for (const [, entries] of buffer) {
    for (const e of entries) {
      if (e.n !== name) continue
      if (from !== undefined && e.t < from) continue
      if (to !== undefined && e.t > to) continue
      result.push({ timestamp: e.t, value: e.v })
    }
  }

  // 按时间排序
  result.sort((a, b) => a.timestamp - b.timestamp)
  return result
}

/**
 * 导出 CSV
 */
export function exportCSV(name: string, from?: number, to?: number): string {
  const data = queryHistory(name, from, to, 50000)
  const rows = ['timestamp,value']
  for (const d of data) {
    rows.push(`${new Date(d.timestamp).toISOString()},${d.value}`)
  }
  return rows.join('\n')
}

// 启动时自动开始刷盘
startFlush()

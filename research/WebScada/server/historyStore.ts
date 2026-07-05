/**
 * 历史数据存储 — 工业级实现
 *
 * 三層架構：
 * 1. 死区检测（Deadband）— 值变化超过阈值才写
 * 2. SQLite 存储（better-sqlite3）— 索引查询毫秒级
 * 3. 降采样（Downsampling）— raw(1h) → _1m(7d) → _1h(30d)
 */

import Database from 'better-sqlite3'
import path from 'path'
import fs from 'fs'

const DB_DIR = path.resolve(import.meta.dirname, '..', 'data', 'history')
const DB_FILE = path.join(DB_DIR, 'history.db')

try { fs.mkdirSync(DB_DIR, { recursive: true }) } catch {}

// ─── 死区配置 ──────────────────────────────────────────
const DEADBAND: Record<string, number> = {}

export function setDeadband(name: string, threshold: number): void {
  if (threshold >= 0) DEADBAND[name] = threshold
}

export function setDeadbands(map: Record<string, number>): void {
  for (const [k, v] of Object.entries(map)) setDeadband(k, v)
}

// ─── SQLite ────────────────────────────────────────────
let db: Database.Database | null = null
try { db = new Database(DB_FILE) } catch {
  console.warn('[history] better-sqlite3 加载失败，历史记录功能不可用')
}
if (db) {
  db.pragma('journal_mode = WAL')
  db.pragma('synchronous = NORMAL')
  db.exec(`
    CREATE TABLE IF NOT EXISTS raw  (name TEXT NOT NULL, ts INTEGER NOT NULL, val REAL NOT NULL);
    CREATE TABLE IF NOT EXISTS _1m   (name TEXT NOT NULL, ts INTEGER NOT NULL, val REAL NOT NULL);
    CREATE TABLE IF NOT EXISTS _1h   (name TEXT NOT NULL, ts INTEGER NOT NULL, val REAL NOT NULL);
    CREATE INDEX IF NOT EXISTS idx_raw  ON raw(name, ts);
    CREATE INDEX IF NOT EXISTS idx_1m   ON _1m(name, ts);
    CREATE INDEX IF NOT EXISTS idx_1h   ON _1h(name, ts);
  `)
}
const noop = () => {}
const insRaw = db?.prepare('INSERT INTO raw VALUES (?, ?, ?)') ?? { run: noop }
const ins1m  = db?.prepare('INSERT OR REPLACE INTO _1m VALUES (?, ?, ?)') ?? { run: noop }
const ins1h  = db?.prepare('INSERT OR REPLACE INTO _1h VALUES (?, ?, ?)') ?? { run: noop }
const delRaw = db?.prepare('DELETE FROM raw WHERE ts < ?') ?? { run: noop }
const del1m  = db?.prepare('DELETE FROM _1m WHERE ts < ?') ?? { run: noop }
const del1h  = db?.prepare('DELETE FROM _1h WHERE ts < ?') ?? { run: noop }

const lastVals = new Map<string, { val: number; ts: number }>()

// ─── 写入（含死区） ────────────────────────────────────
export function writePoint(name: string, value: number | boolean): void {
  const val = typeof value === 'number' ? value : (value ? 1 : 0)
  const th = DEADBAND[name] ?? 0
  const last = lastVals.get(name)
  if (last !== undefined && Math.abs(val - last.val) <= th) return
  lastVals.set(name, { val, ts: Date.now() })
  insRaw.run(name, Date.now(), val)
}

export function writePoints(data: Record<string, number | boolean>): void {
  for (const [name, value] of Object.entries(data)) writePoint(name, value)
}

// ─── 降采样 ────────────────────────────────────────────
let last1m = 0, last1h = 0

function downsample() {
  if (!db) return
  const now = Date.now()
  if (now - last1m >= 60_000) {
    const cutoff = now - 3600_000
    db.transaction(() => {
      const rows = db.prepare(`SELECT name, CAST(ts/60000 AS INTEGER)*60000 AS bucket, AVG(val) AS avg FROM raw WHERE ts<? AND ts>? GROUP BY name,bucket`).all(now, now - 60_000) as any[]
      for (const r of rows) ins1m.run(r.name, r.bucket, r.avg)
      delRaw.run(cutoff)
    })()
    last1m = now
  }
  if (now - last1h >= 3600_000) {
    const cutoff = now - 7 * 86400_000
    db.transaction(() => {
      const rows = db.prepare(`SELECT name, CAST(ts/3600000 AS INTEGER)*3600000 AS bucket, AVG(val) AS avg FROM _1m WHERE ts<? AND ts>? GROUP BY name,bucket`).all(now, now - 3600_000) as any[]
      for (const r of rows) ins1h.run(r.name, r.bucket, r.avg)
      del1m.run(cutoff)
    })()
    last1h = now
  }
  del1h.run(now - 30 * 86400_000)
}

// ─── 定时刷盘 ──────────────────────────────────────────
let timer: ReturnType<typeof setInterval> | null = null

export function startFlush(): void {
  if (!db || timer) return
  timer = setInterval(downsample, 10_000)
}

export function stopFlush(): void {
  if (timer) { clearInterval(timer); timer = null }
}

// ─── 查询（自动选表） ─────────────────────────────────
export function queryHistory(name: string, from?: number, to?: number, limit = 10000): { timestamp: number; value: number }[] {
  if (!db) return []
  const tFrom = from ?? 0, tTo = to ?? Date.now()
  const span = tTo - tFrom
  const table = span > 7 * 86400_000 ? '_1h' : span > 3600_000 ? '_1m' : 'raw'
  const rows = db.prepare(`SELECT ts, val FROM ${table} WHERE name=? AND ts BETWEEN ? AND ? ORDER BY ts LIMIT ?`).all(name, tFrom, tTo, limit) as { ts: number; val: number }[]
  return rows.map(r => ({ timestamp: r.ts, value: r.val }))
}

export function exportCSV(name: string, from?: number, to?: number): string {
  const data = queryHistory(name, from, to, 50000)
  return 'timestamp,value\n' + data.map(d => `${new Date(d.timestamp).toISOString()},${d.value}`).join('\n')
}

startFlush()

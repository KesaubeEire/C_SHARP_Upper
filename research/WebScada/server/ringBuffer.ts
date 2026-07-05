/**
 * 环形缓冲区 — 存储实时数据的时序快照
 *
 * 每个 poll/订阅回调写入一个快照 { timestamp, values }
 * 前端可查询最近 N 秒数据用于画趋势曲线
 */

const DEFAULT_CAPACITY = 600 // 600 个采样点 (10min @ 1s)

interface Snapshot {
  timestamp: number
  values: Record<string, number | boolean>
}

export class RingBuffer {
  private buffer: Snapshot[]
  private capacity: number
  private _count: number

  constructor(capacity: number = DEFAULT_CAPACITY) {
    this.buffer = new Array(capacity)
    this.capacity = capacity
    this._count = 0
  }

  /** 写入一个快照 */
  push(values: Record<string, number | boolean>): void {
    const idx = this._count % this.capacity
    this.buffer[idx] = { timestamp: Date.now(), values: { ...values } }
    this._count++
  }

  /** 当前有效记录数 */
  get count(): number {
    return Math.min(this._count, this.capacity)
  }

  /** 已写满过一轮 */
  get wrapped(): boolean {
    return this._count >= this.capacity
  }

  /**
   * 查询某个变量的历史数据
   * @param name  变量名
   * @param from  起始时间戳 (ms), 不传则取全部
   * @param to    结束时间戳 (ms)
   * @returns  [{ timestamp, value }]
   */
  query(name: string, from?: number, to?: number): { timestamp: number; value: number | boolean | null }[] {
    const result: { timestamp: number; value: number | boolean | null }[] = []
    const len = this.count
    if (len === 0) return result

    const start = this.wrapped ? this._count % this.capacity : 0
    for (let i = 0; i < len; i++) {
      const idx = (start + i) % this.capacity
      const snap = this.buffer[idx]
      if (!snap) continue
      if (from !== undefined && snap.timestamp < from) continue
      if (to !== undefined && snap.timestamp > to) continue
      const val = snap.values[name]
      result.push({ timestamp: snap.timestamp, value: val ?? null })
    }
    return result
  }

  /** 查询多个变量的最新 N 个采样点 */
  queryLatest(names: string[], count: number = 100): Record<string, { timestamp: number; value: number | boolean | null }[]> {
    const len = this.count
    const actual = Math.min(count, len)
    const result: Record<string, { timestamp: number; value: number | boolean | null }[]> = {}
    for (const name of names) result[name] = []

    const start = this.wrapped ? (this._count % this.capacity) : 0
    const beginIdx = len - actual
    for (let i = beginIdx; i < len; i++) {
      const idx = (start + i) % this.capacity
      const snap = this.buffer[idx]
      if (!snap) continue
      for (const name of names) {
        const val = snap.values[name]
        result[name].push({ timestamp: snap.timestamp, value: val ?? null })
      }
    }
    return result
  }

  /** 清空 */
  clear(): void {
    this.buffer = new Array(this.capacity)
    this._count = 0
  }
}

/** 全局实例（供 index.ts 用） */
export const trendBuffer = new RingBuffer()

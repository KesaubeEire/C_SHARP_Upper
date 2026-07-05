/**
 * vPLC 运行时
 * OB 周期管理、模拟数据生成、用户脚本引擎
 */

import { memory, ensureDbSize } from './plc-memory.js'
import { isRunning, addDiag } from './plc-state.js'
import type { OBCycle } from './types.js'

// ─── OB 周期定义 ──

export const obCycles: OBCycle[] = [
  { num: 1,   name: 'OB1',   type: 'freecycle', intervalMs: 0,   runCount: 0, lastRun: 0, errors: 0, lastExecuteMs: 0, state: 'waiting' },
  { num: 35,  name: 'OB35',  type: 'cyclic',    intervalMs: 500, runCount: 0, lastRun: 0, errors: 0, lastExecuteMs: 0, state: 'waiting' },
  { num: 100, name: 'OB100', type: 'startup',   intervalMs: 0,   runCount: 0, lastRun: 0, errors: 0, lastExecuteMs: 0, state: 'waiting' },
]

export function resetAllOBs() {
  for (const ob of obCycles) {
    ob.runCount = 0
    ob.errors = 0
    ob.lastRun = 0
    ob.lastExecuteMs = 0
    ob.state = 'waiting'
  }
}

// ─── OB 执行 ──

function executeOB(ob: OBCycle): void {
  try {
    ob.state = 'running'
    const start = Date.now()

    if (ob.num === 100) {
      // OB100 (Startup): 复位 M 区/Q 区
      memory.MK[0] = 0
      memory.PA[0] = 0
    }

    // 执行用户脚本（如果配置了）
    executeScripts(ob)

    ob.lastExecuteMs = Date.now() - start
    ob.runCount++
    ob.state = 'finished'
  } catch {
    ob.errors++
    ob.state = 'error'
  }
  ob.lastRun = Date.now()
}

function runOBCycles(now: number): void {
  for (const ob of obCycles) {
    if (ob.type === 'startup') continue
    if (ob.num === 1) {
      executeOB(ob)
    } else if (ob.type === 'cyclic' && (now - ob.lastRun >= ob.intervalMs)) {
      executeOB(ob)
    }
  }
}

// ─── 模拟数据生成 ──

function simulateData(): void {
  if (!isRunning()) return
  const now = Date.now()

  // DB7 温度、压力波动
  const db7 = memory.DB[7]
  if (db7 && db7.length >= 50) {
    const dv7 = new DataView(db7.buffer, db7.byteOffset, db7.byteLength)
    dv7.setFloat32(38, 25 + Math.sin(now / 3000) * 3 + Math.random() * 0.5, false)
    dv7.setFloat32(42, 0.5 + Math.sin(now / 5000) * 0.2 + Math.random() * 0.05, false)
  }

  // DB6 位置波动
  const db6 = memory.DB[6]
  if (db6 && db6.length >= 50) {
    const dv6 = new DataView(db6.buffer, db6.byteOffset, db6.byteLength)
    dv6.setFloat32(38, Math.max(0, Math.min(100, (Math.sin(now / 2000) + 1) * 50)), false)
  }

  // I0.0-I0.3 间歇变化
  memory.PE[0] = (Math.floor(now / 800) % 2) * 0x01
                | (Math.floor(now / 1500) % 2) * 0x02
                | (Math.floor(now / 2200) % 2) * 0x04
                | (Math.floor(now / 3000) % 2) * 0x08

  // Q8 模拟
  const qb8 = memory.PA[8]
  if (qb8 & 0b00000100) {
    const cycle = Math.floor(now / 1200) % 4
    memory.PE[8] = (memory.PE[8] & 0xF0) | (cycle === 0 || cycle === 2 ? 0x08 : 0x00)
  }
}

// ─── 步骤 3: 用户脚本引擎 ──────────────────────────────

import vm from 'vm'

export interface ScriptConfig {
  /** 脚本内容 */
  source: string
  /** 关联的 OB 号（1=OB1, 35=OB35, 等） */
  obNumber: number
  /** 启用/禁用 */
  enabled: boolean
  /** 脚本名称（用于显示） */
  name: string
}

let userScripts: ScriptConfig[] = []

/**
 * 设置用户脚本列表
 */
export function setUserScripts(scripts: ScriptConfig[]) {
  userScripts = scripts
}

export function getUserScripts(): ScriptConfig[] {
  return userScripts
}

/** 上下文中可用的变量读写 API */
export interface ScriptContext {
  /** 读取字节：I/Q/M/DB */
  readByte: (area: 'I' | 'Q' | 'M' | 'DB', dbNumber: number, offset: number) => number
  /** 写入字节 */
  writeByte: (area: 'I' | 'Q' | 'M' | 'DB', dbNumber: number, offset: number, value: number) => void
  /** 读取位 */
  readBit: (area: 'I' | 'Q' | 'M' | 'DB', dbNumber: number, offset: number, bit: number) => boolean
  /** 写入位 */
  writeBit: (area: 'I' | 'Q' | 'M' | 'DB', dbNumber: number, offset: number, bit: number, value: boolean) => void
  /** 读取 REAL */
  readReal: (area: 'DB', dbNumber: number, offset: number) => number
  /** 写入 REAL */
  writeReal: (area: 'DB', dbNumber: number, offset: number, value: number) => void
  /** 读取 INT */
  readInt: (area: 'I' | 'Q' | 'M' | 'DB', dbNumber: number, offset: number) => number
  /** 写入 INT */
  writeInt: (area: 'I' | 'Q' | 'M' | 'DB', dbNumber: number, offset: number, value: number) => void
  /** 打印日志 */
  log: (...args: any[]) => void
  /** 获取当前 RTC 时间戳 */
  now: () => number
  /** 获取当前仿真 tick (ms) */
  tick: () => number
}

function buildScriptContext(ob: OBCycle): ScriptContext {
  const readBuf = (area: string, dbNum: number): Uint8Array | undefined => {
    if (area === 'I') return memory.PE
    if (area === 'Q') return memory.PA
    if (area === 'M') return memory.MK
    if (area === 'DB') return ensureDbSize(dbNum, 64)
    return undefined
  }

  return {
    readByte(area, dbNum, offset) {
      const buf = readBuf(area, dbNum)
      return (buf && offset < buf.length) ? buf[offset] : 0
    },
    writeByte(area, dbNum, offset, value) {
      const buf = readBuf(area, dbNum)
      if (buf && offset < buf.length) buf[offset] = value & 0xFF
    },
    readBit(area, dbNum, offset, bit) {
      const buf = readBuf(area, dbNum)
      return !!(buf && offset < buf.length && (buf[offset] & (1 << bit)))
    },
    writeBit(area, dbNum, offset, bit, value) {
      const buf = readBuf(area, dbNum)
      if (buf && offset < buf.length) {
        if (value) buf[offset] |= (1 << bit)
        else buf[offset] &= ~(1 << bit)
      }
    },
    readReal(area, dbNum, offset) {
      const buf = readBuf(area, dbNum)
      if (!buf || offset + 4 > buf.length) return 0
      return new DataView(buf.buffer, buf.byteOffset + offset, 4).getFloat32(0, false)
    },
    writeReal(area, dbNum, offset, value) {
      const buf = readBuf(area, dbNum)
      if (buf && offset + 4 <= buf.length) {
        new DataView(buf.buffer, buf.byteOffset + offset, 4).setFloat32(0, value, false)
      }
    },
    readInt(area, dbNum, offset) {
      const buf = readBuf(area, dbNum)
      if (!buf || offset + 2 > buf.length) return 0
      return new DataView(buf.buffer, buf.byteOffset + offset, 2).getInt16(0, false)
    },
    writeInt(area, dbNum, offset, value) {
      const buf = readBuf(area, dbNum)
      if (buf && offset + 2 <= buf.length) {
        new DataView(buf.buffer, buf.byteOffset + offset, 2).setInt16(0, Math.trunc(value), false)
      }
    },
    log: (...args: any[]) => console.log(`[Script:${ob.name}]`, ...args),
    now: () => Date.now(),
    tick: () => Date.now(),
  }
}

function executeScripts(ob: OBCycle) {
  const scriptsForOB = userScripts.filter(s => s.enabled && s.obNumber === ob.num)
  if (scriptsForOB.length === 0) return

  const ctx = buildScriptContext(ob)

  for (const script of scriptsForOB) {
    try {
      const wrappedSrc = `(function(ctx) { with(ctx) { ${script.source} } })`
      const fn = vm.runInNewContext(wrappedSrc, { ...ctx }, {
        filename: `script:${script.name}`,
        timeout: 100, // 超时 100ms 防止死循环
      })
      fn(ctx)
    } catch (err) {
      addDiag('warn', `SCRIPT:${script.name}`, `脚本执行错误: ${(err as Error).message}`)
    }
  }
}

// ─── 主模拟循环 ──

let _simTimer: ReturnType<typeof setInterval> | null = null

export function startRuntime() {
  if (_simTimer) return

  // 启动时执行 OB100
  const ob100 = obCycles.find(o => o.num === 100)
  if (ob100) executeOB(ob100)

  _simTimer = setInterval(() => {
    const now = Date.now()
    runOBCycles(now)
    simulateData()
  }, 500)
}

export function stopRuntime() {
  if (_simTimer) {
    clearInterval(_simTimer)
    _simTimer = null
  }
}

/**
 * 初始化 DB6/DB7 默认值（用于模拟）
 */
export function initSimulationDBs() {
  if (memory.DB[6] && memory.DB[6].length >= 50) {
    const dv = new DataView(memory.DB[6].buffer, memory.DB[6].byteOffset, memory.DB[6].byteLength)
    dv.setFloat32(38, 0, false)   // position
    dv.setFloat32(42, 0, false)   // target
    dv.setFloat32(46, 0, false)   // speed
  }
  if (memory.DB[7] && memory.DB[7].length >= 50) {
    const dv = new DataView(memory.DB[7].buffer, memory.DB[7].byteOffset, memory.DB[7].byteLength)
    dv.setUint8(0, 0b00000000)    // X0.0-X0.7
    dv.setFloat32(38, 25, false)  // temp
    dv.setFloat32(42, 0.5, false) // pressure
  }
}

export function getRuntimeSnapshot() {
  const db6 = memory.DB[6]
  const db7 = memory.DB[7]
  const dv6 = db6 && db6.length >= 50 ? new DataView(db6.buffer, db6.byteOffset, db6.byteLength) : null
  const dv7 = db7 && db7.length >= 50 ? new DataView(db7.buffer, db7.byteOffset, db7.byteLength) : null

  return {
    DB6: dv6 ? {
      position: dv6.getFloat32(38, false).toFixed(2),
      target: dv6.getFloat32(42, false).toFixed(2),
      speed: dv6.getFloat32(46, false).toFixed(2),
    } : {},
    DB7: dv7 ? {
      startBtn: !!(dv7.getUint8(0) & 0x01),
      stopBtn: !!(dv7.getUint8(0) & 0x02),
      running: !!(dv7.getUint8(0) & 0x04),
      alarm: !!(dv7.getUint8(0) & 0x08),
      sensorA: !!(memory.PE[8] & 0x08),
      sensorB: !!(memory.PE[8] & 0x04),
      valve: !!(memory.PA[8] & 0x20),
      temp: dv7 ? dv7.getFloat32(38, false).toFixed(2) : '--',
      pressure: dv7 ? dv7.getFloat32(42, false).toFixed(2) : '--',
    } : {},
    Q: {
      QB8: memory.PA[8],
      bits: Array.from({ length: 8 }, (_, i) => !!(memory.PA[8] & (1 << i))),
    },
  }
}

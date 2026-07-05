/**
 * vPLC 状态管理
 * RUN/STOP, RTC, LED, 诊断缓冲区
 */

import type { PlcStateType, PLCLED, DiagEntry } from './types.js'

// ─── RUN/STOP ──
export let plcState: PlcStateType = 'RUN'
export let stateChangedAt = Date.now()

export function setPlcState(s: PlcStateType) {
  plcState = s
  stateChangedAt = Date.now()
  updateLEDs()
}

export function isRunning(): boolean { return plcState === 'RUN' }

// ─── RTC ──
export let rtcOffset = 0

export function setRtcOffset(offset: number) { rtcOffset = offset }

export function getRtcIso(): string {
  return new Date(Date.now() + rtcOffset).toISOString()
}

export function getRtcMs(): number {
  return Date.now() + rtcOffset
}

// ─── LED ──
export const plcLEDs: Record<string, PLCLED> = {
  RUN: { color: 'green', state: 'on' },
  STOP: { color: 'orange', state: 'off' },
  ERROR: { color: 'red', state: 'off' },
  MAINT: { color: 'yellow', state: 'off' },
}

export function updateLEDs() {
  if (plcState === 'RUN') {
    plcLEDs.RUN.state = 'on'
    plcLEDs.STOP.state = 'off'
    plcLEDs.ERROR.state = 'off'
    plcLEDs.MAINT.state = 'off'
  } else if (plcState === 'STOP') {
    plcLEDs.RUN.state = 'off'
    plcLEDs.STOP.state = 'on'
    plcLEDs.ERROR.state = 'off'
    plcLEDs.MAINT.state = 'off'
  } else {
    plcLEDs.RUN.state = 'blink'
    plcLEDs.STOP.state = 'off'
    plcLEDs.ERROR.state = 'off'
    plcLEDs.MAINT.state = 'off'
  }
}

export function getLedsSnapshot() {
  return Object.fromEntries(
    Object.entries(plcLEDs).map(([k, v]) => [k.toLowerCase(), { ...v }])
  )
}

// ─── 诊断缓冲区 ──
const MAX_DIAG = 200
const diagBuffer: DiagEntry[] = []
let diagId = 0

export function addDiag(cat: string, src: string, msg: string, det?: string) {
  diagBuffer.unshift({ id: ++diagId, timestamp: Date.now(), category: cat, source: src, message: msg, detail: det })
  if (diagBuffer.length > MAX_DIAG) diagBuffer.length = MAX_DIAG
}

export function getDiagBuffer(): DiagEntry[] { return diagBuffer }

export function clearDiagBuffer() { diagBuffer.length = 0; diagId = 0 }

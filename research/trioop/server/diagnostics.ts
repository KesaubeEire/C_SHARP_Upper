/**
 * 系统诊断 — 通信统计、健康监控
 */

let _pollCount = 0
let _errorCount = 0
let _lastPollTime = 0
let _lastErrorTime = 0
let _lastErrorMsg = ''
let _responseTimes: number[] = []
let _startTime = Date.now()

export function recordPoll(durationMs: number): void {
  _pollCount++
  _lastPollTime = Date.now()
  _responseTimes.push(durationMs)
  if (_responseTimes.length > 200) _responseTimes.shift()
}

export function recordError(msg: string): void {
  _errorCount++
  _lastErrorTime = Date.now()
  _lastErrorMsg = msg
}

export function getDiagnostics() {
  const avg = _responseTimes.length > 0
    ? _responseTimes.reduce((a, b) => a + b, 0) / _responseTimes.length
    : 0
  const max = _responseTimes.length > 0 ? Math.max(..._responseTimes) : 0
  const uptime = Math.floor((Date.now() - _startTime) / 1000)

  return {
    uptime,
    pollCount: _pollCount,
    errorCount: _errorCount,
    lastPollTime: _lastPollTime || null,
    lastErrorTime: _lastErrorTime || null,
    lastError: _lastErrorMsg || null,
    avgResponseMs: Math.round(avg * 100) / 100,
    maxResponseMs: Math.round(max * 100) / 100,
    sampleCount: _responseTimes.length,
  }
}

export function resetDiagnostics(): void {
  _pollCount = 0
  _errorCount = 0
  _lastPollTime = 0
  _lastErrorTime = 0
  _lastErrorMsg = ''
  _responseTimes = []
  _startTime = Date.now()
}

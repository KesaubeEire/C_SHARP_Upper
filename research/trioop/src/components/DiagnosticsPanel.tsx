import { useState, useEffect, useCallback } from 'react'
import CollapsibleSection from './CollapsibleSection'

export default function DiagnosticsPanel() {
  const [diag, setDiag] = useState<any>(null)

  const load = useCallback(async () => {
    try { setDiag(await (await fetch('/api/diagnostics')).json()) } catch {}
  }, [])

  useEffect(() => { load(); const t = setInterval(load, 1000); return () => clearInterval(t) }, [load])

  const handleReset = async () => {
    await fetch('/api/diagnostics/reset', { method: 'POST' })
    load()
  }

  if (!diag) return null

  const uptimeStr = diag.uptime >= 86400
    ? `${Math.floor(diag.uptime / 86400)}d ${Math.floor((diag.uptime % 86400) / 3600)}h`
    : diag.uptime >= 3600
      ? `${Math.floor(diag.uptime / 3600)}h ${Math.floor((diag.uptime % 3600) / 60)}m`
      : `${Math.floor(diag.uptime / 60)}m ${diag.uptime % 60}s`

  return (
    <CollapsibleSection title="🩺 系统诊断" storageKey="diagnostics">
      <div className="diag-grid">
        <div className="diag-item">
          <span className="diag-item__label">运行时间</span>
          <span className="diag-item__value">{uptimeStr}</span>
        </div>
        <div className="diag-item">
          <span className="diag-item__label">轮询次数</span>
          <span className="diag-item__value">{diag.pollCount}</span>
        </div>
        <div className="diag-item">
          <span className="diag-item__label">错误次数</span>
          <span className="diag-item__value" style={{ color: diag.errorCount > 0 ? '#ef5350' : '#4caf50' }}>{diag.errorCount}</span>
        </div>
        <div className="diag-item">
          <span className="diag-item__label">平均响应</span>
          <span className="diag-item__value">{diag.avgResponseMs}ms</span>
        </div>
        <div className="diag-item">
          <span className="diag-item__label">最大响应</span>
          <span className="diag-item__value">{diag.maxResponseMs}ms</span>
        </div>
        <div className="diag-item">
          <span className="diag-item__label">采样数</span>
          <span className="diag-item__value">{diag.sampleCount}</span>
        </div>
      </div>
      {diag.lastError && (
        <div className="diag-error">
          <span className="diag-error__time">{diag.lastErrorTime ? new Date(diag.lastErrorTime).toLocaleTimeString() : ''}</span>
          <span className="diag-error__msg">{diag.lastError}</span>
        </div>
      )}
      <button className="btn btn--sm btn--ghost" style={{ marginTop: 8 }} onClick={handleReset}>重置统计</button>
    </CollapsibleSection>
  )
}
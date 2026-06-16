import { useRef, useEffect, useState, useCallback } from 'react'

const CHART_COLORS = ['#2196f3', '#ff5722', '#4caf50', '#ff9800', '#9c27b0', '#00bcd4', '#e91e63', '#607d8b']

interface TrendChartProps {
  /** 要显示的变量名列表 */
  variables: string[]
  /** 实时数据（来自 SSE） */
  liveData?: Record<string, { value: number | boolean }>
  /** 可见时间范围（秒） */
  timeRange?: number
}

export default function TrendChart({ variables, liveData, timeRange = 300 }: TrendChartProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const dataRef = useRef<Map<string, { t: number; v: number }[]>>(new Map())
  const animRef = useRef<number>(0)
  const [paused, setPaused] = useState(false)
  const [loaded, setLoaded] = useState(false)

  // 启动时加载历史数据
  useEffect(() => {
    if (variables.length === 0) return
    const names = variables.join(',')
    fetch(`/api/trend?names=${encodeURIComponent(names)}&count=300`)
      .then(r => r.json())
      .then(res => {
        if (res.data) {
          for (const name of variables) {
            const pts = res.data[name] || []
            const arr: { t: number; v: number }[] = []
            for (const p of pts) {
              if (p.value !== null && p.value !== undefined) {
                arr.push({ t: p.timestamp, v: typeof p.value === 'number' ? p.value : (p.value ? 1 : 0) })
              }
            }
            dataRef.current.set(name, arr)
          }
          setLoaded(true)
        }
      })
      .catch(() => setLoaded(true))
  }, [variables.join(',')])

  // 将 liveData 追加到缓冲区
  useEffect(() => {
    if (paused || !liveData || variables.length === 0) return
    const now = Date.now()
    for (const name of variables) {
      const pt = liveData[name]
      if (pt === undefined || pt.value === undefined || pt.value === null) continue
      const arr = dataRef.current.get(name) || []
      const val = typeof pt.value === 'number' ? pt.value : (pt.value ? 1 : 0)
      arr.push({ t: now, v: val })
      // 只保留 timeRange 内的数据
      const cutoff = now - timeRange * 1000
      while (arr.length > 0 && arr[0].t < cutoff) arr.shift()
      dataRef.current.set(name, arr)
    }
  }, [liveData, paused, variables, timeRange])

  // Canvas 绘制
  const draw = useCallback(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    const { width, height } = canvas
    const pad = { top: 10, right: 15, bottom: 25, left: 55 }
    const plotW = width - pad.left - pad.right
    const plotH = height - pad.top - pad.bottom
    const now = Date.now()
    const rangeMs = timeRange * 1000
    const cutoff = now - rangeMs

    ctx.clearRect(0, 0, width, height)
    ctx.font = '11px sans-serif'

    // 收集所有可见数据
    const series: { name: string; pts: { t: number; v: number }[]; color: string }[] = []
    let minY = Infinity, maxY = -Infinity
    for (let i = 0; i < variables.length; i++) {
      const name = variables[i]
      const pts = dataRef.current.get(name) || []
      const filtered = pts.filter(p => p.t >= cutoff)
      if (filtered.length === 0) continue
      for (const p of filtered) {
        if (p.v < minY) minY = p.v
        if (p.v > maxY) maxY = p.v
      }
      series.push({ name, pts: filtered, color: CHART_COLORS[i % CHART_COLORS.length] })
    }

    if (series.length === 0 || minY === Infinity) {
      ctx.fillStyle = '#999'
      ctx.textAlign = 'center'
      ctx.fillText('等待数据...', width / 2, height / 2)
      return
    }

    // Y 轴范围
    const yRange = maxY - minY
    const yPad = yRange === 0 ? 1 : yRange * 0.1
    const yMin = minY - yPad
    const yMax = maxY + yPad

    // 网格线
    ctx.strokeStyle = '#e8e8e8'
    ctx.lineWidth = 1
    const gridLines = 4
    for (let i = 0; i <= gridLines; i++) {
      const y = pad.top + (plotH * i) / gridLines
      ctx.beginPath()
      ctx.moveTo(pad.left, y)
      ctx.lineTo(width - pad.right, y)
      ctx.stroke()
      // Y 轴标签
      const val = yMax - ((yMax - yMin) * i) / gridLines
      ctx.fillStyle = '#666'
      ctx.textAlign = 'right'
      ctx.fillText(val.toFixed(1), pad.left - 4, y + 4)
    }

    // X 轴时间标签
    ctx.textAlign = 'center'
    ctx.fillStyle = '#666'
    const xLabels = 5
    for (let i = 0; i < xLabels; i++) {
      const t = now - (rangeMs * (xLabels - 1 - i)) / (xLabels - 1)
      const x = pad.left + (plotW * i) / (xLabels - 1)
      const d = new Date(t)
      ctx.fillText(`${d.getMinutes().toString().padStart(2, '0')}:${d.getSeconds().toString().padStart(2, '0')}`, x, height - 6)
    }

    // 绘制每条曲线
    for (const s of series) {
      if (s.pts.length < 2) continue
      ctx.strokeStyle = s.color
      ctx.lineWidth = 2
      ctx.beginPath()
      for (let i = 0; i < s.pts.length; i++) {
        const x = pad.left + ((s.pts[i].t - cutoff) / rangeMs) * plotW
        const y = pad.top + plotH - ((s.pts[i].v - yMin) / (yMax - yMin)) * plotH
        if (i === 0) ctx.moveTo(x, y)
        else ctx.lineTo(x, y)
      }
      ctx.stroke()

      // 最新值标签
      const last = s.pts[s.pts.length - 1]
      const lx = pad.left + ((last.t - cutoff) / rangeMs) * plotW
      const ly = pad.top + plotH - ((last.v - yMin) / (yMax - yMin)) * plotH
      ctx.fillStyle = s.color
      ctx.font = 'bold 11px sans-serif'
      ctx.textAlign = 'left'
      ctx.fillText(`${s.name}=${typeof last.v === 'number' ? last.v.toFixed(1) : last.v}`, Math.min(lx + 6, width - pad.right - 80), Math.max(ly - 4, pad.top + 10))
    }
  }, [variables, timeRange])

  // 动画循环
  useEffect(() => {
    let running = true
    function loop() {
      if (!running) return
      draw()
      animRef.current = requestAnimationFrame(loop)
    }
    loop()
    return () => { running = false; cancelAnimationFrame(animRef.current) }
  }, [draw])

  // 画布大小自适应
  useEffect(() => {
    function resize() {
      const canvas = canvasRef.current
      if (!canvas || !canvas.parentElement) return
      const rect = canvas.parentElement.getBoundingClientRect()
      canvas.width = rect.width * devicePixelRatio
      canvas.height = rect.height * devicePixelRatio
      canvas.style.width = rect.width + 'px'
      canvas.style.height = rect.height + 'px'
      const ctx = canvas.getContext('2d')
      if (ctx) ctx.scale(devicePixelRatio, devicePixelRatio)
    }
    resize()
    window.addEventListener('resize', resize)
    return () => window.removeEventListener('resize', resize)
  }, [])

  if (variables.length === 0) return null

  return (
    <div className="trend-chart">
      <div className="trend-chart__bar">
        <span className="trend-chart__title">📈 实时趋势</span>
        <span className="trend-chart__vars">
          {variables.map((v, i) => (
            <span key={v} className="trend-chart__legend" style={{ color: CHART_COLORS[i % CHART_COLORS.length] }}>● {v}</span>
          ))}
        </span>
        <button className={`btn btn--sm ${paused ? 'btn--primary' : 'btn--ghost'}`} onClick={() => setPaused(!paused)}>
          {paused ? '▶ 继续' : '⏸ 暂停'}
        </button>
      </div>
      <div className="trend-chart__canvas-wrap">
        <canvas ref={canvasRef} />
        {!loaded && <div className="trend-chart__loading">加载历史数据...</div>}
      </div>
    </div>
  )
}

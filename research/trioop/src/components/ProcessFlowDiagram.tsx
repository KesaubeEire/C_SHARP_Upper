import { useRef, useState, useEffect, useCallback } from 'react'

type PFDNodeType = 'tank' | 'pump' | 'valve' | 'heat-exchanger' | 'column' | 'compressor' | 'instrument'

interface PFDNode {
  id: string; type: PFDNodeType; x: number; y: number; label?: string
}

interface PFDEdge {
  from: string; to: string; label?: string
}

interface PFDProps {
  nodes: PFDNode[]
  edges: PFDEdge[]
  values?: Record<string, number>
  width?: number
  height?: number
}

const STORAGE_KEY = 'trioop_pfd_view'
const NODE_W = 64, NODE_H = 64

function loadView() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return JSON.parse(raw)
  } catch {}
  return { x: 0, y: 0, zoom: 1 }
}

function saveView(x: number, y: number, zoom: number) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify({ x, y, zoom }))
}

function computePath(fx: number, fy: number, tx: number, ty: number): string {
  const dx = tx - fx, dy = ty - fy
  const mx = (fx + tx) / 2, my = (fy + ty) / 2
  if (Math.abs(dx) > Math.abs(dy)) {
    return `M ${fx} ${fy} L ${mx} ${fy} L ${mx} ${ty} L ${tx} ${ty}`
  } else {
    return `M ${fx} ${fy} L ${fx} ${my} L ${tx} ${my} L ${tx} ${ty}`
  }
}

/* ─── 设备符号 ────────────────────────────────────────── */
function Tank({ x, y }: { x: number; y: number }) {
  const cx = x + NODE_W / 2, top = y + 8, bot = y + NODE_H - 8
  return (<g>
    <ellipse cx={cx} cy={top} rx={28} ry={8} fill="#e3f2fd" stroke="#1565c0" strokeWidth="1.5" />
    <rect x={x + 4} y={top} width={NODE_W - 8} height={bot - top} fill="#e3f2fd" stroke="#1565c0" strokeWidth="1.5" />
    <line x1={cx} y1={top + 4} x2={cx} y2={bot - 4} stroke="#90caf9" strokeWidth="1" strokeDasharray="3 2" />
    <ellipse cx={cx} cy={bot} rx={28} ry={8} fill="#e3f2fd" stroke="#1565c0" strokeWidth="1.5" />
  </g>)
}

function Pump({ x, y }: { x: number; y: number }) {
  const cx = x + NODE_W / 2, cy = y + NODE_H / 2
  return (<g>
    <circle cx={cx} cy={cy} r={24} fill="#fff3e0" stroke="#e65100" strokeWidth="1.5" />
    <circle cx={cx} cy={cy} r={16} fill="none" stroke="#e65100" strokeWidth="0.8" />
    <polygon points={`${cx - 6},${cy - 10} ${cx - 6},${cy + 10} ${cx + 12},${cy}`} fill="#e65100" />
    <line x1={cx - 28} y1={cy} x2={cx - 24} y2={cy} stroke="#e65100" strokeWidth="1.5" />
    <line x1={cx + 24} y1={cy} x2={cx + 28} y2={cy} stroke="#e65100" strokeWidth="1.5" />
  </g>)
}

function Valve({ x, y }: { x: number; y: number }) {
  const cx = x + NODE_W / 2, cy = y + NODE_H / 2
  return (<g>
    <line x1={cx - 20} y1={cy} x2={cx + 20} y2={cy} stroke="#333" strokeWidth="1.5" />
    <polygon points={`${cx - 14},${cy} ${cx},${cy - 14} ${cx + 14},${cy} ${cx},${cy + 14}`} fill="#e8f5e9" stroke="#2e7d32" strokeWidth="1.5" />
    <line x1={cx} y1={cy - 14} x2={cx} y2={cy + 14} stroke="#2e7d32" strokeWidth="1.2" />
  </g>)
}

function HeatExchanger({ x, y }: { x: number; y: number }) {
  const w = NODE_W + 16, h = NODE_H - 20
  return (<g>
    <rect x={x} y={y + 10} width={w} height={h} rx={3} fill="#fce4ec" stroke="#c62828" strokeWidth="1.5" />
    <line x1={x + 14} y1={y + 10} x2={x + 14} y2={y + 10 + h} stroke="#c62828" strokeWidth="1" />
    <line x1={x + w - 14} y1={y + 10} x2={x + w - 14} y2={y + 10 + h} stroke="#c62828" strokeWidth="1" />
    <path d={`M ${x + 6} ${y + 16 + h / 2 - 8} L ${x + w - 6} ${y + 16 + h / 2 + 8} M ${x + 6} ${y + 16 + h / 2 + 8} L ${x + w - 6} ${y + 16 + h / 2 - 8}`} stroke="#ef9a9a" strokeWidth="1" />
    <text x={x + w / 2} y={y + 10 + h / 2 + 4} textAnchor="middle" fill="#c62828" fontSize="11" fontWeight="600">E</text>
  </g>)
}

function Column({ x, y }: { x: number; y: number }) {
  const cx = x + NODE_W / 2
  return (<g>
    <rect x={x + 10} y={y} width={NODE_W - 20} height={NODE_H} fill="#e8eaf6" stroke="#283593" strokeWidth="1.5" rx={2} />
    <ellipse cx={cx} cy={y} rx={NODE_W / 2 - 10} ry={6} fill="#e8eaf6" stroke="#283593" strokeWidth="1.5" />
    <ellipse cx={cx} cy={y + NODE_H} rx={NODE_W / 2 - 10} ry={6} fill="#e8eaf6" stroke="#283593" strokeWidth="1.5" />
  </g>)
}

function Compressor({ x, y }: { x: number; y: number }) {
  const cx = x + NODE_W / 2, cy = y + NODE_H / 2
  return (<g>
    <circle cx={cx} cy={cy} r={22} fill="#f3e5f5" stroke="#6a1b9a" strokeWidth="1.5" />
    <path d={`M ${cx - 8} ${cy + 10} L ${cx + 8} ${cy} L ${cx - 8} ${cy - 10} Z`} fill="none" stroke="#6a1b9a" strokeWidth="1.5" />
    <line x1={cx - 26} y1={cy} x2={cx - 22} y2={cy} stroke="#6a1b9a" strokeWidth="1.5" />
    <line x1={cx + 22} y1={cy} x2={cx + 26} y2={cy} stroke="#6a1b9a" strokeWidth="1.5" />
  </g>)
}

function Instrument({ x, y }: { x: number; y: number }) {
  const cx = x + NODE_W / 2, cy = y + NODE_H / 2
  return (<g>
    <circle cx={cx} cy={cy} r={16} fill="#fff" stroke="#333" strokeWidth="1.5" />
    <line x1={cx} y1={cy - 16} x2={cx} y2={cy + 16} stroke="#ccc" strokeWidth="0.5" />
    <line x1={cx - 16} y1={cy} x2={cx + 16} y2={cy} stroke="#ccc" strokeWidth="0.5" />
  </g>)
}

const SYMBOLS: Record<PFDNodeType, React.FC<{ x: number; y: number }>> = {
  tank: Tank, pump: Pump, valve: Valve, 'heat-exchanger': HeatExchanger,
  column: Column, compressor: Compressor, instrument: Instrument,
}

const LABEL_OFFSETS: Record<PFDNodeType, { dy: number; fontSize: number }> = {
  tank: { dy: NODE_H + 4, fontSize: 11 },
  pump: { dy: 34, fontSize: 10 },
  valve: { dy: 28, fontSize: 10 },
  'heat-exchanger': { dy: NODE_H - 8, fontSize: 10 },
  column: { dy: -8, fontSize: 10 },
  compressor: { dy: 32, fontSize: 10 },
  instrument: { dy: -2, fontSize: 10 },
}

export default function ProcessFlowDiagram({ nodes, edges, values = {}, width = 800, height = 300 }: PFDProps) {
  const svgRef = useRef<SVGSVGElement>(null)
  const saved = useRef(loadView())
  const [view, setView] = useState(saved.current)
  const [dragging, setDragging] = useState(false)
  const dragRef = useRef({ startX: 0, startY: 0, origX: 0, origY: 0 })

  const updateView = useCallback((dx: number, dy: number, zoom: number) => {
    setView((v: typeof saved.current) => {
      const nv = { x: v.x + dx, y: v.y + dy, zoom: Math.max(0.3, Math.min(3, v.zoom * zoom)) }
      saved.current = nv
      saveView(nv.x, nv.y, nv.zoom)
      return nv
    })
  }, [])

  // 滚轮缩放
  useEffect(() => {
    const el = svgRef.current?.parentElement
    if (!el) return
    const handler = (e: WheelEvent) => {
      if (!e.ctrlKey && !e.metaKey) return
      e.preventDefault()
      updateView(0, 0, e.deltaY > 0 ? 0.9 : 1.1)
    }
    el.addEventListener('wheel', handler, { passive: false })
    return () => el.removeEventListener('wheel', handler)
  }, [updateView])

  // 拖拽平移
  const onMouseDown = (e: React.MouseEvent) => {
    if (e.button !== 0) return
    const el = svgRef.current?.parentElement
    if (!el) return
    setDragging(true)
    dragRef.current = { startX: e.clientX, startY: e.clientY, origX: view.x, origY: view.y }
    el.style.cursor = 'grabbing'
  }
  const onMouseMove = (e: React.MouseEvent) => {
    if (!dragging) return
    updateView(
      (e.clientX - dragRef.current.startX) / view.zoom,
      (e.clientY - dragRef.current.startY) / view.zoom,
      1,
    )
    dragRef.current.startX = e.clientX
    dragRef.current.startY = e.clientY
  }
  const onMouseUp = () => {
    setDragging(false)
    if (svgRef.current?.parentElement) svgRef.current.parentElement.style.cursor = 'grab'
  }

  const nodeMap = new Map(nodes.map(n => [n.id, n]))
  const edgeData = edges.map(e => {
    const from = nodeMap.get(e.from), to = nodeMap.get(e.to)
    if (!from || !to) return null
    return { key: `${e.from}→${e.to}`, d: computePath(from.x + NODE_W / 2, from.y + NODE_H / 2, to.x + NODE_W / 2, to.y + NODE_H / 2), label: e.label }
  }).filter(Boolean) as { key: string; d: string; label?: string }[]

  return (
    <section className="section">
      <div className="section__title-row">
        <h2 className="section__title" style={{ margin: 0 }}>🏭 工艺流程图</h2>
        <span className="pfd-hint">Ctrl+滚轮缩放 · 拖拽平移</span>
        <button className="btn btn--ghost btn--sm" onClick={() => { const r = { x: 0, y: 0, zoom: 1 }; setView(r); saved.current = r; saveView(0, 0, 1) }}>重置</button>
      </div>
      <div className="pfd-wrap" style={{ cursor: 'grab', height, overflow: 'hidden', borderRadius: 8, border: '1px solid var(--border)', background: 'var(--card-bg)' }}
        onMouseDown={onMouseDown} onMouseMove={onMouseMove} onMouseUp={onMouseUp} onMouseLeave={onMouseUp}
      >
        <svg ref={svgRef} viewBox={`${-view.x} ${-view.y} ${width / view.zoom} ${height / view.zoom}`} className="pfd-svg" style={{ width: '100%', height: '100%', pointerEvents: dragging ? 'none' : 'auto' }}>
          <defs>
            <marker id="pfdArrow" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto">
              <path d="M 0 0 L 8 3 L 0 6 Z" fill="#78909c" />
            </marker>
          </defs>
          {edgeData.map(ep => (
            <g key={ep.key}>
              <path d={ep.d} fill="none" stroke="#78909c" strokeWidth="2" markerEnd="url(#pfdArrow)" />
              {ep.label && <text fontSize="10" fill="#78909c"><textPath href={`#${ep.key}`} startOffset="50%" textAnchor="middle">{ep.label}</textPath></text>}
            </g>
          ))}
          {nodes.map(n => {
            const SvgSymbol = SYMBOLS[n.type]
            const off = LABEL_OFFSETS[n.type]
            const val = values[n.id]
            return (
              <g key={n.id} className="pfd-node">
                <SvgSymbol x={n.x} y={n.y} />
                {n.label && <text x={n.x + NODE_W / 2} y={n.y + off.dy} textAnchor="middle" fill="var(--text)" fontSize={off.fontSize} fontWeight={600}>{n.label}</text>}
                {val !== undefined && <text x={n.x + NODE_W / 2} y={n.y + off.dy + 12} textAnchor="middle" fill="var(--text-muted)" fontSize="10">{val.toFixed(1)}</text>}
              </g>
            )
          })}
        </svg>
        {nodes.length === 0 && <div className="db-empty" style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>暂无流程节点</div>}
      </div>
    </section>
  )
}

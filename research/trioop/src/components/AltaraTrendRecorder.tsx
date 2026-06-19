import { useEffect, useMemo, useRef } from 'react';
import type { TrendChannel, TrendTimeScale } from '@altara/industrial';

export interface AltaraTrendRecorderProps {
  channels?: TrendChannel[];
  timeScale?: TrendTimeScale;
  showGrid?: boolean;
  showLegend?: boolean;
  showPoints?: boolean;
  lineWidth?: number;
  yAxisLabel?: string;
  backgroundColor?: string;
  mockMode?: boolean;
  className?: string;
  width?: number;
  height?: number;
  /** 实时数据 { 变量全名 → { value } } */
  liveData?: Record<string, { value: number | boolean }>
  /** 通道 key → 变量全名 映射 */
  varMap?: Record<string, string>
}

const SCALE_MS: Record<TrendTimeScale, number> = {
  '1m': 60_000,
  '5m': 300_000,
  '15m': 900_000,
  '1h': 3_600_000,
  '4h': 14_400_000,
  '8h': 28_800_000,
  '24h': 86_400_000,
};

interface Sample { t: number; v: number; }

const DEFAULT_CHANNELS: TrendChannel[] = [
  { key: 'temp', label: 'Reactor Temp', color: '#E24B4A', unit: '°C', min: 60, max: 110 },
  { key: 'press', label: 'Pressure', color: '#37D3E0', unit: 'bar', min: 0, max: 16 },
  { key: 'flow', label: 'Feed Flow', color: '#1D9E75', unit: 'm³/h', min: 0, max: 50 },
  { key: 'level', label: 'Tank Level', color: '#F4D03F', unit: '%', min: 0, max: 100 },
];

export function TrendRecorder({
  channels: channelsProp,
  timeScale = '1h',
  showGrid = true,
  showLegend = true,
  showPoints = false,
  lineWidth = 1.5,
  yAxisLabel = '',
  backgroundColor,
  mockMode,
  className,
  width,
  height,
  liveData,
  varMap,
}: AltaraTrendRecorderProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const buffersRef = useRef<Map<string, Sample[]>>(new Map());
  const channels = useMemo(
    () => mockMode ? DEFAULT_CHANNELS : channelsProp ?? [],
    [channelsProp, mockMode],
  );

  // mockMode: synthesize one sample per channel per ~200 ms.
  useEffect(() => {
    if (!mockMode) return;
    // 用实际 channels 的 key 生成模拟数据（支持自定义通道）
    const keys = channels.map(c => c.key)
    if (keys.length === 0) return
    const id = setInterval(() => {
      const now = performance.now();
      const t = now / 1000;
      const ensure = (key: string) => {
        let buf = buffersRef.current.get(key);
        if (!buf) { buf = []; buffersRef.current.set(key, buf); }
        return buf;
      };
      for (const ch of channels) {
        // 根据通道索引生成不同频率/幅度的模拟数据
        const idx = keys.indexOf(ch.key)
        const freq = 0.03 + idx * 0.015
        const amp = (ch.max - ch.min) * 0.35
        const mid = (ch.max + ch.min) / 2
        const val = mid + Math.sin(t * freq) * amp + Math.sin(t * freq * 3) * amp * 0.3
        ensure(ch.key).push({ t: now, v: val })
      }
      for (const buf of buffersRef.current.values()) {
        const cutoff = now - SCALE_MS[timeScale] - 1000;
        while (buf.length && buf[0]!.t < cutoff) buf.shift();
        if (buf.length > 5000) buf.shift();
      }
    }, 200);
    return () => clearInterval(id);
  }, [mockMode, timeScale, channels]);

  // 实时数据：每次 liveData 更新都采样 + 裁剪
  useEffect(() => {
    if (mockMode || !varMap || !liveData) return
    const now = performance.now()
    for (const ch of channels) {
      const fullName = varMap[ch.key]
      if (!fullName) continue
      const pt = liveData[fullName]
      if (pt === undefined || pt.value === null || pt.value === undefined) continue
      const val = typeof pt.value === 'number' ? pt.value : (pt.value ? 1 : 0)
      let buf = buffersRef.current.get(ch.key)
      if (!buf) { buf = []; buffersRef.current.set(ch.key, buf) }
      buf.push({ t: now, v: val })
    }
    // 裁剪过期数据
    const cutoff = now - SCALE_MS[timeScale] - 1000
    for (const buf of buffersRef.current.values()) {
      while (buf.length && buf[0]!.t < cutoff) buf.shift()
      while (buf.length > 5000) buf.shift()
    }
  }, [mockMode, liveData, varMap, channels, timeScale])

  // 绘图：初始化 canvas 像素缓冲，draw 时仅读尺寸不设宽高（防频闪）
  const containerRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // 一次性初始化 canvas 像素缓冲
    const dpr = window.devicePixelRatio || 1;
    const initW = width ?? 720;
    const initH = height ?? 280;
    canvas.width = Math.max(initW, 1) * dpr;
    canvas.height = Math.max(initH, 1) * dpr;
    canvas.style.width = initW + 'px';
    canvas.style.height = initH + 'px';
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    let raf = 0;
    let lastTokens: Record<string, string> | null = null;
    const draw = () => {
      raf = requestAnimationFrame(draw);
      const W = canvas.width / dpr;
      const H = canvas.height / dpr;

      // 主题 tokens：每 500ms 重读一次，不每帧读（防样式重算频闪）
      if (!lastTokens || performance.now() % 500 < 16) {
        const s = getComputedStyle(container);
        lastTokens = {
          bg: s.getPropertyValue('--vt-bg-panel').trim() || '#181A1B',
          textPrimary: s.getPropertyValue('--vt-text-primary').trim() || '#E8E6DF',
          textMuted: s.getPropertyValue('--vt-text-muted').trim() || '#7A7872',
          border: s.getPropertyValue('--vt-border').trim() || '#2E3133',
        };
      }

      ctx.fillStyle = backgroundColor || lastTokens.bg;
      ctx.fillRect(0, 0, W, H);

      const padX = 8;
      const padTop = 8;
      const legendH = showLegend ? 18 : 0;
      const plotH = H - padTop - legendH - 4;
      const plotY = padTop;

      // Y 轴标签
      if (yAxisLabel) {
        ctx.save()
        ctx.translate(12, padTop + plotH / 2)
        ctx.rotate(-Math.PI / 2)
        ctx.fillStyle = lastTokens.textMuted
        ctx.font = '11px sans-serif'
        ctx.textAlign = 'center'
        ctx.textBaseline = 'middle'
        ctx.fillText(yAxisLabel, 0, 0)
        ctx.restore()
      }

      // 网格
      if (showGrid) {
        ctx.strokeStyle = lastTokens.border;
        ctx.lineWidth = 1;
        for (let i = 0; i <= 4; i++) {
          const yy = plotY + (plotH / 4) * i;
          ctx.beginPath(); ctx.moveTo(padX, yy); ctx.lineTo(W - padX, yy); ctx.stroke();
        }
        for (let i = 0; i <= 6; i++) {
          const xx = padX + ((W - 2 * padX) / 6) * i;
          ctx.beginPath(); ctx.moveTo(xx, plotY); ctx.lineTo(xx, plotY + plotH); ctx.stroke();
        }
      }

      const now = performance.now();
      const windowMs = SCALE_MS[timeScale];
      const start = now - windowMs;

      for (const ch of channels) {
        const buf = buffersRef.current.get(ch.key) ?? [];
        if (buf.length < 2) continue;
        ctx.strokeStyle = ch.color;
        ctx.lineWidth = lineWidth;
        ctx.beginPath();
        let started = false;
        for (const s of buf) {
          if (s.t < start) continue;
          const x = padX + ((s.t - start) / windowMs) * (W - 2 * padX);
          const y = plotY + plotH * (1 - (s.v - ch.min) / Math.max(0.0001, ch.max - ch.min));
          if (!started) { ctx.moveTo(x, y); started = true; } else { ctx.lineTo(x, y); }
          if (showPoints) {
            ctx.fillStyle = ch.color;
            ctx.beginPath(); ctx.arc(x, y, 2.5, 0, Math.PI * 2); ctx.fill();
          }
        }
        ctx.stroke();
      }

      // 图例
      if (showLegend) {
        ctx.font = '11px monospace';
        ctx.textBaseline = 'middle';
        let lx = padX;
        const ly = H - legendH / 2;
        for (const ch of channels) {
          const buf = buffersRef.current.get(ch.key) ?? [];
          const last = buf[buf.length - 1]?.v;
          ctx.fillStyle = ch.color;
          ctx.fillRect(lx, ly - 4, 10, 8);
          ctx.fillStyle = lastTokens.textPrimary;
          ctx.textAlign = 'left';
          const label = `${ch.label}${last !== undefined ? ` ${last.toFixed(1)}${ch.unit ?? ''}` : ''}`;
          ctx.fillText(label, lx + 14, ly + 1);
          lx += 18 + ctx.measureText(label).width + 14;
        }
      }
    };
    raf = requestAnimationFrame(draw);
    return () => cancelAnimationFrame(raf);
  }, [channels, timeScale, showGrid, showLegend, showPoints, lineWidth, yAxisLabel, backgroundColor, width, height]);

  return (
    <div ref={containerRef}
      className={['vt-component vt-trend', className].filter(Boolean).join(' ')}
      style={{
        display: 'block',
        width: width ?? 720,
        height: height ?? 280,
        background: backgroundColor || 'var(--vt-bg-panel)',
        border: '1px solid var(--vt-border)',
        borderRadius: 4,
      }}
      role="img"
      aria-label={`Trend recorder — ${channels.length} channels, ${timeScale} window`}
    >
      <canvas ref={canvasRef} style={{ display: 'block', width: '100%', height: '100%' }} aria-hidden="true" />
    </div>
  );
}

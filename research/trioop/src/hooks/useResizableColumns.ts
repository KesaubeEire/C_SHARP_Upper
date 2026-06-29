import { useCallback, useEffect, useRef, useState } from 'react'

const STORAGE_KEY = 'trioop_recipe_col_widths'

interface ColDef {
  key: string
  defaultWidth: number
}

export function useResizableColumns(cols: ColDef[]) {
  const [widths, setWidths] = useState<Record<string, number>>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY)
      if (saved) return { ...Object.fromEntries(cols.map(c => [c.key, c.defaultWidth])), ...JSON.parse(saved) }
    } catch { /* ignore */ }
    return Object.fromEntries(cols.map(c => [c.key, c.defaultWidth]))
  })

  const dragRef = useRef<{ key: string; startX: number; startW: number } | null>(null)

  const onMouseDown = useCallback((e: React.MouseEvent, key: string) => {
    e.preventDefault()
    dragRef.current = { key, startX: e.clientX, startW: widths[key] ?? 80 }
  }, [widths])

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!dragRef.current) return
      const { key, startX, startW } = dragRef.current
      const delta = e.clientX - startX
      setWidths(prev => {
        const next = Math.max(40, startW + delta)
        return { ...prev, [key]: next }
      })
    }
    const onUp = () => {
      if (dragRef.current) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(widths))
        dragRef.current = null
        document.body.style.cursor = ''
        document.body.style.userSelect = ''
      }
    }
    if (dragRef.current) {
      document.body.style.cursor = 'col-resize'
      document.body.style.userSelect = 'none'
    }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => { window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp) }
  }, [widths])

  const colProps = (key: string) => ({
    width: widths[key] ?? 80,
    onMouseDown: (e: React.MouseEvent) => onMouseDown(e, key),
  })

  return { widths, colProps, colStyle: (key: string) => ({ width: widths[key] ?? 80, position: 'relative' as const }) }
}

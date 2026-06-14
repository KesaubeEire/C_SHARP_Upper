/**
 * SSE 实时数据 Hook
 *
 * 连接 /api/plc/stream，自动接收 PLC 数据推送
 */

import { useEffect, useState, useRef, useCallback } from 'react'
import type { PLCData } from '../shared/types'

const RECONNECT_DELAY = 3000

export function usePLCData() {
  const [data, setData] = useState<PLCData>({})
  const [connected, setConnected] = useState(false)
  const esRef = useRef<EventSource | null>(null)
  const retryRef = useRef<ReturnType<typeof setTimeout>>()

  const connect = useCallback(() => {
    esRef.current?.close()

    const es = new EventSource('/api/plc/stream')
    esRef.current = es

    es.onopen = () => {
      setConnected(true)
      if (retryRef.current) clearTimeout(retryRef.current)
    }

    es.onmessage = (event) => {
      try {
        setData(JSON.parse(event.data))
      } catch { /* ignore malformed data */ }
    }

    es.onerror = () => {
      setConnected(false)
      es.close()
      // 自动重连
      retryRef.current = setTimeout(connect, RECONNECT_DELAY)
    }
  }, [])

  useEffect(() => {
    connect()
    return () => {
      esRef.current?.close()
      if (retryRef.current) clearTimeout(retryRef.current)
    }
  }, [connect])

  return { data, connected }
}

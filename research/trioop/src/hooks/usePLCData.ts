/**
 * SSE 实时数据 Hook
 *
 * 连接 /api/plc/stream，自动接收 PLC 数据推送
 * 数据格式: { db: { [name]: PLCDataPoint }, io: { i: number[], q: number[] } }
 */

import { useEffect, useState, useRef, useCallback } from 'react'
import type { PLCData } from '../../shared/types'

const RECONNECT_DELAY = 3000

export interface IOData {
  i: Record<number, number>
  q: Record<number, number>
  m: Record<number, number>
}

interface StreamPayload {
  db: PLCData
  io: IOData
  dbBlocks?: Record<string, number[] | null>
}

export function usePLCData() {
  const [db, setDb] = useState<PLCData>({})
  const [io, setIo] = useState<IOData>({ i: {}, q: {}, m: {} })
  const [dbBlocks, setDbBlocks] = useState<Record<string, number[] | null>>({})
  const [connected, setConnected] = useState(false)
  const [lastDataTime, setLastDataTime] = useState(0)
  const [ioLatency, setIoLatency] = useState(0)
  const lastIoTimeRef = useRef(0)
  const esRef = useRef<EventSource | null>(null)
  const retryRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

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
        const payload: StreamPayload = JSON.parse(event.data)
        setDb(payload.db ?? {})
        setIo({ i: payload.io?.i ?? {}, q: payload.io?.q ?? {}, m: payload.io?.m ?? {} })
        setDbBlocks(payload.dbBlocks ?? {})
        const now = Date.now()
        setLastDataTime(now)
        // 跟踪 I 区更新间隔（两次信号接收时间差）
        if (lastIoTimeRef.current > 0) {
          setIoLatency(now - lastIoTimeRef.current)
        }
        lastIoTimeRef.current = now
      } catch { /* ignore malformed data */ }
    }

    es.onerror = () => {
      setConnected(false)
      es.close()
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

  return { db, io, setIo, dbBlocks, connected, lastDataTime, ioLatency }
}

/**
 * PLC 写入操作 Hook
 */

import { useState, useCallback } from 'react'

interface WriteState {
  loading: boolean
  error: string | null
}

export function usePLCWrite() {
  const [states, setStates] = useState<Record<string, WriteState>>({})

  const write = useCallback(async (name: string, value: number) => {
    setStates(prev => ({ ...prev, [name]: { loading: true, error: null } }))

    try {
      const res = await fetch('/api/plc/write', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, value }),
      })

      const result = await res.json()
      if (!result.success) {
        throw new Error(result.error || '写入失败')
      }

      setStates(prev => ({ ...prev, [name]: { loading: false, error: null } }))
      return true
    } catch (err) {
      const msg = (err as Error).message
      setStates(prev => ({ ...prev, [name]: { loading: false, error: msg } }))
      return false
    }
  }, [])

  const dismissError = useCallback((name: string) => {
    setStates(prev => {
      const next = { ...prev }
      if (next[name]) next[name] = { ...next[name], error: null }
      return next
    })
  }, [])

  return { write, states, dismissError }
}

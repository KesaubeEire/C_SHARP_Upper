import { useState, useEffect } from 'react'
import CollapsibleSection from './CollapsibleSection'

type AlarmState = 'normal' | 'warning' | 'alarm' | 'acknowledged'

interface AlarmDef {
  id: string; label: string; priority?: 1 | 2 | 3; group?: string
}

interface AlarmAnnunciatorProps {
  alarms?: AlarmDef[]
  states?: Record<string, AlarmState>
  onAcknowledge?: (id: string) => void
  columns?: number
  groupBy?: string
}

const PRIORITY_COLORS: Record<number, string> = {
  1: '#ef5350', // high - red
  2: '#ff9800', // medium - orange
  3: '#ffc107', // low - yellow
}

export default function AlarmAnnunciator({ alarms = [], states = {}, onAcknowledge, columns = 6, groupBy }: AlarmAnnunciatorProps) {
  const [blink, setBlink] = useState(false)

  useEffect(() => {
    const t = setInterval(() => setBlink(b => !b), 500)
    return () => clearInterval(t)
  }, [])

  const groups = groupBy
    ? alarms.reduce<Record<string, AlarmDef[]>>((acc, a) => {
        const k = a.group || '_ungrouped'
        ;(acc[k] ||= []).push(a)
        return acc
      }, {})
    : { _all: alarms }

  return (
    <CollapsibleSection title="🚨 报警面板" storageKey="alarm-annunciator">
      {Object.entries(groups).map(([group, groupAlarms]) => (
        <div key={group}>
          {groupBy && group !== '_ungrouped' && (
            <div className="annunciator__group-label">{group}</div>
          )}
          <div className="annunciator__grid" style={{ gridTemplateColumns: `repeat(${columns}, 1fr)` }}>
            {groupAlarms.map(a => {
              const state = states[a.id] || 'normal'
              const priColor = PRIORITY_COLORS[a.priority || 3]
              const isBlinking = (state === 'alarm' || state === 'warning') && !state.endsWith('acknowledged') && blink
              return (
                <button
                  key={a.id}
                  className={`annunciator__tile annunciator__tile--${state} ${isBlinking ? 'annunciator__tile--blink' : ''}`}
                  style={{ '--pri-color': priColor } as React.CSSProperties}
                  onClick={() => onAcknowledge?.(a.id)}
                  title={state === 'alarm' ? '点击确认' : a.label}
                >
                  <span className="annunciator__tile-label">{a.label}</span>
                  <span className="annunciator__tile-state">
                    {state === 'alarm' ? 'ALARM' : state === 'warning' ? 'WARN' : state === 'acknowledged' ? 'ACK' : 'OK'}
                  </span>
                </button>
              )
            })}
          </div>
        </div>
      ))}
    </CollapsibleSection>
  )
}
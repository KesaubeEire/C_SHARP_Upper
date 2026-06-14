import type { PLCVariable, PLCDataPoint } from '../../shared/types'
import WriteControl from './WriteControl'

interface PLCCardProps {
  variable: PLCVariable
  point: PLCDataPoint | undefined
  writeLoading: boolean
  writeError: string | null
  onWrite: (name: string, value: number) => Promise<boolean>
  onDismissError: (name: string) => void
}

function formatValue(value: number | boolean | undefined, type: string): string {
  if (value === undefined || value === null) return '--'
  if (type === 'bool') return ''
  if (type === 'real') return Number(value).toFixed(2)
  return String(value)
}

export default function PLCCard({
  variable, point, writeLoading, writeError, onWrite, onDismissError,
}: PLCCardProps) {
  const value = point?.value
  const tag = `${variable.type.toUpperCase()}${variable.writable ? ' ✏️' : ''}`
  const tagClass = variable.writable ? 'card__tag card__tag--writable' : 'card__tag'

  // Bool 类型特殊渲染
  if (variable.type === 'bool') {
    const isOn = value === true || value === 1
    return (
      <div className="card">
        <div className="card__header">
          <span className="card__name">{variable.name}</span>
          <span className={tagClass}>{tag}</span>
        </div>
        <div className={`card__value card__value--${isOn ? 'on' : 'off'}`}>
          {value === undefined || value === null ? '--' : isOn ? '● ON' : '○ OFF'}
        </div>
        {variable.writable && (
          <WriteControl
            name={variable.name}
            type="bool"
            currentValue={value ?? false}
            loading={writeLoading}
            error={writeError}
            onWrite={onWrite}
            onDismissError={onDismissError}
          />
        )}
      </div>
    )
  }

  // 数值类型
  return (
    <div className="card">
      <div className="card__header">
        <span className="card__name">{variable.name}</span>
        <span className={tagClass}>{tag}</span>
      </div>
      <div className="card__value">{formatValue(value, variable.type)}</div>
      {variable.writable && (
        <WriteControl
          name={variable.name}
          type={variable.type}
          currentValue={value ?? 0}
          loading={writeLoading}
          error={writeError}
          onWrite={onWrite}
          onDismissError={onDismissError}
        />
      )}
    </div>
  )
}

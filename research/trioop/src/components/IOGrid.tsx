import { useState } from 'react'

interface IOGridProps {
  label: string
  data: Record<number, number> | null | undefined
  prefix: string
  bytes: number[]
  onToggle?: (byteAddr: number, bit: number, value: boolean) => void
}

export default function IOGrid({ label, data, prefix, bytes, onToggle }: IOGridProps) {
  const [controlMode, setControlMode] = useState(false)
  const canControl = prefix.toUpperCase() === 'Q' || prefix.toUpperCase() === 'M'

  function handleClick(addr: number, bit: number, newVal: boolean) {
    if (!controlMode || !onToggle) return
    onToggle(addr, bit, newVal)
  }

  return (
    <div className="io-panel">
      <div className="io-panel__title-row">
        <h3 className="io-panel__title">{label}</h3>
        {canControl && onToggle && (
          <button
            className={`btn btn--${controlMode ? 'danger' : 'primary'} io-panel__ctrl-btn`}
            onClick={() => setControlMode(!controlMode)}
          >
            {controlMode ? '退出控制' : '进入控制'}
          </button>
        )}
      </div>
      {controlMode && (
        <div className="io-panel__ctrl-hint">⚡ 点击 {prefix} 点位可切换 ON/OFF</div>
      )}
      <div className="io-table">
        <div className="io-table__header">
          <span className="io-table__addr-col">地址</span>
          <span className="io-table__bits-row">
            {[0, 1, 2, 3, 4, 5, 6, 7].map(b => (
              <span key={b} className="io-table__bit-label">{b}</span>
            ))}
          </span>
          <span className="io-table__hex-col">HEX</span>
        </div>
        {bytes.map(addr => {
          const val = data?.[addr]
          const hasData = val !== undefined && val !== null
          return (
            <div key={addr} className="io-table__row">
              <span className="io-table__addr-col">{prefix}{addr}</span>
              <span className="io-table__bits-row">
                {[0, 1, 2, 3, 4, 5, 6, 7].map(bit => {
                  const on = hasData && ((val & (1 << bit)) !== 0)
                  let cls = 'io-bit io-bit--gray'
                  if (!hasData) cls = 'io-bit io-bit--red'
                  else if (on)  cls = 'io-bit io-bit--green'
                  if (controlMode) cls += ' io-bit--clickable'
                  return (
                    <span
                      key={bit}
                      className={cls}
                      title={controlMode ? `点击${on ? '关闭' : '打开'} ${prefix}${addr}.${bit}` : `${prefix}${addr}.${bit} = ${on ? '1' : '0'}`}
                      onClick={() => handleClick(addr, bit, !on)}
                    />
                  )
                })}
              </span>
              <span className="io-table__hex-col">
                {hasData ? `0x${val.toString(16).toUpperCase().padStart(2, '0')}` : '--'}
              </span>
            </div>
          )
        })}
      </div>
    </div>
  )
}

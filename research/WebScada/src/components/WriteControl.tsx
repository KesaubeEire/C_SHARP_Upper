import { useState } from 'react'

interface WriteControlProps {
  name: string
  type: string
  currentValue: number | boolean
  loading: boolean
  error: string | null
  onWrite: (name: string, value: number) => Promise<boolean>
  onDismissError: (name: string) => void
}

export default function WriteControl({
  name, type, currentValue, loading, error, onWrite, onDismissError,
}: WriteControlProps) {
  const [inputVal, setInputVal] = useState('')

  // bool 类型：直接渲染开关按钮
  if (type === 'bool') {
    const isOn = currentValue === true || currentValue === 1
    return (
      <div className="write-control">
        <button
          className={`btn ${isOn ? 'btn--danger' : 'btn--success'}`}
          disabled={loading}
          onClick={() => onWrite(name, isOn ? 0 : 1)}
        >
          {loading ? '⏳' : isOn ? '关闭' : '开启'}
        </button>
      </div>
    )
  }

  // 数值类型：输入框 + 写入按钮
  return (
    <div className="write-control">
      <input
        className="write-control__input"
        type="number"
        step="any"
        value={inputVal}
        placeholder={String(currentValue ?? '')}
        onChange={e => setInputVal(e.target.value)}
        onKeyDown={e => {
          if (e.key === 'Enter' && inputVal) {
            onWrite(name, Number(inputVal))
          }
        }}
      />
      <button
        className="btn btn--primary"
        disabled={loading || !inputVal}
        onClick={() => {
          if (inputVal) onWrite(name, Number(inputVal))
        }}
      >
        {loading ? '⏳' : '写入'}
      </button>

      {error && (
        <span className="write-control__error" onClick={() => onDismissError(name)}>
          ⚠ {error}
        </span>
      )}
    </div>
  )
}

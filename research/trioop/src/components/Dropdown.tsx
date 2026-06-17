import { useState, useRef, useEffect } from 'react'

interface DropdownItem { label: string; value: any }

interface DropdownProps {
  value: any
  onChange: (value: any) => void
  options: DropdownItem[]
  placeholder?: string
  className?: string
}

export default function Dropdown({ value, onChange, options, placeholder = '选择...', className = '' }: DropdownProps) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const selected = options.find(o => o.value === value)

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  return (
    <div className={`dropdown ${className}`} ref={ref}>
      <button className="dropdown__trigger" onClick={() => setOpen(!open)} type="button">
        <span className="dropdown__value">{selected ? selected.label : placeholder}</span>
        <svg className={`dropdown__arrow ${open ? 'dropdown__arrow--open' : ''}`} width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="m6 9 6 6 6-6" />
        </svg>
      </button>
      {open && (
        <div className="dropdown__content">
          {options.map((opt, i) => (
            <button key={i} className={`dropdown__item ${opt.value === value ? 'dropdown__item--selected' : ''}`}
              onClick={() => { onChange(opt.value); setOpen(false) }} type="button">
              {opt.label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

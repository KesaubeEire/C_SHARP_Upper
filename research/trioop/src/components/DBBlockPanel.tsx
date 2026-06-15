import { useState } from 'react'

interface DBBlockConfig {
  label: string
  dbNumber: number
  startOffset: number
  byteCount: number
}

interface DBBlockPanelProps {
  blocks: DBBlockConfig[]
  data: Record<string, number[] | null>
  onAdd: (block: DBBlockConfig) => void
  onRemove: (label: string) => void
}

export default function DBBlockPanel({ blocks, data, onAdd, onRemove }: DBBlockPanelProps) {
  const [label, setLabel] = useState('')
  const [dbNumber, setDbNumber] = useState('1')
  const [startOffset, setStartOffset] = useState('0')
  const [byteCount, setByteCount] = useState('8')

  function handleAdd() {
    if (!label.trim() || !dbNumber) return
    onAdd({
      label: label.trim(),
      dbNumber: parseInt(dbNumber),
      startOffset: parseInt(startOffset) || 0,
      byteCount: Math.min(parseInt(byteCount) || 8, 240),
    })
    setLabel('')
  }

  return (
    <section className="section">
      <h2 className="section__title">📦 DB 块监控</h2>

      {/* 添加 DB 块 */}
      <div className="db-form">
        <input className="db-form__input" value={label} onChange={e => setLabel(e.target.value)} placeholder="标签(如 DB1_状态)" />
        <input className="db-form__input db-form__input--sm" value={dbNumber} onChange={e => setDbNumber(e.target.value)} placeholder="DB号" />
        <input className="db-form__input db-form__input--sm" value={startOffset} onChange={e => setStartOffset(e.target.value)} placeholder="起始" />
        <input className="db-form__input db-form__input--sm" value={byteCount} onChange={e => setByteCount(e.target.value)} placeholder="长度" />
        <button className="btn btn--primary" onClick={handleAdd} disabled={!label.trim()}>添加</button>
      </div>

      {/* DB 块列表 */}
      {blocks.length === 0 ? (
        <div className="db-empty">尚未添加 DB 块，上方添加后实时显示</div>
      ) : (
        <div className="db-list">
          {blocks.map(block => {
            const bytes = data[block.label]
            return (
              <div key={block.label} className="db-card">
                <div className="db-card__header">
                  <span className="db-card__label">{block.label}</span>
                  <span className="db-card__info">DB{block.dbNumber} @{block.startOffset} · {block.byteCount} 字节</span>
                  <button className="btn btn--danger db-card__del" onClick={() => onRemove(block.label)}>✕</button>
                </div>
                <div className="db-card__body">
                  {bytes === undefined ? (
                    <span className="db-card__pending">等待数据...</span>
                  ) : bytes === null ? (
                    <span className="db-card__error">读取失败</span>
                  ) : (
                    <div className="db-hex">
                      {/* 地址列 */}
                      <div className="db-hex__col db-hex__addr">
                        <span className="db-hex__row-label">位</span>
                        {bytes.map((_, i) => {
                          const addr = block.startOffset + i
                          return <span key={i} className="db-hex__addr-val">{addr.toString(16).toUpperCase().padStart(4, '0')}</span>
                        })}
                      </div>
                      {/* 位矩阵列 */}
                      <div className="db-hex__col db-hex__bits">
                        <span className="db-hex__row-label">7 6 5 4 3 2 1 0</span>
                        {bytes.map((b, i) => (
                          <span key={i} className="db-hex__bit-row">
                            {[7,6,5,4,3,2,1,0].map(bit => {
                              const on = (b & (1 << bit)) !== 0
                              return <span key={bit} className={`db-bit ${on ? 'db-bit--on' : ''}`} title={`位 ${bit} = ${on ? '1' : '0'}`} />
                            })}
                          </span>
                        ))}
                      </div>
                      {/* HEX 列 */}
                      <div className="db-hex__col db-hex__hex">
                        <span className="db-hex__row-label">HEX</span>
                        {bytes.map((b, i) => (
                          <span key={i} className="db-hex__byte">{b.toString(16).toUpperCase().padStart(2, '0')}</span>
                        ))}
                      </div>
                      {/* ASCII 列 */}
                      <div className="db-hex__col db-hex__ascii">
                        <span className="db-hex__row-label">ASCII</span>
                        {bytes.map((b, i) => (
                          <span key={i} className="db-hex__char">{b >= 32 && b <= 126 ? String.fromCharCode(b) : '·'}</span>
                        ))}
                      </div>
                      {/* 数值解读 */}
                      <div className="db-hex__col db-hex__values">
                        <span className="db-hex__row-label">INT</span>
                        {bytes.length >= 2 && (
                          <div className="db-hex__value-row">
                            <span>{(bytes[0] << 8) | bytes[1]}</span>
                          </div>
                        )}
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </section>
  )
}

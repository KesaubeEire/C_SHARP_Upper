import type { PLCVariable, PLCData, PLCDataPoint } from '../../shared/types'
import PLCCard from './PLCCard'

interface PLCGridProps {
  variables: PLCVariable[]
  data: PLCData
  writeStates: Record<string, { loading: boolean; error: string | null }>
  onWrite: (name: string, value: number) => Promise<boolean>
  onDismissError: (name: string) => void
}

export default function PLCGrid({
  variables, data, writeStates, onWrite, onDismissError,
}: PLCGridProps) {
  if (variables.length === 0) {
    return (
      <div className="empty">
        <h3>⏳ 等待 PLC 数据...</h3>
        <p>
          请确认 <code>server/config.ts</code> 中已配置变量，
          PLC 已开机且 PUT/GET 访问已启用
        </p>
      </div>
    )
  }

  return (
    <div className="grid">
      {variables.map(v => {
        const point: PLCDataPoint | undefined = data[v.name]
        const state = writeStates[v.name]
        return (
          <PLCCard
            key={v.name}
            variable={v}
            point={point}
            writeLoading={state?.loading ?? false}
            writeError={state?.error ?? null}
            onWrite={onWrite}
            onDismissError={onDismissError}
          />
        )
      })}
    </div>
  )
}

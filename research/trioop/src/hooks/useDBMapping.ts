const MAP_KEY = 'trioop_db_mapping'
const DATA_KEY = 'trioop_db_data'

/** 变量定义 */
interface VarDef { name: string; type: string; offset: number; bit?: number; arrayCount?: number }
interface DBData { dbNumber: number; dbName: string; variables: VarDef[] }

// ─── DB 号映射 ──────────────────────────────────────────
export function loadMapping(): Record<string, number> {
  try { return JSON.parse(localStorage.getItem(MAP_KEY) || '{}') } catch { return {} }
}
export function saveMapping(mapping: Record<string, number>): void {
  localStorage.setItem(MAP_KEY, JSON.stringify(mapping))
}

// ─── DB 变量定义存储 ─────────────────────────────────────
export function saveDBData(data: DBData): void {
  const all = loadAllDBData()
  const idx = all.findIndex(d => d.dbName === data.dbName)
  if (idx >= 0) all[idx] = data
  else all.push(data)
  localStorage.setItem(DATA_KEY, JSON.stringify(all))
}

export function loadAllDBData(): DBData[] {
  try { return JSON.parse(localStorage.getItem(DATA_KEY) || '[]') } catch { return [] }
}

/** 从存储中查找变量定义 */
export function findVarDef(dbName: string, varName: string): VarDef | undefined {
  const dbs = loadAllDBData()
  const db = dbs.find(d => d.dbName === dbName)
  return db?.variables.find(v => v.name === varName)
}

/** 构造 S7 地址：bool→B字节(用于RMW), 数值→类型字母 */
export function buildS7Address(dbNumber: number, v: VarDef): string {
  if (v.type === 'bool') {
    return `DB${dbNumber},B${v.offset}.1`
  }
  const typeMap: Record<string, string> = { real: 'R', int: 'I', dint: 'DI', word: 'W', dword: 'DW', byte: 'B' }
  const t = typeMap[v.type] || 'B'
  return `DB${dbNumber},${t}${v.offset}.${v.arrayCount && v.arrayCount > 1 ? v.arrayCount : 1}`
}

/** 解析变量名 "DB1:转速" → { dbNumber, varName } */
export function resolveVarName(fullName: string): { dbNumber: number; varName: string } {
  const parts = fullName.split(':')
  if (parts.length < 2) return { dbNumber: 1, varName: fullName }
  const dbName = parts[0]
  const varName = parts.slice(1).join(':')
  const mapping = loadMapping()
  const dbNumber = mapping[dbName] ?? (parseInt(dbName.replace(/^DB/i, '')) || 1)
  return { dbNumber, varName }
}

// ─── UDT 持久化 ─────────────────────────────────────────
const UDT_FILES_KEY = 'trioop_udt_files'

export function saveUDTContent(content: string): void {
  const all = loadAllUDTFiles()
  if (!all.includes(content)) {
    all.push(content)
    try { localStorage.setItem(UDT_FILES_KEY, JSON.stringify(all)) } catch {}
  }
}

export function loadAllUDTContent(): string {
  try { return JSON.parse(localStorage.getItem(UDT_FILES_KEY) || '[]').join('\n') } catch { return '' }
}

function loadAllUDTFiles(): string[] {
  try { return JSON.parse(localStorage.getItem(UDT_FILES_KEY) || '[]') } catch { return [] }
}

export function clearUDTCache(): void {
  try { localStorage.removeItem(UDT_FILES_KEY) } catch {}
}

/** 写入 PLC：bool 走 modifyBit(RMW), 数值直接写 */
export async function writePLC(fullName: string, value: number): Promise<void> {
  const { dbNumber, varName } = resolveVarName(fullName)
  const parts = fullName.split(':')
  const dbName = parts[0]
  const vd = findVarDef(dbName, varName)
  if (!vd) throw new Error(`未找到变量定义: ${fullName}`)

  if (vd.type === 'bool') {
    const address = buildS7Address(dbNumber, vd)
    const res = await fetch('/api/plc/write-raw', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ address, bit: vd.bit ?? 0, value: value ? 1 : 0 }),
    })
    if (!res.ok) throw new Error((await res.json()).error || '写入失败')
  } else {
    const address = buildS7Address(dbNumber, vd)
    const res = await fetch('/api/plc/write-raw', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ address, value }),
    })
    if (!res.ok) throw new Error((await res.json()).error || '写入失败')
  }
}

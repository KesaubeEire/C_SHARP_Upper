const BASE = '/api/vplc'

export interface ImportedField {
  name: string
  type: string
  offset: number
  bit?: number
  comment?: string
  arrayCount?: number
  opaqueSize?: number
}

export interface ImportedDB {
  dbNumber: number
  dbName: string
  variableCount: number
  variables: ImportedField[]
}

export interface UDTDetail {
  name: string
  fields: Array<{ name: string; type: string; bit?: number }>
}

export interface VPLCSnapshot {
  PE: number[]
  PA: number[]
  MK: number[]
  DB: Record<string, number[]>
  fields?: Record<string, { dbNumber: number; values: Record<string, any>; fieldMeta?: Record<string, any> }>
  _imported?: { dbNumber: number; dbName: string; fieldCount: number }[]
  _triggers?: Trigger[]
  _parsed?: any
}

export interface Trigger {
  id: string; name: string; enabled: boolean
  sourceDb: number; sourceOffset: number; sourceType: string; sourceBit?: number
  condition: string; threshold: number
  targetDb: number; targetOffset: number; targetType: string; targetBit?: number
  targetValue: number; active?: boolean
}

export async function fetchSnapshot(): Promise<VPLCSnapshot> {
  const r = await fetch(BASE)
  return r.json()
}

export async function writeValue(area: string, dbNumber: number, offset: number, type: string, value: number, bit?: number) {
  const r = await fetch(BASE + '/write', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ area, dbNumber, offset, type, value, bit }),
  })
  return r.json()
}

export async function toggleBit(area: string, offset: number, bit: number) {
  const r = await fetch(BASE + '/toggle-bit', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ area, offset, bit }),
  })
  return r.json()
}

export async function importDB(content: string, dbNumber?: number) {
  const r = await fetch(BASE + '/import-db', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ content, dbNumber }),
  })
  return r.json()
}

export async function importUDT(content: string) {
  const r = await fetch(BASE + '/import-udt', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ content }),
  })
  return r.json()
}

export async function fetchImportedDBs(): Promise<ImportedDB[]> {
  const r = await fetch(BASE + '/imported-dbs')
  return r.json()
}

export async function deleteImportedDB(key: string) {
  const r = await fetch(BASE + '/imported-dbs/' + encodeURIComponent(key), { method: 'DELETE' })
  return r.json()
}

export async function refreshImportedDB(key: string) {
  const r = await fetch(BASE + '/imported-dbs/' + encodeURIComponent(key) + '/refresh', { method: 'POST' })
  return r.json()
}

export async function writeImportedField(key: string, fieldName: string, value: number | boolean) {
  const r = await fetch(BASE + '/imported-dbs/' + encodeURIComponent(key) + '/write', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ fieldName, value }),
  })
  return r.json()
}

export async function randomizeImportedField(key: string, fieldName: string) {
  const r = await fetch(BASE + '/imported-dbs/' + encodeURIComponent(key) + '/randomize', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ fieldName }),
  })
  return r.json()
}

export async function fetchImportedUDTs(): Promise<string[]> {
  const r = await fetch(BASE + '/imported-udts')
  return r.json()
}

export async function fetchImportedUDTDetail(name: string): Promise<UDTDetail> {
  const r = await fetch(BASE + '/imported-udts/' + encodeURIComponent(name))
  return r.json()
}

export async function deleteImportedUDT(name: string) {
  const r = await fetch(BASE + '/imported-udts/' + encodeURIComponent(name), { method: 'DELETE' })
  return r.json()
}

export async function createDB(dbNumber: number, size: number) {
  const r = await fetch(BASE + '/create-db', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ dbNumber, size }),
  })
  return r.json()
}

export async function fetchDbs(): Promise<Record<string, number>> {
  const r = await fetch(BASE + '/dbs')
  return r.json()
}

export async function upsertDb(dbNumber: number, size: number) {
  const r = await fetch(BASE + '/dbs', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ dbNumber, size }),
  })
  return r.json()
}

export async function deleteDb(dbNumber: number) {
  const r = await fetch(BASE + '/dbs/' + dbNumber, { method: 'DELETE' })
  return r.json()
}

export async function fetchTriggers(): Promise<Trigger[]> {
  const r = await fetch(BASE + '/triggers')
  return r.json()
}

export async function createTrigger(body: any) {
  const r = await fetch(BASE + '/triggers', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  return r.json()
}

export async function deleteTrigger(id: string) {
  await fetch(BASE + '/triggers/' + id, { method: 'DELETE' })
}

export async function setPLCState(state: string) {
  const r = await fetch(BASE + '/state', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ state }) })
  return r.json()
}
export async function fetchRTC() {
  const r = await fetch(BASE + '/rtc')
  return r.json()
}
export async function setRTC(iso: string) {
  const r = await fetch(BASE + '/rtc', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ iso }) })
  return r.json()
}
export async function fetchDiag() {
  const r = await fetch(BASE + '/diag')
  return r.json()
}
export async function clearDiag() {
  await fetch(BASE + '/diag', { method: 'DELETE' })
}
export async function fetchLeds() {
  const r = await fetch(BASE + '/leds')
  return r.json()
}

// ── 用户脚本 ──

export interface ScriptConfig {
  name: string
  source: string
  obNumber: number
  enabled: boolean
}

export async function fetchScripts(): Promise<ScriptConfig[]> {
  const r = await fetch(BASE + '/scripts')
  return r.json()
}

export async function saveScripts(scripts: ScriptConfig[]) {
  const r = await fetch(BASE + '/scripts', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ scripts }),
  })
  return r.json()
}

// ── DB Editor ──

export interface DBEditorField {
  name: string
  type: string
  startValue?: string
  comment?: string
  offset?: number
  bit?: number
}

export interface DBEditorDef {
  key: string
  dbNumber: number
  dbName: string
  fields: DBEditorField[]
  values?: Record<string, any>
  totalSize?: number
  createdAt: number
  updatedAt: number
}

export async function fetchDBEditors(): Promise<DBEditorDef[]> {
  const r = await fetch(BASE + '/db-editor')
  return r.json()
}

export async function saveDBEditor(dbNumber: number, dbName: string, fields: DBEditorField[]): Promise<any> {
  const r = await fetch(BASE + '/db-editor', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ dbNumber, dbName, fields }),
  })
  return r.json()
}

export async function deleteDBEditor(key: string) {
  const r = await fetch(BASE + '/db-editor/' + encodeURIComponent(key), { method: 'DELETE' })
  return r.json()
}

export async function writeDBEditorField(key: string, fieldName: string, value: number | boolean) {
  const r = await fetch(BASE + '/db-editor/' + encodeURIComponent(key) + '/write', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ fieldName, value }),
  })
  return r.json()
}

export async function importDBEditorDB(content: string, dbNumber?: number): Promise<any> {
  const r = await fetch(BASE + '/db-editor/import-db', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ content, dbNumber }),
  })
  return r.json()
}

export async function importDBEditorUDT(content: string): Promise<any> {
  const r = await fetch(BASE + '/db-editor/import-udt', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ content }),
  })
  return r.json()
}

export async function exportDBEditorDB(key: string): Promise<any> {
  const r = await fetch(BASE + '/db-editor/' + encodeURIComponent(key) + '/export-db')
  return r.json()
}

export async function randomizeDBEditorField(key: string, fieldName: string): Promise<any> {
  const r = await fetch(BASE + '/db-editor/' + encodeURIComponent(key) + '/randomize', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ fieldName }),
  })
  return r.json()
}

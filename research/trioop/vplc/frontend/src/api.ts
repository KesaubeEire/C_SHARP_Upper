const BASE = '/api/vplc'

export interface VPLCSnapshot {
  PE: number[]
  PA: number[]
  MK: number[]
  DB: Record<string, number[]>
  fields?: Record<string, { dbNumber: number; values: Record<string, any> }>
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

export async function createDB(dbNumber: number, size: number) {
  const r = await fetch(BASE + '/create-db', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ dbNumber, size }),
  })
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

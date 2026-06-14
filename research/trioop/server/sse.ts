/**
 * SSE（Server-Sent Events）推送管理
 */

import type { Response } from 'express'
import type { PLCData } from '../shared/types.js'

const clients = new Set<Response>()

export function addClient(res: Response): void {
  clients.add(res)
  res.on('close', () => { clients.delete(res) })
}

export function broadcast(data: PLCData): void {
  const payload = JSON.stringify(data)
  for (const client of clients) {
    client.write(`data: ${payload}\n\n`)
  }
}

export function getClientCount(): number {
  return clients.size
}

/**
 * vPLC 客户端 — 通过 HTTP API / TCP 直写读写 vPLC
 *
 * 自动检测端口：
 *   1. 优先读 .port.json（vPLC 启动后写出，含自动回退后的真实端口）
 *   2. 其次读 vplc-config.json
 *   3. 兜底默认 :1200(S7) / :1201(HTTP)
 */

import net from 'net'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

interface VplcPorts {
  s7: number
  webApi: number
}

function detectPorts(): VplcPorts {
  // 1. 运行时端口文件（含自动回退）
  try {
    const pj = JSON.parse(fs.readFileSync(path.resolve(__dirname, '..', '.port.json'), 'utf-8'))
    return { s7: pj.s7Port ?? 1200, webApi: pj.webApiPort ?? 1201 }
  } catch {}

  // 2. 配置文件
  try {
    const cfg = JSON.parse(fs.readFileSync(path.resolve(__dirname, '..', 'vplc-config.json'), 'utf-8'))
    const s7 = cfg.port ?? 1200
    return { s7, webApi: s7 + 1 }
  } catch {}

  return { s7: 1200, webApi: 1201 }
}

const ports = detectPorts()

/** 从 vPLC HTTP API 读取 I/Q 区数据 */
export async function fetchIO(): Promise<{ i: Record<number, number>; q: Record<number, number> } | null> {
  try {
    const res = await fetch(`http://localhost:${ports.webApi}/api/vplc`)
    if (!res.ok) return null
    const d = await res.json()
    if (!d || !d.PE) return null
    const i: Record<number, number> = {}
    const q: Record<number, number> = {}
    for (let b = 0; b < d.PE.length; b++) if (d.PE[b] !== undefined) i[b] = d.PE[b]
    for (let b = 0; b < d.PA.length; b++) if (d.PA[b] !== undefined) q[b] = d.PA[b]
    return { i, q }
  } catch { return null }
}

/** TCP 直写 Q/M 区 (COTP + S7) */
export async function writeByte(area: 'q' | 'm', byteAddr: number, value: number): Promise<void> {
  const host = '127.0.0.1'
  const port = ports.s7
  const areaCode = area === 'q' ? 0x82 : 0x83

  return new Promise((resolve, reject) => {
    const sock = new net.Socket()
    const tOut = setTimeout(() => { sock.destroy(); reject(new Error('vPLC 直写超时')) }, 3000)
    sock.connect(port, host, () => {
      const cr = Buffer.alloc(22)
      cr[0] = 0x03; cr[1] = 0x00; cr.writeUInt16BE(22, 2)
      cr[4] = 0x11; cr[5] = 0xE0; cr[6] = 0x00; cr[7] = 0x00
      cr[8] = 0x00; cr[9] = 0x01; cr[10] = 0x00
      cr[11] = 0xC1; cr[12] = 0x02; cr.writeUInt16BE(0x0100, 13)
      cr[15] = 0xC2; cr[16] = 0x02; cr.writeUInt16BE(0x0102, 17)
      sock.write(cr)
    })
    let waitCC = true, buf = Buffer.alloc(0)
    sock.on('data', (chunk) => {
      buf = Buffer.concat([buf, chunk])
      if (buf.length < 4) return
      const tlen = buf.readUInt16BE(2)
      if (buf.length < tlen) return
      if (waitCC && buf[5] === 0xD0) {
        waitCC = false
        const req = Buffer.alloc(32)
        let o = 0
        req[o++] = 0x03; req[o++] = 0x00; req[o++] = 0x00; req[o++] = 0x20
        req[o++] = 0x02; req[o++] = 0xF0; req[o++] = 0x80
        req[o++] = 0x32; req[o++] = 0x01; req[o++] = 0x00; req[o++] = 0x00
        req[o++] = 0x00; req[o++] = 0x01
        req[o++] = 0x00; req[o++] = 0x0E; req[o++] = 0x00; req[o++] = 0x01
        req[o++] = 0x05; req[o++] = 0x01
        req[o++] = 0x12; req[o++] = 0x0A; req[o++] = 0x10
        req[o++] = 0x04; req[o++] = 0x00; req[o++] = 0x01
        req[o++] = 0x00; req[o++] = 0x00; req[o++] = areaCode
        req[o++] = 0x00
        req[o++] = byteAddr >> 8
        req[o++] = byteAddr & 0xFF
        req[o++] = value
        sock.write(req)
        buf = Buffer.alloc(0)
      } else if (!waitCC && buf[5] === 0xF0 && buf.length >= tlen) {
        clearTimeout(tOut)
        sock.destroy()
        const ok = buf[tlen - 1] === 0xFF
        if (ok) resolve()
        else reject(new Error('vPLC 写入返回非 0xFF'))
      }
    })
    sock.on('error', (e) => { clearTimeout(tOut); reject(e) })
  })
}

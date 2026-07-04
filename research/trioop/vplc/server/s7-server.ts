/**
 * S7 TCP 服务器
 * 处理 ISO-on-TCP 连接管理，派发 S7 协议帧
 */

import net from 'net'
import { handleCOTPConnect, handleCOTPData } from './s7-protocol.js'

export interface S7ServerOptions {
  host: string
  preferredPort: number
  onListening?: (port: number) => void
  onError?: (err: Error) => void
}

/** 创建 S7 TCP 服务器 */
export function createS7Server(options: S7ServerOptions): Promise<net.Server> {
  return new Promise((resolve) => {
    const server = net.createServer((sock) => {
      let cotpConnected = false

      sock.on('data', (data: Buffer) => {
        try {
          if (data.length < 4) return
          const tpktLen = data.readUInt16BE(2)
          const payload = data.subarray(4, tpktLen)

          if (!cotpConnected) {
            if (handleCOTPConnect(sock, payload)) {
              cotpConnected = true
            }
            return
          }

          // COTP DT → 交给协议层
          handleCOTPData(sock, payload)
        } catch { /* 协议解析异常不崩溃 */ }
      })

      sock.on('error', () => {})
      sock.on('close', () => { cotpConnected = false })
    })

    let portRef = options.preferredPort

    server.on('error', (err: any) => {
      if (err.code === 'EACCES') {
        console.error(`\n╔════════════════════════════════════╗`)
        console.error(`║  权限不足！端口 ${portRef}           ║`)
        console.error(`║  修改 vplc-config.json 改端口      ║`)
        console.error(`╚════════════════════════════════════╝`)
        process.exit(1)
      }
      if (err.code === 'EADDRINUSE' && portRef < 65535) {
        portRef++
        server.listen(portRef, options.host)
      } else {
        options.onError?.(err)
      }
    })

    server.listen(portRef, options.host)
    server.on('listening', () => {
      const actualPort = (server.address() as net.AddressInfo).port
      options.onListening?.(actualPort)
      resolve(server)
    })
  })
}

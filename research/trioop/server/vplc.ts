/**
 * 虚拟 S7-1200 PLC — 纯 Node.js 实现
 *
 * 实现 ISO-on-TCP (RFC1006) + S7 协议 Read/Write，
 * 无需任何原生依赖，兼容所有 Node.js 版本。
 *
 * 启动：pnpm dev:vplc
 * 连接：PLC IP 127.0.0.1, Rack 0, Slot 1
 */

import net from 'net'

const PORT = parseInt(process.env.PORT || '') || 102

// ─── PLC 内存 ──────────────────────────────────────────────
const memory = {
  DB: {} as Record<number, Uint8Array>,
  PE: new Uint8Array(256),   // I 区
  PA: new Uint8Array(256),   // Q 区
  MK: new Uint8Array(256),   // M 区
  TM: new Uint8Array(256),   // 定时器
  CT: new Uint8Array(256),   // 计数器
}

// 初始化 DB
memory.DB[6] = new Uint8Array(64)
memory.DB[7] = new Uint8Array(100)
memory.DB[1] = new Uint8Array(64)

// ─── 模拟数据初始化 ────────────────────────────────────────
function setDB6() {
  const buf = memory.DB[6]
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength)
  dv.setFloat32(38, 0, false)    // position
  dv.setFloat32(42, 0, false)    // target
  dv.setFloat32(46, 0, false)    // speed
}

function setDB7() {
  const buf = memory.DB[7]
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength)
  dv.setUint8(0, 0b00000000)    // X0.0-X0.7: startBtn, stopBtn, running, alarm...
  dv.setFloat32(38, 25, false)  // temp
  dv.setFloat32(42, 0.5, false) // pressure
}

setDB6()
setDB7()

// ─── 模拟数据变化 ──────────────────────────────────────────
function simulate() {
  const now = Date.now()
  const db7 = memory.DB[7]
  const dv7 = new DataView(db7.buffer, db7.byteOffset, db7.byteLength)

  // 温度、压力波动
  dv7.setFloat32(38, 25 + Math.sin(now / 3000) * 3 + Math.random() * 0.5, false)
  dv7.setFloat32(42, 0.5 + Math.sin(now / 5000) * 0.2 + Math.random() * 0.05, false)

  // 位置波动
  const db6 = memory.DB[6]
  const dv6 = new DataView(db6.buffer, db6.byteOffset, db6.byteLength)
  dv6.setFloat32(38, Math.max(0, Math.min(100, (Math.sin(now / 2000) + 1) * 50)), false)

  // Q8 点位的模拟（如果 Q8.2=1 表示运行，则 Q8.3 周期性变化）
  const qb8 = memory.PA[8]
  if (qb8 & 0b00000100) {
    const cycle = Math.floor(now / 1200) % 4
    memory.PE[8] = (memory.PE[8] & 0xF0) | (cycle === 0 || cycle === 2 ? 0x08 : 0x00)
  }
}

// ─── S7 协议 ───────────────────────────────────────────────
// ISO-on-TCP (RFC1006): TPKT(4) + COTP(可变) + S7(可变)

/** 发送 TPKT + COTP + S7 响应 */
function sendS7(sock: net.Socket, s7payload: Buffer) {
  // COTP Connection Response (TSDU)
  const cotp = Buffer.alloc(2)
  cotp[0] = 0x02    // DT (Data Transfer)
  cotp[1] = 0xF0    // 保留

  const tpktLen = 4 + cotp.length + s7payload.length
  const tpkt = Buffer.alloc(4)
  tpkt[0] = 0x03    // 版本
  tpkt[1] = 0x00
  tpkt.writeUInt16BE(tpktLen, 2)

  sock.write(Buffer.concat([tpkt, cotp, s7payload]))
}

/** 构建 S7 Read 响应 */
function s7ReadResponse(reqData: Buffer, resultData: Buffer) {
  const paramLen = 2
  const dataLen = resultData.length
  const header = Buffer.alloc(12 + paramLen + dataLen)

  // S7 Header
  header[0] = 0x32          // Protocol ID
  header[1] = 0x01          // Message Type: Response
  header[2] = 0x00          // Reserved
  header[3] = 0x00          // Reserved
  header.writeUInt16BE(8 + paramLen + dataLen, 4)  // PDU Ref
  header[6] = 0x00          // Param length high
  header[7] = paramLen      // Param length low
  header[8] = dataLen >> 8  // Data length high
  header[9] = dataLen & 0xFF// Data length low

  // S7 Parameter: Read Response
  header[10] = 0xFF         // Item count
  header[11] = 0x00         // ?

  // S7 Data: Returned items
  resultData.copy(header, 12)

  return header
}

/** 构建 S7 Write 响应 */
function s7WriteResponse() {
  const buf = Buffer.alloc(14)
  buf[0] = 0x32
  buf[1] = 0x01
  buf[2] = 0x00
  buf[3] = 0x00
  buf.writeUInt16BE(10, 4)  // PDU Ref
  buf[6] = 0x00
  buf[7] = 0x02             // Param length = 2
  buf[8] = 0x00
  buf[9] = 0x00             // Data length = 0

  // S7 Parameter: Write Response
  buf[10] = 0xFF            // Item count
  buf[11] = 0x00            // Return code

  // 没有 data 部分
  const actualLen = 12
  return buf.subarray(0, actualLen)
}

/** 解析 S7 地址并读取 */
function s7ReadArea(area: number, dbNum: number, byteAddr: number, bit: number, count: number, transportSize: number): Buffer | null {
  let mem: Uint8Array | undefined

  if (area === 0x82) mem = memory.PE       // I 区 / PE
  else if (area === 0x83) mem = memory.MK  // M 区
  else if (area === 0x84) {                // DB
    mem = memory.DB[dbNum]
    if (!mem) mem = new Uint8Array(count + byteAddr)  // 不存在就动态创建
  }
  else if (area === 0x85) mem = memory.CT  // 计数器
  else if (area === 0x87) mem = memory.TM  // 定时器
  else if (area === 0x81) mem = memory.PA  // Q 区 / PA
  else return null

  const buf = Buffer.alloc(count + 4)
  // Return item header
  buf[0] = 0xFF      // Return code: OK
  buf[1] = transportSize || 0x04  // Transport size
  buf[2] = count >> 8
  buf[3] = count & 0xFF

  if (transportSize === 0x03) {
    // BIT: 读单个位
    const byteVal = mem[byteAddr] ?? 0
    buf[4] = (byteVal >> bit) & 1
  } else {
    for (let i = 0; i < count; i++) {
      buf[4 + i] = mem[byteAddr + i] ?? 0
    }
  }

  return buf
}

/** 解析 S7 地址并写入 */
function s7WriteArea(area: number, dbNum: number, byteAddr: number, bit: number, data: Buffer): boolean {
  let mem: Uint8Array | undefined

  if (area === 0x82) mem = memory.PE
  else if (area === 0x83) mem = memory.MK
  else if (area === 0x84) mem = memory.DB[dbNum]
  else if (area === 0x81) mem = memory.PA
  else return false

  if (!mem) return false
  if (byteAddr + data.length > mem.length) return false

  for (let i = 0; i < data.length; i++) {
    mem[byteAddr + i] = data[i]
  }
  return true
}

/** 解析 COTP Connection Request，回复 Connection Response */
function handleCOTPConnect(sock: net.Socket, tpktPayload: Buffer): boolean {
  if (tpktPayload.length < 6) return false
  // COTP CR (0x0E)
  if (tpktPayload[0] !== 0x0E) return false

  // 提取 src-tsap / dst-tsap
  const srcTSAP = tpktPayload.subarray(2, 4)
  const dstTSAP = tpktPayload.subarray(4, 6)

  // 构建 COTP CC (Connection Confirm)
  const cc = Buffer.alloc(6)
  cc[0] = 0x0D               // COTP CC
  cc[1] = 0xE0               // 保留
  cc[2] = dstTSAP[0]         // 交换 TSAP
  cc[3] = dstTSAP[1]
  cc[4] = srcTSAP[0]
  cc[5] = srcTSAP[1]

  const tpkt = Buffer.alloc(4)
  tpkt[0] = 0x03
  tpkt[1] = 0x00
  tpkt.writeUInt16BE(4 + cc.length, 2)

  sock.write(Buffer.concat([tpkt, cc]))
  return true
}

// ─── TCP 服务 ──────────────────────────────────────────────
const server = net.createServer((sock) => {
  let cotpConnected = false

  sock.on('data', (data) => {
    if (data.length < 4) return

    // TPKT header
    const tpktLen = data.readUInt16BE(2)
    const payload = data.subarray(4, tpktLen)

    if (!cotpConnected) {
      if (handleCOTPConnect(sock, payload)) {
        cotpConnected = true
      }
      return
    }

    // COPT DT 帧
    let s7Req = payload
    if (payload[0] === 0x02 && payload[1] === 0xF0) {
      s7Req = payload.subarray(2)  // 跳过 COTP DT header
    }

    if (s7Req.length < 12) return

    // S7 Header
    const rosctr = s7Req[1]  // ROSCTR: 1=Job, 2=ACK, 3=ACK-Data, 7=UserData
    if (rosctr !== 0x01) return

    const paramLen = (s7Req[6] << 8) | s7Req[7]
    const dataLen = (s7Req[8] << 8) | s7Req[9]
    const params = s7Req.subarray(10, 10 + paramLen)
    const dataSection = dataLen > 0 ? s7Req.subarray(10 + paramLen, 10 + paramLen + dataLen) : Buffer.alloc(0)

    const funcGroup = params[1]

    if ((funcGroup & 0xF0) === 0x00 && funcGroup !== 0x00) {
      // S7 Read (Function 0x04)
      // 也可能是其他功能，检查 params[0]
    }

    // S7 Read Job (function code 0x04 in params after header)
    if (params[0] === 0x04) {
      // 读取项目的数量
      const itemCount = params[1]
      const results: Buffer[] = []

      for (let i = 0; i < itemCount; i++) {
        const off = 2 + i * 10  // 每个 item 10 bytes
        if (off + 10 > params.length) break

        const transportSize = params[off + 1]
        const count = (params[off + 3] << 8) | params[off + 4]
        const dbNum = (params[off + 5] << 8) | params[off + 6]
        const area = params[off + 7]
        const byteAddr = (params[off + 8] << 8) | params[off + 9]  // 包含 bit 信息

        if (transportSize === 0x03) {
          // Bit access
          const bit = byteAddr & 0x07
          const byteOff = (byteAddr >> 3) & 0xFFFF
          const result = s7ReadArea(area, dbNum, byteOff, bit, 1, 0x03)
          if (result) results.push(result)
        } else {
          // Byte/word access
          const addr = (params[off + 8] << 8) | params[off + 9]
          const result = s7ReadArea(area, dbNum, addr, 0, count, transportSize)
          if (result) results.push(result)
        }
      }

      const respData = Buffer.concat(results)
      const resp = s7ReadResponse(data, respData)
      sendS7(sock, resp)
    }
    // S7 Write Job (function code 0x05)
    else if (params[0] === 0x05) {
      const itemCount = params[1]

      // 解析写入参数
      let dataOff = 0
      for (let i = 0; i < itemCount; i++) {
        const off = 2 + i * 10
        if (off + 10 > params.length) break

        const transportSize = params[off + 1]
        const count = (params[off + 3] << 8) | params[off + 4]
        const dbNum = (params[off + 5] << 8) | params[off + 6]
        const area = params[off + 7]
        const byteAddr = (params[off + 8] << 8) | params[off + 9]

        if (transportSize === 0x03) {
          // Bit write
          const bit = byteAddr & 0x07
          const byteOff = (byteAddr >> 3) & 0xFFFF
          const val = dataSection[dataOff] ?? 0
          const mem = area === 0x84 ? memory.DB[dbNum]
            : area === 0x81 ? memory.PA
            : area === 0x82 ? memory.PE
            : area === 0x83 ? memory.MK : undefined
          if (mem && byteOff < mem.length) {
            if (val) mem[byteOff] |= (1 << bit)
            else mem[byteOff] &= ~(1 << bit)
          }
          dataOff += 1
        } else {
          const byteLen = transportSize === 0x04 ? count
            : transportSize === 0x05 ? count * 2
            : transportSize === 0x06 ? count * 4
            : transportSize === 0x07 ? count * 8
            : count
          const writeData = dataSection.subarray(dataOff, dataOff + byteLen)
          s7WriteArea(area, dbNum, byteAddr, 0, writeData)
          dataOff += byteLen
        }
      }

      sendS7(sock, s7WriteResponse())
    }
    // S7 其他功能（如 SZL 读取、块信息等）— 返回空响应
    else {
      // 返回基本响应
      const resp = Buffer.alloc(18)
      resp[0] = 0x32
      resp[1] = 0x03       // ACK-Data
      resp.writeUInt16BE(22, 4)  // PDU Ref (copied from request)
      resp[6] = 0x00
      resp[7] = 0x02       // Param length = 2
      resp[8] = 0x00
      resp[9] = 0x00
      resp[10] = 0xFF
      resp[11] = 0x00
      const actual = resp.subarray(0, 12)
      sendS7(sock, actual)
    }
  })

  sock.on('error', () => {})
  sock.on('close', () => { cotpConnected = false })
})

server.on('error', (err: any) => {
  if (err.code === 'EACCES') {
    console.error('')
    console.error('╔══════════════════════════════════════════════════╗')
    console.error('║  权限不足！102 端口需要管理员权限。             ║')
    console.error('║                                               ║')
    console.error('║  以管理员身份运行终端再执行:                    ║')
    console.error('║    pnpm dev:vplc                              ║')
    console.error('║                                               ║')
    console.error('║  或用备用端口:                                  ║')
    console.error('║    PORT=1102 pnpm dev:vplc                    ║')
    console.error('╚══════════════════════════════════════════════════╝')
    process.exit(1)
  }
  console.error('服务器错误:', err)
})

server.listen(PORT, '0.0.0.0', () => {
  console.log('')
  console.log('╔══════════════════════════════════════════════╗')
  console.log('║    虚拟 S7-1200 PLC 已启动                   ║')
  console.log(`║    地址: 127.0.0.1:${PORT}                      ║`)
  console.log('║    协议: ISO-on-TCP (S7)                    ║')
  console.log('║    纯 Node.js，零原生依赖                     ║')
  console.log('║                                              ║')
  console.log('║    上位机连接:                               ║')
  console.log('║      IP: 127.0.0.1                          ║')
  console.log('║      Rack: 0, Slot: 1                       ║')
  console.log('║      ConnType: PG / OP / BASIC              ║')
  console.log('║                                              ║')
  console.log('║    模拟区域:                                 ║')
  console.log('║      DB1 / DB6 / DB7                        ║')
  console.log('║      I 区 (IB0-255)                         ║')
  console.log('║      Q 区 (QB0-255)                         ║')
  console.log('║      M 区 (MB0-255)                         ║')
  console.log('║                                              ║')
  console.log('║    模拟值自动变化: 温度/压力/位置             ║')
  console.log('╚══════════════════════════════════════════════╝')
  console.log('')
})

// 模拟定时器
setInterval(simulate, 500)

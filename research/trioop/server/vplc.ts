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
import http from 'http'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

/** 读取配置文件，不存在则创建默认 */
function loadConfig(): { port: number; host: string } {
  const cfgPath = path.resolve(__dirname, 'vplc-config.json')
  try {
    return JSON.parse(fs.readFileSync(cfgPath, 'utf-8'))
  } catch {
    const defaults = { port: 1102, host: '0.0.0.0' }
    fs.writeFileSync(cfgPath, JSON.stringify(defaults, null, 2), 'utf-8')
    return defaults
  }
}

const cfg = loadConfig()
const PORT = cfg.port

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

function startS7Server(port: number) {
  server.listen(port, cfg.host)
  server.on('error', (err: any) => {
    if (err.code === 'EACCES') {
      console.error('')
      console.error('╔══════════════════════════════════════════════════╗')
      console.error('║  权限不足！102 端口需要管理员权限。              ║')
      console.error('║                                               ║')
      console.error('║  修改 server/vplc-config.json 改用高位端口:     ║')
      console.error('║    { "port": 1102 }                           ║')
      console.error('╚══════════════════════════════════════════════════╝')
      process.exit(1)
    }
    if (err.code === 'EADDRINUSE' && port < 65535) {
      console.log(`[vPLC] S7 端口 ${port} 被占用，尝试 ${port + 1}...`)
      startS7Server(port + 1)
    } else {
      console.error('[vPLC] S7 服务器启动失败:', err.message)
      process.exit(1)
    }
  })
}
startS7Server(PORT)
const s7PortRef = { current: PORT }
server.on('listening', () => {
  s7PortRef.current = server.address()?.port || PORT

// ─── Web 仪表盘（HTTP 服务） ──────────────────────────────
const WEB_PORT = PORT + 1  // S7端口+1

function memorySnapshot() {
  const snap: Record<string, any> = { DB: {}, PE: {}, PA: {}, MK: {} }
  for (const [k, v] of Object.entries(memory.DB)) {
    snap.DB[`DB${k}`] = Array.from(v.subarray(0, Math.min(v.length, 64)))
  }
  snap.PE = Array.from(memory.PE.subarray(0, 32))
  snap.PA = Array.from(memory.PA.subarray(0, 32))
  snap.MK = Array.from(memory.MK.subarray(0, 32))

  // 添加解析后的可读值
  const db6 = memory.DB[6]; const dv6 = db6 ? new DataView(db6.buffer, db6.byteOffset, db6.byteLength) : null
  const db7 = memory.DB[7]; const dv7 = db7 ? new DataView(db7.buffer, db7.byteOffset, db7.byteLength) : null
  snap._parsed = {
    DB6: dv6 ? {
      position: dv6.getFloat32(38, false).toFixed(2),
      target: dv6.getFloat32(42, false).toFixed(2),
      speed: dv6.getFloat32(46, false).toFixed(2),
    } : {},
    DB7: dv7 ? {
      startBtn: !!(dv7.getUint8(0) & 0x01),
      stopBtn: !!(dv7.getUint8(0) & 0x02),
      running: !!(dv7.getUint8(0) & 0x04),
      alarm: !!(dv7.getUint8(0) & 0x08),
      sensorA: !!(memory.PE[8] & 0x08),
      sensorB: !!(memory.PE[8] & 0x04),
      valve: !!(memory.PA[8] & 0x20),
      temp: dv7.getFloat32(38, false).toFixed(2),
      pressure: dv7.getFloat32(42, false).toFixed(2),
    } : {},
    Q: {
      QB8: memory.PA[8],
      bits: Array.from({length:8}, (_, i) => !!(memory.PA[8] & (1 << i))),
    },
  }
  return snap
}

const webServer = http.createServer((req, res) => {
  if (req.url === '/api/vplc') {
    res.writeHead(200, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' })
    res.end(JSON.stringify(memorySnapshot()))
    return
  }
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' })
  res.end(`<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8"><title>虚拟 PLC 状态</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:system-ui,-apple-system,sans-serif;background:#0E0F10;color:#E8E6DF;padding:20px}
h1{font-size:18px;margin-bottom:16px;color:#9A9890}
h2{font-size:14px;margin:16px 0 8px;color:#378ADD;border-bottom:1px solid #2E3133;padding-bottom:4px}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:8px}
.card{background:#181A1B;border:1px solid #2E3133;border-radius:6px;padding:10px}
.card .label{font-size:10px;color:#7A7872;text-transform:uppercase;letter-spacing:.5px}
.card .val{font-size:20px;font-weight:600;font-family:'JetBrains Mono','Fira Code',monospace;margin-top:2px}
.card .val.green{color:#1D9E75}
.card .val.red{color:#E24B4A}
.bits{display:flex;gap:4px;margin-top:6px}
.bit{width:24px;height:24px;border-radius:4px;display:flex;align-items:center;justify-content:center;font-size:10px;font-family:monospace;border:1px solid #2E3133}
.bit.on{background:#1D9E75;color:#fff;border-color:#1D9E75}
.bit.off{background:transparent;color:#5F5E5A}
.bytes{display:flex;flex-wrap:wrap;gap:4px;margin-top:4px}
.byte{background:#1F2224;border:1px solid #2E3133;border-radius:4px;padding:4px 6px;font-size:11px;font-family:monospace}
.byte .addr{color:#7A7872;margin-right:4px}
.byte .hex{color:#E8E6DF}
</style>
</head><body>
<h1>🔌 虚拟 S7-1200 PLC · <span id="status">运行中</span></h1>
<p style="font-size:12px;color:#7A7872;margin-bottom:16px">
  S7 端口: ${PORT} &nbsp;|&nbsp; Web 端口: ${WEB_PORT} &nbsp;|&nbsp;
  更新: <span id="uptime">0</span>s
</p>

<h2>DB6 — 滑台位置</h2>
<div class="grid" id="db6"></div>

<h2>DB7 — 综合数据</h2>
<div class="grid" id="db7"></div>

<h2>Q 区 — QB8</h2>
<div class="grid" id="qarea"></div>

<h2>Q 区字节</h2>
<div class="bytes" id="qbytes"></div>

<h2>I 区字节</h2>
<div class="bytes" id="ibytes"></div>

<h2>M 区字节</h2>
<div class="bytes" id="mbytes"></div>

<script>
function createCard(label, val, cls='') {
  return \`<div class="card"><div class="label">\${label}</div><div class="val \${cls}">\${val}</div></div>\`
}
function bitsRow(v, label) {
  let html = '<div class="card"><div class="label">' + label + '</div><div class="bits">'
  for(let i=7;i>=0;i--) html += '<div class="bit ' + (v&(1<<i)?'on':'off') + '">' + i + '</div>'
  return html + '</div></div>'
}
function bytesHtml(data) {
  return data.map((v,i) => '<span class="byte"><span class="addr">' + i + ':</span><span class="hex">0x' + v.toString(16).padStart(2,'0') + '</span></span>').join('')
}
async function refresh() {
  const r = await fetch('/api/vplc')
  const d = await r.json()
  const p = d._parsed
  document.getElementById('db6').innerHTML =
    createCard('位置', p.DB6.position) +
    createCard('目标', p.DB6.target) +
    createCard('速度', p.DB6.speed)
  document.getElementById('db7').innerHTML =
    createCard('温度', p.DB7.temp + ' ℃') +
    createCard('压力', p.DB7.pressure + ' MPa') +
    createCard('运行', p.DB7.running ? '● 运行中' : '○ 停止', p.DB7.running?'green':'') +
    createCard('报警', p.DB7.alarm ? '⚠ 报警' : '正常', p.DB7.alarm?'red':'green') +
    createCard('传感器A', p.DB7.sensorA ? '触发' : '未触发', p.DB7.sensorA?'green':'') +
    createCard('传感器B', p.DB7.sensorB ? '触发' : '未触发', p.DB7.sensorB?'green':'') +
    createCard('阀门', p.DB7.valve ? '开启' : '关闭', p.DB7.valve?'green':'')
  document.getElementById('qarea').innerHTML = bitsRow(d.PA[8]||0, 'QB8')
  document.getElementById('qbytes').innerHTML = bytesHtml(d.PA)
  document.getElementById('ibytes').innerHTML = bytesHtml(d.PE)
  document.getElementById('mbytes').innerHTML = bytesHtml(d.MK)
}
refresh()
setInterval(refresh, 300)
</script>
</body></html>`)
})

function startWebServer(port: number) {
  webServer.listen(port, cfg.host)
  webServer.on('error', (err: any) => {
    if (err.code === 'EADDRINUSE' && port < PORT + 100) {
      startWebServer(port + 1)
    } else {
      console.error(`[vPLC] Web 服务器启动失败 (${port}): ${err.message}`)
    }
  })
}
startWebServer(WEB_PORT)
const webPortRef = { current: WEB_PORT }
webServer.on('listening', () => {
  webPortRef.current = webServer.address()?.port || WEB_PORT
  webReady = true
  printFinalBanner()
})

// 等两个服务器都就绪后再打完整的启动横幅
let s7Ready = false, webReady = false
function printFinalBanner() {
  if (!s7Ready || !webReady) return
  console.log('')
  console.log('╔══════════════════════════════════════════════╗')
  console.log('║    虚拟 S7-1200 PLC 已启动                   ║')
  console.log(`║    S7:  127.0.0.1:${s7PortRef.current}                   ║`)
  console.log(`║    Web: http://localhost:${webPortRef.current}           ║`)
  console.log('║                                              ║')
  console.log('║    上位机连接:                               ║')
  console.log('║      IP: 127.0.0.1  Rack:0  Slot:1          ║')
  console.log('║      Port: ' + s7PortRef.current + '                        ║')
  console.log('║                                              ║')
  console.log('║    模拟区域:  DB1/DB6/DB7  I区 Q区 M区      ║')
  console.log('║    模拟值自动变化: 温度/压力/位置             ║')
  console.log('╚══════════════════════════════════════════════╝')
  console.log('')
}
server.on('listening', () => { s7Ready = true; printFinalBanner() })
webServer.on('listening', () => { webReady = true; printFinalBanner() })

// 模拟定时器
setInterval(simulate, 500)

/**
 * 虚拟 PLC — 纯软件 S7-1200 模拟器
 *
 * 在没有实体 PLC 时，启动此服务即可用 trioop 上位机连接。
 * 模拟了同真实 PLC 一致的 DB6、DB7、Q 区、M 区。
 *
 * 启动：pnpm dev:vplc
 * 连接：上位机配置 IP 127.0.0.1，端口 102（S7 默认）
 */

import { S7PLC } from 'goovplc'

const PORT = 102  // S7 标准端口（Windows 需管理员权限，见下方说明）

// ─── 构建虚拟 PLC ──────────────────────────────────────────
const plc = new S7PLC({
  host: '0.0.0.0',        // 监听所有网卡
  port: PORT,
  areas: [
    // ── DB6：滑台圆盘位置 ──
    {
      name: 'DB6',
      type: 'DB',
      DBNO: 6,
      bytes: 64,
      tags: [
        { name: 'position', type: 'REAL', value: 0 },                 // DB6.DBD38 — 当前位置
        { name: 'target',   type: 'REAL', value: 0 },                 // DB6.DBD42 — 目标位置
        { name: 'speed',    type: 'REAL', value: 0 },                 // DB6.DBD46 — 速度
      ],
    },

    // ── DB7：综合数据 ──
    {
      name: 'DB7',
      type: 'DB',
      DBNO: 7,
      bytes: 100,
      tags: [
        { name: 'startBtn',  type: 'BOOL', value: false },            // DB7.X0.0
        { name: 'stopBtn',   type: 'BOOL', value: false },            // DB7.X0.1
        { name: 'running',   type: 'BOOL', value: false },            // DB7.X0.2
        { name: 'alarm',     type: 'BOOL', value: false },            // DB7.X0.3
        { name: 'sensorA',   type: 'BOOL', value: false },            // DB7.X36.3 — 传感器A
        { name: 'sensorB',   type: 'BOOL', value: false },            // DB7.X36.4 — 传感器B
        { name: 'valve',     type: 'BOOL', value: false },            // DB7.X36.5 — 阀门
        { name: 'temp',      type: 'REAL', value: 25.0 },             // DB7.DBD38 — 温度
        { name: 'pressure',  type: 'REAL', value: 0.5 },              // DB7.DBD42 — 压力
      ],
    },

    // ── Q 区（字节 0~16）─ ─
    {
      name: 'QB0-16',
      type: 'PA',
      bytes: 16,
      tags: [
        { name: 'motor',    type: 'BYTE', value: 0 },                 // QB0
        { name: 'conveyor', type: 'BYTE', value: 0 },                 // QB1
        { name: 'outputs',  type: 'BYTE', value: 0 },                 // QB8 — 含 Q8.3（皮带）、Q8.6（点动）
      ],
    },

    // ── I 区（字节 0~16）─ ─
    {
      name: 'IB0-16',
      type: 'PE',
      bytes: 16,
      tags: [
        { name: 'sensors',  type: 'BYTE', value: 0 },                 // IB0
        { name: 'buttons',  type: 'BYTE', value: 0 },                 // IB1
        { name: 'inputs',   type: 'BYTE', value: 0 },                 // IB8
      ],
    },

    // ── M 区 ──
    {
      name: 'MK0-16',
      type: 'MK',
      bytes: 16,
      tags: [
        { name: 'flags',    type: 'BYTE', value: 0 },                 // MB0
        { name: 'markers',  type: 'BYTE', value: 0 },                 // MB8
      ],
    },
  ],
})

// ─── 模拟数据变化 ──────────────────────────────────────────
// 让温度等数值随时间自动波动，看起来像真实设备
function simulateDrift() {
  try {
    const now = Date.now()
    const temp = 25 + Math.sin(now / 3000) * 3 + Math.random() * 0.5
    const pressure = 0.5 + Math.sin(now / 5000) * 0.2 + Math.random() * 0.05
    const position = Math.max(0, Math.min(100, (Math.sin(now / 2000) + 1) * 50))

    plc.Write('DB6', 'position', position)
    plc.Write('DB7', 'temp', temp)
    plc.Write('DB7', 'pressure', pressure)

    // 如果 running（DB7.X0.2）为 true，模拟传感器信号
    const running = plc.Read('DB7', 'running') as boolean
    if (running) {
      const cycle = Math.floor(now / 1000) % 4
      plc.Write('DB7', 'sensorA', cycle === 0 || cycle === 2)
      plc.Write('DB7', 'sensorB', cycle === 1 || cycle === 2)
    }
  } catch {
    // 启动初期可能还没就绪，忽略
  }
}

// ─── 事件监听 ──────────────────────────────────────────────
plc.on('event', (event: any) => {
  console.log(`[vPLC] ${plc.EventText(event)}`)
})

plc.on('read', (tagObj: any, buffer: any) => {
  // 可选的读日志，默认太吵，注释掉
  // console.log(`[vPLC] 读: ${tagObj?.name}`)
})

plc.on('write', (tagObj: any, buffer: any) => {
  console.log(`[vPLC] 写: ${tagObj?.name} = ${Array.isArray(buffer) ? buffer.join(',') : buffer}`)
})

// ─── 启动 ──────────────────────────────────────────────────
console.log('')
console.log('╔══════════════════════════════════════════╗')
console.log('║    虚拟 S7-1200 PLC 已启动               ║')
console.log(`║    地址: 127.0.0.1:${PORT}                    ║`)
console.log('║    协议: ISO-on-TCP (S7)                 ║')
console.log('║                                           ║')
console.log('║    上位机连接配置:                        ║')
console.log('║      PLC IP: 127.0.0.1                   ║')
console.log('║      Rack: 0, Slot: 1                    ║')
console.log('║      ConnType: PG (1)                    ║')
console.log('║                                           ║')
console.log('║    模拟区域:                              ║')
console.log('║      DB6 — 滑台位置 (REAL)               ║')
console.log('║      DB7 — 综合数据 (BOOL/REAL)          ║')
console.log('║      QB0-16 — Q 区                       ║')
console.log('║      IB0-16 — I 区                       ║')
console.log('║      MB0-16 — M 区                       ║')
console.log('╚══════════════════════════════════════════╝')
console.log('')
console.log('提示: Windows 上 102 端口需管理员权限。')
console.log('      如果启动报 EACCES，用管理员终端运行:')
console.log('        pnpm dev:vplc')
console.log('')
console.log('      或用备用端口 1102:')
console.log('        PORT=1102 pnpm dev:vplc')
console.log('')

// 模拟数据每 500ms 更新一次
setInterval(simulateDrift, 500)

try {
  plc.start_serve()
} catch (err: any) {
  if (err?.code === 'EACCES' || err?.message?.includes('EACCES')) {
    console.error('')
    console.error('╔══════════════════════════════════════════════════╗')
    console.error('║  权限不足！102 端口需要管理员权限。             ║')
    console.error('║                                               ║')
    console.error('║  方案一：以管理员身份运行终端再执行:           ║')
    console.error('║    pnpm dev:vplc                              ║')
    console.error('║                                               ║')
    console.error('║  方案二：换用 1102 端口:                       ║')
    console.error('║    PORT=1102 pnpm dev:vplc                    ║')
    console.error('║    然后上位机连接 127.0.0.1:1102              ║')
    console.error('╚══════════════════════════════════════════════════╝')
    console.error('')
  } else {
    console.error('启动失败:', err)
  }
  process.exit(1)
}

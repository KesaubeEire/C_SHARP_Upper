/**
 * 虚拟 S7-1200 PLC — 纯 Node.js 实现
 *
 * 实现 ISO-on-TCP (RFC1006) + S7 协议 Read/Write，
 * 无需任何原生依赖，兼容所有 Node.js 版本。
 *
 * 启动：pnpm dev:vplc
 * 连接：PLC IP 127.0.0.1, Rack 0, Slot 1
 */

import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import net from 'net'

import { memory, dbsConfig, ensureDbSize } from './plc-memory.js'
import { addDiag } from './plc-state.js'
import { initSimulationDBs, startRuntime, getUserScripts } from './plc-runtime.js'
import { createS7Server } from './s7-server.js'
import { createWebServer } from './web-api.js'
import { createModbusServer, getModbusPort } from './modbus-server.js'
import {
  loadConfig, restoreImports,
  killPreviousInstance, removePidFile,
  loadMemory, saveMemory, startAutoSave,
  cfgPath,
} from './persistence.js'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

// ─── 启动参数 ──
const cfg = loadConfig()
const PORT = cfg.port
const WEB_PORT = PORT + 1
const MODBUS_PORT = PORT + 10

// 恢复 dbsConfig
Object.assign(dbsConfig, cfg.dbs)

// ─── 初始化 DB ──
for (const [dbNum, size] of Object.entries(dbsConfig)) {
  if (!memory.DB[Number(dbNum)]) {
    memory.DB[Number(dbNum)] = new Uint8Array(size)
  }
}

// 从配置恢复 UDT / 导入 DB
try {
  const raw = JSON.parse(fs.readFileSync(cfgPath, 'utf-8'))
  restoreImports(raw)
} catch { /* 忽略 */ }

// 恢复持久化的内存数据
loadMemory()
addDiag('info', 'SYSTEM', 'VPLC 启动中...')

// ─── 初始化模拟数据 ──
initSimulationDBs()

// ─── 服务器引用 ──
let s7Port = PORT
let webPort = WEB_PORT
let modbusServer: net.Server | null = null
let s7Server: net.Server | null = null

// ─── 启动标志 ──
let s7Ready = false, webReady = false, modbusReady = false

const portJsonPath = path.resolve(__dirname, '..', '.port.json')

function writePortJson(ports: { s7: number; webApi: number; modbus: number }) {
  try {
    fs.writeFileSync(portJsonPath, JSON.stringify({
      s7Port: ports.s7, webApiPort: ports.webApi, modbusPort: ports.modbus, updatedAt: Date.now(),
    }, null, 2), 'utf-8')
  } catch { /* 忽略 */ }
}

function printFinalBanner() {
  if (!s7Ready || !webReady) return
  const modbusPort = modbusServer ? getModbusPort(modbusServer) : 0
  writePortJson({ s7: s7Port, webApi: webPort, modbus: modbusPort })
  console.log('')
  console.log('╔══════════════════════════════════════════════════╗')
  console.log('║     虚拟 S7-1200 PLC 已启动                      ║')
  console.log(`║    S7:  127.0.0.1:${s7Port}                              ║`)
  console.log(`║    API: http://localhost:${webPort}/api/vplc              ║`)
  if (modbusPort) console.log(`║    Modbus: 127.0.0.1:${modbusPort}                        ║`)
  console.log('║                                                    ║')
  console.log(`║    上位机: 127.0.0.1 Rack:0 Slot:1 Port:${s7Port}               ║`)
  if (modbusPort) console.log(`║    Modbus TCP: ${modbusPort}                                ║`)
  console.log('║                                                    ║')
  console.log('║    模拟: DB' + Object.keys(dbsConfig).sort((a, b) => Number(a) - Number(b)).join('/') + ' I Q M        ║')
  console.log('║    脚本: ' + (typeof getUserScripts === 'function' ? '已启用' : '未启用') + '                                    ║')
  console.log('╚══════════════════════════════════════════════════╝')
  console.log('')
}

// ─── 杀前一个实例 ──
killPreviousInstance()

// ─── 启动 S7 服务器 ──
createS7Server({
  host: cfg.host,
  preferredPort: PORT,
  onListening: (port) => {
    s7Port = port
    s7Ready = true
    printFinalBanner()
  },
  onError: (err) => {
    console.error('[vPLC] S7 服务器启动失败:', err.message)
    process.exit(1)
  },
}).then(srv => { s7Server = srv })

// ─── 启动 Web 服务器 ──
{
  const webServer = createWebServer()
  let wp = WEB_PORT
  webServer.on('error', (err: any) => {
    if (err.code === 'EADDRINUSE' && wp < PORT + 100) {
      wp++
      webServer.listen(wp, cfg.host)
    } else {
      console.error(`[vPLC] Web 服务器启动失败 (${wp}): ${err.message}`)
    }
  })
  webServer.listen(wp, cfg.host, () => {
    webPort = wp
    webReady = true
    printFinalBanner()
  })
}

// ─── 启动 Modbus TCP 服务器 ──
createModbusServer({ preferredPort: MODBUS_PORT })
  .then(srv => {
    modbusServer = srv
    modbusReady = true
    printFinalBanner()
  })
  .catch((err) => {
    console.error('[vPLC] Modbus 服务器启动失败:', err.message)
  })

// ─── 启动运行时 ──
startRuntime()
addDiag('info', 'SYSTEM', `VPLC 启动完成. DB 块: ${Object.keys(dbsConfig).join(',')}`)

// ─── 自动保存 ──
startAutoSave()

// ─── 优雅退出 ──
function shutdown() {
  console.log('\n[vPLC] 正在关闭...')
  saveMemory()
  s7Server?.close()
  modbusServer?.close()
  removePidFile()
  process.exit(0)
}
process.on('SIGINT', shutdown)
process.on('SIGTERM', shutdown)


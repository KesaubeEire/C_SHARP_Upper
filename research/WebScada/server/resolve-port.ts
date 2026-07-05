/**
 * CLI: 解析端口并写入 .port.json
 * 用法: tsx server/resolve-port.ts [base_port]
 */
import { resolveAndSavePort } from './port.js'

const base = parseInt(process.argv[2] || '3000')
const port = await resolveAndSavePort(base)
console.log(`[Port] API 端口已分配: ${port}`)

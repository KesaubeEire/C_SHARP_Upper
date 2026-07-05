import net from 'net'
import fs from 'fs'
import path from 'path'
import { createHash } from 'crypto'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const PORT_FILE = path.resolve(__dirname, '..', '.port.json')

/**
 * 根据当前工作目录路径计算一个 0-99 的偏移量。
 * 同一个 worktree 每次启动得到相同的偏移，端口稳定。
 */
function calcOffset(): number {
  const hash = createHash('md5').update(process.cwd()).digest('hex')
  return parseInt(hash.slice(0, 4), 16) % 100
}

/**
 * 查找从 start 开始的第一个可用端口
 */
function findFreePort(start: number): Promise<number> {
  return new Promise((resolve) => {
    const server = net.createServer()
    server.unref()
    server.on('error', () => {
      resolve(findFreePort(start + 1))
    })
    server.listen(start, () => {
      const port = (server.address() as net.AddressInfo).port
      server.close(() => resolve(port))
    })
  })
}

/**
 * 基于基线端口 + 路径 hash 偏移，找空闲端口，写入 .port.json。
 * 不同 worktree 的偏移不同，端口互不冲突且稳定可预测。
 */
export async function resolveAndSavePort(base: number = 3000): Promise<number> {
  const start = base + calcOffset()
  const port = await findFreePort(start)
  fs.writeFileSync(PORT_FILE, JSON.stringify({ port }), 'utf-8')
  return port
}

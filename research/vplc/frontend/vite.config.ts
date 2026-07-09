import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const portJsonPath = path.resolve(__dirname, '..', '.port.json')
const cfgPath = path.resolve(__dirname, '..', 'vplc-config.json')

/**
 * 解析 Web API 端口：
 *   1. .port.json（vPLC 启动后写出，含回退后的真实端口）
 *   2. vplc-config.json（port + 1）
 *   3. 兜底 1201
 */
function resolveWebApiPort(): number {
  try {
    const pj = JSON.parse(fs.readFileSync(portJsonPath, 'utf-8'))
    if (pj.webApiPort) return pj.webApiPort
  } catch {}
  try {
    const raw = JSON.parse(fs.readFileSync(cfgPath, 'utf-8'))
    return (raw.port ?? 1200) + 1
  } catch {
    return 1201
  }
}

const target = `http://localhost:${resolveWebApiPort()}`

const PREFERRED_PORT = 1520

export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: PREFERRED_PORT,
    strictPort: false, // 端口被占自动回退
    proxy: {
      '/api/vplc': { target, changeOrigin: true },
    },
  },
  define: {
    __VPLC_API_PORT__: JSON.stringify(resolveWebApiPort()),
  },
})

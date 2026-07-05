import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'
import fs from 'fs'

export default defineConfig({
  plugins: [react()],
  define: {
    'process.env.NODE_ENV': JSON.stringify(process.env.NODE_ENV || 'development'),
  },
  resolve: {
    alias: {
      '@shared': path.resolve(__dirname, 'shared'),
      'react-leaflet': path.resolve(__dirname, 'src/stubs/empty.ts'),
      'leaflet': path.resolve(__dirname, 'src/stubs/empty.ts'),
      'mqtt': path.resolve(__dirname, 'src/stubs/empty.ts'),
    },
  },
  build: {
    rollupOptions: {
      external: ['react-leaflet', 'leaflet', 'react-grid-layout', 'react-grid-layout/css/styles.css', 'mqtt', '@altara/core', '@altara/industrial'],
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.API_TARGET || readBackendPort(),
        changeOrigin: true,
      },
    },
  },
})

/**
 * 读取后端写入的 .port.json，用作 proxy target。
 * worktree 中后端自动跳到空闲端口，前端跟随。
 */
function readBackendPort(): string {
  try {
    const p = path.resolve(__dirname, '.port.json')
    return `http://localhost:${JSON.parse(fs.readFileSync(p, 'utf-8')).port}`
  } catch {
    return 'http://localhost:3001'
  }
}

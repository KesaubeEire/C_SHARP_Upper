import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 1202,
    proxy: {
      '/api/vplc': { target: 'http://localhost:1201', changeOrigin: true },
    },
  },
})

import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@shared': path.resolve(__dirname, 'shared'),
      'react-leaflet': path.resolve(__dirname, 'src/stubs/empty.ts'),
      'leaflet': path.resolve(__dirname, 'src/stubs/empty.ts'),
      'react-grid-layout': path.resolve(__dirname, 'src/stubs/empty.ts'),
      'mqtt': path.resolve(__dirname, 'src/stubs/empty.ts'),
    },
  },
  build: {
    rollupOptions: {
      external: ['react-leaflet', 'leaflet', 'react-grid-layout', 'mqtt'],
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:3001',
        changeOrigin: true,
      },
    },
  },
})

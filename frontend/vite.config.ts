import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // En desarrollo, /api/* se redirige a la API local. Evita CORS: para el navegador es el mismo origen.
    proxy: {
      '/api': {
        target: process.env.API_URL ?? 'http://localhost:5236',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
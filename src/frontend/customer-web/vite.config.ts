import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const {
    CATALOG_API_PROXY_TARGET = 'http://localhost:8080',
    BOOKING_API_PROXY_TARGET = 'http://localhost:8081',
    TICKET_API_PROXY_TARGET = 'http://localhost:8082',
  } = loadEnv(
    mode,
    process.cwd(),
    '',
  )

  return {
    plugins: [react()],
    server: {
      proxy: {
        '/api': {
          target: CATALOG_API_PROXY_TARGET,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/api/, ''),
        },
        '/booking-api': {
          target: BOOKING_API_PROXY_TARGET,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/booking-api/, ''),
        },
        '/ticket-api': {
          target: TICKET_API_PROXY_TARGET,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/ticket-api/, ''),
        },
      },
    },
  }
})

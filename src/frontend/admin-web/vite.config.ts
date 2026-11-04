import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // dotnet run uses port 5080; set CATALOG_API_PROXY_TARGET to http://localhost:8080 for Docker.
  const {
    CATALOG_API_PROXY_TARGET = 'http://localhost:8080',
    BOOKING_API_PROXY_TARGET = 'http://localhost:8081',
    TICKET_API_PROXY_TARGET = 'http://localhost:8082',
    PAYMENT_API_PROXY_TARGET = 'http://localhost:5111',
  } = loadEnv(
    mode,
    process.cwd(),
    '',
  )

  return {
    plugins: [react()],
    server: {
      proxy: {
        '/payment-api': {
          target: PAYMENT_API_PROXY_TARGET,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/payment-api/, ''),
        },
        // Keep /api so it cannot conflict with React Router's /venues and /events pages.
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

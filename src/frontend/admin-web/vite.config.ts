import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'
import { createApiProxy } from './proxyHeaders.ts'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Preserve API prefixes; ApiGateway handles routing and prefix removal.
  const { API_GATEWAY_PROXY_TARGET = 'http://localhost:5212' } = loadEnv(
    mode,
    process.cwd(),
    '',
  )

  return {
    plugins: [react()],
    server: {
      proxy: {
        '/payment-api': createApiProxy(API_GATEWAY_PROXY_TARGET),
        // Keep /api so it cannot conflict with React Router's /venues and /events pages.
        '/api': createApiProxy(API_GATEWAY_PROXY_TARGET),
        '/booking-api': createApiProxy(API_GATEWAY_PROXY_TARGET, true),
        '/ticket-api': createApiProxy(API_GATEWAY_PROXY_TARGET),
      },
    },
  }
})

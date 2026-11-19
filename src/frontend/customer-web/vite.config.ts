import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'
import { createApiProxy } from './proxyHeaders.ts'

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
      allowedHosts: ['troy-agreed-wiley-campus.trycloudflare.com'],
      proxy: {
        '/api': createApiProxy(API_GATEWAY_PROXY_TARGET),
        '/booking-api': createApiProxy(API_GATEWAY_PROXY_TARGET),
        '/ticket-api': createApiProxy(API_GATEWAY_PROXY_TARGET),
      },
    },
  }
})

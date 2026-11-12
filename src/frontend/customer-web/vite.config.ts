import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

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
        '/api': {
          target: API_GATEWAY_PROXY_TARGET,
          changeOrigin: true,
        },
        '/booking-api': {
          target: API_GATEWAY_PROXY_TARGET,
          changeOrigin: true,
        },
        '/ticket-api': {
          target: API_GATEWAY_PROXY_TARGET,
          changeOrigin: true,
        },
      },
    },
  }
})

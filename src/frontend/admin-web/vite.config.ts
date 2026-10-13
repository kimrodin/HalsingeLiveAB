import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // dotnet run uses port 5080; set CATALOG_API_PROXY_TARGET to http://localhost:8080 for Docker.
  const { CATALOG_API_PROXY_TARGET = 'http://localhost:8080' } = loadEnv(
    mode,
    process.cwd(),
    '',
  )

  return {
    plugins: [react()],
    server: {
      proxy: {
        // Keep /api so it cannot conflict with React Router's /venues and /events pages.
        '/api': {
          target: CATALOG_API_PROXY_TARGET,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/api/, ''),
        },
      },
    },
  }
})
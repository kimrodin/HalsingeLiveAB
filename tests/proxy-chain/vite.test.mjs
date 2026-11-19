import assert from 'node:assert/strict'
import { createServer as createHttpServer, request } from 'node:http'
import { once } from 'node:events'
import { resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { test } from 'node:test'

const root = fileURLToPath(new URL('../../', import.meta.url))
const spoofedHeaders = {
  'X-Forwarded-For': '203.0.113.90, 203.0.113.91',
  'X-Forwarded-Proto': 'https',
  'X-Real-IP': '203.0.113.92',
  Forwarded: 'for=203.0.113.93',
  'X-Original-For': '203.0.113.94',
}

for (const app of ['admin-web', 'customer-web']) {
  test(`${app}: actual Vite proxy replaces forged headers for IPv4 and IPv6`, async () => {
    const echo = createHttpServer((req, res) => {
      res.setHeader('Content-Type', 'application/json')
      res.end(JSON.stringify(req.headers))
    })
    echo.on('upgrade', (req, socket) => {
      socket.end('HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: websocket\r\n' +
        `X-Probe-For: ${req.headers['x-forwarded-for']}\r\n` +
        `X-Probe-Real: ${req.headers['x-real-ip'] ?? 'absent'}\r\n` +
        `X-Probe-Forwarded: ${req.headers.forwarded ?? 'absent'}\r\n\r\n`)
    })
    echo.listen(0, '127.0.0.1')
    await once(echo, 'listening')
    const previousTarget = process.env.API_GATEWAY_PROXY_TARGET
    process.env.API_GATEWAY_PROXY_TARGET = `http://127.0.0.1:${echo.address().port}`
    let vite
    try {
      const { createServer } = await import(pathToFileURL(resolve(root, `src/frontend/${app}/node_modules/vite/dist/node/index.js`)))
      vite = await createServer({
        root: resolve(root, `src/frontend/${app}`),
        configFile: resolve(root, `src/frontend/${app}/vite.config.ts`),
        server: { host: '::', port: 0, hmr: false, watch: null },
        optimizeDeps: { noDiscovery: true, entries: [] },
        logLevel: 'error',
      })
      await vite.listen()
      const port = vite.httpServer.address().port
      for (const [host, expected] of [['127.0.0.1', /^(::ffff:)?127\.0\.0\.1$/], ['[::1]', /^::1$/]]) {
        const response = await fetch(`http://${host}:${port}/booking-api/probe`, { headers: spoofedHeaders })
        assert.equal(response.status, 200)
        const headers = await response.json()
        assert.match(headers['x-forwarded-for'], expected)
        assert.equal(headers['x-forwarded-proto'], 'http')
        for (const name of ['forwarded', 'x-real-ip', 'x-original-for']) assert.equal(headers[name], undefined)
      }
      if (app === 'admin-web') {
        await new Promise((accept, reject) => {
          const req = request({ hostname: '127.0.0.1', port, path: '/booking-api/probe', headers: {
            ...spoofedHeaders, Connection: 'Upgrade', Upgrade: 'websocket',
          } })
          req.on('upgrade', (res, socket) => {
            try {
              assert.match(res.headers['x-probe-for'], /^(::ffff:)?127\.0\.0\.1$/)
              assert.equal(res.headers['x-probe-real'], 'absent')
              assert.equal(res.headers['x-probe-forwarded'], 'absent')
              socket.destroy()
              accept()
            } catch (error) { socket.destroy(); reject(error) }
          })
          req.on('error', reject)
          req.setTimeout(5000, () => req.destroy(new Error('WebSocket proxy timed out.')))
          req.end()
        })
      }
    } finally {
      if (previousTarget === undefined) delete process.env.API_GATEWAY_PROXY_TARGET
      else process.env.API_GATEWAY_PROXY_TARGET = previousTarget
      await vite?.close()
      echo.closeAllConnections()
      await new Promise(accept => echo.close(accept))
    }
  })
}

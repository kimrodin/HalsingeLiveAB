import type { ProxyOptions } from 'vite'

// Vite is the outer proxy in development: client-supplied forwarding claims
// must be replaced, including WebSocket handshakes.
export function createApiProxy(target: string, ws = false): ProxyOptions {
  return {
    target,
    changeOrigin: true,
    ws,
    configure(proxy) {
      const setClientHeaders = (proxyRequest: import('node:http').ClientRequest, request: import('node:http').IncomingMessage) => {
        const address = request.socket.remoteAddress
        if (!address) {
          proxyRequest.destroy(new Error('Client connection address is unavailable.'))
          return
        }
        for (const header of ['forwarded', 'x-real-ip', 'x-forwarded-for', 'x-forwarded-proto', 'x-forwarded-host', 'x-forwarded-prefix', 'x-original-for', 'x-original-proto', 'x-original-host', 'x-original-prefix']) {
          proxyRequest.removeHeader(header)
        }
        proxyRequest.setHeader('X-Forwarded-For', address)
        proxyRequest.setHeader('X-Forwarded-Proto', 'encrypted' in request.socket && request.socket.encrypted ? 'https' : 'http')
      }
      proxy.on('proxyReq', setClientHeaders)
      proxy.on('proxyReqWs', setClientHeaders)
    },
  }
}

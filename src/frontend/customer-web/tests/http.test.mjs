import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createServer } from 'node:http'
import { ApiError, postJson } from '../src/shared/api/http.ts'

test('429 retains server message and Retry-After, including malformed responses', async () => {
  const server = createServer((request, response) => {
    response.writeHead(429, { 'Retry-After': request.url === '/invalid-header' ? 'invalid' : '42' })
    response.end(request.url === '/invalid-json' ? 'unavailable' : JSON.stringify({ detail: 'Vänta innan nästa försök.' }))
  })
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve))
  const base = `http://127.0.0.1:${server.address().port}`
  try {
    await assert.rejects(postJson('/quota', {}, base), error => {
      assert.ok(error instanceof ApiError)
      assert.equal(error.status, 429)
      assert.equal(error.message, 'Vänta innan nästa försök.')
      assert.equal(error.retryAfterSeconds, 42)
      return true
    })
    await assert.rejects(postJson('/invalid-json', {}, base), error => error.retryAfterSeconds === 42)
    await assert.rejects(postJson('/invalid-header', {}, base), error => error.retryAfterSeconds === undefined)
  } finally {
    await new Promise(resolve => server.close(resolve))
  }
})

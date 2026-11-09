const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Execute the real k6 entry point against scripted HTTP responses. No k6 dependencies on the host.
async function harness(responses, env = {}, iteration = 1) {
  const metrics = new Map();
  const requests = [];
  let now = 0;
  class Metric {
    constructor(name) { this.values = []; metrics.set(name, this); }
    add(value) { this.values.push(Number(value)); }
    get total() { return this.values.reduce((a, b) => a + b, 0); }
  }
  function request(method, url, options) {
    const next = responses.shift();
    assert.ok(next, `unexpected ${method} ${url}`);
    assert.equal(method, next.method);
    assert.ok(url.includes(next.path), `${url} did not match ${next.path}`);
    requests.push({ method, url, options });
    return { status: next.status, timings: { duration: 5 }, json: () => next.body };
  }
  const context = vm.createContext({
    __ENV: { ABANDON_PERCENT: '0', CANCEL_PERCENT: '0', ...env },
    Date: { now: () => now }, console: { log() {}, error() {} },
  });
  const imports = {
    'k6/http': { default: {
      expectedStatuses: (...statuses) => statuses,
      get: (url, options) => request('GET', url, options),
      post: (url, body, options) => request('POST', url, options),
    } },
    'k6': {
      check: (value, predicates) => Object.values(predicates).every((predicate) => predicate(value)),
      sleep: (seconds) => { now += seconds * 1000; },
    },
    'k6/execution': { default: { scenario: { name: 'burst', iterationInTest: iteration } } },
    'k6/metrics': { Counter: Metric, Rate: Metric, Trend: Metric },
  };
  const source = new vm.SourceTextModule(fs.readFileSync(path.join(__dirname, 'release.js'), 'utf8'), { context });
  await source.link((name) => {
    const exports = imports[name];
    return new vm.SyntheticModule(Object.keys(exports), function () {
      for (const [key, value] of Object.entries(exports)) this.setExport(key, value);
    }, { context });
  });
  await source.evaluate();
  source.namespace.attempt();
  assert.equal(responses.length, 0, 'journey failed to perform all expected requests');
  return { metrics, requests };
}

const reserve = { method: 'POST', path: '/CreateBooking', status: 202,
  body: { bookingId: 'booking-1', guestAccessToken: 'token-1' } };
const start = { method: 'POST', path: '/StartPayment/', status: 202, body: {} };
const status = (state, paymentStatus) => ({ method: 'GET', path: '/GetBooking/', status: 200,
  body: { status: state, paymentStatus } });
const tickets = (items) => ({ method: 'GET', path: '/GetTicketsForBooking/', status: 200, body: items });
const valid = (id) => ({ ticketId: id, bookingId: 'booking-1', status: 'Valid' });

test('stock exhaustion is an expected business outcome', async () => {
  const { metrics, requests } = await harness([{ method: 'POST', path: '/CreateBooking', status: 409,
    body: { title: 'Not enough tickets are available.' } }]);
  assert.equal(metrics.get('release_sold_out').total, 1);
  assert.equal(metrics.get('release_unexpected_error').total, 0);
  assert.deepEqual(Array.from(requests[0].options.responseCallback), [202, 409]);
});

test('a different 409 cannot masquerade as sold out', async () => {
  const { metrics } = await harness([{ method: 'POST', path: '/CreateBooking', status: 409,
    body: { title: 'The event or section is no longer available.' } }]);
  assert.equal(metrics.get('release_sold_out').total, 0);
  assert.equal(metrics.get('release_unexpected_error').total, 1);
});

test('abandoned reservations are left untouched for real expiry', async () => {
  const { metrics, requests } = await harness([{ ...reserve }], { ABANDON_PERCENT: '10' }, 0);
  assert.equal(requests.length, 1);
  assert.equal(metrics.get('release_abandoned').total, 1);
  assert.equal(metrics.get('release_unexpected_error').total, 0);
});

test('customer cancellation is completed before its outcome is counted', async () => {
  const { metrics } = await harness([{ ...reserve }, { method: 'POST', path: '/CancelBooking/', status: 200,
    body: { status: 'Cancelled' } }], { CANCEL_PERCENT: '10' }, 0);
  assert.equal(metrics.get('release_cancelled').total, 1);
  assert.equal(metrics.get('release_unexpected_error').total, 0);
});

test('a purchase waits for confirmation and the entire requested ticket quantity', async () => {
  const { metrics } = await harness([{ ...reserve }, { ...start }, status('Reserved', 'Pending'),
    status('Confirmed', 'Succeeded'), tickets([]), status('Confirmed', 'Succeeded'),
    tickets([valid('a'), valid('b')])], { MAX_QUANTITY: '2' });
  assert.equal(metrics.get('release_paid').total, 1);
  assert.equal(metrics.get('release_unexpected_error').total, 0);
  assert.equal(metrics.get('release_paid_duration').values.length, 1);
});

test('duplicate tickets fail instead of contributing a successful-purchase latency', async () => {
  const { metrics } = await harness([{ ...reserve }, { ...start }, status('Confirmed', 'Succeeded'),
    tickets([valid('a'), valid('a')])], { MAX_QUANTITY: '2' });
  assert.equal(metrics.get('release_paid').total, 0);
  assert.equal(metrics.get('release_paid_duration').values.length, 0);
  assert.equal(metrics.get('release_unexpected_error').total, 1);
});

test('a planned payment rejection accepts its terminal HTTP 409 and verifies booking state', async () => {
  const { metrics } = await harness([{ ...reserve }, { ...start, status: 409 }, status('Cancelled', 'Failed')],
    { PAYMENT_OUTCOME: 'Failed' });
  assert.equal(metrics.get('release_payment_rejected').total, 1);
  assert.equal(metrics.get('release_unexpected_error').total, 0);
});

test('an unexpected cancellation is a technical failure', async () => {
  const { metrics } = await harness([{ ...reserve }, { ...start }, status('Cancelled', 'Failed')]);
  assert.equal(metrics.get('release_payment_rejected').total, 0);
  assert.equal(metrics.get('release_unexpected_error').total, 1);
});

test('a scheduler boundary iteration cannot create an extra reservation', async () => {
  const { metrics, requests } = await harness([], { BURST_RATE: '5', BURST_SECONDS: '4' }, 20);
  assert.equal(requests.length, 0);
  assert.equal(metrics.get('release_attempts').total, 0);
});

const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Execute the real k6 entry point against scripted HTTP responses. No k6 dependencies on the host.
async function harness(responses, env = {}, iteration = 1, scenario = 'burst', script = 'release.js', invoke = true) {
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
    'k6/execution': { default: { scenario: { name: scenario, iterationInTest: iteration } } },
    'k6/metrics': { Counter: Metric, Rate: Metric, Trend: Metric },
  };
  const source = new vm.SourceTextModule(fs.readFileSync(path.join(__dirname, script), 'utf8'), { context });
  await source.link((name) => {
    if (name === './config.js') {
      return new vm.SourceTextModule(fs.readFileSync(path.join(__dirname, 'config.js'), 'utf8'), { context });
    }
    const exports = imports[name];
    return new vm.SyntheticModule(Object.keys(exports), function () {
      for (const [key, value] of Object.entries(exports)) this.setExport(key, value);
    }, { context });
  });
  await source.evaluate();
  if (invoke) source.namespace.attempt();
  assert.equal(responses.length, 0, 'journey failed to perform all expected requests');
  return { metrics, requests, options: JSON.parse(JSON.stringify(source.namespace.options)) };
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

test('ramp delays both purchase plateaus and keeps browsing active throughout', async () => {
  const { options } = await harness([], {
    PRESALE_SECONDS: '2', RAMP_SECONDS: '3', BURST_RATE: '5', BURST_SECONDS: '4',
    TAIL_RATE: '2', TAIL_SECONDS: '4', PREALLOCATED_VUS: '32',
  }, 0, 'ramp', 'release.js', false);
  assert.equal(options.scenarios.ramp.executor, 'ramping-arrival-rate');
  assert.equal(options.scenarios.ramp.startRate, 0);
  assert.deepEqual(options.scenarios.ramp.stages, [{ target: 5, duration: '3s' }]);
  assert.equal(options.scenarios.ramp.startTime, '2s');
  assert.equal(options.scenarios.ramp.preAllocatedVUs, 32);
  assert.equal(options.scenarios.burst.startTime, '5s');
  assert.equal(options.scenarios.tail.startTime, '9s');
  assert.equal(options.scenarios.browse.duration, '13s');
  assert.deepEqual(options.thresholds.release_attempts, ['count==35']);
});

test('disabling ramp preserves the fifty-attempt last-ticket workload', async () => {
  const { options } = await harness([], {
    RAMP_SECONDS: '0', BURST_RATE: '20', BURST_SECONDS: '2', TAIL_RATE: '5', TAIL_SECONDS: '2',
  }, 0, 'burst', 'release.js', false);
  assert.equal(options.scenarios.ramp, undefined);
  assert.deepEqual(options.thresholds.release_attempts, ['count==50']);
});

test('ramp accepts its final interior arrival but excludes an exact end-boundary arrival', async () => {
  const soldOut = { method: 'POST', path: '/CreateBooking', status: 409,
    body: { title: 'Not enough tickets are available.' } };
  // 0 -> 5/s over 4s: arrivals 1..9 are inside the phase; arrival 10 is at t=4s.
  const last = await harness([soldOut], { RAMP_SECONDS: '4', BURST_RATE: '5' }, 8, 'ramp');
  assert.equal(last.metrics.get('release_attempts').total, 1);
  const boundary = await harness([], { RAMP_SECONDS: '4', BURST_RATE: '5' }, 9, 'ramp');
  assert.equal(boundary.requests.length, 0);
  assert.equal(boundary.metrics.get('release_attempts').total, 0);
  // A half-integer area has no arrival at its end: all seven interior arrivals count.
  const fractional = await harness([soldOut], { RAMP_SECONDS: '3', BURST_RATE: '5' }, 6, 'ramp');
  assert.equal(fractional.metrics.get('release_attempts').total, 1);
});

for (const script of ['release.js', 'checkout.js']) {
  test(`${script} exports p99 and applies configurable budgets without weakening reconciliation`, async () => {
    const { options } = await harness([], {
      HTTP_P95_MS: '123', HTTP_P99_MS: '456', CHECKOUT_P95_MS: '789', CHECKOUT_P99_MS: '1234',
      MAX_HTTP_ERROR_RATE: '0.025',
    }, 0, 'burst', script, false);
    assert.ok(options.summaryTrendStats.includes('med'));
    assert.ok(options.summaryTrendStats.includes('p(95)'));
    assert.ok(options.summaryTrendStats.includes('p(99)'));
    assert.ok(options.summaryTrendStats.includes('count'));
    assert.deepEqual(options.thresholds.http_req_duration, ['p(95)<=123', 'p(99)<=456']);
    assert.deepEqual(options.thresholds.http_req_failed, ['rate<=0.025']);
    const journey = script === 'release.js' ? 'release_paid_duration' : 'checkout_duration';
    assert.deepEqual(options.thresholds[journey], ['p(95)<=789', 'p(99)<=1234']);
    assert.deepEqual(options.thresholds.checks, ['rate==1']);
  });
}

test('invalid performance and ramp settings fail before making HTTP requests', async () => {
  for (const env of [
    { RAMP_SECONDS: '-1' }, { RAMP_SECONDS: '1.5' }, { RAMP_SECONDS: 'NaN' },
    { HTTP_P95_MS: '0' }, { HTTP_P99_MS: 'Infinity' }, { CHECKOUT_P95_MS: '' },
    { HTTP_P95_MS: '1000', HTTP_P99_MS: '500' },
    { CHECKOUT_P95_MS: '20000', CHECKOUT_P99_MS: '10000' },
    { MAX_HTTP_ERROR_RATE: '-0.1' }, { MAX_HTTP_ERROR_RATE: '1.1' },
  ]) {
    await assert.rejects(harness([], env, 0, 'ramp', 'release.js', false), /must be/);
  }
});

test('separate warm-up exports latency but only gates correctness', async () => {
  const { options } = await harness([], { WARMUP_ONLY: 'true' }, 0, 'burst', 'checkout.js', false);
  assert.equal(options.thresholds.http_req_duration, undefined);
  assert.equal(options.thresholds.checkout_duration, undefined);
  assert.deepEqual(options.thresholds.http_req_failed, ['rate==0']);
  assert.deepEqual(options.thresholds.completed_checkouts, ['count==10']);
  assert.ok(options.summaryTrendStats.includes('p(99)'));
});

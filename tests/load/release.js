import http from 'k6/http';
import { check, sleep } from 'k6';
import execution from 'k6/execution';
import { Counter, Rate, Trend } from 'k6/metrics';
import { numberSetting, performanceThresholds, summaryTrendStats } from './config.js';

const gateway = 'http://api-gateway:8080';
const baseUrl = `${gateway}/booking-api`;
const eventId = '73000000-0000-0000-0000-000000000001';
const inventoryId = '74000000-0000-0000-0000-000000000001';
const preSaleSeconds = Number(__ENV.PRESALE_SECONDS || 30);
const burstSeconds = Number(__ENV.BURST_SECONDS || 20);
const tailSeconds = Number(__ENV.TAIL_SECONDS || 40);
const burstRate = Number(__ENV.BURST_RATE || 30);
const tailRate = Number(__ENV.TAIL_RATE || 10);
const rampSeconds = numberSetting('RAMP_SECONDS', 0, 0, 3600);
// Only arrivals strictly before the phase ends count. k6 can race its deadline
// against an arrival exactly on the boundary; cap that one before business I/O.
const rampAttempts = Math.max(0, Math.ceil(burstRate * rampSeconds / 2) - 1);
const allocatedVus = Number(__ENV.PREALLOCATED_VUS || 128);
const abandonPercent = Number(__ENV.ABANDON_PERCENT ?? 10);
const cancelPercent = Number(__ENV.CANCEL_PERCENT ?? 10);
const maxQuantity = Number(__ENV.MAX_QUANTITY || 3);
const thinkMaxSeconds = Number(__ENV.THINK_MAX_SECONDS ?? 3);
const checkoutTimeoutSeconds = Number(__ENV.CHECKOUT_TIMEOUT_SECONDS || 60);
const paymentOutcome = __ENV.PAYMENT_OUTCOME || 'Succeeded';
const expectSoldOut = __ENV.EXPECT_SOLD_OUT !== 'false';
const plannedAttempts = rampAttempts + burstRate * burstSeconds + tailRate * tailSeconds;
const phaseAttempts = { ramp: rampAttempts, burst: burstRate * burstSeconds, tail: tailRate * tailSeconds };

const attempts = new Counter('release_attempts');
const accepted = new Counter('release_reserved');
const soldOut = new Counter('release_sold_out');
const abandoned = new Counter('release_abandoned');
const cancelled = new Counter('release_cancelled');
const paid = new Counter('release_paid');
const paymentRejected = new Counter('release_payment_rejected');
const unexpected = new Rate('release_unexpected_error');
const reservationDuration = new Trend('release_reservation_duration', true);
const paidDuration = new Trend('release_paid_duration', true);

function arrivals(rate, duration, startTime, exec, vus) {
  return {
    executor: 'constant-arrival-rate', exec, rate, timeUnit: '1s',
    duration: `${duration}s`, startTime: `${startTime}s`, preAllocatedVUs: vus,
    gracefulStop: `${Math.ceil(thinkMaxSeconds + checkoutTimeoutSeconds + 10)}s`,
  };
}

export const options = {
  summaryTrendStats,
  scenarios: {
    browse: arrivals(Number(__ENV.BROWSE_RATE || 5), preSaleSeconds + rampSeconds + burstSeconds + tailSeconds,
      0, 'browse', 16),
    ...(rampSeconds > 0 ? { ramp: {
      executor: 'ramping-arrival-rate', exec: 'attempt', startRate: 0, timeUnit: '1s',
      stages: [{ target: burstRate, duration: `${rampSeconds}s` }],
      startTime: `${preSaleSeconds}s`, preAllocatedVUs: allocatedVus,
      gracefulStop: `${Math.ceil(thinkMaxSeconds + checkoutTimeoutSeconds + 10)}s`,
    } } : {}),
    burst: arrivals(burstRate, burstSeconds, preSaleSeconds + rampSeconds, 'attempt', allocatedVus),
    tail: arrivals(tailRate, tailSeconds, preSaleSeconds + rampSeconds + burstSeconds, 'attempt', allocatedVus),
  },
  thresholds: {
    checks: ['rate==1'],
    ...performanceThresholds('release_paid_duration'),
    dropped_iterations: ['count==0'],
    release_unexpected_error: ['rate==0'],
    release_attempts: [`count==${plannedAttempts}`],
    release_reserved: ['count>0'],
    release_sold_out: [expectSoldOut ? 'count>0' : 'count==0'],
    ...(paymentOutcome === 'Succeeded'
      ? { release_paid: ['count>0'] } : { release_payment_rejected: ['count>0'] }),
  },
};

function requestOptions(name, token, statuses = [200]) {
  return {
    tags: { name },
    headers: { 'Content-Type': 'application/json', ...(token ? { 'X-Guest-Access-Token': token } : {}) },
    responseCallback: http.expectedStatuses(...statuses),
    timeout: '5s',
  };
}

function requireCondition(value, description, predicate) {
  if (!check(value, { [description]: predicate })) throw new Error(description);
}

function requireStatus(response, status) {
  requireCondition(response, `expected HTTP ${status}`, (r) => r.status === status);
}

function readInventory() {
  const response = http.get(`${baseUrl}/GetEventInventory/${inventoryId}`, requestOptions('GetEventInventory'));
  requireStatus(response, 200);
  requireCondition(response.json(), 'inventory remains within capacity',
    (body) => body.availableQuantity >= 0 && body.availableQuantity <= body.totalQuantity);
}

export function setup() {
  // The runner warms a DIFFERENT event before applying this run's payment policy.
  // Sales open at the scenario boundary; this does not test the server's sale-date enforcement.
  readInventory();
  for (const metric of [attempts, accepted, soldOut, abandoned, cancelled, paid, paymentRejected]) metric.add(0);
  console.log(`Release: ${plannedAttempts} attempts; ramp ${rampSeconds}s to ${burstRate}/s, then ${tailRate}/s; payment ${paymentOutcome}.`);
}

export function browse() {
  let failed = false;
  try {
    requireStatus(http.get(`${gateway}/api/ListEvents`, requestOptions('ListEvents')), 200);
    requireStatus(http.get(`${gateway}/api/GetEvent/${eventId}`, requestOptions('GetEvent')), 200);
    readInventory();
  } catch (error) {
    failed = true;
    console.error(`browse: ${error}`);
  } finally {
    unexpected.add(failed);
  }
}

function waitForPayment(bookingId, token, quantity, started) {
  const deadline = Date.now() + checkoutTimeoutSeconds * 1000;
  while (Date.now() < deadline) {
    const response = http.get(`${baseUrl}/GetBooking/${bookingId}`, requestOptions('GetBooking', token));
    requireStatus(response, 200);
    const booking = response.json();
    if (booking.status === 'Confirmed') {
      requireCondition(booking, 'successful payment belongs to a confirmed booking',
        (body) => paymentOutcome === 'Succeeded' && body.paymentStatus === 'Succeeded');
      const ticketsResponse = http.get(`${baseUrl}/GetTicketsForBooking/${bookingId}/tickets`,
        requestOptions('GetTicketsForBooking', token));
      requireStatus(ticketsResponse, 200);
      const tickets = ticketsResponse.json();
      if (tickets.length > 0) {
        requireCondition(tickets, 'one valid unique ticket per purchased item', (items) =>
          items.length === quantity && new Set(items.map((ticket) => ticket.ticketId)).size === quantity
          && items.every((ticket) => ticket.bookingId === bookingId && ticket.status === 'Valid'));
        paid.add(1);
        paidDuration.add(Date.now() - started);
        return;
      }
      sleep(1.5); // Match the customer app's ticket polling.
    } else if (booking.status === 'Cancelled') {
      requireCondition(booking, 'planned payment failure releases the booking',
        (body) => paymentOutcome !== 'Succeeded' && body.paymentStatus === paymentOutcome);
      paymentRejected.add(1);
      return;
    } else {
      requireCondition(booking, 'payment remains in a recoverable state',
        (body) => ['Reserved', 'Confirming', 'Cancelling'].includes(body.status));
      sleep(['Confirming', 'Cancelling'].includes(booking.status) ? 1 : 2);
    }
  }
  throw new Error(`Payment/tickets did not reach the expected outcome within ${checkoutTimeoutSeconds}s.`);
}

export function attempt() {
  const iteration = execution.scenario.iterationInTest;
  // k6 1.3 can schedule one extra boundary iteration; never issue extra business requests.
  if (iteration >= phaseAttempts[execution.scenario.name]) return;
  // Deterministic variation makes reruns comparable. Accepted cohorts may differ under contention.
  const bucket = (iteration * 37) % 100;
  const cohort = bucket < abandonPercent ? 'abandon'
    : bucket < abandonPercent + cancelPercent ? 'cancel' : 'pay';
  const quantity = 1 + (iteration % maxQuantity);
  const started = Date.now();
  let failed = false;
  attempts.add(1);
  try {
    const created = http.post(`${baseUrl}/CreateBooking`, JSON.stringify({
      inventoryId, quantity, customerName: `Release ${cohort} ${execution.scenario.name} ${iteration}`,
      customerEmail: `release-${cohort}-${execution.scenario.name}-${iteration}@example.com`,
    }), requestOptions('CreateBooking', null, [202, 409]));
    reservationDuration.add(created.timings.duration);
    if (created.status === 409) {
      // Other conflicts (e.g. a missing catalog snapshot) are still test failures.
      requireCondition(created.json(), 'reservation rejected because stock is unavailable',
        (body) => body.title === 'Not enough tickets are available.');
      soldOut.add(1);
      return;
    }
    requireStatus(created, 202);
    const { bookingId, guestAccessToken } = created.json();
    requireCondition(created.json(), 'reservation contains guest credentials',
      (body) => Boolean(body.bookingId && body.guestAccessToken));
    accepted.add(1);
    if (cohort === 'abandon') {
      // Leave the real ten-minute hold untouched. The runner observes expiry after traffic ends.
      abandoned.add(1);
      return;
    }
    sleep(thinkMaxSeconds * ((iteration * 17) % 101) / 100);
    if (cohort === 'cancel') {
      const response = http.post(`${baseUrl}/CancelBooking/${bookingId}`, null,
        requestOptions('CancelBooking', guestAccessToken));
      requireStatus(response, 200);
      requireCondition(response.json(), 'customer cancellation accepted', (body) => body.status === 'Cancelled');
      cancelled.add(1);
      return;
    }
    const response = http.post(`${baseUrl}/StartPayment/${bookingId}`, null,
      requestOptions('StartPayment', guestAccessToken, paymentOutcome === 'Succeeded' ? [200, 202] : [202, 409]));
    requireCondition(response, 'payment request accepted or reached its planned terminal outcome',
      (r) => r.status === 202 || (paymentOutcome === 'Succeeded' ? r.status === 200 : r.status === 409));
    waitForPayment(bookingId, guestAccessToken, quantity, started);
  } catch (error) {
    failed = true;
    console.error(`${execution.scenario.name}/${iteration}: ${error}`);
  } finally {
    unexpected.add(failed);
  }
}

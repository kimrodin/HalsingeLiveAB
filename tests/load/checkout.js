import http from 'k6/http';
import { check, sleep } from 'k6';
import execution from 'k6/execution';
import { Counter, Rate, Trend } from 'k6/metrics';

const baseUrl = 'http://api-gateway:8080/booking-api';
const inventoryId = __ENV.INVENTORY_ID || '74000000-0000-0000-0000-000000000001';
const skipSetupWarmup = __ENV.SKIP_SETUP_WARMUP === 'true';
const iterations = Number(__ENV.ITERATIONS || 10);
const vus = Number(__ENV.VUS || 2);
const checkoutSucceeded = new Rate('checkout_succeeded');
const completedCheckouts = new Counter('completed_checkouts');
const checkoutDuration = new Trend('checkout_duration', true);

export const options = {
  scenarios: {
    checkout: { executor: 'shared-iterations', vus, iterations, maxDuration: '10m' },
  },
  thresholds: {
    checks: ['rate==1'],
    http_req_failed: ['rate==0'],
    checkout_succeeded: ['rate==1'],
    completed_checkouts: [`count==${iterations}`],
  },
};

function requestOptions(name, token) {
  return {
    tags: { name }, // Group requests without putting booking IDs into metric labels.
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { 'X-Guest-Access-Token': token } : {}),
    },
    timeout: '5s',
  };
}

function requireStatus(response, status, message) {
  if (!check(response, { [message]: (r) => r.status === status })) {
    throw new Error(`${message}: HTTP ${response.status}; ${String(response.body).slice(0, 300)}`);
  }
}

function checkout(customer) {
  // 1. Reserve one ticket and keep the returned guest access token.
  const created = http.post(`${baseUrl}/CreateBooking`, JSON.stringify({
    inventoryId,
    quantity: 1,
    customerName: `Load test ${customer}`,
    customerEmail: `load-${customer}@example.com`,
  }), requestOptions('CreateBooking'));
  requireStatus(created, 202, 'reservation accepted');
  const { bookingId, guestAccessToken } = created.json();
  if (!bookingId || !guestAccessToken) throw new Error('Reservation response is missing its ID or guest token.');

  // 2. Start payment. HTTP 202 means queued; the purchase is still in progress.
  const payment = http.post(`${baseUrl}/StartPayment/${bookingId}`, null,
    requestOptions('StartPayment', guestAccessToken));
  requireStatus(payment, 202, 'payment accepted');

  // 3. Wait for confirmation AND exactly one valid ticket, at most 30 seconds.
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    const booking = http.get(`${baseUrl}/GetBooking/${bookingId}`,
      requestOptions('GetBooking', guestAccessToken));
    requireStatus(booking, 200, 'booking status available');
    const status = booking.json('status');
    if (status === 'Cancelled' || status === 'Expired') throw new Error(`Booking became ${status}.`);
    if (status === 'Confirmed') {
      const response = http.get(`${baseUrl}/GetTicketsForBooking/${bookingId}/tickets`,
        requestOptions('GetTicketsForBooking', guestAccessToken));
      requireStatus(response, 200, 'tickets available');
      const tickets = response.json();
      if (tickets.length > 0) {
        if (!check(tickets, {
          'exactly one valid ticket for this booking': (items) => items.length === 1
            && items[0].bookingId === bookingId && items[0].status === 'Valid',
        })) throw new Error('Unexpected ticket count, booking ID, or status.');
        return;
      }
    }
    sleep(0.5);
  }
  throw new Error(`Booking ${bookingId} did not confirm and issue a ticket within 30 seconds.`);
}

export function setup() {
  // Verify the gateway and fixture, then warm the complete messaging path once.
  const inventory = http.get(`${baseUrl}/GetEventInventory/${inventoryId}`, requestOptions('GetEventInventory'));
  requireStatus(inventory, 200, 'fixture available');
  if (inventory.json('availableQuantity') < iterations + (skipSetupWarmup ? 0 : 1)) {
    throw new Error('Not enough fixture inventory.');
  }
  if (!skipSetupWarmup) checkout('warmup');
  console.log(`Starting ${iterations} checkouts with ${vus} virtual users.`);
}

export default function () {
  const started = Date.now();
  let succeeded = false;
  try {
    checkout(execution.scenario.iterationInTest);
    succeeded = true;
    completedCheckouts.add(1);
  } catch (error) {
    console.error(String(error));
  } finally {
    // Include failed journeys in the duration and success metrics.
    checkoutDuration.add(Date.now() - started);
    checkoutSucceeded.add(succeeded);
  }
}

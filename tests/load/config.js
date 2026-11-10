// Budgets for local validation, not a production SLA. Override before running.
export function numberSetting(name, fallback, min, max, integer = true) {
  const raw = __ENV[name];
  const value = raw === undefined ? fallback : Number(raw);
  if (raw === '' || !Number.isFinite(value) || value < min || value > max
      || (integer && !Number.isInteger(value))) {
    throw new Error(`${name} must be ${integer ? 'an integer' : 'a number'} between ${min} and ${max}.`);
  }
  return value;
}

export const summaryTrendStats = ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)', 'count'];

export function performanceThresholds(journeyMetric) {
  const httpP95 = numberSetting('HTTP_P95_MS', 500, 1, 300000);
  const httpP99 = numberSetting('HTTP_P99_MS', 1000, 1, 300000);
  const checkoutP95 = numberSetting('CHECKOUT_P95_MS', 15000, 1, 300000);
  const checkoutP99 = numberSetting('CHECKOUT_P99_MS', 20000, 1, 300000);
  const errorRate = numberSetting('MAX_HTTP_ERROR_RATE', 0, 0, 1, false);
  if (httpP99 < httpP95 || checkoutP99 < checkoutP95) {
    throw new Error('A p99 budget must be greater than or equal to its p95 budget.');
  }
  // The runner's separate warm-up verifies correctness before measured traffic.
  // Cold-start latency is exported, but is not judged against the measured budgets.
  if (__ENV.WARMUP_ONLY === 'true') return { http_req_failed: ['rate==0'] };
  return {
    http_req_duration: [`p(95)<=${httpP95}`, `p(99)<=${httpP99}`],
    http_req_failed: [`rate<=${errorRate}`],
    [journeyMetric]: [`p(95)<=${checkoutP95}`, `p(99)<=${checkoutP99}`],
  };
}

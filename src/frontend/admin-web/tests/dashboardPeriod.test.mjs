import assert from 'node:assert/strict';
import test from 'node:test';
import { eventPeriodBounds, last30Days, validatePeriod } from '../src/features/dashboard/utils/dashboardPeriod.ts';

test('default period uses the Swedish date and accepts only valid date ranges', () => {
  const period = last30Days(new Date('2026-10-31T23:30:00Z'));
  assert.deepEqual(period, { from: '2026-10-03', to: '2026-11-01' });
  assert.equal(validatePeriod(period), null);
  assert.ok(validatePeriod({ from: '2026-02-30', to: '2026-03-01' }));
  assert.ok(validatePeriod({ from: '2026-11-02', to: '2026-11-01' }));
  assert.ok(validatePeriod({ from: '2026-01-01', to: '2027-01-02' }));
});

test('event period includes the full Swedish day across both DST transitions', () => {
  assert.deepEqual(eventPeriodBounds({ from: '2026-03-29', to: '2026-03-29' }), {
    startAfter: '2026-03-28T23:00:00.000Z', startsBefore: '2026-03-29T21:59:59.999Z',
  });
  assert.deepEqual(eventPeriodBounds({ from: '2026-10-25', to: '2026-10-25' }), {
    startAfter: '2026-10-24T22:00:00.000Z', startsBefore: '2026-10-25T22:59:59.999Z',
  });
});

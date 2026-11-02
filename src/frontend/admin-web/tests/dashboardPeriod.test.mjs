import assert from 'node:assert/strict';
import test from 'node:test';
import { last30Days, shiftDate, validatePeriod } from '../src/features/dashboard/utils/dashboardPeriod.ts';

test('last 30 days use the Swedish date even when UTC is on the previous day', () => {
  const now = new Date('2026-10-31T23:30:00Z');
  assert.deepEqual(last30Days(now), { from: '2026-10-03', to: '2026-11-01' });
});

test('calendar arithmetic crosses DST and leap-day boundaries without shifting dates', () => {
  assert.equal(shiftDate('2026-03-30', -1), '2026-03-29');
  assert.equal(shiftDate('2026-10-26', -1), '2026-10-25');
  assert.equal(shiftDate('2024-03-01', -1), '2024-02-29');
});

test('validation rejects blank, invalid and reversed dates', () => {
  for (const range of [
    { from: '', to: '2026-11-01' },
    { from: '2026-02-30', to: '2026-03-01' },
    { from: '2026-11-02', to: '2026-11-01' },
  ]) assert.ok(validatePeriod(range));
  assert.equal(validatePeriod({ from: '2026-11-01', to: '2026-11-01' }), null);
});

test('366 calendar days are allowed, 367 are rejected regardless of DST', () => {
  assert.equal(validatePeriod({ from: '2026-01-01', to: '2027-01-01' }), null);
  assert.ok(validatePeriod({ from: '2026-01-01', to: '2027-01-02' }));
});

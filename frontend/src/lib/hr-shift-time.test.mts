import assert from 'node:assert/strict';
import test from 'node:test';
import { formatShiftTimeRange, isOvernightShift } from './hr-shift-time.ts';

test('formats an overnight shift in start-to-end order inside an RTL label', () => {
  const label = formatShiftTimeRange('22:00:00', '02:00:00');

  assert.equal(label, 'من \u206622:00\u2069 إلى \u206602:00\u2069 (اليوم التالي)');
  assert.equal(isOvernightShift('22:00:00', '02:00:00'), true);
});

test('does not mark a daytime shift as crossing into the next day', () => {
  const label = formatShiftTimeRange('09:00:00', '17:00:00');

  assert.equal(label, 'من \u206609:00\u2069 إلى \u206617:00\u2069');
  assert.equal(isOvernightShift('09:00:00', '17:00:00'), false);
});

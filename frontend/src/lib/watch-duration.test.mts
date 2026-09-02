import assert from 'node:assert/strict';
import test from 'node:test';

import { formatWatchDuration } from './watch-duration.ts';

test('watch threshold duration uses hours, minutes, and seconds as needed', () => {
  assert.equal(formatWatchDuration(0), '0 ثانية');
  assert.equal(formatWatchDuration(35), '35 ثانية');
  assert.equal(formatWatchDuration(60), '1 دقيقة');
  assert.equal(formatWatchDuration(65), '1 دقيقة و5 ثانية');
  assert.equal(formatWatchDuration(3665), '1 ساعة و1 دقيقة و5 ثانية');
});

test('watch threshold duration safely normalizes fractions and invalid values', () => {
  assert.equal(formatWatchDuration(1.9), '1 ثانية');
  assert.equal(formatWatchDuration(-10), '0 ثانية');
  assert.equal(formatWatchDuration(Number.NaN), '0 ثانية');
});

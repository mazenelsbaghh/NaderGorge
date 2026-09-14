import assert from 'node:assert/strict';
import test from 'node:test';
import { delayUntilNextCairoMidnight } from './cairoTime.js';

test('calculates Cairo midnight while daylight saving time is active', () => {
  const now = new Date('2026-07-30T20:00:00.000Z'); // 23:00 Cairo
  assert.equal(delayUntilNextCairoMidnight(now), 60 * 60 * 1000);
});

test('calculates Cairo midnight during standard time', () => {
  const now = new Date('2026-12-10T21:30:00.000Z'); // 23:30 Cairo
  assert.equal(delayUntilNextCairoMidnight(now), 30 * 60 * 1000);
});

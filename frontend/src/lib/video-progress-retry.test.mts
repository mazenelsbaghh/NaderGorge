import assert from 'node:assert/strict';
import test from 'node:test';
import { videoProgressRetryDelayMs } from './video-progress-retry.ts';

test('2026-09-12 progress throttling respects header/body deadlines and bounds invalid values', () => {
  const now = Date.parse('2026-09-12T20:00:00Z');
  for (const [response, expected] of [
    [{ status: 429, headers: { 'retry-after': '20' } }, 20_000],
    [{ status: 503, data: { retryAfterSeconds: 5 } }, 5_000],
    [{ status: 429, headers: { 'retry-after': 'Sat, 12 Sep 2026 20:00:30 GMT' } }, 30_000],
    [{ status: 429 }, 60_000],
    [{ status: 429, headers: { 'retry-after': '999999999' } }, 300_000],
    [{ status: 403 }, 0],
    [{ status: 500 }, 5_000],
  ] as const) assert.equal(videoProgressRetryDelayMs({ response }, now), expected);
  assert.equal(videoProgressRetryDelayMs(new TypeError('Failed to fetch'), now), 5_000);
});

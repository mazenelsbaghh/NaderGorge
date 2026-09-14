import assert from 'node:assert/strict';
import test from 'node:test';

import {
  DOUBLE_TAP_WINDOW_MS,
  isDoubleTapSeek,
  resolveSeekTarget,
} from './video-seek.ts';

test('same-side taps within the gesture window trigger a seek', () => {
  const firstTap = { direction: 'forward' as const, timestamp: 1_000 };

  assert.equal(
    isDoubleTapSeek(firstTap, {
      direction: 'forward',
      timestamp: 1_000 + DOUBLE_TAP_WINDOW_MS,
    }),
    true,
  );
  assert.equal(
    isDoubleTapSeek(firstTap, { direction: 'backward', timestamp: 1_100 }),
    false,
  );
  assert.equal(
    isDoubleTapSeek(firstTap, {
      direction: 'forward',
      timestamp: 1_001 + DOUBLE_TAP_WINDOW_MS,
    }),
    false,
  );
});

test('ten-second seeks clamp to the start and known duration', () => {
  assert.equal(resolveSeekTarget(5, 120, 'backward'), 0);
  assert.equal(resolveSeekTarget(30, 120, 'forward'), 40);
  assert.equal(resolveSeekTarget(115, 120, 'forward'), 120);
  assert.equal(resolveSeekTarget(Number.NaN, 120, 'forward'), 10);
});

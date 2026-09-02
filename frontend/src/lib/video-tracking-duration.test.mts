import assert from 'node:assert/strict';
import test from 'node:test';

import { resolveTrackableDurationSeconds } from './video-tracking-duration.ts';

test('2026-09-02 Bunny duration race never produces an invalid tracking duration', () => {
  for (const unavailableDuration of [0, -1, Number.NaN, Number.POSITIVE_INFINITY]) {
    assert.equal(resolveTrackableDurationSeconds(unavailableDuration), null);
  }

  assert.equal(resolveTrackableDurationSeconds(2466.266), 2466);
});

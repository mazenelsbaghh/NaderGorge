import assert from 'node:assert/strict';
import test from 'node:test';

import {
  BUNNY_PLAYBACK_STABILITY_WINDOW_MS,
  MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS,
  canRetryBunnyPlayback,
  isBunnyPlaybackStable,
} from './video-playback-recovery.ts';

test('2026-09-02 transient Bunny playback failure retries twice and then stops', () => {
  assert.equal(canRetryBunnyPlayback('bunny', 0), true);
  assert.equal(canRetryBunnyPlayback('Bunny', 1), true);
  assert.equal(
    canRetryBunnyPlayback('bunny', MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS),
    false,
  );
  assert.equal(canRetryBunnyPlayback('youtube', 0), false);
});

test('2026-09-02 Bunny recovery budget resets only after stable playback', () => {
  const readyAtMs = 1_000;
  assert.equal(
    isBunnyPlaybackStable(readyAtMs, readyAtMs + BUNNY_PLAYBACK_STABILITY_WINDOW_MS - 1),
    false,
  );
  assert.equal(
    isBunnyPlaybackStable(readyAtMs, readyAtMs + BUNNY_PLAYBACK_STABILITY_WINDOW_MS),
    true,
  );
  assert.equal(isBunnyPlaybackStable(0, Number.MAX_SAFE_INTEGER), false);
});

import assert from 'node:assert/strict';
import test from 'node:test';

import {
  MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS,
  canRetryBunnyPlayback,
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

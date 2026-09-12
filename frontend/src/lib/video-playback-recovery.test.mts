import assert from 'node:assert/strict';
import test from 'node:test';

import {
  BUNNY_PLAYBACK_STABILITY_WINDOW_MS,
  MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS,
  canRetryBunnyPlayback,
  isBunnyPlaybackError,
  isBunnyPlaybackStable,
  isCurrentVideoSession,
  isExpiredHlsSourceError,
  shouldRenewHlsSource,
} from './video-playback-recovery.ts';

test('2026-09-12 a renewed session refreshes its expiring HLS source before fragment rejection', () => {
  for (const [signedExpiry, sessionExpiry, now, expected] of [
    [100_000, 300_000, 70_000, true],
    [100_000, 300_000, 100_001, true],
    [100_000, 300_000, 69_999, false],
    [100_000, 100_000, 70_000, false],
    [0, 300_000, 70_000, false],
    [100_000, 300_000, 300_001, false],
  ] as const) assert.equal(shouldRenewHlsSource(signedExpiry, sessionExpiry, now), expected);
});

test('2026-09-02 transient Bunny playback failure retries twice and then stops', () => {
  assert.equal(canRetryBunnyPlayback('bunny', 0), true);
  assert.equal(canRetryBunnyPlayback('Bunny', 1), true);
  assert.equal(
    canRetryBunnyPlayback('bunny', MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS),
    false,
  );
  assert.equal(canRetryBunnyPlayback('youtube', 0), false);
});

test('only an explicit Bunny provider error consumes the Bunny recovery budget', () => {
  assert.equal(isBunnyPlaybackError('bunny'), true);
  assert.equal(isBunnyPlaybackError('Bunny'), true);
  assert.equal(isBunnyPlaybackError(undefined), false);
  assert.equal(isBunnyPlaybackError('youtube'), false);
});

test('2026-09-10 only an expired signed HLS rejection permits bounded same-session recovery', () => {
  for (const [status, expiry, now, expected] of [
    [403, 1000, 1000, true], [401, 1000, 1001, true],
    [403, 1001, 1000, false], [403, 0, 1000, false],
    [404, 1000, 1001, false], [0, 1000, 1001, false],
  ]) {
    assert.equal(isExpiredHlsSourceError(Number(status), Number(expiry), Number(now)), expected);
  }
  assert.equal(canRetryBunnyPlayback('bunny-hls', 0), true);
  assert.equal(canRetryBunnyPlayback('bunny-hls', MAX_BUNNY_PLAYBACK_RECOVERY_ATTEMPTS), false);
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

test('2026-09-02 a late progress response cannot replace the active playback session', () => {
  assert.equal(isCurrentVideoSession('current-session', 'current-session'), true);
  assert.equal(isCurrentVideoSession('old-session', 'current-session'), false);
  assert.equal(isCurrentVideoSession('old-session', null), false);
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { trackedPlaybackTickSeconds } from './video-tracking-clock.ts';

// 2026-09-15: completed lessons lose time when mobile browsers delay timer callbacks.
for (const [name, wall, media, rate, expected] of [
  ['normal tick', 0.25, 0, 1, 0.25],
  ['delayed playback', 10, 10, 1, 10],
  ['delayed double speed', 10, 20, 2, 10],
  ['delayed half speed', 10, 5, 0.5, 10],
  ['forward seek', 10, 100, 1, 1.5],
  ['backward seek', 10, -10, 1, 1.5],
  ['stalled clock', 10, 0, 1, 1.5],
] as const) {
  test(name, () => assert.equal(trackedPlaybackTickSeconds(wall, media, rate), expected));
}

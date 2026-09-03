import assert from 'node:assert/strict';
import test from 'node:test';

import {
  resolveProgressReportDurationSeconds,
  resolveStableVideoDuration,
  resolveTrackableDurationSeconds,
  resolveWatchThresholdSeconds,
} from './video-tracking-duration.ts';

test('2026-09-02 Bunny duration race never produces an invalid tracking duration', () => {
  for (const unavailableDuration of [0, -1, Number.NaN, Number.POSITIVE_INFINITY]) {
    assert.equal(resolveTrackableDurationSeconds(unavailableDuration), null);
  }

  assert.equal(resolveTrackableDurationSeconds(2466.266), 2466);
});

test('server-authoritative sessions can report progress before player metadata arrives', () => {
  assert.equal(resolveProgressReportDurationSeconds(0, true), 0);
  assert.equal(resolveProgressReportDurationSeconds(Number.NaN, true), 0);
  assert.equal(resolveProgressReportDurationSeconds(0, false), null);
  assert.equal(resolveProgressReportDurationSeconds(125.6, true), 126);
});

test('the session duration remains stable when player metadata changes later', () => {
  assert.equal(resolveStableVideoDuration(2466, 2465.2), 2466);
  assert.equal(resolveStableVideoDuration(2466, 2470), 2466);
  assert.equal(resolveStableVideoDuration(null, 2466.4), 2466);
  assert.equal(resolveStableVideoDuration(null, 0), null);
});

test('watch threshold rounding matches the backend and remains bounded', () => {
  assert.equal(resolveWatchThresholdSeconds(58, 30), 17);
  assert.equal(resolveWatchThresholdSeconds(35, 30), 10);
  assert.equal(resolveWatchThresholdSeconds(45, 30), 14);
  assert.equal(resolveWatchThresholdSeconds(100, 0), 1);
  assert.equal(resolveWatchThresholdSeconds(100, 150), 100);
});

import assert from 'node:assert/strict';
import test from 'node:test';
import {
  createYouTubeStartupRecovery,
  YOUTUBE_BOOTSTRAP_DEADLINE_MS,
} from './youtube-startup-recovery.ts';

test('a silent YouTube embed gets one fresh attempt before a terminal startup failure', (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });
  const failures: string[] = [];
  const recovery = createYouTubeStartupRecovery({
    schedule: setTimeout,
    cancelScheduled: clearTimeout,
    onTimeout: () => failures.push(recovery.failAttempt()),
  });

  assert.equal(recovery.startAttempt(), true);
  context.mock.timers.tick(YOUTUBE_BOOTSTRAP_DEADLINE_MS - 1);
  assert.deepEqual(failures, []);
  context.mock.timers.tick(1);
  assert.deepEqual(failures, ['retry']);
  assert.equal(recovery.isRecoveryAttempt(), true);
  assert.equal(recovery.failAttempt(), 'ignored');

  assert.equal(recovery.startAttempt(), true);
  context.mock.timers.tick(YOUTUBE_BOOTSTRAP_DEADLINE_MS);
  assert.deepEqual(failures, ['retry', 'exhausted']);
  assert.equal(recovery.startAttempt(), false);
  context.mock.timers.tick(YOUTUBE_BOOTSTRAP_DEADLINE_MS * 10);
  assert.deepEqual(failures, ['retry', 'exhausted']);
});

test('a failed SDK can recover without a deadline interrupting successful playback', (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });
  const deadlines: string[] = [];
  const recovery = createYouTubeStartupRecovery({
    schedule: setTimeout,
    cancelScheduled: clearTimeout,
    onTimeout: () => deadlines.push('startup-timeout'),
  });

  recovery.startAttempt();
  assert.equal(recovery.failAttempt(), 'retry');
  recovery.startAttempt();
  recovery.markReady();
  context.mock.timers.tick(YOUTUBE_BOOTSTRAP_DEADLINE_MS * 2);
  assert.deepEqual(deadlines, []);
  assert.equal(recovery.failAttempt(), 'ignored');
  assert.equal(recovery.startAttempt(), false);
});

for (const restoredOutcome of ['ready', 'timeout'] as const) {
  test(`BFCache restoration rearms the second startup deadline without resetting its retry budget: ${restoredOutcome}`, (context) => {
    context.mock.timers.enable({ apis: ['setTimeout'] });
    const failures: string[] = [];
    const recovery = createYouTubeStartupRecovery({
      schedule: setTimeout,
      cancelScheduled: clearTimeout,
      onTimeout: () => failures.push(recovery.failAttempt()),
    });
    recovery.startAttempt();
    assert.equal(recovery.failAttempt(), 'retry');
    recovery.startAttempt();
    context.mock.timers.tick(YOUTUBE_BOOTSTRAP_DEADLINE_MS - 1);
    recovery.suspend();
    context.mock.timers.tick(YOUTUBE_BOOTSTRAP_DEADLINE_MS * 20);
    assert.deepEqual(failures, []);
    recovery.resume();
    assert.equal(recovery.isRecoveryAttempt(), true);
    context.mock.timers.tick(YOUTUBE_BOOTSTRAP_DEADLINE_MS - 1);
    assert.deepEqual(failures, []);
    if (restoredOutcome === 'ready') recovery.markReady();
    context.mock.timers.tick(1);
    assert.deepEqual(failures, restoredOutcome === 'ready' ? [] : ['exhausted']);
  });
}

test('cancelled or replaced startup deadlines cannot revive playback', () => {
  const scheduled: Array<() => void> = [];
  const deadlines: string[] = [];
  const recovery = createYouTubeStartupRecovery({
    schedule: callback => scheduled.push(callback),
    cancelScheduled: () => undefined,
    onTimeout: () => deadlines.push('startup-timeout'),
  });

  recovery.startAttempt();
  recovery.failAttempt();
  recovery.startAttempt();
  // Browser timer callbacks can already be queued when cancellation occurs.
  scheduled[0]();
  assert.deepEqual(deadlines, []);
  recovery.suspend();
  recovery.resume();
  scheduled[1]();
  assert.deepEqual(deadlines, []);
  recovery.cancel();
  recovery.markReady();
  recovery.resume();
  scheduled[2]();
  assert.deepEqual(deadlines, []);
  assert.equal(recovery.failAttempt(), 'ignored');
  assert.equal(recovery.startAttempt(), false);
});

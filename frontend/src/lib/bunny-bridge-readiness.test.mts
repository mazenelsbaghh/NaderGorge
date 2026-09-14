import assert from 'node:assert/strict';
import test from 'node:test';

import { createBunnyBridgeReadinessWatchdog } from './bunny-bridge-readiness.ts';

type ScheduledTask = {
  at: number;
  callback: () => void;
  cancelled: boolean;
};

class FakeClock {
  private now = 0;
  private readonly tasks: ScheduledTask[] = [];

  schedule = (callback: () => void, delayMs: number): ScheduledTask => {
    const task = { at: this.now + delayMs, callback, cancelled: false };
    this.tasks.push(task);
    return task;
  };

  cancel = (task: ScheduledTask): void => {
    task.cancelled = true;
  };

  advance(milliseconds: number): void {
    const target = this.now + milliseconds;
    while (true) {
      const next = this.tasks
        .filter((task) => !task.cancelled && task.at <= target)
        .sort((left, right) => left.at - right.at)[0];
      if (!next) break;

      next.cancelled = true;
      this.now = next.at;
      next.callback();
    }
    this.now = target;
  }
}

test('loaded Bunny surface retries its bridge in place before recovering the embed', () => {
  const clock = new FakeClock();
  const events: string[] = [];
  const watchdog = createBunnyBridgeReadinessWatchdog({
    schedule: clock.schedule,
    cancelScheduled: clock.cancel,
    retryBridgeInPlace: ({ source }) => {
      events.push(source === 'alternate' ? 'fail-over-hostname' : 'retry-same-iframe');
      return true;
    },
    recoverEmbed: () => events.push('replace-embed'),
    initialDeadlineMs: 30_000,
    surfaceDeadlineMs: 30_000,
    retryDeadlineMs: 15_000,
  });

  watchdog.start();
  watchdog.markSurfaceLoaded();
  clock.advance(29_999);
  assert.deepEqual(events, []);

  clock.advance(1);
  assert.deepEqual(events, ['retry-same-iframe']);

  clock.advance(14_999);
  assert.deepEqual(events, ['retry-same-iframe']);
  watchdog.markReady();
  clock.advance(1);
  assert.deepEqual(events, ['retry-same-iframe']);
});

test('2026-09-04 a loaded Bunny surface is re-probed before hostname failover', () => {
  const clock = new FakeClock();
  const events: string[] = [];
  const watchdog = createBunnyBridgeReadinessWatchdog({
    schedule: clock.schedule,
    cancelScheduled: clock.cancel,
    retryBridgeInPlace: ({ source }) => {
      events.push(source === 'alternate' ? 'fail-over-hostname' : 'retry-same-iframe');
      return true;
    },
    recoverEmbed: () => events.push('replace-embed'),
    initialDeadlineMs: 30_000,
    surfaceDeadlineMs: 8_000,
    retryDeadlineMs: 15_000,
  });

  watchdog.start();
  clock.advance(5_000);
  watchdog.markSurfaceLoaded();
  clock.advance(7_999);
  assert.deepEqual(events, []);

  clock.advance(1);
  assert.deepEqual(events, ['retry-same-iframe']);
  clock.advance(14_999);
  assert.deepEqual(events, ['retry-same-iframe']);
  clock.advance(1);
  assert.deepEqual(events, ['retry-same-iframe', 'fail-over-hostname']);
});

test('bridge retry has a bounded deadline and readiness cannot consume a cancelled timer', () => {
  const clock = new FakeClock();
  const events: string[] = [];
  const watchdog = createBunnyBridgeReadinessWatchdog({
    schedule: clock.schedule,
    cancelScheduled: clock.cancel,
    retryBridgeInPlace: ({ source }) => {
      events.push(source === 'alternate' ? 'fail-over-hostname' : 'retry-same-iframe');
      return true;
    },
    recoverEmbed: () => events.push('replace-embed'),
    initialDeadlineMs: 100,
    surfaceDeadlineMs: 100,
    retryDeadlineMs: 50,
  });

  watchdog.start();
  watchdog.markSurfaceLoaded();
  clock.advance(100);
  clock.advance(50);
  assert.deepEqual(events, ['retry-same-iframe', 'fail-over-hostname']);
  clock.advance(50);
  assert.deepEqual(events, ['retry-same-iframe', 'fail-over-hostname', 'replace-embed']);

  watchdog.markReady();
  clock.advance(1_000);
  assert.deepEqual(events, ['retry-same-iframe', 'fail-over-hostname', 'replace-embed']);
});

test('2026-09-03 an unloaded Bunny surface tries the alternate hostname before recovery', () => {
  const clock = new FakeClock();
  const events: string[] = [];
  const watchdog = createBunnyBridgeReadinessWatchdog({
    schedule: clock.schedule,
    cancelScheduled: clock.cancel,
    retryBridgeInPlace: ({ source }) => {
      events.push(source === 'alternate' ? 'fail-over-hostname' : 'retry-same-iframe');
      return true;
    },
    recoverEmbed: () => events.push('replace-embed'),
    initialDeadlineMs: 100,
    retryDeadlineMs: 50,
  });

  watchdog.start();
  clock.advance(100);
  assert.deepEqual(events, ['fail-over-hostname']);
  clock.advance(49);
  assert.deepEqual(events, ['fail-over-hostname']);
  clock.advance(1);
  assert.deepEqual(events, ['fail-over-hostname', 'replace-embed']);
});

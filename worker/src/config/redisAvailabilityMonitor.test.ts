import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { test } from 'node:test';
import type { Redis } from 'ioredis';
import { monitorRedisSentinelAvailability } from './redisAvailabilityMonitor.js';

test('2026-08-09 sub-second Sentinel outage recovers without emitting an alert', async (testContext) => {
  const redis = new EventEmitter() as Redis;
  const errors: unknown[][] = [];
  const originalError = console.error;
  console.error = (...args: unknown[]) => errors.push(args);
  testContext.after(() => { console.error = originalError; });
  monitorRedisSentinelAvailability(redis, 20);

  redis.emit('error', new Error('All sentinels are unreachable. Retrying from scratch after 250ms.'));
  redis.emit('ready');
  await new Promise((resolve) => setTimeout(resolve, 30));

  assert.equal(errors.length, 0);
});

test('sustained Sentinel outage emits one alert and a recovery event', async (testContext) => {
  const redis = new EventEmitter() as Redis;
  const errors: unknown[][] = [];
  const warnings: unknown[][] = [];
  const originalError = console.error;
  const originalWarn = console.warn;
  console.error = (...args: unknown[]) => errors.push(args);
  console.warn = (...args: unknown[]) => warnings.push(args);
  testContext.after(() => {
    console.error = originalError;
    console.warn = originalWarn;
  });
  monitorRedisSentinelAvailability(redis, 10);

  redis.emit('error', new Error('All sentinels are unreachable. Retrying from scratch after 250ms.'));
  redis.emit('error', new Error('All sentinels are unreachable. Retrying from scratch after 250ms.'));
  await new Promise((resolve) => setTimeout(resolve, 20));
  redis.emit('ready');

  assert.equal(errors.length, 1);
  assert.equal(warnings.length, 1);
});

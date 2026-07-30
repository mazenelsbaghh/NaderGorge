import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Redis } from 'ioredis';
import { claimStaleStreamMessages } from './streamRecovery.js';

test('claimStaleStreamMessages uses XAUTOCLAIM and processes claimed messages', async () => {
  const oldGet = Redis.prototype.get;
  try {
    Redis.prototype.get = async () => null;
    const calls: any[] = [];
    const redis = {
      xautoclaim: async (...args: any[]) => {
        calls.push(args);
        return ['0-0', [['9-0', ['jobType', 'video analysis', 'jobId', 'job-9', 'payload', '{}']]], []];
      },
      xack: async () => undefined,
      xdel: async () => undefined,
    };
    const queue = {
      getJob: async () => undefined,
      add: async () => undefined,
    };
    const count = await claimStaleStreamMessages(redis as any, {
      aiQueue: queue,
      mindmapsQueue: queue,
      notifQueue: queue,
      essayQueue: queue,
      liveSupportQueue: queue,
    } as any, 'consumer-a');

    assert.equal(count, 1);
    assert.equal(calls[0][0], 'job-stream');
    assert.equal(calls[0][1], 'worker-group');
    assert.equal(calls[0][2], 'consumer-a');
  } finally {
    Redis.prototype.get = oldGet;
  }
});

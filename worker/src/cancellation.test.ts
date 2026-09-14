import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { Job } from 'bullmq';
import { Redis } from 'ioredis';
import { isJobCancellationMarked, markJobCancellation, throwIfCancellationRequested } from './cancellation.js';

test('cancelling a waiting job persists the marker before removing it', async (testContext) => {
  const originalGet = Redis.prototype.get;
  const originalSet = Redis.prototype.set;
  const cancellationKeys = new Set<string>();
  Redis.prototype.get = async (key: string) => cancellationKeys.has(key) ? '1' : null;
  Redis.prototype.set = async (key: string) => {
    cancellationKeys.add(key);
    return 'OK';
  };
  testContext.after(() => {
    Redis.prototype.get = originalGet;
    Redis.prototype.set = originalSet;
  });

  let removed = false;
  const job = {
    id: 'waiting-job-1',
    data: {},
    getState: async () => 'waiting',
    remove: async () => { removed = true; },
  } as unknown as Job;

  const cancellation = await markJobCancellation(job);

  assert.deepEqual(cancellation, { removed: true, state: 'waiting' });
  assert.equal(removed, true);
  assert.equal(await isJobCancellationMarked('waiting-job-1'), true);
});

test('active cancellation is unrecoverable so BullMQ does not retry it', async (testContext) => {
  const originalGet = Redis.prototype.get;
  Redis.prototype.get = async () => '1';
  testContext.after(() => { Redis.prototype.get = originalGet; });

  await assert.rejects(
    throwIfCancellationRequested({ id: 'active-job-1' } as Job),
    (error: unknown) => error instanceof Error
      && error.name === 'UnrecoverableError'
      && error.message === 'Job cancellation requested',
  );
});

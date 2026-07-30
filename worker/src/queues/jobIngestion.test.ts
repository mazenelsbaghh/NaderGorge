import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Redis } from 'ioredis';
import { ingestStreamJob, resolveQueueTarget } from './jobIngestion.js';

function queue(existingJob?: any) {
  const instance = {
    added: [] as any[],
    getJob: async () => existingJob,
    add: async (...args: any[]) => { instance.added.push(args); },
  };
  return instance;
}
let queueRef: ReturnType<typeof queue>;

function queues(existingJob?: any) {
  queueRef = queue(existingJob);
  return {
    aiQueue: queueRef,
    mindmapsQueue: queue(),
    notifQueue: queue(),
    essayQueue: queue(),
    liveSupportQueue: queue(),
  } as any;
}

function redis() {
  return {
    acked: [] as string[],
    deleted: [] as string[],
    xack: async (_stream: string, _group: string, id: string) => { redisRef.acked.push(id); },
    xdel: async (_stream: string, id: string) => { redisRef.deleted.push(id); },
  };
}
let redisRef: ReturnType<typeof redis>;

test('resolveQueueTarget sanitizes BullMQ job ids', () => {
  const result = resolveQueueTarget('video analysis', 'a:b/c job', {}, queues());
  assert.equal(result?.targetJobId, 'a-b-c-job');
});

test('ingestStreamJob acknowledges invalid JSON without enqueue', async () => {
  redisRef = redis();
  const result = await ingestStreamJob(redisRef as any, queues(), '1-0', ['jobType', 'video analysis', 'jobId', 'job-1', 'payload', '{bad']);
  assert.equal(result.action, 'acked-invalid');
  assert.deepEqual(redisRef.acked, ['1-0']);
  assert.equal(queueRef.added.length, 0);
});

test('ingestStreamJob skips existing completed job without removing or enqueuing', async () => {
  redisRef = redis();
  let removed = false;
  const existing = { getState: async () => 'completed', remove: async () => { removed = true; } };
  const result = await ingestStreamJob(redisRef as any, queues(existing), '2-0', ['jobType', 'video analysis', 'jobId', 'job-2', 'payload', '{}']);
  assert.equal(result.action, 'skipped-existing');
  assert.equal(removed, false);
  assert.equal(queueRef.added.length, 0);
});

test('ingestStreamJob preserves cancellation marker on ordinary duplicate ingestion', async () => {
  const oldGet = Redis.prototype.get;
  try {
    Redis.prototype.get = async () => '1';
    redisRef = redis();
    const result = await ingestStreamJob(redisRef as any, queues(), '3-0', ['jobType', 'video analysis', 'jobId', 'job-3', 'payload', '{}']);
    assert.equal(result.action, 'skipped-existing');
    assert.equal(queueRef.added.length, 0);
  } finally {
    Redis.prototype.get = oldGet;
  }
});

test('ingestStreamJob retries essay grading jobs after a fixed 20 seconds', async () => {
  const originalGet = Redis.prototype.get;
  try {
    Redis.prototype.get = async () => null;
    redisRef = redis();
    const essayQueue = queue();
    const queueSet = {
      aiQueue: queue(),
      mindmapsQueue: queue(),
      notifQueue: queue(),
      essayQueue,
      liveSupportQueue: queue(),
    } as any;

    const result = await ingestStreamJob(redisRef as any, queueSet, '4-0', [
      'jobType',
      'essay',
      'jobId',
      'essay-submission-1',
      'payload',
      JSON.stringify({ essaySubmissionId: 'essay-submission-1', answerText: 'answer' }),
    ]);

    assert.equal(result.action, 'enqueued');
    assert.equal(essayQueue.added.length, 1);
    assert.equal(essayQueue.added[0][2].attempts, 5);
    assert.deepEqual(essayQueue.added[0][2].backoff, { type: 'fixed', delay: 20_000 });
  } finally {
    Redis.prototype.get = originalGet;
  }
});

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
    adminAIQueue: queue(),
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

test('ingestStreamJob replaces a failed job when analysis is requested again', async () => {
  const originalGet = Redis.prototype.get;
  try {
    Redis.prototype.get = async () => null;
    redisRef = redis();
    let removed = false;
    const existing = { getState: async () => 'failed', remove: async () => { removed = true; } };
    const result = await ingestStreamJob(redisRef as any, queues(existing), '2-1', ['jobType', 'video analysis', 'jobId', 'job-2', 'payload', '{}']);
    assert.equal(result.action, 'enqueued');
    assert.equal(removed, true);
    assert.equal(queueRef.added.length, 1);
  } finally {
    Redis.prototype.get = originalGet;
  }
});

test('video analysis ingestion caps the queue retry policy at three attempts', async () => {
  const originalGet = Redis.prototype.get;
  try {
    Redis.prototype.get = async () => null;
    redisRef = redis();
    const result = await ingestStreamJob(redisRef as any, queues(), '2-2', [
      'jobType', 'video analysis', 'jobId', 'job-video-attempts', 'payload', '{}',
    ]);

    assert.equal(result.action, 'enqueued');
    assert.equal(queueRef.added[0][2].attempts, 3);
  } finally {
    Redis.prototype.get = originalGet;
  }
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
      adminAIQueue: queue(),
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

test('ingestStreamJob routes Admin AI turns to their isolated BullMQ queue', async () => {
  const originalGet = Redis.prototype.get;
  try {
    Redis.prototype.get = async () => null;
    redisRef = redis();
    const adminAIQueue = queue();
    const queueSet = {
      aiQueue: queue(),
      mindmapsQueue: queue(),
      notifQueue: queue(),
      essayQueue: queue(),
      liveSupportQueue: queue(),
      adminAIQueue,
    } as any;

    const result = await ingestStreamJob(redisRef as any, queueSet, '5-0', [
      'jobType',
      'admin ai turn',
      'jobId',
      'admin-ai-turn-70000000-0000-4000-8000-000000000001',
      'payload',
      JSON.stringify({
        schemaVersion: '1',
        turnId: '70000000-0000-4000-8000-000000000001',
        conversationId: '60000000-0000-4000-8000-000000000001',
      }),
    ]);

    assert.equal(result.action, 'enqueued');
    assert.equal(adminAIQueue.added.length, 1);
    assert.equal(adminAIQueue.added[0][0], 'respond');
    assert.equal(adminAIQueue.added[0][1].schemaVersion, '1');
    assert.equal(adminAIQueue.added[0][2].jobId, 'admin-ai-turn-70000000-0000-4000-8000-000000000001');
    assert.deepEqual(adminAIQueue.added[0][2].backoff, { type: 'exponential', delay: 2000 });
    assert.deepEqual(redisRef.acked, ['5-0']);
  } finally {
    Redis.prototype.get = originalGet;
  }
});

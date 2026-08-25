import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Redis } from 'ioredis';
import { ingestStreamJob, resolveQueueTarget } from './jobIngestion.js';

function queue(existingJob?: any, name = 'ai-video-chapters') {
  const instance = {
    name,
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
    mindmapsQueue: queue(undefined, 'generate-chapter-mindmaps'),
    notifQueue: queue(undefined, 'notifications'),
    essayQueue: queue(undefined, 'ai-essay-grading'),
    liveSupportQueue: queue(undefined, 'ai-live-support-turns'),
    adminAIQueue: queue(undefined, 'ai-admin-agent-turns'),
  } as any;
}

function redis() {
  return {
    acked: [] as string[],
    deleted: [] as string[],
    events: [] as string[],
    aliases: [] as Array<{ key: string; value: string; expiryMode?: string; ttl?: number }>,
    xack: async (_stream: string, _group: string, id: string) => {
      redisRef.acked.push(id);
      redisRef.events.push(`ack:${id}`);
    },
    xdel: async (_stream: string, id: string) => { redisRef.deleted.push(id); },
    eval: async (_script: string, _keyCount: number, key: string, value: string, _streamId: string, ttl: string) => {
      redisRef.aliases.push({ key, value, expiryMode: 'EX', ttl: Number(ttl) });
      redisRef.events.push('alias');
      return 1;
    },
  };
}
let redisRef: ReturnType<typeof redis>;

test('resolveQueueTarget sanitizes BullMQ job ids', () => {
  const result = resolveQueueTarget('video analysis', 'a:b/c job', {}, queues());
  assert.equal(result?.targetJobId, 'a-b-c-job');
});

test('generation jobs use a run-scoped physical id and retain a stable logical id', () => {
  const generationRunId = '11111111-1111-4111-8111-111111111111';
  const result = resolveQueueTarget('mind maps', 'stream-job', {
    lessonVideoId: 'video-1',
    chapterId: 'chapter-1',
    generationRunId,
  }, queues());

  assert.equal(result?.logicalJobId, 'video-1_mindmaps');
  assert.equal(result?.targetJobId, `video-1_mindmap_chapter-1--run-${generationRunId}`);
});

test('ingestStreamJob acknowledges invalid JSON without enqueue', async () => {
  redisRef = redis();
  const result = await ingestStreamJob(redisRef as any, queues(), '1-0', ['jobType', 'video analysis', 'jobId', 'job-1', 'payload', '{bad']);
  assert.equal(result.action, 'acked-invalid');
  assert.deepEqual(redisRef.acked, ['1-0']);
  assert.equal(queueRef.added.length, 0);
});

test('ingestStreamJob replaces a retained completed job when generation is requested again', async () => {
  const originalGet = Redis.prototype.get;
  try {
    Redis.prototype.get = async () => null;
    redisRef = redis();
    let removed = false;
    const existing = { getState: async () => 'completed', remove: async () => { removed = true; } };
    const result = await ingestStreamJob(redisRef as any, queues(existing), '2-0', ['jobType', 'video analysis', 'jobId', 'job-2', 'payload', '{}']);
    assert.equal(result.action, 'enqueued');
    assert.equal(removed, true);
    assert.equal(queueRef.added.length, 1);
  } finally {
    Redis.prototype.get = originalGet;
  }
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

test('ingestStreamJob deduplicates non-terminal jobs without removing them', async () => {
  const scenarios = [
    { state: 'active', messageStreamId: '20-0' },
    { state: 'waiting', messageStreamId: '20-1' },
    { state: 'delayed', messageStreamId: '20-2' },
  ];
  for (const { state, messageStreamId } of scenarios) {
    redisRef = redis();
    let removed = false;
    const existing = { getState: async () => state, remove: async () => { removed = true; } };
    const result = await ingestStreamJob(redisRef as any, queues(existing), messageStreamId, [
      'jobType', 'video analysis', 'jobId', `job-${state}`, 'payload', '{}',
    ]);
    assert.equal(result.action, 'skipped-existing');
    assert.equal(removed, false);
    assert.equal(queueRef.added.length, 0);
    assert.equal(redisRef.aliases.length, 1);
    assert.ok(redisRef.events.indexOf('alias') < redisRef.events.indexOf(`ack:${messageStreamId}`));
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

test('generation ingestion adds the stable logical callback id to the queued payload', async () => {
  const originalGet = Redis.prototype.get;
  try {
    Redis.prototype.get = async () => null;
    redisRef = redis();
    const generationRunId = '11111111-1111-4111-8111-111111111111';
    await ingestStreamJob(redisRef as any, queues(), '2-3', [
      'jobType', 'video analysis', 'jobId', 'video-logical', 'payload', JSON.stringify({ generationRunId }),
    ]);

    assert.equal(queueRef.added[0][1].logicalJobId, 'video-logical');
    assert.equal(queueRef.added[0][2].jobId, `video-logical--run-${generationRunId}`);
    assert.equal(redisRef.aliases.length, 1);
    const aliasWrite = redisRef.aliases[0]!;
    assert.match(aliasWrite.key, /^job-alias:v1:[0-9a-f]{64}$/);
    assert.deepEqual(JSON.parse(aliasWrite.value), {
      logicalJobId: 'video-logical',
      physicalJobId: `video-logical--run-${generationRunId}`,
      queueName: 'ai-video-chapters',
      sourceStreamId: '2-3',
    });
    assert.equal(aliasWrite.expiryMode, 'EX');
    assert.ok((aliasWrite.ttl ?? 0) > 0);
    assert.ok(redisRef.events.indexOf('alias') < redisRef.events.indexOf('ack:2-3'));
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

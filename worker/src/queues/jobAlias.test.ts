import { test } from 'node:test';
import assert from 'node:assert/strict';

import { resolveQueuedJobAlias, storeQueuedJobAlias } from './jobAlias.js';
import { resolveGenerationJob } from './logicalJobResolver.js';

function streamIdParts(streamId: string) {
  const [milliseconds = '0', sequence = '0'] = streamId.split('-');
  return [BigInt(milliseconds), BigInt(sequence)] as const;
}

function fakeRedis() {
  const values = new Map<string, string>();
  const writes: Array<{ key: string; value: string; expiryMode: string; ttl: number }> = [];
  return {
    values,
    writes,
    get: async (key: string) => values.get(key) ?? null,
    eval: async (_script: string, _keyCount: number, key: string, value: string, streamId: string, ttl: string) => {
      const current = values.get(key);
      const currentStreamId = current
        ? (JSON.parse(current) as { sourceStreamId?: string }).sourceStreamId
        : undefined;
      const [milliseconds, sequence] = streamIdParts(streamId);
      const [currentMilliseconds, currentSequence] = streamIdParts(currentStreamId || '0-0');
      if (currentStreamId && (milliseconds < currentMilliseconds
        || (milliseconds === currentMilliseconds && sequence < currentSequence))) {
        return 0;
      }
      values.set(key, value);
      writes.push({ key, value, expiryMode: 'EX', ttl: Number(ttl) });
      return 1;
    },
  };
}

function fakeQueue(name: string, jobs: Map<string, unknown>) {
  const requestedIds: string[] = [];
  return {
    name,
    requestedIds,
    getJob: async (jobId: string) => {
      requestedIds.push(jobId);
      return jobs.get(jobId);
    },
  };
}

test('queued-job aliases use a hashed bounded key and an expiry', async () => {
  const redis = fakeRedis();
  const logicalJobId = 'video-with-a-stable-logical-id_mindmaps';
  const alias = {
    logicalJobId,
    physicalJobId: 'video--run-11111111-1111-4111-8111-111111111111',
    queueName: 'generate-chapter-mindmaps',
  };

  await storeQueuedJobAlias(redis as never, alias, '100-0');

  assert.equal(redis.writes.length, 1);
  const write = redis.writes[0]!;
  assert.match(write.key, /^job-alias:v1:[0-9a-f]{64}$/);
  assert.equal(write.key.includes(logicalJobId), false);
  assert.equal(write.expiryMode, 'EX');
  assert.ok(write.ttl > 0);
  assert.deepEqual(await resolveQueuedJobAlias(redis as never, logicalJobId), alias);
});

test('the greatest Redis stream id wins when physical runs arrive out of order', async () => {
  const redis = fakeRedis();
  const logicalJobId = 'video-current-run';
  await storeQueuedJobAlias(redis as never, {
    logicalJobId,
    physicalJobId: 'video--run-oldest',
    queueName: 'ai-video-chapters',
  }, '1700000000000-999');
  await storeQueuedJobAlias(redis as never, {
    logicalJobId,
    physicalJobId: 'video--run-current',
    queueName: 'ai-video-chapters',
  }, '1700000000001-10');
  await storeQueuedJobAlias(redis as never, {
    logicalJobId,
    physicalJobId: 'video--run-old',
    queueName: 'ai-video-chapters',
  }, '1700000000001-9');

  assert.equal(
    (await resolveQueuedJobAlias(redis as never, logicalJobId))?.physicalJobId,
    'video--run-current',
  );
});

test('status and cancellation resolution maps a logical id to the current physical job', async () => {
  const redis = fakeRedis();
  const logicalJobId = 'video-control-id_mindmaps';
  const physicalJobId = 'video-control-id_mindmaps--run-11111111-1111-4111-8111-111111111111';
  const physicalJob = { id: physicalJobId };
  await storeQueuedJobAlias(redis as never, {
    logicalJobId,
    physicalJobId,
    queueName: 'generate-chapter-mindmaps',
  }, '101-0');
  const analysis = fakeQueue('ai-video-chapters', new Map());
  const mindmaps = fakeQueue(
    'generate-chapter-mindmaps',
    new Map([[physicalJobId, physicalJob]]),
  );

  const resolved = await resolveGenerationJob(
    redis as never,
    { analysis: analysis as never, mindmaps: mindmaps as never },
    logicalJobId,
  );

  assert.equal(resolved?.job, physicalJob);
  assert.equal(resolved?.logicalJobId, logicalJobId);
  assert.equal(resolved?.physicalJobId, physicalJobId);
  assert.deepEqual(mindmaps.requestedIds, [physicalJobId]);
  assert.deepEqual(analysis.requestedIds, []);
});

test('legacy jobs remain addressable when no run alias exists', async () => {
  const redis = fakeRedis();
  const logicalJobId = 'legacy-video-job';
  const legacyJob = { id: logicalJobId };
  const analysis = fakeQueue(
    'ai-video-chapters',
    new Map([[logicalJobId, legacyJob]]),
  );
  const mindmaps = fakeQueue('generate-chapter-mindmaps', new Map());

  const resolved = await resolveGenerationJob(
    redis as never,
    { analysis: analysis as never, mindmaps: mindmaps as never },
    logicalJobId,
  );

  assert.equal(resolved?.job, legacyJob);
  assert.equal(resolved?.logicalJobId, logicalJobId);
  assert.equal(resolved?.physicalJobId, logicalJobId);
});

test('malformed or mismatched alias values fail closed', async () => {
  const redis = fakeRedis();
  const logicalJobId = 'video-bad-alias';
  await storeQueuedJobAlias(redis as never, {
    logicalJobId,
    physicalJobId: 'physical',
    queueName: 'ai-video-chapters',
  }, '102-0');
  const key = redis.writes[0]!.key;
  redis.values.set(key, JSON.stringify({
    logicalJobId: 'different-logical-id',
    physicalJobId: 'physical',
    queueName: 'ai-video-chapters',
  }));

  assert.equal(await resolveQueuedJobAlias(redis as never, logicalJobId), undefined);
});

test('oversized logical aliases are rejected before Redis is written', async () => {
  const redis = fakeRedis();

  await assert.rejects(storeQueuedJobAlias(redis as never, {
    logicalJobId: 'x'.repeat(181),
    physicalJobId: 'physical',
    queueName: 'ai-video-chapters',
  }, '103-0'));
  assert.equal(redis.writes.length, 0);
});

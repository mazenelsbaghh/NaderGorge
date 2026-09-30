import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Queue, Worker, type Job } from 'bullmq';
import { Redis } from 'ioredis';
import { ingestStreamJob, type QueueSet } from '../queues/jobIngestion.js';
import { AdminAICallbackError, type AdminAICallbackClient, type AdminAIClaimContext } from '../services/adminAICallbackClient.js';
import { createAdminAITurnProcessor, type AdminAITurnJobData } from './processAdminAITurn.js';

const redisUrl = process.env.ADMIN_AI_TEST_REDIS_URL;
if (!redisUrl) throw new Error('ADMIN_AI_TEST_REDIS_URL must point to a disposable Redis instance.');

function waitForJob(worker: Worker, event: 'failed' | 'completed'): Promise<Job> {
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => { cleanup(); reject(new Error(`Timed out waiting for ${event}`)); }, 15_000);
    const onJob = (job: Job | undefined) => {
      if (!job) { cleanup(); reject(new Error(`Worker emitted ${event} without a job`)); return; }
      cleanup(); resolve(job);
    };
    const onError = (error: Error) => { cleanup(); reject(error); };
    const cleanup = () => {
      clearTimeout(timeout);
      worker.off(event, onJob);
      worker.off('error', onError);
    };
    worker.once(event, onJob);
    worker.once('error', onError);
  });
}

test('Redis delivery and a new worker replay the saved completion without a second inference', async () => {
  const redis = new Redis(redisUrl, { maxRetriesPerRequest: null });
  const prefix = `admin-ai-restart-${crypto.randomUUID()}`;
  const queue = new Queue<AdminAITurnJobData>('ai-admin-agent-turns', { connection: redis, prefix });
  let firstWorker: Worker<AdminAITurnJobData> | undefined;
  let secondWorker: Worker<AdminAITurnJobData> | undefined;
  try {
    await queue.waitUntilReady();
    try { await redis.xgroup('CREATE', 'job-stream', 'worker-group', '0', 'MKSTREAM'); }
    catch (error) { if (!(error instanceof Error) || !error.message.includes('BUSYGROUP')) throw error; }

    const turnId = crypto.randomUUID();
    const context: AdminAIClaimContext = {
      schemaVersion: '1', turnId, conversationId: crypto.randomUUID(), actorAdminUserId: crypto.randomUUID(),
      stepNumber: 1, expectedTurnVersion: 4, expectedConversationVersion: 1, expectedSecurityVersion: 1,
      capabilityBaseline: { id: crypto.randomUUID(), version: 'b1', manifestHash: 'a'.repeat(64) },
      sensitiveDataPolicy: { id: crypto.randomUUID(), version: 'p1', policyHash: 'b'.repeat(64) },
      leaseToken: 'lease', leaseExpiresAt: new Date(Date.now() + 60_000).toISOString(),
      callbackIdempotencyKey: 'callback-1', deadlineAt: new Date(Date.now() + 60_000).toISOString(),
      systemInstructions: 'safe', messages: [], readTools: [], actionTools: [], budgets: {},
    };
    const data: AdminAITurnJobData = {
      schemaVersion: '1', turnId, conversationId: context.conversationId, queuedAt: new Date().toISOString(),
    };
    const fields = ['jobType', 'admin ai turn', 'jobId', `admin-ai-turn-${turnId}`, 'payload', JSON.stringify(data)];
    const queues = Object.fromEntries(['aiQueue', 'mindmapsQueue', 'notifQueue', 'essayQueue',
      'liveSupportQueue', 'adminAIQueue', 'lessonGameQueue'].map(key => [key, queue])) as unknown as QueueSet;
    const firstStreamId = await redis.xadd('job-stream', '*', ...fields);
    assert.ok(firstStreamId);
    const ingested = await ingestStreamJob(redis, queues, firstStreamId, fields, async () => false);
    assert.equal(ingested.action, 'enqueued');
    assert.equal(ingested.targetJobId, `admin-ai-turn-${turnId}`);

    let claims = 0;
    let inference = 0;
    let deliveries = 0;
    const callbacks: AdminAICallbackClient = {
      claim: async () => {
        claims++;
        return claims === 1 ? context : { ...context, expectedTurnVersion: 5, leaseToken: 'renewed-lease' };
      },
      renew: async () => ({}), reads: async () => ({}), fail: async () => ({}),
      complete: async () => {
        deliveries++;
        if (deliveries === 1) throw new AdminAICallbackError('CALLBACK_UNAVAILABLE', true);
        if (deliveries === 2) throw new AdminAICallbackError('CALLBACK_REJECTED', false, 409);
        return {};
      },
    };
    const processor = createAdminAITurnProcessor({
      callbacks, cancelled: async () => false,
      runAgent: async () => {
        inference++;
        return {
          decision: { schemaVersion: '1', type: 'refuse', refusal: { reasonCode: 'OUT_OF_SCOPE', messageAr: 'لا' } },
          decisionHash: 'c'.repeat(64), provider: 'test', model: 'test', providerResponseId: null,
          inputTokenCount: null, outputTokenCount: null, stepNumber: 1, expectedTurnVersion: 4, leaseToken: 'lease',
        };
      },
    });
    const workerOptions = { connection: redis, prefix, autorun: false, concurrency: 1 };
    firstWorker = new Worker<AdminAITurnJobData>('ai-admin-agent-turns', processor, workerOptions);
    const firstFailure = waitForJob(firstWorker, 'failed');
    void firstWorker.run();
    await firstFailure;
    await firstWorker.close();
    firstWorker = undefined;

    const saved = await queue.getJob(ingested.targetJobId!);
    assert.ok(saved?.data.completion);
    assert.equal(inference, 1);
    assert.equal(deliveries, 1);
    const duplicateStreamId = await redis.xadd('job-stream', '*', ...fields);
    assert.ok(duplicateStreamId);
    const redelivery = await ingestStreamJob(redis, queues, duplicateStreamId, fields, async () => false);
    assert.equal(redelivery.action, 'skipped-existing');

    secondWorker = new Worker<AdminAITurnJobData>('ai-admin-agent-turns', processor, workerOptions);
    const completed = waitForJob(secondWorker, 'completed');
    void secondWorker.run();
    await completed;
    assert.equal(await saved.getState(), 'completed');
    assert.equal((await queue.getJob(ingested.targetJobId!))?.data.completion?.leaseToken, 'renewed-lease');
    assert.equal(claims, 2);
    assert.equal(inference, 1);
    assert.equal(deliveries, 3);
  } finally {
    if (firstWorker) await firstWorker.close();
    if (secondWorker) await secondWorker.close();
    await queue.obliterate({ force: true });
    await queue.close();
    await redis.quit();
  }
});

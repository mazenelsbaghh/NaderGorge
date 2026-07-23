import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { Job } from 'bullmq';
import { createLiveSupportTurnProcessor, type LiveSupportTurnJobData } from './processLiveSupportTurn.js';
import { LiveSupportCallbackError, type LiveSupportCallbackClient } from '../services/liveSupportCallbackClient.js';
import type { LiveSupportClaimContext } from '../services/liveSupportAgent.js';

function claim(): LiveSupportClaimContext {
  return {
    schemaVersion: '1', turnId: crypto.randomUUID(), conversationId: crypto.randomUUID(), policyVersionId: crypto.randomUUID(),
    expectedConversationVersion: 4, callbackIdempotencyKey: crypto.randomUUID(), deadlineAt: new Date(Date.now() + 30_000).toISOString(),
    systemInstructions: 'ساعد بأمان', knowledgeDocuments: [], studentContext: {},
    messages: [{ senderType: 'Guest', content: 'مساعدة', sentAt: new Date().toISOString() }],
    allowedActions: [], allowedDecisionTypes: ['reply'],
  };
}

function fakeJob(context: LiveSupportClaimContext) {
  const job = {
    data: { schemaVersion: '1', turnId: context.turnId, conversationId: context.conversationId, queuedAt: new Date().toISOString() } as LiveSupportTurnJobData,
    updateData: async (data: LiveSupportTurnJobData) => { job.data = data; },
  };
  return job as unknown as Job<LiveSupportTurnJobData>;
}

test('callback retry reuses persisted completion and never repeats inference', async () => {
  const context = claim();
  const job = fakeJob(context);
  let inferenceCalls = 0;
  let completionCalls = 0;
  const callbacks: LiveSupportCallbackClient = {
    claim: async () => context,
    complete: async () => {
      completionCalls++;
      if (completionCalls === 1) throw new LiveSupportCallbackError('CALLBACK_UNAVAILABLE', true);
      return 'completed';
    },
    fail: async () => { throw new Error('fail callback must not run for delivery errors'); },
  };
  const processor = createLiveSupportTurnProcessor({ callbacks, infer: async () => {
    inferenceCalls++;
    return { decision: { schemaVersion: '1', type: 'reply', messageAr: 'تحت أمرك' }, provider: 'test', model: 'test-model' };
  } });

  await assert.rejects(() => processor(job), /CALLBACK_UNAVAILABLE/);
  assert.ok(job.data.completion);
  await processor(job);
  assert.equal(inferenceCalls, 1);
  assert.equal(completionCalls, 2);
});

test('inference failures send stable safe codes without raw errors', async () => {
  const context = claim();
  let reported: unknown;
  const callbacks: LiveSupportCallbackClient = {
    claim: async () => context,
    complete: async () => { throw new Error('not expected'); },
    fail: async (_turnId, payload) => { reported = payload; return 'failed'; },
  };
  const processor = createLiveSupportTurnProcessor({ callbacks, infer: async () => { throw new Error('secret raw provider response'); } });

  const result = await processor(fakeJob(context));
  assert.deepEqual(result, { success: false, reason: 'AI_PROVIDER_FAILURE', failureReported: true });
  assert.equal((reported as { failureCode: string }).failureCode, 'AI_PROVIDER_FAILURE');
  assert.doesNotMatch(JSON.stringify(reported), /secret raw provider response/);
});

test('a successful fail callback ends the job without a retry or second inference', async () => {
  const context = claim();
  let inferenceCalls = 0;
  let claimCalls = 0;
  let failCalls = 0;
  const callbacks: LiveSupportCallbackClient = {
    claim: async () => { claimCalls++; return context; },
    complete: async () => { throw new Error('not expected'); },
    fail: async () => { failCalls++; return 'failed'; },
  };
  const processor = createLiveSupportTurnProcessor({ callbacks, infer: async () => {
    inferenceCalls++;
    throw new Error('provider unavailable');
  } });

  const result = await processor(fakeJob(context));
  assert.deepEqual(result, { success: false, reason: 'AI_PROVIDER_FAILURE', failureReported: true });
  assert.equal(claimCalls, 1);
  assert.equal(inferenceCalls, 1);
  assert.equal(failCalls, 1);
});

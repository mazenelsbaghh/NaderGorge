import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { Job } from 'bullmq';
import { AdminAICallbackError, type AdminAICallbackClient, type AdminAIClaimContext } from '../services/adminAICallbackClient.js';
import { AdminAIAgentRuntimeError, runAdminAIAgent } from '../services/adminAIAgent.js';
import { GeminiDeveloperApiError } from '../services/aiProvider.js';
import { createAdminAITurnProcessor, type AdminAITurnJobData } from './processAdminAITurn.js';

function claim(): AdminAIClaimContext { return { schemaVersion: '1', turnId: crypto.randomUUID(), conversationId: crypto.randomUUID(), actorAdminUserId: crypto.randomUUID(), stepNumber: 1, expectedTurnVersion: 4, expectedConversationVersion: 1, expectedSecurityVersion: 1, capabilityBaseline: { id: crypto.randomUUID(), version: 'b1', manifestHash: 'a'.repeat(64) }, sensitiveDataPolicy: { id: crypto.randomUUID(), version: 'p1', policyHash: 'b'.repeat(64) }, leaseToken: 'lease', leaseExpiresAt: new Date(Date.now() + 60_000).toISOString(), callbackIdempotencyKey: 'cb', deadlineAt: new Date(Date.now() + 60_000).toISOString(), systemInstructions: 'safe', messages: [], readTools: [], actionTools: [], budgets: {} }; }
function fakeJob(context: AdminAIClaimContext, queuedAt = new Date().toISOString()) { const job = { id: 'job', data: { schemaVersion: '1', turnId: context.turnId, conversationId: context.conversationId, queuedAt } as AdminAITurnJobData, updateData: async (data: AdminAITurnJobData) => { job.data = data; } }; return job as unknown as Job<AdminAITurnJobData>; }
function clients(context: AdminAIClaimContext, complete: AdminAICallbackClient['complete'], fail: AdminAICallbackClient['fail'] = async () => ({})): AdminAICallbackClient { return { claim: async () => context, renew: async () => ({}), reads: async () => ({}), complete, fail }; }
const agentResult = { decision: { schemaVersion: '1' as const, type: 'refuse' as const, refusal: { reasonCode: 'OUT_OF_SCOPE' as const, messageAr: 'لا' } }, decisionHash: 'a'.repeat(64), provider: 'gemini-developer', model: 'test', providerResponseId: null, inputTokenCount: null, outputTokenCount: null, stepNumber: 1, expectedTurnVersion: 4, leaseToken: 'lease' };

test('provider-completed callback-pending retry persists completion and performs no second inference', async () => {
  const context = claim(); const job = fakeJob(context); let inference = 0; let completed = 0;
  const processor = createAdminAITurnProcessor({ callbacks: clients(context, async () => { completed++; if (completed === 1) throw new AdminAICallbackError('CALLBACK_UNAVAILABLE', true); return {}; }), runAgent: async () => { inference++; return agentResult; }, cancelled: async () => false });
  await assert.rejects(() => processor(job), /CALLBACK_UNAVAILABLE/); assert.ok(job.data.completion); await processor(job);
  assert.equal(inference, 1); assert.equal(completed, 2);
});
test('stale queue job fails safely without provider inference', async () => {
  const context = claim(); let inference = 0; let reported: unknown;
  const processor = createAdminAITurnProcessor({ callbacks: clients(context, async () => ({}), async (_id, payload) => { reported = payload; return {}; }), runAgent: async () => { inference++; return agentResult; }, cancelled: async () => false });
  const result = await processor(fakeJob(context, new Date(Date.now() - 600_000).toISOString())); assert.equal(inference, 0); assert.equal(result.reason, 'AI_QUEUE_STALE'); assert.equal((reported as { failureCode: string }).failureCode, 'AI_QUEUE_STALE');
});
test('provider failure callback contains only stable safe code and no raw error', async () => {
  const context = claim(); let reported: unknown;
  const processor = createAdminAITurnProcessor({ callbacks: clients(context, async () => ({}), async (_id, payload) => { reported = payload; return {}; }), runAgent: async () => { throw new Error('SECRET_PROVIDER_BODY'); }, cancelled: async () => false });
  assert.equal((await processor(fakeJob(context))).reason, 'AI_PROVIDER_FAILURE'); assert.doesNotMatch(JSON.stringify(reported), /SECRET_PROVIDER_BODY/);
});
test('provider failure telemetry exposes only a safe category and status', async (testContext) => {
  const context = claim(); const events: unknown[][] = []; const originalInfo = console.info;
  console.info = (...args: unknown[]) => { events.push(args); };
  testContext.after(() => { console.info = originalInfo; });
  const processor = createAdminAITurnProcessor({
    callbacks: clients(context, async () => ({})),
    runAgent: async () => { throw new AdminAIAgentRuntimeError(new GeminiDeveloperApiError('provider-overloaded', 'SECRET_NAME', 503), 'lease', 4); },
    cancelled: async () => false,
  });
  await processor(fakeJob(context));
  const serialized = JSON.stringify(events);
  assert.match(serialized, /provider-overloaded/);
  assert.match(serialized, /503/);
  assert.doesNotMatch(serialized, /SECRET_NAME/);
});
test('wrapped callback rejection telemetry exposes only its safe code and status', async (testContext) => {
  const context = claim(); const events: unknown[][] = []; const originalInfo = console.info;
  console.info = (...args: unknown[]) => { events.push(args); };
  testContext.after(() => { console.info = originalInfo; });
  const processor = createAdminAITurnProcessor({
    callbacks: clients(context, async () => ({})),
    runAgent: async () => { throw new AdminAIAgentRuntimeError(new AdminAICallbackError('CALLBACK_REJECTED', false, 400), 'lease', 4); },
    cancelled: async () => false,
  });
  await processor(fakeJob(context));
  const serialized = JSON.stringify(events);
  assert.match(serialized, /CALLBACK_REJECTED/);
  assert.match(serialized, /400/);
});
test('failure after a read uses the renewed lease token', async () => {
  const context = claim(); let reported: unknown;
  const processor = createAdminAITurnProcessor({
    callbacks: clients(context, async () => ({}), async (_id, payload) => { reported = payload; return {}; }),
    runAgent: async () => { throw new AdminAIAgentRuntimeError(new Error('provider failed'), 'renewed-lease', 6); },
    cancelled: async () => false,
  });
  await processor(fakeJob(context));
  assert.equal((reported as { leaseToken: string }).leaseToken, 'renewed-lease');
});
test('cancellation before claim prevents all callback and provider work', async () => {
  const context = claim(); let claims = 0; const callback = clients(context, async () => ({})); callback.claim = async () => { claims++; return context; };
  const result = await createAdminAITurnProcessor({ callbacks: callback, runAgent: async () => agentResult, cancelled: async () => true })(fakeJob(context)); assert.equal(result.reason, 'CANCELLED'); assert.equal(claims, 0);
});

test('2026-08-20 processor completes with the lease renewed during inference', async () => {
  const context = claim(); let renewedLease = ''; let completion: Record<string, unknown> | undefined;
  const callback = clients(context, async (_turnId, payload) => { completion = payload; return {}; });
  callback.renew = async (_turnId, payload) => {
    assert.equal(payload.workerInstanceId, 'worker-forwarded');
    renewedLease = `renewed-${crypto.randomUUID()}`;
    return { turnVersion: context.expectedTurnVersion, leaseToken: renewedLease };
  };
  await createAdminAITurnProcessor({
    callbacks: callback,
    workerInstanceId: 'worker-forwarded',
    runAgent: (agentContext, callbacks, options) => runAdminAIAgent(agentContext, callbacks, {
      ...options,
      provider: async () => { await new Promise(resolve => setTimeout(resolve, 25)); return { text: JSON.stringify(agentResult.decision) }; },
      model: 'test',
      leaseRenewIntervalMs: 5,
    }),
    cancelled: async () => false,
  })(fakeJob(context));
  assert.ok(renewedLease);
  assert.equal(completion?.leaseToken, renewedLease);
});

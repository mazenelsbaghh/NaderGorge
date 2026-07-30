import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createLiveSupportCallbackClient, LiveSupportCallbackError } from './liveSupportCallbackClient.js';

const token = 'callback-secret-that-is-long-enough-for-tests';
const turnId = crypto.randomUUID();

function validClaim() {
  return {
    schemaVersion: '1', turnId, conversationId: crypto.randomUUID(), policyVersionId: crypto.randomUUID(),
    expectedConversationVersion: 1, callbackIdempotencyKey: 'turn-key', deadlineAt: new Date(Date.now() + 60_000).toISOString(),
    systemInstructions: 'ساعد بأمان', knowledgeDocuments: [], studentContext: {}, messages: [], allowedActions: [], allowedDecisionTypes: ['reply'],
  };
}

function response(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
}

test('claim rejects a malformed or mismatched contract before the worker uses it', async () => {
  const client = createLiveSupportCallbackClient({ token, baseUrl: 'http://backend', fetchImpl: async () => response({ ...validClaim(), turnId: crypto.randomUUID() }) });
  await assert.rejects(() => client.claim(turnId), (error: unknown) => error instanceof LiveSupportCallbackError && error.code === 'CALLBACK_INVALID_RESPONSE' && !error.retryable);
});

test('claim validates the deadline and bounded collection shape', async () => {
  const claim = validClaim();
  claim.deadlineAt = new Date(Date.now() - 1).toISOString();
  const client = createLiveSupportCallbackClient({ token, baseUrl: 'http://backend', fetchImpl: async () => response(claim) });
  await assert.rejects(() => client.claim(turnId), /CALLBACK_INVALID_RESPONSE/);
});

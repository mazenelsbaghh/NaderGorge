import assert from 'node:assert/strict';
import { test } from 'node:test';
import { AdminAICallbackError, createAdminAICallbackClient } from './adminAICallbackClient.js';

const token = 'admin-callback-secret-that-is-long-enough'; const turnId = crypto.randomUUID();
const validClaim = () => ({ schemaVersion: '1', turnId, conversationId: crypto.randomUUID(), actorAdminUserId: crypto.randomUUID(), stepNumber: 1, expectedTurnVersion: 1, expectedConversationVersion: 1, expectedSecurityVersion: 1, leaseToken: 'lease', leaseExpiresAt: new Date(Date.now() + 60_000).toISOString(), callbackIdempotencyKey: 'callback-key', deadlineAt: new Date(Date.now() + 60_000).toISOString(), systemInstructions: 'آمن', messages: [], readTools: [], actionTools: [], budgets: {}, capabilityBaseline: { id: crypto.randomUUID(), version: '1', manifestHash: 'a'.repeat(64) }, sensitiveDataPolicy: { id: crypto.randomUUID(), version: '1', policyHash: 'b'.repeat(64) } });
const response = (body: unknown, status = 200, headers?: HeadersInit) => new Response(JSON.stringify(body), { status, ...(headers ? { headers } : {}) });

test('claim sends internal token and validates identity', async () => {
  const client = createAdminAICallbackClient({ token, baseUrl: 'http://backend', fetchImpl: async (_url, init) => { assert.equal((init?.headers as Record<string, string>)['X-Internal-Token'], token); return response(validClaim()); } });
  assert.equal((await client.claim(turnId, 'worker-1'))?.turnId, turnId);
});
test('claim rejects mismatched response and safely treats 404 as no work', async () => {
  const bad = createAdminAICallbackClient({ token, fetchImpl: async () => response({ ...validClaim(), turnId: crypto.randomUUID() }) });
  await assert.rejects(() => bad.claim(turnId, 'worker'), /CALLBACK_INVALID_RESPONSE/);
  const missing = createAdminAICallbackClient({ token, fetchImpl: async () => response({}, 404) });
  assert.equal(await missing.claim(turnId, 'worker'), null);
});
test('malformed JSON is permanent invalid response, not retryable transport failure', async () => {
  const client = createAdminAICallbackClient({ token, fetchImpl: async () => new Response('{broken', { status: 200 }) });
  await assert.rejects(() => client.claim(turnId, 'worker'), (error: unknown) => error instanceof AdminAICallbackError && error.code === 'CALLBACK_INVALID_RESPONSE' && !error.retryable);
});
test('timeouts, oversized responses and retry classification are closed', async () => {
  const timeout = createAdminAICallbackClient({ token, timeoutMs: 1, fetchImpl: async (_url, init) => new Promise((_resolve, reject) => init?.signal?.addEventListener('abort', () => reject(new Error('aborted')))) });
  await assert.rejects(() => timeout.claim(turnId, 'worker'), (error: unknown) => error instanceof AdminAICallbackError && error.code === 'CALLBACK_TIMEOUT' && error.retryable);
  const large = createAdminAICallbackClient({ token, fetchImpl: async () => response({}, 200, { 'content-length': String(200_000) }) });
  await assert.rejects(() => large.claim(turnId, 'worker'), /CALLBACK_RESPONSE_TOO_LARGE/);
  const rejected = createAdminAICallbackClient({ token, fetchImpl: async () => response({}, 503) });
  await assert.rejects(() => rejected.claim(turnId, 'worker'), (error: unknown) => error instanceof AdminAICallbackError && error.retryable && error.httpStatus === 503);
});
test('all callback operations use bounded internal routes and payloads', async () => {
  const seen: string[] = [];
  const client = createAdminAICallbackClient({ token, baseUrl: 'http://backend/api/v1', fetchImpl: async (url) => { seen.push(String(url)); return response({ ok: true }); } });
  await client.renew(turnId, { schemaVersion: '1' }); await client.reads(turnId, 2, { schemaVersion: '1' }); await client.complete(turnId, { schemaVersion: '1' }); await client.fail(turnId, { schemaVersion: '1' });
  assert.deepEqual(seen.map(url => new URL(url).pathname), [`/api/v1/internal/admin-ai/turns/${turnId}/lease/renew`, `/api/v1/internal/admin-ai/turns/${turnId}/steps/2/reads`, `/api/v1/internal/admin-ai/turns/${turnId}/complete`, `/api/v1/internal/admin-ai/turns/${turnId}/fail`]);
});
test('4xx is permanent while rate-limit and server errors are retryable, without response leakage', async () => {
  for (const [status, retryable] of [[400, false], [409, false], [429, true], [500, true]] as const) {
    const client = createAdminAICallbackClient({ token, fetchImpl: async () => new Response('SECRET_SENTINEL', { status }) });
    await assert.rejects(() => client.complete(turnId, {}), (error: unknown) => error instanceof AdminAICallbackError && error.retryable === retryable && !error.message.includes('SECRET_SENTINEL'));
  }
});
test('claim rejects missing baseline or policy and response bytes over the streaming limit', async () => {
  const missing = createAdminAICallbackClient({ token, fetchImpl: async () => response({ ...validClaim(), capabilityBaseline: undefined }) });
  await assert.rejects(() => missing.claim(turnId, 'worker'), /CALLBACK_INVALID_RESPONSE/);
  const huge = createAdminAICallbackClient({ token, fetchImpl: async () => new Response(JSON.stringify({ payload: 'x'.repeat(140_000) })) });
  await assert.rejects(() => huge.complete(turnId, {}), /CALLBACK_RESPONSE_TOO_LARGE/);
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { decideAdminAiSequence, parseAdminAiRealtimeEnvelope } from './admin-ai-agent-client-contract.ts';

const base = {
  schemaVersion: '1', eventId: crypto.randomUUID(), sequence: 1, type: 'turn.changed',
  conversationId: crypto.randomUUID(), occurredAt: new Date().toISOString(),
};

test('accepts the content-free closed envelope', () => assert.deepEqual(parseAdminAiRealtimeEnvelope(base), base));
test('rejects payload content, unknown events, bad versions and invalid identifiers', () => {
  assert.equal(parseAdminAiRealtimeEnvelope({ ...base, payload: { content: 'secret' } }), undefined);
  assert.equal(parseAdminAiRealtimeEnvelope({ ...base, type: 'message.content' }), undefined);
  assert.equal(parseAdminAiRealtimeEnvelope({ ...base, schemaVersion: '2' }), undefined);
  assert.equal(parseAdminAiRealtimeEnvelope({ ...base, eventId: 'bad' }), undefined);
});
test('sequence decisions dedupe and reconcile gaps', () => {
  assert.equal(decideAdminAiSequence(0, 1), 'accept');
  assert.equal(decideAdminAiSequence(4, 4), 'duplicate');
  assert.equal(decideAdminAiSequence(4, 6), 'reconcile');
});

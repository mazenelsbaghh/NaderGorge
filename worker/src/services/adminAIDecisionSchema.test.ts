import assert from 'node:assert/strict';
import { test } from 'node:test';
import { ADMIN_AI_DECISION_TYPES, hashAdminAIDecision, parseAdminAIDecision } from './adminAIDecisionSchema.js';

test('admin AI decision exposes exactly five schema-v1 branches', () => assert.deepEqual(ADMIN_AI_DECISION_TYPES, ['answer', 'clarify', 'request_reads', 'propose_actions', 'refuse']));
test('canonical hash is stable across object key order', () => {
  const first = parseAdminAIDecision({ schemaVersion: '1', type: 'propose_actions', messageAr: 'x', actions: [{ clientActionId: 'a', capabilityKey: 'users.note', arguments: { b: 2, a: 1 }, safeIntentAr: 'x' }] });
  const second = parseAdminAIDecision({ actions: [{ safeIntentAr: 'x', arguments: { a: 1, b: 2 }, capabilityKey: 'users.note', clientActionId: 'a' }], messageAr: 'x', type: 'propose_actions', schemaVersion: '1' });
  assert.equal(hashAdminAIDecision(first), hashAdminAIDecision(second)); assert.equal(hashAdminAIDecision(first).length, 64);
});
test('2026-08-21 Arabic answer hash matches the backend canonical JSON contract', () => {
  const decision = parseAdminAIDecision({ schemaVersion: '1', type: 'answer', answer: { summaryAr: 'عدد الطلاب 10 😀', facts: ['سطر\u2028جديد'], calculations: [], inferences: [], limitations: [], suggestions: [], evidenceInvocationIds: ['11111111-1111-4111-8111-111111111111'] } });
  assert.equal(hashAdminAIDecision(decision), 'f0af81c51fddb9880cd4de4248b87a1aac7c5586824ed5b25a2ca0f90861a185');
});
test('parser accepts every terminal closed branch', () => {
  const values = [
    { schemaVersion: '1', type: 'answer', answer: { summaryAr: 'x', facts: ['x'], calculations: [], inferences: [], limitations: [], suggestions: [], evidenceInvocationIds: [] } },
    { schemaVersion: '1', type: 'clarify', clarification: { questionAr: 'أي طالب؟', reasonCode: 'AMBIGUOUS_TARGET', options: [] } },
    { schemaVersion: '1', type: 'propose_actions', messageAr: 'مقترح', actions: [{ clientActionId: 'a', capabilityKey: 'user.note', arguments: {}, safeIntentAr: 'إضافة ملاحظة' }] },
    { schemaVersion: '1', type: 'refuse', refusal: { reasonCode: 'OUT_OF_SCOPE', messageAr: 'لا يمكن تنفيذ الطلب.' } },
  ];
  for (const value of values) assert.doesNotThrow(() => parseAdminAIDecision(value));
});
test('parser rejects unknown versions, branches, extras, depth, count and size', () => {
  const invalid = [
    { schemaVersion: '2', type: 'clarify', clarification: {} }, { schemaVersion: '1', type: 'execute', input: {} },
    { schemaVersion: '1', type: 'clarify', clarification: { questionAr: 'x', reasonCode: 'AMBIGUOUS_TARGET', options: [] }, extra: true },
    { schemaVersion: '1', type: 'request_reads', calls: Array(5).fill({ callId: 'x', capabilityKey: 'x' }) },
    { schemaVersion: '1', type: 'propose_actions', messageAr: 'x', actions: [{ clientActionId: 'x', capabilityKey: 'x', arguments: { a: { b: { c: { d: { e: { f: 1 } } } } } }, safeIntentAr: 'x' }] },
    { schemaVersion: '1', type: 'clarify', clarification: { questionAr: 'x'.repeat(70_000), reasonCode: 'AMBIGUOUS_TARGET', options: [] } },
  ];
  for (const value of invalid) assert.throws(() => parseAdminAIDecision(value), /invalid admin-agent decision/i);
});

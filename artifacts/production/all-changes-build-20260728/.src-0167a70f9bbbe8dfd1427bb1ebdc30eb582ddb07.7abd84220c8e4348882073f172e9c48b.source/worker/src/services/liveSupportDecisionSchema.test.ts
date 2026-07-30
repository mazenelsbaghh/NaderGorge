import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  LIVE_SUPPORT_DECISION_TYPES,
  LIVE_SUPPORT_SCHEMA_VERSION,
  hashLiveSupportDecision,
  isLiveSupportDecisionType,
  parseLiveSupportDecision,
} from './liveSupportDecisionSchema.js';

test('live support decision contract exposes exactly six stable branches', () => {
  assert.equal(LIVE_SUPPORT_SCHEMA_VERSION, '1');
  assert.deepEqual(LIVE_SUPPORT_DECISION_TYPES, [
    'reply',
    'propose_action',
    'request_verification',
    'propose_account_creation',
    'request_resolution',
    'handoff',
  ]);
  assert.equal(isLiveSupportDecisionType('reply'), true);
  assert.equal(isLiveSupportDecisionType('tool_call'), false);
});

test('strict parser accepts each closed branch and produces a stable canonical hash', () => {
  const first = parseLiveSupportDecision({
    type: 'propose_action',
    schemaVersion: '1',
    action: { safeEffectSummaryAr: 'تحديث آمن', arguments: { b: 2, a: 1 }, key: 'student.update' },
  });
  const second = parseLiveSupportDecision({
    schemaVersion: '1',
    type: 'propose_action',
    action: { key: 'student.update', arguments: { a: 1, b: 2 }, safeEffectSummaryAr: 'تحديث آمن' },
  });

  assert.equal(hashLiveSupportDecision(first), hashLiveSupportDecision(second));
  assert.equal(hashLiveSupportDecision(first).length, 64);
});

test('strict parser rejects extras, wrong branches, forced handoff, and excessive payloads', () => {
  const invalid = [
    { schemaVersion: '1', type: 'reply', messageAr: 'أهلاً', extra: true },
    { schemaVersion: '1', type: 'reply', messageAr: 'أهلاً', handoff: { reasonCode: 'x', safeSummaryAr: 'x', forced: false } },
    { schemaVersion: '1', type: 'handoff', handoff: { reasonCode: 'x', safeSummaryAr: 'x', forced: true } },
    { schemaVersion: '1', type: 'reply', messageAr: 'x'.repeat(4001) },
    { schemaVersion: '1', type: 'propose_account_creation', accountCreation: { requestedFields: Array(21).fill('field') } },
  ];
  for (const value of invalid) assert.throws(() => parseLiveSupportDecision(value), /invalid live-support decision/i);
});

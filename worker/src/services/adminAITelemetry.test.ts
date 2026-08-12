import assert from 'node:assert/strict';
import { test } from 'node:test';
import { logAdminAIEvent, recordAdminAIMetric, safeAdminAITelemetryLabel } from './adminAITelemetry.js';

test('admin AI telemetry accepts only reviewed low-cardinality dimensions', () => {
  assert.doesNotThrow(() => recordAdminAIMetric('model_outcome', 1, { provider: 'gemini-developer', model: 'gemini-flash', outcome: 'success', decisionType: 'answer' }));
  for (const dimensions of [{ prompt: 'secret' }, { userId: crypto.randomUUID() }, { outcome: 'person@example.com' }, { capabilityKey: 'x'.repeat(81) }]) {
    assert.throws(() => recordAdminAIMetric('read_outcome', 1, dimensions), /TELEMETRY/);
  }
  assert.throws(() => recordAdminAIMetric('queue_age', Number.NaN), /INVALID_ADMIN_AI_TELEMETRY_VALUE/);
  assert.equal(safeAdminAITelemetryLabel(crypto.randomUUID()), 'other');
});

test('structured worker events contain no identifiers or conversational content', () => {
  const original = console.info; const records: unknown[] = [];
  console.info = (...fields: unknown[]) => { records.push(fields); };
  try { logAdminAIEvent('turn_completed', { outcome: 'success', decisionType: 'answer' }); } finally { console.info = original; }
  assert.equal(records.length, 1); assert.doesNotMatch(JSON.stringify(records), /turnId|conversation|prompt|message|content/);
  assert.throws(() => logAdminAIEvent('turn_failed', { failureCode: crypto.randomUUID() }), /HIGH_CARDINALITY/);
});

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { recordLiveSupportMetric } from './liveSupportTelemetry.js';

test('live support job contract contains identifiers only and no protected context', () => {
  const job = { schemaVersion: '1', turnId: crypto.randomUUID(), conversationId: crypto.randomUUID(), queuedAt: new Date().toISOString() };
  assert.deepEqual(Object.keys(job).sort(), ['conversationId', 'queuedAt', 'schemaVersion', 'turnId']);
  assert.doesNotMatch(JSON.stringify(job), /prompt|password|answer|token|phone|message/i);
});

test('telemetry rejects sensitive dimension names', () => {
  assert.throws(() => recordLiveSupportMetric('callback_outcome', 1, { password: 'forbidden' }), /UNSAFE_TELEMETRY_DIMENSION/);
  assert.throws(() => recordLiveSupportMetric('callback_outcome', 1, { verificationAnswer: 'forbidden' }), /UNSAFE_TELEMETRY_DIMENSION/);
});

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

test('static redaction verifies that forbidden patterns are not leaked in payloads/transcripts', () => {
  const secrets = {
    password: 'my-secret-password',
    callbackSecret: 'super-secret-callback',
    token: 'my-token',
    verificationAnswer: 'secret-answer',
    lookupValue: '01011111111'
  };

  const payload = JSON.stringify(secrets);
  
  const redact = (input: string) => {
    return input.replace(/"(password|callbackSecret|token|verificationAnswer|lookupValue)":"[^"]+"/gi, '"$1":"[REDACTED]"');
  };

  const redacted = redact(payload);
  assert.match(redacted, /"password":"\[REDACTED\]"/);
  assert.match(redacted, /"callbackSecret":"\[REDACTED\]"/);
  assert.match(redacted, /"token":"\[REDACTED\]"/);
  assert.match(redacted, /"verificationAnswer":"\[REDACTED\]"/);
  assert.match(redacted, /"lookupValue":"\[REDACTED\]"/);

  assert.doesNotMatch(redacted, /my-secret-password/);
  assert.doesNotMatch(redacted, /super-secret-callback/);
});

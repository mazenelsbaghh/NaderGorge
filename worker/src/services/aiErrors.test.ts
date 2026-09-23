import { test } from 'node:test';
import assert from 'node:assert/strict';
import { classifyAIError, isQuotaExhausted } from './aiErrors.js';

test('classification accepts only structured quota exhaustion', () => {
  assert.equal(isQuotaExhausted({ status: 429 }), true);
  assert.equal(isQuotaExhausted({ error: { status: 'RESOURCE_EXHAUSTED' } }), true);
  assert.equal(isQuotaExhausted(new Error('429 quota exhausted')), false);
});

test('classification keeps permission validation and implementation errors distinct', () => {
  assert.equal(classifyAIError({ status: 401 }).category, 'authentication');
  assert.equal(classifyAIError({ status: 403 }).category, 'permission');
  assert.equal(classifyAIError({ status: 400 }).category, 'validation');
  assert.equal(classifyAIError({ status: 404 }).category, 'not-found');
  assert.equal(classifyAIError({ status: 500 }).category, 'provider');
  assert.equal(classifyAIError(new Error('bug')).category, 'implementation');
});

test('prepaid exhaustion is distinct from temporary rate limiting and unstructured messages', () => {
  for (const error of [
    { status: 402 },
    { error: { code: 'payment_required' } },
    { status: 429, message: 'Your prepayment credits are depleted.' },
    { status: 429, message: '{"error":{"message":"Your prepayment credits are depleted."}}' },
  ]) assert.equal(classifyAIError(error).category, 'balance-exhausted');
  assert.equal(classifyAIError({ status: 429, message: 'Requests per minute exceeded' }).category, 'quota-exhausted');
  assert.equal(classifyAIError(new Error('Your prepayment credits are depleted.')).category, 'implementation');
});

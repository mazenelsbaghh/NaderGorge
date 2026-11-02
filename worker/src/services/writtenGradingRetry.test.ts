import test from 'node:test';
import assert from 'node:assert/strict';
import { UnrecoverableError } from 'bullmq';
import { GeminiDeveloperApiError } from './aiProvider.js';
import { withWrittenGradingRetryPolicy } from './writtenGradingRetry.js';

test('2026-11-02 billing and access failures stop immediate queue retries while outages remain recoverable', async () => {
  for (const category of ['balance-exhausted', 'authentication', 'permission', 'validation', 'not-found']) {
    const error = new GeminiDeveloperApiError(category);
    await assert.rejects(withWrittenGradingRetryPolicy(async () => { throw error; }),
      failure => failure instanceof UnrecoverableError && failure.cause === error);
  }
  for (const error of [new GeminiDeveloperApiError('quota-exhausted'),
    new GeminiDeveloperApiError('provider-timeout'), new Error('Callback outage')]) {
    await assert.rejects(withWrittenGradingRetryPolicy(async () => { throw error; }), failure => failure === error);
  }
});

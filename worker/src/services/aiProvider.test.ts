import { test } from 'node:test';
import assert from 'node:assert/strict';
import { executeGeminiRequest, GeminiDeveloperApiError } from './aiProvider.js';

test('Gemini Developer request returns the provider response', async () => {
  assert.equal(await executeGeminiRequest(async () => 'developer-result'), 'developer-result');
});

test('Gemini Developer request classifies errors without exposing provider details', async () => {
  await assert.rejects(
    executeGeminiRequest(async () => { throw { name: 'ApiError', status: 403, secret: 'hidden' }; }),
    (error: unknown) => error instanceof GeminiDeveloperApiError
      && error.category === 'permission'
      && error.providerErrorName === 'ApiError'
      && error.providerStatus === 403
      && !error.message.includes('hidden'),
  );
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { executeGeminiRequest, GeminiDeveloperApiError } from './aiProvider.js';

test('Gemini Developer request returns the provider response', async () => {
  assert.equal(await executeGeminiRequest(async () => 'developer-result'), 'developer-result');
});

test('Gemini Developer request classifies errors without exposing provider details', async () => {
  await assert.rejects(
    executeGeminiRequest(async () => { throw { status: 403, secret: 'hidden' }; }),
    (error: unknown) => error instanceof GeminiDeveloperApiError
      && error.category === 'permission'
      && !error.message.includes('hidden'),
  );
});

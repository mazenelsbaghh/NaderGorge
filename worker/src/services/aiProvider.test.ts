import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { executeGeminiRequest, executeRetriableGeminiRequest, GeminiDeveloperApiError, setGeminiRetryWaitForTests } from './aiProvider.js';

afterEach(() => setGeminiRetryWaitForTests());

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

test('transient Gemini 503 failures retry the same stage until it succeeds', async () => {
  const delays: number[] = [];
  setGeminiRetryWaitForTests(async (delayMs) => { delays.push(delayMs); });
  let requests = 0;
  const response = await executeRetriableGeminiRequest(async () => {
    requests += 1;
    if (requests < 3) throw { name: 'ApiError', status: 503 };
    return 'recovered';
  });

  assert.equal(response, 'recovered');
  assert.equal(requests, 3);
  assert.deepEqual(delays, [2_000, 5_000]);
});

test('persistent Gemini 503 failure stops after bounded retries', async () => {
  setGeminiRetryWaitForTests(async () => undefined);
  let requests = 0;
  await assert.rejects(
    executeRetriableGeminiRequest(async () => {
      requests += 1;
      throw { name: 'ApiError', status: 503 };
    }),
    (error: unknown) => error instanceof GeminiDeveloperApiError && error.providerStatus === 503,
  );
  assert.equal(requests, 4);
});

test('hung Gemini request fails at the configured provider deadline', async (testContext) => {
  const originalDeadline = process.env.GEMINI_REQUEST_TIMEOUT_MS;
  process.env.GEMINI_REQUEST_TIMEOUT_MS = '10';
  testContext.after(() => {
    if (originalDeadline === undefined) delete process.env.GEMINI_REQUEST_TIMEOUT_MS;
    else process.env.GEMINI_REQUEST_TIMEOUT_MS = originalDeadline;
  });

  await assert.rejects(
    executeGeminiRequest(() => new Promise(() => undefined)),
    (error: unknown) => error instanceof GeminiDeveloperApiError && error.category === 'provider-timeout',
  );
});

test('provider deadlines use the bounded retry policy', async (testContext) => {
  const originalDeadline = process.env.GEMINI_REQUEST_TIMEOUT_MS;
  process.env.GEMINI_REQUEST_TIMEOUT_MS = '5';
  setGeminiRetryWaitForTests(async () => undefined);
  let requests = 0;
  testContext.after(() => {
    if (originalDeadline === undefined) delete process.env.GEMINI_REQUEST_TIMEOUT_MS;
    else process.env.GEMINI_REQUEST_TIMEOUT_MS = originalDeadline;
  });

  await assert.rejects(
    executeRetriableGeminiRequest(() => {
      requests += 1;
      return new Promise(() => undefined);
    }),
    (error: unknown) => error instanceof GeminiDeveloperApiError && error.category === 'provider-timeout',
  );
  assert.equal(requests, 4);
});

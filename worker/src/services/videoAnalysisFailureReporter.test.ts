import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { reportTerminalVideoFailure } from './videoAnalysisFailureReporter.js';

const originalFetch = globalThis.fetch;
process.env.BACKEND_API_URL = 'http://backend.test/api/v1';
process.env.API_CALLBACK_SECRET = 'test-callback-secret';

afterEach(() => { globalThis.fetch = originalFetch; });

test('terminal unrecoverable failure retries the callback and sends only a safe reason', async () => {
  const requests: Array<{ url: string; body: { status: string; message: string } }> = [];
  globalThis.fetch = async (input, init) => {
    requests.push({ url: String(input), body: JSON.parse(String(init?.body)) });
    return new Response('{}', { status: requests.length === 1 ? 503 : 200 });
  };

  const reported = await reportTerminalVideoFailure(
    { id: 'video-job-terminal', attemptsMade: 1, opts: { attempts: 3 } } as never,
    Object.assign(new Error('provider URL token=SENSITIVE_SENTINEL'), { name: 'UnrecoverableError' }),
  );

  const successfulRequest = requests.at(-1)!;
  assert.equal(reported, true);
  assert.equal(requests.length, 2);
  assert.equal(successfulRequest.url, 'http://backend.test/api/v1/internal/callbacks/ai-progress');
  assert.equal(successfulRequest.body.status, 'failed');
  assert.equal(successfulRequest.body.message, 'تعذر إكمال المهمة. أعد المحاولة أو تواصل مع الدعم.');
  assert.equal(JSON.stringify(requests).includes('SENSITIVE_SENTINEL'), false);
});

test('non-terminal failure does not send the failure callback', async () => {
  let callbackCalls = 0;
  globalThis.fetch = async () => {
    callbackCalls += 1;
    return new Response('{}', { status: 200 });
  };

  const reported = await reportTerminalVideoFailure(
    { id: 'video-job-retrying', attemptsMade: 1, opts: { attempts: 3 } } as never,
    new Error('retryable failure'),
  );

  assert.equal(reported, false);
  assert.equal(callbackCalls, 0);
});

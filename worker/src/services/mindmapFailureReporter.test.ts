import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { reportTerminalSingleMindmapFailure } from './mindmapFailureReporter.js';

const originalFetch = globalThis.fetch;
process.env.BACKEND_API_URL = 'http://backend.test/api/v1';
process.env.API_CALLBACK_SECRET = 'test-callback-secret';

afterEach(() => { globalThis.fetch = originalFetch; });

test('single mindmap failure callback runs only after terminal failure', async () => {
  const callbackBodies: Array<Record<string, unknown>> = [];
  globalThis.fetch = async (_input, init) => {
    callbackBodies.push(JSON.parse(String(init?.body)));
    return new Response('{}', { status: 200 });
  };
  const retryingJob = {
    attemptsMade: 1,
    opts: { attempts: 3 },
    data: { chapterId: 'chapter-1', generationRunId: '11111111-1111-4111-8111-111111111111' },
  };

  assert.equal(await reportTerminalSingleMindmapFailure(retryingJob as never, new Error('retry')), false);
  assert.equal(callbackBodies.length, 0);
  retryingJob.attemptsMade = 3;
  assert.equal(await reportTerminalSingleMindmapFailure(retryingJob as never, new Error('terminal')), true);
  assert.deepEqual(callbackBodies, [{
    chapterId: 'chapter-1',
    generationRunId: '11111111-1111-4111-8111-111111111111',
  }]);
});

test('single mindmap terminal callback exhausts its bounded retry budget visibly', async () => {
  let callbackAttempts = 0;
  globalThis.fetch = async () => {
    callbackAttempts += 1;
    return new Response('{}', { status: 503 });
  };
  const job = {
    attemptsMade: 1,
    opts: { attempts: 5 },
    data: { chapterId: 'chapter-2' },
  };
  const unrecoverable = Object.assign(new Error('terminal'), { name: 'UnrecoverableError' });

  await assert.rejects(reportTerminalSingleMindmapFailure(job as never, unrecoverable));
  assert.equal(callbackAttempts, 3);
});

test('legacy single mindmap failure omits an empty generation fence', async () => {
  let callbackBody: Record<string, unknown> | undefined;
  globalThis.fetch = async (_input, init) => {
    callbackBody = JSON.parse(String(init?.body));
    return new Response('{}', { status: 200 });
  };
  const unrecoverable = Object.assign(new Error('terminal'), { name: 'UnrecoverableError' });

  await reportTerminalSingleMindmapFailure({
    attemptsMade: 1,
    opts: { attempts: 5 },
    data: { chapterId: 'legacy-chapter' },
  } as never, unrecoverable);

  assert.equal(callbackBody?.chapterId, 'legacy-chapter');
  assert.equal(Object.hasOwn(callbackBody || {}, 'generationRunId'), false);
});

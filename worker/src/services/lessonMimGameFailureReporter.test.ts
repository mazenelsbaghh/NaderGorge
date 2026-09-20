import { test } from 'node:test';
import assert from 'node:assert/strict';
import { reportTerminalLessonMimGameFailure } from './lessonMimGameFailureReporter.js';

const data = { gameId: 'g', runId: 'r' };
test('failure callback is sent only after queue retries are exhausted', async () => {
  let calls = 0;
  const callbacks: any = { complete: async () => {}, fail: async () => { calls++; } };
  assert.equal(await reportTerminalLessonMimGameFailure({ data, attemptsMade: 1, opts: { attempts: 3 } } as any, new Error('provider'), callbacks), false);
  assert.equal(await reportTerminalLessonMimGameFailure({ data, attemptsMade: 3, opts: { attempts: 3 } } as any, new Error('provider'), callbacks), true);
  assert.equal(calls, 1);
});

test('invalid model output reports only the safe allowlisted code', async () => {
  let received: any;
  const error = new Error('private model response'); error.name = 'UnrecoverableError';
  await reportTerminalLessonMimGameFailure({ data, attemptsMade: 0, opts: { attempts: 3 } } as any, error, { complete: async () => {}, fail: async p => { received = p; } });
  assert.deepEqual(received, { gameId: 'g', runId: 'r', errorCode: 'MIM_MODEL_INVALID' });
});

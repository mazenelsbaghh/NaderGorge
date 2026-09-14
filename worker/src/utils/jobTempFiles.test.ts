import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { isFinalJobAttempt, isTerminalJobFailure, removeJobTempFile } from './jobTempFiles.js';

test('temporary audio is retained only while another queue attempt remains', () => {
  assert.equal(isFinalJobAttempt({ attemptsMade: 0, opts: { attempts: 3 } } as never), false);
  assert.equal(isFinalJobAttempt({ attemptsMade: 2, opts: { attempts: 3 } } as never), true);
  assert.equal(isFinalJobAttempt({ attemptsMade: 0, opts: {} } as never), true);
});

test('unrecoverable failures are terminal before the retry budget is exhausted', () => {
  const firstAttempt = { attemptsMade: 1, opts: { attempts: 5 } } as never;
  assert.equal(isTerminalJobFailure(firstAttempt, { name: 'UnrecoverableError' } as Error), true);
  assert.equal(isTerminalJobFailure(firstAttempt, { name: 'WorkerExternalError' } as Error), false);
});

test('terminal job cleanup removes the real temporary file', (testContext) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'massar-worker-job-'));
  const audioPath = path.join(directory, 'lesson.mp3');
  fs.writeFileSync(audioPath, 'audio');
  testContext.after(() => fs.rmSync(directory, { recursive: true, force: true }));

  removeJobTempFile(audioPath, 'job-1');

  assert.equal(fs.existsSync(audioPath), false);
});

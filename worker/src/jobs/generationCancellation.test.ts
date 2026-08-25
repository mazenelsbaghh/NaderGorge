import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Redis } from 'ioredis';
import { throwIfGenerationCancellationRequested } from './generationCancellation.js';

test('generation cancellation observes a stable mindmap alias behind a run-scoped job id', async () => {
  const originalGet = Redis.prototype.get;
  try {
    Redis.prototype.get = (async (key: string) => {
      return key === 'cancelled-jobs:video-1_mindmaps' ? '1' : null;
    }) as typeof Redis.prototype.get;
    const job = {
      id: 'video-1_mindmap_chapter-1--run-11111111-1111-4111-8111-111111111111',
    };

    await assert.rejects(
      throwIfGenerationCancellationRequested(job as never, ['video-1', 'video-1_mindmaps']),
      error => error instanceof Error && error.name === 'UnrecoverableError',
    );
  } finally {
    Redis.prototype.get = originalGet;
  }
});

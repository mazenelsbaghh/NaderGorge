import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Job } from 'bullmq';
import { Redis } from 'ioredis';
import { processEvaluateEssayJob, type EvaluateEssayJobData } from './evaluateEssay.js';
import { setAIServiceRuntimeFactoryForTests } from '../services/geminiService.js';

test('2026-09-09 callback outage preserves AI result for retry without regenerating it', async context => {
  const originalFetch = globalThis.fetch;
  const originalGet = Redis.prototype.get;
  Redis.prototype.get = async () => null;
  context.after(() => { globalThis.fetch = originalFetch; Redis.prototype.get = originalGet; setAIServiceRuntimeFactoryForTests(); });
  setAIServiceRuntimeFactoryForTests(() => ({
    config: { primaryProvider: 'developer', developerApiKey: 'test', textModel: 'test', imageModel: 'test' },
    developer: { models: { generateContent: async () => ({ text: '{"isCorrect":true,"feedback":"original feedback"}' }) } } as never,
  }));
  const job = {
    id: 'essay-checkpoint', data: { essaySubmissionId: 'essay', questionId: 'question', studentId: 'student', answerText: 'answer' } as EvaluateEssayJobData,
    updateProgress: async () => {}, updateData: async (next: EvaluateEssayJobData) => { job.data = next; },
  } as unknown as Job<EvaluateEssayJobData>;
  globalThis.fetch = async () => new Response('private backend diagnostics', { status: 503 });
  await assert.rejects(processEvaluateEssayJob(job), error => error instanceof Error && !error.message.includes('private backend diagnostics'));
  assert.deepEqual(job.data.evaluation, { isCorrect: true, feedback: 'original feedback' });
  // The provider is now unavailable. Delivery must still succeed from the checkpoint.
  setAIServiceRuntimeFactoryForTests(() => { throw new Error('provider unavailable'); });
  let delivered: unknown;
  globalThis.fetch = async (_url, init) => { delivered = JSON.parse(String(init?.body)); return new Response('{}', { status: 200 }); };
  const result = await processEvaluateEssayJob(job);
  assert.equal(result.success, true);
  assert.deepEqual(delivered, { essaySubmissionId: 'essay', aiScore: 1, aiFeedback: 'original feedback' });
});

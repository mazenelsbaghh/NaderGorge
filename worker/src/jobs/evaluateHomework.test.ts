import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Job } from 'bullmq';
import { Redis } from 'ioredis';
import { evaluateHomework, type HomeworkEvaluationData } from './evaluateHomework.js';
import { setAIServiceRuntimeFactoryForTests } from '../services/geminiService.js';

test('homework grades survive callback outages and resume without another provider request', async context => {
  const originalFetch = globalThis.fetch;
  const originalGet = Redis.prototype.get;
  Redis.prototype.get = async () => null;
  context.after(() => { globalThis.fetch = originalFetch; Redis.prototype.get = originalGet; setAIServiceRuntimeFactoryForTests(); });
  setAIServiceRuntimeFactoryForTests(() => ({
    config: { primaryProvider: 'developer', developerApiKey: 'test', textModel: 'test', imageModel: 'test' },
    developer: { models: { generateContent: async () => ({ text: '{"isCorrect":true,"feedback":"Correct reasoning"}' }) } } as never,
  }));
  const job = {
    id: 'homework-evaluation', data: { SubmissionId: 'submission', Fingerprint: 'A'.repeat(64), Questions: [
      { AnswerId: 'written', QuestionText: 'Explain', AnswerText: 'Student explanation', ExpectedAnswer: '' },
      { AnswerId: 'blank', QuestionText: 'Explain again', AnswerText: ' ', ExpectedAnswer: '' },
    ] } as HomeworkEvaluationData,
    updateProgress: async () => {}, updateData: async (next: HomeworkEvaluationData) => { job.data = next; },
  } as unknown as Job<HomeworkEvaluationData>;
  globalThis.fetch = async () => new Response('private diagnostics', { status: 503 });
  await assert.rejects(evaluateHomework(job), /status 503/);
  assert.deepEqual(job.data.grades?.map(g => [g.answerId, g.score]), [['written', 1], ['blank', 0]]);
  setAIServiceRuntimeFactoryForTests(() => { throw new Error('provider unavailable'); });
  let delivered: unknown;
  globalThis.fetch = async (url, init) => {
    assert.match(String(url), /\/internal\/callbacks\/homework-graded$/);
    delivered = JSON.parse(String(init?.body));
    return Response.json({ success: true, data: { submissionId: 'submission', status: 'Graded' } });
  };
  assert.equal((await evaluateHomework(job)).success, true);
  assert.deepEqual(delivered, { submissionId: 'submission', fingerprint: 'A'.repeat(64), grades: job.data.grades });
});

test('homework callback must acknowledge the same persisted attempt', async context => {
  const originalFetch = globalThis.fetch;
  const originalGet = Redis.prototype.get;
  Redis.prototype.get = async () => null;
  context.after(() => { globalThis.fetch = originalFetch; Redis.prototype.get = originalGet; });
  const job = { id: 'receipt', data: { SubmissionId: 'submission', Fingerprint: 'A'.repeat(64),
    Questions: [{ AnswerId: 'answer', QuestionText: 'Question', AnswerText: 'Answer', ExpectedAnswer: '' }],
    grades: [{ answerId: 'answer', score: 1, feedback: 'Saved explanation' }] }, updateProgress: async () => {},
  } as unknown as Job<HomeworkEvaluationData>;
  for (const receipt of [{ success: false }, { success: true, data: { submissionId: 'other', status: 'Graded' } },
    { success: true, data: { submissionId: 'submission', status: 'InProgress' } }]) {
    globalThis.fetch = async () => Response.json(receipt);
    await assert.rejects(evaluateHomework(job), /did not confirm/);
  }
});

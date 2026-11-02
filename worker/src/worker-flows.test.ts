import { gradingRuntime } from './test-support/writtenGrading.js';
import { setAIServiceRuntimeFactoryForTests } from './services/geminiService.js';
import { test } from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { Redis } from 'ioredis';
import child_process from 'child_process';

let bunnyExecCalled = false;
let bunnyCapturedArgs: any[] = [];
let vkExecCalled = false;
let vkCapturedArgs: any[] = [];
const originalExecFile = child_process.execFile;

// Override child_process.execFile to mock Bunny Stream yt-dlp execution
child_process.execFile = function(file: any, args: any, options: any, callback?: any) {
  const cb = typeof options === 'function' ? options : callback;
  if (typeof file === 'string' && (file.includes('yt-dlp') || file.includes('youtube-dl')) && args.includes('--referer')) {
    bunnyExecCalled = true;
    bunnyCapturedArgs = args;
    
    // Find output path in args
    const oIndex = args.indexOf('-o');
    if (oIndex !== -1 && args[oIndex + 1]) {
      const outputPath = args[oIndex + 1] + '.mp3';
      fs.writeFileSync(outputPath, 'mock bunny audio content');
    }
    
    if (cb) cb(null, 'mock bunny download', '');
    return {} as any;
  }
  if (typeof file === 'string' && (file.includes('yt-dlp') || file.includes('youtube-dl')) && args[0]?.startsWith('https://vk.com/video')) {
    vkExecCalled = true;
    vkCapturedArgs = args;
    const outputIndex = args.indexOf('-o');
    fs.writeFileSync(`${args[outputIndex + 1]}.mp3`, 'mock vk audio content');
    if (cb) cb(null, 'mock vk download', '');
    return {} as any;
  }
  return originalExecFile(file, args, options, callback);
} as any;

import { markJobCancellation, throwIfCancellationRequested } from './cancellation.js';
import { processEvaluateEssayJob } from './jobs/evaluateEssay.js';
import { extractAudioFromVideo } from './utils/audioExtractor.js';

// Setup basic E2e test environment variables
process.env.GEMINI_API_KEY = 'mock_gemini_api_key_value_1234567890';
process.env.BACKEND_API_URL = 'http://localhost:5245/api/v1';
process.env.AI_CALLBACK_SECRET = 'E2eOnlyAiCallbackSecretValue1234567890';
process.env.AI_MEDIA_RELAY_SECRET = 'E2eOnlyAiMediaRelaySecretValue1234567890';

test('Job cancellation flow works correctly', async () => {
  const mockStore = new Map<string, string>();
  Redis.prototype.get = async (key: string) => mockStore.get(key) || null;
  Redis.prototype.set = async (key: string, val: string) => {
    mockStore.set(key, val);
    return 'OK';
  };

  const dummyJob: any = {
    id: 'test-job-id-123',
    getState: async () => 'active',
    data: {},
    updateData: async (data: any) => {
      dummyJob.data = data;
    }
  };

  const result = await markJobCancellation(dummyJob);
  assert.strictEqual(result.removed, false);
  assert.strictEqual(dummyJob.data.cancellationRequested, true);

  await assert.rejects(async () => {
    await throwIfCancellationRequested(dummyJob);
  }, /Job cancellation requested/);
});

test('processEvaluateEssayJob persists a calculated AI grade through its callback', async context => {
  const originalFetch = globalThis.fetch;
  context.after(() => { globalThis.fetch = originalFetch; setAIServiceRuntimeFactoryForTests(); });
  const provider = gradingRuntime(context, { met: 1, total: 1, answer: 'إجابة الطالب', feedback: 'إجابة رائعة يا بطل!' });
  setAIServiceRuntimeFactoryForTests(() => provider);
  let delivered: any;
  globalThis.fetch = async (_url, options) => {
    delivered = JSON.parse(String(options?.body));
    return Response.json({ success: true, data: { essaySubmissionId: 'sub-789', status: 'TeacherGraded' } });
  };
  const job: any = { id: 'essay-job-456', data: { essaySubmissionId: 'sub-789', questionText: 'ما المقصود بالتشبيه؟',
    answerText: 'إجابة الطالب', expectedAnswer: 'الإجابة النموذجية' },
    updateProgress: async () => {}, updateData: async (next: unknown) => { job.data = next; } };
  const grade = await processEvaluateEssayJob(job);
  assert.equal(grade.success, true);
  assert.equal(grade.score, 1);
  assert.equal(delivered.aiScore, 1);
  assert.equal(delivered.essaySubmissionId, 'sub-789');
  assert.ok(delivered.aiFeedback.endsWith('إجابة رائعة يا بطل!'));
});

test('processEvaluateEssayJob keeps failed callbacks retryable with a saved rubric grade', async context => {
  const originalFetch = globalThis.fetch;
  context.after(() => { globalThis.fetch = originalFetch; setAIServiceRuntimeFactoryForTests(); });
  const provider = gradingRuntime(context, { met: 0, total: 1, answer: 'wrong answer', feedback: 'محاولة جيدة ولكن غير صحيحة.' });
  setAIServiceRuntimeFactoryForTests(() => provider);
  globalThis.fetch = async () => new Response('Internal Server Error', { status: 500 });
  const job: any = { id: 'essay-job-retry', data: { essaySubmissionId: 'sub-retry', answerText: 'wrong answer',
    questionText: 'Question', expectedAnswer: 'Correct answer' },
    updateProgress: async () => {}, updateData: async (next: unknown) => { job.data = next; } };
  await assert.rejects(processEvaluateEssayJob(job), /Essay callback failed with status 500/);
  assert.equal(job.data.evaluation.score, 0);
});

test('extractAudioFromVideo refuses to route YouTube through third-party downloaders', async () => {
  bunnyExecCalled = false;
  await assert.rejects(
    extractAudioFromVideo('https://www.youtube.com/watch?v=AbCdEf12_-3', 'youtube-must-be-direct'),
    /YouTube/,
  );
  assert.equal(bunnyExecCalled, false);
});

test('extractAudioFromVideo detects Bunny Stream GUIDs and constructs the correct URL and referer', async () => {
  const oldLibraryId = process.env.BUNNY_STREAM_LIBRARY_ID;
  process.env.BUNNY_STREAM_LIBRARY_ID = '99999';
  let outputPath: string | undefined;

  bunnyExecCalled = false;
  bunnyCapturedArgs = [];

  try {
    const bunnyGuid = '12345678-abcd-1234-abcd-123456789abc';
    outputPath = await extractAudioFromVideo(bunnyGuid, 'mock_bunny_test');

    assert.ok(bunnyExecCalled, 'execFile should have been intercepted');
    assert.ok(outputPath.endsWith('mock_bunny_test.mp3'), 'should return expected mp3 path');
    assert.ok(fs.existsSync(outputPath), 'output file should exist');

    assert.strictEqual(bunnyCapturedArgs[0], `https://iframe.mediadelivery.net/embed/99999/${bunnyGuid}`);
    const refererIndex = bunnyCapturedArgs.indexOf('--referer');
    assert.ok(refererIndex !== -1, '--referer arg must be passed');
    assert.strictEqual(bunnyCapturedArgs[refererIndex + 1], 'https://admin.massar-academy.net/');

  } finally {
    if (outputPath) fs.rmSync(outputPath, { force: true });
    if (oldLibraryId === undefined) delete process.env.BUNNY_STREAM_LIBRARY_ID;
    else process.env.BUNNY_STREAM_LIBRARY_ID = oldLibraryId;
  }
});

test('extractAudioFromVideo uses the library scoped in a Bunny source instead of the legacy environment', async () => {
  const oldLibraryId = process.env.BUNNY_STREAM_LIBRARY_ID;
  process.env.BUNNY_STREAM_LIBRARY_ID = '99999';
  bunnyExecCalled = false;
  bunnyCapturedArgs = [];
  let outputPath: string | undefined;

  try {
    const bunnyGuid = '12345678-abcd-1234-abcd-123456789abc';
    outputPath = await extractAudioFromVideo(
      `740801/${bunnyGuid}`,
      'mock_bunny_scoped_library_test',
    );

    assert.equal(bunnyExecCalled, true);
    assert.equal(
      bunnyCapturedArgs[0],
      `https://iframe.mediadelivery.net/embed/740801/${bunnyGuid}`,
    );
    assert.equal(fs.existsSync(outputPath), true);
  } finally {
    if (outputPath) fs.rmSync(outputPath, { force: true });
    if (oldLibraryId === undefined) delete process.env.BUNNY_STREAM_LIBRARY_ID;
    else process.env.BUNNY_STREAM_LIBRARY_ID = oldLibraryId;
  }
});

test('extractAudioFromVideo normalizes the stored VK identifier before download', async () => {
  vkExecCalled = false;
  vkCapturedArgs = [];
  const result = await extractAudioFromVideo('oid=-22822305&id=456241864', 'mock-vk-stored-id');

  try {
    assert.equal(vkExecCalled, true);
    assert.equal(vkCapturedArgs[0], 'https://vk.com/video-22822305_456241864');
    assert.equal(fs.existsSync(result), true);
  } finally {
    fs.rmSync(result, { force: true });
  }
});

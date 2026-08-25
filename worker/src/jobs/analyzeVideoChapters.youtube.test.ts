import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import childProcess from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { Redis } from 'ioredis';

const testStorageRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'massar-youtube-analysis-'));
process.env.SHARED_STORAGE_ROOT = testStorageRoot;
process.env.SHARED_PUBLIC_ROOT = path.join(testStorageRoot, 'public');
process.env.SUBTITLE_STORAGE_PATH = path.join(testStorageRoot, 'public', 'subtitles');
process.env.AI_VIDEO_CHECKPOINT_STORAGE_PATH = path.join(testStorageRoot, 'private', 'checkpoints');
process.env.BACKEND_API_URL = 'http://backend.test';
process.env.API_CALLBACK_SECRET = 'test-callback-secret';

Redis.prototype.get = async () => null;
const originalExecFile = childProcess.execFile;
let externalProcessCalls = 0;
childProcess.execFile = (() => {
  externalProcessCalls += 1;
  throw new Error('external process must not run for public YouTube');
}) as unknown as typeof childProcess.execFile;

const geminiService = await import('../services/geminiService.js');
const checkpointService = await import('../services/aiVideoCheckpoint.js');
const { default: analyzeVideoProcessor } = await import('./analyzeVideoChapters.js');
const GENERATION_RUN_ID = '11111111-1111-4111-8111-111111111111';

afterEach(() => {
  geminiService.setAIServiceRuntimeFactoryForTests(undefined);
  externalProcessCalls = 0;
});

process.on('exit', () => {
  childProcess.execFile = originalExecFile;
  fs.rmSync(testStorageRoot, { recursive: true, force: true });
});

function fakeJob(sourceUrl: string, suffix: string) {
  const data: Record<string, unknown> = {
    lessonVideoId: `youtube-direct-${suffix}`,
    sourceUrl,
    outputLanguage: 'ar',
    generationRunId: GENERATION_RUN_ID,
  };
  return {
    id: `youtube-direct-${suffix}`,
    timestamp: 1_700_000_000_000,
    data,
    attemptsMade: 0,
    opts: { attempts: 5 },
    updateProgress: async () => {},
    updateData: async (updated: Record<string, unknown>) => {
      Object.assign(data, updated);
    },
  };
}

test('video processor bypasses downloaders for a public YouTube URL', async (testContext) => {
  const originalFetch = globalThis.fetch;
  const callbackUrls: string[] = [];
  const callbackBodies: Array<Record<string, unknown>> = [];
  globalThis.fetch = async (input, init) => {
    callbackUrls.push(String(input));
    if (init?.body) callbackBodies.push(JSON.parse(String(init.body)));
    return new Response('{"data":{"accepted":true}}', { status: 200 });
  };
  testContext.after(() => { globalThis.fetch = originalFetch; });

  const requests: any[] = [];
  const client = { models: { generateContent: async (request: any) => {
    requests.push(request);
    if (request.config.responseMimeType === 'text/plain') {
      return { text: '1\n00:00:00,000 --> 00:00:01,000\nمقدمة الدرس' };
    }
    return {
      text: '[{"title":"مقدمة","startTime":0,"endTime":1,"summaryText":"مقدمة الدرس","order":1}]',
    };
  } } };
  geminiService.setAIServiceRuntimeFactoryForTests(() => ({
    config: { primaryProvider: 'developer', developerApiKey: 'test', textModel: 'test-model', imageModel: 'test-image' },
    developer: client as never,
  }));

  const job = fakeJob('https://youtu.be/AbCdEf12_-3?feature=share', 'success');
  const result = await analyzeVideoProcessor(job as never);

  assert.deepEqual(result, { success: true, chaptersProcessed: 1 });
  assert.equal(externalProcessCalls, 0);
  assert.equal(job.data.audioPath, undefined);
  assert.equal(requests[0].contents[0].parts[0].fileData.fileUri, 'https://www.youtube.com/watch?v=AbCdEf12_-3');
  assert.equal(requests[0].contents[0].parts[0].fileData.mimeType, 'video/*');
  assert.equal(requests[0].contents[0].parts.some((part: any) => part.inlineData), false);
  assert.ok(callbackUrls.every((url) => url.startsWith('http://backend.test/')));
  assert.ok(callbackBodies.every((body) => body.generationRunId === GENERATION_RUN_ID));
  const completion = callbackBodies.find(body => typeof body.subtitleUrl === 'string');
  assert.equal(completion?.jobId, 'youtube-direct-success');
  assert.match(String(completion?.subtitleUrl), new RegExp(`_run_${GENERATION_RUN_ID}\\.srt$`));
});

test('legacy analysis jobs default to auto and omit the callback fence', async (testContext) => {
  const originalFetch = globalThis.fetch;
  const callbackBodies: Array<Record<string, unknown>> = [];
  globalThis.fetch = async (_input, init) => {
    if (init?.body) callbackBodies.push(JSON.parse(String(init.body)));
    return new Response('{}', { status: 200 });
  };
  testContext.after(() => { globalThis.fetch = originalFetch; });

  const client = { models: { generateContent: async (request: any) => request.config.responseMimeType === 'text/plain'
    ? { text: '1\n00:00:00,000 --> 00:00:01,000\nLegacy lesson' }
    : { text: '[{"title":"Legacy lesson","startTime":0,"endTime":1,"summaryText":"In this part, we review the legacy lesson.","order":1}]' } } };
  geminiService.setAIServiceRuntimeFactoryForTests(() => ({
    config: { primaryProvider: 'developer', developerApiKey: 'test', textModel: 'test-model', imageModel: 'test-image' },
    developer: client as never,
  }));

  const job = fakeJob('https://youtu.be/AbCdEf12_-3', 'legacy');
  delete job.data.outputLanguage;
  delete job.data.generationRunId;
  await analyzeVideoProcessor(job as never);

  assert.ok(callbackBodies.every(body => !Object.hasOwn(body, 'generationRunId')));
  const completion = callbackBodies.find(body => typeof body.subtitleUrl === 'string');
  assert.equal(completion?.jobId, 'youtube-direct-legacy');
  assert.match(String(completion?.subtitleUrl), /_run_legacy-[0-9a-f]{32}\.srt$/);
});

test('a final ambiguous completion callback failure preserves the current run SRT', async (testContext) => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (input) => {
    if (String(input).includes('/ai-analysis-completed')) {
      throw new TypeError('connection closed after request upload');
    }
    return new Response('{}', { status: 200 });
  };
  testContext.after(() => { globalThis.fetch = originalFetch; });

  const client = { models: { generateContent: async (request: any) => request.config.responseMimeType === 'text/plain'
    ? { text: '1\n00:00:00,000 --> 00:00:01,000\nFinal callback failure' }
    : { text: '[{"title":"Final callback failure","startTime":0,"endTime":1,"summaryText":"In this part, we review the final callback failure safely.","order":1}]' } } };
  geminiService.setAIServiceRuntimeFactoryForTests(() => ({
    config: { primaryProvider: 'developer', developerApiKey: 'test', textModel: 'test-model', imageModel: 'test-image' },
    developer: client as never,
  }));

  const job = fakeJob('https://youtu.be/AbCdEf12_-3', 'final-callback');
  job.data.outputLanguage = 'en';
  job.attemptsMade = 4;
  job.opts = { attempts: 5 };

  await assert.rejects(
    analyzeVideoProcessor(job as never),
    (error: unknown) => error instanceof Error && error.name === 'WorkerExternalError',
  );
  assert.equal(
    fs.existsSync(path.join(
      process.env.SUBTITLE_STORAGE_PATH!,
      `youtube-direct-final-callback_run_${GENERATION_RUN_ID}.srt`,
    )),
    true,
  );
});

test('permanent YouTube rejection stops queue retries with a sanitized reason', async (testContext) => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => new Response('{}', { status: 200 });
  testContext.after(() => { globalThis.fetch = originalFetch; });

  const client = { models: { generateContent: async () => {
    throw { status: 400, name: 'SENSITIVE_PROVIDER_SENTINEL' };
  } } };
  geminiService.setAIServiceRuntimeFactoryForTests(() => ({
    config: { primaryProvider: 'developer', developerApiKey: 'test', textModel: 'test-model', imageModel: 'test-image' },
    developer: client as never,
  }));

  const job = fakeJob('https://www.youtube.com/watch?v=AbCdEf12_-3', 'permanent');
  await assert.rejects(
    analyzeVideoProcessor(job as never),
    (error: unknown) => error instanceof Error
      && error.name === 'UnrecoverableError'
      && !error.message.includes('SENSITIVE_PROVIDER_SENTINEL')
      && !error.message.includes('AbCdEf12_-3'),
  );
  assert.equal(externalProcessCalls, 0);
});

test('unexpected analysis defects are terminal and never leak raw details', async (testContext) => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => new Response('{}', { status: 200 });
  testContext.after(() => { globalThis.fetch = originalFetch; });

  const client = { models: { generateContent: async (request: any) => request.config.responseMimeType === 'text/plain'
    ? { text: '1\n00:00:00,000 --> 00:00:01,000\nمقدمة الدرس' }
    : { text: 'SENSITIVE_MALFORMED_PROVIDER_OUTPUT' } } };
  geminiService.setAIServiceRuntimeFactoryForTests(() => ({
    config: { primaryProvider: 'developer', developerApiKey: 'test', textModel: 'test-model', imageModel: 'test-image' },
    developer: client as never,
  }));

  const job = fakeJob('https://www.youtube.com/watch?v=AbCdEf12_-3', 'unexpected');
  await assert.rejects(
    analyzeVideoProcessor(job as never),
    (error: unknown) => error instanceof Error
      && error.name === 'UnrecoverableError'
      && !error.message.includes('SENSITIVE_MALFORMED_PROVIDER_OUTPUT'),
  );
  assert.equal(externalProcessCalls, 0);
  const checkpoint = checkpointService.createVideoAnalysisCheckpoint(
    'youtube-direct-unexpected',
    'https://www.youtube.com/watch?v=AbCdEf12_-3',
    'ar',
    GENERATION_RUN_ID,
  );
  assert.equal(checkpoint.transcription(), undefined);
});

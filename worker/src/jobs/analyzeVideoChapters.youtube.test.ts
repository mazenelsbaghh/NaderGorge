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
const { default: analyzeVideoProcessor } = await import('./analyzeVideoChapters.js');

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
  };
  return {
    id: `youtube-direct-${suffix}`,
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
  globalThis.fetch = async (input) => {
    callbackUrls.push(String(input));
    return new Response('{}', { status: 200 });
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
});

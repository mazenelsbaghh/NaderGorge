import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'fs';
import path from 'path';
import { AIProviderGateway } from './aiProvider.js';
import type { AIConfig } from './aiConfig.js';
import { analyzeVideoChapters, evaluateEssayWithAI, generateChapterMindmap, setAIServiceRuntimeFactoryForTests } from './geminiService.js';

const vertexConfig: AIConfig = {
  primaryProvider: 'vertex', project: 'p', location: 'global', temporaryBucket: 'bucket',
  temporaryPrefix: 'ai-analysis', textModel: 'text-model', imageModel: 'image-model', fallbackApiKey: 'fallback',
};

afterEach(() => setAIServiceRuntimeFactoryForTests(undefined));

test('video chapter service reuses one GCS URI and deletes the object', async () => {
  const requests: any[] = [];
  const deleted: any[] = [];
  const vertex = {
    models: { generateContent: async (request: any) => {
      requests.push(request);
      return requests.length === 1
        ? { text: '1\n00:00:00,000 --> 00:00:01,000\nنص' }
        : { text: '[{"title":"فصل","startTime":0,"endTime":1,"summaryText":"ملخص","order":1}]' };
    } },
    files: {},
  };
  const developer = { models: { generateContent: async () => { throw new Error('unused'); } }, files: {} };
  setAIServiceRuntimeFactoryForTests(() => ({
    config: vertexConfig,
    gateway: new AIProviderGateway(vertexConfig),
    vertex: vertex as any,
    developer: developer as any,
    temporaryStorage: {
      upload: async () => ({ uri: 'gs://bucket/opaque.mp3', objectName: 'opaque.mp3', generation: '1' }),
      delete: async (value: any) => { deleted.push(value); },
    } as any,
  }));

  const result = await analyzeVideoChapters('/tmp/audio.mp3', 'job-id');
  assert.equal(result.chapters.length, 1);
  assert.equal(requests[0].contents[0].parts[0].fileData.fileUri, 'gs://bucket/opaque.mp3');
  assert.equal(requests[1].contents[0].parts[0].fileData.fileUri, 'gs://bucket/opaque.mp3');
  assert.equal(deleted.length, 1);
});

test('video chapter quota fallback uploads through Developer File API and cleans both stores', async () => {
  let vertexCalls = 0;
  let developerUploads = 0;
  let developerDeletes = 0;
  let gcsDeletes = 0;
  const vertex = { models: { generateContent: async () => {
    vertexCalls++;
    if (vertexCalls === 1) throw { status: 429 };
    return { text: '[{"title":"فصل","startTime":0,"endTime":1,"summaryText":"ملخص","order":1}]' };
  } }, files: {} };
  const developer = {
    models: { generateContent: async () => ({ text: '1\n00:00:00,000 --> 00:00:01,000\nنص' }) },
    files: {
      upload: async () => { developerUploads++; return { uri: 'https://developer/file', name: 'files/1', mimeType: 'audio/mpeg' }; },
      delete: async () => { developerDeletes++; },
    },
  };
  setAIServiceRuntimeFactoryForTests(() => ({
    config: vertexConfig, gateway: new AIProviderGateway(vertexConfig), vertex: vertex as any, developer: developer as any,
    developerUploadDelayMs: 0,
    temporaryStorage: { upload: async () => ({ uri: 'gs://bucket/opaque.mp3', objectName: 'opaque.mp3' }), delete: async () => { gcsDeletes++; } } as any,
  }));
  await analyzeVideoChapters('/tmp/audio.mp3', 'job-id');
  assert.equal(developerUploads, 1);
  assert.equal(developerDeletes, 1);
  assert.equal(gcsDeletes, 1);
});

test('video chapter non-quota failure does not call fallback and still deletes GCS audio', async () => {
  let developerCalls = 0;
  let gcsDeletes = 0;
  const vertex = { models: { generateContent: async () => { throw { status: 403 }; } }, files: {} };
  const developer = { models: { generateContent: async () => { developerCalls++; return { text: 'unused' }; } }, files: {} };
  setAIServiceRuntimeFactoryForTests(() => ({
    config: vertexConfig, gateway: new AIProviderGateway(vertexConfig), vertex: vertex as any, developer: developer as any,
    temporaryStorage: { upload: async () => ({ uri: 'gs://bucket/opaque.mp3', objectName: 'opaque.mp3' }), delete: async () => { gcsDeletes++; } } as any,
  }));
  await assert.rejects(analyzeVideoChapters('/tmp/audio.mp3', 'job-id'));
  assert.equal(developerCalls, 0);
  assert.equal(gcsDeletes, 1);
});

test('essay evaluation validates and returns the existing structured result', async () => {
  const client = { models: { generateContent: async () => ({ text: '{"isCorrect":true,"feedback":"برافو عليك"}' }) }, files: {} };
  setAIServiceRuntimeFactoryForTests(() => ({
    config: vertexConfig, gateway: new AIProviderGateway(vertexConfig), vertex: client as any, developer: client as any,
    temporaryStorage: {} as any,
  }));
  assert.deepEqual(await evaluateEssayWithAI('إجابة', 'نموذج'), { isCorrect: true, feedback: 'برافو عليك' });
});

test('essay quota exhaustion uses the configured fallback once', async () => {
  let fallbackCalls = 0;
  const vertex = { models: { generateContent: async () => { throw { status: 429 }; } }, files: {} };
  const developer = { models: { generateContent: async () => { fallbackCalls++; return { text: '{"isCorrect":false,"feedback":"حاول تاني"}' }; } }, files: {} };
  setAIServiceRuntimeFactoryForTests(() => ({
    config: vertexConfig, gateway: new AIProviderGateway(vertexConfig), vertex: vertex as any, developer: developer as any,
  }));
  assert.deepEqual(await evaluateEssayWithAI('إجابة'), { isCorrect: false, feedback: 'حاول تاني' });
  assert.equal(fallbackCalls, 1);
});

test('mindmap keeps the teacher image first and writes a compatible PNG URL', async (testContext) => {
  const photo = path.join(process.cwd(), '.tmp', 'teacher-test.png');
  fs.mkdirSync(path.dirname(photo), { recursive: true });
  fs.writeFileSync(photo, 'photo');
  testContext.after(() => fs.rmSync(photo, { force: true }));
  let request: any;
  const client = { models: { generateContent: async (value: any) => {
    request = value;
    return { candidates: [{ content: { parts: [{ inlineData: { data: Buffer.from('png').toString('base64') } }] } }] };
  } }, files: {} };
  setAIServiceRuntimeFactoryForTests(() => ({
    config: vertexConfig, gateway: new AIProviderGateway(vertexConfig), vertex: client as any, developer: client as any,
    temporaryStorage: {} as any,
  }));
  const url = await generateChapterMindmap({ title: 'فصل', summaryText: 'ملخص', order: 1 }, 'test-video', photo);
  assert.ok(request.contents[0].parts[0].inlineData);
  assert.equal(request.config.aspectRatio, '16:9');
  assert.match(url || '', /^\/mindmaps\/test-video_chapter_1_/);
  if (url) {
    const generatedImage = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot', url.slice(1));
    testContext.after(() => fs.rmSync(generatedImage, { force: true }));
  }
});

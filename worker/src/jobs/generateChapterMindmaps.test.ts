import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'fs';
import path from 'path';
import type { Job } from 'bullmq';
import { Redis } from 'ioredis';
import { findReusableMindmapUrl, generateMindmapsProcessor, type GenerateMindmapsJobData } from './generateChapterMindmaps.js';
import type { AIConfig } from '../services/aiConfig.js';
import { generateChapterMindmap, setAIServiceRuntimeFactoryForTests } from '../services/geminiService.js';

const developerConfig: AIConfig = {
  primaryProvider: 'developer', developerApiKey: 'test-key', textModel: 'text-model', imageModel: 'image-model',
};

const GENERATION_RUN_ID = '11111111-1111-4111-8111-111111111111';
const ONE_PIXEL_PNG = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';

function disableCancellationRedis(testContext: { after(callback: () => void): void }) {
  const originalGet = Redis.prototype.get;
  Redis.prototype.get = async () => null;
  testContext.after(() => { Redis.prototype.get = originalGet; });
}

test('2026-06-20 partial chapter generation does not publish an incomplete batch', async (testContext) => {
  disableCancellationRedis(testContext);
  const originalFetch = globalThis.fetch;
  const callbackUrls: string[] = [];
  let generatedChapterCount = 0;
  const client = { models: { generateContent: async (request: any) => {
    if (request.config.responseMimeType === 'application/json') {
      return { text: '{"arabicLetterCount":12,"latinLetterCount":0,"hasIllegibleText":false}' };
    }
    generatedChapterCount++;
    return generatedChapterCount === 1
      ? { candidates: [{ content: { parts: [{ inlineData: { data: ONE_PIXEL_PNG } }] } }] }
      : { candidates: [{ content: { parts: [] } }] };
  } } };

  setAIServiceRuntimeFactoryForTests(() => ({
    config: developerConfig, developer: client as any,
  }));
  globalThis.fetch = async (url) => {
    callbackUrls.push(String(url));
    return { ok: true, status: 200, text: async () => '' } as Response;
  };

  testContext.after(() => {
    globalThis.fetch = originalFetch;
    setAIServiceRuntimeFactoryForTests(undefined);
    const mindmapsDirectory = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
    for (const file of fs.readdirSync(mindmapsDirectory)) {
      if (file.startsWith(`batch-regression-video_run_${GENERATION_RUN_ID}_chapter_1_`)) {
        fs.rmSync(path.join(mindmapsDirectory, file), { force: true });
      }
    }
  });

  const job = {
    id: 'batch-regression-job',
    timestamp: 1_700_000_000_000,
    attemptsMade: 0,
    opts: { attempts: 2 },
    data: {
      lessonVideoId: 'batch-regression-video',
      outputLanguage: 'ar',
      generationRunId: GENERATION_RUN_ID,
      chapters: [
        {
          chapterId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
          title: 'الفصل الأول',
          summaryText: 'ملخص أول',
          order: 1,
        },
        {
          chapterId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
          title: 'الفصل الثاني',
          summaryText: 'ملخص ثان',
          order: 2,
        },
      ],
    },
    updateProgress: async () => undefined,
  } as unknown as Job<GenerateMindmapsJobData>;

  await assert.rejects(generateMindmapsProcessor(job), /returned no image/);
  assert.equal(callbackUrls.some((url) => url.includes('/mindmaps-completed')), false);
});

test('mindmap reuse is isolated to the same generation run', (testContext) => {
  const directory = path.join(process.cwd(), '.tmp', `mindmap-reuse-${Date.now()}`);
  const oldRunId = '11111111-1111-4111-8111-111111111111';
  const currentRunId = '22222222-2222-4222-8222-222222222222';
  const videoId = 'run-fenced-video';
  fs.mkdirSync(directory, { recursive: true });
  testContext.after(() => fs.rmSync(directory, { recursive: true, force: true }));

  fs.writeFileSync(path.join(directory, `${videoId}_run_${oldRunId}_chapter_1_100.webp`), 'old');
  assert.equal(findReusableMindmapUrl(directory, videoId, 1, currentRunId), undefined);

  const currentArtifact = `${videoId}_run_${currentRunId}_chapter_1_200.webp`;
  fs.writeFileSync(path.join(directory, currentArtifact), 'current');
  assert.equal(
    findReusableMindmapUrl(directory, videoId, 1, currentRunId),
    `/mindmaps/${currentArtifact}`,
  );
});

test('mindmap reuse ignores a symlink that has a valid same-run artifact name', (testContext) => {
  const directory = path.join(process.cwd(), '.tmp', `mindmap-symlink-${Date.now()}`);
  const videoId = 'symlink-fenced-video';
  const artifact = `${videoId}_run_${GENERATION_RUN_ID}_chapter_1_200.webp`;
  const externalTarget = path.join(directory, 'external.webp');
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(externalTarget, 'external');
  fs.symlinkSync(externalTarget, path.join(directory, artifact));
  testContext.after(() => fs.rmSync(directory, { recursive: true, force: true }));

  assert.equal(
    findReusableMindmapUrl(directory, videoId, 1, GENERATION_RUN_ID),
    undefined,
  );
});

test('a final ambiguous batch callback preserves artifacts that the backend may reference', async (testContext) => {
  disableCancellationRedis(testContext);
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (input) => {
    if (String(input).includes('/mindmaps-completed')) {
      throw new TypeError('connection closed after request upload');
    }
    return new Response('{}', { status: 200 });
  };
  const videoId = 'ambiguous-mindmap-callback';
  const mindmapsDirectory = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
  const artifactName = `${videoId}_run_${GENERATION_RUN_ID}_chapter_1_200.webp`;
  const artifactPath = path.join(mindmapsDirectory, artifactName);
  fs.mkdirSync(mindmapsDirectory, { recursive: true });
  fs.writeFileSync(artifactPath, 'current artifact');
  testContext.after(() => {
    globalThis.fetch = originalFetch;
    fs.rmSync(artifactPath, { force: true });
  });
  const job = {
    id: 'ambiguous-mindmap-callback-job',
    timestamp: 1_700_000_000_000,
    attemptsMade: 1,
    opts: { attempts: 2 },
    data: {
      lessonVideoId: videoId,
      generationRunId: GENERATION_RUN_ID,
      chapters: [{
        chapterId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
        title: 'Chapter',
        summaryText: 'Summary',
        order: 1,
      }],
    },
    updateProgress: async () => undefined,
  } as unknown as Job<GenerateMindmapsJobData>;

  await assert.rejects(
    generateMindmapsProcessor(job),
    (error: unknown) => error instanceof Error && error.name === 'WorkerExternalError',
  );
  assert.equal(fs.existsSync(artifactPath), true);
});

test('an explicit stale batch receipt removes only the rejected run artifact', async (testContext) => {
  disableCancellationRedis(testContext);
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (input) => String(input).includes('/mindmaps-completed')
    ? new Response('{"data":{"accepted":false}}', { status: 200 })
    : new Response('{}', { status: 200 });
  const videoId = 'stale-mindmap-callback';
  const mindmapsDirectory = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
  const currentArtifact = `${videoId}_run_${GENERATION_RUN_ID}_chapter_1_200.webp`;
  const retainedRunId = '22222222-2222-4222-8222-222222222222';
  const retainedArtifact = `${videoId}_run_${retainedRunId}_chapter_1_100.webp`;
  fs.mkdirSync(mindmapsDirectory, { recursive: true });
  fs.writeFileSync(path.join(mindmapsDirectory, currentArtifact), 'stale artifact');
  fs.writeFileSync(path.join(mindmapsDirectory, retainedArtifact), 'retained artifact');
  testContext.after(() => {
    globalThis.fetch = originalFetch;
    fs.rmSync(path.join(mindmapsDirectory, currentArtifact), { force: true });
    fs.rmSync(path.join(mindmapsDirectory, retainedArtifact), { force: true });
  });
  const job = {
    id: 'stale-mindmap-callback-job',
    timestamp: 1_700_000_000_000,
    attemptsMade: 0,
    opts: { attempts: 2 },
    data: {
      lessonVideoId: videoId,
      generationRunId: GENERATION_RUN_ID,
      chapters: [{
        chapterId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
        title: 'Chapter',
        summaryText: 'Summary',
        order: 1,
      }],
    },
    updateProgress: async () => undefined,
  } as unknown as Job<GenerateMindmapsJobData>;

  const result = await generateMindmapsProcessor(job);

  assert.deepEqual(result, { success: true, mindmapsGenerated: 1 });
  assert.equal(fs.existsSync(path.join(mindmapsDirectory, currentArtifact)), false);
  assert.equal(fs.existsSync(path.join(mindmapsDirectory, retainedArtifact)), true);
});

test('an empty chapter payload fails terminally without publishing completion', async (testContext) => {
  disableCancellationRedis(testContext);
  const originalFetch = globalThis.fetch;
  const callbackUrls: string[] = [];
  globalThis.fetch = async (input) => {
    callbackUrls.push(String(input));
    return new Response('{"data":{"accepted":true}}', {
      status: 200,
      headers: { 'content-type': 'application/json' },
    });
  };
  testContext.after(() => { globalThis.fetch = originalFetch; });

  const job = {
    id: 'empty-mindmap-job',
    timestamp: 1_700_000_000_000,
    attemptsMade: 0,
    opts: { attempts: 3 },
    data: {
      lessonVideoId: 'empty-mindmap-video',
      generationRunId: GENERATION_RUN_ID,
      chapters: [],
    },
    updateProgress: async () => undefined,
  } as unknown as Job<GenerateMindmapsJobData>;

  await assert.rejects(
    generateMindmapsProcessor(job),
    (error: unknown) => error instanceof Error && error.name === 'UnrecoverableError',
  );
  assert.equal(callbackUrls.some(url => url.includes('/mindmaps-completed')), false);
  assert.equal(callbackUrls.some(url => url.includes('/single-mindmap-completed')), false);
});

test('fenced batch callbacks preserve chapter identity and deterministic order with duplicate titles', async (testContext) => {
  disableCancellationRedis(testContext);
  const originalFetch = globalThis.fetch;
  const callbackBodies: Array<Record<string, unknown>> = [];
  globalThis.fetch = async (_input, init) => {
    if (init?.body) callbackBodies.push(JSON.parse(String(init.body)));
    return new Response('{"data":{"accepted":true}}', {
      status: 200,
      headers: { 'content-type': 'application/json' },
    });
  };
  const videoId = 'stable-chapter-identity';
  const firstChapterId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
  const secondChapterId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
  const client = { models: { generateContent: async () => ({
    candidates: [{ content: { parts: [{ inlineData: { data: ONE_PIXEL_PNG, mimeType: 'image/png' } }] } }],
  }) } };
  setAIServiceRuntimeFactoryForTests(() => ({ config: developerConfig, developer: client as any }));
  testContext.after(() => {
    globalThis.fetch = originalFetch;
    setAIServiceRuntimeFactoryForTests(undefined);
    const directory = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
    if (!fs.existsSync(directory)) return;
    for (const fileName of fs.readdirSync(directory)) {
      if (fileName.startsWith(`${videoId}_run_${GENERATION_RUN_ID}_`)) {
        fs.rmSync(path.join(directory, fileName), { force: true });
      }
    }
  });

  const job = {
    id: 'stable-chapter-identity-job',
    timestamp: 1_700_000_000_000,
    attemptsMade: 0,
    opts: { attempts: 2 },
    data: {
      lessonVideoId: videoId,
      generationRunId: GENERATION_RUN_ID,
      chapters: [
        { chapterId: secondChapterId, title: 'Repeated title', summaryText: 'Second', order: 2 },
        { chapterId: firstChapterId, title: 'Repeated title', summaryText: 'First', order: 1 },
      ],
    },
    updateProgress: async () => undefined,
  } as unknown as Job<GenerateMindmapsJobData>;

  await generateMindmapsProcessor(job);

  const completion = callbackBodies.find(body => Array.isArray(body.mindmaps));
  const mindmaps = completion?.mindmaps as Array<{
    chapterId: string;
    title: string;
    imageUrl: string;
    order: number;
  }>;
  assert.deepEqual(mindmaps.map(({ chapterId, title, order }) => ({ chapterId, title, order })), [
    { chapterId: firstChapterId, title: 'Repeated title', order: 1 },
    { chapterId: secondChapterId, title: 'Repeated title', order: 2 },
  ]);
  assert.ok(mindmaps.every(mindmap => mindmap.imageUrl.startsWith('/mindmaps/')));
});

test('legacy mindmap jobs use a deterministic artifact run and omit callback fencing', async (testContext) => {
  disableCancellationRedis(testContext);
  const originalFetch = globalThis.fetch;
  const callbackBodies: Array<Record<string, unknown>> = [];
  globalThis.fetch = async (_input, init) => {
    if (init?.body) callbackBodies.push(JSON.parse(String(init.body)));
    return new Response('{"data":{"accepted":true}}', {
      status: 200,
      headers: { 'content-type': 'application/json' },
    });
  };
  const videoId = 'legacy-mindmap-artifact';
  const client = { models: { generateContent: async () => ({
    candidates: [{ content: { parts: [{ inlineData: { data: ONE_PIXEL_PNG, mimeType: 'image/png' } }] } }],
  }) } };
  setAIServiceRuntimeFactoryForTests(() => ({ config: developerConfig, developer: client as any }));
  testContext.after(() => {
    globalThis.fetch = originalFetch;
    setAIServiceRuntimeFactoryForTests(undefined);
    const directory = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
    if (!fs.existsSync(directory)) return;
    for (const fileName of fs.readdirSync(directory)) {
      if (fileName.startsWith(`${videoId}_run_legacy-`)) {
        fs.rmSync(path.join(directory, fileName), { force: true });
      }
    }
  });

  const job = {
    id: 'legacy-mindmap-physical-job',
    timestamp: 1_700_000_000_000,
    attemptsMade: 0,
    opts: { attempts: 2 },
    data: {
      lessonVideoId: videoId,
      chapters: [{ title: 'Cell structure', summaryText: 'A short lesson summary.', order: 1 }],
    },
    updateProgress: async () => undefined,
  } as unknown as Job<GenerateMindmapsJobData>;

  const result = await generateMindmapsProcessor(job);
  const completion = callbackBodies.find(body => Array.isArray(body.mindmaps));
  const mindmaps = completion?.mindmaps as Array<{ imageUrl: string }> | undefined;

  assert.deepEqual(result, { success: true, mindmapsGenerated: 1 });
  assert.match(mindmaps?.[0]?.imageUrl ?? '', /_run_legacy-[0-9a-f]{32}_chapter_1_/);
  assert.ok(callbackBodies.every(body => !Object.hasOwn(body, 'generationRunId')));
});

test('explicit image language rejects a wrong-script image before it is persisted', async (testContext) => {
  const videoId = 'wrong-script-image';
  const mindmapsDirectory = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
  const artifactPrefix = `${videoId}_run_${GENERATION_RUN_ID}_chapter_1_`;
  const clearArtifacts = () => {
    if (!fs.existsSync(mindmapsDirectory)) return;
    for (const file of fs.readdirSync(mindmapsDirectory)) {
      if (file.startsWith(artifactPrefix)) fs.rmSync(path.join(mindmapsDirectory, file), { force: true });
    }
  };
  clearArtifacts();
  testContext.after(() => {
    clearArtifacts();
    setAIServiceRuntimeFactoryForTests(undefined);
  });
  const client = { models: { generateContent: async (request: any) => {
    if (request.config.responseMimeType === 'application/json') {
      return { text: '{"arabicLetterCount":14,"latinLetterCount":0,"hasIllegibleText":false}' };
    }
    return { candidates: [{ content: { parts: [{ inlineData: { data: ONE_PIXEL_PNG, mimeType: 'image/png' } }] } }] };
  } } };
  setAIServiceRuntimeFactoryForTests(() => ({ config: developerConfig, developer: client as any }));

  await assert.rejects(
    generateChapterMindmap(
      { title: 'Cell Structure', summaryText: 'We will learn the structure of a cell.', order: 1 },
      videoId,
      undefined,
      { outputLanguage: 'en', generationRunId: GENERATION_RUN_ID },
    ),
    /لغة النص المطلوبة/,
  );
  assert.equal(
    fs.existsSync(mindmapsDirectory)
      && fs.readdirSync(mindmapsDirectory).some(file => file.startsWith(artifactPrefix)),
    false,
  );
});

test('automatic image language skips the extra vision verification request', async (testContext) => {
  let providerRequests = 0;
  const videoId = 'auto-image-language';
  const client = { models: { generateContent: async () => {
    providerRequests += 1;
    return { candidates: [{ content: { parts: [{ inlineData: { data: ONE_PIXEL_PNG, mimeType: 'image/png' } }] } }] };
  } } };
  setAIServiceRuntimeFactoryForTests(() => ({ config: developerConfig, developer: client as any }));
  testContext.after(() => {
    setAIServiceRuntimeFactoryForTests(undefined);
    const mindmapsDirectory = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
    if (!fs.existsSync(mindmapsDirectory)) return;
    for (const file of fs.readdirSync(mindmapsDirectory)) {
      if (file.startsWith(`${videoId}_run_${GENERATION_RUN_ID}_chapter_1_`)) {
        fs.rmSync(path.join(mindmapsDirectory, file), { force: true });
      }
    }
  });

  const imageUrl = await generateChapterMindmap(
    { title: 'الفصل', summaryText: 'ملخص الفصل', order: 1 },
    videoId,
    undefined,
    { outputLanguage: 'auto', generationRunId: GENERATION_RUN_ID },
  );

  assert.match(imageUrl, /\/mindmaps\/auto-image-language_run_/);
  assert.equal(providerRequests, 1);
});

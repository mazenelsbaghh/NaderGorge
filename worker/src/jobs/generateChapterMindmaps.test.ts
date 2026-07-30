import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'fs';
import path from 'path';
import type { Job } from 'bullmq';
import { generateMindmapsProcessor, type GenerateMindmapsJobData } from './generateChapterMindmaps.js';
import type { AIConfig } from '../services/aiConfig.js';
import { setAIServiceRuntimeFactoryForTests } from '../services/geminiService.js';

const developerConfig: AIConfig = {
  primaryProvider: 'developer', developerApiKey: 'test-key', textModel: 'text-model', imageModel: 'image-model',
};

test('2026-06-20 partial chapter generation does not publish an incomplete batch', async (testContext) => {
  const originalFetch = globalThis.fetch;
  const callbackUrls: string[] = [];
  let generatedChapterCount = 0;
  const client = { models: { generateContent: async () => {
    generatedChapterCount++;
    return generatedChapterCount === 1
      ? { candidates: [{ content: { parts: [{ inlineData: { data: 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==' } }] } }] }
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
      if (file.startsWith('batch-regression-video_chapter_1_')) {
        fs.rmSync(path.join(mindmapsDirectory, file), { force: true });
      }
    }
  });

  const job = {
    data: {
      lessonVideoId: 'batch-regression-video',
      chapters: [
        { title: 'الفصل الأول', summaryText: 'ملخص أول', order: 1 },
        { title: 'الفصل الثاني', summaryText: 'ملخص ثان', order: 2 },
      ],
    },
    updateProgress: async () => undefined,
  } as unknown as Job<GenerateMindmapsJobData>;

  await assert.rejects(generateMindmapsProcessor(job), /returned no image/);
  assert.equal(callbackUrls.some((url) => url.includes('/mindmaps-completed')), false);
});

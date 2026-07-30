import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'node:child_process';
import type { AIConfig } from './aiConfig.js';
import { analyzeVideoChapters, evaluateEssayWithAI, generateChapterMindmap, generateLiveSupportReply, setAIServiceRuntimeFactoryForTests } from './geminiService.js';
import type { LiveSupportAgentPrompt } from './liveSupportAgent.js';

const developerConfig: AIConfig = {
  primaryProvider: 'developer', developerApiKey: 'test-key', textModel: 'text-model', imageModel: 'image-model',
};

function runtime(client: any) {
  return { config: developerConfig, developer: client as any };
}

afterEach(() => setAIServiceRuntimeFactoryForTests(undefined));

test('video analysis sends compressed audio inline and never calls the Files API', async (testContext) => {
  const audioPath = path.join(process.cwd(), '.tmp', 'inline-audio-test.wav');
  fs.mkdirSync(path.dirname(audioPath), { recursive: true });
  execFileSync('ffmpeg', ['-y', '-f', 'lavfi', '-i', 'anullsrc=r=16000:cl=mono', '-t', '0.1', audioPath], { stdio: 'ignore' });
  testContext.after(() => fs.rmSync(audioPath, { force: true }));
  const requests: any[] = [];
  const client = { models: { generateContent: async (request: any) => {
    requests.push(request);
    return requests.length === 1
      ? { text: '1\\n00:00:00,000 --> 00:00:00,100\\nنص' }
      : { text: '[{"title":"فصل","startTime":0,"endTime":1,"summaryText":"ملخص","order":1}]' };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  const result = await analyzeVideoChapters(audioPath);
  assert.equal(result.chapters.length, 1);
  for (const request of requests) {
    const audio = request.contents[0].parts[0].inlineData;
    assert.equal(audio.mimeType, 'audio/mpeg');
    assert.ok(audio.data.length > 0);
    assert.equal(request.contents[0].parts.some((part: any) => part.fileData), false);
  }
});

test('essay evaluation validates and returns the existing structured result', async () => {
  const client = { models: { generateContent: async () => ({ text: '{"isCorrect":true,"feedback":"برافو عليك"}' }) } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));
  assert.deepEqual(await evaluateEssayWithAI('إجابة', 'نموذج'), { isCorrect: true, feedback: 'برافو عليك' });
});

test('live support returns a valid Developer API decision', async () => {
  const client = { models: { generateContent: async () => ({ text: '{"schemaVersion":"1","type":"reply","messageAr":"تمام"}' }) } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));
  const prompt: LiveSupportAgentPrompt = {
    systemInstruction: 'ساعد بأمان', contents: [{ role: 'user', parts: [{ text: 'مساعدة' }] }],
    deadlineAt: new Date(Date.now() + 5_000).toISOString(),
  };
  const result = await generateLiveSupportReply(prompt);
  assert.equal(result.provider, 'developer');
});

test('mindmap provider response without an image fails instead of returning partial success', async () => {
  const client = { models: { generateContent: async () => ({ candidates: [{ content: { parts: [] } }] }) } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));
  await assert.rejects(
    generateChapterMindmap({ title: 'فصل بلا صورة', summaryText: 'ملخص', order: 2 }, 'test-video'),
    /returned no image/,
  );
});

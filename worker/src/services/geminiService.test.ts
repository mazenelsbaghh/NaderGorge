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

test('video analysis grounds chapter language in the verbatim transcript (2026-08-10 regression)', async (testContext) => {
  const audioPath = path.join(process.cwd(), '.tmp', 'inline-audio-test.wav');
  fs.mkdirSync(path.dirname(audioPath), { recursive: true });
  execFileSync('ffmpeg', ['-y', '-f', 'lavfi', '-i', 'anullsrc=r=16000:cl=mono', '-t', '5', audioPath], { stdio: 'ignore' });
  testContext.after(() => fs.rmSync(audioPath, { force: true }));
  const transcribedSrt = '1\n00:00:00,000 --> 00:00:01,000\nToday we will study the past simple.';
  const requests: any[] = [];
  const client = { models: { generateContent: async (request: any) => {
    requests.push(request);
    return request.config.responseMimeType === 'text/plain'
      ? { text: transcribedSrt }
      : { text: '[{"title":"Past Simple","startTime":0,"endTime":1,"summaryText":"Today we will study the past simple.","order":1}]' };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  const result = await analyzeVideoChapters(audioPath);
  assert.equal(result.chapters.length, 1);
  assert.equal(result.chapters.at(0)?.title, 'Past Simple');
  const transcriptionRequest = requests.find((request) => request.config.responseMimeType === 'text/plain');
  const chapterRequest = requests.find((request) => request.config.responseMimeType === 'application/json');
  const audio = transcriptionRequest.contents[0].parts[0].inlineData;
  assert.equal(audio.mimeType, 'audio/mpeg');
  assert.ok(audio.data.length > 0);
  assert.equal(transcriptionRequest.contents[0].parts.some((part: any) => part.fileData), false);
  assert.equal(typeof chapterRequest.contents, 'string');
  assert.ok(chapterRequest.contents.includes(transcribedSrt));
  const compressedPath = path.join(process.cwd(), '.tmp', 'inline-audio-test.mp3');
  fs.writeFileSync(compressedPath, Buffer.from(audio.data, 'base64'));
  testContext.after(() => fs.rmSync(compressedPath, { force: true }));
  const bitrate = Number(execFileSync('ffprobe', [
    '-v', 'error', '-show_entries', 'format=bit_rate', '-of', 'default=noprint_wrappers=1:nokey=1', compressedPath,
  ], { encoding: 'utf8' }).trim());
  assert.ok(bitrate <= 14_000, `expected compressed bitrate near 12kbps, received ${bitrate}`);
});

test('English lesson mindmap keeps visible text in the source language (2026-08-10 regression)', async () => {
  const requests: any[] = [];
  const client = { models: { generateContent: async (request: any) => {
    requests.push(request);
    return { candidates: [{ content: { parts: [] } }] };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  await assert.rejects(
    generateChapterMindmap(
      { title: 'Past Simple', summaryText: 'In this part, we will learn regular and irregular verbs.', order: 1 },
      'english-language-regression',
    ),
    /returned no image/,
  );

  const prompt = requests[0].contents[0].parts.at(-1).text;
  assert.match(prompt, /<REQUIRED_VISIBLE_LANGUAGE>the same Latin-script language used in the source/);
  assert.match(prompt, /Past Simple/);
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

test('mindmap sends every teacher reference with its real image type and identity-lock contract', async (testContext) => {
  const referenceDirectory = path.join(process.cwd(), '.tmp', `teacher-references-${Date.now()}`);
  const referencePaths = [
    path.join(referenceDirectory, 'front.webp'),
    path.join(referenceDirectory, 'profile.png'),
    path.join(referenceDirectory, 'smile.jpg'),
  ];
  fs.mkdirSync(referenceDirectory, { recursive: true });
  referencePaths.forEach((referencePath, index) => fs.writeFileSync(referencePath, Buffer.from(`reference-${index}`)));
  testContext.after(() => fs.rmSync(referenceDirectory, { recursive: true, force: true }));

  const requests: any[] = [];
  const client = { models: { generateContent: async (request: any) => {
    requests.push(request);
    return { candidates: [{ content: { parts: [] } }] };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  await assert.rejects(
    generateChapterMindmap(
      { title: 'تنظيم الوقت', summaryText: 'تنظيم الوقت بين المذاكرة والترفيه', order: 1 },
      'teacher-reference-test',
      referencePaths,
      { teacherStyles: ['cartoon'] },
    ),
    /returned no image/,
  );

  const requestParts = requests[0].contents[0].parts;
  assert.equal(requests[0].config.imageConfig.aspectRatio, '16:9');
  assert.equal(requests[0].config.imageConfig.imageSize, '4K');
  assert.deepEqual(
    requestParts.slice(0, 3).map((part: any) => part.inlineData.mimeType),
    ['image/webp', 'image/png', 'image/jpeg'],
  );
  assert.equal(requestParts.filter((part: any) => part.inlineData).length, referencePaths.length);
  assert.match(requestParts.at(-1).text, /TEACHER IDENTITY LOCK/);
});

test('mindmap refuses to silently omit a missing teacher reference', async () => {
  const client = { models: { generateContent: async () => {
    throw new Error('provider must not be called');
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  await assert.rejects(
    generateChapterMindmap(
      { title: 'فصل', summaryText: 'ملخص', order: 1 },
      'missing-reference-test',
      ['/missing/teacher-reference.webp'],
    ),
    /Teacher reference image is missing/,
  );
});

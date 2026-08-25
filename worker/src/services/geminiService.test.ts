import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'node:child_process';
import type { AIConfig } from './aiConfig.js';
import { analyzeVideoChapters, assertChapterOutputLanguage, evaluateEssayWithAI, generateChapterMindmap, generateLiveSupportReply, generateVideoChapters, setAIServiceRuntimeFactoryForTests, transcribePublicYouTubeVideo } from './geminiService.js';
import type { LiveSupportAgentPrompt } from './liveSupportAgent.js';
import { setGeminiRetryWaitForTests } from './aiProvider.js';
import { WorkerExternalError } from './workerFetch.js';

const developerConfig: AIConfig = {
  primaryProvider: 'developer', developerApiKey: 'test-key', textModel: 'text-model', imageModel: 'image-model',
};

function runtime(client: any) {
  return { config: developerConfig, developer: client as any };
}

afterEach(() => {
  setAIServiceRuntimeFactoryForTests(undefined);
  setGeminiRetryWaitForTests(undefined);
});

test('public YouTube transcription sends the canonical URL directly to Gemini (2026-08-24 regression)', async () => {
  const requests: any[] = [];
  const client = { models: { generateContent: async (request: any) => {
    requests.push(request);
    return { text: '1\n00:00:00,000 --> 00:00:01,000\nمقدمة الدرس' };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  const source = 'https://www.youtube.com/watch?v=AbCdEf12_-3';
  const result = await transcribePublicYouTubeVideo(source);

  assert.match(result, /مقدمة الدرس/);
  assert.equal(requests.length, 1);
  assert.equal(requests[0].contents[0].parts[0].fileData.fileUri, source);
  assert.equal(requests[0].contents[0].parts[0].fileData.mimeType, 'video/*');
  assert.equal(requests[0].contents[0].parts.some((part: any) => part.inlineData), false);
  assert.equal(requests[0].config.responseMimeType, 'text/plain');
});

test('public or unavailable YouTube rejection is permanent and provider details stay private', async () => {
  const client = { models: { generateContent: async () => {
    throw { status: 400, name: 'SENSITIVE_PROVIDER_SENTINEL' };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  await assert.rejects(
    transcribePublicYouTubeVideo('https://www.youtube.com/watch?v=AbCdEf12_-3'),
    (error: unknown) => error instanceof WorkerExternalError
      && !error.retryable
      && error.category === 'rejected'
      && !error.message.includes('SENSITIVE_PROVIDER_SENTINEL')
      && !error.message.includes('AbCdEf12_-3'),
  );
});

test('transient YouTube failure is classified for one bounded BullMQ retry layer', async () => {
  let calls = 0;
  setGeminiRetryWaitForTests(async () => {});
  const client = { models: { generateContent: async () => {
    calls += 1;
    throw { status: 503, name: 'SENSITIVE_PROVIDER_SENTINEL' };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  await assert.rejects(
    transcribePublicYouTubeVideo('https://www.youtube.com/watch?v=AbCdEf12_-3'),
    (error: unknown) => error instanceof WorkerExternalError
      && error.retryable
      && error.category === 'provider'
      && !error.message.includes('SENSITIVE_PROVIDER_SENTINEL'),
  );
  assert.equal(calls, 1);
});

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
  assert.equal(result.srtContent, transcribedSrt);
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

test('explicit English chapter language translates summaries while leaving the SRT source authoritative', async () => {
  const requests: any[] = [];
  const sourceSrt = '1\n00:00:00,000 --> 00:00:01,000\nهنا هنتعلم تركيب الخلية.';
  const client = { models: { generateContent: async (request: any) => {
    requests.push(request);
    return { text: '[{"title":"Cell Structure","startTime":0,"endTime":1,"summaryText":"In this part, we will learn the structure of a cell.","order":1}]' };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));

  const chapters = await generateVideoChapters(sourceSrt, 'en');

  assert.equal(chapters[0]?.title, 'Cell Structure');
  assert.match(requests[0].contents, /هنا هنتعلم تركيب الخلية/);
});

test('chapter language validation rejects wrong-script output before persistence', () => {
  const arabicChapters = [{
    title: 'تركيب الخلية', startTime: 0, endTime: 1,
    summaryText: 'هنا هنتعلم تركيب الخلية ووظيفة كل جزء فيها.', order: 1,
  }];
  const englishChapters = [{
    title: 'Cell Structure', startTime: 0, endTime: 1,
    summaryText: 'In this part, we will learn the structure of a cell.', order: 1,
  }];

  assert.doesNotThrow(() => assertChapterOutputLanguage(arabicChapters, 'ar'));
  assert.doesNotThrow(() => assertChapterOutputLanguage(englishChapters, 'en'));
  assert.throws(() => assertChapterOutputLanguage(arabicChapters, 'en'), /لغة عنوان الفصل/);
  assert.throws(() => assertChapterOutputLanguage(englishChapters, 'ar'), /لغة عنوان الفصل/);
});

test('English lesson mindmap accepts verified English visible text (2026-08-10 regression)', async (testContext) => {
  const videoId = 'english-language-regression';
  const generationRunId = '11111111-1111-4111-8111-111111111111';
  const client = { models: { generateContent: async (request: any) => {
    if (request.config.responseMimeType === 'application/json') {
      return { text: '{"arabicLetterCount":0,"latinLetterCount":18,"hasIllegibleText":false}' };
    }
    return { candidates: [{ content: { parts: [{ inlineData: {
      data: 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
      mimeType: 'image/png',
    } }] } }] };
  } } };
  setAIServiceRuntimeFactoryForTests(() => runtime(client));
  testContext.after(() => {
    const mindmapsDirectory = path.resolve(process.cwd(), '../backend/src/NaderGorge.API/wwwroot/mindmaps');
    if (!fs.existsSync(mindmapsDirectory)) return;
    for (const file of fs.readdirSync(mindmapsDirectory)) {
      if (file.startsWith(`${videoId}_run_${generationRunId}_chapter_1_`)) {
        fs.rmSync(path.join(mindmapsDirectory, file), { force: true });
      }
    }
  });

  const imageUrl = await generateChapterMindmap(
    { title: 'Past Simple', summaryText: 'In this part, we will learn regular and irregular verbs.', order: 1 },
    videoId,
    undefined,
    { outputLanguage: 'en', generationRunId },
  );

  assert.match(imageUrl, /\/mindmaps\/english-language-regression_run_/);
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

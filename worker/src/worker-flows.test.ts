import { test } from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { Redis } from 'ioredis';
import { TelegramClient } from 'telegram';
import { StringSession } from 'telegram/sessions/index.js';
import { markJobCancellation, throwIfCancellationRequested } from './cancellation.js';
import { processEvaluateEssayJob } from './jobs/evaluateEssay.js';
import { extractAudioFromVideo } from './utils/audioExtractor.js';

// Setup basic E2e test environment variables
process.env.GEMINI_API_KEY = 'mock_gemini_api_key_value_1234567890';
process.env.AI_PRIMARY_PROVIDER = 'developer';
process.env.BACKEND_API_URL = 'http://localhost:5245/api/v1';
process.env.AI_CALLBACK_SECRET = 'E2eOnlyAiCallbackSecretValue1234567890';

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

test('processEvaluateEssayJob runs successfully with Gemini AI mock and triggers callback', async () => {
  let calledUrl = '';
  let calledBody: any = null;
  
  globalThis.fetch = async (url: RequestInfo | URL, options?: RequestInit) => {
    const urlString = String(url);
    if (urlString.includes('callbacks/essay-graded')) {
      calledUrl = urlString;
      calledBody = JSON.parse(options?.body as string);
      return {
        ok: true,
        status: 200,
        headers: {
          get: (n: string) => null,
          entries: () => []
        },
        text: async () => 'OK'
      } as unknown as Response;
    } else {
      // Gemini API mock response
      const geminiResponse = {
        candidates: [
          {
            content: {
              parts: [
                {
                  text: JSON.stringify({ isCorrect: true, feedback: 'إجابة رائعة يا بطل!' })
                }
              ]
            }
          }
        ]
      };
      return {
        ok: true,
        status: 200,
        headers: {
          get: (name: string) => {
            if (name.toLowerCase() === 'content-type') return 'application/json';
            return null;
          },
          entries: () => []
        },
        json: async () => geminiResponse,
        text: async () => JSON.stringify(geminiResponse)
      } as unknown as Response;
    }
  };

  const progressCalls: any[] = [];
  const dummyJob: any = {
    id: 'essay-job-456',
    data: {
      essaySubmissionId: 'sub-789',
      answerText: 'إجابة الطالب',
      expectedAnswer: 'الإجابة النموذجية'
    },
    updateProgress: async (p: any) => {
      progressCalls.push(p);
    }
  };

  const result = await processEvaluateEssayJob(dummyJob);
  assert.deepStrictEqual(result, { success: true, score: 1, feedback: 'إجابة رائعة يا بطل!' });
  assert.strictEqual(calledUrl, 'http://localhost:5245/api/v1/internal/callbacks/essay-graded');
  assert.strictEqual(calledBody.essaySubmissionId, 'sub-789');
  assert.strictEqual(calledBody.aiScore, 1);
  assert.strictEqual(calledBody.aiFeedback, 'إجابة رائعة يا بطل!');
  assert.ok(progressCalls.length > 0);
});

test('processEvaluateEssayJob throws error to trigger queue retry if callback fails', async () => {
  globalThis.fetch = async (url: RequestInfo | URL, options?: RequestInit) => {
    const urlString = String(url);
    if (urlString.includes('callbacks/essay-graded')) {
      return {
        ok: false,
        status: 500,
        headers: {
          get: (n: string) => null,
          entries: () => []
        },
        text: async () => 'Internal Server Error'
      } as unknown as Response;
    } else {
      const geminiResponse = {
        candidates: [
          {
            content: {
              parts: [
                {
                  text: JSON.stringify({ isCorrect: false, feedback: 'محاولة جيدة ولكن غير صحيحة.' })
                }
              ]
            }
          }
        ]
      };
      return {
        ok: true,
        status: 200,
        headers: {
          get: (name: string) => {
            if (name.toLowerCase() === 'content-type') return 'application/json';
            return null;
          },
          entries: () => []
        },
        json: async () => geminiResponse,
        text: async () => JSON.stringify(geminiResponse)
      } as unknown as Response;
    }
  };

  const dummyJob: any = {
    id: 'essay-job-retry',
    data: {
      essaySubmissionId: 'sub-retry',
      answerText: 'wrong answer'
    },
    updateProgress: async () => {}
  };

  await assert.rejects(async () => {
    await processEvaluateEssayJob(dummyJob);
  }, /Webhook failed with status 500/);
});

test('extractAudioFromVideo calls Telegram client download flow when configured', async () => {
  // Generate a valid string session starting with '1' and valid base64 representation
  const dcBuffer = Buffer.from([1]);
  const addressBuffer = Buffer.from("127.0.0.1");
  const addressLengthBuffer = Buffer.alloc(2);
  addressLengthBuffer.writeInt16BE(addressBuffer.length, 0);
  const portBuffer = Buffer.alloc(2);
  portBuffer.writeInt16BE(443, 0);
  const key = Buffer.alloc(256);
  const encoded = Buffer.concat([
    dcBuffer,
    addressLengthBuffer,
    addressBuffer,
    portBuffer,
    key,
  ]).toString('base64');

  process.env.TELEGRAM_API_ID = '123456';
  process.env.TELEGRAM_API_HASH = 'mock_hash';
  process.env.TELEGRAM_STRING_SESSION = '1' + encoded;

  (TelegramClient.prototype as any).connect = async () => true;
  (TelegramClient.prototype as any).disconnect = async () => {};
  (TelegramClient.prototype as any).getEntity = async (username: string) => ({ username } as any);
  (TelegramClient.prototype as any).sendMessage = async (entity: any, options: any) => ({ } as any);
  (TelegramClient.prototype as any).addEventHandler = function(handler: any) {
    setTimeout(() => {
      handler({
        message: {
          senderId: '123',
          message: 'Here is your audio',
          getSender: async () => ({ username: 'utubebot' }),
          media: {
            document: {
              attributes: [
                {
                  className: 'DocumentAttributeFilename',
                  fileName: 'audio.mp3'
                }
              ]
            }
          }
        }
      });
    }, 50);
  } as any;
  (TelegramClient.prototype as any).removeEventHandler = () => {};
  (TelegramClient.prototype as any).downloadMedia = async function(media: any, options: any) {
    fs.writeFileSync(options.outputFile, 'mock mp3 data');
    return Buffer.from('mock');
  } as any;

  const result = await extractAudioFromVideo('https://youtube.com/watch?v=mock_video', 'mock_audio_tg');
  assert.ok(result.endsWith('mock_audio_tg.mp3'));
  assert.ok(fs.existsSync(result));

  try {
    fs.unlinkSync(result);
  } catch {}
});

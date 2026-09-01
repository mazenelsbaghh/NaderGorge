import assert from 'node:assert/strict';
import childProcess from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';

import { extractAudioFromInternalBunnyVideo } from './audioExtractor.js';
import { WorkerExternalError } from '../services/workerFetch.js';

const lessonVideoId = '12345678-abcd-1234-abcd-123456789abc';
const generationRunId = '11111111-1111-4111-8111-111111111111';

test('internal Bunny extraction fetches platform bytes with the internal token and never invokes yt-dlp', async (testContext) => {
  const originalFetch = globalThis.fetch;
  const originalExecFile = childProcess.execFile;
  const originalBackendUrl = process.env.BACKEND_API_URL;
  const originalRelaySecret = process.env.AI_MEDIA_RELAY_SECRET;
  const outputName = `internal_bunny_${Date.now()}`;
  let fetchedUrl = '';
  let fetchedToken = '';
  let ffmpegCalls = 0;
  let ytDlpCalls = 0;

  process.env.BACKEND_API_URL = 'http://backend.test';
  process.env.AI_MEDIA_RELAY_SECRET = 'test-internal-media-token';
  globalThis.fetch = async (input, init) => {
    fetchedUrl = String(input);
    fetchedToken = new Headers(init?.headers).get('X-Internal-Token') || '';
    return new Response('private-video-bytes', {
      status: 200,
      headers: { 'content-type': 'video/mp4' },
    });
  };
  childProcess.execFile = ((file: string, args: string[], options: unknown, callback?: unknown) => {
    if (file.includes('yt-dlp') || file.includes('youtube-dl')) ytDlpCalls += 1;
    if (file === 'ffmpeg') {
      ffmpegCalls += 1;
      const destination = args.at(-1);
      assert.ok(destination);
      fs.writeFileSync(destination!, 'compressed-audio');
    }
    const done = typeof options === 'function' ? options : callback;
    (done as ((error: Error | null, stdout: string, stderr: string) => void) | undefined)?.(null, '', '');
    return { on: () => undefined } as never;
  }) as unknown as typeof childProcess.execFile;

  testContext.after(() => {
    globalThis.fetch = originalFetch;
    childProcess.execFile = originalExecFile;
    if (originalBackendUrl === undefined) delete process.env.BACKEND_API_URL;
    else process.env.BACKEND_API_URL = originalBackendUrl;
    if (originalRelaySecret === undefined) delete process.env.AI_MEDIA_RELAY_SECRET;
    else process.env.AI_MEDIA_RELAY_SECRET = originalRelaySecret;
  });

  const audioPath = await extractAudioFromInternalBunnyVideo(lessonVideoId, generationRunId, outputName);
  try {
    assert.equal(
      fetchedUrl,
      `http://backend.test/api/v1/internal/ai-media/bunny/${lessonVideoId}/runs/${generationRunId}/original`,
    );
    assert.equal(fetchedToken, 'test-internal-media-token');
    assert.equal(ffmpegCalls, 1);
    assert.equal(ytDlpCalls, 0);
    assert.equal(fs.existsSync(audioPath), true);
    assert.equal(fs.existsSync(path.join(path.dirname(audioPath), `${outputName}.bunny-original`)), false);
  } finally {
    fs.rmSync(audioPath, { force: true });
  }
});

test('internal Bunny extraction makes protected relay failures terminal without starting ffmpeg', async (testContext) => {
  const originalFetch = globalThis.fetch;
  const originalExecFile = childProcess.execFile;
  const originalBackendUrl = process.env.BACKEND_API_URL;
  const originalRelaySecret = process.env.AI_MEDIA_RELAY_SECRET;
  let processCalls = 0;

  process.env.BACKEND_API_URL = 'http://backend.test';
  process.env.AI_MEDIA_RELAY_SECRET = 'test-internal-media-token';
  globalThis.fetch = async () => new Response('{"code":"BUNNY_ANALYSIS_ORIGINAL_UNAVAILABLE"}', { status: 422 });
  childProcess.execFile = (() => {
    processCalls += 1;
    throw new Error('ffmpeg must not run when the relay rejects access');
  }) as unknown as typeof childProcess.execFile;

  testContext.after(() => {
    globalThis.fetch = originalFetch;
    childProcess.execFile = originalExecFile;
    if (originalBackendUrl === undefined) delete process.env.BACKEND_API_URL;
    else process.env.BACKEND_API_URL = originalBackendUrl;
    if (originalRelaySecret === undefined) delete process.env.AI_MEDIA_RELAY_SECRET;
    else process.env.AI_MEDIA_RELAY_SECRET = originalRelaySecret;
  });

  await assert.rejects(
    extractAudioFromInternalBunnyVideo(lessonVideoId, generationRunId, `internal_bunny_rejected_${Date.now()}`),
    (error: unknown) => error instanceof WorkerExternalError
      && error.category === 'rejected'
      && error.retryable === false,
  );
  assert.equal(processCalls, 0);
});

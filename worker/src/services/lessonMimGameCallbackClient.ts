import { fetchWithTimeout, WorkerExternalError } from './workerFetch.js';

const RETRY_DELAYS_MS = [250, 1_000];

export interface LessonMimGameCallbackClient {
  complete(payload: { gameId: string; runId: string; sourceFingerprint: string; contentJson: string }): Promise<void>;
  fail(payload: { gameId: string; runId: string; errorCode: string }): Promise<void>;
}

function callbackUrl(path: string) {
  const base = (process.env.BACKEND_API_URL || 'http://localhost:5245').replace(/\/$/, '').replace(/\/api\/v1$/, '');
  return `${base}/api/v1/internal/callbacks/${path}`;
}

async function post(path: string, body: object) {
  const response = await fetchWithTimeout(callbackUrl(path), {
    method: 'POST', timeoutMs: 5_000, maxResponseBytes: 16_384, operation: `lesson-mim-${path}`,
    headers: { 'Content-Type': 'application/json', 'X-Internal-Token': process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET || '' },
    body: JSON.stringify(body),
  });
  if (response.ok) return;
  const retryable = response.status === 408 || response.status === 429 || response.status >= 500;
  throw new WorkerExternalError(retryable ? 'provider' : 'rejected', retryable, 'Lesson game callback failed.');
}

async function boundedPost(path: string, body: object) {
  for (let attempt = 0; ; attempt += 1) {
    try { await post(path, body); return; }
    catch (error) {
      const failure = error instanceof WorkerExternalError ? error : new WorkerExternalError('implementation', false, 'Lesson game callback failed.');
      const delay = RETRY_DELAYS_MS[attempt];
      if (!failure.retryable || delay === undefined) throw failure;
      await new Promise(resolve => setTimeout(resolve, delay));
    }
  }
}

export const lessonMimGameCallbacks: LessonMimGameCallbackClient = {
  complete: payload => boundedPost('lesson-mim-game-completed', payload),
  fail: payload => boundedPost('lesson-mim-game-failed', payload),
};

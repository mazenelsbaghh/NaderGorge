import type { Job } from 'bullmq';
import { logWarn } from '../logging.js';
import { isTerminalJobFailure } from '../utils/jobTempFiles.js';
import { fetchWithTimeout, WorkerExternalError } from './workerFetch.js';

const RETRY_DELAYS_MS = [250, 1_000];

function callbackUrl() {
  const baseUrl = (process.env.BACKEND_API_URL || 'http://localhost:5245')
    .replace(/\/$/, '')
    .replace(/\/api\/v1$/, '');
  return `${baseUrl}/api/v1/internal/callbacks/single-mindmap-failed`;
}

async function postSingleMindmapFailure(chapterId: string, generationRunId?: string) {
  const response = await fetchWithTimeout(callbackUrl(), {
    method: 'POST',
    timeoutMs: 5_000,
    maxResponseBytes: 16_384,
    operation: 'single-mindmap-terminal-failure',
    headers: {
      'Content-Type': 'application/json',
      'X-Internal-Token': process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET || '',
    },
    body: JSON.stringify({ chapterId, ...(generationRunId ? { generationRunId } : {}) }),
  });
  if (response.ok) return;

  const retryable = response.status === 408 || response.status === 429 || response.status >= 500;
  throw new WorkerExternalError(
    retryable ? 'provider' : 'rejected',
    retryable,
    'تعذر إرسال حالة فشل الخريطة الذهنية إلى الخادم.',
  );
}

async function postWithBoundedRetry(chapterId: string, generationRunId?: string) {
  for (let attempt = 0; ; attempt += 1) {
    try {
      await postSingleMindmapFailure(chapterId, generationRunId);
      return;
    } catch (error) {
      const failure = error instanceof WorkerExternalError
        ? error
        : new WorkerExternalError('implementation', false, 'تعذر إرسال حالة فشل الخريطة الذهنية إلى الخادم.');
      const retryDelay = RETRY_DELAYS_MS[attempt];
      if (!failure.retryable || retryDelay === undefined) throw failure;
      logWarn('single-mindmap-failed', 'Terminal callback will be retried.', { chapterId, attempt: attempt + 1 });
      await new Promise(resolve => setTimeout(resolve, retryDelay));
    }
  }
}

export async function reportTerminalSingleMindmapFailure(
  job: Pick<Job, 'attemptsMade' | 'data' | 'opts'> | undefined,
  error: Pick<Error, 'name'>,
) {
  if (!job || !isTerminalJobFailure(job, error)) return false;
  const payload = job.data && typeof job.data === 'object'
    ? job.data as Record<string, unknown>
    : {};
  const chapterId = payload.chapterId || payload.ChapterId;
  if (typeof chapterId !== 'string' || !chapterId) return false;
  const generationRunId = payload.generationRunId || payload.GenerationRunId;
  await postWithBoundedRetry(
    chapterId,
    typeof generationRunId === 'string' && generationRunId ? generationRunId : undefined,
  );
  return true;
}

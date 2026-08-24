import type { Job } from 'bullmq';
import { logWarn } from '../logging.js';
import { publicFailedJobReason } from '../server/jobStatus.js';
import { isTerminalJobFailure } from '../utils/jobTempFiles.js';
import { fetchWithTimeout, WorkerExternalError } from './workerFetch.js';

const RETRY_DELAYS_MS = [250, 1_000];

function callbackUrl() {
  const baseUrl = (process.env.BACKEND_API_URL || 'http://localhost:5245')
    .replace(/\/$/, '')
    .replace(/\/api\/v1$/, '');
  return `${baseUrl}/api/v1/internal/callbacks/ai-progress`;
}

async function postTerminalFailure(jobId: string, safeReason: string) {
  const response = await fetchWithTimeout(callbackUrl(), {
    method: 'POST',
    timeoutMs: 5_000,
    maxResponseBytes: 16_384,
    operation: 'ai-terminal-failure',
    headers: {
      'Content-Type': 'application/json',
      'X-Internal-Token': process.env.API_CALLBACK_SECRET || process.env.AI_CALLBACK_SECRET || '',
    },
    body: JSON.stringify({ jobId, progress: 0, status: 'failed', message: safeReason }),
  });
  if (response.ok) return;

  const retryable = response.status === 408 || response.status === 429 || response.status >= 500;
  throw new WorkerExternalError(
    retryable ? 'provider' : 'rejected',
    retryable,
    'تعذر إرسال حالة فشل تحليل الفيديو إلى الخادم.',
  );
}

async function postTerminalFailureWithRetry(jobId: string, safeReason: string) {
  for (let attempt = 0; ; attempt += 1) {
    try {
      await postTerminalFailure(jobId, safeReason);
      return;
    } catch (error) {
      const failure = error instanceof WorkerExternalError
        ? error
        : new WorkerExternalError('implementation', false, 'تعذر إرسال حالة فشل تحليل الفيديو إلى الخادم.');
      const retryDelay = RETRY_DELAYS_MS[attempt];
      if (!failure.retryable || retryDelay === undefined) throw failure;
      logWarn('ai-video-callback', 'Terminal failure callback will be retried.', { jobId, attempt: attempt + 1 });
      await new Promise((resolve) => setTimeout(resolve, retryDelay));
    }
  }
}

export async function reportTerminalVideoFailure(
  job: Pick<Job, 'id' | 'attemptsMade' | 'opts'> | undefined,
  error: Pick<Error, 'name' | 'message'>,
) {
  if (!job?.id || !isTerminalJobFailure(job, error)) return false;
  await postTerminalFailureWithRetry(String(job.id), publicFailedJobReason(error.message));
  return true;
}

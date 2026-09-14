import { getAccessToken } from '@/lib/auth-memory';
import {
  sanitizeAiJobStatus,
  type SafeAiJobStatus,
} from '@/lib/ai-job-status';

export type WorkerJobStatus = SafeAiJobStatus;

async function workerRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getAccessToken();
  if (!token) {
    throw new Error('Authentication required');
  }

  const response = await fetch(`/api/worker/${path.replace(/^\/+/, '')}`, {
    ...init,
    cache: 'no-store',
    headers: {
      ...(init.headers ?? {}),
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new Error(payload?.error || payload?.message || 'Worker request failed');
  }

  return response.json() as Promise<T>;
}

export const workerService = {
  getWorkerJobStatus: async (jobId: string) =>
    sanitizeAiJobStatus(
      await workerRequest<unknown>(`status/${encodeURIComponent(jobId)}`),
      jobId,
    ),

  cancelWorkerJob: (jobId: string) =>
    workerRequest<{ success?: boolean; message?: string }>(
      `status/${encodeURIComponent(jobId)}`,
      { method: 'DELETE' },
    ),

  retryWorkerJob: (jobId: string) =>
    workerRequest<{ success?: boolean; message?: string }>(
      `status/${encodeURIComponent(jobId)}/retry`,
      { method: 'POST' },
    ),
};

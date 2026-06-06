import { getStoredAccessToken } from '@/lib/auth-storage';

export interface WorkerJobStatus {
  id?: string;
  state: 'waiting' | 'active' | 'completed' | 'failed' | 'not_found';
  progress?: number | { percentage?: number; stage?: string };
  failedReason?: string | null;
}

async function workerRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getStoredAccessToken();
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
  getWorkerJobStatus: (jobId: string) =>
    workerRequest<WorkerJobStatus>(`status/${encodeURIComponent(jobId)}`),

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

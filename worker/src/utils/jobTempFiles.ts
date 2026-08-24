import fs from 'node:fs';
import type { Job } from 'bullmq';
import { logWarn } from '../logging.js';

export function isFinalJobAttempt(job: Pick<Job, 'attemptsMade' | 'opts'>) {
  return job.attemptsMade + 1 >= (job.opts.attempts ?? 1);
}

export function isTerminalJobFailure(
  job: Pick<Job, 'attemptsMade' | 'opts'> | undefined,
  error: Pick<Error, 'name'>,
) {
  if (error.name === 'UnrecoverableError') return true;
  if (!job) return true;
  return job.attemptsMade >= (job.opts.attempts ?? 1);
}

export function removeJobTempFile(filePath: string, jobId: string | number | undefined) {
  if (!filePath) return;

  try {
    fs.rmSync(filePath, { force: true });
  } catch (error) {
    logWarn('worker-cleanup', 'Failed to remove a job temporary file.', { jobId, error });
  }
}

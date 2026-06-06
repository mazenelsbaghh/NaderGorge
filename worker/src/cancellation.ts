import { Job } from 'bullmq';
import { Redis } from 'ioredis';

const DEFAULT_REDIS_URL = process.env.REDIS_URL || 'redis://localhost:6379';
const CANCELLATION_TTL_SECONDS = 24 * 60 * 60;
const cancellationRedis = new Redis(DEFAULT_REDIS_URL);

function cancellationKey(jobId: string | number) {
  return `cancelled-jobs:${jobId}`;
}

export async function markJobCancellation(job: Job) {
  const state = await job.getState();

  if (state === 'waiting' || state === 'delayed' || state === 'prioritized') {
    await job.remove();
    return { removed: true, state };
  }

  if (!job.id) {
    return { removed: false, state };
  }

  await cancellationRedis.set(cancellationKey(job.id), '1', 'EX', CANCELLATION_TTL_SECONDS);
  await job.updateData({ ...job.data, cancellationRequested: true });
  return { removed: false, state };
}

export async function throwIfCancellationRequested(job: Job) {
  if (!job.id) return;

  const isCancelled = await cancellationRedis.get(cancellationKey(job.id));
  if (isCancelled) {
    throw new Error('Job cancellation requested');
  }
}

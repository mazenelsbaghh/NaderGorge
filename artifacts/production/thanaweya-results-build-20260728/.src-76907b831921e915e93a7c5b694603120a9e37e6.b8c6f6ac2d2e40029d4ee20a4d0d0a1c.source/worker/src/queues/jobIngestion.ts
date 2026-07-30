import type { Queue } from 'bullmq';
import type { Redis } from 'ioredis';
import { isJobCancellationMarked } from '../cancellation.js';
import { logQueueEvent, logWarn, logError } from '../logging.js';

export interface QueueSet {
  aiQueue: Queue;
  mindmapsQueue: Queue;
  notifQueue: Queue;
  essayQueue: Queue;
  liveSupportQueue: Queue;
}

export interface IngestResult {
  action: 'enqueued' | 'skipped-existing' | 'acked-invalid';
  targetJobId?: string;
}

const JOB_RETENTION_OPTIONS = {
  removeOnComplete: { count: 1000, age: 7 * 24 * 3600 },
  removeOnFail: { count: 500, age: 14 * 24 * 3600 },
};

export function resolveQueueTarget(jobType: string, jobId: string, parsedPayload: any, queues: QueueSet) {
  let targetQueue: Queue;
  let bullmqJobName: string;
  let targetJobId: string;

  if (jobType === 'video analysis') {
    targetQueue = queues.aiQueue;
    bullmqJobName = 'analyze';
    targetJobId = jobId;
  } else if (jobType === 'mind maps') {
    targetQueue = queues.mindmapsQueue;
    bullmqJobName = 'generate';
    const chapId = parsedPayload.chapterId || parsedPayload.ChapterId;
    const vidId = parsedPayload.lessonVideoId || parsedPayload.LessonVideoId;
    targetJobId = chapId ? `${vidId}_mindmap_${chapId}` : `${vidId}_mindmaps`;
  } else if (jobType === 'essay') {
    targetQueue = queues.essayQueue;
    bullmqJobName = 'evaluate';
    targetJobId = jobId;
  } else if (jobType === 'notification') {
    targetQueue = queues.notifQueue;
    bullmqJobName = parsedPayload.WarningId ? 'send-warning' : parsedPayload.ParentPush ? 'parent-push' : 'chat-mention';
    targetJobId = jobId;
  } else if (jobType === 'live support turn') {
    targetQueue = queues.liveSupportQueue;
    bullmqJobName = 'respond';
    targetJobId = jobId;
  } else {
    return undefined;
  }

  return { targetQueue, bullmqJobName, targetJobId: sanitizeBullMqJobId(targetJobId) };
}

function sanitizeBullMqJobId(jobId: string) {
  return jobId.replace(/[^A-Za-z0-9._-]/g, '-').slice(0, 180);
}

export async function ingestStreamJob(redis: Redis, queues: QueueSet, messageStreamId: string, fields: string[]): Promise<IngestResult> {
  const obj: Record<string, string | undefined> = {};
  for (let i = 0; i < fields.length; i += 2) {
    const key = fields[i];
    if (key !== undefined) obj[key] = fields[i + 1];
  }

  const { jobType, jobId, payload } = obj;
  if (!jobType || !jobId || !payload) {
    logWarn('job-stream', 'Invalid stream message.', { messageStreamId });
    await acknowledge(redis, messageStreamId);
    return { action: 'acked-invalid' };
  }

  let parsedPayload: any;
  try {
    parsedPayload = JSON.parse(payload);
  } catch (error) {
    logWarn('job-stream', 'Failed to parse stream payload.', { messageStreamId, error });
    await acknowledge(redis, messageStreamId);
    return { action: 'acked-invalid' };
  }

  const target = resolveQueueTarget(jobType, jobId, parsedPayload, queues);
  if (!target) {
    logWarn('job-stream', 'Unknown job type.', { messageStreamId, jobType });
    await acknowledge(redis, messageStreamId);
    return { action: 'acked-invalid' };
  }

  const { targetQueue, bullmqJobName, targetJobId } = target;
  logQueueEvent('job-stream', `Ingesting ${jobType} job to BullMQ`, { jobId: targetJobId });

  const existingJob = await targetQueue.getJob(targetJobId);
  if (existingJob) {
    const state = await existingJob.getState();
    logQueueEvent('job-stream', 'Skipping duplicate existing BullMQ job.', { jobId: targetJobId, state });
    await acknowledge(redis, messageStreamId);
    return { action: 'skipped-existing', targetJobId };
  }

  if (await isJobCancellationMarked(targetJobId)) {
    logQueueEvent('job-stream', 'Skipping cancelled job ingestion.', { jobId: targetJobId });
    await acknowledge(redis, messageStreamId);
    return { action: 'skipped-existing', targetJobId };
  }

  try {
    const isLiveSupportTurn = jobType === 'live support turn';
    const isEssay = jobType === 'essay';
    await targetQueue.add(bullmqJobName, parsedPayload, {
      jobId: targetJobId,
      ...JOB_RETENTION_OPTIONS,
      attempts: isLiveSupportTurn ? 4 : 5,
      backoff: isEssay
        ? { type: 'fixed', delay: 20_000 }
        : { type: 'exponential', delay: isLiveSupportTurn ? 2000 : 5000 },
    });
    await acknowledge(redis, messageStreamId);
    return { action: 'enqueued', targetJobId };
  } catch (error) {
    logError('job-stream', 'Failed to enqueue BullMQ job.', { jobId: targetJobId, error });
    throw error;
  }
}

async function acknowledge(redis: Redis, messageStreamId: string) {
  await redis.xack('job-stream', 'worker-group', messageStreamId);
  await redis.xdel('job-stream', messageStreamId);
}

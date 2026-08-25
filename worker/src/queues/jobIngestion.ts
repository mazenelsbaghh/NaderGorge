import type { Queue } from 'bullmq';
import type { Redis } from 'ioredis';
import { isJobCancellationMarked } from '../cancellation.js';
import { logQueueEvent, logWarn, logError } from '../logging.js';
import { storeQueuedJobAlias } from './jobAlias.js';

export interface QueueSet {
  aiQueue: Queue;
  mindmapsQueue: Queue;
  notifQueue: Queue;
  essayQueue: Queue;
  liveSupportQueue: Queue;
  adminAIQueue: Queue;
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
  let physicalBaseJobId: string;
  let logicalJobId: string;

  if (jobType === 'video analysis') {
    targetQueue = queues.aiQueue;
    bullmqJobName = 'analyze';
    physicalBaseJobId = jobId;
    logicalJobId = jobId;
  } else if (jobType === 'mind maps') {
    targetQueue = queues.mindmapsQueue;
    bullmqJobName = 'generate';
    const chapId = parsedPayload.chapterId || parsedPayload.ChapterId;
    const vidId = parsedPayload.lessonVideoId || parsedPayload.LessonVideoId;
    physicalBaseJobId = chapId ? `${vidId}_mindmap_${chapId}` : `${vidId}_mindmaps`;
    logicalJobId = `${vidId}_mindmaps`;
  } else if (jobType === 'essay') {
    targetQueue = queues.essayQueue;
    bullmqJobName = 'evaluate';
    physicalBaseJobId = jobId;
    logicalJobId = jobId;
  } else if (jobType === 'notification') {
    targetQueue = queues.notifQueue;
    bullmqJobName = parsedPayload.WarningId ? 'send-warning' : parsedPayload.ParentPush ? 'parent-push' : 'chat-mention';
    physicalBaseJobId = jobId;
    logicalJobId = jobId;
  } else if (jobType === 'live support turn') {
    targetQueue = queues.liveSupportQueue;
    bullmqJobName = 'respond';
    physicalBaseJobId = jobId;
    logicalJobId = jobId;
  } else if (jobType === 'admin ai turn') {
    targetQueue = queues.adminAIQueue;
    bullmqJobName = 'respond';
    physicalBaseJobId = jobId;
    logicalJobId = jobId;
  } else {
    return undefined;
  }

  const sanitizedLogicalJobId = sanitizeBullMqJobId(logicalJobId);
  const rawRunId = jobType === 'video analysis' || jobType === 'mind maps'
    ? parsedPayload.generationRunId || parsedPayload.GenerationRunId
    : undefined;
  const targetJobId = rawRunId
    ? runScopedBullMqJobId(physicalBaseJobId, String(rawRunId))
    : sanitizeBullMqJobId(physicalBaseJobId);
  return { targetQueue, bullmqJobName, targetJobId, logicalJobId: sanitizedLogicalJobId };
}

function sanitizeBullMqJobId(jobId: string) {
  return jobId.replace(/[^A-Za-z0-9._-]/g, '-').slice(0, 180);
}

function runScopedBullMqJobId(physicalBaseJobId: string, generationRunId: string) {
  const runSuffix = `--run-${sanitizeBullMqJobId(generationRunId).slice(0, 64)}`;
  const baseLength = Math.max(1, 180 - runSuffix.length);
  return `${sanitizeBullMqJobId(physicalBaseJobId).slice(0, baseLength)}${runSuffix}`;
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

  const { targetQueue, bullmqJobName, targetJobId, logicalJobId } = target;
  const isGenerationJob = jobType === 'video analysis' || jobType === 'mind maps';
  const queuedAlias = isGenerationJob
    ? { logicalJobId, physicalJobId: targetJobId, queueName: targetQueue.name }
    : undefined;
  logQueueEvent('job-stream', `Ingesting ${jobType} job to BullMQ`, { jobId: targetJobId });

  const existingJob = await targetQueue.getJob(targetJobId);
  if (existingJob) {
    const state = await existingJob.getState();
    if (queuedAlias) await storeQueuedJobAlias(redis, queuedAlias, messageStreamId);
    if (state === 'completed' || state === 'failed') {
      await existingJob.remove();
    } else {
      logQueueEvent('job-stream', 'Skipping duplicate existing BullMQ job.', { jobId: targetJobId, state });
      await acknowledge(redis, messageStreamId);
      return { action: 'skipped-existing', targetJobId };
    }
  }

  if (await isJobCancellationMarked(targetJobId) || await isJobCancellationMarked(logicalJobId)) {
    logQueueEvent('job-stream', 'Skipping cancelled job ingestion.', { jobId: targetJobId });
    await acknowledge(redis, messageStreamId);
    return { action: 'skipped-existing', targetJobId };
  }

  try {
    const isLiveSupportTurn = jobType === 'live support turn';
    const isAdminAITurn = jobType === 'admin ai turn';
    const isEssay = jobType === 'essay';
    let attempts = 5;
    if (jobType === 'video analysis') attempts = 3;
    else if (isLiveSupportTurn) attempts = 4;
    const queuedPayload = isGenerationJob
      ? { ...parsedPayload, logicalJobId }
      : parsedPayload;
    await targetQueue.add(bullmqJobName, queuedPayload, {
      jobId: targetJobId,
      ...JOB_RETENTION_OPTIONS,
      attempts,
      backoff: isEssay
        ? { type: 'fixed', delay: 20_000 }
        : { type: 'exponential', delay: isLiveSupportTurn || isAdminAITurn ? 2000 : 5000 },
    });
    if (queuedAlias) await storeQueuedJobAlias(redis, queuedAlias, messageStreamId);
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

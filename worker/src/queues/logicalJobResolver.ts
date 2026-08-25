import type { Job, Queue } from 'bullmq';
import type { Redis } from 'ioredis';

import { resolveQueuedJobAlias } from './jobAlias.js';

export interface GenerationJobQueues {
  analysis: Queue;
  mindmaps: Queue;
}

export interface ResolvedGenerationJob {
  job: Job;
  logicalJobId: string;
  physicalJobId: string;
  queueName: string;
}

function queueForName(
  queues: GenerationJobQueues,
  queueName: string,
): Queue | undefined {
  if (queues.analysis.name === queueName) {
    return queues.analysis;
  }

  if (queues.mindmaps.name === queueName) {
    return queues.mindmaps;
  }

  return undefined;
}

export async function resolveGenerationJob(
  redis: Redis,
  queues: GenerationJobQueues,
  logicalJobId: string,
): Promise<ResolvedGenerationJob | undefined> {
  const alias = await resolveQueuedJobAlias(redis, logicalJobId);
  if (alias) {
    const aliasedQueue = queueForName(queues, alias.queueName);
    const aliasedJob = await aliasedQueue?.getJob(alias.physicalJobId);
    if (aliasedJob) {
      return {
        job: aliasedJob,
        logicalJobId,
        physicalJobId: alias.physicalJobId,
        queueName: alias.queueName,
      };
    }
  }

  for (const queue of [queues.analysis, queues.mindmaps]) {
    const legacyJob = await queue.getJob(logicalJobId);
    if (legacyJob) {
      return {
        job: legacyJob,
        logicalJobId,
        physicalJobId: logicalJobId,
        queueName: queue.name,
      };
    }
  }

  return undefined;
}

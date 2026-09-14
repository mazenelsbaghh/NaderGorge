import type { Redis } from 'ioredis';
import type { QueueSet } from './jobIngestion.js';
import { ingestStreamJob } from './jobIngestion.js';
import { logQueueEvent } from '../logging.js';

export async function claimStaleStreamMessages(redis: Redis, queues: QueueSet, consumerName: string) {
  const minIdleMs = Number.parseInt(process.env.WORKER_STREAM_CLAIM_IDLE_MS || '60000', 10);
  const batchSize = Number.parseInt(process.env.WORKER_STREAM_CLAIM_BATCH_SIZE || '10', 10);
  const claimed = (await redis.xautoclaim('job-stream', 'worker-group', consumerName, minIdleMs, '0-0', 'COUNT', batchSize)) as any;
  const messages = Array.isArray(claimed?.[1]) ? claimed[1] : [];
  if (messages.length > 0) {
    logQueueEvent('job-stream', 'Claimed stale Redis stream messages.', { count: messages.length, consumerName });
  }
  for (const [messageStreamId, fields] of messages) {
    await ingestStreamJob(redis, queues, messageStreamId, fields);
  }
  return messages.length;
}

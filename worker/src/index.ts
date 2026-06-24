import { Redis } from 'ioredis';
import { Pool } from 'pg';
import crypto from 'crypto';
import dotenv from 'dotenv';
import { Worker, Queue } from 'bullmq';
import express from 'express';
import cors from 'cors';
import { createBullBoard } from '@bull-board/api';
import { BullMQAdapter } from '@bull-board/api/bullMQAdapter';
import { ExpressAdapter } from '@bull-board/express';
import { runNightlySweep } from './jobs/commitment-engine.js';
import { processNotificationJob } from './jobs/notification-sender.js';
import { requireWorkerAdminToken, validateWorkerSecurityConfig } from './security.js';
import { logQueueEvent } from './logging.js';
import { markJobCancellation, clearJobCancellation } from './cancellation.js';
import { readAIConfig } from './services/aiConfig.js';
import { TemporaryAudioStorage } from './services/temporaryAudioStorage.js';
import { generateLiveSupportReply } from './services/geminiService.js';
import { runLiveSupportAgent, type LiveSupportClaimContext } from './services/liveSupportAgent.js';

dotenv.config();
validateWorkerSecurityConfig();
let aiStartupReady = false;
let liveSupportWorkerReady = false;

async function validateAIStartup() {
  const config = readAIConfig();
  if (config.primaryProvider === 'vertex') {
    await new TemporaryAudioStorage(config).validateAccess();
  }
  aiStartupReady = true;
  console.log('[AI startup] Provider and temporary-storage configuration validated.', {
    provider: config.primaryProvider,
    location: config.location,
  });
}

const DEFAULT_REDIS_URL = process.env.REDIS_URL || 'redis://localhost:6379';
const JOB_RETENTION_OPTIONS = {
  removeOnComplete: { count: 1000, age: 7 * 24 * 3600 },
  removeOnFail: { count: 500, age: 14 * 24 * 3600 },
};

const redis = new Redis(DEFAULT_REDIS_URL);
const pool = new Pool({
  connectionString: process.env.DATABASE_URL || process.env.DB_CONNECTION_STRING || 'postgresql://postgres:postgres@localhost:5435/nadergorge?schema=public'
});

// BullMQ Connection Shared config
const connection = {
  host: new URL(DEFAULT_REDIS_URL).hostname,
  port: parseInt(new URL(DEFAULT_REDIS_URL).port) || 6379,
  username: new URL(DEFAULT_REDIS_URL).username || undefined,
  password: new URL(DEFAULT_REDIS_URL).password || undefined,
};

async function reportProgressToBackend(jobId: string, progress: any) {
  try {
    const backendBaseUrl = process.env.BACKEND_API_URL || 'http://localhost:5245';
    const apiKey = process.env.API_CALLBACK_SECRET;
    
    let percentage = 0;
    let stage = '';
    if (typeof progress === 'object' && progress !== null) {
      percentage = progress.percentage ?? 0;
      stage = progress.stage ?? '';
    } else {
      percentage = Number(progress) || 0;
    }

    const res = await fetch(`${backendBaseUrl}/api/v1/internal/callbacks/ai-progress`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Token': apiKey || ''
      },
      body: JSON.stringify({
        jobId,
        progress: percentage,
        status: 'active',
        message: stage
      })
    });
    if (!res.ok) {
      console.error(`[Worker] Progress callback failed for job ${jobId} with status ${res.status}`);
    }
  } catch (err) {
    console.error(`[Worker] Failed to report progress for job ${jobId}:`, err);
  }
}

async function reportFailureToBackend(jobId: string, errorMsg: string) {
  try {
    const backendBaseUrl = process.env.BACKEND_API_URL || 'http://localhost:5245';
    const apiKey = process.env.API_CALLBACK_SECRET;

    const res = await fetch(`${backendBaseUrl}/api/v1/internal/callbacks/ai-progress`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Token': apiKey || ''
      },
      body: JSON.stringify({
        jobId,
        progress: 0,
        status: 'failed',
        message: errorMsg
      })
    });
    if (!res.ok) {
      console.error(`[Worker] Failure callback failed for job ${jobId} with status ${res.status}`);
    }
  } catch (err) {
    console.error(`[Worker] Failed to report failure for job ${jobId}:`, err);
  }
}

async function startNotificationWorker() {
  const worker = new Worker('notifications', async (job) => {
    return await processNotificationJob(job);
  }, { connection });

  worker.on('completed', job => {
    console.log(`[Worker] Job ${job.id} has completed!`);
  });

  worker.on('failed', (job, err) => {
    console.error(`[Worker] Job ${job?.id} has failed with ${err.message}`);
  });
  
  console.log('[Worker] Notification BullMQ worker started on queue: notifications');
}

async function startAIWorker() {
  const worker = new Worker('ai-video-chapters', async (job) => {
    // Dynamic import to avoid loading heavy modules if not needed immediately
    const processor = await import('./jobs/analyzeVideoChapters.js');
    return await processor.default(job);
  }, { connection });

  worker.on('progress', (job, progress) => {
    reportProgressToBackend(job.id!, progress);
  });

  worker.on('completed', job => {
    console.log(`[AI Worker] Job ${job.id} has completed successfully!`);
  });

  worker.on('failed', (job, err) => {
    console.error(`[AI Worker] Job ${job?.id} has failed with ${err.message}`);
    if (job) {
      reportFailureToBackend(job.id!, err.message);
    }
  });
  
  console.log('[Worker] AI Video Chapters BullMQ worker started on queue: ai-video-chapters');
}

async function startEssayWorker() {
  const worker = new Worker('ai-essay-grading', async (job) => {
    const processor = await import('./jobs/evaluateEssay.js');
    return await processor.processEvaluateEssayJob(job);
  }, { connection });

  worker.on('completed', job => {
    console.log(`[Essay Worker] Job ${job.id} has completed successfully!`);
  });

  worker.on('failed', (job, err) => {
    console.error(`[Essay Worker] Job ${job?.id} has failed with ${err.message}`);
  });
  
  console.log('[Worker] AI Essay Grading BullMQ worker started on queue: ai-essay-grading');
}

async function startMindmapsWorker() {
  const worker = new Worker('generate-chapter-mindmaps', async (job) => {
    const processor = await import('./jobs/generateChapterMindmaps.js');
    return await processor.default(job);
  }, { connection });

  worker.on('progress', (job, progress) => {
    reportProgressToBackend(job.id!, progress);
  });

  worker.on('completed', job => {
    console.log(`[Mindmaps Worker] Job ${job.id} has completed successfully!`);
  });

  worker.on('failed', (job, err) => {
    console.error(`[Mindmaps Worker] Job ${job?.id} has failed with ${err.message}`);
    const maxAttempts = job?.opts.attempts ?? 1;
    const attemptsExhausted = job ? job.attemptsMade >= maxAttempts : true;
    if (job && attemptsExhausted) {
      reportFailureToBackend(job.id!, err.message);
    }
  });
  
  console.log('[Worker] Mindmaps BullMQ worker started on queue: generate-chapter-mindmaps');
}

async function startLiveSupportWorker() {
  const worker = new Worker('ai-live-support-turns', async (job) => {
    const processor = await import('./jobs/processLiveSupportTurn.js');
    return await processor.default(job);
  }, {
    connection,
    concurrency: Math.max(1, Number.parseInt(process.env.AI_LIVE_SUPPORT_CONCURRENCY || '4', 10) || 4),
    lockDuration: 60_000,
    stalledInterval: 30_000,
    maxStalledCount: 1,
  });

  worker.on('completed', job => {
    console.log(`[Live Support Worker] Job ${job.id} has completed successfully!`);
  });

  worker.on('failed', (job, err) => {
    console.error(`[Live Support Worker] Job ${job?.id} has failed with ${err.message}`);
  });
  liveSupportWorkerReady = true;
  await redis.set('live-support-worker:ready', new Date().toISOString(), 'EX', 60);
  setInterval(() => void redis.set('live-support-worker:ready', new Date().toISOString(), 'EX', 60).catch(() => undefined), 30_000);
  
  console.log('[Worker] Live Support BullMQ worker started on queue: ai-live-support-turns');
}

async function startCronJobs() {
    // Basic JS Interval as a mock Cron Job for MVP. 
    // Usually BullMQ repeated jobs can handle this, but an interval works fine.
    console.log('[Worker] Commitment Engine Nightly Sweep starting every 24 hours (simulated hourly for testing).');
    setInterval(async () => {
        try {
            await runNightlySweep();
        } catch(e) { console.error('Sweep failed', e); }
    }, 1000 * 60 * 60); // Run every hour
}

async function startWorker() {
  console.log('Worker listening on code-generation-queue (Legacy BRPOP)...');
  
  startNotificationWorker();
  startAIWorker();
  startMindmapsWorker();
  startEssayWorker();
  startLiveSupportWorker();
  startCronJobs();
  
  const aiQueue = new Queue('ai-video-chapters', { connection });
  const mindmapsQueue = new Queue('generate-chapter-mindmaps', { connection });
  const notifQueue = new Queue('notifications', { connection });
  const essayQueue = new Queue('ai-essay-grading', { connection });
  const liveSupportQueue = new Queue('ai-live-support-turns', { connection });

  const app = express();
  if (process.env.NODE_ENV !== 'production') {
    app.use(cors({ origin: process.env.WORKER_ALLOWED_ORIGIN || 'http://localhost:8738' }));
  }
  app.use(express.json());
  app.get('/health', (_req, res) => res.json({ ok: true }));

  app.post('/internal/live-support/preview', requireWorkerAdminToken, async (req, res) => {
    const startedAt = Date.now();
    try {
      const context = req.body as LiveSupportClaimContext;
      let provider = '';
      let model = '';
      const validated = await runLiveSupportAgent(context, async prompt => {
        const inference = await generateLiveSupportReply(prompt);
        provider = inference.provider;
        model = inference.model;
        return inference.decision;
      });
      return res.json({
        decision: validated.decision,
        decisionHash: validated.decisionHash,
        provider,
        model,
        latencyMs: Math.max(0, Date.now() - startedAt),
      });
    } catch (error) {
      const invalidDecision = error instanceof Error &&
        (error.name === 'LiveSupportDecisionValidationError' || error.message === 'AI_DECISION_NOT_JSON');
      return res.status(invalidDecision ? 422 : 503).json({
        error: invalidDecision ? 'AI_PREVIEW_DECISION_INVALID' : 'AI_PREVIEW_UNAVAILABLE',
      });
    }
  });

  app.get('/ready', async (_req, res) => {
    let dbOk = false;
    let redisOk = false;
    let callbackOk = false;
    try {
      await pool.query('SELECT 1');
      dbOk = true;
    } catch (err: any) {
      console.error('[Worker Ready Check] DB failure:', err.message);
    }

    try {
      const pingRes = await redis.ping();
      if (pingRes === 'PONG') {
        redisOk = true;
      }
    } catch (err: any) {
      console.error('[Worker Ready Check] Redis failure:', err.message);
    }

    try {
      const base = (process.env.BACKEND_API_URL || 'http://localhost:5245').replace(/\/$/, '').replace(/\/api\/v1$/, '');
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), 2_000);
      try {
        const response = await fetch(`${base}/api/v1/internal/callbacks/live-support-ai/readiness`, { headers: { 'X-Internal-Token': process.env.AI_CALLBACK_SECRET! }, signal: controller.signal });
        callbackOk = response.ok;
      } finally { clearTimeout(timer); }
    } catch {
      callbackOk = false;
    }

    if (!dbOk || !redisOk || !aiStartupReady || !liveSupportWorkerReady || !callbackOk) {
      return res.status(503).json({
        status: 'unhealthy',
        database: dbOk ? 'healthy' : 'unhealthy',
        redis: redisOk ? 'healthy' : 'unhealthy',
        ai: aiStartupReady ? 'healthy' : 'unhealthy',
        liveSupport: liveSupportWorkerReady ? 'healthy' : 'unhealthy',
        callback: callbackOk ? 'healthy' : 'unhealthy',
      });
    }

    return res.json({
      status: 'healthy',
      database: 'healthy',
      redis: 'healthy',
      ai: 'healthy',
      liveSupport: 'healthy',
      callback: 'healthy',
      timestamp: new Date().toISOString()
    });
  });
  
  // Custom API endpoint to fetch Job Status directly for frontend
  app.get('/api/status/:id', requireWorkerAdminToken, async (req, res) => {
    try {
      const jobId = String(req.params.id);
      let job = await aiQueue.getJob(jobId);
      if (!job) {
          job = await mindmapsQueue.getJob(jobId);
      }
      if (!job) {
          return res.json({ id: jobId, state: 'not_found', progress: 0 });
      }
      const state = await job.getState();
      let progress = typeof job.progress === 'object' && job.progress !== null 
          ? job.progress 
          : { percentage: Number(job.progress) || 0, stage: 'جاري التحضير ووضع المهمة في الطابور...' };
      
      const failedReason = job.failedReason || null;
      
      return res.json({ id: job.id, state, progress, failedReason });
    } catch (e: any) {
        return res.status(500).json({ error: e.message });
    }
  });

  // Cancel Job endpoint
  app.delete('/api/status/:id', requireWorkerAdminToken, async (req, res) => {
    try {
      const jobId = String(req.params.id);
      let job = await aiQueue.getJob(jobId);
      if (!job) {
          job = await mindmapsQueue.getJob(jobId);
      }
      if (job) {
          const cancellation = await markJobCancellation(job);
          return res.json({
            success: true,
            message: cancellation.removed ? 'Job cancelled' : 'Cancellation requested',
            state: cancellation.state
          });
      }
      return res.status(404).json({ success: false, message: 'Job not found' });
    } catch (e: any) {
        return res.status(500).json({ error: e.message });
    }
  });

  // Retry failed Job endpoint
  app.post('/api/status/:id/retry', requireWorkerAdminToken, async (req, res) => {
    try {
      const jobId = String(req.params.id);
      let job = await aiQueue.getJob(jobId);
      if (!job) {
          job = await mindmapsQueue.getJob(jobId);
      }
      if (job && await job.getState() === 'failed') {
          await clearJobCancellation(jobId);
          await job.retry();
          return res.json({ success: true, message: 'Job retried' });
      }
      return res.status(400).json({ success: false, message: 'Job not found or not in failed state' });
    } catch (e: any) {
        return res.status(500).json({ error: e.message });
    }
  });

  // Setup Bull Board
  const serverAdapter = new ExpressAdapter();
  serverAdapter.setBasePath('/ui');
  createBullBoard({
    queues: [
      new BullMQAdapter(aiQueue),
      new BullMQAdapter(mindmapsQueue),
      new BullMQAdapter(notifQueue),
      new BullMQAdapter(essayQueue),
      new BullMQAdapter(liveSupportQueue)
    ],
    serverAdapter: serverAdapter,
  });

  app.use('/ui', requireWorkerAdminToken, serverAdapter.getRouter());
  app.listen(3001, () => {
    console.log('[Worker] Bull Board Dashboard running on http://localhost:3001/ui');
  });

  async function handleStreamMessage(
    messageStreamId: string,
    fields: string[]
  ) {
      const obj: any = {};
      for (let i = 0; i < fields.length; i += 2) {
          const key = fields[i];
          if (key !== undefined) {
              obj[key] = fields[i + 1];
          }
      }

      const { jobType, jobId, payload } = obj;
      if (!jobType || !payload) {
          console.warn(`[Worker] Invalid stream message: ${messageStreamId}`);
          await redis.xack('job-stream', 'worker-group', messageStreamId);
          await redis.xdel('job-stream', messageStreamId);
          return;
      }

      let parsedPayload: any;
      try {
          parsedPayload = JSON.parse(payload);
      } catch (err) {
          console.error(`[Worker] Failed to parse payload for message ${messageStreamId}`, err);
          await redis.xack('job-stream', 'worker-group', messageStreamId);
          await redis.xdel('job-stream', messageStreamId);
          return;
      }

      let targetQueue: Queue;
      let bullmqJobName: string;
      let targetJobId: string;

      if (jobType === 'video analysis') {
          targetQueue = aiQueue;
          bullmqJobName = 'analyze';
          targetJobId = jobId;
      } else if (jobType === 'mind maps') {
          targetQueue = mindmapsQueue;
          bullmqJobName = 'generate';
          const chapId = parsedPayload.chapterId || parsedPayload.ChapterId;
          const vidId = parsedPayload.lessonVideoId || parsedPayload.LessonVideoId;
          targetJobId = chapId ? `${vidId}_mindmap_${chapId}` : `${vidId}_mindmaps`;
      } else if (jobType === 'essay') {
          targetQueue = essayQueue;
          bullmqJobName = 'evaluate';
          targetJobId = jobId;
      } else if (jobType === 'notification') {
          targetQueue = notifQueue;
          if (parsedPayload.WarningId) {
              bullmqJobName = 'send-warning';
          } else if (parsedPayload.ParentPush) {
              bullmqJobName = 'parent-push';
          } else {
              bullmqJobName = 'chat-mention';
          }
          targetJobId = jobId;
      } else if (jobType === 'live support turn') {
          targetQueue = liveSupportQueue;
          bullmqJobName = 'respond';
          targetJobId = jobId;
      } else {
          console.warn(`[Worker] Unknown jobType: ${jobType}`);
          await redis.xack('job-stream', 'worker-group', messageStreamId);
          await redis.xdel('job-stream', messageStreamId);
          return;
      }

      // Ensure BullMQ Job IDs never contain colons to avoid namespace/key errors
      targetJobId = targetJobId.replace(/:/g, '-');

      logQueueEvent('job-stream', `Ingesting ${jobType} job to BullMQ`, { jobId: targetJobId });

      // Remove any existing job with the same ID to allow re-running/retrying the job cleanly
      try {
          const existingJob = await targetQueue.getJob(targetJobId);
          if (existingJob) {
              await existingJob.remove();
          }
      } catch (err: any) {
          console.warn(`[Worker] Failed to remove existing job ${targetJobId}:`, err.message);
      }

      // Clear any cancellation marker in Redis
      try {
          await clearJobCancellation(targetJobId);
      } catch (err: any) {
          console.warn(`[Worker] Failed to clear cancellation for job ${targetJobId}:`, err.message);
      }

      try {
          const isLiveSupportTurn = jobType === 'live support turn';
          await targetQueue.add(bullmqJobName, parsedPayload, {
              jobId: targetJobId,
              ...JOB_RETENTION_OPTIONS,
              attempts: isLiveSupportTurn ? 4 : 5,
              backoff: {
                  type: 'exponential',
                  delay: isLiveSupportTurn ? 2000 : 5000
              }
          });

          await redis.xack('job-stream', 'worker-group', messageStreamId);
          await redis.xdel('job-stream', messageStreamId);
      } catch (err: any) {
          console.error(`[Worker] Failed to enqueue job ${targetJobId} into BullMQ: ${err.message}`);
      }
  }

  (async () => {
      const consumerName = `worker-consumer-${crypto.randomUUID().substring(0, 8)}`;
      console.log(`[Worker] Starting Redis Stream consumer ${consumerName} on job-stream...`);

      try {
          await redis.xgroup('CREATE', 'job-stream', 'worker-group', '0', 'MKSTREAM');
          console.log('[Worker] Created consumer group worker-group for job-stream');
      } catch (err: any) {
          if (!err.message.includes('BUSYGROUP')) {
              console.error('[Worker] Error creating consumer group:', err.message);
          }
      }

      while (true) {
          try {
              const pendingData = (await redis.xreadgroup(
                  'GROUP', 'worker-group', consumerName,
                  'COUNT', '10',
                  'STREAMS', 'job-stream',
                  '0'
              )) as any;

              if (pendingData && pendingData.length > 0) {
                  const [_, messages] = pendingData[0];
                  if (messages && messages.length > 0) {
                      console.log(`[Worker] Processing ${messages.length} pending messages from backlog...`);
                      for (const [messageStreamId, fields] of messages) {
                          await handleStreamMessage(messageStreamId, fields);
                      }
                      continue;
                  }
              }

              const newData = (await redis.xreadgroup(
                  'GROUP', 'worker-group', consumerName,
                  'COUNT', '10',
                  'BLOCK', '2000',
                  'STREAMS', 'job-stream',
                  '>'
              )) as any;

              if (newData && newData.length > 0) {
                  const [_, messages] = newData[0];
                  if (messages && messages.length > 0) {
                      for (const [messageStreamId, fields] of messages) {
                          await handleStreamMessage(messageStreamId, fields);
                      }
                  }
              }
          } catch (e: any) {
              console.error('[Worker] Redis Stream consumer loop error:', e.message);
              await new Promise(r => setTimeout(r, 5000));
          }
      }
  })();
}

await validateAIStartup();
await startWorker();

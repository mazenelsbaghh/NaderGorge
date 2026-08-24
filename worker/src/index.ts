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
import { validateWorkerSecurityConfig } from './security.js';
import { installSystemLogCapture, logError, logInfo } from './logging.js';
import { markJobCancellation, clearJobCancellation } from './cancellation.js';
import { createWorkerAdminGuard, isWorkerAdminEnabled } from './server/adminAccess.js';
import { ingestStreamJob, type QueueSet } from './queues/jobIngestion.js';
import { claimStaleStreamMessages } from './queues/streamRecovery.js';
import { readAIConfig } from './services/aiConfig.js';
import { generateLiveSupportReply } from './services/geminiService.js';
import { runLiveSupportAgent, type LiveSupportClaimContext } from './services/liveSupportAgent.js';
import { fetchWithTimeout } from './services/workerFetch.js';
import { createRedisConnection, redisConnectionOptions } from './config/redis.js';
import { monitorRedisSentinelAvailability } from './config/redisAvailabilityMonitor.js';
import { scheduleClusterCron } from './scheduling/clusterCron.js';
import { databaseUrl } from './config/database.js';
import { runBirthdaySweep } from './scripts/birthday-congratulator.js';
import { delayUntilNextCairoMidnight } from './scheduling/cairoTime.js';
import { publicJobFailureReason } from './server/jobStatus.js';
import { reportTerminalVideoFailure } from './services/videoAnalysisFailureReporter.js';

dotenv.config();
validateWorkerSecurityConfig();
let aiStartupReady = false;
let liveSupportWorkerReady = false;
let adminAIWorkerReady = false;
const adminAIEnabled = process.env.ADMIN_AI_ENABLED?.trim().toLowerCase() === 'true';

async function validateAIStartup() {
  const config = readAIConfig();
  aiStartupReady = true;
  console.log('[AI startup] Gemini Developer API configuration validated.', {
    provider: config.primaryProvider,
  });
}

const JOB_RETENTION_OPTIONS = {
  removeOnComplete: { count: 1000, age: 7 * 24 * 3600 },
  removeOnFail: { count: 500, age: 14 * 24 * 3600 },
};

const redis = createRedisConnection();
installSystemLogCapture(redis);
monitorRedisSentinelAvailability(redis);
const pool = new Pool({
  connectionString: databaseUrl()
});

// BullMQ Connection Shared config
const connection = redisConnectionOptions();

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

    const res = await fetchWithTimeout(`${backendBaseUrl}/api/v1/internal/callbacks/ai-progress`, {
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

    const res = await fetchWithTimeout(`${backendBaseUrl}/api/v1/internal/callbacks/ai-progress`, {
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
  }, {
    connection,
    lockDuration: 10 * 60_000,
    lockRenewTime: 30_000,
    stalledInterval: 60_000,
    maxStalledCount: 2,
  });

  worker.on('progress', (job, progress) => {
    reportProgressToBackend(job.id!, progress);
  });

  worker.on('completed', job => {
    console.log(`[AI Worker] Job ${job.id} has completed successfully!`);
  });

  worker.on('failed', async (job, err) => {
    logError('ai-video-worker', 'Video analysis job failed.', { jobId: job?.id, errorName: err.name });
    try {
      await reportTerminalVideoFailure(job, err);
    } catch (callbackError) {
      logError('ai-video-callback', 'Terminal failure callback exhausted its retry budget.', {
        jobId: job?.id,
        errorName: callbackError instanceof Error ? callbackError.name : 'UnknownError',
      });
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

async function startAdminAIWorker() {
  const worker = new Worker('ai-admin-agent-turns', async (job) => {
    const processor = await import('./jobs/processAdminAITurn.js');
    return processor.default(job);
  }, {
    connection,
    concurrency: Math.max(1, Number.parseInt(process.env.AI_ADMIN_AGENT_CONCURRENCY || '2', 10) || 2),
    lockDuration: 60_000,
    stalledInterval: 30_000,
    maxStalledCount: 1,
  });
  worker.on('completed', job => console.log(`[Admin AI Worker] Job ${job.id} completed.`));
  worker.on('failed', (job, error) => console.error(`[Admin AI Worker] Job ${job?.id} failed.`, { name: error.name }));
  adminAIWorkerReady = true;
  const heartbeat = () => redis.set('admin-ai-worker:ready', new Date().toISOString(), 'EX', 60).catch(() => undefined);
  await heartbeat(); setInterval(() => void heartbeat(), 30_000);
  console.log('[Worker] Admin AI BullMQ worker started on queue: ai-admin-agent-turns');
}

async function startCronJobs() {
    const runAtNextCairoDay = () => {
      const now = new Date();
      const parts = new Intl.DateTimeFormat('en-CA', {
        timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit',
      }).formatToParts(now);
      const value = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find((part) => part.type === type)?.value);
      const nextLocal = new Date(Date.UTC(value('year'), value('month') - 1, value('day') + 1, 2, 5));
      const offsetName = new Intl.DateTimeFormat('en-US', {
        timeZone: 'Africa/Cairo', timeZoneName: 'longOffset',
      }).formatToParts(nextLocal).find((part) => part.type === 'timeZoneName')?.value ?? 'GMT+00:00';
      const offset = /GMT([+-])(\d{2}):(\d{2})/.exec(offsetName);
      const offsetMilliseconds = offset
        ? (Number(offset[2]) * 60 + Number(offset[3])) * 60_000 * (offset[1] === '+' ? 1 : -1)
        : 0;
      return Math.max(0, nextLocal.getTime() - offsetMilliseconds - now.getTime());
    };
    console.log('[Worker] Commitment Engine Nightly Sweep scheduled for 02:05 Africa/Cairo.');
    scheduleClusterCron(pool, {
      leaseName: 'commitment-engine-nightly',
      ownerToken: crypto.randomUUID(),
      leaseLifetimeMs: 3 * 60 * 60 * 1000,
      delayUntilNextRun: runAtNextCairoDay,
      task: runNightlySweep,
    });

    console.log('[Worker] Birthday celebration sweep scheduled for 00:00 Africa/Cairo.');
    scheduleClusterCron(pool, {
      leaseName: 'student-birthday-midnight',
      ownerToken: crypto.randomUUID(),
      leaseLifetimeMs: 30 * 60 * 1000,
      delayUntilNextRun: delayUntilNextCairoMidnight,
      task: async ({ signal }) => {
        if (signal.aborted) return;
        await runBirthdaySweep(pool);
      },
    });
}

async function startWorker() {
  console.log('Worker listening on code-generation-queue (Legacy BRPOP)...');
  
  startNotificationWorker();
  startAIWorker();
  startMindmapsWorker();
  startEssayWorker();
  startLiveSupportWorker();
  if (adminAIEnabled) startAdminAIWorker();
  startCronJobs();
  
  const aiQueue = new Queue('ai-video-chapters', { connection });
  const mindmapsQueue = new Queue('generate-chapter-mindmaps', { connection });
  const notifQueue = new Queue('notifications', { connection });
  const essayQueue = new Queue('ai-essay-grading', { connection });
  const liveSupportQueue = new Queue('ai-live-support-turns', { connection });
  const adminAIQueue = new Queue('ai-admin-agent-turns', { connection });
  const queues: QueueSet = { aiQueue, mindmapsQueue, notifQueue, essayQueue, liveSupportQueue, adminAIQueue };
  const workerAdminGuard = createWorkerAdminGuard();

  const app = express();
  if (process.env.NODE_ENV !== 'production') {
    app.use(cors({ origin: process.env.WORKER_ALLOWED_ORIGIN || 'http://localhost:8738' }));
  }
  app.use(express.json());
  app.get('/health', (_req, res) => res.json({ ok: true }));

  app.post('/internal/live-support/preview', workerAdminGuard, async (req, res) => {
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
      const response = await fetchWithTimeout(`${base}/api/v1/internal/callbacks/live-support-ai/readiness`, {
        headers: { 'X-Internal-Token': process.env.AI_CALLBACK_SECRET! },
        timeoutMs: 2_000,
      });
      callbackOk = response.ok;
    } catch {
      callbackOk = false;
    }

    let adminAICallbackOk = !adminAIEnabled;
    if (adminAIEnabled) {
      try {
        const base = (process.env.BACKEND_API_URL || 'http://localhost:5245').replace(/\/$/, '').replace(/\/api\/v1$/, '');
        const response = await fetchWithTimeout(`${base}/api/v1/internal/admin-ai/readiness`, { headers: { 'X-Internal-Token': process.env.AI_CALLBACK_SECRET! }, timeoutMs: 2_000, maxResponseBytes: 16_384 });
        adminAICallbackOk = response.ok;
      } catch { adminAICallbackOk = false; }
    }

    if (!dbOk || !redisOk || !aiStartupReady || !liveSupportWorkerReady || (adminAIEnabled && (!adminAIWorkerReady || !adminAICallbackOk)) || !callbackOk) {
      return res.status(503).json({
        status: 'unhealthy',
        database: dbOk ? 'healthy' : 'unhealthy',
        redis: redisOk ? 'healthy' : 'unhealthy',
        ai: aiStartupReady ? 'healthy' : 'unhealthy',
        liveSupport: liveSupportWorkerReady ? 'healthy' : 'unhealthy',
        adminAI: !adminAIEnabled ? 'disabled' : adminAIWorkerReady ? 'healthy' : 'unhealthy',
        callback: callbackOk ? 'healthy' : 'unhealthy',
        adminAICallback: !adminAIEnabled ? 'disabled' : adminAICallbackOk ? 'healthy' : 'unhealthy',
      });
    }

    return res.json({
      status: 'healthy',
      database: 'healthy',
      redis: 'healthy',
      ai: 'healthy',
      liveSupport: 'healthy',
      adminAI: adminAIEnabled ? 'healthy' : 'disabled',
      callback: 'healthy',
      adminAICallback: adminAIEnabled ? 'healthy' : 'disabled',
      timestamp: new Date().toISOString()
    });
  });
  
  // Custom API endpoint to fetch Job Status directly for frontend
  app.get('/api/status/:id', workerAdminGuard, async (req, res) => {
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
      
      const failedReason = publicJobFailureReason(job.failedReason, state);
      
      return res.json({ id: job.id, state, progress, failedReason });
    } catch {
        return res.status(500).json({ error: 'WORKER_STATUS_UNAVAILABLE' });
    }
  });

  // Cancel Job endpoint
  app.delete('/api/status/:id', workerAdminGuard, async (req, res) => {
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
  app.post('/api/status/:id/retry', workerAdminGuard, async (req, res) => {
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

  if (isWorkerAdminEnabled()) {
    const serverAdapter = new ExpressAdapter();
    serverAdapter.setBasePath('/ui');
    createBullBoard({
      queues: [
        new BullMQAdapter(aiQueue),
        new BullMQAdapter(mindmapsQueue),
        new BullMQAdapter(notifQueue),
        new BullMQAdapter(essayQueue),
        new BullMQAdapter(liveSupportQueue),
        new BullMQAdapter(adminAIQueue)
      ],
      serverAdapter: serverAdapter,
    });
    app.use('/ui', workerAdminGuard, serverAdapter.getRouter());
  }

  app.listen(3001, () => {
    logInfo('worker', isWorkerAdminEnabled() ? 'Worker HTTP server running with admin UI enabled.' : 'Worker HTTP server running with admin UI disabled.', { port: 3001 });
  });

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
              await claimStaleStreamMessages(redis, queues, consumerName);
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
                          await ingestStreamJob(redis, queues, messageStreamId, fields);
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
                          await ingestStreamJob(redis, queues, messageStreamId, fields);
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

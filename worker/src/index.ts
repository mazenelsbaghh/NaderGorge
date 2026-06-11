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
import { markJobCancellation } from './cancellation.js';

dotenv.config();
validateWorkerSecurityConfig();

const DEFAULT_REDIS_URL = process.env.REDIS_URL || 'redis://localhost:6379';
const JOB_RETENTION_OPTIONS = {
  removeOnComplete: { count: 1000, age: 7 * 24 * 3600 },
  removeOnFail: { count: 500, age: 14 * 24 * 3600 },
};

const redis = new Redis(DEFAULT_REDIS_URL);
const pool = new Pool({
  connectionString: process.env.DATABASE_URL || process.env.DB_CONNECTION_STRING || 'postgresql://postgres:postgres@localhost:5435/nadergorge?schema=public'
});

async function processJob(json: string) {
  const payload = JSON.parse(json);
  const { packageId, lessonId, count, codeLength, adminId } = payload;
  const groupId = crypto.randomUUID();
  
  const client = await pool.connect();
  try {
    await client.query('BEGIN');
    
    // Create CodeGroup
    await client.query(
      `INSERT INTO code_groups ("Id", "Name", "TotalCodes", "PackageId", "LessonId", "CreatedByUserId", "CreatedAt", "UpdatedAt") 
       VALUES ($1, $2, $3, $4, $5, $6, NOW(), NOW())`,
      [groupId, `Batch-${Date.now()}`, count, packageId, lessonId, adminId]
    );

    // Batch generate and insert
    const insertValues = [];
    const BATCH_SIZE = 5000;
    let valuesClause = [];
    let bindIdx = 1;

    for (let i = 0; i < count; i++) {
        // Simple random alphanum
        const code = crypto.randomBytes(codeLength / 2 || 4).toString('hex').toUpperCase();
        const codeHash = crypto.createHash('sha256').update(code, 'utf8').digest('base64');
        const codeId = crypto.randomUUID();
        
        insertValues.push(codeId, codeHash, code, groupId, false);
        valuesClause.push(`($${bindIdx++}, $${bindIdx++}, $${bindIdx++}, $${bindIdx++}, $${bindIdx++}, NOW(), NOW())`);

        if (insertValues.length >= BATCH_SIZE * 5 || i === count - 1) { // 5 params
            const query = `INSERT INTO access_codes ("Id", "CodeHash", "CodePlaintext", "CodeGroupId", "IsConsumed", "CreatedAt", "UpdatedAt") VALUES ${valuesClause.join(', ')}`;
            await client.query(query, insertValues);
            insertValues.length = 0;
            valuesClause.length = 0;
            bindIdx = 1;
        }
    }

    await client.query('COMMIT');
    console.log(`Generated ${count} codes for group ${groupId}`);
  } catch (error) {
    await client.query('ROLLBACK');
    console.error('Job failed', error);
  } finally {
    client.release();
  }
}

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
    if (job) {
      reportFailureToBackend(job.id!, err.message);
    }
  });
  
  console.log('[Worker] Mindmaps BullMQ worker started on queue: generate-chapter-mindmaps');
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
  startCronJobs();
  
  const aiQueue = new Queue('ai-video-chapters', { connection });
  const mindmapsQueue = new Queue('generate-chapter-mindmaps', { connection });
  const notifQueue = new Queue('notifications', { connection });
  const essayQueue = new Queue('ai-essay-grading', { connection });

  const app = express();
  if (process.env.NODE_ENV !== 'production') {
    app.use(cors({ origin: process.env.WORKER_ALLOWED_ORIGIN || 'http://localhost:8738' }));
  }
  app.use(express.json());
  app.get('/health', (_req, res) => res.json({ ok: true }));
  
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
      new BullMQAdapter(essayQueue)
    ],
    serverAdapter: serverAdapter,
  });

  app.use('/ui', requireWorkerAdminToken, serverAdapter.getRouter());
  app.listen(3001, () => {
    console.log('[Worker] Bull Board Dashboard running on http://localhost:3001/ui');
  });

  // Dedicated loop for AI ingestion from .NET
  (async () => {
    console.log('Worker listening on ai-video-queue...');
    while (true) {
        try {
            const result = await redis.brpop('ai-video-queue', 0);
            if (result) {
                const wrapper = JSON.parse(result[1]);
                const payload = wrapper.data || wrapper; // Fallback to raw if no wrapper
                logQueueEvent('ai-video-queue', 'Enqueueing BullMQ job', { lessonVideoId: payload.lessonVideoId });
                await aiQueue.add('analyze', payload, {
                    jobId: payload.lessonVideoId,
                    ...JOB_RETENTION_OPTIONS,
                });
            }
        } catch (e) {
            console.error('AI Redis loop error', e);
            await new Promise(r => setTimeout(r, 5000));
        }
    }
  })();

  const mindmapsSubRedis = new Redis(DEFAULT_REDIS_URL);
  
  // Dedicated loop for Mindmaps ingestion from .NET
  (async () => {
    console.log('Worker listening on ai-mindmaps-queue...');
    while (true) {
        try {
            const result = await mindmapsSubRedis.brpop('ai-mindmaps-queue', 0);
            if (result) {
                let wrapper;
                try {
                    wrapper = JSON.parse(result[1]);
                } catch(err) {
                    console.error("JSON PARSE ERROR", err);
                    continue;
                }
                const payload = wrapper.data || wrapper;
                const chapId = payload.chapterId || payload.ChapterId;
                const vidId = payload.lessonVideoId || payload.LessonVideoId;
                // Use chapterId-based jobId for single-chapter regen, videoId for batch
                const jobId = chapId
                    ? `${vidId}_mindmap_${chapId}`
                    : `${vidId}_mindmaps`;
                logQueueEvent('ai-mindmaps-queue', 'Enqueueing BullMQ job', { jobId, lessonVideoId: vidId, chapterId: chapId });
                await mindmapsQueue.add('generate', payload, {
                    jobId,
                    ...JOB_RETENTION_OPTIONS,
                });
            }
        } catch (e) {
            console.error('Mindmaps Redis loop error', e);
            await new Promise(r => setTimeout(r, 5000));
        }
    }
  })();

  const essaySubRedis = new Redis(DEFAULT_REDIS_URL);
  
  // Dedicated loop for Essay Ingestion from .NET
  (async () => {
    console.log('Worker listening on ai-essay-queue...');
    while (true) {
        try {
            const result = await essaySubRedis.brpop('ai-essay-queue', 0);
            if (result) {
                let wrapper;
                try {
                    wrapper = JSON.parse(result[1]);
                } catch(err) {
                    console.error("JSON PARSE ERROR", err);
                    continue;
                }
                const payload = wrapper.data || wrapper;
                logQueueEvent('ai-essay-queue', 'Enqueueing BullMQ job', { essaySubmissionId: payload.essaySubmissionId });
                await essayQueue.add('evaluate', payload, {
                    jobId: payload.essaySubmissionId,
                    ...JOB_RETENTION_OPTIONS,
                });
            }
        } catch (e) {
            console.error('Essay Redis loop error', e);
            await new Promise(r => setTimeout(r, 5000));
        }
    }
  })();
}

startWorker();

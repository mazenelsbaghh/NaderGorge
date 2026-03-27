import { Redis } from 'ioredis';
import { Pool } from 'pg';
import crypto from 'crypto';
import dotenv from 'dotenv';
import { Worker } from 'bullmq';
import { runNightlySweep } from './jobs/commitment-engine.js';
import { processNotificationJob } from './jobs/notification-sender.js';

dotenv.config();

const redis = new Redis(process.env.REDIS_URL || 'redis://localhost:6379');
const pool = new Pool({
  connectionString: process.env.DATABASE_URL || 'postgresql://postgres:postgres@localhost:5432/nadergorge?schema=public'
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
        const codeId = crypto.randomUUID();
        
        insertValues.push(codeId, code, code, groupId, false);
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
  host: new URL(process.env.REDIS_URL || 'redis://localhost:6379').hostname,
  port: parseInt(new URL(process.env.REDIS_URL || 'redis://localhost:6379').port) || 6379,
  username: new URL(process.env.REDIS_URL || 'redis://localhost:6379').username || undefined,
  password: new URL(process.env.REDIS_URL || 'redis://localhost:6379').password || undefined,
};

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
  startCronJobs();
  
  while (true) {
    try {
      // Blocking pop for Legacy
      const result = await redis.brpop('code-generation-queue', 0);
      if (result) {
        console.log('Processing new legcy code-generation job...');
        await processJob(result[1]);
      }
    } catch (e) {
      console.error('Redis loop error', e);
      await new Promise(r => setTimeout(r, 5000));
    }
  }
}

startWorker();

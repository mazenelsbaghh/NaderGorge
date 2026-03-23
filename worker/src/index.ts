import { Redis } from 'ioredis';
import { Pool } from 'pg';
import crypto from 'crypto';
import dotenv from 'dotenv';
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
      `INSERT INTO code_groups (id, name, total_codes, package_id, lesson_id, created_by_user_id, created_at, updated_at) 
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
            const query = `INSERT INTO access_codes (id, code_hash, code_plaintext, code_group_id, is_consumed, created_at, updated_at) VALUES ${valuesClause.join(', ')}`;
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

async function startWorker() {
  console.log('Worker listening on code-generation-queue...');
  while (true) {
    try {
      // Blocking pop
      const result = await redis.brpop('code-generation-queue', 0);
      if (result) {
        console.log('Processing new job...');
        await processJob(result[1]);
      }
    } catch (e) {
      console.error('Redis loop error', e);
      await new Promise(r => setTimeout(r, 5000));
    }
  }
}

startWorker();

import { Pool } from 'pg';
import crypto from 'crypto';
import dotenv from 'dotenv';
import { databaseUrl } from '../config/database.js';
dotenv.config();

const pool = new Pool({
  connectionString: databaseUrl()
});

const cairoDateFormatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Africa/Cairo',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

function cairoDateKey(now = new Date()) {
  const parts = cairoDateFormatter.formatToParts(now);
  const value = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value;
  return `${value('year')}-${value('month')}-${value('day')}`;
}

export async function runNightlySweep(context?: { signal: AbortSignal }) {
    console.log('[CommitmentEngine] Starting nightly student status evaluation sweep...');
    const client = await pool.connect();
    try {
        context?.signal.throwIfAborted();
        await client.query('BEGIN');
        
        // MVP Sweep Logic:
        // 1. Identify students who haven't logged in for 7 days
        // 2. Insert warning events for them
        
        const res = await client.query(`
            SELECT "Id" FROM "users" 
            WHERE "Id" IN (SELECT "UserId" FROM "user_roles" r JOIN "roles" rol ON r."RoleId" = rol."Id" WHERE rol."Name" = 'Student')
            AND "Id" NOT IN (
                SELECT "StudentId" FROM "student_status_trackers" 
                WHERE "LastActiveAt" >= NOW() - INTERVAL '7 days'
            )
        `);
        
        const inactiveStudents = res.rows;
        
        if (inactiveStudents.length > 0) {
            console.log(`[CommitmentEngine] Found ${inactiveStudents.length} inactive students. Generating warnings.`);
            
            for (const student of inactiveStudents) {
                context?.signal.throwIfAborted();
                const warningId = crypto.randomUUID();
                const dateStr = cairoDateKey();
                const occurrenceKey = `commitment:${student.Id}:inactive_7d:${dateStr}`;
                await client.query(`
                    INSERT INTO "warning_events" ("Id", "StudentId", "Severity", "TriggerReason", "IsResolved", "OccurrenceKey", "CreatedAt")
                    VALUES ($1, $2, $3, $4, $5, $6, NOW())
                    ON CONFLICT ("OccurrenceKey") DO NOTHING
                `, [warningId, student.Id, 1, 'Inactive for more than 7 days', false, occurrenceKey]);
            }
        } else {
            console.log('[CommitmentEngine] No inactive students found.');
        }

        context?.signal.throwIfAborted();
        await client.query('COMMIT');
        console.log('[CommitmentEngine] Sweep completed successfully.');
    } catch (error) {
        await client.query('ROLLBACK');
        console.error('[CommitmentEngine] Sweep failed', error);
        throw error;
    } finally {
        client.release();
    }
}

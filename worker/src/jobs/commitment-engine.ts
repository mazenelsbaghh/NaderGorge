import crypto from 'crypto';
import dotenv from 'dotenv';
import { databasePool } from '../config/database.js';
import { inactiveStudentsSql, inactivityReason, resolveActiveWarningsSql } from './commitment-activity.js';
dotenv.config();

const pool = databasePool();

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
        
        // Serialize repeated/manual sweeps as well as the scheduled cluster job.
        await client.query("SELECT pg_advisory_xact_lock(hashtext('commitment-inactivity-sweep'))");
        await client.query(resolveActiveWarningsSql, [inactivityReason]);
        const res = await client.query(inactiveStudentsSql, [inactivityReason]);

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
                `, [warningId, student.Id, 1, inactivityReason, false, occurrenceKey]);
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

import { Pool } from 'pg';
import crypto from 'crypto';
import dotenv from 'dotenv';
dotenv.config();

const pool = new Pool({
  connectionString: process.env.DATABASE_URL || 'postgresql://postgres:postgres@localhost:5432/nadergorge?schema=public'
});

export async function runNightlySweep() {
    console.log('[CommitmentEngine] Starting nightly student status evaluation sweep...');
    const client = await pool.connect();
    try {
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
                const warningId = crypto.randomUUID();
                await client.query(`
                    INSERT INTO "warning_events" ("Id", "StudentId", "Severity", "TriggerReason", "IsResolved", "CreatedAt")
                    VALUES ($1, $2, $3, $4, $5, NOW())
                `, [warningId, student.Id, 1, 'Inactive for more than 7 days', false]); // 1 = Medium severity usually
            }
        } else {
            console.log('[CommitmentEngine] No inactive students found.');
        }

        await client.query('COMMIT');
        console.log('[CommitmentEngine] Sweep completed successfully.');
    } catch (error) {
        await client.query('ROLLBACK');
        console.error('[CommitmentEngine] Sweep failed', error);
    } finally {
        client.release();
    }
}

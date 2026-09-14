import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { test } from 'node:test';
import { databasePool } from '../config/database.js';
import { runNightlySweep } from './commitment-engine.js';
import { inactivityReason } from './commitment-activity.js';

// Run explicitly against a disposable database with repository EF migrations applied.
test('2026-09-13 activity without a tracker resolves false warnings and inactivity is not repeated', async () => {
    assert.ok(process.env.DATABASE_URL, 'A migrated disposable DATABASE_URL is required');
    const pool = databasePool();
    pool.options.max = 1;
    try {
        // Copy migrated column/index definitions; unrelated content foreign keys are outside this sweep.
        for (const table of ['users', 'roles', 'user_roles', 'student_status_trackers', 'devices',
            'video_watch_events', 'student_exam_attempts', 'homework_submissions', 'warning_events']) {
            await pool.query(`CREATE TEMP TABLE ${table} (LIKE public.${table} INCLUDING ALL)`);
        }
        const roleId = crypto.randomUUID();
        await pool.query('INSERT INTO roles ("Id", "Name", "Type", "CreatedAt") VALUES ($1, \'Student\', 0, NOW())', [roleId]);
        const ids = Array.from({ length: 6 }, () => crypto.randomUUID());
        for (const [index, id] of ids.entries()) {
            await pool.query(`INSERT INTO users ("Id", "FullName", "PhoneNumber", "PasswordHash", "IsActive", "IsProfileComplete", "CreatedAt")
                VALUES ($1, 'Regression student', $2, 'test-only', TRUE, TRUE, NOW() - $3::interval)`,
            [id, `fixture-${index}`, index === 2 ? '1 day' : '30 days']);
            await pool.query('INSERT INTO user_roles VALUES ($1, $2)', [id, roleId]);
        }
        const [watching, inactive, newlyRegistered, loggedIn, homework, exam] = ids;
        await pool.query(`INSERT INTO video_watch_events ("Id", "UserId", "LessonVideoId", "WatchCount",
            "TimeWatchedInSeconds", "ActualWatchedSeconds", "IsLocked", "CreatedAt", "UpdatedAt")
            VALUES ($1, $2, $3, 0, 18, 15, FALSE, NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days')`,
        [crypto.randomUUID(), watching, crypto.randomUUID()]);
        await pool.query(`INSERT INTO devices ("Id", "UserId", "DeviceFingerprint", "LastUsedAt", "IsActive", "CreatedAt")
            VALUES ($1, $2, 'fixture', NOW(), TRUE, NOW())`, [crypto.randomUUID(), loggedIn]);
        await pool.query(`INSERT INTO homework_submissions ("Id", "StudentId", "HomeworkId", "StartedAt", "Status", "OverallScore")
            VALUES ($1, $2, $3, NOW(), 0, 0)`, [crypto.randomUUID(), homework, crypto.randomUUID()]);
        await pool.query(`INSERT INTO student_exam_attempts ("Id", "UserId", "ExamId", "CreatedAt", "ScoreAchieved", "IsPassed", "IsTimeExpired")
            VALUES ($1, $2, $3, NOW(), 0, FALSE, FALSE)`, [crypto.randomUUID(), exam, crypto.randomUUID()]);
        for (const reason of [inactivityReason, 'Unrelated academic warning']) {
            await pool.query(`INSERT INTO warning_events ("Id", "StudentId", "Severity", "TriggerReason", "IsResolved", "CreatedAt")
                VALUES ($1, $2, 1, $3, FALSE, NOW())`, [crypto.randomUUID(), watching, reason]);
        }
        await runNightlySweep();
        await runNightlySweep();
        const warnings = (await pool.query('SELECT "StudentId", "TriggerReason", "IsResolved" FROM warning_events')).rows;
        assert.equal(warnings.length, 3);
        assert.equal(warnings.find(w => w.StudentId === watching && w.TriggerReason === inactivityReason)?.IsResolved, true);
        assert.equal(warnings.find(w => w.TriggerReason === 'Unrelated academic warning')?.IsResolved, false);
        assert.deepEqual(warnings.filter(w => !w.IsResolved && w.TriggerReason === inactivityReason).map(w => w.StudentId), [inactive]);
        assert.ok(warnings.every(w => w.StudentId !== newlyRegistered));
    } finally {
        await pool.end();
    }
});

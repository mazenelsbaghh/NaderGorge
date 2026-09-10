# Nader first lesson: historical progress correction

Prepared locally at the owner's request. **Do not run against production until the owner authorizes release and the data correction.** This is a separate operation from deployment; applying EF migrations does not change student progress.

Target: `086d664a-f784-4cdd-a0c8-09948619bef4`, «المحاضرة الاولي: بناء الدولة المصرية الجزء الاول». The screenshot's second lesson is not targeted.

Prerequisites:

- Apply the repository EF migration `AddHistoricalLearningDuration` before using the script.
- Choose and review an explicit `before_utc` timestamp including its timezone. The tracking migration timestamp alone is not evidence of the deployment time.
- Use the repository production SSH workflow, a reviewed backup, and an audit actor's user ID. Do not copy connection secrets into commands or reports.
- Preview the four video durations and eligible audience. The script stops if fewer than 20 historical duration observations per video exist or their duration spread exceeds one second. It does not infer duration from elapsed watch time.

With `psql` connected through the reviewed transport, supply `-v before_utc=...` and execute [nader-first-lesson-progress.sql](nader-first-lesson-progress.sql). `apply` defaults to false: only temporary tables are written, and the transaction rolls back. The output contains duration evidence and counts, not student names or phone numbers.

After reviewing the preview and receiving release authorization, use the same cutoff plus `-v apply=true -v actor_id=... -v expected_students=...`. The count must match the preview. The script records per-row before/after learning values in `audit_logs`, then raises each eligible student's four video progress records to at least 95%. Existing higher values survive. It does not increase actual viewing time, quota counters, or fabricate playback sessions. Previously missing watch rows receive zero actual time and zero quota counts.

Eligibility requires a positive recorded watch time or count on this lesson, in a watch record created before the reviewed cutoff. Students without that evidence are excluded. Duration falls back to a historical observation only when the student's current asset/session evidence is missing. Newly recorded actual duration takes precedence in application reads.

Re-run the same preview after application; unchanged rows are excluded. Do not blindly reverse the update if students have watched since the correction. Compare current values against the audit evidence first.

Local verification used a disposable PostgreSQL 16 database with repository migrations: preview left rows unchanged, application preserved higher progress and actual/quota counters, and the second preview found zero changes. The application integration test also verifies that historical duration does not leak to other students and that subsequent session duration takes precedence.

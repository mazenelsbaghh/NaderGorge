# Phase 0: Research & Architecture Decisions

## Unknowns & Clarifications

1. **How to orchestrate multi-stage AI tasks in Node.js?**
   - *Alternative A*: LangGraph JS. Provides a robust state graph for agents, but adds significant complexity and doesn't natively expose stage progress to our existing BullMQ dashboard.
   - *Alternative B*: BullMQ Flows. Provides parent/child job chaining. Very robust, but requires breaking the current `analyzeVideoChapters` logic into 3 separate BullMQ worker queues (Extract, Analyze, Sync), which scatters the logic.
   - *Alternative C*: Stateful Job execution (Idempotent Steps within a single BullMQ Job). Since BullMQ supports job retries, we can store intermediate state (e.g., `audioFilePath`) in Redis or the job's `data` payload. When a job fails and is retried, it checks if `audioFilePath` exists and skips extraction, going straight to AI Analysis.
   - *Decision*: **Alternative C (Stateful Idempotent Job)**. It maintains the current worker architecture, natively supports the `Retry` button the admin already has, natively integrates with `job.updateProgress`, and fulfills the fault-tolerance requirement without introducing heavyweight frameworks like LangGraph for a linear 3-step pipeline.

2. **How to persist intermediate state for retries?**
   - *Decision*: We will use BullMQ's `job.updateData()` to save the `audioPath` back to the job payload once extraction finishes. When the job is manually retried by the admin, `job.data.audioPath` will already be populated, allowing the code to bypass `extractAudioFromVideo`.

3. **How does the frontend track these descriptive stages?**
   - *Decision*: BullMQ's `job.updateProgress()` accepts an object, not just a number! We can update progress as `{ percentage: 10, stage: 'Downloading Video...' }`. We will update the `worker` API and `LessonVideoList.tsx` to read `progress.percentage` and `progress.stage` to display detailed Arabic text to the user.

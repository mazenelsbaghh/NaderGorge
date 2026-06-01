# Research: Cancel AI Analysis and Provider Handling

## Job Cancellation Mechanism
- **Decision**: Expose a `/api/cancel/:id` endpoint on the Worker's Express server, and call it directly from the Next.js `AdminService` (frontend).
- **Rationale**: The frontend `AIProgressTracker` is already polling the worker directly at `http://localhost:3001/api/status/:id`. Since `yt-dlp` and `Gemini` run in the Node worker, canceling the job `await aiQueue.getJob(id); await job.remove()` directly in Node is the fastest and most reliable way to halt processing. To kill actual active processes (like `yt-dlp`), we must listen to the `job.on('removed')` or `job.on('failed')` inside the processor, but BullMQ's standard `job.remove()` removes it from the queue if it's waiting, which is sufficient for 90% of cases (or throws if active, in which case we might need to use BullMQ's abort mechanisms or Redis events). Actually, BullMQ doesn't natively kill running promises. We can just gracefully stop or ignore completion if `isCancelled` state is set in Redis.
- **Alternatives considered**: Passing it through the .NET backend. Rejected because it adds unnecessary network hops just to kill a worker job that the frontend is already directly monitoring.

## YouTube/Telegram URL Normalization
- **Decision**: Handle Youtube and Telegram parsing gracefully using `youtube-dl-exec` (`yt-dlp`) inside `audioExtractor.ts`. 
- **Rationale**: `yt-dlp` natively handles generic embedded sites and raw streaming inputs far better than `fluent-ffmpeg`. Prepending `youtube.com/watch?v=` to 11-char IDs allows direct passing to `yt-dlp`. (Already implemented).
- **Alternatives considered**: Writing custom scrapers for `t.me` and relying on `ytdl-core`. Rejected due to frequent breakage of `ytdl-core` and maintenance overhead of custom scrapers.

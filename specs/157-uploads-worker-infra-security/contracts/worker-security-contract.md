# Contract: Worker Security, Ingestion, Recovery, And Timeouts

## Worker Admin Access

**Routes**:

- `GET /ui`
- `GET /api/status/:id`
- `DELETE /api/status/:id`
- `POST /api/status/:id/retry`
- `POST /internal/live-support/preview`

**Required behavior**:

- In `NODE_ENV=production`, admin UI is disabled unless `WORKER_ADMIN_ENABLED=true`.
- Admin API routes require a strong bearer token using fixed-time comparison.
- Unauthorized attempts return 401 or 404/disabled response without queue details.
- Per-source rate limit applies to admin routes.
- Denied and allowed sensitive requests emit redacted audit logs.
- Ordinary status reads must not mutate queues or cancellation state.
- Retry endpoint may clear cancellation only when job exists and is failed.

## Queue Ingestion

**Input**: Redis stream message fields `jobType`, `jobId`, `payload`.

**Required behavior**:

- Unknown job types: acknowledge/delete stream message and log redacted warning.
- Invalid JSON payload: acknowledge/delete stream message and log redacted warning.
- `targetJobId` derived deterministically and sanitized by replacing `:` with `-`.
- Existing BullMQ job:
  - completed/active/waiting/delayed/prioritized: skip enqueue, acknowledge stream message.
  - failed: skip by default unless explicit retry path is used.
  - cancelled marker present: skip ordinary ingestion and preserve marker.
- No ordinary ingestion path may call `existingJob.remove()` or `clearJobCancellation()`.

## Redis Stream Recovery

**Required behavior**:

- Consumer group `worker-group` exists on `job-stream`.
- Worker periodically claims stale pending messages from dead consumers using `XAUTOCLAIM` or documented `XCLAIM` fallback.
- Claimed messages are processed through the same idempotent ingestion function.
- Stream message is acknowledged only after invalid-message handling or successful/skip ingestion decision.

## External Operations

**Required behavior**:

- Worker fetches use timeout and operation label.
- Callback timeout default: 10 seconds.
- Provider/download timeout default: configurable and bounded.
- Errors classify into operator-safe categories: timeout, network, authentication, permission, quota-exhausted, validation, not-found, provider, conversion, cancelled, implementation.
- Failure callbacks and logs must not include raw URLs, tokens, prompts, response bodies, or full stderr.

## Readiness

**Routes**:

- `GET /health`: liveness only, returns quickly if process is alive.
- `GET /ready`: readiness, checks DB, Redis, AI startup config, worker processors, callback readiness.

**Required behavior**:

- Docker healthcheck must use `/ready`.
- `/ready` returns 503 until required dependencies/processors are ready.

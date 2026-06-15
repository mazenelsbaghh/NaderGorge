# API and Surface Contracts

## Teacher Codes

### `GET /api/teacher/codes/groups`

- Requires Teacher authentication.
- Returns only groups whose `TeacherId` matches the caller's teacher profile.

### `GET /api/teacher/codes/groups/{groupId}/details`

- Requires Teacher authentication and ownership.
- Cross-teacher access returns `403`.

### Removed mutation contract

- `POST /api/teacher/codes/bulk-generate` is removed.
- Teacher UI exposes no create action.
- Direct requests cannot create any code data.

## Administrative Code Generation

### `POST /api/admin/codes/bulk-generate`

- Requires authenticated caller with `codes.manage`.
- Handler repeats permission validation.
- Validates count, length, target cardinality, expiry, discount, and balance amount.
- Returns `403` for missing permission and `400` for invalid business input.

## Manual Unlock

### `POST /api/exams/admin/lessons/{lessonId}/students/{studentId}/unlock`

- Requires `watch_requests.manage`.
- Returns `403` for unauthorized callers and no mutation occurs.
- Returns `404` for missing lesson or student.

## Exam and Homework Access

### `POST /api/exams/{examId}/start`

- Resolves lesson, video, package hierarchy, and explicit exam grants.
- Returns `403` with stable `FORBIDDEN` error code when entitlement is absent.

### Homework submission endpoint

- Requires access to the owning lesson.
- Duplicate concurrent submission is idempotent and never awards duplicate gamification.

## Session Contracts

### `POST /api/auth/refresh`

- Reads refresh cookie.
- Atomically consumes one active token.
- Returns one successor access token, refresh cookie, and current user/permissions.
- Replay returns `401` and does not revoke a valid successor.

### `POST /api/auth/logout`

- Reads refresh cookie if present.
- Revokes the matching active token.
- Expires the refresh cookie using the same security attributes as creation.
- Is idempotent and returns success even when no active cookie remains.

### Password reset

- Reset token includes user ID, reset role, expiry, unique token ID, and reset version.
- Successful use invalidates replay and revokes active refresh sessions.

## Health Contracts

### Backend

- `GET /api/health`: process liveness only.
- `GET /api/health/ready`: verifies database connection, Redis connection, and migration compatibility.

### Worker

- `GET /health`: process liveness only.
- `GET /ready`: verifies PostgreSQL, Redis, queue construction, and consumer initialization.

## Worker Message Contract

Producer writes one Redis Stream entry containing `messageId`, `jobType`, `jobId`, `payload`, and `createdAt`. Consumer acknowledges only after BullMQ returns ownership of the stable `jobId`. Duplicate stream delivery resolves to the same BullMQ job.

## Frontend Contracts

- Concurrent unauthorized HTTP responses share one refresh promise.
- Refresh success updates persisted token, Zustand token, user, roles, permissions, and SignalR token source together.
- Logout calls the server before clearing local state, with local cleanup guaranteed on network failure.
- Teacher code feature is read-only and contains no mutation controls.
- Shared dialogs contain keyboard focus and restore the trigger.
- Unsafe rich HTML never reaches `dangerouslySetInnerHTML` without sanitization.

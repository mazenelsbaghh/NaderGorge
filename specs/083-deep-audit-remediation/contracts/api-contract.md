# API Contract: Deep Technical Audit Remediation

## Auth User Snapshot

`GET /api/auth/me`

- Requires normal user bearer token.
- Returns current user id, phone, full name, roles, active/profile state.
- Used by the Next worker proxy to verify staff roles before forwarding to the worker service.

## Worker Proxy

`GET /api/worker/status/{jobId}`  
`DELETE /api/worker/status/{jobId}`  
`POST /api/worker/status/{jobId}/retry`

- Browser request must include the active user bearer token.
- Next route validates the token with backend auth snapshot.
- Only `Admin` and `Teacher` can pass.
- Next route forwards to worker with server-side `WORKER_ADMIN_TOKEN`.

## QR Redemption

`GET /api/qr/{codeHash}`

- Compatibility route.
- Redirects to `/qr/{codeHash}` with no server-side redemption attempt.

`GET /qr/{codeHash}`

- Client page.
- If unauthenticated, redirects to `/login?returnUrl=/qr/{codeHash}`.
- If authenticated, calls existing code activation API and redirects to returned destination or student dashboard.

## Homework

`GET /api/homework/pending`  
`POST /api/homework/{homeworkId}/submit`

- Frontend service must call these exact paths relative to the API base.
- No `/api/v1/students` prefix is used.

## Video Embed Material

`GET /api/student/video-session/{sessionId}/embed-material`

- Called only from server-side Next route.
- Requires configured internal token header.
- Returns encrypted session token and key only to the server-side route.
- Browser iframe URL carries only `s={sessionId}`.

## Internal Callbacks

`POST /api/v1/internal/callbacks/*`

- Requires reusable internal-token authorization filter.
- Inventory classification must show internal-token protection.

## E2E Endpoints

`/api/e2e/*`

- Requires E2E environment, strong E2E token, and destructive reset only against test/E2E database markers.
- Inventory classification must show E2E-only protection.

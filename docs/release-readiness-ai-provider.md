# T119 — Real AI Provider Acceptance Evidence

This harness is the release gate for the configured live-support AI provider. It performs real network/provider checks and never uses mocks, intercepted responses, business records, or frontend code.

Run from the repository root:

```bash
cd worker
npm run accept:real-ai
```

The harness prints JSON evidence with secret-safe details and uses these exit codes:

- `0`: configuration, callback readiness, worker readiness, and real provider inference passed.
- `1`: a check ran and failed (for example provider rejection, invalid decision, or an unhealthy endpoint).
- `2`: a required credential, URL, or runtime dependency is unavailable; this is a blocker, not a pass.

Required environment for a real run:

- Gemini Developer API: `GEMINI_API_KEY`.
- Callback readiness: `AI_CALLBACK_SECRET` and a reachable `BACKEND_API_URL`.
- Worker readiness: `WORKER_URL` pointing at the running worker HTTP server.

The probe uses a synthetic prompt and only validates the provider response schema. It does not claim T119 acceptance if credentials/quota are missing, readiness is unreachable, or the provider returns an error. A callback readiness `200` proves token-authenticated endpoint availability only; it is not evidence that a real business turn was completed.

Current local evidence (2026-07-13):

```text
GEMINI_API_KEY=missing
AI_CALLBACK_SECRET=missing
BACKEND_API_URL=missing
WORKER_URL=missing
```

Therefore T119 remains blocked until the harness returns exit code `0` in the deployed E2E/release environment. Do not mark the task complete from unit tests or provider mocks.

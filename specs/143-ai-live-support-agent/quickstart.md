# Quickstart and Verification: AI Live Support Agent

## Prerequisites

- PostgreSQL 16, Redis 7, backend, worker, and all frontend surfaces.
- Existing callback secrets plus valid AI provider configuration (`AI_PRIMARY_PROVIDER`, Vertex project/location/credentials and bucket, or Developer API key).
- Built-in Admin, authenticated student, guest browser, and checked-in support employee.

## Static and unit gates

```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter 'FullyQualifiedName~LiveSupportAI|FullyQualifiedName~LiveSupport'
dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter 'FullyQualifiedName~LiveSupportAI|FullyQualifiedName~LiveSupport'
npm --prefix worker test
npm --prefix frontend run lint
(cd frontend && npx tsc --noEmit)
npm --prefix frontend run build
```

Expected: no introduced warning/error; deterministic AI doubles cover provider behavior without live spend.

## Browser gates

```bash
cd frontend
npx playwright test tests/e2e/live-support-ai.spec.ts --project=chromium --project=webkit
```

Cover Admin-only settings, draft/preview/publish, student confirmation/cancel, guest lookup/verification, account creation, explicit handoff, post-handoff stop, inactivity warning, mobile RTL, keyboard focus, and reduced motion.

## Docker and migration

```bash
docker compose config -q
make migrate
make up
make ps
curl -fsS http://localhost:5245/api/health
curl -fsS http://localhost:3001/health
curl -fsS http://localhost:3001/ready
```

Verify the migration head and new AI tables/indexes against real PostgreSQL. Rebuild backend, worker, landing, student, admin, and assistant images after source changes.

## Manual acceptance

1. Admin creates a draft with one knowledge entry, one readable category, one allowed action, safe lookup/questions, timeout/grace, then runs preview and confirms zero writes.
2. Admin publishes/enables. With no staff checked in, student receives an AI answer.
3. Student requests an allowed action, cancels it, requests again, confirms, and sees one business effect and audit entry.
4. Student requests a human. Verify offline copy, durable next-shift queue, and zero later AI messages even if a delayed callback arrives.
5. Guest receives general help, creates an account once on retry, then in a separate conversation finds an existing account with a complete safe key and passes questions.
6. Fail and exhaust guest verification. Verify no candidate disclosure and permanent handoff.
7. Staff checks in, receives queued chat, and sees safe summary/transcript/reason without answers or system instructions.
8. Admin disables AI during an in-flight turn. Verify no new turn after five seconds and safe handoff.

## Negative security checks

- Non-Admin custom role cannot read or mutate AI configuration.
- Disabled data/action keys are absent from context and cannot be executed.
- Prompt injection in participant/knowledge/account text cannot reveal policy or invoke action.
- Raw verification answers, passwords, tokens, prompts, and disallowed fields are absent from logs, Redis payloads, audit, callbacks, and staff UI.
- Duplicate messages/jobs/callbacks/confirmations/account creation produce one logical effect.

## Rollback

Disable AI first, confirm all AI-active conversations handed off, stop/drain the dedicated queue, then roll back application images. Preserve database history and published versions; do not down-migrate referenced audit tables in production.

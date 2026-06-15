# Implementation Plan: Comprehensive Audit Remediation

**Branch**: `[125-comprehensive-audit-remediation]` | **Date**: 2026-06-15 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/125-comprehensive-audit-remediation/spec.md`

## Summary

Remediate the verified security, authorization, financial-integrity, session, worker-reliability, deployment, dependency, accessibility, responsive, and repository-data findings from the June 2026 deep audit. The implementation keeps teacher code access read-only, introduces explicit permission enforcement, makes state transitions atomic and idempotent, replaces destructive Redis-list handoff with acknowledged Redis Streams, separates public/protected delivery paths, and ties production deployment to the exact tested revision.

## Technical Context

**Language/Version**: C# 13 / .NET 9 backend; TypeScript 5.x / Next.js 16.2.7 / React 19.2.4 frontend; Node.js 20+ worker; YAML/Bash/JavaScript operational tooling  
**Primary Dependencies**: ASP.NET Core, MediatR, EF Core 9, Npgsql, StackExchange.Redis, BullMQ 5, ioredis, Axios, Zustand, SignalR, Tailwind CSS 4, Framer Motion, existing shared UI components  
**Storage**: PostgreSQL for relational state; Redis Streams and BullMQ for durable background delivery; separate public and protected asset paths/volumes  
**Testing**: xUnit with SQLite/PostgreSQL-focused integration cases, Node built-in test runner, Playwright, Python API/smoke tests, ESLint, TypeScript/Next production build, Docker Compose validation  
**Target Platform**: Linux Docker production host with nginx reverse proxy; modern desktop and mobile browsers; RTL Arabic-first UI  
**Project Type**: Multi-service web platform  
**Performance Goals**: No extra per-row permission queries in list paths; one refresh request for concurrent 401 responses; queue recovery without polling storms; mobile calendar usable at 375px; optimized hero image discovery  
**Constraints**: Preserve existing API envelopes and content hierarchy; no destructive Git-history rewrite; no external credential rotation without operator approval; no teacher code creation path; zero new broad role shortcuts for sensitive actions  
**Scale/Scope**: Cross-cutting remediation across backend, frontend, worker, database migrations, Docker/nginx, CI/CD, repository hygiene, and critical regression tests

## Constitution Check

*GATE: Passed before research and re-checked after design.*

- **Layering**: Domain receives only durable state fields; Application owns authorization and transaction rules; Infrastructure owns EF/Redis implementations; API owns HTTP/cookie mapping; frontend service/store/hooks own session behavior; worker owns queue consumers and provider calls.
- **Provider abstraction**: Notification delivery is implemented behind a worker provider function and fails explicitly when configuration is absent. AI vendor behavior remains isolated in the worker.
- **Security by default**: Sensitive repository material is removed, teacher code generation is denied in UI/API/handler, permissions are explicit, rich HTML is sanitized, session tokens become single-use, and internal service ports bind only to loopback or private networks.
- **Academic integrity**: Exam/homework/unlock access resolves every supported ownership and entitlement relationship before mutation.
- **Observability**: Liveness and readiness are separated; queue recovery, retry, dead-letter retention, and deployment checks are observable.
- **Phase verification**: Backend, frontend, worker, security scans, dependency scans, Docker config, targeted E2E, and manual role checks are required. The next release is blocked on any failed required gate.
- **UI/UX**: Existing Massar navy/teal/gold tokens, Tajawal typography, 44px touch targets, WCAG AA contrast, restrained product UI, 150-250ms state motion, RTL, focus containment, and 375/768/1024/1440 responsive checks remain authoritative. The generated Cyberpunk recommendation is rejected as incompatible with `PRODUCT.md` and `DESIGN.md`.

## Project Structure

### Documentation (this feature)

```text
specs/125-comprehensive-audit-remediation/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── api.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.Domain/Entities/
├── src/NaderGorge.Domain/Interfaces/
├── src/NaderGorge.Application/Features/
├── src/NaderGorge.Application/Services/
├── src/NaderGorge.Infrastructure/{Background,Data,Migrations,Services}/
├── src/NaderGorge.API/{Controllers,Middleware}/
└── tests/NaderGorge.Application.Tests/

frontend/
├── src/packages/teacher/
├── src/components/{admin,forms,landing,media,shared,ui}/
├── src/hooks/
├── src/services/
├── src/stores/
└── tests/e2e/

worker/
├── src/jobs/
├── src/queue/
├── src/providers/
└── src/*.test.ts

docker/
├── nginx/
└── docker-compose*.yml

.github/workflows/
scripts/
docs/
```

**Structure Decision**: Preserve the current multi-service repository. New frontend behavior is divided into feature package exports (`packages/teacher`), feature components (`components/forms`, `components/media`, `components/landing`), and reusable primitives (`components/shared` or existing `components/ui`). Queue reliability helpers live in `worker/src/queue`; provider-specific notification code lives in `worker/src/providers`.

## Technical Design

### Authorization and Codes

- Remove teacher code-generation controllers, service routing, UI controls, and legacy indirect generation code.
- Keep teacher list/detail queries scoped by `TeacherProfile.Id` and add direct-object authorization tests.
- Require `codes.manage` inside `BulkGenerateCodesCommandHandler`, not only on the controller, so indirect MediatR calls cannot bypass permission checks.
- Validate balance code amount and generation boundaries server-side. Multi-role users are authorized by explicit permission, not by the presence or absence of the Teacher role.
- Restrict manual unlock to `watch_requests.manage` and authorized administrative surfaces. Ownership/assignment-aware service checks remain available for future delegated unlock workflows but are not inferred from broad Teacher/Assistant roles.

### Academic Access and Data Integrity

- Add `HasAccessToExamAsync` to the access-check contract. Resolve lesson-linked exams, video-linked exams, direct exam grants, and parent package hierarchy before creating/reusing attempts.
- Require lesson access before homework submission. Add a unique database index on `(HomeworkId, StudentId)` and handle the losing concurrent insert as an idempotent conflict without publishing duplicate rewards.
- Execute balance updates as atomic database increments inside a transaction, then write ledger, audit, and outbox rows before commit.
- Preserve accepted video watch seconds after crossing limits; lock prevents future accumulation, while watch-count clamping does not rewrite already accepted time.

### Session Security

- Add a persisted password-reset version to `User`; include it in reset tokens and increment it atomically after success.
- Consume refresh tokens with one conditional database update inside a transaction; only the request affecting one row may create a successor.
- Add `POST /api/auth/logout` to revoke the refresh token represented by the HttpOnly cookie and expire the cookie.
- Add a shared frontend refresh promise. On success, update browser storage and Zustand user/token state atomically so HTTP and SignalR use the same claims.
- Consolidate shell logout behavior through the auth service while retaining local cleanup in a `finally` path.

### Worker Reliability and Notifications

- Produce structured Redis Stream messages from .NET and acknowledge only after BullMQ owns a job. Use stable message/job identifiers and recover stale pending entries.
- Configure bounded attempts, exponential backoff, timeouts, and retained failures for AI, mind-map, essay, notification, and commitment jobs.
- Make backend progress/completion commands idempotent by terminal-state checks and stable job identifiers.
- Schedule commitment sweeps with a BullMQ scheduler. Persist a unique occurrence key and use conflict-safe insertion.
- Replace simulated notification success with a real configured HTTP provider call. Missing provider configuration throws a non-successful explicit error.
- Expose `/health` for liveness and `/ready` for PostgreSQL, Redis, and worker-initialization readiness.

### Repository, Assets, Docker, and Deployment

- Delete tracked SQL dumps and dangerous migration-history repair utilities from the active tree. Ignore dump/backup patterns and add a tracked-file security verification script to CI.
- Document, but do not automatically execute, token/code revocation, credential rotation, backup validation, and Git-history rewriting.
- Separate public asset storage from protected subtitles/media. nginx serves only the public mount; protected delivery uses backend authorization and internal acceleration.
- Bind internal host ports to `127.0.0.1` for local/operator access and keep nginx as the only public binding in production configuration.
- Pin migrator tooling and Telegram Bot API image inputs. Resolve the high-severity MessagePack dependency through a verified compatible package update/override.
- Trigger production deployment only after successful required CI for the same SHA. Build revision-tagged images first, run that revision's migrator, verify all readiness endpoints/surfaces, and roll back to the last successful image tag on failure.
- Retire inferred migration repair. Deployment verifies migration state using reviewed migrations and database history only.

### Frontend UX and Architecture

- Teacher codes become a read-only feature module with list, filtering, details, printing/export only where already allowed, and a clear explanatory empty/information state. No create button, modal, mutation hook, or teacher generation API route remains.
- Apply `sanitizeRichHtml` consistently to homework question renderers and preserve plain-text truncation semantics.
- Align profile completion fields with backend `District` and `SchoolName` persistence.
- Harden the shared dialog primitive for initial focus, Tab containment, Escape, inert background, focus restoration, and reduced motion; migrate confirmation and profile completion dialogs to it.
- Render the social planner as an agenda list below the desktop breakpoint and retain the seven-column calendar on larger screens.
- Replace CSS hero background loading with `next/image` fill, `priority`, responsive `sizes`, and a separate overlay.
- Keep SignalR connection lifecycle dependent on endpoint and authentication only; subscription callbacks use refs.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet test backend/NaderGorge.sln --no-restore`
- PostgreSQL-backed concurrency/authorization integration subset where database semantics are under test
- `cd frontend && npm run lint && npm run build`
- Targeted Playwright specs for teacher codes, concurrent refresh/logout, rich HTML, profile completion, dialogs, and mobile planner
- `cd worker && npm test`
- `python -m pytest tests/test_auth.py tests/test_codes.py tests/test_exams.py tests/test_questions.py tests/test_video.py`
- `node scripts/verify-no-sensitive-tracked-files.mjs`
- `dotnet list backend/NaderGorge.sln package --vulnerable --include-transitive`
- `cd frontend && npm audit --omit=dev --audit-level=high`
- `cd worker && npm audit --omit=dev --audit-level=high`

**Docker Gate Required**:

- `docker compose config -q`
- Build backend, frontend surfaces, worker, nginx, and revision-tagged migrator
- Apply migrations to an isolated database and run readiness checks
- Verify host bindings and surface separation
- Run `scripts/verify-surface-separation.mjs`
- Production `make up`/external provider verification is operator-gated when secrets are unavailable

**Manual QA Required**:

- Teacher read-only code list/detail and direct generation denial
- Admin code creation with and without permission
- Cross-teacher unlock denial and entitled/unentitled exam/homework flows
- Concurrent refresh, logout, reset replay, and shared-device behavior
- Keyboard-only dialog operation and mobile social planner
- Worker transient failure/retry and dependency readiness

**End-of-Phase Report Format**: Implemented scope, changed files, migrations, commands and results, dependency/security scan results, Docker/readiness evidence, manual QA checklist, external operator actions, residual risks, and release go/no-go.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Cross-service remediation in one feature | The audit findings form connected security and release boundaries and the user requested complete remediation | Isolated fixes would leave bypass paths between API, worker, frontend, and deployment |
| New Redis Streams bridge | Accepted jobs require acknowledgement and crash recovery before BullMQ ownership | Existing destructive list pop silently loses jobs; retrying list reads cannot recover removed payloads |
| New shared accessible dialog primitive | Two verified dialogs and additional planner overlays need the same focus contract | Repeating manual key handlers preserves the existing accessibility defect |

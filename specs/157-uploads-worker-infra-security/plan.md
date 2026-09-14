# Implementation Plan: Uploads, Assets, Worker, and Infrastructure Security

**Branch**: `157-uploads-worker-infra-security` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/157-uploads-worker-infra-security/spec.md`

## Summary

Close Phase 3 of the full-platform remediation plan by hardening uploads, asset serving, worker admin access, Redis stream ingestion/recovery, worker readiness, external-call timeouts, logging redaction, Redis/compose/Nginx security, and worker container privileges. The approach is intentionally incremental: keep existing public content-image flows compatible where safe, move lesson resource downloads behind signed/controller delivery, harden live-support attachments, extract worker security-sensitive queue ingestion into testable modules, and update Docker/Nginx configuration so production-like defaults are safer without replacing the current architecture.

## Technical Context

**Language/Version**: C# 13 on .NET 9 backend; TypeScript 5.9 strict on Node.js 20 worker; Next.js 16.2.7/React 19 frontend only for service contract compatibility if needed.  
**Primary Dependencies**: ASP.NET Core controllers/middleware, MediatR, EF Core 9, ImageSharp, BullMQ, ioredis, Express, undici/global fetch, Docker Compose, Nginx.  
**Storage**: PostgreSQL for LessonResource/LiveSupportAttachment metadata; local/shared asset volume for uploads/subtitles/mindmaps; Redis streams/BullMQ for jobs. No new database schema is required unless implementation discovers a metadata gap for safe asset classification.  
**Testing**: `dotnet test` for backend upload/E2E guard/resource download tests; `cd worker && npm test` for worker modules; `docker compose config -q`; focused config smoke checks for Nginx/Docker; frontend build/lint only if frontend files change.  
**Target Platform**: Linux Docker deployment plus local E2E environment.  
**Project Type**: Multi-service web platform: backend API, Next.js frontend surfaces, Node worker, Redis/PostgreSQL, Nginx reverse proxy.  
**Performance Goals**: Upload validation must reject spoofed samples before disk write; worker timeout tests must finish within configured test timeout; Redis recovery must process reclaimed messages no more than once.  
**Constraints**: Preserve safe public assets when already intentional; do not expose protected/private content through wildcard public static paths; keep existing backend/auth patterns; avoid large worker rewrite reserved for later Phase 4 refactor; do not revert unrelated dirty worktree changes.  
**Scale/Scope**: Phase 3 covers P1-7/P1-8/P1-9/P1-10/P1-11/P1-17/P1-18/P1-19/P1-20/P2-11/P2-19/P2-24/P2-25 from the remediation document.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**:
  - Backend API/Application/Infrastructure: upload validation, resource download safety, E2E destructive guard tests, live-support attachment safety.
  - Worker: admin UI gating, admin audit/rate limit, queue ingestion idempotency, Redis stream recovery, bounded external fetch helpers, redacted logging.
  - Docker/Nginx: worker healthcheck `/ready`, non-root worker user, Redis auth/persistence/maxmemory, no default production worker port publishing, protected asset CORS restrictions, TLS termination contract.
  - Frontend: no expected UI changes; only run frontend checks if service contract changes surface.
- **Automated tests required**:
  - Backend upload spoofing/resource download/live-support attachment/E2E DB guard tests.
  - Worker unit tests for admin auth/rate limit/audit, job ingestion duplicate/cancelled behavior, stream recovery, timeout helper, log redaction, readiness config.
  - Docker/Nginx config tests via static assertions plus `docker compose config -q`.
- **Manual QA required**:
  - Admin upload allowed/rejected files.
  - Private resource direct browser access denied.
  - Worker UI closed in production-like config.
  - Stop worker during stream job and restart to verify recovery.
- **Docker gate commands**:
  - `docker compose config -q`
  - `cd worker && npm run build`
  - `cd worker && npm test`
  - `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj`
  - focused `dotnet test` commands listed in quickstart.
  - Full `make up`/health checks are required unless local secrets are unavailable; blockers must be recorded.
- **No-next-phase rule**: Phase 4 task generation and Phase 5 implementation may proceed only after this plan passes validation; final report may not claim completion until failed gates are fixed or explicitly recorded as external blockers.

**Initial Constitution Result**: PASS. No new architecture violation; worker extraction is scoped to testability and Phase 3 safety, not the larger Phase 4 worker refactor.

## Project Structure

### Documentation (this feature)

```text
specs/157-uploads-worker-infra-security/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── upload-asset-security-contract.md
│   ├── worker-security-contract.md
│   └── infrastructure-security-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.API/
│   ├── Controllers/AdminController.cs
│   ├── Controllers/AdminSalesController.cs
│   ├── Controllers/LiveSupportParticipantController.cs
│   ├── Controllers/PublicController.cs
│   ├── Controllers/E2eTestingController.cs
│   ├── Services/ContentImageStorage.cs
│   └── Configuration/E2eOnlyAttribute.cs
├── src/NaderGorge.Application/
│   ├── Features/LiveSupport/Interfaces/ILiveSupportAttachmentStorage.cs
│   └── Interfaces/IContentImageStorage.cs
├── src/NaderGorge.Infrastructure/Services/
│   ├── LiveSupportAttachmentStorage.cs
│   └── LiveSupportService.cs
└── tests/NaderGorge.Application.Tests/
    ├── ContentImageStorageTests.cs
    ├── UploadContentImageCommandTests.cs
    └── Security/UploadsAndAssetsSecurityTests.cs

worker/
├── Dockerfile
├── src/
│   ├── index.ts
│   ├── security.ts
│   ├── logging.ts
│   ├── cancellation.ts
│   ├── utils/audioExtractor.ts
│   ├── services/aiErrors.ts
│   ├── services/aiProvider.ts
│   ├── services/workerFetch.ts
│   ├── queues/jobIngestion.ts
│   ├── queues/streamRecovery.ts
│   ├── server/adminAccess.ts
│   └── health/readiness.ts
└── src/**/*.test.ts

docker/
├── nginx/massar.conf
├── docker-compose.yml
└── docker-compose.infra-only.yml

docker-compose.yml
.env.example
docs/
├── full-platform-defects-remediation-phases-2026-06-29.md
└── verification-contract.md
```

**Structure Decision**: Use existing backend/worker/docker boundaries. Add small worker modules for Phase 3 safety and testability, but do not perform the broader Phase 4 `index.ts` decomposition.

## Phase 0 Research Output

Research decisions are captured in [research.md](./research.md). The critical decisions are: central upload validation, protected resource download through controller/X-Accel paths, public image compatibility through safe conversion, live-support attachment sanitization, production-disabled Bull Board, idempotent queue ingestion, Redis `XAUTOCLAIM` recovery, bounded worker external calls, redacted failure classification, E2E destructive guard tightening, and Docker/Nginx hardening.

## Phase 1 Design Output

The logical data model is captured in [data-model.md](./data-model.md). Interface contracts are captured in [contracts/upload-asset-security-contract.md](./contracts/upload-asset-security-contract.md), [contracts/worker-security-contract.md](./contracts/worker-security-contract.md), and [contracts/infrastructure-security-contract.md](./contracts/infrastructure-security-contract.md). Verification steps are captured in [quickstart.md](./quickstart.md).

## Phase Closure & Verification Plan

**Automated Tests Required**:

- Backend:
  - `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~UploadsAndAssetsSecurityTests|FullyQualifiedName~ContentImageStorageTests|FullyQualifiedName~UploadContentImageCommandTests"`
  - Covers MIME spoofing, safe resource storage/download disposition, live-support attachment sanitization, E2E destructive DB-name guard.
- Worker:
  - `cd worker && npm test`
  - Covers job idempotency, cancellation preservation, explicit retry cancellation clearing, Redis stream recovery claim behavior, admin auth/rate-limit/audit, timeout helper/failure classification, log redaction.
- Build/config:
  - `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj`
  - `cd worker && npm run build`
  - `docker compose config -q`
  - Static checks for `worker/Dockerfile`, `docker-compose.yml`, `docker/docker-compose*.yml`, and `docker/nginx/massar.conf`.
- Frontend:
  - Run `cd frontend && npm run lint && npm run build` only if frontend code changes.

**Docker Gate Required**:

- `docker compose config -q`.
- Verify worker healthcheck uses `http://localhost:3001/ready`.
- Verify worker image defines and runs as a non-root user.
- Verify production-like worker port publishing is disabled or profile-gated.
- Verify Redis service includes password/auth, append-only persistence, maxmemory policy, and healthcheck uses authenticated ping when password is set.
- If local secrets allow: `make up`, then check backend `/api/health`, worker `/ready`, Nginx landing route, and denied worker `/ui` without valid admin auth.

**Manual QA Required**:

- Admin content upload: upload a valid PDF/image and a spoofed `.pdf` containing HTML/script; valid file returns safe URL, spoofed file is rejected.
- Student/resource download: open a protected resource URL directly without a valid token; request is denied or returns no private content. Open with a valid token; response downloads as attachment.
- Worker UI: production-like config should deny `/ui` without auth and should not expose host port by default.
- Worker crash recovery: create or simulate a pending stream message, stop worker, start worker, confirm the message is claimed and not duplicated.

**End-of-Phase Report Format**:

- Implemented scope by defect ID.
- Changed files.
- Clean-code-guard result.
- Test-guard result.
- Feature test matrix and exact command results.
- Docker/config gate results.
- Manual QA checklist.
- Known blockers or external dependencies.
- Final readiness and remaining risks.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |

## Post-Design Constitution Check

PASS. The design preserves layered boundaries, avoids direct database changes unless discovered necessary, keeps provider interactions behind worker utilities, includes automated tests and Docker gates, and documents manual QA.

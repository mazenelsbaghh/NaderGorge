# Implementation Plan: Real-time Platform Speed & Sync

**Branch**: `121-realtime-platform-speed` | **Date**: 2026-06-11 | **Spec**: [specs/121-realtime-platform-speed/spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/121-realtime-platform-speed/spec.md)
**Input**: Feature specification from `/specs/121-realtime-platform-speed/spec.md`

## Summary

This plan addresses the implementation of a comprehensive platform acceleration and real-time synchronization system based on SignalR, Redis, and optimized database queries. It introduces the outbox pattern to guarantee event dispatching, real-time UI invalidation without full-page reloads, secure signed media URLs, rate limiting, and HTTP idempotency.

## Technical Context

**Language/Version**: C# (.NET 9, C# 13) for backend, TypeScript (Next.js 16.2.1, React 19) for frontend, Node.js (v20) for worker.  
**Primary Dependencies**: `@microsoft/signalr` (JS/TS Client), `Microsoft.AspNetCore.SignalR` (C# Server), `StackExchange.Redis` for caching & rate limiting, `BullMQ` for job queues.  
**Storage**: PostgreSQL (DB store for Outbox events and transactional state), Redis (Cache store, job state, idempotency keys, rate limiting).  
**Testing**: `dotnet test` (for backend handlers & services), Playwright (for frontend E2E/manual validation scripts).  
**Target Platform**: Docker-compose deployment environment (Postgres Alpine, Redis Alpine, Node.js Slim, .NET net9.0 SDK).  
**Performance Goals**: SignalR notifications dispatched in < 1 second. Read queries latencies < 300ms. 100% of idempotent requests handled safely.  
**Constraints**: Zero full-page `window.location.reload()` or `router.refresh()` in the standard flow. Support Range requests for video proxies.  
**Scale/Scope**: Real-time event routing for 10k+ concurrent student connections grouped by UserId, Role, PackageId, and LessonId.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer Impact**:
  - **Database**: Add `OutboxEvents` table via EF Core migration.
  - **Backend (API/Infrastructure)**: Add `/hubs/platform` endpoint, SignalR `PlatformHub`, custom `OutboxProcessorBackgroundService`, `IdempotencyFilter` for endpoint deduplication, and Signed URL generation logic.
  - **Worker**: Ensure BullMQ job processors trigger callbacks that raise outbox events (e.g. `VideoReady` when video chapters analysis is complete).
  - **Frontend**: Add `usePlatformEvents.ts` hook, update core UI components (Sidebar, Balance, Lesson details, AI Job monitor) to react to events without reload.
  - **Docker**: Nginx configuration supporting `X-Accel-Redirect` for local media storage acceleration.
- **Automated Tests**:
  - Backend integration tests verifying that publishing a lesson or updating balance adds an event to the `OutboxEvents` table, and that the Outbox background service processes it.
  - Unit tests for the Idempotency filter and rate limiter.
- **Manual QA Flows**:
  - Test connection restoration of SignalR (disable/enable network in DevTools).
  - Test double submission of package purchase/code activation and confirm only one request succeeds.
  - Test real-time lesson card appearance on student dashboard when published by admin in another browser session.
- **Docker Gate Commands**:
  - `docker compose config -q`
  - `make up`
  - `make migrate`
  - Verify `http://localhost:5245/api/health` and `http://localhost:8738/` are healthy.

*Failed gates must be resolved inside this phase before moving forward.*

## Project Structure

### Documentation (this feature)

```text
specs/121-realtime-platform-speed/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   ├── OutboxEvent.cs
│   │   │   └── IdempotentRequest.cs
│   │   └── Interfaces/
│   │       └── IOutboxService.cs
│   ├── NaderGorge.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Migrations/
│   │   └── Services/
│   │       ├── OutboxService.cs
│   │       └── RedisIdempotencyService.cs
│   └── NaderGorge.API/
│       ├── Hubs/
│       │   └── PlatformHub.cs
│       ├── Filters/
│       │   └── IdempotentAttribute.cs
│       ├── BackgroundServices/
│       │   └── OutboxProcessorBackgroundService.cs
│       └── Controllers/
│           └── VideoSessionController.cs
frontend/
├── src/
│   ├── components/
│   │   └── layout/
│   │       └── Sidebar.tsx
│   ├── hooks/
│   │   └── usePlatformEvents.ts
│   └── services/
│       └── content-service.ts
```

**Structure Decision**: Standard decoupled web application structure. C# backend project layers (Domain, Application, Infrastructure, API) and Next.js frontend project structure with dedicated SignalR hub hooks and service layer modifications.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Backend: `dotnet test` covering Outbox pattern operations and `PlatformHub` connection logic.
- E2E Playwright tests simulating multi-user SignalR sync (e.g. `tests/e2e/realtime-sync.spec.ts`).

**Docker Gate Required**:
- Run `make up` and verify all containers are operational.
- Check database migrations are fully applied by running `make migrate`.

**Manual QA Required**:
- Admin logs in, opens lesson list. Student logs in, opens same package list. Admin publishes new lesson. Verify lesson card instantly appears for student.
- Send concurrent HTTP requests with same `Idempotency-Key` and verify that the duplicate request is rejected and returns cached results.

**End-of-Phase Report Format**:
- Plan vs Implemented scope matching.
- Run results of `dotnet test` and Playwright tests.
- Verify Nginx X-Accel-Redirect responses for secure downlods.
- Status checklist of all P0 & P1 scenarios defined in specifications.

## Complexity Tracking

*No constitution violations present.*

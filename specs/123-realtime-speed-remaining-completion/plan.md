# Implementation Plan: Real-time Speed Remaining Completion

**Branch**: `123-realtime-speed-remaining-completion` | **Date**: 2026-06-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/123-realtime-speed-remaining-completion/spec.md`

## Summary

This plan addresses all remaining items from the platform speed and real-time update audit. It introduces outbox events for all remaining state-changing commands, partitions the lesson detail API response to allow lazy loading of heavy elements (comments and resources), optimizes the student shell store to prevent full refreshes, adds per-user Redis-backed rate limiting to heavy endpoints, and implements client-side Web Vitals tracking with backend storage.

## Technical Context

**Language/Version**: C# 13 (.NET 9.0), TypeScript 5.x, Node.js 20  
**Primary Dependencies**: MediatR 12.4.1, EF Core 9.0.6, Next.js 16.2.1, Zustand 5.0.12, `@google/genai` (Gemini), StackExchange.Redis  
**Storage**: PostgreSQL 16, Redis 7  
**Testing**: `dotnet test` (backend unit/integration tests), Playwright (frontend E2E), `npm run lint`  
**Target Platform**: Docker container deployment (Linux Alpine/Slim base images)  
**Project Type**: Multi-tier Web Application & Worker Ingestion System  
**Performance Goals**: Initial lesson page load (P95) under 500ms, API response (P95) under 200ms  
**Constraints**: Zero full-page reloads, no polling intervals below 30s, strict content moderation  
**Scale/Scope**: ~10k active students, ~85 page routes, ~116 MediatR handlers  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer Impact**:
  - **Backend**: Update MediatR handlers to emit outbox events, split `GetLessonDetail` endpoint, add `/api/v1/metrics/web-vitals` route, and add rate-limiting Redis checks.
  - **Frontend**: Split API calls to fetch comments and resources lazily, update Zustand stores, implement Web Vitals reporting hook.
  - **Worker**: No direct worker changes required for this phase.
  - **Database**: Add `WebVitalsMetric` table, run `EXPLAIN ANALYZE` and add indexes for lesson and comment queries.
  - **Docker**: Health verification for all running containers.
- **Automated Tests**:
  - `dotnet test` (must compile and pass).
- **Manual QA**:
  - Verify real-time SignalR notifications upon comment approval, resource addition, and post creation.
  - Test rate limiter by sending rapid activation requests (verify 429 status).
- **Docker Gate**:
  - Run `docker compose config -q` and verify health of all services.
- **End-of-Phase Rule**:
  - The next phase cannot start until all checklist items are completed and quality checks pass.

## Project Structure

### Documentation (this feature)

```text
specs/123-realtime-speed-remaining-completion/
├── spec.md              # Feature specification
├── plan.md              # This implementation plan
├── research.md          # Research findings
├── data-model.md        # Database schema updates
├── quickstart.md        # Quickstart setup instructions
├── contracts/
│   └── api.md           # API endpoints contracts
└── tasks.md             # Detailed task checklist
```

### Source Code

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   ├── Controllers/
│   │   └── Middleware/
│   ├── NaderGorge.Application/
│   │   └── Features/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   └── Interfaces/
│   └── NaderGorge.Infrastructure/
└── tests/

frontend/
├── src/
│   ├── app/
│   ├── components/
│   ├── hooks/
│   ├── services/
│   └── lib/
└── package.json
```

**Structure Decision**: Standard Next.js + .NET multi-project structure.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `cd backend && dotnet test` (all 90 tests must pass).
- `cd frontend && npm run build && npm run lint` (clean build and zero errors).

**Docker Gate Required**:
- Verify running containers with `docker compose ps`.
- Check `/api/health` and `/ui` endpoints.

**Manual QA Required**:
- Create a post, like a post, and comment on a post; verify that events trigger and propagate.
- Verify lesson detail loads core metadata first and comments lazily.
- Verify rate limiting returns HTTP 429 after 5 requests/min on code activation.

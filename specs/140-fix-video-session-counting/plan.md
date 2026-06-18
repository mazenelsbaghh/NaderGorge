# Implementation Plan: Session-Safe Video View Counting

**Branch**: `140-fix-video-session-counting` | **Date**: 2026-06-18 | **Spec**: `specs/140-fix-video-session-counting/spec.md`  
**Input**: Feature specification from `specs/140-fix-video-session-counting/spec.md`

## Summary

Bind every student watch-progress update to a concrete `VideoPlaybackSession`, make each refresh/reopen create a new newest session, and allow that session to register at most one view. Persist per-session registration and progress-sequence state so retries are idempotent, cap accepted time at the next threshold boundary, extend active sessions on valid playback, reject superseded sessions, and stop the older player with Arabic recovery UI. Preserve the existing cumulative `VideoWatchEvent`, threshold, limits, locking, extra-watch, override, and repurchase behavior.

## Technical Context

**Language/Version**: C# 13 / .NET 9 backend; TypeScript 5.x strict / Next.js 16.2.7 / React 19.2.4 frontend  
**Primary Dependencies**: ASP.NET Core Web API, MediatR 12.4.1, EF Core 9.0.6, Npgsql 9.0.4, Axios 1.17, existing `SecureVideoPlayer` and service layer  
**Storage**: PostgreSQL; new lifecycle/idempotency fields on existing `VideoPlaybackSession`; existing `VideoWatchEvent` remains aggregate source of truth  
**Testing**: xUnit backend application tests, Playwright frontend E2E, repository Python API smoke tests, build/lint, Docker health gates  
**Target Platform**: Dockerized Linux web platform, browser student player  
**Project Type**: Backend API plus Next.js frontend; worker is unaffected  
**Performance Goals**: Preserve standard API p95 below 500ms; one transaction and indexed latest-session lookup per progress update  
**Constraints**: Server-side authority; one view maximum per session; actual-time anti-inflation cap retained; retry idempotency; newest session wins; no pricing/entitlement redesign  
**Scale/Scope**: One student/video aggregate with potentially multiple short-lived playback sessions; 10-second normal progress flush cadence

## Constitution Check

- **Architecture**: PASS. Controller maps HTTP only; Application handlers own session/watch rules; Domain holds state; Infrastructure supplies EF persistence and migration; frontend calls through `video-session-service.ts`.
- **Security/access**: PASS. Authenticated user identity remains claim-derived. Handler validates session ownership and video match. Superseded/expired sessions cannot mutate state.
- **Data integrity**: PASS. Progress sequence plus serializable transaction makes accepted updates idempotent and prevents one session crossing multiple view boundaries.
- **Backend impact**: `VideoPlaybackSession`, EF mapping/migration, create-session and track-progress handlers/controller, calculator behavior, backend tests.
- **Frontend impact**: session-aware progress contract, monotonic per-session sequence, stop/message/reload state in `SecureVideoPlayer`, focused E2E coverage.
- **Worker impact**: None.
- **Database impact**: Add session progress state and lookup index through an EF migration; no change to `VideoWatchEvent` semantics.
- **Docker impact**: Backend/frontend rebuild plus migration; run Compose validation, migration, and health checks.
- **Testing gate**: Backend unit tests, frontend build/lint and targeted Playwright path, Python regression smoke where feasible, full compile gates.
- **Manual QA gate**: Same-session seek/continue, partial progress across refresh, second view only after refresh, stale tab stop/reload, long-session renewal, lock/extra-view/repurchase regressions.
- **No-next-phase rule**: Task generation cannot start until all planning artifacts exist, quality validation passes, and every planning decision is resolved.

Post-design re-check: PASS. The design uses existing layers and entities, adds only state required to make session counting enforceable, exposes one versioned-in-place API contract through the existing service, and introduces no worker or unrelated UI changes.

## Project Structure

### Documentation

```text
specs/140-fix-video-session-counting/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   └── video-progress-api.md
├── quickstart.md
└── tasks.md
```

### Source Code

```text
backend/src/
├── NaderGorge.Domain/Entities/
│   └── VideoPlaybackSession.cs
├── NaderGorge.Application/
│   ├── Common/VideoWatchProgressCalculator.cs
│   └── Features/Student/Commands/
│       ├── CreateVideoSessionCommand.cs
│       └── TrackWatchProgressCommand.cs
├── NaderGorge.API/Controllers/VideoSessionController.cs
└── NaderGorge.Infrastructure/
    ├── Data/AppDbContext.cs
    └── Migrations/<timestamp>_AddPlaybackSessionProgressState.cs

backend/tests/NaderGorge.Application.Tests/
└── VideoWatchProgressTests.cs

frontend/src/
├── services/video-session-service.ts
└── components/video/SecureVideoPlayer.tsx

frontend/tests/e2e/
└── video-session-counting.spec.ts

tests/
└── test_video.py
```

**Structure Decision**: Extend the existing student video-session vertical slice. Do not add a new project or bypass the service/MediatR boundaries. The older `/api/tracking/video-event` entry point must no longer mutate student watch state without a session; it returns a session-required failure and the secure player remains the supported counting path.

## Exact Implementation Design

1. Extend `VideoPlaybackSession` with `HasRegisteredView`, `LastProgressSequence`, `LastProgressAt`, and `IsSuperseded`. Map defaults and add an index supporting newest active session lookup by `(UserId, LessonVideoId, CreatedAt)`.
2. Change `CreateVideoSessionCommandHandler` to always create a distinct session for a successful open/refresh, mark prior non-expired sessions for the same user/video superseded, and retain their rows for stale-request detection. `IsConsumed` continues to mean one-time embed-material consumption, not progress-session termination.
3. Change the progress request contract to require `SessionId` and positive monotonically increasing `ProgressSequence`. Validate authenticated ownership, lesson-video match, expiry, superseded state, and newest-session status before touching `VideoWatchEvent`.
4. Treat a sequence less than or equal to `LastProgressSequence` as an idempotent successful no-op with current watch state. For a new sequence, apply existing elapsed-time and 30-second anti-inflation caps.
5. If `HasRegisteredView` is false, cap the accepted delta to the exact remaining seconds before `(WatchCount + 1) * thresholdSeconds`. Apply it, register at most one view, then set `HasRegisteredView = true`. Discard the same update's excess and ignore later progress from that session.
6. On every valid newest-session progress request, update `LastProgressAt` and renew `ExpiresAt` by the existing five-minute session window. A renewal never resets `HasRegisteredView`.
7. Keep maximum/custom maximum calculation and lock transitions unchanged. Preserve negative `TimeWatchedInSeconds` reset normalization before calculating the next boundary.
8. Make `/api/tracking/video-event` reject state mutation with `SESSION_REQUIRED`, preventing a sessionless bypass. Retain the endpoint temporarily for compatibility with an explicit failure contract.
9. Store the created session ID and a per-session sequence counter in `SecureVideoPlayer`. Stop generating/queueing seconds after `viewRegistered`. Send `sessionId` and the current sequence for each flush; retry the same sequence and delta until accepted, then advance.
10. Map `SESSION_SUPERSEDED` to HTTP 409. In the player, clear timers/pending progress, send pause, set a dedicated superseded error message, and show a reload action that requests a fresh session. Map expiry/invalid-session failures to safe recoverable errors.
11. Add backend tests for partial cross-refresh accumulation, one-view cap and excess discard, duplicate sequence, concurrent/latest-session rejection, ownership/video mismatch, renewal, exact max locking, reset compatibility, and sessionless endpoint rejection. Add a focused Playwright flow for stale-player UI and service-contract assertions.

## Phase 0: Research Output

`research.md` records the decisions for the playback-session boundary, refresh supersession, embed-consumption semantics, sequence idempotency, excess-second handling, renewal, the legacy endpoint, PostgreSQL consistency, and stale-player recovery. All planning unknowns are resolved.

## Phase 1: Design and Contracts Output

`data-model.md` defines the additive playback-session fields, invariants, indexes, and transitions. `contracts/video-progress-api.md` defines request/response and error behavior. `quickstart.md` defines automated, manual, and Docker verification. No unresolved design gate remains.

## Failure Modes and Rollout

- Migration defaults keep existing playback sessions safe: false flags, zero sequence, null last-progress timestamp. Existing sessions created before deployment will lack a client session-bound progress request and must reload.
- Superseded/expired/mismatched requests return stable error codes without mutating `VideoWatchEvent`.
- Network retries reuse the same sequence and delta. A client must never advance its sequence before a successful response.
- Serializable conflicts may surface as retryable failures; the client retries the same idempotent update. Database constraints and state validation ensure the retry cannot double count.
- Deployment order: apply migration, deploy backend, then frontend. During mixed-version rollout, old frontend progress requests receive `SESSION_REQUIRED` and cannot corrupt counts; students may need one reload after frontend deployment.
- Rollback requires reverting backend/frontend together. The additive database columns may remain safely if code is rolled back.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter VideoWatchProgressTests`
- `dotnet test backend/NaderGorge.sln --no-restore`
- `npm --prefix frontend run lint`
- `npm --prefix frontend run build`
- `npm --prefix frontend run test:e2e -- video-session-counting.spec.ts`
- `python3 -m pytest tests/test_video.py -k watch -q`

**Docker Gate Required**:

- `docker compose config -q`
- `make up`
- `make migrate`
- `make ps`
- `curl -f http://localhost:5245/api/health`
- `curl -f http://localhost:8738`
- `curl -f http://localhost:3001/ui`

**Manual QA Required**:

- Student lesson/video page: reach threshold, continue and seek; count stays +1.
- Student lesson/video page: watch below threshold, refresh, continue; partial seconds combine.
- Student lesson/video page: after first counted session refresh and reach threshold again; exactly one second view registers.
- Two tabs/devices: newest session continues; older player pauses, explains supersession in Arabic, and offers reload.
- Long playback: valid progress renews the same session without a second view.
- Locked/extra-watch/repurchase: existing status, approvals, reset, and lock outcomes remain unchanged.
- Negative access: another user/session ID and mismatched video cannot update progress.

**End-of-Phase Report Format**: List implemented scope and changed files, migration result, exact automated/Docker commands and outcomes, manual QA checklist, fixed failures, remaining operator-only checks, and go/no-go readiness.

## Complexity Tracking

No constitution violations require justification.

# Implementation Plan: Employee Workflows and Realtime Refresh

**Branch**: `160-employee-realtime-refresh` | **Date**: 2026-07-12 | **Spec**: [spec.md](./spec.md)
**Input**: Approved remediation plan in `docs/employee-and-realtime-refresh-remediation-plan.md`.

## Summary

Build a single, typed server-state invalidation contract across the existing Next.js frontend and ASP.NET Core/SignalR backend. The first production slice hardens employee/session authorization and HR lookups, then migrates domains in priority order. Each successful mutation updates the current client immediately, each `StaffDataChanged` event invalidates only active mapped queries, and reconnect performs reconciliation. Existing backend authorization remains authoritative; no worker change is planned.

## Technical Context

**Language/Version**: C# 13/.NET 9 backend; TypeScript 5.x/Next.js 16.2.7/React 19.2.4 frontend; Node.js worker unchanged.
**Primary Dependencies**: ASP.NET Core Web API, MediatR, FluentValidation, EF Core 9/Npgsql, SignalR 9, Redis backplane, Axios, Zustand, Tailwind CSS; evaluate `@tanstack/react-query` as the single query cache.
**Storage**: PostgreSQL for user authorization/version and durable outbox state; Redis for SignalR backplane/ephemeral coordination; browser memory/local auth storage for session bootstrap; no worker storage change.
**Testing**: `dotnet test` application/integration suites, frontend ESLint/typecheck/build, Vitest/Jest only if an existing runner is introduced, Playwright E2E, Docker/health gates from `docs/verification-contract.md`.
**Target Platform**: Dockerized Linux services behind Next.js and Nginx, same-site `.lvh.me` E2E domains.
**Project Type**: Full-stack web application with ASP.NET Core API, Next.js App Router, SignalR, PostgreSQL, Redis, and a separate Node worker.
**Performance Goals**: Same-session affected active queries refreshed within 1 second of a successful mutation; connected permission/session changes converge within 2 seconds; event bursts are debounced and deduplicated without a request storm.
**Constraints**: Backend authorization is final; drafts must not be replaced by realtime refetch; inactive queries must not refetch; full reload is allowlisted only for documented secure-video recovery; existing API and security conventions must remain compatible.
**Scale/Scope**: Inventory approximately 217 frontend mutation calls across 32 service/page areas, then migrate all domains by priority: auth/employee/HR, operations/CRM/support, content, codes/sales/finance, exams/homework, community/comments/notifications/media/reports, and settings/forms.

## Constitution Check

- **Modular Clean Architecture**: PASS. Backend work stays in Domain/Application/Infrastructure/API boundaries; frontend contracts live in `lib`, services, hooks, and feature components. Worker is unchanged unless an existing event contract requires a compatibility-only update.
- **Security & Access Control**: PASS with explicit gates. Add a current-session contract and authorization versioning, increment the existing `User.SecurityStampVersion` for role/permission/status changes, keep JWT validation authoritative, and test 403/safe redirect behavior.
- **Provider/Integration Abstraction**: PASS. SignalR event routing is behind a typed event envelope and a frontend scope-to-query registry; no page calls SignalR directly.
- **Phased Delivery**: PASS. Baseline inventory, P0 employee/HR slice, then domain migrations with feature flags/canary and phase-close evidence.
- **Academic/Data Integrity**: PASS. Durable workflow state remains backend/PostgreSQL-owned; query invalidation cannot replace transaction or audit behavior.
- **Observability**: PASS. Add structured counters/timers around mutation-to-refresh, event dedupe/reconnect, refetch counts, and 401/403 after authorization changes without logging sensitive payloads.
- **Required test/Docker gates**: PASS. Every phase closes with focused tests, `make verify`, frontend checks, `docker compose config -q`, health checks, and `make verify-e2e` when the environment is available. A failed gate blocks the next phase unless an external blocker is recorded with evidence and owner approval.

## Research Decisions

See [research.md](./research.md). Key decisions are: use the existing `SecurityStampVersion` as the authorization/session version to avoid a second invalidation counter; add `GET /api/auth/session` as a non-refresh current-session snapshot; adopt TanStack Query only after a dependency/build check, otherwise extend the current registry with the same typed contract; evolve `StaffDataChanged` compatibly with event IDs/scopes and preserve old consumers during migration; and use optimistic updates only for safe local list operations.

## Source Structure and Exact Change Areas

```text
backend/src/NaderGorge.Domain/Entities/User.cs
backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs
backend/src/NaderGorge.Infrastructure/Data/StaffRealtimeChangeDetector.cs
backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs
backend/src/NaderGorge.API/Hubs/PlatformHub.cs
backend/src/NaderGorge.API/Controllers/AuthController.cs
backend/src/NaderGorge.API/Program.cs
backend/src/NaderGorge.Application/Features/Auth/Commands/{LoginCommand,RefreshTokenCommand}.cs
backend/src/NaderGorge.Application/Features/Auth/Queries/GetCurrentSessionQuery.cs
backend/src/NaderGorge.Application/Features/Admin/Commands/{AdminCreateUserCommand,UpdateUserRoleCommand,UpdateUserStatusCommand,UpdateRoleCommand}.cs
backend/src/NaderGorge.Application/Features/Admin/Hr/Commands/*
backend/tests/NaderGorge.Application.Tests/{Auth/...,HR/...,StaffRealtimeOutboxTests.cs,OutboxProcessorTests.cs}
frontend/src/lib/{query-keys.ts,realtime-invalidation-map.ts,cache-invalidation.ts,auth-session.ts}
frontend/src/providers/QueryProvider.tsx
frontend/src/app/layout.tsx
frontend/src/hooks/{usePlatformEvents.ts,useStaffRealtimeInvalidation.ts,useCurrentSession.ts}
frontend/src/stores/auth-store.ts
frontend/src/services/{auth-service.ts,hr-service.ts,admin-service.ts}
frontend/src/components/layout/{StaffRealtimeBoundary.tsx,AuthBootstrap.tsx,StaffGuard.tsx}
frontend/src/app/{admin,teacher}/layout.tsx
frontend/src/app/admin/** and frontend/src/app/teacher/** migrated domain consumers
frontend/src/services/query-contracts.test.ts
frontend/tests/e2e/employee-realtime-refresh.spec.ts
docs/data-refresh-inventory.md
docs/employee-workflow-bug-matrix.md
frontend/scripts/check-no-unallowlisted-reloads.mjs
```

No database migration is needed if `SecurityStampVersion` remains the version source. If implementation discovers a conflicting semantic use that prevents role/status invalidation, stop before migration and add a dedicated `AuthorizationVersion` migration plus integration evidence; do not silently overload an unrelated column.

## Implementation Phases and Gates

### Phase 0 — Research and baseline preparation

This phase is represented by the evidence in [research.md](./research.md): inspect existing authorization/version behavior, SignalR/outbox delivery, frontend cache ownership, mutation inventory shape, dependency/build constraints, and verification contract. No product code is changed until the baseline inventory and acceptance tests are defined.

### Phase 1 — Design and contract foundation

This phase is represented by [data-model.md](./data-model.md), the contracts under `contracts/`, and [quickstart.md](./quickstart.md). It establishes the current-session, event-envelope, query-invalidation, conflict, and phase-gate contracts before domain migration.

### Phase A — Baseline inventory

Generate machine-readable and human-readable inventories from all frontend services/pages. Classify every mutation by domain, affected query keys, current post-mutation behavior, event, cache, and reload. Record employee create/edit/role/status/attendance/vacation/payroll/assignment flows and reproduce known bugs. Deliver `docs/data-refresh-inventory.md`, `docs/employee-workflow-bug-matrix.md`, and `frontend/src/lib/query-contracts.ts` (or generated equivalent). Gate: every mutation is classified; no migration begins with unknown employee acceptance paths.

### Phase B — P0 current session and employee/HR

Add `GET /api/auth/session` returning the same `UserDto` fields as login plus `authorizationVersion` sourced from `SecurityStampVersion`. Update role/status/employee mutations to increment the version and emit a user-targeted/session-relevant `StaffDataChanged` event. Centralize the frontend permission evaluator, refresh the auth store on the current-user event or a 401/403 authorization response, rebuild navbar/route guards, and safely redirect. Migrate employee lists/details/lookups and HR dashboard to explicit query keys and mutation contracts. Add row-version/ETag-style conflict checks to employee edit DTO/commands without clearing drafts.

### Phase C — Typed server state and realtime adapter

Install and configure TanStack Query only if build/dependency checks pass; create one `QueryClientProvider` in the client root with GET-only retries, no mutation retry by default, domain stale times, cancellation, and active-query-only refetch. Otherwise extend `cache-invalidation.ts` with typed keys, all-match prefix invalidation, active registration, and equivalent policies. Implement `query-keys.ts`, `realtime-invalidation-map.ts`, and an adapter from event scopes to query keys. Replace `StaffRealtimeBoundary` revision-only behavior with query invalidation while preserving its provider API during migration.

### Phase D — Domain migration

Migrate each domain as a self-contained package of query keys, fetch hooks, mutation hooks, loading/empty/error states, invalidation mapping, same-tab tests, two-session test, reconnect test, and permission-negative test. Order: employee/HR; operations/CRM/live support; content; codes/sales/finance; exams/homework; community/comments/notifications/media/reports; settings/forms. Remove module-level caches only after the domain has one authoritative query cache.

### Phase E — Reconnect, conflicts, and reload cleanup

On SignalR reconnect, rejoin groups, reset event dedupe window, and invalidate/refetch active critical queries. Add bounded event IDs/sequence metadata and metrics. Preserve form drafts and show conflict banners. Replace safe `StudentContextPanel` and `LessonCarousel` reload workarounds with targeted recovery; retain secure-video reload only if its security contract requires it and add an allowlist check. Remove obsolete `force: true` calls after each domain migration.

### Phase F — Observability and rollout

Record mutation success/failure, UI refresh latency, event delivery/reconnect, invalidation/refetch counts, duplicate/missed events, and 401/403 after permission changes. Put employee/HR behind a feature flag, canary to internal staff, compare stale-state/error metrics, and expand by domain. Rollback disables the new adapter/flag without rolling back compatible API/schema changes.

## API and Event Contracts

See [contracts/current-session.md](./contracts/current-session.md), [contracts/staff-data-changed.md](./contracts/staff-data-changed.md), and [contracts/query-invalidation.md](./contracts/query-invalidation.md).

## Phase Closure & Verification Plan

**Automated Tests Required**: focused Auth/HR/StaffRealtime/Outbox tests; query key and scope mapping contract tests; frontend lint/typecheck/build; Playwright two-session employee/permission/reconnect/conflict smoke; full `make verify` before final close.

**Docker Gate Required**: `docker compose config -q`; `make up`; `make migrate` only if a migration is introduced; `make ps`; backend `/api/health`, frontend surface, worker `/ready`; `make verify-e2e` using the `.lvh.me` contract.

**Manual QA Required**: Admin creates/edits/disables employee; two staff sessions observe HR/operations changes; permission revocation redirects safely; external edit preserves draft; reconnect converges; duplicate event does not duplicate rows/toasts.

**End-of-Phase Report Format**: changed files and scope, exact commands/results, Docker/health evidence, manual QA checklist, unresolved external blockers, metrics/risks, and go/no-go for the next phase.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Add a shared query-state layer across legacy page-local fetches | The approved plan explicitly requires one authoritative server-state contract for approximately 217 mutations and active-query invalidation | Extending each page's `useEffect` manually preserves the defect and cannot guarantee coverage |

# Implementation Plan: Authentication, Sessions, and Permission Safety

**Branch**: `154-auth-session-permission-safety` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/154-auth-session-permission-safety/spec.md`

## Summary

Close Phase 1 of `docs/full-platform-defects-remediation-phases-2026-06-29.md` by hardening session invalidation, refresh behavior, frontend auth hydration, admin route permission denial, 401/403 semantics, and parent report link leakage. The implementation will add account security versioning to existing user sessions, validate token claims against live account state, reject unsafe refresh attempts, move frontend runtime auth toward in-memory access tokens with refresh-cookie bootstrap, derive admin route permissions from a shared inventory, map forbidden application failures to 403, and shorten parent report URL tokens while applying strict referrer policy.

## Technical Context

**Language/Version**: C# 13 on .NET 9 backend; TypeScript 5.x strict on Next.js 16.2.7 / React 19 frontend  
**Primary Dependencies**: ASP.NET Core JWT bearer auth, EF Core 9/Npgsql, MediatR, FluentValidation, Axios, Zustand, Next.js App Router, Playwright  
**Storage**: PostgreSQL via EF Core migration for new user account security version; browser storage only for non-sensitive compatibility state where still required  
**Testing**: `dotnet test` for application/API behavior, frontend lint/typecheck/build, Playwright E2E for auth/admin/parent report flows  
**Target Platform**: Dockerized web platform with backend API, Next.js frontend surfaces, PostgreSQL, Redis  
**Project Type**: Full-stack web application remediation phase  
**Performance Goals**: Token validation must avoid noticeable page latency; one indexed user lookup per authenticated request is acceptable for security-critical validation and can be optimized later with short-lived cache if measured as hot-path issue  
**Constraints**: Do not break existing student one-year token product decision; do not remove parent report token-in-URL flow in this phase; do not start Phase 2 remediation until Phase 1 gates pass  
**Scale/Scope**: Existing platform auth/session/admin/parent report surfaces; no worker or mobile app logic change in this phase

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Backend API auth pipeline, Application auth/admin commands, Domain user model, Infrastructure EF mapping/migration and token service, frontend auth storage/bootstrap/API client/admin route guard, frontend E2E tests. Worker has no direct change. Docker gate remains `docker compose config -q`.
- **Automated tests required**: Backend tests for disabled refresh, stale token validation, refresh revocation on reset/role/device changes, forbidden mapping; frontend/unit or E2E tests for auth bootstrap, 403 behavior, admin route deny-by-default, parent report expiration/referrer policy.
- **Manual QA required**: Student disable/refresh, staff role change/direct admin URL, cross-surface login/bootstrap, parent report valid/expired links, 403 forbidden action without logout.
- **Docker gates**: `docker compose config -q`; backend build/tests; frontend lint/typecheck/build; Playwright smoke where local services are available.
- **No-next-phase rule**: Phase 2 financial/data-integrity remediation must not begin until introduced failures are fixed or explicitly accepted by owner.

Initial gate status: PASS. The feature touches security-critical code but follows existing layers and adds tests for every critical path.

## Project Structure

### Documentation (this feature)

```text
specs/154-auth-session-permission-safety/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── auth-session-contract.md
│   └── admin-route-permissions.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.Domain/Entities/
│   ├── User.cs
│   └── RefreshToken.cs
├── src/NaderGorge.Domain/Interfaces/
│   └── ITokenService.cs
├── src/NaderGorge.Application/
│   ├── Common/
│   │   └── ForbiddenException.cs
│   └── Features/
│       ├── Auth/Commands/
│       │   ├── RefreshTokenCommand.cs
│       │   └── ResetPasswordCommand.cs
│       └── Admin/Commands/
│           ├── RemoveDeviceCommand.cs
│           └── UpdateUserRoleCommand.cs
├── src/NaderGorge.API/
│   ├── Program.cs
│   ├── Middleware/ExceptionHandlingMiddleware.cs
│   └── Controllers/ParentController.cs
├── src/NaderGorge.Infrastructure/
│   ├── Data/AppDbContext.cs
│   ├── Services/TokenService.cs
│   └── Migrations/
└── tests/NaderGorge.Application.Tests/
    ├── Auth/
    ├── Parent/
    └── Operations/

frontend/
├── src/lib/
│   ├── auth-memory.ts
│   └── auth-storage.ts
├── src/services/
│   ├── api-client.ts
│   └── auth-service.ts
├── src/stores/
│   └── auth-store.ts
├── src/components/layout/
│   ├── AuthBootstrap.tsx
│   └── AdminGuard.tsx
├── src/packages/admin/
│   ├── navigation.tsx
│   └── route-permissions.ts
└── tests/e2e/
    ├── auth.spec.ts
    ├── admin-users.spec.ts
    └── parent-report.spec.ts
```

**Structure Decision**: Use the existing backend clean architecture and frontend service/store/component boundaries. Add a small shared frontend admin route-permission module instead of duplicating route lists inside guards. Add one small backend exception type rather than overloading `UnauthorizedAccessException` for forbidden behavior.

## Phase 0: Research Output

Research decisions are captured in [research.md](./research.md). The main resolved decisions are:

- Add `User.SecurityStampVersion` for broad stale-session invalidation.
- Validate `IsActive`, `PasswordResetVersion`, and `SecurityStampVersion` during JWT bearer token validation.
- Reject inactive/stale/device-revoked refresh attempts before minting replacement tokens.
- Keep parent report URL tokens for this phase, shorten lifetime, and enforce strict referrer policy.
- Move frontend bearer token access to in-memory runtime state with a bounded compatibility fallback.
- Use a shared admin route inventory for deny-by-default route guarding.
- Introduce a dedicated forbidden failure type for 403 semantics.

## Phase 1: Design Output

Design artifacts are complete:

- Data model: [data-model.md](./data-model.md)
- Auth/session contract: [contracts/auth-session-contract.md](./contracts/auth-session-contract.md)
- Admin route permission contract: [contracts/admin-route-permissions.md](./contracts/admin-route-permissions.md)
- Verification quickstart: [quickstart.md](./quickstart.md)

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AuthSessionSafetyTests|FullyQualifiedName~ParentReport|FullyQualifiedName~TaskTests"` for refresh/session/forbidden/parent behavior.
- `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj` for backend compile and migration model validity.
- `npm run lint` in `frontend`.
- `npm run typecheck` in `frontend` if the script exists; otherwise record absence and run `npm run build`.
- `npm run build` in `frontend`.
- `npx playwright test tests/e2e/auth.spec.ts tests/e2e/admin-users.spec.ts tests/e2e/parent-report.spec.ts` when local webServer and backend fixtures are available.

**Docker Gate Required**:

- `docker compose config -q`.
- If schema migration is added, verify migration compiles with backend build and document whether `dotnet ef database update` was run locally or deferred for environment.

**Manual QA Required**:

- Disable a student and verify refresh/opening student page fails.
- Reset password and verify old token fails.
- Change staff role/permissions and verify old direct admin privileges stop.
- Open unknown/unmapped `/admin/*` as assistant/staff and verify denial without data exposure.
- Clear browser token storage while keeping refresh cookie and verify auth bootstrap restores session.
- Trigger a 403 and verify frontend does not logout.
- Open parent report link before and after expiration and verify referrer policy behavior.

**End-of-Phase Report Format**:

- Implemented scope mapped to P1-1, P1-2, P1-3, P1-4, P2-1, P2-5, P2-17.
- Files changed.
- Tests/commands run with pass/fail.
- Docker gate result.
- Manual QA completed or pending.
- Known risks and whether Phase 2 may start.

## Complexity Tracking

No constitution violations currently require justification.

## Post-Design Constitution Check

- **Layering**: PASS. Domain stores version fields; Application owns business invalidation; Infrastructure owns token generation/EF mapping; API owns auth events/middleware; frontend owns client auth state and route guards.
- **Provider abstraction**: N/A. No external provider added.
- **Security by default**: PASS. Deny-by-default admin routes, stale-token rejection, short-lived parent reports, 401/403 split.
- **Phased delivery**: PASS. Only Phase 1 security/auth scope included.
- **Verification gate**: PASS. Automated, Docker, and manual QA gates are documented.

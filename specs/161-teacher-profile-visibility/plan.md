# Implementation Plan: Teacher Profile & Content Visibility

**Branch**: `161-teacher-profile-visibility` | **Date**: 2026-07-13 | **Spec**: [spec.md](spec.md)
**Input**: Approved feature specification for Admin-only teacher editing and independent teacher/content visibility.

## Summary

Extend the existing teacher administration workflow so an Admin can edit the linked `User` and `TeacherProfile` atomically, including write-only password replacement. Add two independent persisted visibility states on `TeacherProfile`: teacher discovery visibility and teacher-content visibility. Apply those states server-side to every public/student teacher/content projection and protected content/access check, while leaving admin/teacher operational views, purchase history, financial history, and audit history intact. Reuse the existing `HasPermission("users.manage")`, EF migration, audit, cache invalidation, and `StaffDataChanged` outbox patterns.

## Technical Context

**Language/Version**: C# 13 / .NET 9; TypeScript 5.x / Next.js 16.2.7 / React 19.2.4  
**Primary Dependencies**: ASP.NET Core Web API, MediatR, EF Core 9/Npgsql, FluentValidation, Axios, existing cache invalidation and admin components  
**Storage**: PostgreSQL through `AppDbContext`; Redis/SignalR for staff refresh only; no worker storage change  
**Testing**: `dotnet test` application tests, backend build, frontend lint/typecheck/build, focused API/handler tests, optional E2E smoke  
**Target Platform**: Docker Compose production stack and existing Admin, Student, and public web surfaces  
**Project Type**: Layered web application with .NET backend, Next.js frontend, and unchanged Node worker  
**Performance Goals**: Visibility filtering must be applied in database queries; normal list/detail requests remain within existing response budgets and hide state reaches refreshed staff consumers within 2 seconds  
**Constraints**: Admin authorization must be enforced server-side; hidden content must deny direct access for previous purchasers; password hashes must never leave the backend; existing records must not be deleted  
**Scale/Scope**: All teacher-facing public/student list, detail, community, package/content, search, recommendation, purchase, and protected access paths that can expose teacher-owned content

## Constitution Check

- **Layer impact**: Domain adds explicit teacher visibility state; Application adds admin commands/queries, validation, public visibility predicates, and access checks; Infrastructure adds EF migration/configuration; API adds Admin endpoints and applies existing policies; Frontend updates the Admin teacher form/list and student/public consumers; Worker is unchanged.
- **Security gate**: `users.manage`/Admin authorization is required for every mutation; password replacement is write-only; public and student endpoints fail closed for hidden records.
- **Data integrity gate**: User/profile fields, visibility states, subject links, audit, and outbox refresh are saved atomically. Existing purchases/grants are retained and evaluated as inaccessible while content is hidden.
- **Testing gate**: Cover Admin happy path, non-Admin denial, validation/duplicate login, independent hide/show, visitor/student/previous-purchaser denial, restore, audit, cache/realtime invalidation, and regression of admin/teacher access.
- **Docker gate**: Before closure run `docker compose config -q`, migration/build checks, service health checks, and feature smoke checks using the existing project commands.
- **Decision**: No next phase is complete until failed tests, warnings introduced by this feature, migration issues, and health failures are fixed or explicitly recorded as owner-approved risk.

## Project Structure

### Documentation

```text
specs/161-teacher-profile-visibility/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   └── teacher-management-api.md
├── quickstart.md
└── tasks.md
```

### Source Code

```text
backend/src/NaderGorge.Domain/Entities/TeacherProfile.cs
backend/src/NaderGorge.Application/Features/Admin/Commands/AdminTeacherCommands.cs
backend/src/NaderGorge.Application/Features/Admin/Queries/AdminTeacherQueries.cs
backend/src/NaderGorge.Application/Features/Admin/Validators/
backend/src/NaderGorge.Application/Services/TeacherVisibilityService.cs
backend/src/NaderGorge.Application/Services/AccessCheckService.cs
backend/src/NaderGorge.Application/Features/Public/Queries/GetActiveTeachersQuery.cs
backend/src/NaderGorge.API/Controllers/AdminController.cs
backend/src/NaderGorge.API/Controllers/PublicTeachersController.cs
backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs
backend/src/NaderGorge.Infrastructure/Data/StaffRealtimeChangeDetector.cs
backend/src/NaderGorge.Infrastructure/Migrations/<timestamp>_AddTeacherVisibilityControls.cs
backend/tests/NaderGorge.Application.Tests/
frontend/src/services/teacher-service.ts
frontend/src/services/student-service.ts
frontend/src/app/admin/teachers/AdminTeachersPageClient.tsx
frontend/src/app/admin/teachers/[id]/TeacherProfilePageClient.tsx
frontend/src/app/student/teachers/
frontend/src/app/teachers/
```

**Structure Decision**: Keep the existing Clean Architecture and feature folders. Teacher visibility is a domain rule shared by public discovery, student content, and protected access, so the plan uses one Application-level service/predicate rather than duplicating ad-hoc filters in controllers. The worker and Docker topology do not change.

## Phase 0: Research

Research decisions are recorded in [research.md](research.md). Required findings include existing teacher/user fields, current public routes, current `IsActive` semantics, authorization and audit patterns, cache/realtime behavior, and the correct point to deny inherited content access.

## Phase 1: Design & Contracts

- Persist independent teacher and content visibility states on `TeacherProfile` with defaults preserving current visibility for existing teachers.
- Add an Application visibility service/predicate that resolves teacher ownership for packages, terms, sections, lessons, videos, exams, shared packages, and community posts.
- Update admin DTOs/commands to include all supported editable User/Profile fields and optional new password; use a transaction/atomic save and version invalidation for credentials.
- Apply the predicate to public teacher list/landing/detail/community endpoints, student teacher services, content projections, purchase/preview paths, and `AccessCheckService` protected access.
- Publish existing staff data refresh scopes and invalidate public/student query keys after successful mutations; do not expose visibility mutations to teachers or staff.
- Add focused backend tests and frontend tests/smoke coverage described in `quickstart.md`.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- Focused application tests for teacher update validation, write-only password replacement, Admin authorization, visibility state transitions, audit/outbox behavior, public/student filtering, hidden direct-access denial, previous-purchaser denial, restore, and independent toggles.
- Full backend application test project and backend build with zero new warnings.
- Frontend `npm run lint`, `npm run typecheck`, and `npm run build`; feature UI smoke for Admin edit/hide/show states.
- E2E/API smoke where the verification backend and browser are available.

**Docker Gate Required**: `docker compose config -q`; create/apply EF migration; `make verify` or the repository's focused backend/frontend commands; `docker compose ps` with db, redis, backend, worker, and all web surfaces healthy; verify `/api/health` and worker `/ready`.

**Manual QA Required**: Admin edits every visible field and sets a new password; non-Admin mutation is denied; teacher and content toggles operate independently; visitor/student lists, direct profile, course/package, community, search/recommendation, and direct content access hide correctly; previous purchaser is denied while hidden; Admin shows content and access returns; audit log contains before/after state without password hash.

**End-of-Phase Report Format**: implementation summary, changed files, migration ID, exact commands/results, Docker health evidence, manual QA checklist, unresolved risks, and go/no-go decision.

## Complexity Tracking

No constitution violations are planned. The visibility service is required to avoid duplicating a security-sensitive ownership rule across multiple public/student endpoints; adding a new abstraction is simpler and safer than repeating filters.

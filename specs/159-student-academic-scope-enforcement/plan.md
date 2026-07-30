# Implementation Plan: Student Academic Scope Enforcement

**Branch**: `159-student-academic-scope-enforcement` | **Date**: 2026-07-06 | **Spec**: `specs/159-student-academic-scope-enforcement/spec.md`
**Input**: Feature specification from `specs/159-student-academic-scope-enforcement/spec.md`

## Summary

Enforce one authoritative academic-scope rule across every student-facing list, detail, purchase, code, coupon, gift, grant, community, teacher, notification, public exam, and shared-package surface. The technical approach is to add a reusable backend academic scope model and service, migrate existing partial scope fields into that model, then route all student-facing visibility and access decisions through the service before returning data or creating financial/access side effects.

## Technical Context

**Language/Version**: C# 13 on .NET 9 backend; TypeScript 5.x strict on Next.js 16.2.7 / React 19.2.4 frontend; Node.js worker unchanged.  
**Primary Dependencies**: ASP.NET Core Web API, MediatR, FluentValidation, EF Core 9.0.6, Npgsql 9.0.4, Next.js App Router, Axios service layer, Zustand, Tailwind CSS, Lucide React.  
**Storage**: PostgreSQL through EF Core migrations; no Redis or worker storage change required.  
**Testing**: `dotnet test backend/NaderGorge.sln`; `cd frontend && npm run lint && npm run build`; targeted Playwright/E2E after seeded data exists; Docker gates through `docker compose config -q`, `make up`, `make migrate`, health checks.  
**Target Platform**: Dockerized web platform: backend `:5245`, frontend `:8738`, worker `:3001`, PostgreSQL 16, Redis 7.  
**Project Type**: Full-stack web application with backend API, frontend student/admin/teacher surfaces, and database migration.  
**Performance Goals**: Keep student list/detail APIs under constitution p95 target `<500ms` for standard CRUD/listing; academic-scope checks must be query-composable and indexed, not per-row N+1 loops for large lists.  
**Constraints**: Fail closed for missing student profile, missing scope, invalid subject mapping, or unscoped target; preserve non-student admin/teacher/staff visibility rules; preserve current role and moderation/payment/expiration restrictions.  
**Scale/Scope**: All student-facing surfaces: packages, terms, sections/months, lessons, videos, public exams, teachers, community posts/comments/polls, notifications/offers, shared packages, direct purchases, coupons, printable codes, access codes, gifts, grants, homework/exam/video access paths.

## Constitution Check

**Pre-Research Gate**: PASS with required scope controls.

- **Layer impact**:
  - Backend Domain: add scope entities/enums and service interfaces.
  - Backend Application: add academic eligibility service, validators, query filters, grant-time checks, access re-evaluation, tests.
  - Backend Infrastructure: add DbSets, EF mappings, migration, legacy data backfill.
  - Backend API: no new controller family required; existing controllers keep MediatR dispatch and return updated DTOs/errors.
  - Frontend: update admin forms/services to capture academic scope, update student empty/error states and DTOs.
  - Worker: no direct changes.
  - Docker: migration required, health gate required.
- **Automated tests required**:
  - Backend unit/integration tests for scope matching, inheritance, grant re-evaluation, purchase/code/coupon/gift rejection, admin validation.
  - Frontend lint/build; E2E smoke for student packages/teachers/community/public exams/shared packages/code redemption.
- **Manual QA required**:
  - Student list/detail/purchase/code/gift negative flows.
  - Admin create/publish validation for unscoped and general-scope records.
  - Docker stack with migration and health checks.
- **Docker gate commands**: `docker compose config -q`, `make up`, `make migrate`, `curl -f http://localhost:5245/api/health`, `curl -f http://localhost:8738`, `curl -f http://localhost:3001/ui`.
- **Next phase rule**: Do not start implementation tasks until this plan validates with `validate_spec_plan_quality.py`; do not mark implementation complete until failed phase gates are fixed or explicitly documented.

**Post-Design Gate**: PASS. The plan preserves clean architecture by keeping academic scope in Domain/Application services, using EF migrations only in Infrastructure, and keeping frontend filtering presentation-only.

## Project Structure

### Documentation (this feature)

```text
specs/159-student-academic-scope-enforcement/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── academic-scope-api.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.Domain/
│   ├── Entities/AcademicScopeEntities.cs
│   ├── Entities/ContentEntities.cs
│   ├── Entities/SalesEntities.cs
│   ├── Entities/SharedTeacherPackage.cs
│   ├── Entities/Notifications/NotificationEvent.cs
│   ├── Enums/AcademicScopeEnums.cs
│   └── Interfaces/IAcademicScopeService.cs
├── src/NaderGorge.Application/
│   ├── Services/AcademicScopeService.cs
│   ├── Services/AcademicValidationService.cs
│   ├── Services/AccessCheckService.cs
│   ├── Features/Content/Queries/*.cs
│   ├── Features/Student/Commands/PurchaseContentCommand.cs
│   ├── Features/Codes/Commands/ActivateCodeCommand.cs
│   ├── Features/Codes/Queries/ValidateCodeQuery.cs
│   ├── Features/Admin/Gifts/Commands/IssueGiftCommand.cs
│   ├── Features/Admin/Sales/*.cs
│   ├── Features/Community/*.cs
│   └── Features/Exams/*.cs
├── src/NaderGorge.Infrastructure/
│   ├── Data/AppDbContext.cs
│   └── Migrations/
└── tests/NaderGorge.Application.Tests/
    ├── AcademicScopeServiceTests.cs
    ├── StudentAcademicScopeAccessTests.cs
    ├── StudentAcademicScopePurchaseTests.cs
    └── StudentAcademicScopeAdminValidationTests.cs

frontend/
├── src/services/
│   ├── admin-service.ts
│   ├── code-service.ts
│   ├── content-service.ts
│   ├── community-service.ts
│   ├── public-exams-service.ts
│   ├── shared-package-service.ts
│   └── student-service.ts
├── src/components/admin/
├── src/app/admin/
├── src/app/student/
└── tests/
```

**Structure Decision**: Use the existing layered backend and service-oriented frontend. Do not create a new deployable service or worker job. Add a reusable academic-scope model and service that existing feature modules consume.

## Phase 0 Research Decisions

See `specs/159-student-academic-scope-enforcement/research.md`.

Key decisions:

- Add normalized `StudentFacingAcademicScope` rows instead of adding nullable scope columns to every entity.
- Add `AcademicSubjectEligibility` mapping for stage/grade/subject rules.
- Use `IAcademicScopeService` as the only backend source of truth for student eligibility.
- Re-evaluate current academic eligibility inside `AccessCheckService` and grant creation paths.
- Backfill `Package.TargetGrade`, `PublicExamProduct.GradeLevel/SubjectId/IsPlatformWide`, `SharedTeacherPackage.EducationStage/GradeLevel`, and teacher-subject data into normalized scopes.

## Phase 1 Data/Contract Design

See:

- `specs/159-student-academic-scope-enforcement/data-model.md`
- `specs/159-student-academic-scope-enforcement/contracts/academic-scope-api.md`
- `specs/159-student-academic-scope-enforcement/quickstart.md`

## Implementation Scope

### Backend Domain and Persistence

- Add `AcademicScopeLevel` enum:
  - `Exact = 0`
  - `PlatformWide = 1`
  - `StageWide = 2`
  - `GradeAllSubjects = 3`
- Add `StudentFacingScopeOwnerType` enum for at least:
  - `Package`, `Term`, `ContentSection`, `Lesson`, `LessonVideo`, `Exam`, `PublicExamProduct`, `Teacher`, `CommunityPost`, `NotificationEvent`, `SalesCoupon`, `PrintableCodeBatch`, `CodeGroup`, `GiftIssuance`, `SharedTeacherPackage`, `SharedTeacherPackageItem`.
- Add `AcademicSubjectEligibility` entity:
  - `EducationStage`
  - `GradeLevel`
  - `SubjectId`
  - `IsActive`
  - uniqueness on `(EducationStage, GradeLevel, SubjectId)`.
- Add `StudentFacingAcademicScope` entity:
  - `OwnerType`, `OwnerId`, `ScopeLevel`
  - nullable `EducationStage`, `GradeLevel`, `SubjectId`
  - `InheritedFromOwnerType`, `InheritedFromOwnerId` for diagnostic/backfill only if needed
  - audit fields through `BaseEntity`
  - indexes on `(OwnerType, OwnerId)`, `(ScopeLevel, EducationStage, GradeLevel, SubjectId)`.
- Add DbSets to `IAppDbContext` and `AppDbContext`.
- Add migration:
  - create new tables and indexes
  - backfill from existing fields
  - do not mark unscoped old records as general by default
  - keep legacy fields for compatibility during rollout; writes should sync normalized scopes.

### Backend Application Services

- Add `IAcademicScopeService` with methods:
  - `GetStudentProfileAsync(Guid studentId, CancellationToken ct)`
  - `GetAllowedSubjectIdsAsync(EducationStage stage, GradeLevel grade, CancellationToken ct)`
  - `IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType ownerType, Guid ownerId, Guid studentId, CancellationToken ct)`
  - `FilterEligibleOwnersAsync(...)` or query helper for list endpoints
  - `ValidateTargetHasScopeAsync(...)`
  - `ValidateStudentCanUseTargetAsync(...)`
  - `ResolveEffectiveScopesAsync(...)` for hierarchy inheritance.
- Service rules:
  - missing `StudentProfile` fails closed
  - missing owner scope fails closed
  - platform-wide matches all students
  - stage-wide matches same `EducationStage`
  - grade-all-subjects matches same `EducationStage` and `GradeLevel`
  - exact matches stage/grade/subject, where subject must be in `AcademicSubjectEligibility`
  - multiple scopes are OR: any matching scope allows eligibility
  - child without explicit scope inherits nearest explicit parent
  - child with explicit scope must match on its own scope after parent path is eligible.
- Update `AccessCheckService`:
  - staff/admin/teacher bypass remains
  - student grant checks must additionally call academic eligibility for the requested target at use time
  - old grants remain active as records but do not permit use if current profile no longer matches.

### Student-Facing APIs to Filter

- `backend/src/NaderGorge.Application/Features/Content/Queries/GetPackagesQuery.cs`
- `GetPackageByIdQuery.cs`, `GetTermsQuery.cs`, `GetSectionsQuery.cs`, `GetLessonsQuery.cs`, `GetLessonDetailQuery.cs`, lesson resources/comments queries.
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs`, `GetQuickAccessQuery.cs`, `GetProgressQuery.cs`, `GetMistakesQuery.cs`, `GetStudentNotificationsQuery.cs`.
- `backend/src/NaderGorge.Application/Features/Community/Queries/GetCommunityPostsQuery.cs` and comment/like/vote commands for target post eligibility.
- Public teacher APIs behind `PublicTeachersController` / corresponding Application queries.
- Public exam product APIs in `PublicExamsController`, `AdminPublicExamsController`, and sales public exam handlers.
- `StudentSharedPackagesController` and shared package query handlers.

### Grant and Financial Side-Effect Paths

- `PurchaseContentCommand`:
  - before discount/promo/balance deduction, validate target scope for student
  - reject with Arabic message and no `StudentAccessGrant`, `SalesFinancialEffect`, `BalanceTransaction`, or teacher accounting event.
- `ActivateCodeCommand` and `ValidateCodeQuery`:
  - validate target scope preview for current student when validating code
  - re-check before marking consumed or creating grant
  - leave failed attempts auditable without consuming code.
- `SalesCoupon` and `PrintableCodeBatch`:
  - creation requires scoped target
  - discount application re-checks actual student.
- `IssueGiftCommand`:
  - creation verifies target has valid scope
  - for every recipient, re-check target against that student before `GiftRecipient.Active` or `StudentAccessGrant` creation
  - failed recipients use explicit `ACADEMIC_SCOPE_DENIED`.

### Admin and Frontend

- Admin creation/update DTOs for content, public exams, sales coupons, printable batches, code groups, gifts, shared packages, notifications/offers, and community scopes must include `academicScopes`.
- Frontend services must type `academicScopes` in:
  - `frontend/src/services/admin-service.ts`
  - `frontend/src/services/code-service.ts`
  - `frontend/src/services/admin-gifts-service.ts`
  - `frontend/src/services/public-exams-service.ts`
  - `frontend/src/services/shared-package-service.ts`
  - `frontend/src/services/community-service.ts`
  - `frontend/src/lib/academic-labels.ts`
- Admin forms must require either at least one exact scope or one explicit general scope.
- Student pages keep empty states but must rely on backend-filtered data.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AcademicScope"`
- `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AccessCheck|FullyQualifiedName~Purchase|FullyQualifiedName~Gift|FullyQualifiedName~Code|FullyQualifiedName~Sales"`
- `cd frontend && npm run lint`
- `cd frontend && npm run build`
- `make verify` after implementation stabilizes.

**Docker Gate Required**:

- `docker compose config -q`
- `make up`
- `make migrate`
- `curl -f http://localhost:5245/api/health`
- `curl -f http://localhost:8738`
- `curl -f http://localhost:3001/ui`
- `make ps`

**Manual QA Required**:

- Student `FirstSecondary`: open `/student/packages`, `/student/teachers`, `/student/community`, `/student/public-exams`, `/student/shared-packages`, `/student/notifications`; verify only exact matching, platform-wide, stage-wide, or grade-all-subjects items appear.
- Student negative direct URL: open a non-matching package/lesson/video/exam/community target; verify denial without protected details.
- Student purchase negative: attempt to buy non-matching package/public exam with coupon/printable code; verify no discount, no balance deduction, no grant.
- Code redemption: redeem matching, non-matching, and platform-wide/stage-wide/grade-all-subjects codes.
- Admin: attempt to save/publish unscoped package/public exam/coupon/code/gift/shared package/community/notification; verify validation blocks.
- Admin: create each general scope level and verify expected student visibility.
- Profile change: grant access, change student grade or subject mapping, verify existing grant record remains but access is denied.

**End-of-Phase Report Format**:

- Implemented scope by backend/frontend/database.
- Exact changed files.
- Migrations created and backfill behavior.
- Commands run and results.
- Docker gate result.
- Manual QA checklist status.
- Known risks and owner follow-up.
- Go/no-go for next phase.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |

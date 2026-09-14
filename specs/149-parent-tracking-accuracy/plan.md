# Implementation Plan: Parent Tracking Accuracy

**Branch**: `[149-parent-tracking-accuracy]` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/149-parent-tracking-accuracy/spec.md`

## Summary

Fix the parent tracking payload and Android presentation so parent academic sections are driven by the teacher who owns the purchased lesson/package. The backend will resolve all active entitlement paths into a purchased lesson map, use that map for teachers, watch logs, exams, and homework, and return official balance ledger data. The Android parent app will consume the safer payload, keep schedules replaced by watch logs, and render exams/homework/balance with no-crash empty states.

## Technical Context

**Language/Version**: C# 13 on .NET 9 backend; Kotlin/Jetpack Compose Android parent app; TypeScript/Next.js present but not in scope  
**Primary Dependencies**: MediatR, EF Core 9, PostgreSQL/Npgsql, existing `AccessCheckService` entitlement cascade patterns, Android Gradle plugin, Retrofit/Gson, Jetpack Compose Material 3  
**Storage**: Existing PostgreSQL entities only; no schema migration expected  
**Testing**: `dotnet test` for application tests; `make build-mobile-android-offline` for Android compile; optional Docker health gate  
**Target Platform**: ASP.NET Core API plus Android parent app  
**Project Type**: Mobile app + backend API feature fix  
**Performance Goals**: Parent details API remains under existing standard API p95 expectation (<500ms for normal CRUD-like reads) on typical student data; avoid N+1 query loops over watch/exam/homework rows  
**Constraints**: Preserve `RequireParent` authorization, reuse existing DTO/API endpoint, remain backward tolerant on Android optional fields, no direct DB schema edits  
**Scale/Scope**: One parent details endpoint, one Android parent dashboard surface, existing test project coverage

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Backend Application query only plus existing API controller contract; Domain/Infrastructure schema unchanged; Android parent app UI/models updated; worker/frontend web out of scope; Docker config unchanged.
- **Architecture**: Keeps MediatR query boundary and `IAppDbContext`; no Domain dependency on EF-specific types is added.
- **Security**: Maintains `ParentController.GetStudentDetails` claim-based student profile resolution and `RequireParent` policy; query must never accept arbitrary student user ID from the client.
- **Academic content integrity**: Uses content hierarchy `Package -> Term -> ContentSection -> Lesson -> LessonVideo` as the teacher source. Exam/homework creator is not authoritative for parent filtering when content is attached to a purchased lesson.
- **Pricing/currency**: Uses `StudentBalance.CurrentBalance` and `BalanceTransaction` ledger; does not mix gamification points with money.
- **UI/design**: Android Compose surfaces keep Arabic labels and existing parent theme; no web design-system work in scope.
- **Automated tests required**: Backend tests for package/term/section/lesson/video grant resolution, wrong creator teacher filtering, homework/exam states, balance ledger, empty payloads; Android compile for model/UI safety.
- **Docker gate required**: `docker compose config -q`; `make up`; `make migrate` only if a migration appears (expected no); `curl -f http://localhost:5245/api/health`; `make ps`.
- **Manual QA required**: Parent Android flows for المشاهدات, الامتحانات, الواجبات, الرصيد with positive and negative teacher visibility checks.
- **Next-phase rule**: Failed automated/Docker/manual-verification blockers must be fixed or documented before the feature is reported ready.

## Project Structure

### Documentation (this feature)

```text
specs/149-parent-tracking-accuracy/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── parent-student-details.yaml
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/NaderGorge.API/Controllers/ParentController.cs
├── src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs
├── src/NaderGorge.Application/Services/AccessCheckService.cs
├── src/NaderGorge.Domain/Entities/
│   ├── CodeEntities.cs
│   ├── ContentEntities.cs
│   ├── ExamEntities.cs
│   ├── Homework/
│   ├── StudentBalance.cs
│   └── TrackingEntities.cs
└── tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs

mobile/parent-android/
├── app/src/main/java/com/nadergorge/parent/data/api/StudentDetailsResponse.kt
├── app/src/main/java/com/nadergorge/parent/ui/screens/DashboardScreen.kt
└── app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt
```

**Structure Decision**: Implement as a targeted backend Application query correction plus Android DTO/UI hardening. No new backend controller, no schema migration, no web frontend changes, no worker changes.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter Parent` covers parent API query behavior and controller authorization regressions.
- `dotnet build backend/NaderGorge.sln --no-restore` verifies backend compile after DTO/query changes.
- `make build-mobile-android-offline` verifies Android model/UI compile without re-downloading dependencies.
- If test fixtures need broader validation, run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj`.

**Docker Gate Required**:
- `docker compose config -q`
- `make up`
- `make migrate` only if EF migrations are generated; expected not required.
- `curl -f http://localhost:5245/api/health`
- `make ps`

**Manual QA Required**:
- Parent Android: linked student with purchases from two teachers; open المشاهدات, select each teacher, confirm only purchased lessons for that teacher appear.
- Parent Android: open الامتحانات, select teacher, verify `NotStarted`, `Passed`, and `Failed` status display plus mistake review for submitted attempts.
- Parent Android: open الواجبات, select teacher, verify `NotSubmitted`, `InProgress`, `PendingReview`, `Graded`, and `Missed` states plus mistake review only where valid.
- Parent Android: open الرصيد, compare current balance and newest transactions against admin/backend record.
- Negative check: content from a teacher whose grants are inactive, expired, cancelled, or unrelated must not appear.

**End-of-Phase Report Format**:
- Implemented scope and files changed.
- Commands run with pass/fail results.
- Docker gate result or documented local blocker.
- Manual QA checklist for product owner.
- Known risks and go/no-go readiness.

## Complexity Tracking

No constitution violations expected.

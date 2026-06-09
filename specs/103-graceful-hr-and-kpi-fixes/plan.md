# Implementation Plan: Graceful HR Warnings & KPI Fixes

**Branch**: `103-graceful-hr-and-kpi-fixes` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/103-graceful-hr-and-kpi-fixes/spec.md`

## Summary

This plan resolves:
1. Duplicate red toast notifications appearing on loading the Admin dashboard due to unconfigured employee profiles.
2. The EF Core 9 query translation failure when calculating editor performance and leaderboard stats.

The technical approach will:
- Modify `GetMyAttendanceQuery` and `/api/hr/attendance/my` to return `MyAttendanceStatusDto` which wraps the logs list and a `hasProfile` flag.
- Enable `ClockInOutWidget` and `/admin/hr/my-attendance` page to read `hasProfile` and show a warning panel without throwing errors.
- Remove redundant `toast.error` calls from components' local catch blocks when calling `clockIn`, `clockOut`, and `submitVacation` since the global Axios response interceptor already displays them.
- Refactor the `.GroupBy` LINQ queries in `GetMediaKpisQuery` and `GetCrmPerformanceReportQuery` to group by ID on the database side and join user names in-memory.

## Technical Context

**Language/Version**: C# (.NET 9) Backend, TypeScript (Next.js 16.2 / React 19) Frontend
**Primary Dependencies**: EF Core 9.0, MediatR, Axios, react-hot-toast
**Storage**: PostgreSQL
**Testing**: dotnet test, pytest
**Target Platform**: Linux/Docker
**Project Type**: Monorepo Web Application

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Backend Impact**: Yes, updates query handlers and controller responses.
- **Frontend Impact**: Yes, updates API services, widget layout, and page logic.
- **Worker Impact**: No.
- **Database Impact**: No schema changes.
- **Docker Impact**: Yes, backend container will be rebuilt.
- **Automated Tests**: Backend tests and python pytest suite must pass.
- **Manual QA**: Product owner tests loading `/admin` and `/admin/media` as Admin.

## Project Structure

### Documentation (this feature)

```text
specs/103-graceful-hr-and-kpi-fixes/
├── spec.md              # Feature specification
├── plan.md              # This file
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Detailed tasks breakdown
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   └── Controllers/
│   │       └── HrController.cs
│   └── NaderGorge.Application/
│       └── Features/
│           ├── HR/
│           │   ├── Queries/
│           │   │   └── GetMyAttendanceQuery.cs
│           │   └── Commands/
│           │       ├── ClockInCommand.cs
│           │       └── ClockOutCommand.cs
│           ├── CRM/
│           │   └── Queries/
│           │       └── GetCrmPerformanceReportQuery.cs
│           └── Admin/
│               └── Media/
│                   └── Queries/
│                       └── GetMediaKpisQuery.cs
```

```text
frontend/
├── src/
│   ├── services/
│   │   └── hr-service.ts
│   └── components/
│       └── admin/
│           ├── ClockInOutWidget.tsx
│           └── VacationRequestModal.tsx
└── src/app/
    └── admin/
        └── hr/
            └── my-attendance/
                └── page.tsx
```

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Run C# backend tests: `dotnet test backend/NaderGorge.sln`
- Run Python integration tests: `pytest tests/`

**Docker Gate Required**:
- Rebuild backend service: `docker compose up -d --build backend`
- Check health: `docker compose ps`

**Manual QA Required**:
- Log in as Root Admin `20000000000`. Dashboard `/admin` and page `/admin/hr/my-attendance` must load without error toasts.
- Open `/admin/media` -> Click "مؤشرات الأداء" -> Leaderboard must load.

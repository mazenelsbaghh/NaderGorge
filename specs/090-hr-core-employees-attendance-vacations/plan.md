# Implementation Plan: HR Core - Employees, Attendance, Vacations

**Branch**: `090-hr-core-employees-attendance-vacations` | **Date**: 2026-06-07 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/090-hr-core-employees-attendance-vacations/spec.md)
**Input**: Feature specification from `/specs/090-hr-core-employees-attendance-vacations/spec.md`

## Summary

This phase implements the HR Core capabilities for the Massar Platform, introducing Employee Profiles, daily Attendance logging (Clock-in/out), and Vacation Requests. We will establish the database tables, create CQRS backend handlers, write REST API controllers, design premium frontend widgets, and implement strict validation and audit logging.

## Technical Context

- **Language/Version**: .NET 9 (C# 13), Next.js 16.2.1 / React 19 (TypeScript 5.x)
- **Primary Dependencies**: Entity Framework Core 9, MediatR, FluentValidation, Lucide Icons, Framer Motion
- **Storage**: PostgreSQL 16
- **Testing**: `dotnet test` (xUnit backend tests), Python integration tests
- **Target Platform**: Web application (Admin Portal)
- **Performance Goals**: Clock-in and clock-out API requests return within 200ms.
- **Constraints**: Enforce a 1-to-1 relationship between User and EmployeeProfile. Access to HR admin routes requires `hr.manage` permission.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer Impact**:
  - **Database**: Add `EmployeeProfiles`, `AttendanceLogs`, and `EmployeeVacations` tables to PostgreSQL via EF Core migrations.
  - **Backend (Domain)**: Create domain models (`EmployeeProfile.cs`, `AttendanceLog.cs`, `EmployeeVacation.cs`) and status enums.
  - **Backend (Application)**: Implement CQRS commands and queries under a new `HR` application folder. Apply fluent validation. Write audit logs on salary or log updates.
  - **Backend (API)**: Expose `HrController` for employee self-service and `AdminHrController` (protected by `[HasPermission("hr.manage")]`) for HR admin features.
  - **Frontend**: Add `hr-service.ts`, add dashboard clock-in widgets, create vacation submission forms, and build HR admin dashboards for attendance lists and vacation moderation.
- **Automated Tests**:
  - Write xUnit tests to verify business constraints (e.g. late minutes calculation, unique user profiles, invalid clock-outs).
- **Manual QA**:
  - Verify that a staff user sees the clock-in widget, receives late badges if logging in late, and is restricted from the HR settings page unless granted the `hr.manage` permission.
- **Docker Gate**:
  - Run `docker compose config -q` to validate compose files.
  - Run `make migrate` to apply schema updates.
- **Quality Gate**:
  - All warnings or failing tests must be resolved before proceeding.

## Project Structure

### Documentation (this feature)

```text
specs/090-hr-core-employees-attendance-vacations/
├── spec.md              # Feature Specification
├── plan.md              # This file (Technical Implementation Plan)
└── checklists/
    └── requirements.md  # Specification Quality Checklist
```

### Source Code (repository root)

```text
backend/src/
├── NaderGorge.Domain/
│   └── Entities/
│       ├── EmployeeProfile.cs
│       ├── AttendanceLog.cs
│       └── EmployeeVacation.cs
├── NaderGorge.Infrastructure/
│   └── Data/
│       └── AppDbContext.cs               # Register new DbSet tables
├── NaderGorge.Application/
│   └── Features/HR/                      # [NEW] MediatR CQRS handlers
│       ├── Commands/
│       │   ├── ClockInCommand.cs
│       │   ├── ClockOutCommand.cs
│       │   ├── SubmitVacationCommand.cs
│       │   ├── AdminApproveVacationCommand.cs
│       │   ├── AdminRejectVacationCommand.cs
│       │   └── AdminSaveEmployeeProfileCommand.cs
│       └── Queries/
│           ├── GetMyAttendanceQuery.cs
│           ├── GetMyVacationsQuery.cs
│           ├── AdminGetEmployeesQuery.cs
│           ├── AdminGetAttendanceQuery.cs
│           └── AdminGetVacationsQuery.cs
├── NaderGorge.API/
│   └── Controllers/
│       ├── HrController.cs              # Employee endpoints
│       └── AdminHrController.cs         # Admin HR management

frontend/src/
├── services/
│   └── hr-service.ts                    # HR REST API service layer
├── components/admin/
│   ├── ClockInOutWidget.tsx             # Interactive dashboard log button
│   ├── EmployeeProfileDrawer.tsx        # HR configuration drawer in users
│   └── VacationRequestModal.tsx         # Vacation form dialog
├── app/admin/
│   ├── hr/
│   │   ├── page.tsx                     # HR Admin Dashboard view
│   │   └── my-attendance/
│   │       └── page.tsx                 # Employee my-attendance sheet
```

**Structure Decision**: Clean Architecture and Next.js App Router structure will be strictly followed. The backend is divided into domain entities, application MediatR commands, infrastructure DB sets, and API controllers. The frontend encapsulates HR logic within dedicated pages and modular UI components.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Build test: `dotnet build backend/NaderGorge.sln`
- Run unit tests: `dotnet test backend/NaderGorge.sln`
- Frontend lint check: `npm run lint` inside `frontend/`

**Docker Gate Required**:
- Run `docker compose config -q`
- When DB is connected, run EF Core migrations natively or via Compose.
- Assert API health endpoint `/api/health` responds with `200`.

**Manual QA Required**:
- **Admin Setup**: Admin logs into `/admin/users`, configures standard start time as `09:00 AM` for a staff member.
- **Clock In Flow**: Staff user logs in at `09:12 AM`, clicks "Clock In". Verify dashboard shows status `Late` with `12` minutes.
- **Vacation Workflow**: Staff user submits a vacation request. Admin approves it. Verify that the calendar updates and attendance log is seeded as `Leave` status.
- **Negative Check**: Login as staff user without `hr.manage` and verify that accessing `/admin/hr` results in a redirect to `/admin/unauthorized`.

**End-of-Phase Report Format**:
- Implemented scope details.
- Compilation and test execution results.
- EF Core migration verification.
- Risks/TODOs for Phase 3 (Tasks & Payroll).

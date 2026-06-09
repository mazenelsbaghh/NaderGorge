# Implementation Plan: Operations Task Manager and Approval Pipeline

**Branch**: `091-operations-task-manager` | **Date**: 2026-06-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/091-operations-task-manager/spec.md`

## Summary

This phase implements the daily Operations Task Manager and Approval Pipeline. We will build database schemas for tasks and comments, write backend MediatR handlers for scheduling, commenting, status updates, and overdue checks, develop REST controllers, and design responsive user interfaces for both the assistant dashboard and admin operations page with premium UI details (using custom Tailwind design tokens and Cairo/Montserrat fonts).

## Technical Context

- **Language/Version**: .NET 9 (C# 13), Next.js 16.2.1 / React 19 (TypeScript 5.x)
- **Primary Dependencies**: Entity Framework Core 9, MediatR, FluentValidation, Lucide Icons, Framer Motion
- **Storage**: PostgreSQL 16
- **Testing**: `dotnet test` (xUnit tests), Playwright E2E tests, Python integration checks
- **Target Platform**: Web Application (Admin & Assistant Dashboards)
- **Performance Goals**: Task creation and comments posting APIs respond in under 200ms.
- **Constraints**: Enforce role-based permission boundaries (e.g. only staff/assistants can be assignees; students are barred; only supervisors/admins can approve task completions).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer Impact**:
  - **Database**: Add `TaskItems` and `TaskComments` tables to PostgreSQL.
  - **Backend (Domain)**: Create entities (`TaskItem.cs`, `TaskComment.cs`) and enums (`TaskStatus.cs`, `TaskPriority.cs`).
  - **Backend (Application)**: Write CQRS MediatR commands/queries under new `Operations` feature folders.
  - **Backend (API)**: Expose `AssistantController` for self-service queues and `AdminOperationsController` for admin management.
  - **Frontend**: Create client API wrapper `assistant-service.ts`, build task cards, comments streams, and task creation modals. Expose `/assistant/dashboard` for assistant queues and `/admin/operations` for master task controls.
- **Automated Tests**:
  - Write xUnit tests to verify overdue computation, completion constraints (assignee checks), and status downgrade restrictions.
- **Manual QA**:
  - Verify that a manager can create and assign a task, an assistant can update it and request closure, and a supervisor can approve it. Ensure student roles receive 403 Forbidden.
- **Docker Gate**:
  - Run `docker compose config -q` to validate compose configurations.
  - Run `make migrate` to apply new tables.
- **Quality Gate**:
  - All compilation warnings and ESLint issues must be resolved before merging.

## Project Structure

### Documentation (this feature)

```text
specs/091-operations-task-manager/
├── spec.md              # Feature Specification
├── plan.md              # This file (Technical Implementation Plan)
├── research.md          # Decision log
├── data-model.md        # Database schema details
└── checklists/
    └── requirements.md  # Specification Quality Checklist
```

### Source Code (repository root)

```text
backend/src/
├── NaderGorge.Domain/
│   └── Entities/
│       ├── TaskItem.cs                   # Core task properties
│       └── TaskComment.cs                # Collaboration comments
├── NaderGorge.Infrastructure/
│   └── Data/
│       └── AppDbContext.cs               # Register new DbSet tables
├── NaderGorge.Application/
│   └── Features/Operations/              # MediatR CQRS handlers
│       ├── Commands/
│       │   ├── CreateTaskCommand.cs
│       │   ├── UpdateTaskStatusCommand.cs
│       │   └── AddTaskCommentCommand.cs
│       └── Queries/
│           ├── GetMyTasksQuery.cs
│           ├── GetAdminTasksQuery.cs
│           └── GetTaskDetailsQuery.cs
├── NaderGorge.API/
│   └── Controllers/
│       ├── AssistantController.cs        # Self-service actions
│       └── AdminOperationsController.cs  # Administrative tasks and reviews

frontend/src/
├── services/
│   └── assistant-service.ts             # API client wrapper
├── components/assistant/
│   ├── TaskBoard.tsx                    # Drag-and-drop or status board
│   ├── TaskCard.tsx                     # Compact task summary
│   ├── TaskDetailsModal.tsx             # Comments and details overlay
│   └── TaskCreateModal.tsx              # Form to spawn new tasks
├── app/
│   ├── assistant/
│   │   └── dashboard/
│   │       └── page.tsx                 # Assistant Task Queue page
│   └── admin/
│       └── operations/
│           └── page.tsx                 # Operations Task Manager and Approvals page
```

**Structure Decision**: Clean Architecture layers will be strictly followed. The frontend is separated into services, components under `components/assistant/`, and pages inside the Next.js app router.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Build check: `dotnet build backend/`
- Run backend tests: `dotnet test backend/`
- Frontend lint check: `npm run lint` inside `frontend/`

**Docker Gate Required**:
- Run `docker compose config -q`
- Run `make migrate` to apply database changes.

**Manual QA Required**:
- **Admin Setup**: Admin creates a task and assigns it.
- **Progress Tracking**: Assistant logs in, updates status to `InProgress`, adds a comment, and requests completion.
- **Approval Flow**: Supervisor approves task completion. Verify status transitions to `Completed`.
- **Negative Check**: Student user tries to fetch `/api/hr` or `/api/assistant/tasks` and is blocked with `403 Forbidden` or redirected to `/admin/unauthorized`.

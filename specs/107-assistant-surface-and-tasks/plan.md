# Implementation Plan: Assistant/Staff Surface and Task Workflow

**Branch**: `107-assistant-surface-and-tasks` | **Date**: 2026-06-09 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/107-assistant-surface-and-tasks/spec.md)
**Input**: Feature specification from `/specs/107-assistant-surface-and-tasks/spec.md`

## Summary
Implement Phase 2 - Assistant/Staff Surface and Task Workflow by securing `/assistant` routes, creating an independent navigation bar for Assistant/Staff roles, isolating existing CRM, Chat, and Dashboard pages into this layout, implementing task detail and management pages, and enforcing backend task ownership checks.

## Technical Context

**Language/Version**: TypeScript (Next.js 16.2.1 / React 19) (Frontend), C# 13 (.NET 9.0) (Backend)
**Primary Dependencies**: `framer-motion` (Frontend), `lucide-react` (Frontend), MediatR (Backend), Entity Framework Core 9 (Backend)
**Storage**: PostgreSQL (Data Store)
**Testing**: Vitest / Playwright (Frontend), xUnit (Backend), Python (E2E Integration Smoke Tests)
**Target Platform**: Linux (Docker containers) / Web browsers
**Project Type**: Multi-surface Web Application
**Performance Goals**: Instant client-side page routing transitions (<100ms), 60fps animations
**Constraints**: Cairo/Tajawal Arabic fonts, strict role-based route separation
**Scale/Scope**: 1 dedicated layout shell (`/assistant`), 4 new/updated routes, 3 backend authorization handler modifications

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**:
  - Frontend: `frontend/src/app/assistant/layout.tsx` (New Assistant shell), `frontend/src/app/assistant/tasks/*` (New pages), `frontend/src/app/assistant/attendance/*` (New page), `frontend/src/app/assistant/vacations/*` (New page)
  - Backend: `GetTaskDetailsQueryHandler.cs`, `AddTaskCommentCommandHandler.cs`, `UpdateTaskStatusCommandHandler.cs` (Ownership security checks)
- **Automated tests required**: Frontend build verification and Python E2E surface isolation tests.
- **Manual QA flows**: Login as Assistant, navigate to Dashboard/Tasks/CRM/Chat/Attendance/Vacations, update task, verify supervisor approval logic, try accessing admin pages.
- **Docker gate commands**: `docker compose config -q`, `docker compose ps`
- **Explicit decision**: Next phase cannot start until failed gates are fixed.

## Project Structure

### Documentation (this feature)

```text
specs/107-assistant-surface-and-tasks/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 data structure mappings
├── quickstart.md        # Quickstart instructions
└── checklists/
    └── requirements.md  # Specification quality checklist
```

### Source Code (repository root)

```text
backend/
└── src/
    ├── NaderGorge.API/
    │   └── Controllers/
    │       └── AssistantController.cs
    └── NaderGorge.Application/
        └── Features/
            └── Operations/
                ├── Queries/
                │   ├── GetTaskDetailsQuery.cs
                │   └── GetMyTasksQuery.cs
                └── Commands/
                    ├── AddTaskCommentCommand.cs
                    └── UpdateTaskStatusCommand.cs

frontend/
└── src/
    ├── app/
    │   └── assistant/
    │       ├── layout.tsx         # NEW - Assistant layout shell with AssistantGuard and Navbar
    │       ├── dashboard/
    │       │   └── page.tsx       # MODIFIED - Dashboard page wrapper
    │       ├── tasks/
    │       │   ├── page.tsx       # NEW - Tasks list page
    │       │   └── [id]/
    │       │       └── page.tsx   # NEW - Task details page
    │       ├── attendance/
    │       │   └── page.tsx       # NEW - Personal attendance log page
    │       └── vacations/
    │           └── page.tsx       # NEW - Vacation requests and balance page
    └── components/
        └── assistant/             # Reusable assistant elements
```

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `npm run build` inside `frontend/` to verify TS/Next compile passes.
- `dotnet build` inside `backend/` to verify C# compile passes.
- `python -m pytest tests/` or similar E2E test verification.

**Docker Gate Required**:
- `docker compose build assistant backend`
- Check container health for `massar_assistant` on port `8742`.

**Manual QA Required**:
- Log in as Assistant, ensure menu contains: Dashboard, Tasks, CRM, Chat, Attendance, Vacations, Notifications.
- Open `staff.localhost:8742/assistant/tasks`, view lists.
- Click a task, modify its status to "InProgress", then "Review". Verify regular assistants cannot complete tasks directly.
- Log in as Admin/Supervisor, navigate to tasks page, approve the task under review, verify status becomes Completed.
- As Assistant, try navigating to `admin.localhost:8740/admin/settings`, verify it redirects to `/login` or shows 403.
- As Assistant, try to load details of a task assigned to someone else (`/assistant/tasks/<other-guid>`), verify 403 Forbidden page.

**End-of-Phase Report Format**:
- Implemented files list.
- Verification checks output (build, tests).
- QA checklist execution status.

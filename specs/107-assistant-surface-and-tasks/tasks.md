# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in [spec.md](./spec.md)
- [x] Phase 2: Technical Planning (`speckit-plan`) completed in [plan.md](./plan.md)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed in [tasks.md](./tasks.md)

---

# Tasks: Assistant/Staff Surface and Task Workflow

**Input**: Design documents from `/specs/107-assistant-surface-and-tasks/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: E2E domain surface isolation checks and backend build checks are mandatory.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify the assistant folder structure and setup base references.

- [x] T001 [P] [US1] Create directory `frontend/src/app/assistant/` and sub-pages folders (`tasks/`, `attendance/`, `vacations/`, `notifications/`) if missing.
- [x] T002 [P] [US1] Check `frontend/src/components/layout/AssistantGuard.tsx` logic to ensure role validation maps correctly.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Set up the assistant layout shell and navbar.

- [x] T003 [US1] Create `frontend/src/app/assistant/layout.tsx` to secure the entire route group under `/assistant/*` using `AssistantGuard` and render a consistent responsive layout.
- [x] T004 [US1] Implement a premium, Arabic-first sidebar/navbar in `frontend/src/components/assistant/AssistantNavbar.tsx` containing Dashboard, Tasks, CRM, Chat, Attendance, Vacations, and Notifications. Apply permission checks to hide items the user does not possess permissions for.

**Checkpoint**: Foundation ready - assistant shell layout is functional.

---

## Phase 3: User Story 1 - Assistant/Staff Space Isolation (Priority: P1)

**Goal**: Isolate existing assistant pages and routes into the assistant workspace.

- [x] T005 [US1] Update `frontend/src/app/assistant/dashboard/page.tsx` to mount nicely in the new layout and display basic stats summaries.
- [x] T006 [US1] Update `frontend/src/app/assistant/crm/page.tsx` and `/assistant/chat/page.tsx` to align with the new layout, keeping styles clean and consistent.
- [x] T007 [US1] Create a notifications list view page at `frontend/src/app/assistant/notifications/page.tsx` using Sand/Gold design tokens.

**Checkpoint**: User Story 1 isolation is complete.

---

## Phase 4: User Story 2 - Task Workflow and Ownership Checks (Priority: P2)

**Goal**: Secure task detail reads/updates and comments, and implement tasks frontend.

- [x] T008 [P] [US2] In `backend/src/NaderGorge.Application/Features/Operations/Queries/GetTaskDetailsQuery.cs`, update query request to accept `UserId` and role/permission bypass info. Check if the user is authorized (Assignee, Creator, Admin, or Supervisor) and throw `UnauthorizedAccessException` if not.
- [x] T009 [P] [US2] Update `AssistantController.cs` details endpoint to pass auth details to `GetTaskDetailsQuery`.
- [x] T010 [P] [US2] In `backend/src/NaderGorge.Application/Features/Operations/Commands/AddTaskCommentCommand.cs`, verify if user has task access (Assignee, Creator, Admin, Supervisor) before allowing comment insertion.
- [x] T011 [P] [US2] In `backend/src/NaderGorge.Application/Features/Operations/Commands/UpdateTaskStatusCommand.cs`, enforce that non-managers (regular assistants) can only update tasks assigned to them, and restrict direct status transition to `Completed` (only allowing transition to `Review` status).
- [x] T012 [US2] Create `/assistant/tasks` view page at `frontend/src/app/assistant/tasks/page.tsx` displaying the assignee's list of tasks with filters (Pending, InProgress, Review, Completed) using Tajawal font.
- [x] T013 [US2] Create `/assistant/tasks/[id]` detail view page at `frontend/src/app/assistant/tasks/[id]/page.tsx` allowing the assistant to read details, add comments/attachments, and change status (e.g. InProgress, Review).

**Checkpoint**: Task ownership checks and frontend details view are functional.

---

## Phase 5: User Story 3 - HR Core: Attendance and Vacations (Priority: P3)

**Goal**: Bind HR page interfaces for the logged-in assistant.

- [x] T014 [US3] Create `/assistant/attendance` view page at `frontend/src/app/assistant/attendance/page.tsx` displaying working hours and sign-in/out records.
- [x] T015 [US3] Create `/assistant/vacations` view page at `frontend/src/app/assistant/vacations/page.tsx` showcasing remaining balance, status list of requests, and a form to submit a new vacation request.

**Checkpoint**: All user stories functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup, linting, and compilation checks.

- [x] T016 [P] Verify Cairo/Tajawal font styling, glassmorphism shadows, and sand background tones across all assistant pages.
- [x] T017 Run Next.js linting and production build: `npm run lint` and `npm run build` in `frontend/`.
- [x] T018 Run C# backend compile check: `dotnet build` in `backend/`.

---

## Phase 7: Quality Gates (Mandatory)

**Purpose**: Run Clean Code Guard and Test Guard.

- [x] T019 Run `clean-code-guard` on all modified/added production-code files.
- [x] T020 Run `test-guard` on test suites.
- [x] T021 Run python-based surface verification scripts.

---

## Phase 8: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Confirm everything works inside the Docker stack and report manual QA.

- [x] T022 Run `docker compose config -q`.
- [x] T023 Run `make up` and check service health.
- [x] T024 Perform manual QA testing of assistant logins, details checks, and task workflow transitions. Write the final achievements and walkthrough report.

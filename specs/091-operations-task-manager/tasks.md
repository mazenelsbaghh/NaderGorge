# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Operations Task Manager and Approval Pipeline

**Input**: Design documents from `/specs/091-operations-task-manager/`
**Prerequisites**: plan.md (required), spec.md (required)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Path Conventions

- **Web app**: `backend/src/`, `frontend/src/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Verify spec directory and configuration files under `specs/091-operations-task-manager/`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core model extensions and database tables that MUST be complete before user stories can start

- [x] T002 Backend Domain: Create `TaskStatus.cs` enum in `backend/src/NaderGorge.Domain/Enums/TaskStatus.cs` (New=1, InProgress=2, Review=3, Completed=4, Paused=5, Overdue=6).
- [x] T003 Backend Domain: Create `TaskPriority.cs` enum in `backend/src/NaderGorge.Domain/Enums/TaskPriority.cs` (Low=1, Medium=2, High=3, Critical=4).
- [x] T004 Backend Domain: Create `TaskItem.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/TaskItem.cs` storing `Title`, `Description`, `AssigneeId` (Guid, references User), `CreatedById` (Guid, references User), `Status` (enum), `Priority` (enum), `DueDate` (DateTime?), `CompletedAt` (DateTime?), and `ApprovedById` (Guid?, references User).
- [x] T005 Backend Domain: Create `TaskComment.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/TaskComment.cs` storing `TaskId` (Guid, references TaskItem), `UserId` (Guid, references User), `Content` (string), `AttachmentUrl` (string), and `CreatedAt` (DateTime).
- [x] T006 Backend Infrastructure: Register DbSets for `TaskItems` and `TaskComments` in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`. Configure FK relationships and cascade deletes.
- [x] T007 Database Migration: Generate an EF Core migration named `AddOperationsTaskEntities` by running `dotnet ef migrations add AddOperationsTaskEntities --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API`.

**Checkpoint**: Foundation ready - user story database tables and domain objects are established.

---

## Phase 3: User Story 1 - Task Creation & Assignment (Priority: P1) 🎯 MVP

**Goal**: Expose administration endpoints to configure, assign, and search daily operational tasks.

**Independent Test**: Verify that Admin can create tasks for staff members and search them via API.

### Tests for User Story 1
- [x] T008 [P] [US1] Application Tests: Create unit test in `backend/tests/NaderGorge.Application.Tests/Operations/TaskTests.cs` asserting that attempting to assign a task to a `Student` role user throws validation or operations error.

### Implementation for User Story 1
- [x] T009 [P] [US1] Backend Application: Create `CreateTaskCommand.cs` in `backend/src/NaderGorge.Application/Features/Operations/Commands/CreateTaskCommand.cs` to insert new tasks. Verify assignee is not a Student. Add FluentValidation.
- [x] T010 [P] [US1] Backend Application: Create queries `GetMyTasksQuery.cs` and `GetAdminTasksQuery.cs` in `backend/src/NaderGorge.Application/Features/Operations/Queries/` to load tasks for assistants (self-service) and admins (operations control). Ensure overdue status is evaluated dynamically.
- [x] T011 [US1] Backend API: Implement `AssistantController.cs` (`api/assistant/tasks` GET endpoint) and `AdminOperationsController.cs` (`api/admin/operations/tasks` GET/POST endpoints).
- [x] T012 [P] [US1] Frontend Service: Create REST client service methods in `frontend/src/services/assistant-service.ts` to fetch and submit tasks.
- [x] T013 [US1] Frontend Component: Build `TaskCreateModal.tsx` in `frontend/src/components/assistant/TaskCreateModal.tsx` for manager task setup.
- [x] T014 [US1] Frontend Page: Build page `/admin/operations/page.tsx` for master task administration and `/assistant/dashboard/page.tsx` for assistant task queue.

**Checkpoint**: User Story 1 tasks creation and queues are fully functional.

---

## Phase 4: User Story 2 - Task Collaboration & Progress Transition (Priority: P1) 🎯 MVP

**Goal**: Enable staff to update status (InProgress, Review, Paused), post comments, and add attachment links.

**Independent Test**: Login as staff, start task, add comments, and transition status.

### Tests for User Story 2
- [x] T015 [P] [US2] Application Tests: Create unit tests in `backend/tests/NaderGorge.Application.Tests/Operations/TaskTests.cs` validating status transition constraints (preventing non-managers from downgrading Completed or Review tasks).

### Implementation for User Story 2
- [x] T016 [US2] Backend Application: Create `UpdateTaskStatusCommand.cs` in `backend/src/NaderGorge.Application/Features/Operations/Commands/UpdateTaskStatusCommand.cs` ensuring proper permission guard.
- [x] T017 [US2] Backend Application: Create `AddTaskCommentCommand.cs` in `backend/src/NaderGorge.Application/Features/Operations/Commands/AddTaskCommentCommand.cs` to add collaboration comments with author user details.
- [x] T018 [US2] Backend API: Connect status update and comment posting endpoints in `AssistantController.cs`.
- [x] T019 [US2] Frontend Component: Create task detail and comments viewer component `TaskDetailsModal.tsx` in `frontend/src/components/assistant/TaskDetailsModal.tsx`.
- [x] T020 [US2] Frontend Integration: Hook `TaskDetailsModal` trigger on dashboard queues.

**Checkpoint**: User Story 2 progress updates and commenting systems are functional.

---

## Phase 5: User Story 3 - Operations Approval Pipeline (Priority: P2)

**Goal**: Enforce supervisor approvals before completing tasks and logging resolution metadata.

**Independent Test**: Transition task to Review, approve as manager, verify Completed state.

### Tests for User Story 3
- [x] T021 [P] [US3] Application Tests: Create unit tests in `backend/tests/NaderGorge.Application.Tests/Operations/TaskTests.cs` validating permission gates on task approval (blocking non-supervisors).

### Implementation for User Story 3
- [x] T022 [US3] Backend Application: Create command `AdminResolveApprovalCommand.cs` in `backend/src/NaderGorge.Application/Features/Operations/Commands/AdminResolveApprovalCommand.cs` (accepting `Approve` or `Reject` actions). Approving transitions task to `Completed` and sets `CompletedAt` and `ApprovedById`.
- [x] T023 [US3] Backend API: Wire vacation / operations approvals endpoints in `AdminOperationsController.cs`.
- [x] T024 [US3] Frontend Integration: Display approval and rejection CTA buttons in the `/admin/operations` page task list or details popup.

**Checkpoint**: User Story 3 operations approvals are completed and verified.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Format cleanups, linting, and error handling

- [x] T025 Run code formatting tools (`dotnet format` and `npx prettier --write`) across workspaces.
- [x] T026 Audit frontend and backend for build warnings and resolve them.

---

## Phase 7: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment.

- [x] T027 Run backend tests (`dotnet test`) and verify all pass.
- [x] T028 Run frontend lint checks (`npm run lint`).
- [x] T029 Output the final verification report.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories.
- **User Stories (Phase 3+)**: All depend on Foundational phase completion.
- **Polish (Final Phase)**: Depends on all user stories being complete.
- **End-of-Phase Verification**: Depends on all implementation and polish tasks.

# Tasks: Exam Dashboard & Timers

**Input**: Design documents from `/specs/023-exam-dashboard-timers/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/endpoints.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Verify database connection and backup current state before generating new migrations.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented
**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Update `Exam` entity to include `int? DurationMinutes` in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`
- [x] T003 [P] Update `ExamQuestion` entity to include `int? DurationSeconds` in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`
- [x] T004 [P] Update `StudentExamAttempt` entity to include `DateTime? StartedAt` and `bool IsTimeExpired = false` in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`
- [x] T005 Create EF Core Migration `AddExamTimersAndDashboard` and apply to database via CLI.

**Checkpoint**: Foundation ready - Database schema supports timers and attempt tracking.

---

## Phase 3: User Story 1 - Admin Exam Dashboard & Student Results (Priority: P1) 🎯 MVP

**Goal**: Admins can view exam statistics and student submissions in a unified table.
**Independent Test**: Admin can navigate to `/admin/exams/{id}/dashboard`, view aggregate stats, and see mock or existing student submissions.

### Implementation for User Story 1

- [x] T006 [P] [US1] Create `ExamDashboardDto` and `StudentExamResultSummaryDto` models in `backend/src/NaderGorge.Application/Features/Admin/Queries/Models/`
- [x] T007 [P] [US1] Implement `GetExamDashboardQuery` and its handler in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetExamDashboardQuery.cs`
- [x] T008 [P] [US1] Add `GET /api/admin/exams/{examId}/dashboard` endpoint to `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [x] T009 [P] [US1] Add `getExamDashboard` fetching logic to `frontend/src/services/admin-service.ts`
- [x] T010 [US1] Create Exam Dashboard UI component page in `frontend/src/app/admin/content/exams/[id]/page.tsx`
- [x] T011 [US1] Add a link/button pointing to the Exam Dashboard from existing Admin lesson views or exam lists.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Admin Defines Time Limits (Priority: P2)

**Goal**: Admins can specify global and per-question time limits during exam creation.
**Independent Test**: Admin saves a new exam with a 15-minute global limit and 60-second question limit, and verifies it persists in the database.

### Implementation for User Story 2

- [x] T012 [P] [US2] Update `CreateInlineExamCommand` and nested DTOs to accept `DurationMinutes` and `DurationSeconds` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs`
- [x] T013 [P] [US2] Update request payload models in `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [x] T014 [P] [US2] Update frontend types and API payload in `frontend/src/services/admin-service.ts` to pass durations.
- [x] T015 [US2] Add `DurationMinutes` input field in Step 1 of `frontend/src/components/admin/InlineExamEditor.tsx`
- [x] T016 [US2] Add `DurationSeconds` input field to `frontend/src/components/admin/QuestionEditor.tsx`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Resilient Student Exam Timers (Priority: P3)

**Goal**: Students experience a server-synced timer that continues ticking regardless of browser refreshes or navigation, and strictly enforces expiry.
**Independent Test**: Student starts exam, Refreshes page, timer resumes correctly. Timer reaches zero -> auto submits.

### Implementation for User Story 3

- [x] T017 [P] [US3] Update `GetStudentExamQuery` handler to return `DurationMinutes`, `DurationSeconds`, and `StartedAt` in `GetStudentExamQuery.cs`
- [x] T018 [P] [US3] Create `StartExamAttemptCommand` and handler to set `StartedAt = DateTime.UtcNow` in `StartExamAttemptCommand.cs`.
- [x] T019 [P] [US3] Add `POST /api/exams/{examId}/start` endpoint to `ExamsController.cs`
- [x] T020 [P] [US3] Update `SubmitExamCommand` handler to enforce absolute expiry logic `IsTimeExpired`.
- [x] T021 [P] [US3] Add `startExamAttempt` endpoint call to `frontend/src/services/content-service.ts`
- [x] T022 [US3] Create `ExamTimer.tsx` in `frontend/src/components/student`
- [x] T023 [US3] Integrate `StartExamAttemptCommand` and `ExamTimer` into the student UI.'s exam taking UI, enabling auto-submit on zero.

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T024 Add validation ensuring `DurationSeconds` doesn't contradict `DurationMinutes`.
- [ ] T025 Double-check timezone conversions handling (UTC strictly).

---

## Dependencies & Execution Order

### Phase Dependencies
- **Foundational (Phase 2)**: BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion. Story 3 highly depends on Story 2 being complete so time limits actually exist.

### Parallel Opportunities
- Entity and DTO modifications across different layers can run in parallel.
- Admin APIs (US1, US2) and Student APIs (US3) can be worked on concurrently by different developers.

## Implementation Strategy
1. **MVP First**: Complete Phase 2, then US1 to gain visibility into the data.
2. **Incremental Delivery**: Add US2 so limits can be created. Finally add US3 to enforce them on the student side.

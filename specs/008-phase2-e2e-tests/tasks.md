# Task Breakdown: E2E Testing for Phase 2 Academic Operations

**Short Name**: phase2-e2e-tests

## Phase 1: Foundational Setup (E2E Data Orchestration)

**Goal**: Extend the E2eTestingController to support staging specific Phase 2 test data (like Homework submissions, Tasks, Gamification) so the frontend tests can run quickly and deterministically without touching the UI for setups.

- [x] T001 [P] Add `CreateLessonWithHomework` endpoint in `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs`
- [x] T002 [P] Add `CreateAssistantAccount` endpoint in `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs`
- [x] T003 [P] Add gamification/points wipe and reset endpoint in `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs`

**Checkpoint**: Backend test data utilities are ready to generate required states for the Playwright tests.

---

## Phase 2: User Story 1 & 5 - Student Academic Journey & Gamification

**Goal**: Verify that a student can complete a lesson, submit homework, and receive gamification points properly in the E2E environment.

- [x] T004 [US1] Create scaffolding for `frontend/tests/e2e/student-academic.spec.ts`
- [x] T005 [US1] Write test: "Student registers, opens an active lesson, and submits the homework"
- [x] T006 [US5] Write test: "Student gamification points are validated on the Gamification Widget after homework submission"

**Checkpoint**: E2E verifies that student academic completion and gamification rewards are working correctly.

---

## Phase 3: User Story 3 - Assistant Dashboard

**Goal**: Verify that an assistant can view their queue, see pending submissions, and resolve them.

- [x] T007 [US3] Create scaffolding for `frontend/tests/e2e/assistant-dashboard.spec.ts`
- [x] T008 [US3] Write test: "Assistant logs in, views the pending homework in the task board, and resolves it"
- [x] T009 [US3] Write test: "Resolved task no longer appears in the pending queue"

**Checkpoint**: E2E verifies the assistant moderation loop correctly picks up tasks and disposes of them.

---

## Phase 4: User Story 4 - Parent Reporting

**Goal**: Verify that the parent report endpoint correctly aggregates the student's metrics.

- [x] T010 [US4] Create scaffolding for `frontend/tests/e2e/parent-report.spec.ts`
- [x] T011 [US4] Write test: "Accessing the parent report with a valid student ID displays correct metrics and warnings"
- [x] T012 [US4] Write test: "Accessing the parent report with an invalid ID displays a 'Not found' notice"

**Checkpoint**: E2E verifies the unauthenticated parent visibility feature.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T013 Run `npm run test:e2e` locally to ensure all Academic Operations tests pass under 2 minutes.
- [x] T014 Review and adjust any flaky UI locators across the new test suite utilizing `data-testid` where applicable.

---

## Dependencies & Execution Order

### Phase Dependencies
- **Phase 1 (Setup)**: Must be completed first to allow frontend tests to seed data.
- **Phases 2-4 (Scenarios)**: Can be executed fully in parallel once Phase 1 is complete.
- **Phase 5 (Polish)**: Depends on all test phases being complete.

### Parallel Opportunities
- After T001-T003, engineers can split T004-T006, T007-T009, and T010-T012 amongst themselves to write the test files concurrently.

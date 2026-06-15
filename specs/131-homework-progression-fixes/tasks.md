# Tasks: Homework Progression & Location Fixes

**Input**: Design documents from `/specs/131-homework-progression-fixes/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure verification

- [ ] T001 Verify git branch is set to `131-homework-progression-fixes` and setup-plan.sh runs cleanly

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core checks before starting implementation

- [ ] T002 Verify that current dev environment builds and compile is successful for both backend and frontend

---

## Phase 3: User Story 1 - Section-Isolated Progression Locking (Priority: P1) 🎯 MVP

**Goal**: Restrict the previous lesson lookup to only retrieve lessons within the same section.

**Independent Test**: Remove the fallback code blocks in both content query files, compile backend, and verify that progression locks are isolated to each section.

### Implementation for User Story 1

- [ ] T003 [P] [US1] Remove section fallback logic from `GetLessonDetailQueryHandler` in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs`
- [ ] T004 [P] [US1] Remove section fallback logic from `GetLessonsQuery` (specifically `GetBlockingStateAsync`) in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonsQuery.cs`

**Checkpoint**: Section-isolated progression locks are functional and compilation is verified.

---

## Phase 4: User Story 2 - Standalone Homework Solving Workspace (Priority: P1)

**Goal**: Remove duplicate inline homework solver component from the lesson viewer.

**Independent Test**: Navigate to the lesson page, verify that the bottom of the page no longer renders any homework question or stepper elements, and that the carousel's homework button remains active and redirects to the standalone route.

### Implementation for User Story 2

- [ ] T005 [US2] Remove homework answers state, handlers, imports, and the JSX block rendering the multi-step questionnaire at the bottom of the page in `frontend/src/components/content/LessonViewer.tsx`

**Checkpoint**: Inline homework solving layout is removed, while standalone workspace page remains fully active and reachable.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Verification and final code review.

- [ ] T006 [P] Verify backend builds successfully using `dotnet build` and expect 0 errors
- [ ] T007 [P] Verify frontend builds successfully using `npm run build` and expect 0 errors
- [ ] T008 [P] Run `clean-code-guard` against changed production files to review quality
- [ ] T009 [P] Run `test-guard` against changed files to ensure test sanity

---

## Phase 6: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment.

- [ ] T010 Run `docker compose config -q` to verify Docker configuration
- [ ] T011 Run `make up` and expect all services to be healthy
- [ ] T012 Verify manual QA checklist items, ensuring 0 homework questions render inline, and section isolation passes successfully
- [ ] T013 Run feature tests via Playwright and write the end-of-phase report. Verify that the result shows all tests pass.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Setup.
- **User Stories (Phases 3 and 4)**: Depend on Foundational.
- **Polish (Phase 5)**: Depends on all user stories.
- **End-of-Phase Verification (Phase 6)**: Depends on Polish.

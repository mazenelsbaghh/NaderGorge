---
description: "Task list template for feature implementation"
---

# Tasks: E2E Testing Coverage

**Input**: Design documents from `/specs/004-e2e-testing-all/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Initialize Playwright framework inside `frontend/` directory (e.g., via `npm install -D @playwright/test`)
- [x] T002 [P] Create and configure `frontend/playwright.config.ts` tailored for Next.js and backend integration

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 Create `backend/src/NaderGorge.API/appsettings.E2e.json` configuring the `nadergorge_e2e` DB pipeline
- [x] T004 Implement `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs` providing DB clearance and user seeding specific to `E2e` environment
- [x] T005 Construct `frontend/tests/fixtures/global-setup.ts` to call the backend seed API before the test suite begins

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Auth and Access Flow (Priority: P1) 🎯 MVP

**Goal**: Automatically verify that a student can successfully log in, get rejected on bad passwords, and that device limits are enforced securely.

**Independent Test**: Can be validated by successfully executing `npx playwright test tests/e2e/auth.spec.ts` against the E2E API.

### Implementation for User Story 1

- [x] T006 [P] [US1] Create basic success login test in `frontend/tests/e2e/auth.spec.ts` using seeded student info
- [x] T007 [US1] Add test for rejecting invalid password logins into `frontend/tests/e2e/auth.spec.ts`
- [x] T008 [US1] Implement device limit test in `frontend/tests/e2e/auth.spec.ts` simulating login from 3 concurrent synthetic browsers

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Admin Content Management (Priority: P1)

**Goal**: Ensure Admin accounts can successfully create Packages, Sections, Lessons, and Videos through the Next.js UI.

**Independent Test**: Can be independently tested by running `npx playwright test tests/e2e/admin-content.spec.ts`.

### Implementation for User Story 2

- [x] T009 [P] [US2] Create basic package creation UI automation test in `frontend/tests/e2e/admin-content.spec.ts`
- [x] T010 [US2] Expand test to cover creating a section, lesson, and embedding a Vimeo video underneath that package

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Student Lesson Consumption & Exams (Priority: P1)

**Goal**: Verify students can consume lessons, track video views accurately to limits, and take interactive exams.

**Independent Test**: Run `npx playwright test tests/e2e/student-journey.spec.ts`.

### Implementation for User Story 3

- [x] T011 [P] [US3] Add `POST /api/e2e/grant-package` Helper to `E2eTestingController.cs` to instantly unlock a package for the test user
- [x] T012 [US3] Create `frontend/tests/e2e/student-journey.spec.ts` orchestrating watch limit increments
- [x] T013 [US3] Add automated flow for answering & submitting exam questions to `student-journey.spec.ts`

**Checkpoint**: All P1 user stories should now be independently functional

---

## Phase 6: User Story 4 - Access Codes and Unlock (Priority: P2)

**Goal**: Verify bulk generation of Codes as an Admin and single use redemption as a Student context.

**Independent Test**: Run `npx playwright test tests/e2e/codes.spec.ts`.

### Implementation for User Story 4

- [x] T014 [US4] Create `frontend/tests/e2e/codes.spec.ts` automating the admin generation form
- [x] T015 [US4] Add a cross-role scenario transferring the generated code to a student login and performing redemption through the code dialog in `frontend/tests/e2e/codes.spec.ts`

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T016 [P] Add `test:e2e` and `test:e2e:ui` execution scripts to `frontend/package.json`
- [x] T017 [P] Create `.github/workflows/e2e-tests.yml` to trigger Playwright execution in CI environments

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- All user stories can start after Foundational Phase 2 and operate fully in parallel, since they hit distinct URLs and flows, isolating their own state.

### Parallel Opportunities

- Tests across US1, US2, US3, US4 can run locally leveraging Playwright's native concurrent test worker architecture.
- Frontend test configuration (T002) can run concurrently with Backdoor controller stubbing (T004).

# Tasks: Run All E2E Tests

**Input**: Design documents from `specs/126-run-e2e-tests/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`specs/126-run-e2e-tests/spec.md` completed)
- [x] Phase 2: Technical Planning (`specs/126-run-e2e-tests/plan.md` completed)
- [x] Phase 3: Detailed Task Breakdown (`specs/126-run-e2e-tests/tasks.md` created)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure verification

- [x] T001 Verify database and redis services are running in Docker
- [x] T002 Verify backend container is running and healthy on `http://localhost:5245`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core environment validation that MUST be complete before test execution

- [x] T003 Ensure port 3000 is free and not used by other legacy servers
- [x] T004 Verify Next.js dev server starts successfully on port 3000 via `cd frontend && npm run dev -- -p 3000`
- [x] T005 Check database connectivity and environment mode is `E2e` by calling `/api/health`

---

## Phase 3: User Story 1 - Sequential Playwright Tests (Priority: P1) 🎯 MVP

**Goal**: Run all 13 E2E test files sequentially in the `frontend` project

**Independent Test**: Execute the command and observe the sequential run of all 13 test files.

- [ ] T006 [US1] Run Playwright E2E tests in the `frontend` directory using `npm run test:e2e`

---

## Phase 4: User Story 2 - Automated Test Seeding (Priority: P1)

**Goal**: Verify that automated seeding endpoint `/api/e2e/seed` runs successfully during global-setup

**Independent Test**: Verify that the database is cleared and seeded before tests start.

- [ ] T007 [US2] Check global setup output for message `✅ Successfully seeded NaderGorge E2E testing database.`

---

## Phase 5: User Story 3 - Coverage and Failure Logging (Priority: P2)

**Goal**: Ensure HTML reports and screenshots on failure are generated

**Independent Test**: Verify that `playwright-report` is created.

- [ ] T008 [US3] Verify creation of HTML report in `frontend/playwright-report/`

---

## Phase 6: Polish

**Purpose**: Final cleanups

- [ ] T009 Ensure no leftover temporary test artifacts or screenshots are tracked in git

---

## Phase 7: Quality Gates (Clean Code Guard & Test Guard)

**Purpose**: Run the mandatory code and test quality gates

- [ ] T010 Run `clean-code-guard` against changed production files (Note: no production files were changed in this task)
- [ ] T011 Run `test-guard` against changed test files (Note: no test files were changed in this task)

---

## Phase 8: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment

- [ ] T012 Run `docker compose config -q`
- [ ] T013 Verify docker stack services are healthy
- [ ] T014 Write final walkthrough.md and compile the summary report

# Tasks: Comprehensive E2E Testing and Endpoint Verification

**Input**: Design documents from `specs/129-comprehensive-e2e-testing/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`specs/129-comprehensive-e2e-testing/spec.md` completed)
- [x] Phase 2: Arabic Clarification (`specs/129-comprehensive-e2e-testing/checklists/requirements.md` completed)
- [x] Phase 3: Technical Planning (`specs/129-comprehensive-e2e-testing/plan.md` completed)
- [x] Phase 4: Detailed Task Breakdown (`specs/129-comprehensive-e2e-testing/tasks.md` created)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure validation

- [x] T001 Verify database and redis services are running in Docker using `docker compose ps`
- [x] T002 Verify backend container is running and healthy on `http://localhost:5245` using `curl http://localhost:5245/api/health`
- [x] T003 Ensure Next.js dev server is running on port 3000 using `curl http://localhost:3000`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core environment validation before executing the test suites

- [x] T004 Check that database connectivity and E2e environment mode is correctly active by sending a request to the backend seeding endpoint `/api/e2e/seed`

---

## Phase 3: User Story 1 - E2E Test Cataloging and Gap Analysis (Priority: P1)

**Goal**: Catalog existing Playwright and Python integration tests and map them against endpoint inventory

**Independent Test**: Generate mapping file at `specs/129-comprehensive-e2e-testing/research.md` and check that all endpoints are listed

- [x] T005 [P] [US1] Open `frontend/tests/e2e/` directory and catalog all 14 Playwright E2E spec files
- [x] T006 [P] [US1] Open `tests/` directory and catalog all Python pytest files mapping them to target endpoints in `tests/endpoint_inventory.json`

---

## Phase 4: User Story 2 - Sequential Playwright E2E Test Execution (Priority: P1)

**Goal**: Run all 14 Playwright test files sequentially in the local E2e environment

**Independent Test**: Run `npx playwright test` inside `frontend/` and verify that all tests pass

- [ ] T007 [US2] Execute sequential Playwright E2E tests in the `frontend/` directory using the command `npm run test:e2e` and verify the expected result that all tests pass successfully
- [ ] T008 [US2] Verify the Playwright HTML report is successfully generated at `frontend/playwright-report/index.html` and contains no failures

---

## Phase 5: User Story 3 - Python Endpoint and Security Tests Execution (Priority: P1)

**Goal**: Execute the complete suite of Python security and endpoint tests

**Independent Test**: Run `pytest` inside the `tests/` directory and verify that all tests pass

- [ ] T009 [US3] Execute Python API and security segregation tests in `tests/` directory by running the command `python3 -m pytest` and check that the test runner passes with zero failures

---

## Phase 6: User Story 4 - Failure Analysis and Fixes (Priority: P1)

**Goal**: Identify, debug, and resolve any test failures in Playwright or Pytest

**Independent Test**: Confirm that re-running tests results in a clean pass with no failures

- [ ] T010 [US4] Review test execution logs and fix any failures found in either `frontend/tests/e2e/` or `tests/` to guarantee that the full suite passes successfully

---

## Phase 7: Polish & Quality Gates

**Purpose**: Run the mandatory quality gates and document final status

- [ ] T011 Run `clean-code-guard` against changed production files to verify clean code compliance (Note: no production files were changed in this feature)
- [ ] T012 Run `test-guard` against changed test files to verify test code compliance (Note: no test files were changed in this feature)
- [ ] T013 Compile the final results of the E2E and Python feature tests to prepare the final verification evidence

---

## Phase 8: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the feature is complete and working correctly in the real environment

- [ ] T014 Run `docker compose config -q` to verify Compose configuration
- [ ] T015 Verify docker stack services are healthy and running properly
- [ ] T016 Write the final walkthrough.md report detailing the execution results and endpoint coverage

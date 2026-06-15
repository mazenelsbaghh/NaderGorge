# Feature Specification: Run All E2E Tests

**Feature Branch**: `126-run-e2e-tests`  
**Created**: 2026-06-15  
**Status**: Draft  
**Input**: User description: "اعمل كل اختبارات e2e"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run Sequential Playwright Tests (Priority: P1)

As a developer or automation system, I want to execute all 13 E2E test files in the `frontend` directory sequentially, so that I can verify the complete system functionality without database state conflicts.

**Why this priority**: Sequentially running tests is critical because the Playwright test suite shares database state. Running them in parallel would cause state pollution and false failures.

**Independent Test**: Can be tested by running the Playwright CLI command in the `frontend` directory.

**Acceptance Scenarios**:

1. **Given** the backend and database are running in the `E2e` environment, **When** I execute the E2E tests command, **Then** all 13 test suites are executed one by one.
2. **Given** a clean execution, **When** all tests pass, **Then** the terminal reports 0 failures and generates a local HTML report.

---

### User Story 2 - Automated Test Seeding (Priority: P1)

As a developer, I want the test run to automatically invoke the backend `/api/e2e/seed` endpoint before executing any tests, so that every test run starts with a clean, well-defined database state.

**Why this priority**: Essential to avoid test pollution from prior runs or manual interactions.

**Independent Test**: Can be verified by running the global-setup hook and asserting that the database is cleared and seeded with default E2E fixtures.

**Acceptance Scenarios**:

1. **Given** the backend is listening on port 5245, **When** the global setup is executed, **Then** a POST request is sent to `http://localhost:5245/api/e2e/seed` with correct credentials.
2. **Given** the seed endpoint is successfully called, **When** the backend seeds the database, **Then** the global setup completes without errors and allows Playwright to proceed.

---

### User Story 3 - Coverage and Failure Logging (Priority: P2)

As a developer, I want to see detailed failure reasons, screenshots on failure, and HTML trace summaries, so that I can easily debug any regression.

**Why this priority**: Helps pinpoint failing assertions or UI mismatches quickly.

**Independent Test**: Can be tested by intentionally breaking a test and checking if a screenshot/trace is generated in the `playwright-report` folder.

**Acceptance Scenarios**:

1. **Given** a failing test assertion, **When** the test runs, **Then** Playwright captures a screenshot and adds it to the report directory.

---

### Edge Cases

- **Backend Seeding Endpoint Offline**: If the backend container or dotnet server on port 5245 is offline or not running with `ASPNETCORE_ENVIRONMENT=E2e`, the global-setup hook must gracefully fail with a descriptive message rather than hanging indefinitely.
- **Port Conflict**: If port 3000 is occupied by another process, Next.js or Playwright might hit the wrong server. Next.js port needs to be bound cleanly.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Run `npm run test:e2e` in the `frontend` directory. Verify that all 13 test files are executed.
- **Manual QA Negative Check**: Try running tests while the backend is stopped, and verify it aborts with a clear connection error.
- **Docker Acceptance**: Verify that the backend Docker container has `ASPNETCORE_ENVIRONMENT=E2e` and is healthy.
- **External Dependencies**: Local Postgres database container (port 5435) and Redis container (port 6382) must be healthy.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Playwright MUST run tests sequentially with `workers: 1` as configured.
- **FR-002**: System MUST run the global-setup script to seed the database prior to test execution.
- **FR-003**: The E2E tests MUST cover the following modules: Auth, Admin Users, Admin Content, Assistant Dashboard, Codes Wallet, Codes, Comprehensive Features, Package Code Profiles, Parent Report, SignalR Events, Student Academic, Student Journey, and Teacher Isolation.
- **FR-004**: System MUST capture screenshots on test failure.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 13 test files execute successfully.
- **SC-002**: 100% of defined tests in the suite pass or fail with clear traceback details in case of regression.
- **SC-003**: The test runner initializes and completes the setup phase (seeding) in less than 15 seconds.

## Assumptions

- The backend container is running on port 5245 with `ASPNETCORE_ENVIRONMENT=E2e`.
- The frontend Next.js dev server is running on port 3000 or the Playwright configuration targets the correct running frontend instance.

# Feature Specification: Comprehensive E2E Testing and Endpoint Verification

**Feature Branch**: `129-comprehensive-e2e-testing`  
**Created**: 2026-06-15  
**Status**: Draft  
**Input**: User description: "راجع كل اختبارات e2e و جربهم لكل حاجه في المنصة لكل endpoint معموله"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - E2E Test Cataloging and Gap Analysis (Priority: P1)

As a QA lead and developer, I want to catalog all existing E2E and Python integration tests in the repository and map them against the 255 endpoints documented in the platform's inventory, so that we have a clear picture of test coverage.

**Why this priority**: Crucial first step to understand what is currently tested and where the blind spots are in our test coverage.

**Independent Test**: Can be validated by generating a mapping or checklist linking each E2E test file / Python test file to the target endpoints they cover.

**Acceptance Scenarios**:
1. **Given** the repository contains 14 Playwright test files and multiple Python integration test files, **When** I analyze the test files, **Then** I can compile a complete list of test files and the primary endpoints they cover.

---

### User Story 2 - Sequential Playwright E2E Test Execution (Priority: P1)

As a developer, I want to execute all 14 Playwright test files sequentially in the local `E2e` environment, so that I can verify student, admin, teacher, and assistant flows without database collision or state contamination.

**Why this priority**: Ensures that the core web workflows (authentication, homework submission, exam timer, code wallets, teacher isolation) are verified under standard browser-based simulations.

**Independent Test**: Run `npm run test:e2e` inside the `frontend` directory against local Next.js and .NET backend servers.

**Acceptance Scenarios**:
1. **Given** Next.js is running on port 3000 (or 8738 bound to 3000) and the backend is running on port 5245 with environment `E2e`, **When** I run the Playwright E2E test command, **Then** all 14 test suites execute sequentially.
2. **Given** the Playwright execution finishes, **When** all tests pass, **Then** the terminal reports 0 failures and generates a local HTML report.

---

### User Story 3 - Python Endpoint and Security Tests Execution (Priority: P1)

As a security engineer, I want to execute the complete suite of Python security and endpoint tests (including `test_all_endpoints.py`), so that we can verify that access control boundaries (student vs teacher vs admin) are correctly enforced across all API endpoints.

**Why this priority**: Enforces correct role-based access control and prevents unauthorized access to sensitive administrative, financial, and operations endpoints.

**Independent Test**: Run `pytest` inside the `tests` directory.

**Acceptance Scenarios**:
1. **Given** the backend is running on port 5245 in `E2e` mode, **When** I run the python pytest command, **Then** all python tests (including `test_all_endpoints.py` which calls all 255 inventoried endpoints) execute.
2. **Given** the python tests finish, **When** all tests pass, **Then** pytest reports success with 0 failures.

---

### User Story 4 - Failure Analysis and Fixes (Priority: P1)

As a developer, I want to identify any failing tests in either suite, debug the failure causes (e.g., environment configuration issues, state leaks, expired tokens, or actual bugs), and implement corresponding fixes so that both test suites pass 100%.

**Why this priority**: Essential to establish a clean, regression-free baseline for the platform.

**Independent Test**: Verify that re-running tests after fixes results in 100% pass status.

**Acceptance Scenarios**:
1. **Given** a test failure occurs during execution, **When** I inspect the failure logs and traces, **Then** I can pinpoint and fix the root cause in configuration, code, or test setup.

---

### Edge Cases

- **Backend Seeding Endpoint Offline/Timeout**: If the database reset/seeding endpoint `http://localhost:5245/api/e2e/seed` fails, the test suites must fail with clear descriptive errors.
- **Port/Environment Mismatch**: If Next.js runs on a different port than Playwright expects (e.g., 8738 instead of 3000) or backend is not in `E2e` mode, the tests should abort immediately to avoid testing the wrong server.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Verify the status of local docker services (postgres, redis, worker) before running the tests.
- **Manual QA Negative Check**: Verify that when the backend is stopped, E2E tests fail fast and print the connection failure error.
- **Docker Acceptance**: Ensure the Docker environment for database and redis is healthy and listening on ports 5435 and 6382 respectively.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Playwright E2E tests MUST run sequentially with `workers: 1` using the configured global-setup seeding hook.
- **FR-002**: Python tests MUST run using `pytest` and target the running E2e backend.
- **FR-003**: The E2E tests MUST cover the 14 defined Playwright test files in `frontend/tests/e2e/`.
- **FR-004**: The Python test suite MUST cover all files in the root `tests/` directory, including endpoint segregation check (`test_all_endpoints.py`).
- **FR-005**: All failures discovered during E2E/API runs MUST be documented, debugged, and resolved.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 14 Playwright E2E test files execute and pass in under 300 seconds.
- **SC-002**: All Python API tests under `tests/` execute and pass in under 60 seconds.
- **SC-003**: A comprehensive test summary and walkthrough document is generated verifying 255 items from the endpoint inventory.

## Assumptions

- PostgreSQL is running in Docker and mapped to host port 5435.
- Redis is running in Docker and mapped to host port 6382.
- The C# backend can be started locally in `E2e` mode on port 5245.
- Next.js can be run on port 3000 locally using `npm run dev -- -p 3000` or `npx next dev -p 3000`.

# Tasks: Comprehensive Endpoint Testing

**Input**: Design documents from `specs/099-comprehensive-endpoint-testing/`
**Prerequisites**: plan.md (required), spec.md (required)

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Setup & Environment Prep

**Purpose**: Preparing environment, ensuring dependencies are available, and validating Docker stack.

- [x] T001 Verify that Python 3 and required packages (like `requests`) are installed.
- [x] T002 Verify Docker containers are up and healthy and exposed to host ports (`massar_backend` at `http://localhost:5245`).

---

## Phase 2: Test Harness Development

**Purpose**: Writing the `test_all_endpoints.py` script.

- [x] T003 Parse `tests/endpoint_inventory.json` in `scratch/test_all_endpoints.py`.
- [x] T004 Implement multi-role authentication handler (logging in as Admin `20000000000`, Student `20000000001`, Assistant `20000000003`, and Teacher `20000000004` with password `password`).
- [x] T005 Implement URL parameter substitution in the python script (e.g. replacing `{id:guid}`, `{lessonId:guid}`, `{commentId:guid}`, and generic `{id}` with a test GUID `00000000-0000-0000-0000-000000000000`).
- [x] T006 Implement request-sending logic for GET, POST, PUT, DELETE. Handle JSON payloads (empty objects `{}` or partial bodies).
- [x] T007 Implement report-generation logic that tracks endpoint, method, role, status code, response snippet, and classification (Success, Expected Error like 400/401/403/404, or Unexpected Error like 500). Write the output to `specs/099-comprehensive-endpoint-testing/audit_report.md`.

---

## Phase 3: Execution & Remediation (US1 & US2)

**Goal**: Run the sweep, identify any 500 errors, fix any critical backend issues found, and ensure role boundary checks.

**Independent Test**: Running the script successfully to completion.

- [x] T008 Run `python3 scratch/test_all_endpoints.py`.
- [x] T009 Review test output, identify any 500 errors or unhandled backend exceptions.
- [x] T010 Resolve any unhandled backend exceptions (e.g., in controller actions or MediatR handlers) by adding appropriate model/validation checks or try-catch blocks to prevent 500 Internal Server Errors.
- [x] T011 Verify role-based authorization blocks (e.g., student calling admin endpoints returns 403 or 401).

---

## Phase 4: Polish & Quality Gates

**Purpose**: Code cleanup and mandatory Spec Kit checks.

- [x] T012 Run `clean-code-guard` against any modified backend files.
- [x] T013 Run `test-guard` against any modified test files.
- [x] T014 Remove temporary debugging tools or prints, format code.

---

## Phase 5: End-of-Phase Verification & Manual QA Report

**Purpose**: Confirm the phase is complete and verified in the real environment.

- [x] T015 Run `docker compose ps` and check all logs to ensure backend is running without errors.
- [x] T016 Generate final `specs/099-comprehensive-endpoint-testing/audit_report.md`.
- [x] T017 Complete achievements.md file update and mark all steps complete.

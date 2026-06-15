# Implementation Plan: Run All E2E Tests

**Branch**: `126-run-e2e-tests` | **Date**: 2026-06-15 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/126-run-e2e-tests/spec.md)
**Input**: Feature specification from `specs/126-run-e2e-tests/spec.md`

## Summary

Execute the full suite of 13 E2E test files sequentially in the `frontend` project using Playwright. This ensures that the platform's core user journeys remain stable and regression-free, and that the database seeding endpoint executes successfully.

## Technical Context

- **Language/Version**: TypeScript 5.x, Node.js v20+
- **Primary Dependencies**: `@playwright/test`
- **Storage**: PostgreSQL (used by C# backend)
- **Testing**: Playwright test runner (`npx playwright test`)
- **Target Platform**: Local Next.js dev server (`http://localhost:3000`), C# backend (`http://localhost:5245`)
- **Project Type**: Web application E2E testing
- **Performance Goals**: Sequential E2E test execution under 5 minutes
- **Constraints**: Must run with `workers: 1` to prevent database state collision

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer Impact**:
  - **Backend**: None (no code changes, but backend API must be running on port 5245 in E2e mode).
  - **Frontend**: Playwright configuration is target. Next.js must be running on port 3000.
  - **Worker**: None (worker must be running in docker/host if notifications/AI jobs are triggered).
  - **Database**: The E2E tests will seed and clear the database via the `/api/e2e/seed` endpoint.
  - **Docker**: The Docker containers for postgres, redis, and worker must be running and healthy.
- **Automated Tests**:
  - Run the full suite of E2E tests via `npm run test:e2e` inside `frontend`.
- **Manual QA**:
  - Verify that the Playwright HTML report is successfully generated at `frontend/playwright-report/index.html`.
- **Docker Gate**:
  - Run `docker compose config -q` and verify backend and DB containers are healthy on port 5245 and 5435.
- **Next Phase Rule**:
  - The next phase cannot start if E2E tests fail. Any regressions found must be reported or fixed before finalizing.

## Project Structure

### Documentation (this feature)

```text
specs/126-run-e2e-tests/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── quickstart.md        # Phase 1 output
```

### Source Code (repository root)

We are running the existing Playwright E2E tests under `frontend/tests/e2e/`.

## Phase Closure & Verification Plan

- **Automated Tests Required**:
  - Command: `npm run test:e2e` in the `frontend` directory.
  - Critical paths covered: Auth, Admin Users, Admin Content, Assistant Dashboard, Codes Wallet, Codes, Comprehensive Features, Package Code Profiles, Parent Report, SignalR Events, Student Academic, Student Journey, Teacher Isolation.
- **Docker Gate Required**:
  - Command: `docker compose ps` to check that all containers are healthy.
- **Manual QA Required**:
  - Check the Playwright report for failing tests.
- **End-of-Phase Report**:
  - Summary of E2E test execution, total passed/failed tests, details of any failed assertions or screenshots, and a clear verification statement.

# Implementation Plan: Comprehensive E2E Testing and Endpoint Verification

**Branch**: `129-comprehensive-e2e-testing` | **Date**: 2026-06-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/129-comprehensive-e2e-testing/spec.md`

## Summary

This plan outlines the steps to catalog, review, and execute all E2E test suites (14 Playwright test suites in `frontend/tests/e2e/` and all Python test suites in `tests/`) to verify 100% of the platform's API endpoints and security boundaries.

## Technical Context

**Language/Version**: TypeScript 5.x, Python 3.10+, C# (dotnet 9)  
**Primary Dependencies**: `@playwright/test`, `pytest`, `requests`  
**Storage**: PostgreSQL (port 5435), Redis (port 6382)  
**Testing**: Playwright CLI, Pytest CLI  
**Target Platform**: Local Next.js dev server (`http://localhost:3000`), C# backend (`http://localhost:5245`)  
**Project Type**: Test suite execution and verification  
**Performance Goals**: Sequential E2E test execution under 5 minutes  
**Constraints**: Playwright must run with `workers: 1` due to shared database seeding state. Next.js must be running on port 3000. Backend must run with `ASPNETCORE_ENVIRONMENT=E2e` on port 5245.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer Impact**:
  - **Backend**: None (API endpoints are verified, no backend changes needed unless bug fixes arise).
  - **Frontend**: Playwright configurations are targeted. Next.js runs on port 3000.
  - **Worker**: BullMQ worker runs for background processes.
  - **Database**: Database is seeded/cleared using `/api/e2e/seed`.
  - **Docker**: DB and Redis containers must be healthy.
- **Automated Tests Required**:
  - Run Playwright E2E tests: `cd frontend && npm run test:e2e`
  - Run Python endpoint tests: `cd tests && pytest`
- **Manual QA Required**:
  - Verify that both Playwright test suite and Python pytest run cleanly and pass 100%.
- **Docker Gate**:
  - `docker compose config -q` and `docker compose ps` to check container health.

## Project Structure

### Documentation (this feature)

```text
specs/129-comprehensive-e2e-testing/
├── plan.md              # This file
├── research.md          # E2E test catalog and mapping
├── data-model.md        # Data requirements (none, stateless seeding)
├── quickstart.md        # Command guide for test runners
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code (repository root)

```text
backend/                 # C# backend (port 5245)
frontend/                # Next.js frontend (port 3000 for E2e)
├── tests/
│   ├── e2e/             # 14 Playwright E2E tests
│   └── fixtures/        # Global setup & seeding
tests/                   # Python API/E2E tests (pytest)
```

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `cd frontend && npm run test:e2e`
- `cd tests && pytest`

**Docker Gate Required**:
- `docker compose ps` shows database, redis, worker containers healthy.

**Manual QA Required**:
- Review the generated Playwright HTML report and Pytest outputs.

**End-of-Phase Report Format**:
- Walkthrough detailing each Playwright suite result, python test outputs, mapped endpoints, and confirmation that all endpoints are verified.

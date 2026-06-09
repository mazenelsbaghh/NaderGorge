# Implementation Plan: Full Regression, Launch Drill, and Rollback Plan

**Branch**: `098-full-regression-launch-drill` | **Date**: 2026-06-09 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/098-full-regression-launch-drill/spec.md)
**Input**: Feature specification from `/specs/098-full-regression-launch-drill/spec.md`

## Summary

This plan outlines the execution steps for performing the final verification of the Massar Platform expansion. It encompasses executing all automated tests, performing a Docker clean-state launch drill, performing database migration and seeding checks, preparing a database rollback strategy, executing manual QA checks for all roles, and compiling the final readiness report.

## Technical Context

**Language/Version**: C# (.NET 9), TypeScript 5.x / Next.js 16.2.1 / React 19, Node.js v20, Python 3.10+  
**Primary Dependencies**: EF Core 9, MediatR, playright-chromium, pytest, docker compose, nginx  
**Storage**: PostgreSQL (pgdata), Redis (redisdata)  
**Testing**: dotnet test, eslint, npm run build, pytest, Playwright E2E  
**Target Platform**: Docker Alpine / Debian Linux servers  
**Project Type**: Full Stack Platform  
**Performance Goals**: N/A (Verification Phase)  
**Constraints**: All tests must pass, warning-free builds, 0% test failures, all docker services healthy.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: All layers (backend, frontend, worker, database, Docker, Nginx).
- **Automated tests**: Running all backend MediatR tests, Python E2E integration tests, frontend typescript/lint compiles.
- **Manual QA**: Verifying Admin, Assistant, Teacher, and Student user flows.
- **Docker Gate**: Rebuilding the entire stack with no cache, confirming all 8 services healthy, and validating Nginx subdomain headers.

## Project Structure

### Documentation (this feature)

```text
specs/098-full-regression-launch-drill/
├── plan.md              # This file
├── spec.md              # Feature specification
├── quickstart.md        # Commands list and quickstart
└── tasks.md             # Detailed task checklist
```

### Source Code (repository root)
- Verification script: [verify-surface-separation.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/verify-surface-separation.mjs)
- C# Backend: `backend/`
- Next.js Frontend: `frontend/`
- Node.js Worker: `worker/`
- Python Integration Tests: `tests/`
- Docker Compose: `docker-compose.yml`

## Phase Closure & Verification Plan

**Automated Tests Required**:
* C# Backend build and test:
  ```bash
  dotnet build backend/NaderGorge.sln
  dotnet test backend/NaderGorge.sln --no-build
  ```
* Frontend lint and build:
  ```bash
  cd frontend && npm run lint && npm run build
  ```
* Worker build:
  ```bash
  cd worker && npm run build
  ```
* Python tests:
  ```bash
  python3 -m pytest
  ```
* Endpoint inventory and subdomain verification:
  ```bash
  node scripts/generate-endpoint-inventory.mjs --check
  node scripts/verify-surface-separation.mjs
  ```

**Docker Gate Required**:
* Full cold restart:
  ```bash
  make down
  docker compose build --no-cache
  make up
  make migrate
  ```
* Verify service health and Nginx proxy headers.

**Manual QA Required**:
* Verify all 4 primary user roles (Admin, Assistant, Teacher, Student) using localhost surface ports or subdomains.

**End-of-Phase Report Format**:
* Summary of results, list of commands run, external dependencies table, rollback instructions, and final "Ready / Not Ready" sign-off.

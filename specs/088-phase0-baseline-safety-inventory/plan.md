# Implementation Plan: Phase 0 - Baseline, Specs, and Safety Inventory

**Branch**: `088-phase0-baseline-safety-inventory` | **Date**: 2026-06-07 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/088-phase0-baseline-safety-inventory/spec.md)
**Input**: Feature specification from `/specs/088-phase0-baseline-safety-inventory/spec.md`

## Summary

This phase focuses on running a comprehensive baseline inventory and safety check of the entire codebase. The goal is to compile the C# backend, Next.js frontend, and Node.js worker, verify all tests pass, validate the local Docker Compose infrastructure, run migrations against a local Postgres database, check frontend surface boundaries, and ensure secrets are securely configured. No new features are introduced in this phase; it establishes a clean, verified state before any platform expansion.

## Technical Context

- **Language/Version**: C# 13 (.NET 9), TypeScript 5.x (Next.js 16.2.1 / React 19, Node.js v20+)
- **Primary Dependencies**: Entity Framework Core 9, MediatR, BullMQ, `@google/genai`, Tailwind CSS
- **Storage**: PostgreSQL 16, Redis 7
- **Testing**: `dotnet test`, Playwright/E2E, Python `pytest` (endpoint validation)
- **Target Platform**: Docker local environment
- **Performance Goals**: Health checks responding under 10 seconds; services boot under 2 minutes
- **Constraints**: No new DB entities, schemas, or UI packages in this baseline phase

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The plan strictly adheres to the Project Constitution:
- **Layer Impact**:
  - **Backend**: We will build `NaderGorge.sln` and run tests. No code/schema changes.
  - **Frontend**: We will run Next.js lint and build. No UI/route changes.
  - **Worker**: We will compile the Node.js TypeScript code. No processor changes.
  - **Database**: We will apply EF migrations via `make migrate`.
  - **Docker**: The full stack will be verified via `make up` and URL health checks.
- **Automated Tests**:
  - `dotnet test backend/NaderGorge.sln --no-build`
  - `python3 -m pytest tests/test_endpoint_inventory.py -q`
- **Manual QA**:
  - Verify admin login (`http://localhost:8738`), student login (`http://localhost:8740`), and Bull Board dashboard (`http://localhost:3001/ui`).
- **Docker Gate**:
  - `docker compose config -q`
  - `make up`
  - `make migrate`
  - `make ps`
  - Check health endpoints of all containers.
- **No Next Phase until Verified**:
  - We will not start Phase 1 expansion until all Phase 0 baseline checks pass with exit code `0` and a warning-free build is achieved.

## Project Structure

### Documentation (this feature)

```text
specs/088-phase0-baseline-safety-inventory/
├── plan.md              # This file
├── spec.md              # Feature Specification
└── checklists/
    └── requirements.md  # Spec validation checklist
```

### Source Code (repository root)

We are working within the existing multi-project structure:

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   ├── NaderGorge.Application/
│   ├── NaderGorge.Domain/
│   └── NaderGorge.Infrastructure/
└── tests/
    └── NaderGorge.Application.Tests/

frontend/
├── src/
│   ├── app/
│   ├── components/
│   ├── services/
│   └── stores/
└── tests/

worker/
├── src/
│   ├── index.ts
│   ├── jobs/
│   └── services/
└── package.json
```

**Structure Decision**: The project uses the multi-surface web application layout. This structure will be preserved and checked for boundary separation statically.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Backend test suite: `dotnet test backend/NaderGorge.sln`
- Frontend lint: `npm run lint` inside `frontend/`
- Worker build: `npm run build` inside `worker/`
- Endpoint inventory check: `node scripts/generate-endpoint-inventory.mjs --check`
- Surface separation check: `node scripts/verify-surface-separation.mjs --static-only`
- Smoke test check: `python3 -m pytest tests/`

**Docker Gate Required**:
- Run `docker compose config -q` to validate composition files.
- Start infrastructure with `make up` and apply database updates with `make migrate`.
- Run `make ps` to confirm all 5 principal containers are healthy and running.
- Query health checks at `http://localhost:5245/api/health` and `http://localhost:3001/health` to confirm they return status `200`.

**Manual QA Required**:
- Log in as Super Admin at `http://localhost:8738` with `01000000000` / `Admin@123`.
- Verify the active users grid displays correctly.
- Verify Bull Board lists the 4 core job queues at `http://localhost:3001/ui`.

**End-of-Phase Report Format**:
- Summary of baseline features checked.
- Execution logs of automated test runs.
- Docker gate URL responses and service logs.
- Risks, warning occurrences, and go/no-go recommendation for Phase 1.

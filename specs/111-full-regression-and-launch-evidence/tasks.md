# Tasks: Full Regression and Launch Evidence

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g. US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)
- [ ] T001 Verify project directories and environment config file `.env` are present at repository root

## Phase 2: Foundational (Blocking Prerequisites)
- [ ] T002 Verify Docker service container definitions in `docker-compose.yml` and static configuration parameters

## Phase 3: User Story 1 - Comprehensive Build & Test Regression (Priority: P0)
**Goal**: Compile all services warning-free and execute all backend, worker, Python E2E integration, and Playwright tests successfully.
**Independent Test**: Build and run all test commands and expect all tests to pass and zero build/lint errors.

### Implementation for User Story 1
- [ ] T003 [P] [US1] Run Next.js frontend lint check: `npm run lint` inside `frontend/`
- [ ] T004 [P] [US1] Run Next.js frontend production build: `npm run build` inside `frontend/`
- [ ] T005 [P] [US1] Run Node.js worker production build: `npm run build` inside `worker/`
- [ ] T006 [P] [US1] Run C# backend compilation check: `dotnet build backend/NaderGorge.sln`
- [ ] T007 [P] [US1] Run C# backend unit and integration tests: `dotnet test backend/NaderGorge.sln --no-build`
- [ ] T008 [P] [US1] Run Python API / E2E integration tests: `.venv/bin/python -m pytest tests/`
- [ ] T009 [P] [US1] Run Playwright frontend UI tests: `npx playwright test` inside `frontend/`
- [ ] T010 [P] [US1] Run static endpoint contract alignment validation: `node scripts/generate-endpoint-inventory.mjs --check`
- [ ] T011 [P] [US1] Run Nginx configuration validation: `node scripts/verify-surface-separation.mjs --static-only`
- [ ] T012 [P] [US1] Run Docker compose configuration syntax check: `docker compose config -q`

---

## Phase 4: User Story 2 - Docker Cold-Start & Database Migrations (Priority: P0)
**Goal**: Rebuild the platform services cleanly from scratch, ensure all service containers start healthy, and run migrations without errors.
**Independent Test**: Complete down/up sequence and inspect docker containers logs/status.

### Implementation for User Story 2
- [ ] T013 [US2] Stop and clean existing containers: `make down`
- [ ] T014 [US2] Rebuild all containers from scratch with no-cache: `docker compose build --no-cache`
- [ ] T015 [US2] Start all services in the background: `make up`
- [ ] T016 [US2] Apply database migrations on the clean database: `make migrate`
- [ ] T017 [US2] Check container status and health: `make ps` and verify all containers are healthy
- [ ] T018 [US2] Verify database connection and tables by checking logs or querying backend status endpoint

---

## Phase 5: User Story 3 - Multi-Domain Subdomain Route Separation (Priority: P1)
**Goal**: Validate that domains and subdomain routes redirect and load correct services/frontends properly via Nginx.
**Independent Test**: Execute full subdomain isolation test script.

### Implementation for User Story 3
- [ ] T019 [US3] Execute full surface separation verification check: `node scripts/verify-surface-separation.mjs`
- [ ] T020 [US3] Manually verify headers and DNS redirection patterns for subdomains: `admin.*`, `super.*`, `teacher.*`, `staff.*`, `app.*` or `student.*`, `api.*`, `ws.*` using `curl` or local hosts overrides

---

## Phase 6: Polish & Cross-Cutting Concerns
- [ ] T021 Document Backup, Restore & Rollback Strategy, and write backup script or document it under `specs/111-full-regression-and-launch-evidence/backup_restore.md`
- [ ] T022 Document roll-back procedure and evidence under `specs/111-full-regression-and-launch-evidence/rollback.md`

---

## Phase 7: End-of-Phase Verification, Quality Gates & QA Report
- [ ] T023 Run `clean-code-guard` against changed files (even if it's just docs/configs, or confirm no changed production-code files)
- [ ] T024 Run `test-guard` against changed test files (confirm no changed test files exist)
- [ ] T025 Compile and output the Markdown Report at `specs/111-full-regression-and-launch-evidence/readiness_report.md`
- [ ] T026 Update root `walkthrough.md` with the regression summary and update `achievements.md` to mark all phases completed.

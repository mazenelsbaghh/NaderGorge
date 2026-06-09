# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Phase 0 - Baseline, Specs, and Safety Inventory

**Input**: Design documents from `/specs/088-phase0-baseline-safety-inventory/`
**Prerequisites**: plan.md (required), spec.md (required)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files/commands, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)

## Path Conventions

- **Web app**: `backend/src/`, `frontend/src/`, `worker/src/`, `tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure checks

- [x] T001 Verify specs directory and configuration file `.specify/feature.json` points to the correct directory path.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core configuration check that MUST be complete before ANY checks can run

- [x] T002 Verify that the `.env` template exists and compare keys with the active `.env` file to ensure all essential parameters (JWT_SECRET, API_CALLBACK_SECRET, WORKER_ADMIN_TOKEN, GEMINI_API_KEY) are defined.

**Checkpoint**: Foundation ready - verification tasks can now begin.

---

## Phase 3: User Story 1 - System Integrity & Compile Check (Priority: P1) 🎯 MVP

**Goal**: Verify compilation and tests pass for backend, frontend, and worker projects.

**Independent Test**: Running compilation commands inside respective directories.

### Implementation for User Story 1

- [x] T003 [P] [US1] Run command `dotnet build backend/NaderGorge.sln` and check for zero compilation errors.
- [x] T004 [P] [US1] Run command `dotnet test backend/NaderGorge.sln --no-build` and check for zero test failures.
- [x] T005 [P] [US1] Run command `npm run lint` inside `frontend/` to check for zero lint warnings.
- [x] T006 [P] [US1] Run command `npm run build` inside `frontend/` to verify production compilation.
- [x] T007 [P] [US1] Run command `npm run build` inside `worker/` to verify worker compilation.

**Checkpoint**: At this point, User Story 1 is fully completed and verified.

---

## Phase 4: User Story 2 - Docker Environment & Health Gate (Priority: P1)

**Goal**: Ensure the Docker stack boots correctly, database migrations are applied, and service health checks are responsive.

**Independent Test**: docker compose config, make up, make migrate, and health URLs.

### Implementation for User Story 2

- [x] T008 [P] [US2] Validate composition files by running `docker compose config -q`.
- [ ] T009 [US2] Spin up local containers by running `make up` (or `docker compose up -d`). *(BLOCKED: Docker Daemon not running)*
- [ ] T010 [US2] Run EF Core migrations against the PostgreSQL database by running `make migrate`. *(BLOCKED: Docker Daemon not running)*
- [ ] T011 [P] [US2] Check service health checks using `curl -f http://localhost:5245/api/health` (backend) and `curl -f http://localhost:3001/health` (worker). *(BLOCKED: Docker Daemon not running)*
- [ ] T012 [P] [US2] Verify running containers with `make ps` to confirm they are healthy. *(BLOCKED: Docker Daemon not running)*

**Checkpoint**: At this point, User Story 2 is partially blocked due to external docker daemon runtime availability.

---

## Phase 5: User Story 3 - Surface Separation & Endpoint Integrity (Priority: P2)

**Goal**: Run scripts to verify that static boundary rules and endpoint lists are in sync.

**Independent Test**: Running boundary verification scripts.

### Implementation for User Story 3

- [x] T013 [P] [US3] Verify surface separation boundaries by running `node scripts/verify-surface-separation.mjs --static-only` in root folder.
- [x] T014 [P] [US3] Verify endpoint schema validation check by running `node scripts/generate-endpoint-inventory.mjs --check` in root folder.
- [x] T015 [P] [US3] Verify Python smoke/API test suite passes by running `python3 -m pytest tests/test_endpoint_inventory.py -q`.

**Checkpoint**: At this point, User Story 3 is fully completed and verified.

---

## Phase 6: User Story 4 - Security & Secrets Baseline Audit (Priority: P2)

**Goal**: Verify critical environment secrets are configured.

**Independent Test**: Review `.env` configuration file keys.

### Implementation for User Story 4

- [x] T016 [US4] Check `JWT_SECRET`, `API_CALLBACK_SECRET`, `WORKER_ADMIN_TOKEN`, `GEMINI_API_KEY` in the local `.env` file to ensure they are set to non-default, secure values.

**Checkpoint**: At this point, User Story 4 is fully completed and verified.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Save/log reports, tidy up artifacts

- [x] T017 Save output of all baseline test commands in a scratch file `baseline_run.log` under the artifacts directory.

---

## Phase 8: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment before starting the next phase.

- [x] T018 Run `dotnet test`, `npm run lint`, `node scripts/verify-surface-separation.mjs --static-only` to ensure everything is verified.
- [ ] T019 Check Bull Board queue UI at `http://localhost:3001/ui` and Admin Panel at `http://localhost:8738` manually. *(BLOCKED: Docker Daemon not running)*
- [x] T020 Write and save the final report.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories.
- **User Stories (Phase 3+)**: All depend on Foundational phase completion.
- **Polish (Final Phase)**: Depends on all desired user stories being complete.
- **End-of-Phase Verification**: Depends on all implementation and polish tasks.

### Parallel Opportunities

- Tasks marked [P] can run in parallel within their respective phases.

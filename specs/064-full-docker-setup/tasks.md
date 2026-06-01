# Tasks: Full Docker Setup (Frontend, Backend, Database & Make)

**Input**: Design documents from `/specs/064-full-docker-setup/`
**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅ | quickstart.md ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the scaffolding that all Dockerfiles and Compose depend on — `.env.example`, root-level compose file, and `next.config.ts` standalone flag.

- [x] T001 Create root-level `.env.example` documenting all required env vars at `.env.example`
- [x] T002 Add `output: 'standalone'` to `frontend/next.config.ts` to enable minimal Next.js image builds
- [x] T003 Archive existing infra-only compose by renaming `docker/docker-compose.yml` → `docker/docker-compose.infra-only.yml`

---

## Phase 2: Foundational (Dockerfiles — Blocking Prerequisites)

**Purpose**: All three Dockerfiles must exist and build successfully before `docker-compose.yml` and Makefile targets can be validated.

**⚠️ CRITICAL**: No user story work can begin until all three images build cleanly.

- [x] T004 [P] Write multi-stage `frontend/Dockerfile` — Stage 1: `node:20-alpine` install + `next build`; Stage 2: `node:20-alpine` copy `.next/standalone`, `.next/static`, `public/`, run `node server.js -p 8738`
- [x] T005 [P] Write multi-stage `backend/Dockerfile` — Stage 1: `mcr.microsoft.com/dotnet/sdk:8.0` restore + `dotnet publish -c Release -o /app/publish`; Stage 2: `mcr.microsoft.com/dotnet/aspnet:8.0` copy `/app/publish`, expose 5245, `ENTRYPOINT ["dotnet", "NaderGorge.API.dll"]`
- [x] T006 [P] Write multi-stage `worker/Dockerfile` — Stage 1: `node:20-slim` + `apt-get install -y ffmpeg` + `npm ci` + `npm run build`; Stage 2: `node:20-slim` + `apt-get install -y ffmpeg` copy `dist/` + production `node_modules`, run `node dist/index.js`
- [ ] T007 Smoke-test all three images build locally: `docker build -f frontend/Dockerfile frontend/` then backend, then worker — fix any build errors before continuing

**Checkpoint**: `docker build` succeeds for all three services. Foundation ready.

---

## Phase 3: User Story 1 — One-Command Platform Start (Priority: P1) 🎯 MVP

**Goal**: `make up` starts all 5 services in the correct boot order with zero extra steps after cloning + copying `.env.example`.

**Independent Test**: Fresh machine with Docker + Make only → `make up` → `make ps` shows all 5 containers healthy → frontend loads at http://localhost:8738.

### Implementation for User Story 1

- [x] T008 [US1] Write root-level `docker-compose.yml` with 5 services + migrator profile; define `nadergorge_net` bridge network and named volumes `pgdata`, `redisdata`
- [x] T009 [US1] Add `healthcheck` blocks to all 5 services in `docker-compose.yml`
- [x] T010 [US1] Add `depends_on` with `condition: service_healthy` in `docker-compose.yml`
- [x] T011 [US1] Wire all env vars in `docker-compose.yml` `environment:` blocks per data-model.md
- [x] T012 [US1] Add `make up` target to `Makefile`
- [x] T013 [US1] Add `make ps` target to `Makefile`
- [x] T014 [US1] Add `make help` target to `Makefile` — greps `##` comments, formats as usage table

**Checkpoint**: `make up && make ps` → all 5 containers running and healthy. Frontend reachable. ✅

---

## Phase 4: User Story 2 — Stop, Rebuild, Restart with Make (Priority: P1)

**Goal**: Developers can stop, rebuild, restart, and tail logs for all or individual services via short `make` commands.

**Independent Test**: Run `make down` → containers gone; `make build` → images rebuilt; `make up` → everything back healthy; `make logs-backend` → backend logs stream.

### Implementation for User Story 2

- [x] T015 [P] [US2] Add `make down` target
- [x] T016 [P] [US2] Add `make build` target
- [x] T017 [P] [US2] Add `make build-frontend` target
- [x] T018 [P] [US2] Add `make build-backend` target
- [x] T019 [P] [US2] Add `make build-worker` target
- [x] T020 [P] [US2] Add `make restart` target
- [x] T021 [P] [US2] Add `make logs` target
- [x] T022 [P] [US2] Add `make logs-frontend` target
- [x] T023 [P] [US2] Add `make logs-backend` target
- [x] T024 [P] [US2] Add `make logs-worker` target
- [x] T025 [P] [US2] Add `make logs-db` target
- [x] T026 [P] [US2] Add `make logs-redis` target
- [x] T027 [P] [US2] Add `make shell-frontend` target
- [x] T028 [P] [US2] Add `make shell-backend` target
- [x] T029 [P] [US2] Add `make shell-worker` target
- [x] T030 [P] [US2] Add `make shell-db` target

**Checkpoint**: All `make logs-*`, `make shell-*`, `make build-*`, `make restart` targets work. ✅

---

## Phase 5: User Story 3 — Database Migrations Inside Docker (Priority: P2)

**Goal**: `make migrate` applies all pending EF Core migrations using a short-lived SDK container — zero host .NET SDK required.

**Independent Test**: After `make up` on a fresh DB volume, run `make migrate` → migrations applied → backend serves authenticated API responses.

### Implementation for User Story 3

- [x] T031 [US3] Add `migrator` service to `docker-compose.yml` with `profiles: [migration]` and `backend/Dockerfile.migrator`
- [x] T032 [US3] Add `make migrate` target to `Makefile`
- [x] T033 [US3] Add `make clean` target to `Makefile` with 5-second warning guard

**Checkpoint**: `make up && make migrate` → migrations applied. Backend responds. ✅

---

## Phase 6: User Story 4 — Scaffold New Migration via Make (Priority: P2)

**Goal**: `make migrate-add NAME=SomeFeature` creates a new EF Core migration file on the host filesystem without any local .NET SDK.

**Independent Test**: Run `make migrate-add NAME=TestMigration` → new file appears in `backend/src/NaderGorge.Infrastructure/Data/Migrations/` → `make migrate` applies it successfully.

### Implementation for User Story 4

- [x] T034 [US4] Add `make migrate-add` target to `Makefile` with NAME validation and `docker run --rm` + volume mount
- [ ] T035 [US4] Verify the generated migration file appears in `backend/src/NaderGorge.Infrastructure/Data/Migrations/` with correct timestamp prefix — fix volume mount path if needed

**Checkpoint**: `make migrate-add NAME=DockerTest` → file created on host → `make migrate-add` with no `NAME` prints helpful error. ✅

---

## Phase 7: User Story 5 — Production-Ready Multi-Stage Images (Priority: P3)

**Goal**: Each final image contains only runtime artifacts — no SDK, no dev deps, no source code, no compiler caches.

**Independent Test**: Build all images, verify base layers use runtime images (not build images).

### Implementation for User Story 5

- [x] T036 [P] [US5] `frontend/Dockerfile` final stage copies only `.next/standalone/`, `.next/static`, `public/`; `frontend/.dockerignore` added
- [x] T037 [P] [US5] `backend/Dockerfile` final stage uses `mcr.microsoft.com/dotnet/aspnet:8.0`, copies only `/app/publish`; `backend/.dockerignore` added
- [x] T038 [P] [US5] `worker/Dockerfile` final stage uses `node:20-slim` + `npm ci --omit=dev`, copies only `dist/`; `worker/.dockerignore` added
- [x] T039 [US5] Root `.dockerignore` updated to cover all service artifacts and secrets

**Checkpoint**: Images built with lean final stages. ✅

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final touches for developer experience and completeness.

- [ ] T040 [P] Update AGENTS.md / Active Technologies section to include Docker Compose v2, multi-stage builds
- [x] T041 [P] Update root `.gitignore` to ensure `.env`, `frontend/.env*.local`, `worker/.env` gitignored (`.env.example` tracked)
- [ ] T042 Validate `quickstart.md` against the actual implemented workflow
- [ ] T043 [P] Verify `make dev` (native process spawning) still works cleanly alongside Docker stack
- [ ] T044 Add a `## Docker` section to the project README

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)            → No dependencies — start immediately
Phase 2 (Dockerfiles)      → Depends on Phase 1 — BLOCKS all user stories
Phase 3 (US1 — make up)    → Depends on Phase 2 (images must build)
Phase 4 (US2 — make cmds)  → Depends on Phase 3 (needs working compose)
Phase 5 (US3 — migrate)    → Depends on Phase 3 (needs running db)
Phase 6 (US4 — migrate-add)→ Depends on Phase 5 (must have EF tooling validated)
Phase 7 (US5 — images)     → Can run in parallel with Phases 4–6 (audit-only)
Phase 8 (Polish)           → Depends on all story phases complete
```

### Parallel Opportunities

- T004, T005, T006 (three Dockerfiles) → full parallel
- T015–T030 (all individual `make` log/shell/build targets) → full parallel
- T036, T037, T038, T039 (image audits) → full parallel
- T040, T041, T043, T044 (polish tasks) → full parallel

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Dockerfiles (T004–T007)
3. Complete Phase 3: US1 — `make up` working (T008–T014)
4. **STOP and VALIDATE**: `make up && make ps` → all healthy, frontend loads
5. Ship this as the foundation — everything else is additive

### Incremental Delivery

1. Phase 1 + Phase 2 → Images build ✅
2. Phase 3 → `make up` works ✅
3. Phase 4 → Full Makefile DX ✅
4. Phase 5 + 6 → Migration workflow ✅
5. Phase 7 → Production image quality ✅
6. Phase 8 → Polish ✅

---

## Notes

- `[P]` = different files, no deps on incomplete tasks in same phase
- `[US#]` = maps task to user story for traceability
- T007 (smoke test) is a gate — if `docker build` fails, fix before running compose
- The `migrator` service uses `profiles: [migration]` so `make up` never starts it automatically
- `make clean` has a 5-second sleep guard to prevent accidental data loss
- The existing `make dev` / `make stop` / `make frontend` / `make backend` targets are **never modified** — appended only

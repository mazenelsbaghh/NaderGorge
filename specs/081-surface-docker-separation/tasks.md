# Tasks: Surface Docker Separation and Masar Platform Rename

**Input**: Design documents from `/specs/081-surface-docker-separation/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/surface-runtime-contract.md, quickstart.md
**Generation Prompt**: "create the tasks file so that a cheaper llm model can implement without problems"

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in `specs/081-surface-docker-separation/spec.md`
- [x] Phase 2: Technical Planning (`speckit-plan`) completed in `specs/081-surface-docker-separation/plan.md`
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed in `specs/081-surface-docker-separation/tasks.md`

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes a different file and has no dependency on unfinished tasks
- **[Story]**: Maps to user stories in `spec.md`
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare feature-specific shared files and preserve existing app boundaries.

- [x] T001 Create `frontend/src/packages/surface-runtime/config.ts` exporting typed surface names, default public origins, and route decision helpers with no React dependency.
- [x] T002 [P] Create `scripts/verify-surface-separation.mjs` with a Node.js CLI skeleton that supports `--static-only` and default static+runtime modes.
- [x] T003 [P] Update `specs/081-surface-docker-separation/quickstart.md` only if implementation port names or commands differ from the contract.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Fix API URL separation and route-boundary primitives before Docker services use them.

- [x] T004 Update `frontend/src/packages/surface-runtime/config.ts` to define `SurfaceName`, `getSurfaceName()`, `getSurfaceOrigins()`, `getRouteBoundaryDecision()`, and `createRedirectUrl()` functions.
- [x] T005 Update `frontend/src/proxy.ts` to import the surface-runtime helpers and apply route boundaries for `landing`, `student`, `admin`, and fallback `all` surfaces.
- [x] T006 Update `frontend/src/app/page.tsx` so server-side landing stats use `process.env.INTERNAL_API_URL` first, then `NEXT_PUBLIC_API_URL`, then `http://localhost:5245/api`; remove hardcoded replacement from `localhost:5245` to `backend:5245`.
- [x] T007 Update `frontend/src/app/api/qr/[codeHash]/route.ts` so the server-side QR activation request uses `process.env.INTERNAL_API_URL` first, then `NEXT_PUBLIC_API_URL`, then `http://localhost:5245/api`; remove hardcoded replacement from `localhost:5245` to `backend:5245`.
- [x] T008 Update `frontend/src/services/api-client.ts` so the default browser API base URL is `http://localhost:5245/api` and all refresh-token calls reuse the same computed base URL constant.
- [x] T009 Update `frontend/src/utils/resolve-media-url.ts` so the default browser backend origin is `http://localhost:5245` via a single exported or local constant.

**Checkpoint**: `cd frontend && npm run lint` should parse the updated frontend files without new lint errors.

---

## Phase 3: User Story 1 - Run Each Surface Independently (Priority: P1) MVP

**Goal**: Landing, student, admin, and backend run as distinct Docker services with unique host ports.

**Independent Test**: Run static verification and inspect `docker compose config --format json` for separate `landing`, `student`, `admin`, and `backend` services with unique host ports.

### Tests for User Story 1

- [x] T010 [P] [US1] Implement static Compose parsing in `scripts/verify-surface-separation.mjs` using `docker compose config --format json` and assert required services exist.
- [x] T011 [P] [US1] Implement unique application-port validation in `scripts/verify-surface-separation.mjs` for `landing`, `student`, `admin`, and `backend`.
- [x] T012 [P] [US1] Implement healthcheck validation in `scripts/verify-surface-separation.mjs` for `landing`, `student`, `admin`, `backend`, `worker`, `db`, and `redis`.

### Implementation for User Story 1

- [x] T013 [US1] Replace the single `frontend` service in `docker-compose.yml` with three frontend services named `landing`, `student`, and `admin`, each using the same frontend image/build but a distinct `APP_SURFACE`.
- [x] T014 [US1] Update `docker-compose.yml` application container names to `masar_landing`, `masar_student`, `masar_admin`, `masar_backend`, `masar_worker`, `masar_db`, `masar_redis`, and `masar_migrator`.
- [x] T015 [US1] Update `docker-compose.yml` ports so `landing` uses `${MASAR_LANDING_PORT:-8738}:8738`, `student` uses `${MASAR_STUDENT_PORT:-8739}:8738`, `admin` uses `${MASAR_ADMIN_PORT:-8740}:8738`, and `backend` uses `${MASAR_BACKEND_PORT:-5245}:5245`.
- [x] T016 [US1] Update `docker-compose.yml` network and volume names to use Masar naming while preserving db, redis, worker, and migrator service dependencies.
- [x] T017 [US1] Update `frontend/Dockerfile` comments and build args to support reusable frontend surfaces without changing the internal app port.
- [x] T018 [US1] Update `Makefile` `up`, `build-frontend`, `logs-*`, `shell-*`, `stop`, and `ps` output to reference landing/student/admin/backend separated URLs and service names.
- [x] T019 [US1] Add `Makefile` targets `logs-landing`, `logs-student`, `logs-admin`, `shell-landing`, `shell-student`, `shell-admin`, `verify-surfaces-static`, and `verify-surfaces`.

**Checkpoint**: `node scripts/verify-surface-separation.mjs --static-only` passes.

---

## Phase 4: User Story 2 - Keep Backend API Separate and Internally Reachable (Priority: P1)

**Goal**: Frontend surfaces use browser-reachable API URLs for client code and Docker-internal API URLs for server code.

**Independent Test**: Static verification confirms frontend service environment contains both public and internal API URLs; frontend build confirms TypeScript can compile the URL changes.

### Tests for User Story 2

- [x] T020 [P] [US2] Extend `scripts/verify-surface-separation.mjs` to assert each frontend service has `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_BACKEND_URL`, `INTERNAL_API_URL`, and `INTERNAL_BACKEND_URL`.
- [x] T021 [P] [US2] Extend `scripts/verify-surface-separation.mjs` to assert backend CORS default includes landing, student, and admin public origins.

### Implementation for User Story 2

- [x] T022 [US2] Update `docker-compose.yml` frontend build args and runtime env so `NEXT_PUBLIC_API_URL` defaults to `http://localhost:${MASAR_BACKEND_PORT:-5245}/api` and `INTERNAL_API_URL` defaults to `http://backend:5245/api`.
- [x] T023 [US2] Update `docker-compose.yml` backend `Cors__AllowedOrigins` default to include `http://localhost:8738,http://localhost:8739,http://localhost:8740`.
- [x] T024 [US2] Update `.env.example` with `MASAR_LANDING_PORT`, `MASAR_STUDENT_PORT`, `MASAR_ADMIN_PORT`, `MASAR_BACKEND_PORT`, public frontend origins, public backend URL, and internal backend URL variables.
- [x] T025 [US2] Update `docker-compose.override.yml` only if its db/redis port overrides conflict with the new Masar defaults.

**Checkpoint**: `node scripts/verify-surface-separation.mjs --static-only` confirms URL and CORS configuration.

---

## Phase 5: User Story 3 - Rebrand User-Facing Platform Identity to Masar (Priority: P1)

**Goal**: Visible UI metadata and Docker runtime naming use `منصة مسار` / `Masar Platform` / `masar`.

**Independent Test**: Search changed user-facing files and Docker config for old visible brand strings; load landing/login/admin/student roots and confirm Masar identity.

### Tests for User Story 3

- [x] T026 [P] [US3] Extend `scripts/verify-surface-separation.mjs` to fail if application service or container names contain `nadergorge`.
- [x] T027 [P] [US3] Add brand string verification in `scripts/verify-surface-separation.mjs` for frontend source and text/SVG public assets: no `مسار أكاديمي`, `Massar Academy`, `MASSAR ACADEMY`, `Nader George`, or `نادر جورج` in visible metadata/copy.

### Implementation for User Story 3

- [x] T028 [US3] Update `frontend/src/app/layout.tsx` metadata title and description from `مسار أكاديمي | Massar Academy` to `منصة مسار | Masar Platform`.
- [x] T029 [US3] Update `frontend/src/app/(public)/login/page.tsx` visible brand heading and footer caption to `منصة مسار`.
- [x] T030 [US3] Update `frontend/src/app/api/video/embed/route.ts` default watermark brand from `Massar Academy` to `Masar Platform`.
- [x] T031 [US3] Update `frontend/src/app/api/video/embed/route.ts` direct-load rejection text from old academy wording to Masar wording.
- [x] T032 [US3] Update `.env.example`, `Makefile`, and `docker-compose.yml` comments/headings from old brand labels to Masar labels.
- [x] T033 [US3] Update `PRODUCT.md` title and direct old teacher-name references to `Masar Platform` / `منصة مسار` without changing the design principles.
- [x] T047 [US3] Replace legacy public logo assets and remaining visible shell/about/FAQ/QR/navigation copy with `منصة مسار` / `Masar Platform`.

**Checkpoint**: `node scripts/verify-surface-separation.mjs --static-only` brand checks pass.

---

## Phase 6: User Story 4 - Provide Precise Verification Commands (Priority: P2)

**Goal**: Operators have repeatable commands for static and runtime verification.

**Independent Test**: Run `make verify-surfaces-static` without containers, and run `make verify-surfaces` when containers are up.

### Tests for User Story 4

- [x] T034 [P] [US4] Implement runtime HTTP checks in `scripts/verify-surface-separation.mjs` for landing `/`, student `/`, admin `/`, and backend `/api/health`.
- [x] T035 [P] [US4] Implement route-boundary redirect/rewrite checks in `scripts/verify-surface-separation.mjs` using status/location checks where possible.

### Implementation for User Story 4

- [x] T036 [US4] Update `specs/081-surface-docker-separation/quickstart.md` with final Make targets and final default URLs.
- [x] T037 [US4] Add troubleshooting notes to `specs/081-surface-docker-separation/quickstart.md` for port conflicts and missing secrets.

**Checkpoint**: `make verify-surfaces-static` passes and documents how to run runtime verification.

---

## Phase 7: Polish & Cross-Cutting Verification

**Purpose**: Validate all modified surfaces and keep regressions out.

- [x] T038 Run `node scripts/verify-surface-separation.mjs --static-only` from the repository root and fix any reported issue.
- [x] T039 Run `cd frontend && npm run lint` and fix any new lint error from this feature.
- [x] T040 Run `cd frontend && npm run build` and fix any new build error from this feature.
- [x] T041 Run `dotnet build backend/NaderGorge.sln --no-restore` or `dotnet build backend/NaderGorge.sln` and fix any new backend compile issue caused by Docker/env changes.
- [x] T042 Run `docker compose config --format json` and confirm it renders without Compose errors.
- [x] T043 If Docker secrets are available, run `make build` and `make up`, then run `make verify-surfaces`; otherwise record the missing-secret limitation in `achievements.md`.
- [x] T044 Review `specs/081-surface-docker-separation/spec.md`, `plan.md`, and `tasks.md` against the final implementation and update docs if behavior changed.
- [x] T045 Fix Docker build failure by ensuring only one frontend service builds `masar_frontend:local` while the other frontend surfaces reuse that image.
- [x] T046 Suppress non-actionable third-party npm deprecation notices in Docker install steps by using npm error-level logging in `frontend/Dockerfile` and `worker/Dockerfile`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Starts immediately.
- **Foundational (Phase 2)**: Depends on Phase 1 and blocks all Docker service changes.
- **US1 (Phase 3)**: Depends on foundational route/API primitives.
- **US2 (Phase 4)**: Can run after US1 compose service structure exists.
- **US3 (Phase 5)**: Can run after compose naming exists and can overlap with US2 except for shared docs.
- **US4 (Phase 6)**: Depends on verification script and final URLs from US1/US2.
- **Polish (Phase 7)**: Depends on all user stories.

### Parallel Opportunities

- T002 and T003 can run in parallel with T001.
- T010, T011, and T012 can run in parallel after the verification script exists.
- T020 and T021 can run in parallel after compose services exist.
- T026 and T027 can run in parallel after brand/name targets are known.
- T034 and T035 can run in parallel after static verification is complete.

## Implementation Strategy

1. Complete setup and foundational frontend routing/API URL separation.
2. Deliver MVP with US1: separated Docker services and unique ports.
3. Add US2 to make backend/browser/internal URL behavior correct.
4. Add US3 to finish Masar visible/runtime identity.
5. Add US4 and polish verification.

# Feature Specification: Phase 0 - Baseline, Specs, and Safety Inventory

**Feature Branch**: `088-phase0-baseline-safety-inventory`  
**Created**: 2026-06-07  
**Status**: Draft  
**Input**: User description: "Phase 0 - Baseline, Specs, and Safety Inventory from platform_expansion_plan.md. Target is to establish a solid baseline: inventory existing entities, features, UI routes, run baseline automation tests, satisfy Docker gates, perform manual QA checks on existing surfaces, and prepare specifications/tasks for upcoming expansion phases."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - System Integrity & Compile Check (Priority: P1)

Verify that the current codebase (backend, frontend, worker) is clean, compiles correctly, and passes all existing test suites.

**Why this priority**: If the project does not compile or has failing tests on the baseline, any subsequent changes will be unstable and difficult to diagnose.

**Independent Test**: Running compiler commands (`dotnet build`, `npm run lint`, `npm run build`) in their respective directories.

**Acceptance Scenarios**:

1. **Given** the backend solution, **When** running `dotnet build backend/NaderGorge.sln`, **Then** the solution builds successfully with no compilation errors.
2. **Given** the backend tests, **When** running `dotnet test backend/NaderGorge.sln --no-build`, **Then** all tests pass.
3. **Given** the frontend application, **When** running `npm run lint` and `npm run build` inside `frontend/`, **Then** linting completes successfully and the build succeeds.
4. **Given** the worker application, **When** running `npm run build` inside `worker/`, **Then** the TypeScript build succeeds without errors.

---

### User Story 2 - Docker Environment & Health Gate (Priority: P1)

Verify that the local development stack boots correctly using Docker, migrations can be applied, and all system health checks are responsive.

**Why this priority**: Docker compose is the runtime environment for local development and testing. We must ensure it is fully functional before expanding it.

**Independent Test**: Running the docker compose commands and verifying HTTP responses from service health checks.

**Acceptance Scenarios**:

1. **Given** the Docker configuration, **When** running `docker compose config -q`, **Then** the compose setup is validated with no errors.
2. **Given** the containers are running (`make up`), **When** running database migrations (`make migrate`), **Then** C# EF Core migrations are successfully applied.
3. **Given** the stack is running, **When** calling health checks for backend (`http://localhost:5245/api/health`) and worker (`http://localhost:3001/health`), **Then** both endpoints return HTTP 200.

---

### User Story 3 - Surface Separation & Endpoint Integrity (Priority: P2)

Ensure backend endpoints are accurately documented and that there is static separation of roles/surfaces across the frontend.

**Why this priority**: Helps trace dependencies and ensures we do not leak routes across admin, teacher, assistant, and student interfaces.

**Independent Test**: Running endpoint generation and surface separation verification scripts.

**Acceptance Scenarios**:

1. **Given** the endpoint inventory script, **When** running `node scripts/generate-endpoint-inventory.mjs --check`, **Then** it passes successfully with no schema mismatches.
2. **Given** the surface separation script, **When** running `node scripts/verify-surface-separation.mjs --static-only`, **Then** static checks pass, indicating no architectural violations between frontend portals.

---

### User Story 4 - Security & Secrets Baseline Audit (Priority: P2)

Verify that local and environment secrets are configured correctly and that default secrets are not exposed or misused in production.

**Why this priority**: Security must be verified on the baseline to protect sensitive user profiles and operations.

**Independent Test**: Inspection of environment configuration template and checks for presence of required secrets.

**Acceptance Scenarios**:

1. **Given** the `.env` file, **When** checking for critical variables (JWT_SECRET, API_CALLBACK_SECRET, WORKER_ADMIN_TOKEN, GEMINI_API_KEY), **Then** all required keys are populated.

### Edge Cases

- **PostgreSQL Connection Failures**: If PostgreSQL container is slow to start, `make migrate` must gracefully fail or wait instead of leaving database in a corrupted state.
- **Port Conflicts**: Port conflicts on localhost (e.g. 5245, 3001, 8738, 8739, 8740) must be identified so developers know if other instances are running.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin - Visit `http://localhost:8738`, check login screen, and navigate to users page to verify test data.
- **Manual QA Role/Flow 2**: Student - Visit `http://localhost:8740`, check student login, and verify student dashboard.
- **Manual QA Role/Flow 3**: Developer - Open Bull Board at `http://localhost:3001/ui` and check queue status.
- **Manual QA Negative Check**: Attempt to access `/api/admin/*` endpoints without auth headers to verify 401/403 response.
- **Docker Acceptance**: Run `make up`, `make migrate`, `make ps`, and ensure all main services (postgres, redis, backend, worker, frontend) are in `running` status.
- **External Dependencies**: Local mock servers for Telegram or email are active or stubbed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Backend MUST compile with the latest .NET 9 SDK version without compilation errors.
- **FR-002**: Backend test suite MUST execute and pass all tests.
- **FR-003**: Frontend MUST build in Next.js production mode and pass strict linting rules.
- **FR-004**: Worker project MUST build using `tsc`.
- **FR-005**: All endpoint controllers MUST be inventory-checked, validating against existing documentation schemas.
- **FR-006**: The local Docker Compose setup MUST start all services: database (`db`), queue (`redis`), background processor (`worker`), Web API (`backend`), and frontend servers.
- **FR-007**: EF Core migrations MUST be applicable to PostgreSQL automatically via `make migrate`.

### Key Entities *(include if feature involves data)*

This phase is primarily a discovery/baseline task. It does not introduce new entities, but catalogs the following existing critical tables:
- **User / StudentProfile**: Exists in DB. Controls authentication and student progress context.
- **Lesson / LessonVideo**: Exists in DB. Contains lesson chapters and video links.
- **NotificationEvent**: Exists in DB. Contains queued student notifications.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Clean build status (`0` errors) across all backend, worker, and frontend project spaces.
- **SC-002**: All 3 verification scripts (`generate-endpoint-inventory.mjs`, `verify-surface-separation.mjs`, pytest) pass with exit code `0`.
- **SC-003**: Health URLs return `200` status in under `10` seconds from starting.

## Assumptions

- Standard local environment runs Docker and has Docker Compose v2 installed.
- Next.js packages are fully restored without peer dependency conflicts.
- Environment variables are loaded correctly from `.env` file in root directory.

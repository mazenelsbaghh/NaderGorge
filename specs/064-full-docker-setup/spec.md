# Feature Specification: Full Docker Setup (Frontend, Backend, Database & Make)

**Feature Branch**: `064-full-docker-setup`
**Created**: 2026-04-18
**Status**: Draft
**Input**: User description: "كل حاجه من الدوكر للفرونت و للباك و للداتا بيز و اشغلوا بالدوكر و اعمل كل حاجه بالدوكر و ب make"

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Developer Starts Entire Platform with One Command (Priority: P1)

A developer clones the repository on a fresh machine and runs a single Make command to launch every service — frontend, backend, AI worker, PostgreSQL, and Redis — all inside Docker containers. No manual installation of language runtimes, package managers, or databases is required on the host machine.

**Why this priority**: This is the primary goal of the feature. Everything else depends on the containers being buildable and runnable first.

**Independent Test**: Run `make up` (or equivalent) from the project root on a machine with only Docker and Make installed. Verify all services start and the frontend is reachable in the browser.

**Acceptance Scenarios**:

1. **Given** a machine with only Docker and Make installed, **When** the developer runs `make up`, **Then** all services (frontend, backend, worker, PostgreSQL, Redis) start successfully and are reachable at their configured ports.
2. **Given** all services are running, **When** the developer visits `http://localhost:<frontend-port>` in the browser, **Then** the frontend loads correctly and can reach the backend API.
3. **Given** all services are running, **When** the developer checks the backend health endpoint, **Then** it responds with a successful status.

---

### User Story 2 - Developer Stops, Rebuilds, and Restarts with Make (Priority: P1)

Developers need granular Make targets to stop all services, rebuild images (e.g., after code changes), restart individual services, tear down everything including volumes, and view logs — all without memorizing Docker commands.

**Why this priority**: Day-to-day developer workflow depends on predictable stop/rebuild/restart cycles.

**Independent Test**: Run `make down`, then `make build`, then `make up` and verify everything restarts cleanly.

**Acceptance Scenarios**:

1. **Given** running containers, **When** the developer runs `make down`, **Then** all containers stop and are removed without error.
2. **Given** stopped containers, **When** the developer runs `make build`, **Then** all Docker images are rebuilt from their Dockerfiles.
3. **Given** a running environment, **When** the developer runs `make restart`, **Then** all containers restart cleanly with fresh state.
4. **Given** running containers, **When** the developer runs `make logs`, **Then** real-time aggregated logs from all services are streamed in the terminal.
5. **Given** running containers, **When** the developer runs `make logs-backend` (or per-service equivalent), **Then** only that service's logs are shown.

---

### User Story 3 - Developer Runs Database Migrations Inside Docker (Priority: P2)

The developer should be able to apply EF Core migrations and seed initial data via a Make target that executes the migration inside the already-running backend container (or a dedicated migration step), without needing .NET SDK installed on the host.

**Why this priority**: Database schema management must not require host-installed tooling once Docker is the primary environment.

**Independent Test**: Run `make migrate` after `make up` and verify the database schema is up to date and the backend can authenticate a user.

**Acceptance Scenarios**:

1. **Given** the database container is healthy, **When** the developer runs `make migrate`, **Then** all pending EF Core migrations are applied to the PostgreSQL database.
2. **Given** the database has all migrations applied, **When** the backend starts, **Then** it connects successfully and serves API requests.
3. **Given** an error during migration (e.g., duplicate migration), **When** the developer runs `make migrate`, **Then** the failure is reported clearly without crashing other services.

---

### User Story 4 - Developer Adds New Migration from Host via Make (Priority: P2)

A developer adding a new feature can scaffold a new EF Core migration by running a Make target (e.g., `make migrate-add NAME=AddSomeFeature`) that executes the `dotnet ef migrations add` command inside Docker, without needing .NET SDK on the host.

**Why this priority**: Schema evolution is a regular task; it must work fully within the Docker workflow.

**Independent Test**: Run `make migrate-add NAME=TestMigration` and verify a new migration file appears in the backend migrations folder.

**Acceptance Scenarios**:

1. **Given** a running backend container, **When** the developer runs `make migrate-add NAME=MyNewMigration`, **Then** a new migration file is created in the correct backend source directory.
2. **Given** the new migration file exists, **When** the developer runs `make migrate`, **Then** the new migration is applied to the database.

---

### User Story 5 - Production-Ready Docker Images (Priority: P3)

Each service (frontend, backend, worker) has a Dockerfile built with multi-stage builds: a build stage and a lean runtime stage. Images are minimal and do not include development tooling in the final layer.

**Why this priority**: Production correctness is important but secondary to the local development workflow being fully functional first.

**Independent Test**: Build all images, inspect their sizes, and verify they run correctly without unnecessary development dependencies in the final layer.

**Acceptance Scenarios**:

1. **Given** the Dockerfiles, **When** images are built, **Then** the frontend image uses a multi-stage build (build stage + lightweight server stage).
2. **Given** the Dockerfiles, **When** images are built, **Then** the backend image uses a multi-stage build (build stage + ASP.NET runtime stage).
3. **Given** the Dockerfiles, **When** images are built, **Then** the worker image uses a multi-stage build (build stage + Node.js runtime stage).

---

### Edge Cases

- What happens when a port is already in use on the host? The platform should fail fast with a clear error indicating which port is conflicting.
- What happens when the database container is not yet healthy when the backend starts? The backend should retry its connection and not crash permanently; Make should wait for the DB healthcheck before starting dependent services.
- What happens when an environment variable is missing (e.g., no `.env` file)? Startup should fail with a descriptive error rather than silently misconfiguring services.
- What happens when a developer changes a source file and wants only that service rebuilt? A per-service rebuild target (e.g., `make build-frontend`) should exist.
- What happens when Docker volumes contain stale data from a previous schema? `make clean` or `make nuke` should offer a way to destroy volumes and start fresh.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a `Dockerfile` for the Next.js frontend service that builds and serves the application using a minimal runtime image.
- **FR-002**: The system MUST provide a `Dockerfile` for the .NET backend (ASP.NET Core API) service that compiles and runs the application using only the ASP.NET runtime image (no SDK in final layer).
- **FR-003**: The system MUST provide a `Dockerfile` for the Node.js AI worker service that compiles TypeScript and runs the worker using a minimal Node.js runtime image.
- **FR-004**: The system MUST have a single `docker-compose.yml` at the project root that defines ALL services: frontend, backend, worker, PostgreSQL, and Redis — with proper networking, health checks, and dependency ordering (`depends_on`).
- **FR-005**: All environment variables for each service MUST be passed through Docker Compose via `.env` files (one root `.env` for shared secrets, or per-service env files). No secrets shall be hardcoded in Dockerfiles or Compose files.
- **FR-006**: The system MUST provide a `Makefile` at the project root with the following targets at minimum:
  - `make up` — build (if needed) and start all services in the background
  - `make down` — stop and remove all containers
  - `make build` — rebuild all Docker images
  - `make restart` — restart all containers
  - `make logs` — tail logs of all services
  - `make logs-<service>` — tail logs of a specific service (backend, frontend, worker, db, redis)
  - `make migrate` — apply pending EF Core migrations against the running database container
  - `make migrate-add NAME=<name>` — scaffold a new EF Core migration inside the backend container
  - `make shell-<service>` — open an interactive shell inside a running service container
  - `make clean` — stop containers and remove volumes (destroy data)
  - `make ps` — show status of all containers
- **FR-007**: The frontend container MUST be configured with the correct `NEXT_PUBLIC_API_URL` pointing to the backend service using Docker's internal service DNS (not `localhost`).
- **FR-008**: The backend container MUST be configured to connect to PostgreSQL and Redis using Docker's internal service DNS.
- **FR-009**: The worker container MUST be configured to connect to Redis and PostgreSQL using Docker's internal service DNS, and MUST have access to the `GEMINI_API_KEY` and `API_CALLBACK_SECRET`.
- **FR-010**: Each service in Compose MUST declare a `healthcheck` so that `depends_on` can use `condition: service_healthy`.
- **FR-011**: The `make up` target MUST wait for the database to be healthy before starting the backend and worker.
- **FR-012**: `.env.example` files MUST be updated (or a root-level `.env.example` created) documenting all required environment variables for the fully-Dockerized stack.
- **FR-013**: The existing `make dev` target (local non-Docker development with direct process spawning) MUST be preserved and still work alongside the Docker workflow.

### Key Entities

- **Dockerfile (Frontend)**: Multi-stage build image — install deps → build Next.js → serve with a production server.
- **Dockerfile (Backend)**: Multi-stage — restore packages with SDK image → publish → run with ASP.NET runtime image only.
- **Dockerfile (Worker)**: Multi-stage — install Node packages → compile TypeScript → run with Node.js slim image.
- **docker-compose.yml**: Orchestration manifest defining all 5 services, shared networks, named volumes, env variable wiring, and healthchecks.
- **Makefile**: Developer-facing command surface — wraps all Docker Compose commands into short, memorable targets.
- **.env / .env.example**: Root-level environment variable file used by Docker Compose, containing all secrets and configuration for the full stack.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer with zero local language runtimes (no .NET, no Node.js, no npm) can bring the entire platform up and reach the running frontend in under 5 minutes after a cold `make up` (excluding initial image download time).
- **SC-002**: All 5 services (frontend, backend, worker, PostgreSQL, Redis) start in the correct dependency order with zero manual intervention after running `make up`.
- **SC-003**: The developer can apply database migrations via `make migrate` without any local .NET SDK installed and the backend starts serving authenticated API responses within 30 seconds after migrations complete.
- **SC-004**: Running `make down` followed by `make up` completes without error in 100% of attempts (no stale state or port conflicts caused by the Docker setup itself).
- **SC-005**: Each built Docker image for production-bound services (frontend, backend, worker) contains no build-time tooling (SDK, compiler caches, source maps beyond what the runtime needs) in its final layer.
- **SC-006**: All Make targets are documented with `make help` (or inline comments) so that a new developer understands every available command without reading the Makefile source.

---

## Assumptions

- The existing `docker/docker-compose.yml` (which currently only covers PostgreSQL, Redis, and Telegram Bot API) will be replaced by a new root-level compose file covering all 5 services. The SQL scripts in `docker/` are preserved as-is.
- The frontend is a Next.js application that supports being built as a production bundle and served via `next start` (not just `next dev`) inside a container.
- The backend's EF Core migrations can be applied by running `dotnet ef database update` inside a container that has the .NET SDK and the backend source code mounted or copied.
- ffmpeg/ffprobe — required by the worker for video processing — will be installed inside the worker Docker image as part of its Dockerfile rather than relying on a host installation.
- The `make dev` target (local process spawning) is preserved for developers who prefer a native local workflow without Docker.
- A single `.env` file at the project root (gitignored) will be the source of truth for all Docker Compose variable substitution.
- Named Docker volumes are used for PostgreSQL and Redis data persistence across container restarts.

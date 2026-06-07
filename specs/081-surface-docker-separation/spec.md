# Feature Specification: Surface Docker Separation and Massar Platform Rename

**Feature Branch**: `081-surface-docker-separation`  
**Created**: 2026-06-06  
**Status**: Draft  
**Input**: User description: "عايز افصل تماما الاندج بيدج و الباك و صفحه الطالب و الدامن عن بعض ف الدوكر و البورتات و كل حاجه تماما و بدقه شديه و تعمل تيستات تتاكد ان كل حاجه بقت تمام ... و تغير كل حاجه تبقي منصه مسار"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run Each Surface Independently (Priority: P1)

A developer or operator needs the public landing page, student experience, admin experience, and backend API to run as separate runtime surfaces with distinct addresses, ports, health checks, logs, and lifecycle commands.

**Why this priority**: The main requested value is strict separation. If one surface is unavailable or being rebuilt, the other surfaces must remain independently addressable and diagnosable.

**Independent Test**: Start the Docker stack and verify that the landing, student, admin, and backend endpoints each respond on their own configured host port with the expected content or health response.

**Acceptance Scenarios**:

1. **Given** the Docker stack is not running, **When** the operator starts the platform, **Then** four separate application surfaces start: landing, student, admin, and backend API.
2. **Given** all surfaces are running, **When** the operator checks the published ports, **Then** no two surfaces share the same host port and each surface has a clear port assignment.
3. **Given** the student surface is requested, **When** a browser opens the student URL, **Then** it loads the student entry point without relying on the landing or admin URL.
4. **Given** the admin surface is requested, **When** a browser opens the admin URL, **Then** it loads the admin entry point without relying on the landing or student URL.

---

### User Story 2 - Keep Backend API Separate and Internally Reachable (Priority: P1)

Frontend surfaces need a stable way to reach the backend API inside Docker while the backend remains separately published for direct health and API verification.

**Why this priority**: The separated frontend surfaces cannot be correct if they still depend on host-local addresses or a shared ambiguous frontend/backend port.

**Independent Test**: Start the stack and verify that every frontend surface can resolve API calls through the Docker network while the backend health endpoint is reachable on its dedicated host port.

**Acceptance Scenarios**:

1. **Given** the stack is running, **When** the backend health endpoint is called on its dedicated host port, **Then** it returns a successful health response.
2. **Given** any frontend surface is running in Docker, **When** it calls the API, **Then** the call targets the backend service through internal Docker DNS rather than host `localhost`.
3. **Given** backend logs are inspected, **When** requests arrive from landing, student, or admin surfaces, **Then** the requests are distinguishable by source surface configuration or logs.

---

### User Story 3 - Rebrand User-Facing Platform Identity to Massar (Priority: P1)

Students, admins, and public visitors should see the platform identity as "منصة مسار" in Arabic contexts and "Massar Platform" in English contexts, without mixed old brand names in visible UI, metadata, or Docker runtime labels.

**Why this priority**: The user explicitly requested the platform to become "منصة مسار". Mixed naming reduces trust and makes deployments hard to reason about.

**Independent Test**: Search the user-facing frontend output and Docker configuration for old visible brand strings, then load the landing, login, student, and admin routes to verify the Massar identity appears consistently.

**Acceptance Scenarios**:

1. **Given** a public visitor opens the landing surface, **When** the page renders, **Then** visible brand copy and metadata identify the product as "منصة مسار" or "Massar Platform".
2. **Given** a student opens the student surface, **When** the page or login state renders, **Then** visible platform identity uses "منصة مسار" or "Massar Platform".
3. **Given** an admin opens the admin surface, **When** the shell or page metadata renders, **Then** visible platform identity uses "منصة مسار" or "Massar Platform".
4. **Given** an operator inspects Docker containers, **When** viewing service or container names, **Then** application containers use the Massar naming convention rather than the old brand.

---

### User Story 4 - Provide Precise Verification Commands (Priority: P2)

A developer needs repeatable tests that confirm the separation, ports, health checks, and brand migration without manual browser-only inspection.

**Why this priority**: The requested change is operational and easy to regress. Automated verification prevents future Docker or routing changes from silently merging surfaces again.

**Independent Test**: Run the documented verification commands after a Docker rebuild and confirm that all checks pass.

**Acceptance Scenarios**:

1. **Given** the stack is built, **When** the verification command runs, **Then** it checks every configured host port and fails if any surface is missing or sharing a port.
2. **Given** frontend routes are reachable, **When** the verification command runs, **Then** it checks landing, student, and admin entry points for expected routing and Massar branding.
3. **Given** Docker Compose files are present, **When** static validation runs, **Then** it verifies service separation, unique ports, and required health checks.

### Edge Cases

- A configured host port is already in use before startup; the platform must fail clearly and identify the conflicting surface.
- A frontend surface starts before the backend is healthy; the surface must still be served, while API-dependent workflows fail gracefully until backend health returns.
- The backend starts but the database or Redis is unavailable; backend health must report degraded or unhealthy state rather than falsely passing.
- A user opens `/admin` on the landing surface or `/student` on the admin surface; the request must be redirected to the correct dedicated surface or produce a clear route boundary response.
- Environment variables are missing for one surface; startup or tests must identify the affected surface by name.
- Old brand names remain in internal namespaces or historical migration names; only user-visible copy, metadata, Docker service/container names, and operational docs are in scope for rename.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define separate runtime surfaces for landing, student, admin, and backend API.
- **FR-002**: Each runtime surface MUST have a unique default host port and a documented environment override.
- **FR-003**: The landing surface MUST route public root traffic to the public landing experience.
- **FR-004**: The student surface MUST route root traffic to the student experience or the student login flow.
- **FR-005**: The admin surface MUST route root traffic to the admin experience or the admin login flow.
- **FR-006**: The backend API MUST expose a dedicated health endpoint on its own host port.
- **FR-007**: Frontend surfaces MUST use backend internal service discovery when running inside Docker.
- **FR-008**: Docker service names, container names, and labels for application services MUST use a Massar naming convention.
- **FR-009**: User-facing product identity MUST display "منصة مسار" for Arabic surfaces and "Massar Platform" for English metadata or operational text.
- **FR-010**: User-facing old brand names MUST be removed from frontend visible copy, page metadata, operational docs changed by this feature, and Docker runtime labels.
- **FR-011**: The system MUST include automated checks for unique ports, expected services, health checks, and route boundaries.
- **FR-012**: The system MUST provide Make targets or documented commands to start, stop, rebuild, log, and verify each separated surface.
- **FR-013**: Existing database, Redis, worker, and migration workflows MUST remain compatible with the separated runtime layout.
- **FR-014**: Existing local development workflows MUST remain available, while Docker becomes the source of truth for separated runtime verification.

### Key Entities

- **Runtime Surface**: A separately addressable application boundary with a name, host port, internal service address, health check, logs, and route entry point.
- **Port Map**: The documented assignment of host ports to landing, student, admin, backend API, worker, database, Redis, and supporting services.
- **Surface Route Boundary**: The routing rule that determines which entry path belongs to which surface and what happens when a request enters the wrong surface.
- **Platform Identity**: The user-facing Arabic and English brand names, metadata, and Docker runtime naming convention for Massar.
- **Verification Suite**: Automated checks that validate Docker Compose structure, running HTTP endpoints, route boundaries, and brand consistency.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All four application surfaces are reachable on unique host ports after one Docker startup command.
- **SC-002**: Static verification reports zero duplicate host ports across application surfaces.
- **SC-003**: Runtime verification confirms the landing, student, admin, and backend health endpoints respond within 30 seconds of startup on a warmed environment.
- **SC-004**: 100% of changed user-facing UI metadata and Docker application service names use "منصة مسار", "Massar Platform", or `massar` naming.
- **SC-005**: Existing backend, frontend, and Docker verification commands complete without new warnings caused by this feature.
- **SC-006**: Route boundary tests prove that opening a surface root sends the user to that surface's expected entry point.

## Assumptions

- The same frontend codebase may be reused by multiple Docker services as long as each service has separate runtime identity, port, environment, health check, and route entry behavior.
- Physical backend namespaces, database migration names, and historical source paths do not need to be renamed because that would create unnecessary migration and compatibility risk.
- Default local host ports will be chosen to avoid the current single-frontend setup and can be overridden from environment variables.
- The backend remains the single API authority for all surfaces; only runtime exposure and frontend entry points are separated.
- The requested "سبيز بلان" means a complete Spec Kit specification and technical plan before implementation.

# Feature Specification: Full Regression, Launch Drill, and Rollback Plan

**Feature Branch**: `098-full-regression-launch-drill`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 12 - Full Regression, Launch Drill, and Rollback Plan"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Test Suite Regression (Priority: P1)

As an operator, I want to execute the entire automated testing suite across the C# backend, Next.js frontend, Node.js worker, Python integration tests, and static validations, so that I can ensure there are no regression issues in the system.

**Why this priority**: Highly critical to confirm that all services compile and pass tests before attempting any launch drill or deployment.

**Independent Test**: Can be validated by running the complete list of testing and building commands and verifying that all tests pass without errors.

**Acceptance Scenarios**:
1. **Given** the repository state, **When** running the compilation and test commands for backend, frontend, worker, and python tests, **Then** all commands must exit with a `0` status code, and all tests must pass.
2. **Given** the endpoint inventory, **When** running the generate-endpoint-inventory checker, **Then** the check must pass without reporting any drift or missing paths.

---

### User Story 2 - Docker Launch & Database Migration Drill (Priority: P1)

As an operator, I want to execute a clean Docker stack build from scratch, execute EF Core database migrations on an empty database, seed test data, and run backup/restore commands, so that I can verify the end-to-end containerized lifecycle and deployment steps.

**Why this priority**: Crucial for testing the production-readiness of the multi-container setup, database seeding, schema migration, and disaster recovery.

**Independent Test**: Can be validated by spinning down the stack, deleting the database volume, building/starting the containers, and running database backup/restore scripts successfully.

**Acceptance Scenarios**:
1. **Given** the Docker containers are stopped, **When** running `make down` and rebuilding without cache, **Then** all 8 services must start successfully and show "healthy".
2. **Given** an empty PostgreSQL database, **When** running database migrations and seeds, **Then** all schemas and seed records must be created correctly.
3. **Given** a running PostgreSQL service, **When** executing a database backup and restore command, **Then** the database must be successfully restored to the target state.

---

### User Story 3 - Role-Based Manual QA Verification (Priority: P2)

As a QA tester, I want to execute manual workflows for Admin, Assistant, Teacher, and Student roles, so that I can verify that access control, notifications, CRM logs, and finance modules function correctly at the UI layer.

**Why this priority**: Essential to guarantee that the user experience is flawless and role-based permissions are enforced as specified in the specifications.

**Independent Test**: Tested manually by logging in as each role and running through their specific workflows.

**Acceptance Scenarios**:
1. **Given** an authenticated Admin user, **When** they access HR, operations approvals, CRM, reports, and payroll, **Then** they must have complete access.
2. **Given** an authenticated Assistant user, **When** they attempt to access payroll approval or system settings, **Then** they must be redirected or blocked with a 403 Forbidden.
3. **Given** an authenticated Teacher user, **When** they open their finance summaries, **Then** they must only see their own payouts and codes (fully isolated).

---

### User Story 4 - Rollback Strategy & Production Audit (Priority: P2)

As a system administrator, I want to have documented rollback steps and verify the production safety checklist (secrets check, backup setup, logs), so that I can minimize downtime and prevent security issues during deployment.

**Why this priority**: Important for risk management to ensure that any failure during deployment can be instantly rolled back.

**Independent Test**: Verified by reviewing the rollback scripts and checking container logs for exposed secrets.

**Acceptance Scenarios**:
1. **Given** a database migration failure, **When** running EF Core migration rollback commands, **Then** the schema must roll back to the specified migration target.
2. **Given** a container logs inspection, **When** searching logs and configurations, **Then** no production secrets must be found hardcoded or exposed in plaintext.

---

### Edge Cases

- **Partial migration failures**: If database migrations fail midway, how to roll back to the last stable version without data loss.
- **WebSocket reconnect timeouts**: How Nginx handles WS timeouts during high-traffic WebSocket reconnections.
- **Seed duplicate keys**: Running the seeder twice must not crash the database or create duplicate critical configurations.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Flow**: Log in as Admin -> Verify HR Attendance -> Approve an Operations Task -> Verify Audit Log update.
- **Manual QA Assistant Flow**: Log in as Assistant -> Log a CRM Call -> Send Chat Message -> Verify Chat notification.
- **Manual QA Teacher Flow**: Log in as Teacher -> Verify isolation (no access to other teacher data).
- **Manual QA Student Flow**: Log in as Student -> View dashboard -> Load video lesson -> Submit mock payment request.
- **Docker Acceptance**: `docker compose ps` must output "healthy" for all 8 containers.
- **External Dependencies**: Gemini API, Evolution API, SMS Gateway app, Nginx SSL (mark mock/sandbox states explicitly).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST pass all C# unit/integration tests with `dotnet test`.
- **FR-002**: System MUST pass Next.js compilation, eslint checks, and TypeScript verification checks.
- **FR-003**: System MUST pass worker build without compilation warnings.
- **FR-004**: System MUST pass python integration tests with `pytest`.
- **FR-005**: System MUST pass endpoint inventory drift verification.
- **FR-006**: System MUST pass local Nginx reverse proxy subdomain routing validations.
- **FR-007**: Database seeder MUST run cleanly without throwing key violations or duplicate record errors.
- **FR-008**: Database backups MUST generate valid `.sql` or custom-format dumps, and restore commands MUST reconstruct the database state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of automated tests across backend, frontend, and Python suites pass.
- **SC-002**: Docker cold start completes in under 5 minutes, and all healthchecks report healthy.
- **SC-003**: Endpoint inventory verification outputs `0` changes or drifts.
- **SC-004**: Rollback steps are defined, and database migrations are fully reversible to the initial baseline.

## Assumptions

- **A-001**: Host machine has `docker`, `dotnet`, `node`, and `python3` installed.
- **A-002**: Active external APIs (Gemini, Evolution) are simulated or running in sandbox environments for regression testing.
- **A-003**: Local ports mapping (8738, 8739, 8740, 5245, 3001) are free and do not conflict with other local processes.

# Tasks: Full Regression, Launch Drill, and Rollback Plan

**Input**: Design documents from `/specs/098-full-regression-launch-drill/`
**Prerequisites**: plan.md, spec.md, quickstart.md

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in `specs/098-full-regression-launch-drill/spec.md`
- [x] Phase 2: Technical Planning (`speckit-plan`) completed in `specs/098-full-regression-launch-drill/plan.md`
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed in `specs/098-full-regression-launch-drill/tasks.md`

---

## Phase 1: Automated Test Suite Regression

**Purpose**: Execute all build, lint, unit, integration, and static validations in the project to verify clean compilation.

- [x] T001 Compile C# Backend project using `dotnet build backend/NaderGorge.sln`.
- [x] T002 Execute C# Backend unit tests using `dotnet test backend/NaderGorge.sln --no-build`.
- [x] T003 Execute Frontend code linter using `cd frontend && npm run lint`.
- [x] T004 Compile and build Next.js frontend using `cd frontend && npm run build`.
- [x] T005 Compile Node.js worker using `cd worker && npm run build`.
- [x] T006 Ensure python testing packages are installed via `python3 -m pip install -r tests/requirements.txt`.
- [x] T007 Execute Python E2E integration tests using `python3 -m pytest`.
- [x] T008 Check endpoint inventory changes using `node scripts/generate-endpoint-inventory.mjs --check`.
- [x] T009 Validate Nginx subdomain configurations statically using `node scripts/verify-surface-separation.mjs --static-only`.
- [x] T010 Validate Docker Compose setup file statically using `docker compose config -q`.

---

## Phase 2: Docker Launch & Database Drills

**Purpose**: Verify the containerized lifecycle from a cold start, schema migration, test seeding, and backup/restore stability.

- [x] T011 Shut down and delete running containers using `make down`.
- [x] T012 Rebuild all 8 Docker container images without cache using `docker compose build --no-cache`.
- [x] T013 Boot the clean container stack using `make up`.
- [x] T014 Execute database migrations inside Docker using `make migrate`.
- [x] T015 Run database seeders inside the C# backend container: `docker compose exec backend dotnet run --project src/NaderGorge.API/NaderGorge.API.csproj --seed` (or equivalent).
- [x] T016 Check container health status using `make ps` and verify all show "healthy".
- [x] T017 Execute a pg_dump to back up the database: `docker exec -t massar_db pg_dump -U postgres -d nadergorge_db > backup.sql`.
- [x] T018 Execute drop database and restore from `backup.sql` to verify database recovery capabilities.

---

## Phase 3: Rollback Procedures Verification

**Purpose**: Ensure database schema changes and configuration changes are fully reversible.

- [x] T019 Document and verify EF Core database migration rollback command (e.g. `dotnet ef database update <target>`).
- [x] T020 Audit configuration files and logs to confirm no secrets are exposed in plaintext.

---

## Phase 4: Role-Based Manual QA Verification Matrix

**Purpose**: Manually verify UI and permission scopes across all primary user roles.

- [x] T021 Admin verification: Manage users, HR attendance, task approvals, CRM assignments, payouts, payroll approvals, reports/audit.
- [x] T022 Assistant verification: Accept and update tasks, CRM call logs, chat/notifications.
- [x] T023 Teacher verification: View isolated finance summaries, check payout lists, confirm no access to other teachers' data.
- [x] T024 Student verification: Login, view dashboard, load video lesson, send payment request.
- [x] T025 Production audit: Verify logs outputs, verify backup files, test container restarts, check default credential safety.

---

## Phase 5: Quality Gates & Closure Report

**Purpose**: Pass final quality gates and output the platform readiness report.

- [x] T026 Run `clean-code-guard` against modified code.
- [x] T027 Run `test-guard` against modified test files.
- [x] T028 Compile and output the final markdown closure report and mark achievements complete.

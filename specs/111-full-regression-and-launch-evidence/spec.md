# Feature Specification: Full Regression and Launch Evidence

**Feature Branch**: `111-full-regression-and-launch-evidence`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: platform_expansion_gap_report_2026-06-09.md (Phase 3)

## Goal
Establish final verification and launch readiness evidence for the entire Nader Gorge platform expansion, validating that domain separation, docker configurations, role-based pages/permissions, and multi-teacher/assistant boundaries are fully functional, warning-free, and secured.

---

## User Scenarios & Verification

### Scenario 1 - Comprehensive Build & Test Regression (Priority: P0)
Verify that all source code is completely free of warnings and compiles perfectly, and all unit/E2E test suites pass without issues.
* **Verification Command**:
  - Backend: `dotnet build` and `dotnet test` (all tests passing)
  - Frontend: `npm run lint` and `npm run build` (compiled successfully, warning-free)
  - Worker: `npm run build`
  - E2E Integration: `.venv/bin/python -m pytest tests/` (all tests passing)
  - Endpoint alignment check: `node scripts/generate-endpoint-inventory.mjs --check`
  - Nginx configuration static check: `node scripts/verify-surface-separation.mjs --static-only`
  - Docker config check: `docker compose config -q`

### Scenario 2 - Docker Cold-Start & Database Migrations (Priority: P0)
Verify that the complete platform can cold-start from scratch cleanly, rebuild all services, and successfully run database migrations.
* **Verification Steps**:
  1. Tear down the environment: `make down`
  2. Rebuild all containers from scratch: `docker compose build --no-cache`
  3. Start all services: `make up`
  4. Apply DB migrations: `make migrate`
  5. Check status: `make ps` and verify all services are healthy.
  6. Execute surface verification: `node scripts/verify-surface-separation.mjs`

### Scenario 3 - Multi-Domain Subdomain Route Separation (Priority: P1)
Verify that subdomains route correctly and separate surfaces:
- Admin on `admin.massar-academy.net`
- Supervisor on `super.massar-academy.net` (or admin)
- Teacher on `teacher.massar-academy.net`
- Assistant/Staff on `staff.massar-academy.net`
- Student on `app.massar-academy.net` or `student.massar-academy.net`
- API on `api.massar-academy.net`
- WS on `ws.massar-academy.net`

---

## Acceptance Criteria
- **AC-001**: Clean compilation and test success on C# backend, worker build, and Next.js frontend build.
- **AC-002**: Zero failures on Python integration tests, verifying all multi-teacher boundaries and permissions.
- **AC-003**: 100% matched and current endpoint inventories via check script.
- **AC-004**: Clean Docker cold-start with all container health checks passing.
- **AC-005**: Correct Nginx surface isolation checks showing no legacy domains and working redirection blocks.
- **AC-006**: Complete launch evidence report detailing all test runs, readiness status (Ready / Not Ready), backup/restore proof, and rollback plan.

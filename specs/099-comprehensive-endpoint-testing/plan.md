# Implementation Plan: Comprehensive Endpoint Testing

**Branch**: `099-comprehensive-endpoint-testing` | **Date**: 2026-06-09 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/099-comprehensive-endpoint-testing/spec.md)
**Input**: Feature specification from `specs/099-comprehensive-endpoint-testing/spec.md`

## Summary

The objective of this task is to execute a comprehensive REST API endpoint validation sweep on the running Docker container stack using a custom Python script. The script will parse the 215 routes defined in `tests/endpoint_inventory.json`, authenticate as Admin (`20000000000`), Student (`20000000001`), Assistant (`20000000003`), and Teacher (`20000000004`) using the password `password`, and dynamically make requests to all routes to check for authorization enforcement and verify that no unhandled server crashes (500 Internal Server Error) occur.

## Technical Context

**Language/Version**: Python 3.12 (Host) / .NET 9 (API)
**Primary Dependencies**: `requests` (Python library for HTTP requests)
**Storage**: PostgreSQL (Docker container `massar_db`)
**Testing**: Custom test harness `scratch/test_all_endpoints.py`
**Target Platform**: Local Docker Stack (API running at `http://localhost:5245`)
**Project Type**: REST API Verification Harness
**Performance Goals**: Test all 215 endpoints with multiple roles in less than 2 minutes.
**Constraints**: Must run against the live `massar_backend` container. Must not mutate database state destructively.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact across backend, frontend, worker, database, and Docker**:
  - *Backend*: None expected unless code changes are required to fix unhandled 500 errors.
  - *Frontend*: None.
  - *Worker*: None.
  - *Database*: Requires seeded test users. The E2E test users (`20000000000` through `20000000004`) are assumed to be seeded in the PostgreSQL container database.
  - *Docker*: Docker container stack must be up and healthy.
- **Automated tests required for the phase's critical paths**:
  - The custom python script `scratch/test_all_endpoints.py` itself acts as the automated regression and verification suite.
- **Manual QA flows required from the product owner**:
  - Review the generated Markdown report `specs/099-comprehensive-endpoint-testing/audit_report.md` to see the status code for each endpoint under different roles.
- **Docker gate commands**:
  - `docker compose ps` to verify all services are Up (healthy).
  - `curl -f http://localhost:5245/api/health` to verify API health.
- **Explicit decision**: The next phase (detailed task breakdown and implementation) cannot start until this plan is documented and reviewed.

## Project Structure

### Documentation (this feature)

```text
specs/099-comprehensive-endpoint-testing/
├── spec.md              # Feature specification
├── plan.md              # Implementation plan (this file)
├── tasks.md             # Detailed task list
└── audit_report.md      # Generated test results report
```

### Source Code (repository root)

```text
scratch/
└── test_all_endpoints.py # Custom python test harness
```

**Structure Decision**: The test harness script will be created inside the `scratch/` directory as `test_all_endpoints.py` to keep test scripts separate from production source code. The output audit report will be stored in the spec folder as `specs/099-comprehensive-endpoint-testing/audit_report.md`.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Run `python3 scratch/test_all_endpoints.py` to execute the full sweep of requests.

**Docker Gate Required**:
- `docker compose ps` shows all containers healthy.
- `curl -f http://localhost:5245/api/health` returns HTTP 200 with status "healthy".

**Manual QA Required**:
- Open `specs/099-comprehensive-endpoint-testing/audit_report.md` and verify that:
  - All 215 endpoints are listed.
  - No unexpected 500 status codes are reported.
  - Enforced routes return 401 or 403 status codes when called by unauthorized roles.

**End-of-Phase Report Format**:
- Summary of endpoints tested.
- Count of 500 crashes discovered and resolved (if any).
- Authentication validation overview (number of endpoints correctly enforcing authorization).
- List of open items or bugs discovered.

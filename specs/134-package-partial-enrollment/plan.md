# Implementation Plan: Package Partial Enrollment Display

## Technical Context
We will modify the backend's `GetPackagesQuery` to count a package as enrolled/activated if a student has an active access grant for the package itself, OR any of its child Terms, Sections, or Lessons.

## Constitution Check
We confirm that this plan adheres to the codebase's access control boundaries and database design conventions (e.g. avoiding N+1 queries by pre-fetching relations).

## Phase 0: Outline & Research
Research findings are stored in `research.md`. The design leverages C# memory cache and optimized Linq queries to check cascading access in a batch manner.

## Phase 1: Design & Contracts
- The updated schema entities and access check flow are detailed in `data-model.md`.
- No new external API endpoints or contract changes are introduced.
- Build and verification commands are listed in `quickstart.md`.

## Proposed Changes

### Backend (Content Features)

#### [MODIFY] [GetPackagesQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Content/Queries/GetPackagesQuery.cs)
- Update `GetPackagesQueryHandler` to optimize database checks for package enrollment.
- Instead of calling `_access.HasAccessToPackageAsync(...)` in a loop (which queries `StudentAccessGrants` per package):
  - Pre-fetch the list of user roles to determine if they are Admin/Teacher (global access).
  - Pre-fetch active `StudentAccessGrants` for the student once.
  - Pre-fetch mapping tables of Terms, Sections, and Lessons belonging to all retrieved packages.
  - Determine `isEnrolled` for each package in-memory by checking if any of its child entities have an active access grant.

---

### E2E Integration Tests

#### [MODIFY] [test_e2e_content_flow.py](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/test_e2e_content_flow.py)
- Extend the test scenario to check that the package appears as enrolled/accessible when only a child Term or Lesson is purchased/activated (prior to purchasing the package itself).

## Verification Plan

### Automated Tests
- Run Pytest: `.venv/bin/pytest tests/test_e2e_content_flow.py -v`
- Run frontend linter: `npm run lint` inside the `frontend/` directory.

### Manual Verification
- Launch the application and verify that when a student buys a lesson, section, or term inside a package, that package appears in "الباقات المفعّلة" on the student's packages list.

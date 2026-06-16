# Tasks: Package Partial Enrollment Display

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Implementation Tasks

### T001: Modify GetPackagesQuery.cs for Optimized Cascading Access Checks
- **Target File**: [GetPackagesQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Content/Queries/GetPackagesQuery.cs)
- **Task Description**: Update `GetPackagesQueryHandler` to determine package enrollment in-memory by pre-fetching the student's active grants, terms, sections, and lessons, and checking if any direct or child-level grants are active.
- **Verification**: Ensure the backend builds successfully.

### T002: Add E2E tests for partial package enrollment
- **Target File**: [test_e2e_content_flow.py](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/test_e2e_content_flow.py)
- **Task Description**: Add assertion checks verifying that the package appears as enrolled (`isEnrolled = True`) immediately after purchasing a single term or lesson, even before package-level purchase.
- **Verification**: Run `.venv/bin/pytest tests/test_e2e_content_flow.py -v`.

### T003: Deep Critique and Quality Checks
- [x] T003.1: Execute Phase 6 Deep Review and fix any architectural issues
- [x] T003.2: Execute `clean-code-guard` against changed C# backend file
- [x] T003.3: Execute `test-guard` against changed test file
- [x] T003.4: Execute E2E integration test suite to run the feature tests and verify everything passes successfully
- [x] T003.5: Run `npm run lint` and verify frontend builds cleanly

# Implementation Plan: E2E Test Flow for Course Content Creation and Access

**Branch**: `133-e2e-flow-subject-teacher-package` | **Date**: 2026-06-16 | **Spec**: [specs/133-e2e-flow-subject-teacher-package/spec.md](spec.md)
**Input**: Feature specification from `/specs/133-e2e-flow-subject-teacher-package/spec.md`

## Summary

Implement a comprehensive integration test in Python (`pytest`) inside `tests/test_e2e_content_flow.py` to cover the full life-cycle: admin creating subjects, teachers, yearly packages, terms (Month 1, Month 2), sections, lessons, videos, inline exams, and homework. Validate the student-side access checks for free vs paid lessons, balance adjustment, and packages purchasing.

## Technical Context

- **Language/Version**: Python 3.14 (Virtual Environment `.venv`)
- **Primary Dependencies**: `requests`, `pytest`
- **Storage**: PostgreSQL (via local Docker backend)
- **Testing**: `pytest`
- **Target Platform**: Local/Docker environment
- **Project Type**: E2E integration test suite
- **Performance Goals**: Total test execution under 5 seconds
- **Constraints**: Follow exact domain validation models (e.g. teacher specialization and subject mapping constraints)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Modifies the student Term Detail Page client component in the Next.js React frontend to prevent incorrect pricing fallbacks on free terms, and relies on ASP.NET Core backend APIs.
- **Automated tests**: A new test file `tests/test_e2e_content_flow.py` will be created.
- **Docker gate**: Verified by running `docker compose ps` and making sure the backend is reachable.
- **Exit criteria**: The new test file must pass all assertions successfully.

## Project Structure

### Documentation (this feature)

```text
specs/133-e2e-flow-subject-teacher-package/
├── spec.md              # Feature specification
├── plan.md              # Implementation plan (this file)
├── research.md          # Research findings
├── data-model.md        # Data model analysis
├── quickstart.md        # Quickstart instructions
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task breakdown
```

### Source Code (repository root)

```text
frontend/src/app/student/packages/[packageId]/terms/[termId]/
└── TermDetailPageClient.tsx  # [MODIFY] Correct free term pricing display/purchase

tests/
└── test_e2e_content_flow.py  # [NEW] The integration test file
```

**Structure Decision**: Add a new Python test file `tests/test_e2e_content_flow.py` inside the existing test suite and modify `TermDetailPageClient.tsx`.

## Proposed Changes

### Frontend UI

#### [MODIFY] [TermDetailPageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/packages/%5BpackageId%5D/terms/%5BtermId%5D/TermDetailPageClient.tsx)
- Replace all occurrences of `term?.price != null && term.price > 0` with `term != null` (or `term?.price != null`) to ensure that free terms (price = 0) do not fall back to displaying or purchasing the parent Package.
- Make sure that if the term is free, the price display shows "مجاني" and the purchase button opens the purchase modal with the correct "Term" content type, ID, and price (0 EGP).

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Run the newly created test file:
  ```bash
  .venv/bin/pytest tests/test_e2e_content_flow.py -v
  ```

**Docker Gate Required**:
- Ensure backend container is healthy and responding to `/api/health`.

**Manual QA Required**:
- None. This is an automated test suite feature.

# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Full Frontend API Contract Audit

**Input**: Design documents from `specs/101-full-frontend-api-contract-audit/`
**Prerequisites**: [plan.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/101-full-frontend-api-contract-audit/plan.md), [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/101-full-frontend-api-contract-audit/spec.md), [data-model.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/101-full-frontend-api-contract-audit/data-model.md), [contracts/frontend-backend-contract-inventory.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/101-full-frontend-api-contract-audit/contracts/frontend-backend-contract-inventory.md)

**Target prompt for implementation agent**: create the tasks file so that a cheaper llm model can implement without problems

## Phase 1: Setup & Inventory Baseline

- [x] T001 Run `node scripts/generate-endpoint-inventory.mjs` and record current backend endpoint count before editing [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs).
- [x] T002 Run `.venv/bin/python -m pytest tests/test_endpoint_inventory.py` and record baseline failures before editing [tests/test_endpoint_inventory.py](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/test_endpoint_inventory.py).
- [x] T003 Inspect all frontend API call patterns in [frontend/src/services](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services), [frontend/src/app/api](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/api), [frontend/src/components](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components), and [frontend/src/packages](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/packages) using `rg "apiClient\\.|fetch\\(|axios\\."`.

## Phase 2: Foundational Contract Parser Updates

- [x] T004 [P] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), add constants for frontend scan directories: `frontend/src/services`, `frontend/src/app/api`, `frontend/src/components`, and `frontend/src/packages`.
- [x] T005 [P] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), add a recursive TypeScript file collector that returns `.ts` and `.tsx` files while excluding generated artifacts and `node_modules`.
- [x] T006 [P] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), add route-template normalization helpers that convert frontend template literal `${...}` segments to `{param}` and strip literal query strings for path matching.
- [x] T007 [P] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), add backend route normalization that removes ASP.NET route constraints such as `{id:guid}` before comparisons.
- [x] T008 In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), keep existing backend endpoint output fields unchanged so existing tests and reports remain backward-compatible.

## Phase 3: User Story 1 - Frontend Service Requests Never Hit Missing Backend Routes (Priority: P1)

**Goal**: Every frontend-called backend method/path pair matches an active ASP.NET controller action.

**Independent Test**: `node scripts/generate-endpoint-inventory.mjs --check` and `.venv/bin/python -m pytest tests/test_endpoint_inventory.py` report zero missing frontend route findings.

- [x] T009 [P] [US1] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), implement `extractApiClientCalls(source, filePath)` to detect `apiClient.get/post/put/patch/delete(...)` calls and return `FrontendEndpointContract` objects.
- [x] T010 [P] [US1] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), implement extraction for `fetch(...)` calls that target `API_URL`, `API_BASE_URL`, or literal `/api/...` paths.
- [x] T011 [US1] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), classify frontend calls as `backend-api`, `next-api`, `worker-api`, or `external` and compare only `backend-api` calls against backend controller endpoints.
- [x] T012 [US1] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), add `routeFindings` and `missingFrontendRouteCount` to the JSON inventory when backend-api calls do not match a backend route.
- [x] T013 [US1] In [tests/test_endpoint_inventory.py](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/test_endpoint_inventory.py), add assertions that `frontendCallCount == len(frontendCalls)`, every frontend call has method/path/source/origin, and `missingFrontendRouteCount == 0`.
- [x] T014 [US1] Run `node scripts/generate-endpoint-inventory.mjs`; if it reports missing backend routes, fix the exact mismatched route in the corresponding frontend service or backend controller action.
- [x] T015 [US1] Run `node scripts/generate-endpoint-inventory.mjs --check` and `.venv/bin/python -m pytest tests/test_endpoint_inventory.py` after every route fix until route findings are zero.

## Phase 4: User Story 2 - Frontend Payload Fields Match Backend Request DTOs (Priority: P1)

**Goal**: Payload hints from frontend services are visible in the contract report and any discovered request DTO mismatch is fixed.

**Independent Test**: Review the generated frontend call section in `tests/endpoint_inventory.md` and run focused tests for changed endpoint families.

- [x] T016 [P] [US2] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), capture `payloadHint` from the second argument of `post`, `put`, and `patch` calls when the expression is statically visible.
- [x] T017 [P] [US2] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), capture literal query parameter names from strings containing `?name=` and Axios config objects containing `params`.
- [x] T018 [US2] Review generated payload hints for admin, assistant, HR, finance, CRM, media, teacher, student, forms, homework, community, exam, video-session, and auth service calls.
- [x] T019 [US2] For each concrete mismatch found in T018, patch the matching backend command/request DTO or frontend service payload in its exact file and add a finding item to `achievements.md` and this `tasks.md` before fixing it.

## Phase 5: User Story 3 - Frontend Response Types Match Backend Response DTOs (Priority: P2)

**Goal**: Response DTO surfaces are reviewed for frontend-declared fields and high-risk mismatches are fixed or documented.

**Independent Test**: Frontend lint/build passes and no audited response field mismatch remains open in `achievements.md`.

- [x] T020 [P] [US3] Review TypeScript DTOs in [frontend/src/services/admin-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/admin-service.ts), [frontend/src/services/assistant-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/assistant-service.ts), and [frontend/src/services/content-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/content-service.ts) against their backend response DTOs.
- [x] T021 [P] [US3] Review TypeScript DTOs in [frontend/src/services/hr-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/hr-service.ts), [frontend/src/services/finance-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/finance-service.ts), [frontend/src/services/crm-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/crm-service.ts), [frontend/src/services/media-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/media-service.ts), and [frontend/src/services/teacher-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/teacher-service.ts) against their backend response DTOs.
- [x] T022 [US3] For each concrete response field mismatch found in T020 or T021, patch the smallest backend response DTO or frontend service type and add a finding item to `achievements.md` and this `tasks.md` before fixing it.

## Implementation Findings

- [x] Frontend response envelope mismatch: replace `isSuccess` checks/types with backend-aligned `success` in HR and code activation frontend call sites.
- [x] Frontend build blocked by generated-cache ENOSPC while writing `frontend/.next/cache/.tsbuildinfo`; cleared generated build artifacts and reran `npm run build` successfully.
- [x] Endpoint inventory parser false positives: normalize dynamic query suffixes and parse class attributes containing `[controller]`, then regenerate inventory with zero missing route findings.
- [x] General `git diff --check` has unrelated pre-existing whitespace; scoped diff check for this feature's changed files passes cleanly.
- [x] Clean-code guard finding: remove redundant `statSync` filtering from the endpoint inventory frontend collector.

## Phase 6: User Story 4 - Contract Drift Becomes Detectable Before It Reaches Users (Priority: P2)

**Goal**: The inventory Markdown and JSON make future drift obvious and testable.

**Independent Test**: `tests/endpoint_inventory.md` contains backend endpoints, frontend backend calls, and a clean route findings section.

- [x] T023 [US4] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), update Markdown rendering to include a grouped frontend-call table with method, path, origin, query parameters, payload hint, and source.
- [x] T024 [US4] In [scripts/generate-endpoint-inventory.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/generate-endpoint-inventory.mjs), add a route findings Markdown section that prints exact missing route file/line details or the clean message.
- [x] T025 [US4] In [tests/test_endpoint_inventory.py](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/test_endpoint_inventory.py), add a test that confirms the Markdown report contains the frontend-call and route-finding sections.
- [x] T026 [US4] Regenerate [tests/endpoint_inventory.json](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/endpoint_inventory.json) and [tests/endpoint_inventory.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/endpoint_inventory.md) with the updated script.

## Phase 7: Verification, Critique, and Quality Gates

- [x] T027 Run `.venv/bin/python -m pytest tests/test_endpoint_inventory.py` and fix every failure before continuing.
- [x] T028 Run `node scripts/generate-endpoint-inventory.mjs --check` and fix stale inventory failures before continuing.
- [x] T029 Run `dotnet build backend/NaderGorge.sln`; record and fix build warnings/errors introduced by this feature.
- [x] T030 Run `cd frontend && npm run lint && npm run build`; record and fix lint/build warnings/errors introduced by this feature.
- [x] T031 Run `docker compose config -q` and `docker compose ps`; record any environment blocker without changing unrelated Docker files.
- [x] T032 Conduct the mandatory deep architectural, code, and UI/UX critique, record every finding in `achievements.md` and this `tasks.md`, fix each finding, and check it off only after verification.
- [x] T033 Run `clean-code-guard` first against changed production-code files, record every finding in `achievements.md` and this `tasks.md`, fix each finding, and check it off only after verification.
- [x] T034 Run `test-guard` second against changed test files; if no test findings remain, record the result in `achievements.md`.
- [x] T035 Verify every checkbox in `achievements.md` is checked before the final report.

## Dependencies & Execution Order

- Setup tasks T001-T003 must complete before parser updates.
- Foundational parser updates T004-T008 must complete before user story work.
- US1 route matching T009-T015 is the MVP and blocks any claim that frontend routes are aligned.
- US2 payload review T016-T019 can start after T012 emits payload hints.
- US3 response review T020-T022 can run in parallel with US2 after US1 is green.
- US4 reporting tasks T023-T026 complete the repeatable guard.
- Quality gates must run in this order: deep critique, `clean-code-guard`, `test-guard`, final verification.

## Parallel Opportunities

- T004-T007 can be implemented in parallel because they touch separate helpers inside the same script but must be merged carefully.
- T020 and T021 can be reviewed in parallel because they cover different frontend service groups.
- Backend/frontend build commands can run after implementation if no source edits are in progress.

## Implementation Strategy

1. Ship the repeatable route-drift audit first.
2. Fix only concrete missing routes or frontend service route errors found by the audit.
3. Use payload and response hints to guide targeted manual DTO reviews.
4. Avoid broad redesign or schema churn unless an existing frontend flow proves the backend contract is missing a field.

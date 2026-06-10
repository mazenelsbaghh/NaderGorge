# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: 116-performance-deep-remediation

**Input**: Design documents from `/specs/116-performance-deep-remediation/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

- [x] T001 Initialize performance audit remediation branch and verify active workspace path

---

## Phase 2: Foundational (Blocking Prerequisites)

- [x] T002 In `backend/src/NaderGorge.API/Program.cs`, add Brotli response compression:
  - Add `builder.Services.AddResponseCompression(options => { options.EnableForHttps = true; });`
  - Add `app.UseResponseCompression();`
- [x] T003 In `backend/src/NaderGorge.API/Program.cs`, configure output cache middleware:
  - Add `builder.Services.AddOutputCache();`
  - Add `app.UseOutputCache();`
  - Apply output caching policies to public GET endpoints in controller classes.

---

## Phase 3: User Story 1 - Fast Public Page Loading (Priority: P1)

**Goal**: Make the root layout static and optimize fonts/weights so public pages load in under 250ms TTFB.

**Independent Test**: Build the frontend (`npm run build`) and check the routes map to verify `/`, `/about`, `/faq`, `/login`, and `/register` are static (`○`).

### Implementation for User Story 1

- [x] T010 [P] In `frontend/src/app/layout.tsx`, remove `export const dynamic = 'force-dynamic'` and the `headers()` call.
- [x] T011 [P] In `frontend/src/app/layout.tsx`, reduce font weights for Tajawal to `["400", "500", "700", "800"]` and Montserrat to `["500", "700"]`.
- [x] T012 In `frontend/src/app/layout.tsx`, add an inline script in `<head>` that determines the surface based on `window.location.host` and sets the `data-massar-surface` attribute dynamically to prevent FOUC while keeping the root layout 100% static.
- [x] T013 Convert public pages `/about`, `/faq` to server components if they use `"use client"` unnecessarily.

---

## Phase 4: User Story 2 - Efficient Student Shell & Navigation (Priority: P1)

**Goal**: Implement a unified student shell bootstrap endpoint and refactor client components to prevent waterfalls and unnecessary navigation-triggered API fetches.

**Independent Test**: Navigate student sections and observe only one single bootstrap request is made upon layout mount, with zero requests on subsequent navigation.

### Implementation for User Story 2

- [x] T020 In `frontend/src/app/template.tsx`, remove the global `framer-motion` page transition wrapper or replace it with a simple CSS transition.
- [x] T021 [NEW] Create `backend/src/NaderGorge.Application/Features/Student/Queries/GetShellBootstrapQuery.cs` (MediatR query returning notifications count, balance, gamification points/streak, and theme/avatar basics).
- [x] T022 In `backend/src/NaderGorge.API/Controllers/StudentController.cs`, expose the `GET api/student/shell-bootstrap` endpoint.
- [x] T023 [NEW] Implement the handler for `GetShellBootstrapQuery` using direct projections and `AsNoTracking` to select all bootstrap fields in a single SQL query.
- [x] T024 In `frontend/src/services/student-service.ts`, implement the `getShellBootstrap()` Axios call.
- [x] T025 Refactor `frontend/src/components/layout/StudentShellChrome.tsx`, `SidebarBalance.tsx`, and `SidebarGamification.tsx` to retrieve shell data using `getShellBootstrap()` once on mount. Prevent re-fetching on page navigation.

---

## Phase 5: User Story 3 - Rapid Backend Queries (Priority: P1)

**Goal**: Optimize backend query execution plans and database projections to resolve slow responses.

**Independent Test**: Verify that `/api/student/dashboard` and `/api/admin/code-groups` execute in under 250ms with large seeded datasets.

### Implementation for User Story 3

- [x] T030 In `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs`, rewrite the handler to use `AsNoTracking`, projection `.Select()` to DTOs, and count queries instead of deep entity includes.
- [x] T031 In `backend/src/NaderGorge.Application/Features/Admin/Queries/ListCodeGroupsQuery.cs`, rewrite the handler to project the code count using `.Select(cg => new CodeGroupDto { CodeCount = cg.AccessCodes.Count(), UsedCount = cg.AccessCodes.Count(c => c.IsConsumed) })` instead of `.Include(cg => cg.AccessCodes).`
- [x] T032 In `backend/src/NaderGorge.Application/Features/Student/Queries/GetMistakesQuery.cs`, add pagination parameters (skip/take) and project directly to DTO.
- [x] T033 In `backend/src/NaderGorge.Application/Features/Student/Queries/GetProgressQuery.cs`, refactor to use flat query projections.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Goal**: Optimize SVG assets and perform dev environment cleanup.

- [x] T040 Optimize SVG logo files (`frontend/public/images/logo.svg`, `logo-mark.svg`, `logo-mark-light.svg`) by removing embedded base64 rasters/metadata.
- [x] T041 Run `rm -rf frontend/.next` and test build performance.
- [x] T042 Update the master plan documents (`docs/backend_plan.md`, `docs/frontend_plan.md`, `docs/ops_plan.md`, `docs/ui_ux_plan.md`) with all updates and dates.

---

## Phase 7: Quality-Gate Tail Tasks

**Goal**: Verify all code quality, tests, and build standards are fully satisfied.

- [x] T050 Run `clean-code-guard` against all modified production code files.
- [x] T051 Run `test-guard` to review changed or related test suites.
- [x] T052 Compile and build the entire workspace (`dotnet build backend/NaderGorge.sln` and `npm run build` in `frontend/`) to ensure a warning-free build.
- [x] T053 Verify that `docker compose config -q` passes without errors.

---

## Phase 8: End-of-Phase Verification, Docker Gate & Manual QA Report

**Goal**: Final operational check and verification report generation.

- [x] T060 Run manual QA on the optimized routes.
- [x] T061 Create the end-of-phase report and mark achievements as complete.

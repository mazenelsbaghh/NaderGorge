# Tasks: Docker/Domain Isolation and Role Permissions

**Input**: Design documents from `specs/113-docker-domain-isolation-role-permissions/`
**Prerequisites**: plan.md (required), spec.md (required), research.md (optional), data-model.md (optional)

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Configure environment variables for the domain `massar-academy.net` in `frontend/.env.production` (or `frontend/.env.local`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core configurations and environment settings that must be complete before any user story can be implemented.

- [ ] T002 Update `docker-compose.yml` to support distinct hostnames and port mappings for the 5 surfaces (student, admin, teacher, assistant, landing).
- [ ] T003 Clean up AllowedOrigins inside `backend/src/NaderGorge.API/appsettings.json`, `appsettings.Development.json`, and `appsettings.E2e.json` to only permit requests from `*.massar-academy.net` and subdomain localhost ports.

---

## Phase 3: User Story 1 - Environment and Port Isolation (Priority: P1)

**Goal**: Each subdomain runs on its own port and env in Docker.

**Independent Test**: Start the docker compose containers and verify each surface loads on its respective subdomain and port.

- [ ] T004 [P] [US1] Update `docker/nginx/massar.conf` to clean up and legacy-redirect old domains, keeping only `massar-academy.net` subdomains active.
- [ ] T005 [P] [US1] Import and use `getSurfaceName()` instead of `process.env.NEXT_PUBLIC_APP_SURFACE` in `frontend/src/components/forms/LoginForm.tsx` to dynamically detect the surface name.

---

## Phase 4: User Story 2 - Cross-Surface Access Boundaries (Priority: P1)

**Goal**: Block cross-surface page requests on subdomains and show custom 404.

**Independent Test**: Navigate to `http://app.localhost:3000/admin` while logged in as a student, and verify that the custom 404 page is displayed.

- [ ] T006 [P] [US2] Update cross-surface assertions in `frontend/tests/e2e/auth.spec.ts` and `frontend/tests/e2e/student-journey.spec.ts` to assert that Student cannot access Admin, Teacher, or Assistant pages.
- [ ] T007 [P] [US2] Update verification script `scripts/verify-surface-separation.mjs` to include checks asserting that accessing wrong-surface routes on subdomains returns 404.

---

## Phase 5: User Story 3 - Role-Based Permission Enforcement (Priority: P1)

**Goal**: Enforce granular role authorization and teacher bindings in tests.

**Independent Test**: Run Playwright tests and verify Assistant permissions and Teacher bindings are validated.

- [ ] T008 [P] [US3] Implement CRM lead status change checks and permission matrix verification in `frontend/tests/e2e/assistant-dashboard.spec.ts`.
- [ ] T009 [P] [US3] Add Teacher binding verification tests in `frontend/tests/e2e/admin-content.spec.ts` to ensure teachers can only access lessons they are bound to.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup and quality checks before final verification.

- [ ] T010 Update `.env.example` in the repository root to document new `massar-academy.net` environment variables.
- [ ] T011 Run `clean-code-guard` on all modified and new files.
- [ ] T012 Run `test-guard` on all E2E test files.

---

## Phase 7: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment.

- [ ] T013 Run Next.js lint: `cd frontend && npm run lint`
- [ ] T014 Run Next.js build: `cd frontend && npm run build`
- [ ] T015 Run Playwright E2E tests: `npx playwright test`
- [ ] T016 Run surface verification script: `node scripts/verify-surface-separation.mjs --static-only`
- [ ] T017 Run Docker Compose configuration check: `docker compose config -q`
- [ ] T018 Compile final walkthrough report in `specs/113-docker-domain-isolation-role-permissions/walkthrough.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion.
- **User Stories (Phases 3-5)**: Depend on Foundational phase completion.
- **Polish (Phase 6)**: Depends on Phases 3-5.
- **Verification (Phase 7)**: Depends on all previous phases.

### Parallel Opportunities

- Tasks marked with `[P]` can be developed concurrently once their phase prerequisites are met.

---

## Implementation Strategy

### MVP First (User Story 1 & 2)
1. Complete Setup and Foundational routing.
2. Verify subdomain access routing and cross-surface boundaries block students from admin/teacher views.
3. Test locally and verify.

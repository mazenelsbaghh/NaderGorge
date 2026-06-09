# Tasks: Platform Guards, Permissions, and Multi-Domain Routing Isolation

# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed and verified
- [x] Phase 2: Technical Planning (`speckit-plan`) completed and verified
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed and verified

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

---

## Phase 1: Foundational (Docker, Nginx & Backend Setup)

**Purpose**: Establish the multi-domain routing endpoints, CORS origins, and Docker services required to separate the 5 surfaces.

- [ ] T001 [P] [US1] In `docker-compose.yml`, add the `teacher` and `assistant` services, map ports `8741` and `8742` to internal port `8738`, configure the `APP_SURFACE` / `NEXT_PUBLIC_APP_SURFACE` variables, and update the `nginx` service's `depends_on` block.
- [ ] T002 [P] [US1] In `docker/nginx/massar.conf`, separate the virtual host routing blocks: route `teacher.massaracademy.com` / `teacher.massarplatform.com` / `teacher.bsma-academy.com` to `http://teacher:8738`; route `staff.massaracademy.com` / `staff.massarplatform.com` / `staff.bsma-academy.com` to `http://assistant:8738`; keep other administrative domains pointing to `http://admin:8738`.
- [ ] T003 [P] [US2] In `backend/src/NaderGorge.API/Program.cs`, add the new teacher (`https://teacher.massarplatform.com`, `https://teacher.massaracademy.com`, `https://teacher.bsma-academy.com`) and assistant (`https://staff.massarplatform.com`, `https://staff.massaracademy.com`, `https://staff.bsma-academy.com`) domains (both HTTP and HTTPS variants, and localhost:8741 / localhost:8742) to the backend `CORS` origins policy.

**Checkpoint**: Foundation ready - Docker, Nginx, and backend are updated to route and allow requests from all 5 surfaces.

---

## Phase 2: User Story 1 - Surface Boundary Enforcement

**Goal**: Update routing and Next.js layout boundary logic to enforce 5 surfaces and block unauthorized role combinations.

- [ ] T004 [P] [US1] In `frontend/src/packages/surface-runtime/config.ts`, extend the `SurfaceName` type union to include `teacher` and `assistant`.
- [ ] T005 [P] [US1] In `frontend/src/packages/surface-runtime/config.ts`, update the `SurfaceOrigins` interface and returned local/production origins in `getSurfaceOrigins()` to support `teacher` and `assistant`.
- [ ] T006 [US1] In `frontend/src/packages/surface-runtime/config.ts`, update `getRouteBoundaryDecision` with the symmetric path evaluation and redirect URLs for `landing`, `student`, `admin`, `teacher`, and `assistant` surfaces.
- [ ] T007 [US1] In `frontend/src/proxy.ts`, update the routing logic to support redirects for the new subdomains and ports in `all` mode.
- [ ] T008 [P] [US1] Create `frontend/src/components/layout/TeacherGuard.tsx` to restrict access strictly to users holding the `Teacher` role.
- [ ] T009 [P] [US1] Create `frontend/src/components/layout/AssistantGuard.tsx` to restrict access strictly to users holding the `Assistant` or `Staff` roles.
- [ ] T010 [P] [US1] Create `frontend/src/components/layout/StaffGuard.tsx` to restrict access strictly to users holding the `Staff` role.
- [ ] T011 [US1] In `frontend/src/components/layout/StudentGuard.tsx`, update the verification to check if the user roles array includes the `Student` role (allow bypass for `Admin` if in preview mode).
- [ ] T012 [US1] In `frontend/src/components/layout/AdminGuard.tsx`, restrict the access check so that only `Admin` and `Supervisor` (possessing correct permissions) are permitted.

**Checkpoint**: Layout guards and routing boundaries are fully operational on the frontend.

---

## Phase 3: User Story 2 - strict Permission Evaluation

**Goal**: Tighten the authorization rules on both frontend and backend so only the `Admin` role can bypass permission checks.

- [ ] T013 [P] [US2] In `frontend/src/hooks/useHasPermission.ts`, update `useHasPermission` to remove `Teacher` from the automatic bypass line, allowing only `Admin` to bypass permission checks.
- [ ] T014 [P] [US2] In `backend/src/NaderGorge.API/Extensions/HasPermissionAttribute.cs`, update the `PermissionFilter.OnAuthorizationAsync` method to bypass authorization check exclusively for users with the `Admin` role (Teachers and Supervisors must be evaluated against their explicit permission claims).

**Checkpoint**: Non-Admin roles can no longer bypass permission checks.

---

## Phase 4: User Story 3 - Role-Based Login Redirects

**Goal**: Automatically direct the user to their designated surface domain/port based on their user role immediately after authentication.

- [ ] T015 [US3] In `frontend/src/components/forms/LoginForm.tsx`, update the redirection logic after successful login:
  - Redirect users with `Student` role to `origins.student/student`.
  - Redirect users with `Teacher` role to `origins.teacher/teacher`.
  - Redirect users with `Assistant` or `Staff` roles to `origins.assistant/assistant`.
  - Redirect users with `Admin` or `Supervisor` roles to `origins.admin/admin`.
  - Make sure that if `returnUrl` is present, it validates that it belongs to the allowed domain boundary of the role.

**Checkpoint**: Authentication flows seamlessly route users to their respective domains.

---

## Phase 5: Verification & Verification Scripts

**Goal**: Update validation scripts and run tests to ensure no regressions occur.

- [ ] T016 [US1] In `scripts/verify-surface-separation.mjs`, expand the domain checks, redirects, and forbidden headers validation to cover all 5 surfaces (`landing`, `student`, `admin`, `teacher`, `assistant`).
- [ ] T017 Run `npm test` and `npm run lint` in the `frontend/` directory to verify there are no compilation warnings or errors.
- [ ] T018 Run `pytest tests/` from the repository root to verify that E2E integration and login suites pass.

---

## Phase 6: Mandatory Quality Gates

**Purpose**: Execute clean-code-guard and test-guard to ensure production quality.

- [ ] T019 Execute `clean-code-guard` against all modified/created files.
- [ ] T020 Resolve all findings from the clean-code-guard review.
- [ ] T021 Execute `test-guard` against all changed test surfaces.
- [ ] T022 Resolve all findings from the test-guard review.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: Must complete first.
- **Phase 2, 3, 4 (Guards, Permissions, Routing)**: Depends on Phase 1.
- **Phase 5 (Verification)**: Run after implementation completes.
- **Phase 6 (Quality Gates)**: Final validation before report.

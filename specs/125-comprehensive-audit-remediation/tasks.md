# Tasks: Comprehensive Audit Remediation

**Input**: Design documents from `/specs/125-comprehensive-audit-remediation/`
**Prerequisites**: plan.md (required), spec.md (required), data-model.md, contracts/

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Setup & Repository Hygiene

**Purpose**: Clean up tracked files in the repository and add CI security scanners.

- [x] T001 [P] Configure `.gitignore` to explicitly ignore SQL dumps, backup files, and local `.env` files.
- [x] T002 [P] Run git security cleanup to remove any accidentally tracked database dumps or credentials from the active git tree.
- [x] T003 [P] Create validation script `scripts/verify-no-sensitive-tracked-files.mjs` to check for sensitive extensions and credentials in the git tree as part of the CI run.

**Checkpoint**: Git repository is clean and verification script passes locally.

---

## Phase 2: Foundational & Database Migrations (US1 & US2 & US3)

**Purpose**: Update database models and execute EF Core migrations for session, homework, and warning schemas.

- [x] T004 Modify `User.cs` in `backend/src/NaderGorge.Domain/Entities/User.cs` to add `PasswordResetVersion` (default 0).
- [x] T005 Modify `HomeworkSubmission.cs` and add a unique constraint configuration on `(HomeworkId, StudentId)` in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T006 Modify `WarningEvent.cs` in `backend/src/NaderGorge.Domain/Entities/WarningEvent.cs` to add unique index configuration on `OccurrenceKey` field.
- [x] T007 Run C# database migrations scaffold command `dotnet ef migrations add ComprehensiveAuditRemediation --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API`.

**Checkpoint**: Database migrations are generated and C# backend compiles cleanly.

---

## Phase 3: User Story 1 - Contain Critical Security and Financial Risk (Priority: P1)

**Goal**: Make teacher code access read-only, remove teacher code generation, and require explicit administrative permissions.

- [ ] T008 [US1] In `backend/src/NaderGorge.Application/Features/Admin/Commands/BulkGenerateCodesCommand.cs`, add explicit permission check for `codes.manage` inside the handler logic.
- [ ] T009 [US1] Remove generation route handling from `TeacherController.cs` (specifically `POST /api/teacher/codes/bulk-generate`).
- [ ] T010 [US1] Scope code group queries in the backend by teacher identifier so teachers only see code groups assigned to them.
- [ ] T011 [US1] Update teacher dashboard frontend component `frontend/src/app/teacher/codes/TeacherCodesPageClient.tsx` to remove all bulk-generation action buttons, dialogs, and mutation handlers.

**Checkpoint**: Teacher dashboard shows code groups in read-only mode, and backend blocks unauthorized generation.

---

## Phase 4: User Story 2 - Enforce Academic and Session Authorization (Priority: P1)

**Goal**: Protect manual locks, entitlement verification, single-use refresh token rotation, and logouts.

- [ ] T012 [P] [US2] Restrict manual lesson unlock endpoint `POST /api/exams/admin/lessons/{lessonId}/students/{studentId}/unlock` in `ExamsController.cs` to check for `watch_requests.manage` permission.
- [ ] T013 [P] [US2] Implement `HasAccessToExamAsync` check inside exam startup command handler in `backend/src/NaderGorge.Application/Features/Content/` resolving lesson, video, and package entitlement.
- [ ] T014 [P] [US2] Restrict homework submission handler `SubmitHomeworkCommandHandler.cs` to check if the caller has active access to the owning lesson.
- [ ] T015 [US2] Update refresh token database updates to conditionally revoke exactly one row and fail rotation if token is replayed.
- [ ] T016 [US2] Implement `POST /api/auth/logout` endpoint in `AuthController.cs` to revoke active refresh session and clear the HttpOnly cookie.
- [ ] T017 [US2] Enforce PasswordResetVersion validation on reset commands, invalidating active refresh sessions and replayed tokens.
- [ ] T018 [US2] Update frontend `frontend/src/stores/auth-store.ts` to implement a shared in-flight refresh promise, preventing multiple simultaneous requests.
- [ ] T019 [US2] Update frontend shell logout handler to query the server logout endpoint first and perform cleanup in a `finally` block.

**Checkpoint**: Session rotation, logout, and cross-resource access checks pass automated validation.

---

## Phase 5: User Story 3 - Preserve Financial and Academic Data Integrity (Priority: P1)

**Goal**: Atomic transactions, idempotent submissions, consistent video metrics, and safe notification warnings.

- [ ] T020 [US3] Wrap balance adjustment and ledger insertions in an EF Core transaction to perform atomic updates.
- [ ] T021 [US3] Update homework submission handler to catch unique index constraint violations and return the existing submission, ensuring idempotency.
- [ ] T022 [US3] Implement watch sync metrics validation in `SyncVideoProgressCommandHandler.cs` to verify threshold transitions and lock states.
- [ ] T023 [US3] Construct deterministic `OccurrenceKey` in the commitment sweep worker before saving warnings.
- [ ] T024 [US3] Update worker notification providers in `worker/src/providers/` to return an explicit failure when provider settings are absent.

**Checkpoint**: Concurrent balance updates, sync video counts, and homework tests return consistent states.

---

## Phase 6: User Story 4 - Make Background Processing Recoverable (Priority: P1)

**Goal**: Move backend-to-worker handoff to Redis Streams, establish BullMQ retry policies, and implement readiness checks.

- [ ] T025 [US4] Implement backend Redis Stream producer in program startup and outbox publisher.
- [ ] T026 [US4] Implement worker Redis Stream consumer to ingest and acknowledge jobs only after ownership transfers to BullMQ.
- [ ] T027 [US4] Configure BullMQ options in worker queue setup with bounded retry attempts and exponential backoff.
- [ ] T028 [US4] Update backend completion callback endpoints to check for terminal state before applying updates.
- [ ] T029 [US4] Expose `GET /api/health/ready` endpoint in backend verifying PostgreSQL and Redis status.
- [ ] T030 [US4] Expose `GET /ready` endpoint in Node.js worker checking DB, Redis, and queue liveness.

**Checkpoint**: Simulated worker crashes and readiness probes run successfully.

---

## Phase 7: User Story 5 - Deploy Only Verified, Reproducible Releases (Priority: P1)

**Goal**: Bind internal host ports to loopback, pin images, and upgrade vulnerable dependencies.

- [ ] T031 [US5] Update production Docker Compose files `docker-compose.yml` to remove public port bindings for DB, Redis, and internal APIs.
- [ ] T032 [US5] Pin third-party base images and Telegram Bot API versions in Dockerfiles.
- [ ] T033 [US5] Upgrade the `MessagePack` package in `backend/` to resolve high-severity vulnerability warnings.

**Checkpoint**: Production Compose builds and exposes only Nginx publicly.

---

## Phase 8: User Story 6 - Deliver Secure, Consistent Frontend Behavior (Priority: P2)

**Goal**: Frontend HTML sanitization, accessible dialog wrapper, and responsive mobile calendar layout.

- [ ] T034 [US6] Implement HTML sanitizer utility in frontend and wrap homework essay response displays.
- [ ] T035 [US6] Align profile completion page fields to submit backend-compatible District and SchoolName fields.
- [ ] T036 [US6] Create accessible dialog container component supporting Tab trap focus and Escape closure.
- [ ] T037 [US6] Update the social calendar component to switch to agenda list view under mobile width breakpoints (<768px).
- [ ] T038 [US6] Replace CSS backgrounds in hero cards with Next.js optimized images.
- [ ] T039 [US6] Optimize SignalR connection room switching in chat hook to reuse the active connection context.

**Checkpoint**: Keyboard navigation inside dialogs and mobile browser layout previews compile successfully.

---

## Phase 9: User Story 7 - Establish Maintainable Quality Gates (Priority: P2)

**Goal**: Setup automated linting and dependency override rules.

- [ ] T040 [US7] Configure package package-lock overrides to suppress high-priority audit warnings in `frontend/` and `worker/`.

**Checkpoint**: `npm audit` returns zero high-severity errors on client and worker packages.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: General cleanup, refactoring, and documentation updates.

- [ ] T041 Run production builds and ensure zero compilation warnings in backend, frontend, and worker.
- [ ] T042 Update docs/ files to document final audit remediations.

---

## Phase 11: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Validate code quality gates and verify system-wide compilation, Compose stack, and manual checks.

- [ ] T043 Run backend test commands: `dotnet test backend/NaderGorge.sln --no-restore` and verify all tests pass.
- [ ] T044 Run frontend verification: `cd frontend && npm run lint && npm run build` and check for errors.
- [ ] T045 Run worker verification tests: `cd worker && npm test`.
- [ ] T046 Run Docker gate verification: `docker compose config -q` and build/verify readiness endpoints.
- [ ] T047 Perform manual QA checklist for teacher read-only codes, session expiration, and mobile calendar layout.
- [ ] T048 Write end-of-phase walk-through report.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion. Blocks user stories.
- **User Stories (Phases 3 to 9)**: Depend on Foundational completion.
- **Polish (Phase 10)**: Depends on all user stories completion.
- **End-of-Phase Verification (Phase 11)**: Final gate checking all implementation and quality gate tests.

### Quality Gate Tail Tasks

- **Clean Code Guard Gate**: Execute `clean-code-guard` against all modified production code files. Ensure all issues are resolved.
- **Test Guard Gate**: Execute `test-guard` against all modified test code files. Ensure all test cases conform to testing rules.

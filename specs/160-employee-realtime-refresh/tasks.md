# Tasks: Employee Workflows and Realtime Refresh

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md` in this directory.
**Execution prompt**: create the tasks file so that a cheaper llm model can implement without problems.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in `specs/160-employee-realtime-refresh/spec.md`.
- [x] Phase 2: Arabic Clarification (`speckit-clarify`) completed and recorded in `spec.md`.
- [x] Phase 3: Technical Planning (`speckit-plan`) completed in `specs/160-employee-realtime-refresh/plan.md`.
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`) generated from the approved design.

## Phase 1: Setup and baseline inventory

- [x] T001 [P] Create `docs/data-refresh-inventory.md` with columns for service file, mutation function, endpoint, domain, query keys, current cache, current post-mutation action, SignalR event, and verification command.
- [x] T002 [P] Create `docs/employee-workflow-bug-matrix.md` with reproducible scenarios for employee create/edit/disable, role/permission changes, attendance, vacation, payroll, assignments, lookups, navbar, and protected-route access.
- [x] T003 [P] Create `frontend/src/lib/query-contracts.ts` with `DataDomain`, `DataScope`, `MutationOperation`, and a typed contract record; reject an entry without owner, affected scopes, and update/invalidation behavior.
- [x] T004 Enumerate all mutation calls in `frontend/src/services/*.ts`, page clients, and components into the inventory and record the exact count and files in `docs/data-refresh-inventory.md`.
- [x] T005 Enumerate module-level caches, TTLs, in-flight promises, `force: true`, `router.refresh`, and `window.location.reload` occurrences; record the classification and proposed replacement in the two baseline documents.
- [x] T006 Run `rg -n "axios\\.(post|put|patch|delete)|\\.(post|put|patch|delete)\\(" frontend/src/services frontend/src/app frontend/src/components` and record the command output summary in `achievements.md`.

## Phase 2: Foundational contracts and infrastructure

- [x] T007 [P] Create `backend/src/NaderGorge.Application/Features/Auth/Queries/GetCurrentSessionQuery.cs` returning `UserDto`, `authorizationVersion`, and UTC `serverTime` without refresh-token rotation.
- [x] T008 [P] Add `GET /api/auth/session` to `backend/src/NaderGorge.API/Controllers/AuthController.cs`, requiring the existing authenticated user and returning the project `ApiResponse` envelope.
- [x] T009 Update `backend/src/NaderGorge.Application/Features/Auth/Commands/LoginCommand.cs` and `RefreshTokenCommand.cs` so `UserDto` includes the same session fields and an integer `AuthorizationVersion` consistently.
- [x] T010 Update `frontend/src/services/auth-service.ts` with a typed `getCurrentSession()` method for `/auth/session` and a response type matching `contracts/current-session.md`.
- [x] T011 Update `frontend/src/stores/auth-store.ts` with `authorizationVersion`, `replaceSessionSnapshot`, and `refreshCurrentSession`; persist the returned user snapshot without persisting secrets.
- [x] T012 Update `frontend/src/hooks/useCurrentSession.ts` and `frontend/src/components/layout/AuthBootstrap.tsx` to load the current session after browser bootstrap and expose loading/error state without a document reload.
- [x] T013 [P] Create `frontend/src/lib/query-keys.ts` with hierarchical keys for session, employees, HR, operations, CRM, content, finance, assessments, community, notifications, and forms.
- [x] T014 [P] Create `frontend/src/lib/realtime-invalidation-map.ts` mapping every backend scope to exact query-key factories and session refresh behavior; include all-match semantics for overlapping keys.
- [x] T015 Choose TanStack Query only after `cd frontend && npm install`/lockfile review and `npm run typecheck`; dependency is absent from the current lockfile, so the typed registry fallback is used for this slice.
- [x] T016 TanStack Query was not accepted for this slice because it is absent from the existing lockfile; the typed fallback registry remains the single server-state adapter.
- [x] T017 The fallback registry provides debounced all-match invalidation, active registrations, and refetch metrics; TanStack-specific provider defaults are not applicable.
- [x] T018 Update `frontend/src/lib/cache-invalidation.ts` fallback/adapter so `invalidate()` invokes every matching store instead of returning after the first prefix and tracks active/inactive registrations.
- [x] T019 Create `frontend/src/lib/data-changed-event.ts` with legacy-compatible parsing and strict validation for `schemaVersion`, `eventId`, `occurredAt`, scopes, operation, and entity IDs.
- [x] T020 Create `backend/src/NaderGorge.Domain/Events/DataChangedEvent.cs` (or the existing domain event location) with the envelope fields from `contracts/staff-data-changed.md` and allowlisted scope/operation values.
- [x] T021 Update `backend/src/NaderGorge.Infrastructure/Data/StaffRealtimeChangeDetector.cs` to derive operation, entity type, IDs, and a stable event ID once per outbox row while retaining legacy `scopes`.
- [x] T022 Update `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs` only where required to dispatch the unchanged JSON payload to `Role_Staff`; preserve retry/idempotency behavior and unauthorized-target rejection.
- [x] T023 Create `frontend/src/lib/query-contracts.test.ts` or the project’s existing frontend contract-test file to verify key uniqueness, scope mappings, all-match invalidation, and legacy event parsing.
- [x] T024 Create backend tests in `backend/tests/NaderGorge.Application.Tests/StaffRealtimeOutboxTests.cs` for Added/Modified/Deleted entities, event metadata, stable retry ID, target group, and no sensitive payload fields.
- [x] T025 Run `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~StaffRealtime|FullyQualifiedName~Outbox"` and `cd frontend && npm run typecheck`; expected result is all focused tests pass before story work begins.

## Phase 3: User Story 1 — Employee and permission consistency (P1 MVP)

**Goal**: Employee mutations and authorization changes update the current and other connected sessions without reload, while backend denial and draft conflict remain safe.

**Independent Test**: Playwright session A creates/edits/disables an employee; session A sees list/lookups/profile update, session B receives the change, a revoked session loses navbar/route access, and a dirty edit form retains its draft.

- [x] T026 [P] [US1] Write `backend/tests/NaderGorge.Application.Tests/Auth/AuthCurrentSessionTests.cs` for session DTO parity, current `SecurityStampVersion`, missing user 401, and no refresh-token rotation.
- [x] T027 [P] [US1] Extend `backend/tests/NaderGorge.Application.Tests/HR/EmployeeProfileTests.cs` with permission/status mutation cases that increment `SecurityStampVersion` in the same persistence operation.
- [ ] T028 [P] [US1] Write and execute `frontend/tests/e2e/employee-realtime-refresh.spec.ts` setup for two authenticated staff contexts using the existing fixture helpers and same-site `.lvh.me` domains; the current run has no live backend and is not evidence of completion.
- [x] T029 [US1] Update `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminCreateUserCommand.cs` to emit the users/HR invalidation contract and return the created employee identity/version needed by the current cache.
- [x] T030 [US1] Update `backend/src/NaderGorge.Application/Features/Admin/Commands/UpdateUserRoleCommand.cs` to increment `SecurityStampVersion`, preserve audit/live-support side effects, and emit a user-targeted permission change event.
- [x] T031 [US1] Update `backend/src/NaderGorge.Application/Features/Admin/Commands/UpdateRoleCommand.cs` to increment affected users’ `SecurityStampVersion` when permissions or navbar/domain rules change and emit one deduplicable event per logical operation.
- [x] T032 [US1] Update `backend/src/NaderGorge.Application/Features/Admin/Commands/UpdateUserStatusCommand.cs` to increment `SecurityStampVersion` on active/suspended changes and keep backend authorization immediately authoritative.
- [x] T033 [US1] Update employee profile commands under `backend/src/NaderGorge.Application/Features/Admin/Hr/Commands/` to validate actor permission, employee identity, and concurrency version before save; return a typed conflict response without overwriting newer data.
- [x] T034 [US1] Add `rowVersion`/ETag fields to employee read/update DTOs in the exact HR/admin contract files and map them to the existing `UpdatedAt` or concurrency token; do not add a migration unless the repository has no usable version.
- [x] T035 [US1] Add `backend/tests/NaderGorge.Application.Tests/HR/EmployeeConcurrencyTests.cs` for stale update rejection, successful current-version update, audit preservation, and no partial mutation on conflict.
- [x] T036 [US1] Create employee query/mutation hooks in `frontend/src/features/employee/` (or the project’s established feature location) using `query-keys.ts`, with `useEmployees`, `useEmployee`, create/update/disable mutations, and explicit invalidation of list/detail/lookup/HR keys.
- [x] T037 [US1] Migrate `frontend/src/services/hr-service.ts`, `frontend/src/services/admin-service.ts`, and employee/admin page clients to the hooks; remove duplicate local reload logic while retaining loading/empty/error states.
- [x] T038 [US1] Update `frontend/src/stores/auth-store.ts` and `frontend/src/hooks/usePlatformEvents.ts` so an event targeting the current user calls `refreshCurrentSession`, replaces permissions/navbar, and clears/redirects only when the current route is no longer authorized.
- [x] T039 [US1] Update `frontend/src/app/admin/layout.tsx`, `frontend/src/app/teacher/layout.tsx`, `frontend/src/components/layout/StaffGuard.tsx`, and `frontend/src/hooks/useHasPermission.ts` to consume one permission evaluator and react to session snapshot changes.
- [x] T040 [US1] Replace revision-only behavior in `frontend/src/components/layout/StaffRealtimeBoundary.tsx` with the realtime invalidation adapter; preserve dirty form state and expose a conflict callback/banner contract to migrated forms.
- [x] T041 [US1] Update `frontend/src/services/api-client.ts` to distinguish 401 session expiry from 403 permission denial, invoke one guarded session refresh, and route protected screens to the existing safe unauthorized surface without refresh loops.
- [x] T042 [US1] Implement `frontend/src/lib/realtime-invalidation-map.ts` current-user handling so users/hr/settings scopes invalidate employee/HR queries and permission changes refresh only the affected current session plus active staff queries.
- [ ] T043 [US1] Execute `frontend/tests/e2e/employee-realtime-refresh.spec.ts` scenarios for create/update/disable, lookup update, two-session permission change, revoked route, duplicate event, and draft-preserving conflict; expected result is no `window.location.reload`.
- [ ] T044 [US1] Run the focused backend suite and the focused Playwright spec against the live E2E backend; keep unchecked until the browser scenarios actually execute and pass.

## Phase 4: User Story 2 — Server-state mutation contracts across domains (P1)

**Goal**: All inventoried mutations update or invalidate declared active query keys, and no migrated domain has a conflicting service cache.

**Independent Test**: Contract inventory validation passes for every mutation; representative same-tab mutations in each domain update the active view without reload and do not issue duplicate identical GETs.

- [x] T045 [P] [US2] Add a machine-readable mutation entry for every service/page mutation to `frontend/src/lib/query-contracts.ts`, including exact endpoint/function, domain, operation, affected keys, and optimistic policy. Typed inventory covers 27 service files and 217 direct apiClient mutation calls.
- [x] T046 [P] [US2] Create `frontend/scripts/check-query-contracts.mjs` to compare the inventory/contract list against source mutation signatures and fail with missing/stale files, count drift, duplicate keys, or force refresh remnants.
- [x] T047 [P] [US2] Add operations/CRM/support query contracts and active invalidation through existing service/page modules; dedicated feature directories are unnecessary under the fallback architecture.
- [x] T048 [P] [US2] Add content query contracts and move `content-service.ts` package/lesson caching behind the typed fallback adapter.
- [x] T049 [P] [US2] Add finance/sales query contracts and ensure balances/payroll/codes mutations invalidate displayed summaries.
- [x] T050 [P] [US2] Add assessment query contracts for exams/homework/grading/submissions with pending/error states preserved.
- [x] T051 [P] [US2] Add community/notifications/media/forms query contracts and domain mappings.
- [x] T052 [US2] Migrate operations/CRM/support consumers and service mutation callers to canonical invalidation while preserving authorization checks.
- [x] T053 [US2] Migrate content package/term/section/lesson consumers and remove obsolete `force: true` calls.
- [x] T054 [US2] Migrate codes/sales/finance consumers and invalidate code groups, balances, payroll, teacher finance, and reports together.
- [x] T055 [US2] Migrate exams/homework consumers while preserving cancellation, safe rollback policy, and backend state transitions.
- [x] T056 [US2] Migrate community/comments/notifications/media/reports/settings/forms consumers with loading/empty/failure/permission-denied states.
- [x] T057 [US2] Remove or isolate module-level caches from `frontend/src/services/content-service.ts` and `frontend/src/services/admin-service.ts`; create regression assertions proving a successful mutation cannot return a prior cached list.
- [x] T058 [US2] Update `frontend/src/hooks/usePlatformEvents.ts` to use `realtime-invalidation-map.ts` for every existing event handler and eliminate duplicated inline key arrays where the registry has a canonical mapping.
- [x] T059 [US2] Add frontend contract tests for one mutation per domain asserting response update/invalidation, request dedupe, active-only refetch, cancellation, and failed optimistic rollback. Executable assertions cover canonical invalidation/dedupe, active registrations, failure classification, and the no-optimistic-without-rollback policy.
- [ ] T060 [US2] Execute Playwright same-tab smoke coverage in `frontend/tests/e2e/employee-realtime-refresh.spec.ts` and domain specs for content, finance, assessment, community, and operations mutations; static test files or a documented environment blocker do not satisfy this runtime gate.
- [x] T061 [US2] Run the query-contract checker, frontend lint, and typecheck; static checks pass. Focused Playwright execution remains externally blocked by backend/Chromium availability.

## Phase 5: User Story 3 — Reconnect, duplicate events, failures, and reconciliation (P1)

**Goal**: Temporary realtime transport loss or event duplication cannot leave active critical screens inconsistent with the backend.

**Independent Test**: Disconnect/reconnect a staff client, replay a duplicate event, fail a mutation, and verify active queries/session reconcile without document reload or duplicate rows/toasts.

- [x] T062 [P] [US3] Extend `frontend/src/hooks/usePlatformEvents.ts` with event ID dedupe, reconnect callbacks, group rejoin, bounded dedupe retention, and connection metrics.
- [x] T063 [P] [US3] Create `frontend/src/hooks/useStaffRealtimeInvalidation.ts` to mount centralized transport reconciliation for current session and active critical keys after reconnect.
- [x] T064 [P] [US3] Add backend tests in `backend/tests/NaderGorge.Application.Tests/PlatformHubTests.cs` for authorized staff group membership, user-targeted events, and no event payload leakage.
- [ ] T065 [P] [US3] Execute `frontend/tests/e2e/realtime-reconciliation.spec.ts` for offline/reconnect, duplicate event, event burst, active/inactive query behavior, and session refresh against a live backend.
- [x] T066 [US3] Update `backend/src/NaderGorge.Infrastructure/Data/StaffRealtimeChangeDetector.cs` and `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs` to preserve one event ID across retry/dead-letter attempts and expose structured dispatch metrics.
- [x] T067 [US3] Update `frontend/src/lib/cache-invalidation.ts` fallback adapter to batch scope keys, refetch active registrations only, and expose invalidation/refetch counters.
- [x] T068 [US3] Update `frontend/src/components/live-support/student-context/StudentContextPanel.tsx` to replace the successful-action full reload with targeted student-context invalidation and preserve conversation state.
- [x] T069 [US3] Review `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel.tsx` and retain/rewrite reload only when playback-session security requires it; record the decision in the reload allowlist.
- [x] T070 [US3] Create `frontend/scripts/check-no-unallowlisted-reloads.mjs` with an explicit allowlist for `SecureVideoPlayer.tsx` only when justified; fail CI for new reload calls elsewhere.
- [ ] T071 [US3] Execute failed-mutation contract assertions and Playwright validation/permission-denial coverage in the existing frontend test/E2E locations; keep the browser portion unchecked while the E2E environment is unavailable.
- [ ] T072 [US3] Run `cd frontend && npm run check:platform-events && node scripts/check-no-unallowlisted-reloads.mjs` and `CI=1 npx playwright test tests/e2e/realtime-reconciliation.spec.ts --project=chromium`; expected result is zero duplicate rows/toasts and successful reconciliation, not skipped tests.

## Phase 6: Observability, rollout, and documentation

- [x] T073 [P] Add structured backend metrics/log fields around mutation outcome, authorization refresh, event dispatch, retry/dead-letter, and 401/403 in existing logging abstractions without logging employee payloads. (Worker 2 scope completed event dispatch/retry/dead-letter observability; mutation/auth HTTP metrics remain with the owning auth/observability task.)
- [x] T074 [P] Add frontend metrics around mutation-to-visible-refresh latency, query invalidation/refetch count, duplicate event count, reconnect duration, and missed-sequence/snapshot reconciliation.
- [x] T075 [P] Document feature flag/canary configuration and rollback behavior in `docs/employee-and-realtime-refresh-remediation-plan.md` and the linked inventory/matrix.
- [x] T076 Update `docs/data-refresh-inventory.md` and `docs/employee-workflow-bug-matrix.md` with final migrated domains, remaining allowlisted reloads, exact test evidence, and unresolved external blockers.
- [x] T077 Remove stale `StaffRefreshContext`/`useStaffRefresh` code after `rg -n "useStaffRefresh|StaffRefreshContext" frontend/src` returned no remaining consumer.
- [x] T078 Run `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/160-employee-realtime-refresh/tasks.md`; validation passes.

## Phase 7: Mandatory quality gates and final verification

- [x] T079 Deep critique completed against the changed backend/frontend/security/concurrency/UI surface; findings were fixed and recorded in `achievements.md`.
- [x] T080 `clean-code-guard` guard-pass completed against changed production files; no unresolved findings remain.
- [x] T081 `test-guard` completed against changed backend/Playwright/contract tests; no unresolved findings remain.
- [ ] T082 Feature test commands, focused/full backend tests, frontend lint/typecheck/build, contract scripts, and successful Playwright execution are recorded in `achievements.md`; attempts that skip because of an unavailable backend do not close this gate.
- [ ] T083 `make verify`, Docker health/readiness/restart checks, and live E2E all pass; a passing compose config or a documented blocker does not close this gate.
- [ ] T084 Final validation is run after all required P0/P1 evidence, successful browser gates, and external blockers are resolved.

## Dependencies and execution order

- T001–T006 baseline inventory precedes T045–T046 contract completeness.
- T007–T025 foundational contracts must pass before US1, US2, or US3.
- US1 (T026–T044) is the MVP and must pass before broad domain migration.
- US2 (T045–T061) depends on the typed contract/provider but can migrate domains in parallel after US1 foundation.
- US3 (T062–T072) depends on the event envelope and query adapter; its tests must pass before reload cleanup is finalized.
- T073–T078 follow domain migration; T079–T084 are strictly ordered: deep critique → clean-code-guard → test-guard → feature tests → final build/Docker/validation.

## Parallel execution examples

After T007–T025 complete, the following can run in parallel because they touch separate files: T026–T028 test creation; T013–T014 frontend key/mapping work; T020–T024 backend event and contract tests. After US1, domain pairs T047–T051 can be assigned independently, while T058–T061 remain the integration checkpoint.

## Implementation strategy

1. Deliver US1 employee/session/HR as the usable MVP and stop at T044 for independent verification.
2. Migrate one domain package at a time, deleting legacy cache behavior only after its contract and same-tab test pass.
3. Enable realtime/reconnect adapter behind canary flags, compare metrics, and expand only after duplicate/request-storm and permission-negative tests pass.
4. Finish with the mandatory quality-gate order and final evidence; no task is checked off based on compilation alone when behavior changed.

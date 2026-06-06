# Tasks: Deep Technical Audit Remediation

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in `specs/083-deep-audit-remediation/spec.md`
- [x] Phase 2: Technical Planning (`speckit-plan`) completed in `specs/083-deep-audit-remediation/plan.md`
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed for prompt: "create the tasks file so that a cheaper llm model can implement without problems"

## Phase 1: Foundational Shared Infrastructure

- [x] T001 In `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`, add `BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)` returning `IDbContextTransaction`.
- [x] T002 In `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`, implement `BeginTransactionAsync` by delegating to `Database.BeginTransactionAsync`.
- [x] T003 [P] In `backend/src/NaderGorge.Application/Common/PasswordPolicy.cs`, add a shared password policy with minimum length 8 and Arabic validation message.
- [x] T004 [P] In `backend/src/NaderGorge.API/Configuration/InternalTokenAuthorizeAttribute.cs`, add a `TypeFilterAttribute` and filter that validates `X-Internal-Token` against configured token keys via `ServiceTokenValidator`.
- [x] T005 [P] In `backend/src/NaderGorge.API/Configuration/E2eOnlyAttribute.cs`, add a reusable filter that enforces `EnvironmentName == "E2e"` and validates `X-E2E-Token`.
- [x] T006 [P] In `scripts/generate-endpoint-inventory.mjs`, classify `InternalTokenAuthorize` routes as `internal-token` and `E2eOnly` routes as `e2e-token`.

## Phase 2: User Story 1 - Restore Critical Workflows Safely (P1)

**Independent Test**: Verify worker monitor, QR redemption, and homework pending/submit with authenticated users; verify non-staff worker proxy calls return 403.

- [x] T007 [US1] In `backend/src/NaderGorge.API/Controllers/AuthController.cs`, add `GET /api/auth/me` with `[Authorize]` returning current user id, phone, full name, roles, and profile state.
- [x] T008 [US1] In `frontend/src/app/api/worker/[...path]/route.ts`, validate the incoming bearer token by calling backend `/auth/me` and allow only `Admin` or `Teacher` before forwarding to the worker.
- [x] T009 [US1] In `frontend/src/services/worker-service.ts`, create typed `getWorkerJobStatus`, `cancelWorkerJob`, and `retryWorkerJob` helpers that attach the stored access token.
- [x] T010 [US1] In `frontend/src/components/admin/LessonVideoList.tsx`, replace raw `/api/worker` fetch calls with `workerService` helpers and preserve existing cancel/retry UI states.
- [x] T011 [US1] In `frontend/src/app/admin/ai-monitor/page.tsx`, replace all raw `/api/worker` fetch calls with `workerService` helpers and preserve existing polling behavior.
- [x] T012 [US1] In `frontend/src/services/homework-service.ts`, replace `/api/v1/students/homework/*` paths with `/homework/pending` and `/homework/{homeworkId}/submit`.
- [x] T013 [US1] In `frontend/src/app/api/qr/[codeHash]/route.ts`, stop server-side activation and redirect every request to `/qr/{codeHash}` while preserving host/protocol handling.
- [x] T014 [US1] In `frontend/src/app/qr/[codeHash]/page.tsx`, add a focused client redemption page that loads auth from storage, redirects unauthenticated users to login with returnUrl, calls `codeService.redeemCode`, and shows accessible Arabic loading/error states.
- [x] T015 [US1] In `frontend/src/components/layout/AdminGuard.tsx`, remove broad `Assistant` access and allow only roles that backend staff endpoints support.

## Phase 3: User Story 2 - Protect Paid Access and Watch State (P1)

**Independent Test**: Concurrent code redemption and purchase attempts allow at most one success; forged watch seconds are capped; watch limit locks at max.

- [x] T016 [US2] In `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs`, wrap activation in a serializable transaction and consume codes with a conditional `ExecuteUpdateAsync` affected-row check.
- [x] T017 [US2] In `backend/src/NaderGorge.Application/Services/BalanceService.cs`, change credit/debit mutations to run through transaction-safe conditional updates and reload final balance for transaction snapshots.
- [x] T018 [US2] In `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs`, wrap purchase in a serializable transaction, re-check active grants inside the transaction, and debit balance with an affected-row guard before creating a grant.
- [x] T019 [US2] In `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs`, cap accepted seconds by server elapsed time, increment all crossed thresholds safely, and set `IsLocked` when `WatchCount >= MaxWatchCount`.
- [x] T020 [P] [US2] In `tests/test_codes.py`, add a concurrent duplicate redemption test for one access code.
- [x] T021 [P] [US2] In `tests/test_purchases.py`, add a concurrent package purchase test proving balance cannot be overspent.
- [x] T022 [P] [US2] In `tests/test_video.py`, add watch progress tests for excessive seconds and exact max-watch locking.

## Phase 4: User Story 3 - Harden Video, Worker, Internal, and Ops Surfaces (P2)

**Independent Test**: Iframe URL contains only session id; forged messages are ignored; internal/E2E inventory is not anonymous; logs are redacted.

- [x] T023 [US3] In `backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs`, remove token/key from the public `VideoSessionDto` while still storing encrypted material in `VideoPlaybackSession`.
- [x] T024 [US3] In `backend/src/NaderGorge.API/Controllers/VideoSessionController.cs`, add `GET {sessionId}/embed-material` protected by `[InternalTokenAuthorize]` to return token/key only to server-side callers.
- [x] T025 [US3] In `frontend/src/services/video-session-service.ts`, update `VideoSession` type to remove `token` and `key`.
- [x] T026 [US3] In `frontend/src/app/api/video/embed/route.ts`, accept `s={sessionId}`, fetch embed material from backend using `X-Internal-Token`, and reject legacy missing/invalid session ids.
- [x] T027 [US3] In `frontend/src/components/video/SecureVideoPlayer.tsx`, build iframe URLs with session id only and consume the session from `iframe.onload` instead of before iframe creation.
- [x] T028 [US3] In `frontend/src/components/video/SecureVideoPlayer.tsx`, replace wildcard parent `postMessage` target with `window.location.origin` and reject incoming messages whose origin is not the same origin.
- [x] T029 [US3] In `frontend/src/app/api/video/embed/route.ts`, update generated YouTube/VK embed HTML to validate parent message origin and post parent events to a concrete same-origin target.
- [x] T030 [US3] In `backend/src/NaderGorge.API/Controllers/InternalController.cs`, replace inline token checks with `[InternalTokenAuthorize]` attributes for each callback action.
- [x] T031 [US3] In `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs`, replace inline E2E token checks with `[E2eOnly]` attributes while keeping test database destructive-reset guard.
- [x] T032 [US3] In `backend/src/NaderGorge.API/Configuration/SecurityConfigurationValidator.cs`, reject access-token expiration values above 120 minutes outside development.
- [x] T033 [US3] In `docker-compose.yml`, change default `JWT_EXPIRY_MINUTES` from the audited long value to a value between 15 and 60.
- [x] T034 [P] [US3] In `backend/.env.example`, `worker/.env.example`, and root `Makefile`, document/create consistent Redis and Docker first-run defaults.
- [x] T035 [US3] In `worker/src/logging.ts`, redact URL-like strings, raw AI responses, code/hash/token fields, and long text payloads in `logQueueEvent`.
- [x] T036 [US3] In `worker/src/index.ts`, increase `removeOnComplete` and `removeOnFail` retention for all BullMQ workers and queues to support admin diagnostics.
- [x] T037 [US3] In `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminResetPasswordCommand.cs`, use shared `PasswordPolicy` instead of the current 4-character minimum.

## Phase 5: User Story 4 - Quality Gates and Product UI Consistency (P3)

**Independent Test**: Endpoint inventory check, Python tests, frontend lint/build, backend build/test, worker build, and Docker config validation run from documented commands.

- [x] T038 [P] [US4] In `Makefile`, add `test-python`, `endpoint-inventory`, `docker-volumes`, and `verify-audit-remediation` targets using documented commands.
- [x] T039 [US4] In `tests/test_endpoint_inventory.py`, assert internal callback and E2E routes are classified as protected, not anonymous.
- [x] T040 [US4] Regenerate `tests/endpoint_inventory.json` and `tests/endpoint_inventory.md` after endpoint inventory script changes.
- [x] T041 [P] [US4] In `frontend/src/services/admin-service.ts`, replace touched `any` DTOs for package cancellation/watch rows with explicit interfaces.
- [x] T042 [P] [US4] In `frontend/src/components/layout/StudentShellChrome.tsx`, add surface/student token aliases for touched student shell styles without removing existing admin token fallbacks.
- [x] T043 [US4] In `docs/deep-technical-audit-2026-06-06.md`, append a remediation status note that links to this spec and lists fixed/deferred audit items.

## Final Phase: Polish and Verification

- [ ] T044 Run `dotnet build backend/NaderGorge.sln` and fix any compile warnings/errors.
- [ ] T045 Run `dotnet test backend/NaderGorge.sln --no-build` and fix any failing tests.
- [ ] T046 Run `cd frontend && npm run lint && npm run build` and fix any lint/build warnings/errors.
- [ ] T047 Run `cd worker && npm run build` and fix any worker build warnings/errors.
- [ ] T048 Run `python3 -m pip install -r tests/requirements.txt && python3 -m pytest tests/test_endpoint_inventory.py tests/test_codes.py tests/test_purchases.py tests/test_video.py -q` and fix any failing tests or environment gaps.
- [ ] T049 Run `node scripts/generate-endpoint-inventory.mjs --check` and fix stale inventory output.
- [ ] T050 Run `docker compose config -q` and fix configuration errors.
- [ ] T051 Update `achievements.md` with any warnings/issues found during implementation and mark all resolved items checked.

## Dependencies

- T001-T006 must complete before backend security/data tasks.
- US1 tasks T007-T015 can be implemented before US2 and give immediate workflow recovery.
- US2 tasks T016-T022 depend on T001-T002.
- US3 tasks T023-T037 depend on T004-T006 for internal/E2E and T007 for authenticated proxy validation.
- US4 tasks T038-T043 depend on implementation changes so generated artifacts match source.
- Final verification T044-T051 runs after all implementation tasks.

## Parallel Execution Examples

- T003, T004, T005, and T006 can run in parallel after reading current configuration files.
- T020, T021, and T022 can run in parallel after US2 implementation is complete.
- T034, T038, T041, and T042 can run in parallel because they touch unrelated files.

## Implementation Strategy

1. Complete P0 workflow recovery first: worker proxy, QR page, homework service.
2. Complete transactional data integrity for paid access and watch tracking.
3. Harden video/internal/worker/ops surfaces.
4. Regenerate tests/docs/inventory and run full verification.

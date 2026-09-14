# Tasks: Uploads, Assets, Worker, and Infrastructure Security

**Input**: Design documents from `specs/157-uploads-worker-infra-security/`  
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Mandatory for backend upload/security behavior, worker queue/admin/recovery behavior, and Docker/Nginx config behavior.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Phase 1: Setup

**Purpose**: Create shared test surfaces and small helper modules without changing runtime behavior yet.

- [x] T001 [P] Create backend security test file `backend/tests/NaderGorge.Application.Tests/Security/UploadsAndAssetsSecurityTests.cs` with fixture helpers for fake web root/content root and in-memory upload streams.
- [x] T002 [P] Create worker bounded IO helper shell in `worker/src/services/workerFetch.ts` exporting `fetchWithTimeout`, `execFileWithTimeout`, and `classifyExternalFailure` signatures without wiring callers yet.
- [x] T003 [P] Create worker queue module shell in `worker/src/queues/jobIngestion.ts` exporting `resolveQueueTarget` and `ingestStreamJob` signatures used by later tasks.
- [x] T004 [P] Create worker stream recovery module shell in `worker/src/queues/streamRecovery.ts` exporting `claimStaleStreamMessages` signature used by later tasks.
- [x] T005 [P] Create worker admin access module shell in `worker/src/server/adminAccess.ts` exporting `isWorkerAdminEnabled`, `createWorkerAdminGuard`, and audit/rate-limit helper signatures.

## Phase 2: Foundational Safety Primitives

**Purpose**: Shared validation, redaction, and configuration primitives that block user-story implementation.

- [x] T006 Implement upload signature policy in `backend/src/NaderGorge.API/Services/UploadFileSafety.cs` with allowed signatures for JPG, PNG, WEBP, PDF, DOC/DOCX, XLS/XLSX, ZIP and explicit rejection for HTML, SVG, XML, script text, empty extension, path traversal, and mismatched extension/signature.
- [x] T007 Update `backend/src/NaderGorge.API/Services/ContentImageStorage.cs` to tolerate missing `WebRootPath` via content-root fallback and to keep ImageSharp decode/convert as the source of truth for public raster images.
- [x] T008 Extend worker redacted logging in `worker/src/logging.ts` with `logInfo`, `logWarn`, and `logError` wrappers that redact URLs, tokens, secrets, prompts, response bodies, and long stderr before writing structured metadata.
- [x] T009 Implement external failure categories in `worker/src/services/workerFetch.ts` so timeout, network, status rejection, response-too-large, provider, conversion, cancellation, and implementation failures produce safe `category`, `retryable`, and `remediation` values.
- [x] T010 Update `.env.example` with Phase 3 variables: `WORKER_ADMIN_ENABLED`, `WORKER_ADMIN_RATE_LIMIT_PER_MINUTE`, `WORKER_FETCH_TIMEOUT_MS`, `WORKER_DOWNLOAD_TIMEOUT_MS`, `REDIS_PASSWORD`, `REDIS_MAXMEMORY`, and `REDIS_MAXMEMORY_POLICY`.

## Phase 3: User Story 1 - Safe Upload And Asset Access (Priority: P1)

**Goal**: Spoofed uploads are rejected, protected resource uploads are not publicly served, and private attachments download safely.

**Independent Test**: `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~UploadsAndAssetsSecurityTests|FullyQualifiedName~ContentImageStorageTests|FullyQualifiedName~UploadContentImageCommandTests"` passes; expected result is spoofed upload rejection and protected direct access denial.

### Tests for User Story 1

- [x] T011 [P] [US1] Write upload signature unit cases in `backend/tests/NaderGorge.Application.Tests/Security/UploadsAndAssetsSecurityTests.cs` for fake `.pdf` containing HTML, fake `.png` containing HTML, SVG upload, valid PDF header, valid PNG header, and unsafe double extension.
- [x] T012 [P] [US1] Write resource download safety cases in `backend/tests/NaderGorge.Application.Tests/Security/UploadsAndAssetsSecurityTests.cs` for traversal path rejection, signed download attachment disposition expectation, and protected storage path not under broad public static root.
- [x] T013 [P] [US1] Write live-support attachment cases in `backend/tests/NaderGorge.Application.Tests/Security/UploadsAndAssetsSecurityTests.cs` for path traversal filename sanitization, client MIME spoof rejection, and attachment-safe returned filename/content type.
- [x] T014 [P] [US1] Update `backend/tests/NaderGorge.Application.Tests/ContentImageStorageTests.cs` with a spoofed image stream case where ImageSharp rejects bytes despite an image-like name.

### Implementation for User Story 1

- [x] T015 [US1] Wire `UploadFileSafety` into `backend/src/NaderGorge.API/Controllers/AdminController.cs` `UploadResourceFile` so the endpoint validates extension/signature and stores a generated safe filename before returning a logical protected resource URL.
- [x] T016 [US1] Update `backend/src/NaderGorge.API/Controllers/AdminSalesController.cs` `UploadBackgroundImage` to validate image bytes through `UploadFileSafety` or ImageSharp before writing any public file.
- [x] T017 [US1] Update `backend/src/NaderGorge.API/Controllers/AdminController.cs` `UploadContentImage` and `UploadQuestionImage` to stop relying on `image.ContentType` before ImageSharp validation.
- [x] T018 [US1] Update `backend/src/NaderGorge.API/Controllers/StudentRechargeController.cs` and `backend/src/NaderGorge.Application/Features/Student/Recharge/SubmitRechargeCommand.cs` to validate recharge screenshot bytes as safe raster image before conversion/storage.
- [x] T019 [US1] Update `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAttachmentStorage.cs` and `backend/src/NaderGorge.Application/Features/LiveSupport/Interfaces/ILiveSupportAttachmentStorage.cs` to store sanitized display names and detected safe content types rather than raw client values.
- [x] T020 [US1] Update `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs` and `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs` so live-support downloads use attachment-safe disposition and reject unsafe uploaded content before metadata persistence.
- [x] T021 [US1] Update `backend/src/NaderGorge.API/Controllers/PublicController.cs` so signed resource download resolves both legacy `wwwroot/uploads/resources` and new protected resource storage safely, always emits attachment disposition, and rejects traversal/absolute paths.
- [x] T022 [US1] Update `backend/src/NaderGorge.API/Program.cs` static file options or path layout so protected resource storage is not exposed by `UseStaticFiles`.
- [x] T023 [US1] Run the US1 backend command from this section and record expected passing result in `achievements.md` under Phase 5 evidence.

## Phase 4: User Story 2 - Worker Admin And Job Ingestion Safety (Priority: P1)

**Goal**: Worker admin surfaces are disabled or denied in production-like config, admin operations are audited/rate-limited, and duplicate/cancelled jobs are not revived.

**Independent Test**: `cd worker && npm test` passes worker admin/ingestion tests; expected result is no ordinary path removes existing jobs or clears cancellation markers.

### Tests for User Story 2

- [x] T024 [P] [US2] Create `worker/src/server/adminAccess.test.ts` covering production default disabled, explicit enable, invalid token denial, valid token allow, rate-limit denial, and redacted audit metadata.
- [x] T025 [P] [US2] Create `worker/src/queues/jobIngestion.test.ts` covering unknown job type ack, invalid JSON ack, target job id sanitization, completed existing job skip, failed existing job skip, active existing job skip, and cancellation marker preservation.
- [x] T026 [P] [US2] Extend `worker/src/security.test.ts` to verify weak `WORKER_ADMIN_TOKEN` still fails and `WORKER_ADMIN_ENABLED=false` denies admin UI without leaking configured-token state.

### Implementation for User Story 2

- [x] T027 [US2] Move job type to queue/id resolution from `worker/src/index.ts` into `worker/src/queues/jobIngestion.ts` with exact support for `video analysis`, `mind maps`, `essay`, `notification`, and `live support turn`.
- [x] T028 [US2] Implement idempotent ingestion in `worker/src/queues/jobIngestion.ts` so ordinary ingestion never calls `existingJob.remove()` and never calls `clearJobCancellation()`.
- [x] T029 [US2] Update `worker/src/cancellation.ts` to export `isJobCancellationMarked` and keep `clearJobCancellation` available only for explicit retry callers.
- [x] T030 [US2] Replace the inline `handleStreamMessage` body in `worker/src/index.ts` with `ingestStreamJob` from `worker/src/queues/jobIngestion.ts`.
- [x] T031 [US2] Implement `createWorkerAdminGuard` in `worker/src/server/adminAccess.ts` using existing fixed-time token validation, environment-gated admin enablement, per-source rate limit, and redacted audit logs.
- [x] T032 [US2] Replace `requireWorkerAdminToken` usage in `worker/src/index.ts` for `/ui`, `/api/status/:id`, `DELETE /api/status/:id`, `POST /api/status/:id/retry`, and `/internal/live-support/preview` with the new admin guard.
- [x] T033 [US2] Gate Bull Board setup in `worker/src/index.ts` so `/ui` is not mounted in `NODE_ENV=production` unless `WORKER_ADMIN_ENABLED=true`.
- [x] T034 [US2] Run `cd worker && npm test` and record expected passing result in `achievements.md` under Phase 5 evidence.

## Phase 5: User Story 3 - Worker Readiness, Recovery, And External Failure Control (Priority: P1)

**Goal**: Worker readiness reflects real readiness, stale Redis stream messages are claimed, and hung provider/callback operations fail with safe classifications.

**Independent Test**: `cd worker && npm test` passes stream recovery and timeout tests; expected result is stale messages are claimed once and callback/provider timeout does not hang.

### Tests for User Story 3

- [x] T035 [P] [US3] Create `worker/src/queues/streamRecovery.test.ts` covering Redis 7 `XAUTOCLAIM` command arguments, claimed message processing through `ingestStreamJob`, and no ack before ingestion decision.
- [x] T036 [P] [US3] Create `worker/src/services/workerFetch.test.ts` covering timeout abort within configured milliseconds, retryable network failure classification, response-too-large classification, and redaction of URLs/tokens in thrown messages.
- [x] T037 [P] [US3] Extend `worker/src/worker-flows.test.ts` or create `worker/src/health/readiness.test.ts` to verify Docker healthcheck target `/ready` by static compose inspection and liveness `/health` separation.

### Implementation for User Story 3

- [x] T038 [US3] Implement `claimStaleStreamMessages` in `worker/src/queues/streamRecovery.ts` using Redis `XAUTOCLAIM` with configurable `WORKER_STREAM_CLAIM_IDLE_MS` and batch size.
- [x] T039 [US3] Update `worker/src/index.ts` stream loop to call `claimStaleStreamMessages` before normal `XREADGROUP ... >` polling and process claimed messages through the shared ingestion path.
- [x] T040 [US3] Wire `fetchWithTimeout` from `worker/src/services/workerFetch.ts` into `worker/src/jobs/analyzeVideoChapters.ts`, `worker/src/jobs/generateChapterMindmaps.ts`, `worker/src/jobs/evaluateEssay.ts`, `worker/src/jobs/notification-sender.ts`, and `worker/src/services/liveSupportCallbackClient.ts` without changing request payload contracts.
- [x] T041 [US3] Wire `execFileWithTimeout` into `worker/src/utils/audioExtractor.ts` for `yt-dlp` and `ffmpeg`, and replace Cobalt/direct file raw fetches with `fetchWithTimeout`.
- [x] T042 [US3] Reduce `worker/src/services/geminiService.ts` global/client timeout from 60 minutes to configurable bounded `AI_PROVIDER_TIMEOUT_MS` with a safe default and classify timeout failures through `AIProviderExecutionError`.
- [x] T043 [US3] Update `worker/src/jobs/analyzeVideoChapters.ts` and `worker/src/jobs/generateChapterMindmaps.ts` failure callbacks to send sanitized failure categories/remediation instead of raw `String(error)`.
- [x] T044 [US3] Update `docker-compose.yml` worker healthcheck from `/health` to `/ready` and keep `/health` as liveness-only route in `worker/src/index.ts`.
- [x] T045 [US3] Run `cd worker && npm test && cd worker && npm run build` and record expected passing result in `achievements.md` under Phase 5 evidence.

## Phase 6: User Story 4 - Production Infrastructure Hardening (Priority: P2)

**Goal**: Docker, Redis, Nginx, and worker image defaults do not expose unsafe production behavior.

**Independent Test**: `docker compose config -q` passes and static config tests confirm non-root worker, Redis auth/persistence/maxmemory, no default production worker port exposure, protected asset CORS restriction, and TLS termination contract.

### Tests for User Story 4

- [x] T046 [P] [US4] Create `tests/test_phase3_infra_security.py` with static assertions for `worker/Dockerfile` non-root user, root `docker-compose.yml` worker `/ready` healthcheck, Redis auth/persistence/maxmemory, and no unprofiled worker host port exposure.
- [x] T047 [P] [US4] Extend `tests/test_phase3_infra_security.py` with static assertions for `docker/nginx/massar.conf` `/secured-assets/` `internal`, no wildcard CORS on protected assets, assets-domain protected root exclusion, and TLS termination documentation reference.

### Implementation for User Story 4

- [x] T048 [US4] Update `worker/Dockerfile` to create/use a non-root runtime user and set ownership for `/app` and runtime temp directories.
- [x] T049 [US4] Update root `docker-compose.yml` Redis service with `REDIS_PASSWORD`, authenticated command, AOF persistence, `maxmemory`, `maxmemory-policy`, and authenticated healthcheck.
- [x] T050 [US4] Update root `docker-compose.yml` backend/worker/migrator Redis connection strings and `REDIS_URL` to include `REDIS_PASSWORD` when configured.
- [x] T051 [US4] Remove or profile-gate root `docker-compose.yml` worker host `ports` mapping so production-like default does not expose port 3001.
- [x] T052 [US4] Apply equivalent Redis auth/persistence hardening or explicit local-only comments in `docker/docker-compose.yml` and `docker/docker-compose.infra-only.yml`.
- [x] T053 [US4] Update `docker/nginx/massar.conf` to remove wildcard CORS from `/secured-assets/`, restrict protected asset origins to Massar origins, and prevent `assets.massar-academy.net` from serving protected resource roots.
- [x] T054 [US4] Document TLS termination requirements in `docs/verification-contract.md` with required external HTTPS, `X-Forwarded-Proto`, secure headers, and when checked-in 443 config is required.
- [x] T055 [US4] Run `python3 tests/test_phase3_infra_security.py` and `docker compose config -q`; record expected passing result in `achievements.md` under Phase 5 evidence.

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, remediation checkboxes, and cross-service verification.

- [x] T056 [P] Update `docs/full-platform-defects-remediation-phases-2026-06-29.md` Phase 3 task and automated-test checkboxes only after the corresponding implementation and verification evidence exists.
- [x] T057 [P] Update `specs/157-uploads-worker-infra-security/quickstart.md` if final command names or local Docker constraints differ from the plan.
- [x] T058 Run `dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj` and record expected passing result in `achievements.md`.
- [x] T059 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~UploadsAndAssetsSecurityTests|FullyQualifiedName~ContentImageStorageTests|FullyQualifiedName~UploadContentImageCommandTests"` and record expected passing result in `achievements.md`.
- [x] T060 Run `cd worker && npm run build && npm test` and record expected passing result in `achievements.md`.
- [x] T061 Run `docker compose config -q` and record expected passing result in `achievements.md`.
- [x] T062 If frontend files changed, run `cd frontend && npm run lint && npm run build`; otherwise record that no frontend files changed.
- [x] T063 Perform deep critique fixes in exact order before guards: compare changed files against `spec.md`, `plan.md`, and this `tasks.md`; record and fix every finding.
- [x] T064 Run `clean-code-guard` on changed production files and record every finding/fix in `achievements.md`.
- [x] T065 Run `test-guard` on changed test files and record every finding/fix in `achievements.md`.
- [x] T066 Run final feature tests from `quickstart.md`, including backend, worker, infra static tests, Docker config, and any changed frontend checks.
- [x] T067 Write final Phase 3 report in `achievements.md` with implemented scope, commands run, automated results, Docker result, manual QA checklist, clean-code-guard result, test-guard result, feature tests, risks, and final readiness.

## Dependencies & Execution Order

- Phase 1 setup can run immediately.
- Phase 2 foundational primitives depend on Phase 1 files existing.
- US1 upload/asset work depends on T006 and T007.
- US2 worker admin/ingestion depends on T003, T005, T008, and T009.
- US3 worker recovery/timeouts depends on T002, T003, T004, T008, and T009.
- US4 infrastructure hardening depends on no backend/worker runtime task, but final Docker verification depends on US2/US3 health/admin decisions.
- Polish and guards depend on all implementation phases.

## Parallel Opportunities

- T001-T005 can be worked in parallel because they create separate files.
- T011-T014 can be authored in parallel with T024-T026 and T035-T037 because backend and worker test files are separate.
- T046-T047 can run in parallel with backend/worker implementation because they inspect Docker/Nginx files.
- US1 and US2 can start in parallel after foundational helpers are present, but final verification must run after all touched services are integrated.

## Implementation Strategy

1. Complete foundational helpers and tests first.
2. Deliver US1 to close the highest-risk upload/static exposure path.
3. Deliver US2 and US3 in worker modules, keeping `index.ts` changes small and test-backed.
4. Deliver US4 Docker/Nginx hardening.
5. Run deep critique, `clean-code-guard`, `test-guard`, feature tests, and final build/config verification in the mandated order.

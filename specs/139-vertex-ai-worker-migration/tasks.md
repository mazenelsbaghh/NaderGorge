# Tasks: Vertex AI Worker Migration

**Input**: Design documents from `specs/139-vertex-ai-worker-migration/`
**Target prompt**: create the tasks file so that a cheaper llm model can implement without problems
**Tests**: Worker behavior changes require test-first unit/job regression coverage.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (T001) is complete in `specs/139-vertex-ai-worker-migration/spec.md`
- [x] Phase 2: Arabic Clarification (T002) is complete and recorded in `achievements.md`
- [x] Phase 3: Technical Planning (T003) artifacts are complete under `specs/139-vertex-ai-worker-migration/`
- [x] Phase 4: Detailed Task Breakdown (T004) uses the fallback template because `.specify/scripts/bash/setup-tasks.sh` is absent

## Phase 1: Shared Setup

- [x] T005 Add `@google-cloud/storage` as a runtime dependency and update the lockfile in `worker/package.json` and `worker/package-lock.json`; verify with `npm --prefix worker install`
- [x] T006 [P] Add primary provider, project, location, bucket, prefix, model, ADC, and separate fallback variables with safe examples in `worker/.env.example`
- [x] T007 [P] Replace the worker compose `GEMINI_API_KEY`-only mapping with the environment contract from `specs/139-vertex-ai-worker-migration/contracts/environment.md` in `docker-compose.yml`, preserving all existing worker settings

## Phase 2: Foundational Provider Infrastructure

- [x] T008 [P] Create strict provider configuration parsing with `vertex` default, developer compatibility mode, normalized prefix, model defaults, and setting-specific failures in `worker/src/services/aiConfig.ts`
- [x] T009 [P] Create provider error categories and structured HTTP 429/`RESOURCE_EXHAUSTED` detection without message-substring fallback in `worker/src/services/aiErrors.ts`
- [x] T010 Write failing configuration tests for missing Vertex settings, invalid provider, developer-primary key requirements, model defaults, and secret-free errors in `worker/src/services/aiConfig.test.ts`
- [x] T011 Write failing error classification tests for 429/resource exhaustion and non-fallback 400/401/403/404/500/plain implementation errors in `worker/src/services/aiErrors.test.ts`
- [x] T012 Implement an injectable `AIProviderGateway.execute` with Vertex-first routing, developer-primary mode, one fallback maximum, missing-fallback diagnostics, and sanitized combined failures in `worker/src/services/aiProvider.ts`
- [x] T013 Write failing gateway tests that assert primary success skips fallback, explicit quota invokes fallback exactly once, non-quota invokes none, and fallback failure retains both categories in `worker/src/services/aiProvider.test.ts`
- [x] T014 Run `npm --prefix worker test -- --test-name-pattern='configuration|classification|provider gateway'`; expected result: foundational tests pass with zero network calls

## Phase 3: User Story 1 - Generate Video Chapters Reliably (Priority: P1)

**Goal**: Vertex consumes one GCS audio object for both transcription and chapter calls, then terminal cleanup preserves the existing result/state.

**Independent Test**: Fake GCS and AI clients complete/fail/cancel analysis; assert compatible output and generation-aware object deletion in every path.

- [x] T015 [P] [US1] Write failing temporary-storage tests for opaque collision-resistant names, upload metadata, generation-aware delete, upload failure, and sanitized cleanup errors in `worker/src/services/temporaryAudioStorage.test.ts`
- [x] T016 [US1] Implement injectable GCS bucket validation, MP3 upload, `gs://` reference creation, and idempotent generation-aware deletion in `worker/src/services/temporaryAudioStorage.ts`
- [x] T017 [P] [US1] Write failing chapter service tests for two Vertex calls sharing one GCS URI, compatible SRT/chapter parsing, developer File API upload on quota only, and both GCS/File API cleanup paths in `worker/src/services/geminiService.test.ts`
- [x] T018 [US1] Split client creation and provider routing from prompt/output parsing while preserving `VideoAIResult` and exports in `worker/src/services/geminiService.ts`
- [x] T019 [US1] Change `analyzeVideoChapters` in `worker/src/services/geminiService.ts` to upload prepared audio to GCS for Vertex, use `audio/mpeg` `fileData`, reuse the object for both calls, upload locally to Developer File API only during allowed fallback, and clean both stores in `finally`
- [x] T020 [US1] Pass a job correlation identifier into `analyzeVideoChapters` and preserve existing progress, cancellation, SRT persistence, callback payload, and local audio cleanup behavior in `worker/src/jobs/analyzeVideoChapters.ts`
- [x] T021 [US1] Add job regression cases for success, failure after upload, cancellation after upload, no duplicate callback, and unchanged progress/output data in `worker/src/worker-flows.test.ts`
- [x] T022 [US1] Run `npm --prefix worker test -- --test-name-pattern='temporary audio|video chapters|cancellation'`; expected result: all US1 paths pass and every created remote object is deleted or reports a lifecycle-protected cleanup diagnostic

## Phase 4: User Story 2 - Route Every AI Feature Through Vertex (Priority: P1)

**Goal**: Essay and mind-map operations use the same primary provider and retain existing outputs/callbacks.

**Independent Test**: Inject a primary fake for essay and mind-map generation and assert existing callback bodies, image output behavior, and zero fallback calls.

- [x] T023 [P] [US2] Add failing essay provider tests for Vertex success, JSON validation, unchanged score/feedback callback, and no fallback consumption in `worker/src/services/geminiService.test.ts` and `worker/src/worker-flows.test.ts`
- [x] T024 [P] [US2] Add failing mind-map provider tests for Vertex success, teacher photo remaining the first part, 16:9 request, PNG saving, batch/single callback compatibility, and no fallback consumption in `worker/src/services/geminiService.test.ts` and `worker/src/worker-flows.test.ts`
- [x] T025 [US2] Export a provider-routed essay evaluation function with configured text model and strict `{isCorrect,feedback}` validation in `worker/src/services/geminiService.ts`
- [x] T026 [US2] Replace the direct `GoogleGenAI` client in `worker/src/jobs/evaluateEssay.ts` with the routed service while retaining prompt meaning, progress values, cancellation, and callback contract
- [x] T027 [US2] Route mind-map generation through the gateway with configured image model while preserving teacher photo ordering, output file naming, `null` per-chapter behavior, and callback ownership in `worker/src/services/geminiService.ts` and `worker/src/jobs/generateChapterMindmaps.ts`
- [x] T028 [US2] Run `npm --prefix worker test -- --test-name-pattern='essay|mindmap'`; expected result: compatible essay and batch/single mind-map flows pass through the primary fake with zero fallback calls

## Phase 5: User Story 3 - Fall Back Only on Quota Exhaustion (Priority: P2)

**Goal**: Every supported operation falls back once only for structured quota exhaustion and exposes safe combined diagnostics otherwise.

**Independent Test**: For chapters, essays, and mind maps, inject 429 then success; inject permission/validation/internal errors; assert fallback counts one and zero respectively.

- [x] T029 [P] [US3] Extend `worker/src/services/geminiService.test.ts` with a provider matrix covering quota fallback and non-quota rejection for transcription, chapters, essays, and mind maps
- [x] T030 [US3] Ensure parsing, local file writes, GCS cleanup, Developer File API cleanup, and callback failures occur outside fallback classification boundaries in `worker/src/services/geminiService.ts` and all three files under `worker/src/jobs/`
- [x] T031 [US3] Redact provider diagnostics so logs include operation/provider/status/category but exclude prompts, answers, keys, GCS URIs, signed references, and raw response bodies in `worker/src/services/aiProvider.ts` and `worker/src/logging.ts`
- [x] T032 [US3] Run `npm --prefix worker test -- --test-name-pattern='fallback|quota|redact'`; expected result: each quota case has one fallback and every other failure has zero fallback with no sensitive log values

## Phase 6: User Story 4 - Detect Unsafe Configuration Before Work Starts (Priority: P2)

**Goal**: Invalid/inaccessible Vertex or storage configuration stops startup before BullMQ consumes jobs.

**Independent Test**: Inject missing settings and inaccessible bucket metadata; assert startup rejection before any worker constructor is called.

- [x] T033 [P] [US4] Add failing startup validation tests for missing settings, invalid provider, inaccessible bucket, valid bucket, and sanitized diagnostics in `worker/src/services/aiConfig.test.ts` and `worker/src/services/temporaryAudioStorage.test.ts`
- [x] T034 [US4] Export asynchronous AI startup validation that checks parsed settings and bucket metadata without a billable model call in `worker/src/services/aiConfig.ts` and `worker/src/services/temporaryAudioStorage.ts`
- [x] T035 [US4] Invoke AI startup validation before `startWorker()` creates queues/workers and make `/ready` depend on completed AI validation in `worker/src/index.ts`
- [x] T036 [US4] Run `npm --prefix worker test -- --test-name-pattern='startup|bucket validation'`; expected result: invalid configuration fails before queue creation and valid configuration reaches ready state

## Phase 7: Integration, Documentation, and Environment Verification

- [x] T047 Resolve strict `exactOptionalPropertyTypes` errors in `worker/src/services/aiConfig.ts`, `aiErrors.ts`, `temporaryAudioStorage.ts`, and their test fakes; rerun the foundational build/tests
- [x] T048 Fix eager GCS construction for developer-primary essay jobs in `worker/src/services/geminiService.ts`; rerun `npm --prefix worker test`
- [x] T049 Correct the root-level verification command to `npm --prefix worker test`; rerun tests and `docker compose config -q`

- [x] T037 Update implementation-aligned environment, live smoke, negative, lifecycle, IAM, and rollback instructions in `specs/139-vertex-ai-worker-migration/quickstart.md`
- [x] T038 Run `npm --prefix worker test` and `npm --prefix worker run build`; expected result: all existing/new worker tests and strict TypeScript build pass
- [x] T039 Run `docker compose config -q` and `docker compose build worker`; expected result: compose validates and worker image builds with the new dependency/environment
- [x] T040 With valid external ADC, Vertex, and GCS settings run `make up`, `docker compose ps`, `curl -f http://localhost:3001/health`, and `curl -f http://localhost:3001/ready`; record a precise external-secret blocker if live cloud validation is unavailable

## Phase 8: Mandatory Review and Final Gates

- [x] T050 Sanitize provider and storage diagnostics in `worker/src/services/aiProvider.ts`, `worker/src/services/temporaryAudioStorage.ts`, and `worker/src/services/geminiService.ts`; verify raw errors never reach logs
- [x] T051 Delete an uploaded object when metadata retrieval fails in `worker/src/services/temporaryAudioStorage.ts` and cover the path in `worker/src/services/temporaryAudioStorage.test.ts`
- [x] T052 Assess six moderate transitive audit findings from `@google-cloud/storage` using `npm --prefix worker audit fix`; accept only non-breaking dependency changes and record any upstream blocker

- [x] T041 Perform deep architectural/code critique against `specs/139-vertex-ai-worker-migration/spec.md`, `plan.md`, and `tasks.md`; record and resolve each concrete finding in both `achievements.md` and this file
- [x] T042 Run `clean-code-guard` against every changed production file under `worker/src/`, `worker/package.json`, and `docker-compose.yml`; resolve and record all findings
- [x] T043 Run `test-guard` against every changed `worker/src/*.test.ts` and `worker/src/services/*.test.ts`; resolve and record all findings
- [x] T044 Run the final feature tests matrix covering all four user stories, happy paths, configuration/permission/validation failures, cleanup/cancellation/state transitions, and callback regressions; record exact commands/results in `achievements.md`
- [x] T045 Run final `npm --prefix worker run build`, `npm --prefix worker test`, `docker compose config -q`, and applicable Docker health checks; expected result: no feature-introduced error or warning remains
- [x] T046 Run `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/139-vertex-ai-worker-migration`; resolve every failure before the final report
- [x] T053 Align the preparation checklist labels in `specs/139-vertex-ai-worker-migration/tasks.md` with `validate_run.py` and rerun final validation

## Phase 9: Production Completion and Secure Deployment

**Goal**: Mount ADC securely, grant the approved bucket capabilities, deploy the reviewed worker, and close the previous live-cloud blocker without exposing credentials or interrupting the last healthy worker on failure.

**Independent Test**: With the production service-account file mounted read-only, the worker reaches healthy/ready, a Vertex prompt returns `OK`, and an opaque object can be uploaded to and deleted from `gs://massar/ai-analysis/...`; no credential value appears in Git, image history, Compose output, or worker logs.

- [x] T054 [US4] Add `secrets/` and service-account JSON patterns to `.gitignore`, then add a worker-only read-only bind mount from `${GOOGLE_APPLICATION_CREDENTIALS_HOST_PATH:-./secrets/google-application-credentials.json}` to `/run/secrets/google-application-credentials.json` in `docker-compose.yml`
- [x] T055 [US4] Set the worker Compose `GOOGLE_APPLICATION_CREDENTIALS` value to the fixed in-container path when Vertex is enabled, document the host-path variable in `worker/.env.example`, and verify `docker compose config -q` without printing resolved secrets
- [x] T056 [US4] Verify `/Users/mazenelsbagh/Downloads/gen-lang-client-0981415277-c6c34a99e7f1.json` parses as a service-account credential for `ais-gemini-key-ce12231e2f164c7@448008668843.iam.gserviceaccount.com` without printing private-key fields
- [x] T057 [US4] On bucket `massar`, configure a one-day Delete lifecycle and grant the service account bucket-scoped object create/read/delete/list plus bucket metadata read; retain project role `roles/aiplatform.user` on `project-d32eb428-fe1c-4551-b6d`
- [x] T058 [US4] Run a local live-cloud probe with the service account: read bucket lifecycle metadata, upload one opaque `ai-analysis/smoke/` object, read metadata, delete it, and confirm absence; do not print object contents or credentials
- [x] T059 [US4] On `/var/www/nadergorge`, snapshot `.env`, `docker-compose.yml`, the current `massar_worker` image ID, and current health output under a root-only timestamped rollback directory before any production mutation
- [x] T060 [US4] Create `/var/www/nadergorge/secrets` as root mode `0700`, transfer the service-account JSON to `google-application-credentials.json`, set root ownership/mode `0600`, and verify only file path/owner/mode
- [x] T061 [US4] Update production `.env` atomically with Vertex project `project-d32eb428-fe1c-4551-b6d`, location `global`, bucket `massar`, prefix `ai-analysis`, model defaults, fixed container credential path, host credential path, and copy the existing `GEMINI_API_KEY` value to `GEMINI_FALLBACK_API_KEY` without outputting either value
- [ ] T062 [US4] Publish the reviewed feature-139 code and Compose changes through the repository deployment path, preserving unrelated user changes and recording the exact deployed commit
- [ ] T063 [US4] Rebuild/recreate only `massar_worker`, require Docker healthy plus successful `http://127.0.0.1:3001/health` and `/ready`; on failure restore T059 snapshots and the prior worker image before continuing
- [ ] T064 [US1] Run a production-container Vertex `gemini-2.5-flash` exact-`OK` smoke and an opaque GCS upload/delete smoke, then inspect worker logs for credential/private-key/GCS URI leakage and verify the smoke object is absent
- [ ] T065 Perform deep architectural, deployment, IAM, rollback, and secret-handling critique against the final diff and production evidence; record and fix every finding in `achievements.md` and this file
- [ ] T066 Run `clean-code-guard` against every changed production/configuration file, fixing every finding before proceeding
- [ ] T067 Run `test-guard` against every changed test file, or record the required no-test-surface statement if production completion changes no tests
- [ ] T068 Run the complete feature test matrix after T065-T067: worker tests/build, Compose validation/build, live Vertex/GCS probes, production health/readiness, cleanup, permission-negative evidence, and rollback-snapshot verification
- [ ] T069 Run final build verification and `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/139-vertex-ai-worker-migration`; resolve every failure before reporting readiness
- [x] T070 [US4] Resolve the pre-deployment `/ready` HTTP 503 by replacing the worker's .NET-style `DB_CONNECTION_STRING` with the PostgreSQL URI required by Node `pg` in `docker-compose.yml`; production password was verified URI-safe without exposing it

## Dependencies and Execution Order

- T005-T007 set dependencies and deployment inputs.
- T008-T014 are blocking foundations for every story.
- US1 (T015-T022) and US2 (T023-T028) may start after foundations; US3 depends on both provider call surfaces; US4 depends on configuration/storage foundations.
- T037-T040 require all stories. Production completion is sequential: T054-T064 → T065 deep critique → T066 `clean-code-guard` → T067 `test-guard` → T068 feature tests → T069 final build verification/validation.

## Parallel Opportunities

- T006 and T007 affect separate environment files.
- T008/T009 and their test files can be drafted independently before gateway integration.
- US1 storage tests and US2 essay/mind-map test cases touch distinct behavior surfaces when coordinated in the shared test file.
- No parallel execution is allowed for the ordered final quality gates.

## Implementation Strategy

Complete the provider/configuration foundation first. Deliver US1 as the first independently testable increment because it removes the File API constraint. Add essay and mind-map primary routing next, then enable the narrowly classified fallback across all operations, then enforce startup validation. Each story closes with its exact worker test subset before cross-feature tests and Docker validation.

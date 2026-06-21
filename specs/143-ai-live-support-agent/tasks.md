# Tasks: AI Live Support Agent

**Input**: `specs/143-ai-live-support-agent/{spec.md,plan.md,research.md,data-model.md,contracts/,quickstart.md}`

**Tests**: Mandatory for every behavior, permission, data, worker, API, and UI change. Tests precede implementation and state exact expected behavior.

## Spec Kit Preparation Workflow

- [x] T001 Run Arabic feature-intent refinement, record the approved brief in `achievements.md`, and confirm scope before product writes; expected result: approved brief includes AI permissions, confirmation, guest verification, account creation, and irreversible handoff.
- [x] T002 Execute `speckit-specify` and create `specs/143-ai-live-support-agent/spec.md` plus `specs/143-ai-live-support-agent/checklists/requirements.md`; expected result: measurable stories and requirements exist.
- [x] T003 Execute Arabic `speckit-clarify` and encode all five answers in `specs/143-ai-live-support-agent/spec.md`; expected result: zero `NEEDS CLARIFICATION` markers.
- [x] T004 Execute standalone `speckit-plan` and create `specs/143-ai-live-support-agent/{plan.md,research.md,data-model.md,quickstart.md,contracts/}` plus the `AGENTS.md` plan reference; expected result: spec/plan validator passes.
- [x] T005 Execute `speckit-tasks` fallback from `.specify/templates/tasks-template.md` because `.specify/scripts/bash/setup-tasks.sh` is absent; validate this file with `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/143-ai-live-support-agent/tasks.md`.

## Phase 1: Shared Domain and Persistence Foundation

**Purpose**: Create the authoritative schema, catalogs, ports, and migration that block every story.

- [x] T006 [P] Add AI policy/mode/turn/action/verification enums and the `AI` sender plus AI event values in `backend/src/NaderGorge.Domain/Enums/LiveSupportAIEnums.cs`, `LiveSupportSenderType.cs`, and `LiveSupportEventType.cs`; expected result: names match `data-model.md`.
- [x] T007 [P] Add policy and knowledge entities in `backend/src/NaderGorge.Domain/Entities/LiveSupportAI/LiveSupportAIPolicyVersion.cs`, `LiveSupportAIKnowledgeEntry.cs`, `LiveSupportAIKnowledgeRevision.cs`, and `LiveSupportAIPolicyKnowledgeRevision.cs` with immutable publication fields and concurrency version.
- [x] T008 [P] Add conversation and turn entities in `backend/src/NaderGorge.Domain/Entities/LiveSupportAI/LiveSupportAIConversationState.cs` and `LiveSupportAITurn.cs` with unique source/output message references and lifecycle timestamps.
- [x] T009 [P] Add pending action entity in `backend/src/NaderGorge.Domain/Entities/LiveSupportAI/LiveSupportAIPendingAction.cs` with safe proposal, encrypted payload slot, fingerprints, expiry, idempotency, confirming participant, and execution link.
- [x] T010 [P] Add verification entities in `backend/src/NaderGorge.Domain/Entities/LiveSupportAI/LiveSupportAIVerificationPolicyQuestion.cs`, `LiveSupportAIVerificationSession.cs`, and `LiveSupportAIVerificationAttempt.cs` without raw-answer fields.
- [x] T011 Register every new DbSet in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` and `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`; expected result: Application ports can access entities without referencing EF types.
- [x] T012 Configure field limits, enum conversion, concurrency tokens, restrictive FKs, filtered uniqueness, search, and lifecycle indexes in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` exactly as `data-model.md` specifies.
- [x] T013 Create and review EF migration `backend/src/NaderGorge.Infrastructure/Migrations/*_AddAILiveSupportAgent.cs` and model snapshot; expected result: PostgreSQL creates all tables/indexes and no existing live-support data is rewritten.
- [x] T014 [P] Add server-owned stable read/action/lookup/verification catalogs in `backend/src/NaderGorge.Application/Features/LiveSupportAI/Services/LiveSupportAICatalogs.cs`; expected result: catalog keys match `contracts/ai-action-and-verification-catalog.md` and secret fields are absent.
- [ ] T015 [P] Add Application DTOs and ports for policy, context, orchestration, action execution, verification, handoff, and worker jobs in `backend/src/NaderGorge.Application/Features/LiveSupportAI/{Dtos,Interfaces}/`; expected result: no EF/Redis/Google SDK references.
- [x] T016 [P] Add typed errors, telemetry counters/histograms, redaction allowlists, hashing/HMAC, bounded safe JSON, and text normalization helpers in `backend/src/NaderGorge.Application/Features/LiveSupportAI/Services/`; expected result: raw verification answers and secrets have no persistence/log representation.
- [x] T017 [P] Add failing EF model and catalog contract tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIModelTests.cs` and `LiveSupportAICatalogTests.cs`; expected result before implementation: missing entities/indexes/catalog keys fail.
- [ ] T018 Apply the migration to real PostgreSQL in the integration fixture and assert filtered uniqueness, restrictive history FKs, concurrency, and no raw-answer columns in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/LiveSupportAIMigrationTests.cs`.
- [ ] T019 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter FullyQualifiedName~LiveSupportAIModel` and the matching integration filter; expected result: foundation tests pass before US1.

## Phase 2: User Story 1 - Immediate bounded AI support (Priority: P1) 🎯 MVP

**Goal**: AI answers durable student/guest conversations 24/7 using only the active published policy, ranked knowledge, and allowed context.

**Independent Test**: Publish one knowledge-only policy, ask as student and guest with no staff checked in, reconnect, and verify an AI-labeled durable answer with no disabled context.

- [ ] T020 [P] [US1] Add failing policy publication/Admin-only/version tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/PolicyPublicationTests.cs`; expected result: invalid catalog, non-Admin, stale version, and unsafe verification fail.
- [ ] T021 [P] [US1] Add failing context allowlist, knowledge ranking, prompt-injection boundary, and redaction tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/ContextBuilderTests.cs`.
- [ ] T022 [P] [US1] Add failing durable turn/outbox/source-message idempotency tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/TurnOrchestrationTests.cs`.
- [ ] T023 [P] [US1] Add failing worker schema/provider/deadline/redaction tests in `worker/src/services/liveSupportAgent.test.ts` and `worker/src/jobs/processLiveSupportTurn.test.ts` with deterministic provider doubles.
- [ ] T024 [P] [US1] Add failing participant AI disclosure/reconnect E2E in `frontend/tests/e2e/live-support-ai.spec.ts`; expected result: AI works with zero staff and one message survives reload.
- [x] T025 [US1] Implement draft save, publication validation, immutable published versions, active-policy selection, and Admin-only authorization in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIPolicyService.cs` and `backend/src/NaderGorge.API/Controllers/LiveSupportAIAdminController.cs`.
- [ ] T026 [US1] Implement knowledge draft/revision publication and bounded PostgreSQL search in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIKnowledgeService.cs`; expected result: only valid published revisions are retrievable.
- [ ] T027 [US1] Implement allowlisted bounded context packets and safe summary inputs in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIContextBuilder.cs`; expected result: disabled fields, system secrets, and raw verification data never enter packets.
- [ ] T028 [US1] Modify availability/admission and participant message orchestration in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs` so enabled AI admits conversations 24/7, creates `AiActive` state, and creates one turn/outbox record per participant message without human capacity.
- [ ] T029 [US1] Extend `backend/src/NaderGorge.Infrastructure/Background/RedisJobEnqueuer.cs` with explicit fail-closed `live support turn` mapping and deterministic turn job ID; expected result: unknown queue types throw instead of becoming notifications.
- [ ] T030 [US1] Extend `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs` to enqueue `AISupportTurnQueued` only after commit and mark processed only after Redis accepts it.
- [ ] T031 [US1] Add internal claim/complete/fail endpoints with scoped token, idempotency, size/version/mode validation, and safe metadata in `backend/src/NaderGorge.API/Controllers/LiveSupportAIInternalController.cs`.
- [ ] T032 [US1] Extend `worker/src/services/aiProvider.ts` operation union and `worker/src/services/geminiService.ts` with bounded structured `live-support` generation using installed `@google/genai` schema support.
- [ ] T033 [US1] Implement closed decision schema parsing and safe prompt construction in `worker/src/services/liveSupportDecisionSchema.ts` and `worker/src/services/liveSupportAgent.ts`; expected result: malformed/unknown fields fail closed.
- [ ] T034 [US1] Implement dedicated BullMQ processor and separate callback-delivery retry in `worker/src/jobs/processLiveSupportTurn.ts`; enforce 6-second deadline, one transient inference retry, and no inference replay for callback failure.
- [ ] T035 [US1] Register `ai-live-support-turns` queue, worker concurrency, bridge mapping, Bull Board, readiness, and retention in `worker/src/index.ts`; expected result: existing queues remain unchanged.
- [ ] T036 [US1] Persist validated reply decisions as `AI` messages/events/outbox and discard stale disabled/mode callbacks in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIOrchestrator.cs`.
- [ ] T037 [US1] Extend message/conversation DTOs and TypeScript unions in `backend/src/NaderGorge.Application/Features/LiveSupport/Dtos/LiveSupportDtos.cs` and `frontend/src/services/live-support-service.ts` with AI identity, mode, turn state, and deadlines.
- [ ] T038 [US1] Add AI disclosure, message label, thinking/retry/loading states, and human action in `frontend/src/components/live-support/participant/AIConversationStatus.tsx`, `ParticipantConversation.tsx`, and `LiveSupportWidget.tsx` using existing RTL tokens and accessible announcements.
- [x] T039 [US1] Add AI configuration API types/client in `frontend/src/services/live-support-ai-service.ts`; expected result: no admin configuration calls are mixed into participant service.
- [ ] T040 [US1] Run US1 tests: `dotnet test ... --filter FullyQualifiedName~LiveSupportAI`, `npm --prefix worker test`, and Playwright grep `AI disclosure`; expected result: policy/context/turn/worker/reconnect tests pass.

## Phase 3: User Story 2 - Confirmed allowlisted actions (Priority: P1)

**Goal**: AI proposes only published actions; participant cancel/confirm controls exact, stale-safe, exactly-once execution.

**Independent Test**: Allow one action, deny another, cancel then confirm, retry confirmation, revoke policy, and verify zero pre-confirm writes and one post-confirm effect.

- [ ] T041 [P] [US2] Add failing proposal/cancel/expiry/stale/revoked/duplicate tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/AIPendingActionTests.cs`.
- [ ] T042 [P] [US2] Add real PostgreSQL confirmation-vs-handoff and duplicate-confirm concurrency tests in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/AIActionConcurrencyTests.cs`.
- [ ] T043 [P] [US2] Add failing participant confirmation card and keyboard E2E in `frontend/tests/e2e/live-support-ai.spec.ts` with zero write before confirm and one write after duplicate clicks.
- [ ] T044 [US2] Implement AI action metadata adapter and dedicated system actor executor in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIActionExecutor.cs`, reusing MediatR business commands without staff ownership/presence impersonation.
- [ ] T045 [US2] Implement proposal creation and policy/account/payload/state fingerprint binding in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIOrchestrator.cs`; expected result: callback never executes directly.
- [ ] T046 [US2] Implement participant confirm/cancel endpoints and validators in `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs` and `backend/src/NaderGorge.Application/Features/LiveSupportAI/Commands/`; expected result: all AI actions require explicit confirmation.
- [ ] T047 [US2] Add proposal/confirmation/cancel/invalidation SignalR outbox events in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIEventWriter.cs` and document parity with `contracts/ai-support-hub.md`.
- [ ] T048 [US2] Add typed action proposal card with target/effect/consequence/expiry/confirm/cancel in `frontend/src/components/live-support/participant/AIPendingActionCard.tsx` and wire endpoints through `frontend/src/services/live-support-ai-service.ts`.
- [ ] T049 [US2] Add secure secret-input component excluded from transcript/provider state in `frontend/src/components/live-support/participant/AISecureFieldPrompt.tsx`; expected result: password value is cleared on completion/unmount and never enters message store.
- [ ] T050 [US2] Run US2 tests with application/integration filters and Playwright grep `AI action`; expected result: denied action hands off, stale/revoked writes zero, valid duplicate confirmation writes once.

## Phase 4: User Story 3 - Irreversible human handoff and AI resolution (Priority: P1)

**Goal**: User/failure/permission handoff enters existing queue once and permanently stops AI; resolved/inactive conversations close safely.

**Independent Test**: Race callback with handoff/disable, hand off with no staff, accept next shift, confirm resolution, and cancel an inactivity warning.

- [ ] T051 [P] [US3] Add failing explicit/missing-permission/provider-failure/no-staff handoff tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/AIHandoffTests.cs`.
- [ ] T052 [P] [US3] Add real PostgreSQL callback-vs-handoff/disable and single-queue-entry concurrency tests in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/AIHandoffConcurrencyTests.cs`.
- [ ] T053 [P] [US3] Add failing inactivity warning/activity-cancel/auto-close tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/AIInactivityTests.cs`.
- [ ] T054 [P] [US3] Add failing E2E for explicit human request, offline copy, post-handoff stop, staff safe context, resolution, and inactivity warning in `frontend/tests/e2e/live-support-ai.spec.ts`.
- [ ] T055 [US3] Implement serializable irreversible handoff command with routing lock, pending invalidation, one queue entry, reason, audit, and outbox in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIHandoffService.cs`.
- [ ] T056 [US3] Call handoff from explicit participant endpoint, missing permission, verification exhaustion, unsafe/malformed decision, deadline/provider failure, and disable paths in `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs`, `LiveSupportAIInternalController.cs`, and `LiveSupportAIAdminController.cs`.
- [ ] T057 [US3] Extend staff bootstrap/timeline DTOs with safe AI summary, policy version, verification/link state, action outcomes, errors, and handoff reason in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs`; exclude instructions and raw answers.
- [ ] T058 [US3] Implement durable inactivity scanner, one warning, grace-period optimistic close, and activity cancellation in `backend/src/NaderGorge.API/BackgroundServices/LiveSupportAIInactivityBackgroundService.cs`.
- [ ] T059 [US3] Implement participant resolved confirmation and new-conversation behavior in `backend/src/NaderGorge.Application/Features/LiveSupportAI/Commands/ResolveAIConversationCommand.cs` and participant controller.
- [ ] T060 [US3] Add permanent handoff/offline queue/resolution/inactivity UI states in `frontend/src/components/live-support/participant/AIHandoffState.tsx`, `AIInactivityWarning.tsx`, and `LiveSupportLauncher.tsx`; expected result: AI thinking/input never reappears after handoff.
- [ ] T061 [US3] Add staff AI context panel in `frontend/src/components/live-support/staff/AIHandoffContextPanel.tsx` and integrate it into `frontend/src/app/assistant/live-support/AssistantLiveSupportPageClient.tsx`.
- [ ] T062 [US3] Run US3 tests with application/integration filters, worker failure tests, and Playwright grep `AI handoff|AI inactivity`; expected result: handoff wins every race and AI writes zero afterward.

## Phase 5: User Story 4 - Guest verification and account creation (Priority: P2)

**Goal**: Guests get general help, create one account safely, or verify one existing account without enumeration; failure hands off.

**Independent Test**: Complete-value lookup with generic response, successful current-conversation verification, wrong/ambiguous/exhausted paths, and duplicate account-create confirmation.

- [ ] T063 [P] [US4] Add failing lookup non-disclosure, multiple-match, normalization, current-conversation-only, max-attempt, and no-raw-answer tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/GuestVerificationTests.cs`.
- [ ] T064 [P] [US4] Add real PostgreSQL verification/account-link and account-create idempotency tests in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/GuestVerificationIntegrationTests.cs`.
- [ ] T065 [P] [US4] Add failing guest verification/account creation E2E, including no network candidate list and no password message, in `frontend/tests/e2e/live-support-ai.spec.ts`.
- [ ] T066 [US4] Implement safe complete-key lookup, generic response, HMAC lookup storage, ambiguity failure, and per-IP/session/conversation throttling in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIVerificationService.cs`.
- [ ] T067 [US4] Implement backend-selected challenges, in-memory normalized comparison, attempt-only audit, success link, current-conversation scope, and failure handoff in the same verification service.
- [ ] T068 [US4] Add lookup/answer endpoints with constant generic response shapes and rate-limit policies in `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs` and `backend/src/NaderGorge.API/Program.cs`.
- [ ] T069 [US4] Implement guided account proposal and exactly-once validated create/link command in `backend/src/NaderGorge.Application/Features/LiveSupportAI/Commands/ConfirmAIAccountCreationCommand.cs`, reusing current registration/admin validation.
- [ ] T070 [US4] Add guest lookup/challenge/remaining-attempt UI without hints in `frontend/src/components/live-support/participant/AIGuestVerification.tsx`; expected result: candidate identity never renders before success.
- [ ] T071 [US4] Add structured account proposal and secure password confirmation UI in `frontend/src/components/live-support/participant/AIAccountCreationProposal.tsx`; expected result: retries create one account and secret never enters transcript/store.
- [ ] T072 [US4] Run US4 application/integration/E2E tests; expected result: successful link applies only to current conversation and every unsafe/ambiguous/exhausted path links nothing and hands off.

## Phase 6: User Story 5 - Admin configuration, preview, and supervision (Priority: P2)

**Goal**: Built-in Admin controls all AI policy/knowledge/catalog/verification settings, tests zero-write preview, and investigates outcomes.

**Independent Test**: Non-Admin denied; Admin edits draft, preview causes zero writes, publish activates, activity/evidence reconstructs a turn, and disable hands off active work within five seconds.

- [ ] T073 [P] [US5] Add failing built-in Admin-only endpoint tests for every config/knowledge/preview/publish/disable/evidence action in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/AIAdminAuthorizationTests.cs`.
- [ ] T074 [P] [US5] Add failing preview zero-message/account/action/queue/assignment tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/AIPreviewTests.cs`.
- [ ] T075 [P] [US5] Add failing activity/evidence/redaction/metrics tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/AIAdminEvidenceTests.cs`.
- [ ] T076 [P] [US5] Add failing Admin tab accessibility, validation, draft/published conflict, preview, evidence, and disable E2E in `frontend/tests/e2e/live-support-ai.spec.ts`.
- [ ] T077 [US5] Complete Admin config/knowledge/catalog/preview/publish/disable/activity/evidence endpoints in `backend/src/NaderGorge.API/Controllers/LiveSupportAIAdminController.cs` and corresponding Application commands/queries.
- [ ] T078 [US5] Implement zero-write preview orchestration with synthetic context and prohibited command guards in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIPreviewService.cs`.
- [ ] T079 [US5] Implement safe activity aggregates and chronological evidence query in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAIEvidenceService.cs`; expected result: every sampled decision is reconstructable without sensitive values.
- [ ] T080 [P] [US5] Create route shell and sub-navigation in `frontend/src/app/admin/live-support/ai/page.tsx` and `frontend/src/app/admin/live-support/ai/AdminAISupportPageClient.tsx` with built-in Admin route guard.
- [ ] T081 [P] [US5] Create overview and emergency-disable modules in `frontend/src/components/live-support/ai-admin/AIOverview.tsx` and `AIDisableControl.tsx` with readiness/errors and confirmation.
- [ ] T082 [P] [US5] Create instruction and knowledge modules in `frontend/src/components/live-support/ai-admin/AIPolicyEditor.tsx` and `AIKnowledgeManager.tsx` with draft/published/version-conflict states.
- [ ] T083 [P] [US5] Create data/action and verification modules in `frontend/src/components/live-support/ai-admin/AIDataActionSelector.tsx` and `AIVerificationPolicyEditor.tsx` using server catalogs and publication validation.
- [ ] T084 [P] [US5] Create zero-write preview and evidence modules in `frontend/src/components/live-support/ai-admin/AIPreview.tsx` and `AIActivityEvidence.tsx` with loading/empty/error/filter states.
- [x] T085 [US5] Add Admin navigation entry and role-only visibility in `frontend/src/components/admin/AdminShellChrome.tsx`, `frontend/src/packages/admin/navigation.tsx`, and `frontend/src/app/admin/layout.tsx` without adding a delegable custom permission.
- [ ] T086 [US5] Run US5 backend and Playwright tests; expected result: non-Admin always 403, preview writes zero, publish version is active, evidence is redacted, and disable completes gate within five seconds.

## Phase 7: Cross-Cutting Hardening and Observability

- [ ] T087 Add AI queue/provider/callback/turn/action/verification/handoff metrics and safe structured logs in `backend/src/NaderGorge.Application/Features/LiveSupportAI/Services/LiveSupportAITelemetry.cs` and `worker/src/services/liveSupportTelemetry.ts`; expected result: no prompt/PII/secret values.
- [ ] T088 Add recovery for stale queued/processing turns, callback delivery, expired actions/verifications, dead-letter surfacing, and disable reconciliation in `backend/src/NaderGorge.API/BackgroundServices/LiveSupportAIRecoveryBackgroundService.cs` and worker queue events.
- [ ] T089 Add worker security/startup/readiness validation for queue concurrency, callback token, provider deadline, and decision schema in `worker/src/security.ts`, `worker/src/services/aiConfig.ts`, and `docker-compose.yml`.
- [ ] T090 Add backend rate limits and request-size limits for participant AI messages, handoff, confirmation, lookup/answer, account creation, admin preview, and internal callbacks in `backend/src/NaderGorge.API/Program.cs`.
- [ ] T091 Add static redaction regression scans and endpoint/catalog parity checks in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/AISecurityContractTests.cs` and `worker/src/services/liveSupportSecurity.test.ts`.
- [ ] T092 Run `python3 .agents/skills/speckit-all/scripts/validate_spec_plan_quality.py --spec-dir specs/143-ai-live-support-agent` and `validate_tasks_quality.py`; expected result: both pass after implementation edits.

## Phase 8: Mandatory Review and Verification Tail

**Order is mandatory: deep critique fixes → clean-code-guard → test-guard → feature tests → final build/Docker verification.**

- [ ] T093 Perform deep architectural, security, concurrency, privacy, data, worker, and UI/UX critique across every changed file; record every finding in `achievements.md` and this file, then fix and verify each before checking T093.
- [ ] T094 Run `clean-code-guard` guard-pass on every changed production file after T093; record and resolve all findings in `achievements.md` and this file before checking T094.
- [ ] T095 Run `test-guard` on every changed test file after T094; record and resolve all findings in `achievements.md` and this file before checking T095.
- [ ] T096 Run final feature tests from `specs/143-ai-live-support-agent/quickstart.md`: backend application/integration AI/live-support filters, worker tests, Chromium/WebKit `frontend/tests/e2e/live-support-ai.spec.ts`; expected result: all five user stories and security/concurrency regressions pass.
- [ ] T097 Run final static builds: `dotnet build backend/NaderGorge.sln`, `npm --prefix worker test`, `npm --prefix frontend run lint`, `(cd frontend && npx tsc --noEmit)`, and `npm --prefix frontend run build`; expected result: zero introduced errors or warnings.
- [ ] T098 Run migration/runtime Docker gate: `docker compose config -q`, `make migrate`, `make up`, `make ps`, backend/worker readiness curls, frontend surfaces, SignalR negotiate, dedicated queue smoke, configured-provider reply, and no-staff handoff; record exact evidence.
- [ ] T099 Complete manual four-role QA and negative security checklist from `quickstart.md`; record pass/fail or explicit external-only blocker in `achievements.md`.
- [ ] T100 Run `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/143-ai-live-support-agent`; expected result: all artifacts, phases, evidence, order, and checkboxes pass before final reporting.

## Dependencies and Execution Order

1. T006–T019 foundation blocks all stories.
2. US1 T020–T040 delivers policy, context, worker, durable reply, and participant AI states.
3. US2 T041–T050 depends on US1 turn decisions and adds confirmed actions.
4. US3 T051–T062 depends on US1 and must complete before guest failure paths.
5. US4 T063–T072 depends on US1 and US3 handoff, plus US2 confirmation for account creation.
6. US5 T073–T086 depends on all policy/state/evidence capabilities but its component files can proceed in parallel after contracts stabilize.
7. T087–T092 hardening follows all stories. T093–T100 order is strict.

### User story completion gates

- **US1**: Student/guest AI answer works 24/7, survives reconnect, and reads only enabled context.
- **US2**: Zero pre-confirm writes; allowed action exactly once; denied/stale/revoked path cannot write.
- **US3**: One irreversible handoff, zero later AI messages, complete safe staff context, resolution and inactivity closure.
- **US4**: No candidate disclosure; current-conversation verification; one account creation; unsafe paths hand off.
- **US5**: Built-in Admin only; draft/preview/publish/disable/evidence complete; preview zero-write.

### Parallel opportunities

- T006–T010 entities, T014–T018 contracts/tests can run in parallel by file.
- Tests marked `[P]` within each story can run in parallel before their implementation wave.
- Worker US1 tasks T032–T035 can proceed alongside backend policy/context T025–T031 after DTO contracts stabilize.
- Admin US5 components T080–T084 can proceed in parallel after service types are fixed.
- Do not parallelize edits to `AppDbContext.cs`, `LiveSupportService.cs`, `LiveSupportParticipantController.cs`, `worker/src/index.ts`, or the same E2E file.

## Implementation Strategy

### MVP first

1. Foundation T006–T019.
2. US1 T020–T040, with AI knowledge-only and no action keys enabled.
3. Demonstrate 24/7 student/guest answer and reconnect before enabling actions.

### Incremental delivery

1. AI reply foundation, feature disabled publicly.
2. Confirmed allowlisted actions.
3. Irreversible handoff and resolution/inactivity.
4. Guest verification/account creation.
5. Admin preview/evidence and staged enablement.
6. Hardening, mandatory guards, full tests, Docker, manual acceptance.

## Notes

- All tests are written first and expected to fail for the intended missing behavior.
- Existing unrelated working-tree changes from features 142 and player settings remain preserved.
- No task grants AI direct database, Redis, provider tool, global admin, or custom-role configuration authority.
- No task stores raw verification answers, passwords, tokens, prompts, or unrestricted provider output.

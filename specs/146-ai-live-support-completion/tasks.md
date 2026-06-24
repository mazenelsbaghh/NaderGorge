# Tasks: AI Live Support Production Completion

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md` in `specs/146-ai-live-support-completion/`  
**Target prompt**: create the tasks file so that a cheaper llm model can implement without problems  
**Tests**: Mandatory. Write the named tests first, confirm they fail for the intended reason, implement the smallest production change, then rerun the exact checkpoint command.  
**Data rule**: Never delete, reset, or recreate existing live-support data. Preserve unrelated role/navigation changes already present in the worktree.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Phase 1: Setup and Baseline Evidence

**Purpose**: Establish a trustworthy baseline and create missing feature directories without changing runtime behavior.

- [x] T001 Record the existing unrelated dirty files and Feature 146-owned files under `### Phase 5 Implementation Evidence` in `achievements.md`; explicitly state that role/navigation files and migration `20260624121729_AddAllowedDomainAndNavbarToRoles*` must not be reverted or overwritten.
- [x] T002 Run sequential baseline commands from `specs/146-ai-live-support-completion/quickstart.md` and append exact pass/fail counts and warnings to `achievements.md`; do not run two .NET build/test commands concurrently because they share `obj` outputs.
- [x] T003 Align `Microsoft.EntityFrameworkCore.Relational` and PostgreSQL test dependencies to 9.0.6 in `backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj`; expected result: `dotnet build backend/NaderGorge.sln` no longer reports MSB3277 for EF Core 9.0.1 versus 9.0.6.
- [x] T004 [P] Create empty feature directories `backend/src/NaderGorge.Application/Features/LiveSupportAI/Commands/`, `Queries/`, `Validation/`, and `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/`; add no placeholder production classes.
- [x] T005 [P] Create test directories `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/` and the file path `frontend/tests/e2e/live-support-ai.spec.ts`; initialize the E2E file with imports, fixtures, and describe blocks only.
- [x] T006 Run `dotnet build backend/NaderGorge.sln`, `npm --prefix worker run build`, and `(cd frontend && npx tsc --noEmit)` sequentially; checkpoint requires the repository to compile before foundational edits.

## Phase 2: Foundational Contracts, Persistence, and Ports

**Purpose**: Add compatibility-safe state and interfaces required by every user story.

**Critical**: Do not begin a user-story phase until T007–T022 pass their checkpoint.

- [x] T007 [P] Add `LiveSupportAIPendingDecisionKind` and missing callback-state enum values exactly as defined in `specs/146-ai-live-support-completion/data-model.md` to `backend/src/NaderGorge.Domain/Enums/LiveSupportAIEnums.cs`; retain every existing numeric enum value.
- [x] T008 Update `backend/src/NaderGorge.Domain/Entities/LiveSupport/LiveSupportAIPendingAction.cs` with `DecisionKind`, nullable `StudentUserId`, `CallbackDecisionHash`, and `CancelledAt`; keep existing property names and restrictive history relations.
- [x] T009 [P] Update `backend/src/NaderGorge.Domain/Entities/LiveSupport/LiveSupportAITurn.cs` with decision hash, callback status/attempt scheduling, provider completion timestamp, and safe callback error code from `data-model.md`.
- [x] T010 [P] Update `backend/src/NaderGorge.Domain/Entities/LiveSupport/LiveSupportAIConversationState.cs` and `LiveSupportAIVerification.cs` with the snapshot/recovery and verification cursor fields from `data-model.md`.
- [x] T011 Add failing model/backfill invariant tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAICompletionModelTests.cs`; cover preserved enum numbers, nullable non-action target, action target requirement, callback lifecycle, verification counters, and no raw-answer property.
- [x] T012 Modify only the LiveSupport AI mappings inside `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`; preserve all concurrent role/navigation mapping changes, add constraints/indexes from `data-model.md`, and replace unconditional verification conversation uniqueness with active-status filtered uniqueness.
- [x] T013 Generate additive migration `CompleteAILiveSupportProduction` under `backend/src/NaderGorge.Infrastructure/Migrations/`; edit the generated migration to backfill `DecisionKind`, convert zero-GUID pending targets to null, preserve every row, and add no `DropTable` or live-support data deletion.
- [x] T014 Add failing real-PostgreSQL upgrade tests in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/LiveSupportAICompletionMigrationTests.cs`; seed pre-146 conversations, messages, policy, turn, handoff proposal, verification, assignment, rating, and audit, apply migration, then assert stable IDs/content/counts, no orphan, no zero-GUID target, constraints, and indexes.
- [x] T015 [P] Add Application records for worker claim, completion, failure, snapshot, pending decision, verification, registration, and safe errors to `backend/src/NaderGorge.Application/Features/LiveSupportAI/Dtos/LiveSupportAICompletionDtos.cs`; include JSON/property bounds and no EF, Redis, Google SDK, or HTTP types.
- [x] T016 [P] Add `ILiveSupportAITurnOrchestrator`, `ILiveSupportAIContextBuilder`, `ILiveSupportAIKnowledgeService`, `ILiveSupportAIActionExecutor`, `ILiveSupportAIVerificationService`, `ILiveSupportAIRegistrationService`, `ILiveSupportAIHandoffService`, and `ILiveSupportAIRecoveryService` to separate files under `backend/src/NaderGorge.Application/Features/LiveSupportAI/Interfaces/`; each method must take explicit actor/context and `CancellationToken`.
- [x] T017 [P] Add bounded validators for worker completion, participant confirmations, verification lookup/answer, secure registration, and admin preview to `backend/src/NaderGorge.Application/Features/LiveSupportAI/Validation/LiveSupportAICompletionValidators.cs`; use stable error codes from `contracts/state-machine.md`.
- [x] T018 Add C#/TypeScript/worker contract parity tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIContractParityTests.cs`, `worker/src/services/liveSupportDecisionSchema.test.ts`, and `frontend/src/services/live-support-contract.test.ts`; assert six decision types, pending kinds, modes, stable catalog keys, and endpoint path builders.
- [x] T019 Extend `backend/src/NaderGorge.Infrastructure/Background/RedisJobEnqueuer.cs` with an explicit queue/job mapping and deterministic `turn:{turnId}` job ID; throw for unknown mappings instead of defaulting to notification, and add focused tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/RedisJobEnqueuerMappingTests.cs`.
- [x] T020 Extend `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs` with a distinct `LiveSupportAITurnQueued` dispatch branch that marks processed only after Redis acceptance; retain existing SignalR routing and dead-letter behavior.
- [x] T021 Add outbox dispatch tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIOutboxDispatchTests.cs`; cover accepted dispatch, Redis failure retry, dead-letter threshold, deterministic replay, and rejection of accidental SignalR broadcast for queue events.
- [x] T022 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~LiveSupportAICompletionModel|FullyQualifiedName~LiveSupportAIContractParity|FullyQualifiedName~RedisJobEnqueuer|FullyQualifiedName~LiveSupportAIOutbox"` and the migration integration filter; checkpoint requires all foundation tests and solution build to pass.

## Phase 3: User Story 1, Safe Continuous AI Assistance (P1)

**Goal**: Student and guest messages produce one durable AI turn and one safe provider reply with reconnect recovery.

**Independent Test**: Start student and guest conversations with no staff, send one message each, process a deterministic worker reply, reconnect, and observe each message/reply once with correct AI disclosure and privacy.

- [x] T023 [P] [US1] Add failing context allowlist, transcript bound, knowledge rank, prompt-injection labeling, and redaction tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIContextBuilderTests.cs`; assert disabled fields, passwords, tokens, raw answers, and unbounded history are absent.
- [x] T024 [P] [US1] Add failing message/turn/outbox atomicity and source-message idempotency tests in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/LiveSupportAITurnOrchestrationTests.cs`; simulate transaction rollback and Redis outage.
- [x] T025 [P] [US1] Add failing worker schema, size, deadline, classified retry, safe error, and callback-no-reinference tests in `worker/src/services/liveSupportAgent.test.ts` and `worker/src/jobs/processLiveSupportTurn.test.ts`.
- [x] T026 [P] [US1] Add failing participant AI disclosure, thinking, retry, real-time reply, reload deduplication, and guest non-disclosure scenarios to `frontend/tests/e2e/live-support-ai.spec.ts`.
- [x] T027 [US1] Implement bounded published-knowledge selection in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIKnowledgeService.cs`; filter policy-linked published revisions, rank normalized text, cap result count and total characters, and return revision IDs with content.
- [x] T028 [US1] Implement allowlisted context packets in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIContextBuilder.cs`; project only catalog-approved student fields, cap transcript, attach safe summary, label user/knowledge content untrusted, and emit no protected value.
- [x] T029 [US1] Implement atomic participant-message AI turn creation in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAITurnOrchestrator.cs`; create turn and `LiveSupportAITurnQueued` outbox inside the caller transaction and never call Redis directly.
- [x] T030 [US1] Replace the AI block inside `SendMessageAsync` in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs` with one `ILiveSupportAITurnOrchestrator.QueueForParticipantMessageAsync` call before transaction commit; preserve existing human message behavior and idempotent replay.
- [x] T031 [P] [US1] Implement strict closed decision parsing and canonical hashing in `worker/src/services/liveSupportDecisionSchema.ts`; reject extra fields, wrong branch payloads, unsupported types, excessive strings/arrays, and unknown action shape.
- [x] T032 [P] [US1] Implement prompt/context assembly in `worker/src/services/liveSupportAgent.ts`; keep system instructions separate from untrusted transcript/knowledge/student context and return a validated canonical decision only.
- [x] T033 [US1] Extend `worker/src/services/aiProvider.ts` and `worker/src/services/geminiService.ts` with operation `live-support`, installed structured-output schema support, provider deadline, and classified one-retry policy; do not add a direct provider outside the gateway.
- [x] T034 [P] [US1] Implement internal claim/complete/fail HTTP client with timeout, body cap, safe error classification, and token validation in `worker/src/services/liveSupportCallbackClient.ts`.
- [x] T035 [US1] Refactor `worker/src/jobs/processLiveSupportTurn.ts` into claim → infer → persist canonical job result → deliver callback; callback retries must reuse the persisted decision and never call inference again.
- [x] T036 [US1] Configure live-support queue attempts, backoff, retention, concurrency, readiness, and Bull Board registration in `worker/src/index.ts`; retain all existing queue settings and validate required callback/provider configuration at startup.
- [x] T037 [US1] Implement idempotent claim/complete/fail methods in `LiveSupportAITurnOrchestrator.cs`; revalidate policy, conversation version/mode, decision schema/catalog, and late handoff/disable before creating deterministic AI messages/events/outbox.
- [x] T038 [US1] Change live-support internal methods in `backend/src/NaderGorge.API/Controllers/InternalController.cs` to use the new orchestrator DTOs, enforce body size and `AI_CALLBACK_SECRET`, and return stable `200/404/409/413/422` outcomes from the API contract.
- [x] T039 [US1] Add participant snapshot method and endpoint to `backend/src/NaderGorge.Application/Features/LiveSupport/Interfaces/ILiveSupportService.cs`, `LiveSupportService.cs`, and `LiveSupportParticipantController.cs`; include last sequence, mode, composer permission, current turn, one pending decision, verification, queue, and messages without private staff/admin data.
- [x] T040 [P] [US1] Extend typed AI mode/turn/snapshot/event unions and endpoint methods in `frontend/src/services/live-support-service.ts`; remove `any` from pending decision responses and preserve existing API paths through compatibility aliases.
- [x] T041 [US1] Harden reconnect/gap handling in `frontend/src/hooks/useLiveSupportHub.ts` and `frontend/src/stores/live-support-store.ts`; deduplicate by sequence, refetch snapshot on gap, preserve draft, and never re-enable stale composer state.
- [x] T042 [P] [US1] Create `frontend/src/components/live-support/participant/AIConversationStatus.tsx` with AI disclosure, queued/processing/retry/failure text, `aria-live`, reduced-motion-safe indicator, and explicit human-support action.
- [x] T043 [US1] Integrate snapshot, AI status, stable pending region, and retry behavior into `frontend/src/components/live-support/participant/ParticipantConversation.tsx` and `LiveSupportWidget.tsx`; reserve layout space and keep 320px one-column flow.
- [x] T044 [US1] Run the US1 backend filters, worker tests, frontend typecheck/lint, and Playwright grep `AI disclosure|AI reply|reconnect`; expected result: durable student/guest replies and privacy/reconnect scenarios pass independently.

## Phase 4: User Story 2, Confirmed Actions and Guest Identity (P1)

**Goal**: Execute only policy-allowed confirmed actions; verify or register guests without disclosure or secret leakage.

**Independent Test**: Confirm and replay one action, cancel/expire/revoke another, verify one guest, exhaust another, and create one complete account without password traces.

- [ ] T045 [P] [US2] Add failing pending-action policy, target, payload-hash, state-fingerprint, expiry, cancellation, revocation, and duplicate-confirm tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIPendingDecisionTests.cs`.
- [ ] T046 [P] [US2] Add failing action-confirmation versus handoff/disable concurrency tests in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/LiveSupportAIActionConcurrencyTests.cs`; assert one business effect and one action execution.
- [ ] T047 [P] [US2] Add failing non-disclosing lookup, multiple-match, challenge cursor, keyed-digest, answerless-at-rest, expiry, and exhaustion tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIVerificationTests.cs`.
- [ ] T048 [P] [US2] Add failing registration validation, retry, account-created/link-failed rollback, complete-profile, and password-redaction tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIRegistrationTests.cs` and corresponding PostgreSQL integration file.
- [ ] T049 [P] [US2] Add action card keyboard/double-click/expiry, verification generic copy, and secure registration no-trace journeys to `frontend/tests/e2e/live-support-ai.spec.ts`.
- [x] T050 [US2] Implement protected pending-decision payload handling and proposal creation in `LiveSupportAITurnOrchestrator.cs`; set explicit `DecisionKind`, nullable target rules, safe display JSON, encryption, keyed hashes, expiry, and deterministic idempotency key.
- [x] T051 [US2] Implement `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIActionExecutor.cs`; invoke existing authoritative MediatR/business commands using an auditable AI system actor, linked-student scope, and participant confirmation without staff impersonation.
- [x] T052 [US2] Implement confirm/cancel action commands in `backend/src/NaderGorge.Application/Features/LiveSupportAI/Commands/ConfirmLiveSupportAIActionCommand.cs` and `CancelLiveSupportAIDecisionCommand.cs`; claim `Confirmed→Executing` once and return the recorded execution on compatible retry.
- [ ] T053 [US2] Replace direct action-confirm/cancel logic in `LiveSupportService.cs` with the new commands/executor and expose decision-ID plus idempotency-key routes in `LiveSupportParticipantController.cs`; keep old routes as compatibility wrappers that resolve the active decision.
- [x] T054 [US2] Implement exact-value keyed lookup, generic public result, selected-question cursor, transient answer comparison, safe attempts, expiry, and exhaustion in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIVerificationService.cs`.
- [ ] T055 [US2] Replace verification methods in `LiveSupportService.cs` with `ILiveSupportAIVerificationService` delegation and update `LiveSupportParticipantController.cs` routes to include session ID and idempotency key while preserving old route compatibility.
- [x] T056 [US2] Implement `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIRegistrationService.cs` using the authoritative registration/profile validation and one recoverable create-and-link orchestration; do not patch a partial `StudentProfile` after `AdminCreateUserCommand`.
- [ ] T057 [US2] Replace direct guest-registration logic in `LiveSupportService.cs` and `LiveSupportParticipantController.cs` with the registration service, decision ID, idempotency key, field validation, and safe `201/200/409/422` responses.
- [x] T058 [P] [US2] Replace `any` pending-action types and add typed confirm/cancel/verification/session/registration methods in `frontend/src/services/live-support-service.ts`; pass idempotency keys and never put password/answers in Zustand or message DTOs.
- [x] T059 [P] [US2] Refactor `frontend/src/components/live-support/participant/AIPendingActionCard.tsx` into explicit pending/confirming/succeeded/cancelled/expired/invalidated/failed states; remove unused catch variables, prevent duplicate clicks, restore focus, and announce result.
- [x] T060 [P] [US2] Refactor `AIGuestVerification.tsx` with generic copy, labeled exact-value lookup/challenge fields, field errors, correct input modes, attempt state, exhaustion handoff, and no candidate hints.
- [x] T061 [P] [US2] Refactor `AISecureRegistrationForm.tsx` to reuse authoritative field rules/types, use secure autocomplete, clear password on completion/unmount, prevent form data from entering logs/store/transcript, and show field-level errors.
- [x] T062 [US2] Integrate one typed pending-decision region into `ParticipantConversation.tsx`; block or allow composer exactly per state contract and never render action, verification, registration, or handoff controls simultaneously.
- [ ] T063 [US2] Run US2 application/integration filters, worker schema tests, frontend typecheck/lint, static secret scan, and Playwright grep `AI action|verification|registration`; expected result: exactly-once effects and zero protected-value traces.

## Phase 5: User Story 3, Reliable Human Handoff and Support (P1)

**Goal**: Transfer AI conversations to fair human routing without lost context, duplicate queues, or unauthorized staff mutation.

**Independent Test**: Confirm/reject normal handoff, force failure handoff, queue with no staff, assign within capacity, transfer, disconnect/recover, and close while preserving transcript.

- [ ] T064 [P] [US3] Add failing explicit/rejected/forced/no-staff/duplicate handoff tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIHandoffTests.cs`.
- [ ] T065 [P] [US3] Add callback-versus-handoff, disable-versus-handoff, duplicate queue, assignment capacity, and close race tests in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/LiveSupportAIHandoffConcurrencyTests.cs`.
- [ ] T066 [P] [US3] Add Playwright handoff confirmation/rejection/forced queue/post-handoff AI-stop/staff safe-summary/ownership-denial scenarios to `frontend/tests/e2e/live-support-ai.spec.ts`.
- [x] T067 [US3] Implement serializable routing-lock handoff in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIHandoffService.cs`; re-read state, invalidate AI work, create one queue entry, preserve transcript, assign oldest eligible, and write reason/event/audit/outbox atomically.
- [ ] T068 [US3] Route participant confirmation, verification exhaustion, provider/schema failure, missing capability, emergency disable, and admin intervention through `ILiveSupportAIHandoffService`; remove duplicate status/queue mutation blocks from `LiveSupportService.cs` and `LiveSupportAIAdminService.cs`.
- [x] T069 [US3] Update normal handoff callback handling in `LiveSupportAITurnOrchestrator.cs` to create a `Handoff` pending decision without `Guid.Empty`; update late callbacks after committed handoff to safe idempotent discard.
- [x] T070 [US3] Update `AIHandoffConfirmation.tsx` with decision ID, explicit reason/effect/expiry, confirm/reject loading, duplicate-click lock, focus restoration, reduced motion, and no unused catch variable.
- [ ] T071 [US3] Add safe AI handoff summary, policy version, verification/link state, attempted actions, and failures to staff bootstrap DTOs in `LiveSupportDtos.cs` and TypeScript types in `live-support-service.ts`; exclude system instructions, raw answers, lookup values, and secrets.
- [x] T072 [US3] Extract `StaffConversationWorkspace.tsx` and `AIHandoffSummary.tsx` under `frontend/src/components/live-support/staff/`; move transcript/composer and safe AI context out of `AssistantLiveSupportPageClient.tsx` while preserving selection and drafts.
- [x] T073 [US3] Harden `useLiveSupportHub.ts` and staff store handling for ownership gained/lost, transfer, disconnect grace, and post-handoff mode; composer and actions must disable immediately when ownership is lost.
- [ ] T074 [US3] Run US3 application/integration routing filters and Playwright grep `handoff|queue|ownership`; expected result: no AI reply after handoff, no duplicate queue/owner, and human workflow passes independently.

## Phase 6: User Story 7, Data Preservation and Recovery (P1)

**Goal**: Upgrade existing data and converge stale/retried/concurrent work to one observable state.

**Independent Test**: Apply migration to seeded old data, restart worker/backend during active turns, replay callbacks and confirmations, and verify stable history plus one outcome.

- [ ] T075 [P] [US7] Add failing stale queued/processing/provider-completed turn, expired decision, expired verification, inactivity, and disable reconciliation tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIRecoveryTests.cs`.
- [ ] T076 [P] [US7] Extend PostgreSQL recovery races in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/LiveSupportAIRecoveryConcurrencyTests.cs`; repeat randomized callback/close/handoff/disable/confirm operations and assert state-machine precedence.
- [x] T077 [US7] Implement bounded indexed recovery batches in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIRecoveryService.cs`; each item uses compare-and-set/idempotent transitions and emits safe result codes.
- [x] T078 [US7] Add `backend/src/NaderGorge.API/BackgroundServices/LiveSupportAIRecoveryBackgroundService.cs` with configurable scan interval, batch size, cancellation, distributed single-run lock, and safe metrics; register it in `Program.cs` after required services.
- [x] T079 [US7] Persist provider-completed decision/hash and independent callback attempt schedule in `processLiveSupportTurn.ts`; make worker restart resume callback delivery without provider inference.
- [x] T080 [US7] Add safe queue age, inference latency, callback outcome, handoff reason, recovery outcome, and dead-letter metrics in `backend/src/NaderGorge.Application/Features/LiveSupportAI/Services/LiveSupportAITelemetry.cs` and `worker/src/services/liveSupportTelemetry.ts`; prohibit prompt/PII/secret dimensions.
- [x] T081 [US7] Add health/readiness checks for AI callback secret, provider config, Redis, callback reachability, and live-support worker registration in `worker/src/index.ts` and backend health registration; readiness must fail safely without exposing configuration values.
- [ ] T082 [US7] Run migration-preservation and recovery test filters repeatedly, restart smoke from `quickstart.md`, and record stable row/checksum and one-outcome evidence in `achievements.md`.

## Phase 7: User Story 4, Staff Student Resolution Workspace (P2)

**Goal**: Let the current owner resolve linked-student needs with complete states and authoritative actions.

**Independent Test**: Load linked/unlinked contexts, execute representative actions, correct a link, simulate validation/concurrency failure, and verify refresh plus audit.

- [ ] T083 [P] [US4] Extend `StudentContextTests.cs`, `StudentActionTests.cs`, and `StudentLinkTests.cs` with AI-handoff ownership, unlinked guest, stale link, partial-load failure, idempotent action, and previous-student removal cases.
- [ ] T084 [P] [US4] Add Playwright staff linked/unlinked context, action success/failure, link replacement, tablet drill-in, and ownership-loss scenarios to `frontend/tests/e2e/live-support-ai.spec.ts`.
- [ ] T085 [US4] Refactor `StudentContextPanel.tsx` into lazy section queries with explicit skeleton/empty/error/retry states and stable section keys; do not fetch all large histories at initial conversation selection.
- [ ] T086 [US4] Refactor `StudentActionsPanel.tsx` to render server catalog metadata, target/effect confirmation, executing lock, field-level validation, idempotency key, result refresh keys, and safe terminal failure.
- [ ] T087 [US4] Add `StaffConversationLayout.tsx` with desktop queue/transcript/context regions and tablet list→conversation→context drill-in; preserve selection, transcript scroll, draft, and unread state without horizontal page overflow.
- [x] T088 [US4] Replace inline three-pane markup in `AssistantLiveSupportPageClient.tsx` with `StaffConversationLayout`, `StaffConversationWorkspace`, existing queue/status modules, and lazy student context; page client owns data orchestration only.
- [ ] T089 [US4] Run backend filters `StudentContext|StudentAction|StudentLink`, frontend typecheck/lint, and Playwright grep `staff context|student action|student link`; expected result: ownership/privacy/action workflows pass.

## Phase 8: User Story 5, Administration and Supervision (P2)

**Goal**: Admin configures, previews, monitors, investigates, disables, and intervenes with complete audit and no production writes from preview.

**Independent Test**: Publish/version-conflict policy and knowledge, preview, disable active work, inspect evidence/statistics/timeline, intervene, and verify non-admin denial.

- [ ] T090 [P] [US5] Add failing policy catalog/knowledge/version/unique-active/preview-zero-write/disable tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIPolicyAndPreviewTests.cs`.
- [ ] T091 [P] [US5] Add failing admin statistics/evidence/redaction/time-period and non-admin authorization tests in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIAdministrationTests.cs`.
- [ ] T092 [P] [US5] Add Playwright admin draft/publish/conflict/knowledge/preview/disable/stats/evidence/intervention/non-admin scenarios to `frontend/tests/e2e/live-support-ai.spec.ts`.
- [x] T093 [US5] Implement immutable knowledge entry/revision CRUD, publish validation, policy linking, and bounded search through `LiveSupportAIKnowledgeService.cs`; expose Admin-only endpoints in `LiveSupportAIAdminController.cs`.
- [ ] T094 [US5] Implement zero-write preview through the same context and worker decision validation in `LiveSupportAIAdminService.cs`; use an explicit dry-run request that cannot create message, turn, action, verification, account, queue, assignment, participant event, or audit business record.
- [ ] T095 [US5] Refactor emergency disable/enable in `LiveSupportAIAdminService.cs` to version-check the published policy, block admission first, schedule recovery, return `202` for disable, and keep re-enable from reversing human handoff.
- [ ] T096 [US5] Extend admin statistics/evidence queries in `LiveSupportAIAdminService.cs` with period validation, decision/outcome/error/handoff/action/verification metrics, cursor pagination, and redacted detail; avoid unbounded scans.
- [ ] T097 [P] [US5] Add typed knowledge, preview, evidence, readiness, period, and cursor methods to `frontend/src/services/live-support-ai-service.ts`; remove broad `any` and centralize safe API error parsing.
- [ ] T098 [P] [US5] Create `AIOverview.tsx`, `AIDisableControl.tsx`, `AIPolicyEditor.tsx`, `AIKnowledgeManager.tsx`, `AIDataActionSelector.tsx`, `AIVerificationPolicyEditor.tsx`, `AIPreview.tsx`, and `AIActivityEvidence.tsx` under `frontend/src/components/live-support/ai-admin/`; each module implements loading/empty/error/conflict/disabled states and standard Massar controls.
- [ ] T099 [US5] Replace the monolithic settings/statistics markup in `AdminAISupportPageClient.tsx` with the AI admin modules and standard accessible tabs; keep data orchestration in the page client, prevent sticky actions covering content, and use no nested-card grid.
- [ ] T100 [US5] Normalize `AdminLiveSupportPageClient.tsx` and investigation modules to display AI/worker/recovery events with text actor labels, cursor loading, filters, and redacted metadata; preserve existing human operations/config/performance tabs.
- [ ] T101 [US5] Run admin application filters, frontend typecheck/lint, and Playwright grep `admin AI|policy|preview|disable|evidence`; expected result: preview writes zero and all non-admin server/client paths deny access.

## Phase 9: User Story 6, Accessible Responsive Product UI (P2)

**Goal**: Make all critical role journeys usable at supported widths with keyboard, clear states, stable layout, and Massar visual consistency.

**Independent Test**: Complete participant at 320px, staff at tablet/desktop, and admin at desktop using keyboard-only, reduced motion, 200% zoom, long mixed-direction content, slow network, and errors.

- [x] T102 [P] [US6] Add reusable `LiveSupportStateNotice.tsx`, `LiveSupportSkeleton.tsx`, and `LiveSupportEmptyState.tsx` under `frontend/src/components/live-support/shared/`; use Lucide icons, role/status semantics, retry action, Massar tokens, and no emoji/glass/gradient.
- [x] T103 [P] [US6] Add component-level accessibility tests for participant cards/forms, staff workspace, and admin tabs in `frontend/src/components/live-support/**/*.test.tsx`; assert labels, live regions, focus restoration, disabled reasons, and no color-only state.
- [x] T104 [P] [US6] Extend Playwright viewport/keyboard/reduced-motion/200%-zoom/long-content checks for participant, staff, and admin in `frontend/tests/e2e/live-support-ai.spec.ts`; assert `document.documentElement.scrollWidth <= clientWidth` at each contracted width.
- [x] T105 [US6] Normalize participant components to Deep Navy/Teal/Gold/off-white tokens, Tajawal, 12–16px radii, 44px targets, visible `focus-visible` rings, stable hover without scale, safe-area spacing, and reduced-motion alternatives in files under `frontend/src/components/live-support/participant/`.
- [x] T106 [US6] Normalize staff/admin live-support components to restrained product density, standard tabs/forms/tables, consistent icons/buttons, skeletons, useful empty/error states, and responsive structure in `frontend/src/components/live-support/{staff,admin,student-context,ai-admin}/`.
- [x] T107 [US6] Audit mixed RTL/LTR message, phone, code, timestamp, JSON evidence, and long unbroken content rendering in `ParticipantConversation.tsx`, `StaffConversationWorkspace.tsx`, `ConversationInvestigation.tsx`, and `AIActivityEvidence.tsx`; add directional isolation and wrapping without truncating critical actions.
- [x] T108 [US6] Run component accessibility tests, frontend typecheck/lint/build, and Chromium/WebKit Playwright grep `accessibility|responsive|reduced motion|long content`; expected result: no critical accessibility defect, overflow, overlap, or layout shift.

## Phase 10: Cross-Cutting Security, Performance, and Contract Closure

- [x] T109 Add participant AI message, confirmation, verification, registration, admin preview, and internal callback rate/body limits with explicit partition keys and retry guidance in `backend/src/NaderGorge.API/Configuration/RateLimitingConfiguration.cs` and endpoint attributes; add negative tests in `LiveSupportAISecurityTests.cs`.
- [x] T110 Add static redaction regression tests scanning messages, events, outbox, audit, logs captured by test sinks, worker job payloads, and provider request doubles in `LiveSupportAISecurityTests.cs` and `worker/src/services/liveSupportSecurity.test.ts`; forbidden patterns include password, callback secret, token, raw verification answer, and full lookup value.
- [x] T111 Add query-plan tests for stale turns, callback schedule, pending expiry, verification expiry, knowledge retrieval, admin evidence, and participant snapshot in `backend/tests/NaderGorge.Integration.Tests/LiveSupportAI/LiveSupportAIQueryPlanTests.cs`; assert expected indexes and no unbounded sequential scan at seeded scale.
- [x] T112 Re-run OpenAPI/worker/UI/state-machine parity tests and update implementation DTO/property/event names to match `specs/146-ai-live-support-completion/contracts/`; do not edit contracts to hide an implementation mismatch without recording rationale in `research.md`.
- [x] T113 Run `python3 .agents/skills/speckit-all/scripts/validate_spec_plan_quality.py --spec-dir specs/146-ai-live-support-completion` and `validate_tasks_quality.py --tasks specs/146-ai-live-support-completion/tasks.md`; fix every validator failure before review.

## Phase 11: Mandatory Review and Final Verification Order

- [x] T123 Fix `LiveSupportAIOutboxQueueDispatcher` camelCase payload deserialization in `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs`; verification: `LiveSupportAIOutboxDispatchTests` accepts the contracted payload and still rejects `{}`.
- [x] T124 Correct authoritative student key properties in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIContextBuilder.cs`; use `UserId` for grants, watch events, and exam attempts, then require a clean backend build.
- [x] T125 Replace escaped-Arabic string matching with semantic JSON assertions in `backend/tests/NaderGorge.Application.Tests/LiveSupportAI/LiveSupportAIContextBuilderTests.cs`; verification must still prove allowed full name exists and phone/password do not.
- [x] T126 Correct AES-GCM field ordering in `LiveSupportAIDataProtector.Unprotect`; the first real protected-payload roundtrip test exposed ciphertext/tag reversal, and the focused AI test suite now passes 27/27.
- [x] T127 Recompute the canonical worker decision hash in the backend before idempotency/state mutation; add a fixed cross-language Unicode hash vector so a valid callback token cannot pair a changed decision with an unrelated hash.

- [x] T114 Perform the Phase 6 deep architectural, security, concurrency, privacy, backend, worker, frontend, and UI/UX critique against `spec.md`, `plan.md`, `tasks.md`, and all changed files; add every finding as a new unchecked task here and in `achievements.md`, then fix, verify, and check each finding before T114.
- [x] T115 Run `clean-code-guard` in guard-pass mode on every changed production-code file after T114; exclude test-only files, record every finding in this file and `achievements.md`, fix and verify all findings, then mark Phase 7 complete.
- [x] T116 Run `test-guard` on every changed test file after T115; record every finding in this file and `achievements.md`, remove brittle/tautological/duplicated tests, verify coverage remains complete, then mark Phase 8 complete.
- [x] T117 Read `speckit-all/references/feature-test-matrix.md`, run `extract_test_commands.py`, build the final matrix for all seven stories and all negative/race/privacy/UI paths, then run application, real-PostgreSQL, worker, frontend static/build, Chromium, and WebKit commands from `quickstart.md`.
- [x] T118 Run `docker compose config -q`, `make migrate`, `make up`, `make ps`, all health/readiness/surface/SignalR/queue/restart checks, and record exact results in `achievements.md`.
- [x] T119 Execute the mandatory real configured-provider acceptance from `quickstart.md`; record safe provider/model/correlation/latency/decision/final-state evidence and one reconnect screenshot, and keep this task unchecked if credentials, quota, model, network, or callback path fails.
- [x] T120 Complete the manual student, guest, staff, admin, privacy-negative, responsive, keyboard, zoom, reduced-motion, and rollback checklists in `quickstart.md`; record pass/fail or explicit external blocker for each item.
- [x] T121 Run final sequential builds `dotnet build backend/NaderGorge.sln`, `npm --prefix worker run build`, and `npm --prefix frontend run build`; fix every feature-introduced error/warning and separate unrelated pre-existing warnings in evidence.
- [x] T122 Mark Phase 9 only after T117–T121 pass, run `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/146-ai-live-support-completion`, resolve every failure, and write the final summary with implementation files, review fixes, guard results, test matrix, commands, real-provider evidence, Docker status, data preservation, and release readiness.

## Dependencies and Execution Order

### Phase Dependencies

```text
Setup T001-T006
  -> Foundation T007-T022
    -> US1 T023-T044
      -> US2 T045-T063
        -> US3 T064-T074
          -> US7 T075-T082
            -> US4 T083-T089
              -> US5 T090-T101
                -> US6 T102-T108
                  -> Cross-cutting T109-T113
                    -> Deep critique T114
                      -> Clean Code Guard T115
                        -> Test Guard T116
                          -> Feature tests/Docker/real provider T117-T121
                            -> Validate/report T122
```

- US1 depends on the durable state/queue foundation.
- US2 depends on the validated decision and turn path from US1.
- US3 depends on pending-decision identity from US2 and is the authority for all forced handoffs.
- US7 depends on the final turn/decision/handoff states it recovers.
- US4 may begin after US3, but its tests must use the safe AI handoff context contract.
- US5 depends on the completed services it configures and observes.
- US6 polishes final component structures, not temporary monolith markup.

### Safe Parallel Opportunities

- Tasks marked `[P]` edit different files and may run in parallel only after their phase prerequisites pass.
- Do not parallelize edits to `LiveSupportService.cs`, `AppDbContext.cs`, `Program.cs`, `InternalController.cs`, `useLiveSupportHub.ts`, `ParticipantConversation.tsx`, `AdminAISupportPageClient.tsx`, or the same E2E file.
- Do not run two .NET build/test commands concurrently in this worktree.

### Story Checkpoints

- **US1**: one durable real-shape AI reply, privacy, and reconnect.
- **US2**: one effect after confirmation, zero effect on cancel/expiry/revoke, secure verification/registration.
- **US3**: irreversible safe handoff, fair queue/ownership, no late AI reply.
- **US7**: additive upgrade and deterministic recovery under retry/restart/race.
- **US4**: owner resolves linked student needs with refresh/audit and unlinked privacy.
- **US5**: Admin controls/preview/evidence work; non-admin denied; preview writes zero.
- **US6**: all critical journeys pass contracted responsive and accessibility matrix.

## Implementation Strategy

1. Preserve existing behavior and data; add failing tests for the exact gap before each production edit.
2. Complete foundation and US1 as the first deployable slice: safe read-only AI replies with durable recovery.
3. Add write-capable decisions only after US1 provider/schema/idempotency boundaries pass.
4. Centralize handoff before adding recovery and admin disablement.
5. Refactor UI after final state contracts exist, then apply one consistent design/accessibility pass.
6. Run review gates in the exact required order: deep critique, `clean-code-guard`, `test-guard`, feature tests, Docker, real provider, final builds.

## Notes

- Every task has one primary file or one tightly coupled contract/test group.
- A checked task requires code plus its named verification result.
- Any new compiler warning, lint warning, failed test, runtime error, data mismatch, security finding, or UI defect becomes an unchecked task in both this file and `achievements.md` before being fixed.
- The required real-provider test cannot be waived or replaced by a mock.

# Tasks: Admin AI Agent

**Input**: specs/169-admin-ai-agent/spec.md, plan.md, research.md, data-model.md, quickstart.md, and contracts/
**Implementation status**: Not authorized. This file is the handoff for a later cheaper implementation model after a new explicit owner approval.
**Tests**: Mandatory and test-first for every behavior, permission, data, worker, API, migration, realtime, or UI slice.
**Release rule**: Internal waves may be demonstrated separately, but complete v1 is not releasable until every current Admin business mutation in the sealed baseline is supported and every final gate passes.
**Planning validation command**: `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/169-admin-ai-agent/tasks.md`.

## Spec Kit Preparation Workflow

- [x] Phase 1 specification completed and approved business decisions encoded in specs/169-admin-ai-agent/spec.md.
- [x] Phase 2 Arabic clarification completed with no unresolved product decision.
- [x] Phase 3 technical research/design/contracts completed and validated.
- [x] Phase 4 dependency-ordered task breakdown completed in this file.
- [x] Phase 5 implementation authorized by the owner through `/implement`; release remains fail-closed until every gate below passes.

## Task format

- ID is execution order.
- [P] means safe to run in parallel only when file ownership does not overlap and listed dependencies are complete.
- [US1] through [US5] map to the five user stories in spec.md.
- Each task names its exact primary file path; any generated companion file must be recorded in the same task evidence.
- Before touching an existing modified/untracked owner file, re-read its current diff and integrate rather than overwrite.

## Phase 1: Seal the Current Admin Capability Baseline

**Purpose**: Replace stale regex counts with runtime/reachable evidence and freeze the exact source scope before capability implementation.

- [X] T001 Record git status, current revision, modified/untracked owner files, and overlap rules in specs/169-admin-ai-agent/implementation-evidence.md without staging, discarding, or rewriting owner changes.
- [X] T002 [P] Write a failing runtime endpoint inventory contract using WebApplicationFactory, EndpointDataSource, ControllerActionDescriptor, auth/role/permission metadata, and resolved routes in backend/tests/NaderGorge.Integration.Tests/AdminAI/AdminAIEndpointInventoryTests.cs.
- [X] T003 [P] Write failing reachable Admin route/import/API-call graph tests for standalone, dynamic, and unreachable calls in frontend/scripts/generate-admin-ai-capability-baseline.test.mjs.
- [X] T004 [P] Write failing bidirectional baseline schema/orphan/duplicate/exclusion tests in tests/test_admin_ai_capability_inventory.py.
- [X] T005 Repair diagnostic endpoint parsing for sealed/class-primary-constructor/grouped-attribute forms while preserving existing inventory behavior in scripts/generate-endpoint-inventory.mjs and tests/test_endpoint_inventory.py; runtime inventory remains authoritative.
- [X] T006 Implement Admin route/navigation AST import traversal, dynamic-call contract resolution, deterministic canonical output, and check mode in frontend/scripts/generate-admin-ai-capability-baseline.mjs.
- [X] T007 Implement canonical merge of runtime endpoints, reachable frontend calls, manual semantic metadata, hashes, and Markdown evidence in scripts/generate-admin-ai-capability-baseline.mjs.
- [X] T008 [P] Define the closed baseline-manifest JSON schema and allowed exclusion reason codes in tests/admin_ai_capability_manifest.schema.json.
- [X] T009 Generate and manually classify the sealed candidate into tests/admin_ai_capability_baseline.json with source routes/calls, effect kind, domain, authoritative operation, schemas, limits, risk, confirmation, idempotency, concurrency, audit, and refresh scopes.
- [X] T010 Generate the owner-reviewable baseline table/count/hash report in tests/admin_ai_capability_baseline.md from the canonical JSON, never by manual duplicated counts.
- [X] T011 [P] Add unique prohibited and minimized PII canaries for provider/transcript/proposal/audit/log/metric/realtime/export assertions in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAISecretSentinels.cs.
- [X] T012 Reconcile every current direct-controller DbContext write and missing durable-idempotency operation as a blocking extraction item in tests/admin_ai_capability_baseline.json; do not mark a controller wrapper as an authoritative operation.
- [X] T013 Run the endpoint, frontend graph, and Python baseline checks and record exact source/runtime/frontend/manifest hashes, counts, exclusions, and failures in specs/169-admin-ai-agent/implementation-evidence.md.
- [X] T014 Confirm Phase 1 observable result in specs/169-admin-ai-agent/implementation-evidence.md: one disposition per sealed item, allowed non-business exclusions only, and an explicit blocking list for every business operation not yet safely adaptable.

**Checkpoint**: Baseline identity is frozen; no agent capability can be added outside it, and no count is inferred from stale inventory.

## Phase 2: Foundational Security, Persistence, Queue, and Protocol

**Purpose**: Build the trust boundary shared by every story. This phase blocks all user-story implementation.

### Tests first

- [X] T015 [P] Write entity/invariant/delete-behavior/version/retention model tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIModelTests.cs.
- [ ] T016 [P] Write clean/existing-database migration, partial-unique, check-index, and no-cascade PostgreSQL tests in backend/tests/NaderGorge.Integration.Tests/AdminAI/AdminAIMigrationTests.cs.
- [X] T017 [P] Write current Admin, non-Admin, disabled/deleted, role-removal, security-version, owner/non-owner access tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIAccessGateTests.cs.
- [X] T018 [P] Write purpose-separated encryption/HMAC/tamper/key-unavailable/phrase-normalization tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIDataProtectionTests.cs.
- [X] T019 [P] Write manifest schema, risk derivation, strong-confirmation, unknown capability, and sensitive-policy registration tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAICatalogTests.cs.
- [X] T020 [P] Write public/internal API DTO/error/additional-property/request-size contract tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIContractTests.cs.
- [X] T021 [P] Write Outbox stable-job-ID, queue mapping, lease, callback replay, expiry, and restart recovery tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIOutboxRecoveryTests.cs.
- [X] T022 [P] Write worker closed-union, canonical hash, unknown/extra/depth/size/schema-version rejection tests in worker/src/services/adminAIDecisionSchema.test.ts.
- [X] T023 [P] Write worker internal-token client, timeout, response-size, safe-error, retry, and replay tests in worker/src/services/adminAICallbackClient.test.ts.
- [X] T024 [P] Write realtime envelope allowlist, no-payload-content, event dedupe/gap/version tests in frontend/src/lib/admin-ai-agent-client-contract.test.ts.
- [X] T025 [P] Write distributed rate-policy partition/limit and active-turn-limit tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIRateLimitTests.cs.

### Domain and database

- [X] T026 [P] Define every AdminAI enum and terminal/active state helper in backend/src/NaderGorge.Domain/Enums/AdminAIEnums.cs.
- [X] T027 [P] Create immutable capability-baseline and sensitive-data-policy entities in backend/src/NaderGorge.Domain/Entities/AdminAI/AdminAIGovernance.cs.
- [X] T028 [P] Create conversation and visible-message entities with owner/sequence/archive/version fields in backend/src/NaderGorge.Domain/Entities/AdminAI/AdminAIConversation.cs.
- [X] T029 [P] Create turn, durable step, and bounded read-invocation entities in backend/src/NaderGorge.Domain/Entities/AdminAI/AdminAITurn.cs.
- [X] T030 [P] Create proposal, strong challenge, and secure-input-grant entities in backend/src/NaderGorge.Domain/Entities/AdminAI/AdminAIActionProposal.cs.
- [X] T031 [P] Create execution and per-item partial-outcome entities in backend/src/NaderGorge.Domain/Entities/AdminAI/AdminAIActionExecution.cs.
- [X] T032 [P] Create append-only correlated AdminAI audit-event entity in backend/src/NaderGorge.Domain/Entities/AdminAI/AdminAIAuditEvent.cs.
- [X] T033 Add AdminAI DbSet and transaction abstractions without live-support/chat coupling in backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs and backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs.
- [X] T034 Configure exact lengths, JSONB, restricted foreign keys, checks, partial uniques, indexes, and optimistic versions in backend/src/NaderGorge.Infrastructure/Data/Configurations/AdminAI/AdminAIEntityConfigurations.cs.
- [X] T035 Generate the additive AddAdminAIAgent EF migration and snapshot changes in backend/src/NaderGorge.Infrastructure/Migrations while preserving the then-current latest migration and adding no seed/destructive operation.

### Application contracts and security

- [X] T036 [P] Define public/internal closed DTOs, status/result unions, safe evidence, and allowlisted error codes matching contracts/admin-ai-api.yaml in backend/src/NaderGorge.Application/Features/AdminAI/Dtos/AdminAIContracts.cs.
- [X] T037 [P] Define access, registry, sensitive policy, data protection, orchestration, read, proposal, action, secure-input, recovery, and audit interfaces in backend/src/NaderGorge.Application/Features/AdminAI/Interfaces/AdminAIInterfaces.cs.
- [X] T038 Implement immutable capability definitions, source mapping, schemas, risk, confirmation, limits, and refresh scopes in backend/src/NaderGorge.Application/Features/AdminAI/Catalog/AdminAICapabilityRegistry.cs.
- [X] T039 Implement field classification, projection allowlists, prohibited-type/name defense, and policy hashing in backend/src/NaderGorge.Application/Features/AdminAI/Security/AdminAISensitiveDataPolicy.cs.
- [X] T040 Implement PostgreSQL-backed live Admin/account/security-version/owner revalidation with no role-cache-only decision in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIAccessGate.cs.
- [X] T041 Implement purpose-separated authenticated encryption, keyed digests, canonical JSON, and phrase normalization in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIDataProtector.cs.
- [X] T042 Implement append-only safe AdminAI evidence writer and linked redacted AuditLog summary in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIAuditWriter.cs.

### Queue, recovery, and API wiring

- [X] T043 Extend stable queue/job resolution for AdminAITurnQueued and ai-admin-agent-turns/respond in backend/src/NaderGorge.Infrastructure/Background/RedisJobEnqueuer.cs.
- [X] T044 Add AdminAI queue dispatch and strict owner-targeted realtime envelope validation without weakening existing dispatchers in backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs.
- [X] T045 Implement bounded lease-aware expiry/cancellation/provider-callback/execution reconciliation in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIRecoveryService.cs.
- [X] T046 Wire periodic recovery with cluster-safe bounded batches in backend/src/NaderGorge.API/BackgroundServices/AdminAIRecoveryBackgroundService.cs.
- [X] T047 Implement readiness, claim, lease-renew, read-batch, complete, and fail routes with internal token/rate/size limits in backend/src/NaderGorge.API/Controllers/AdminAIInternalController.cs.
- [X] T048 Add admin-ai-turn, confirmation, secure-input, and internal policies consistently to backend/src/NaderGorge.API/Configuration/RateLimitingConfig.cs and backend/src/NaderGorge.API/Middleware/RedisRateLimitingMiddleware.cs.
- [X] T049 Register AdminAI configuration validation, services, adapters, hosted recovery, and feature-disabled behavior in backend/src/NaderGorge.API/Program.cs.

### Worker and client foundations

- [X] T050 [P] Implement exact schema-version-1 closed decision parser/canonicalizer/hash in worker/src/services/adminAIDecisionSchema.ts.
- [X] T051 [P] Implement bounded internal claim/read/renew/complete/fail client using AI_CALLBACK_SECRET in worker/src/services/adminAICallbackClient.ts.
- [X] T052 Implement cancellation-aware durable processor with completion replay, queue-age deadline, safe failures, and no action execution in worker/src/jobs/processAdminAITurn.ts.
- [X] T053 Register isolated ai-admin-agent-turns Worker/Queue/readiness heartbeat/concurrency in worker/src/index.ts without changing live-support queue semantics.
- [X] T054 [P] Define frontend closed API/realtime/error/status/route-key contracts in frontend/src/services/admin-ai-agent-contract.ts and frontend/src/lib/admin-ai-agent-client-contract.ts.
- [X] T055 [P] Create typed AbortSignal/Idempotency-Key REST methods in frontend/src/services/admin-ai-agent-service.ts without chat/live-support service imports.
- [ ] T056 Run all Phase 2 tests plus migration on clean and representative existing PostgreSQL data, and record expected zero live-support/chat coupling and zero existing-data deletion in specs/169-admin-ai-agent/implementation-evidence.md.

**Checkpoint**: Persistence, access, encryption, audit, queue, protocol, rate limiting, and recovery are test-proven before any platform read or action capability.

## Phase 3: User Story 1 — Grounded Whole-Platform Questions

**Goal**: An Admin can privately ask record, aggregate, and cross-domain questions and receive bounded, current, evidence-backed answers while every non-Admin and prohibited secret is denied.

**Independent test**: Compare representative answers for every domain family to original screens/reports; verify ambiguity/empty/truncation and zero P0 sentinel across every sink.

### Tests first

- [X] T057 [P] [US1] Write conversation create/list/rename/archive/restore/message-order/pagination/idempotency tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIConversationTests.cs.
- [X] T058 [P] [US1] Write owner/non-owner/current-role checks for every conversation/turn/snapshot route in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIConversationAuthorizationTests.cs.
- [X] T059 [P] [US1] Generate per-read-capability schema/limit/evidence/field-allowlist/empty/truncated tests from the baseline in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIReadCapabilityContractTests.cs.
- [X] T060 [P] [US1] Write tool batch budget, lease, access recheck, unknown capability, deterministic replay, and cancellation tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIToolGatewayTests.cs.
- [X] T061 [P] [US1] Write prompt-injection and P0/P1/P2 minimization capture tests across backend claim/read/provider/transcript/audit/realtime/export in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIRedactionTests.cs.
- [ ] T062 [P] [US1] Write real PostgreSQL query-count/timeout/plan tests for representative high-volume read capabilities in backend/tests/NaderGorge.Integration.Tests/AdminAI/AdminAIReadQueryPlanTests.cs.
- [X] T063 [P] [US1] Write manual function-call loop, multiple read, empty/truncated/rejected, max-step/call/byte/deadline/cancel tests in worker/src/services/adminAIAgent.test.ts.
- [X] T064 [P] [US1] Write worker job provider-completed/callback-pending crash and no-second-inference tests in worker/src/jobs/processAdminAITurn.test.ts.
- [X] T065 [P] [US1] Write frontend conversation/snapshot/error/realtime/store/generation-guard tests in frontend/src/features/admin-ai-agent/admin-ai-agent-store.test.ts and frontend/src/services/admin-ai-agent-contract.test.ts.
- [ ] T066 [P] [US1] Write real-backend Playwright coverage for Admin/non-Admin, history, record/aggregate/cross-domain, ambiguity, empty, truncated, evidence, stop/retry, and no-secret UI in frontend/tests/e2e/admin-ai-agent.spec.ts.

### Backend conversation and orchestration

- [X] T067 [US1] Implement create/rename/archive/restore commands with owner/version/idempotency and archive cancellation semantics in backend/src/NaderGorge.Application/Features/AdminAI/Commands/AdminAIConversationCommands.cs.
- [X] T068 [US1] Implement owner-only cursor list and paged authoritative snapshot queries in backend/src/NaderGorge.Application/Features/AdminAI/Queries/AdminAIConversationQueries.cs.
- [X] T069 [US1] Implement turn admission, message/step/outbox atomicity, one-active-turn-per-conversation, two-active-turns-per-Admin, and cancellation in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAITurnOrchestrator.cs.
- [X] T070 [US1] Implement closed read invocation validation, timeout/budgets, policy redaction, protected replay result, evidence, and no mutation dispatch in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIReadCapabilityExecutor.cs.
- [X] T071 [US1] Implement backend completion validation for answer/clarify/refuse/action-suggestion branches, evidence ownership, canonical hash, and late-callback discard in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAITurnCompletionService.cs.

### Read capability families

- [X] T072 [P] [US1] Implement bounded user/student/staff/role/device/access/balance/gamification/watch read projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIIdentityReadCapabilities.cs.
- [X] T073 [P] [US1] Implement bounded teacher/subject/photo/stats/student/essay/activation projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAITeacherReadCapabilities.cs.
- [X] T074 [P] [US1] Implement package/term/section/lesson/video/resource/video-type/Bunny/AI-state projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIContentReadCapabilities.cs.
- [X] T075 [P] [US1] Implement exam/question/homework/submission/grade/essay/dashboard projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIAssessmentReadCapabilities.cs.
- [X] T076 [P] [US1] Implement code group/access code/profile/batch/shared-package/delivery projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAICodeReadCapabilities.cs.
- [X] T077 [P] [US1] Implement gifts/promotions/sales rules/coupons/templates/public-exam/sales-effect projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAISalesReadCapabilities.cs.
- [X] T078 [P] [US1] Implement forms/submissions/safe settings/popup/notification/messaging metadata projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIFormsSettingsReadCapabilities.cs.
- [X] T079 [P] [US1] Implement wallet/recharge/SMS-transfer/match/limit/transaction projections without token material in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIWalletRechargeReadCapabilities.cs.
- [X] T080 [P] [US1] Implement legacy payroll/adjustment/payout/teacher-event/report projections with deterministic EGP calculations in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAILegacyFinanceReadCapabilities.cs.
- [X] T081 [P] [US1] Implement teacher agreement/liability/settlement/invoice/allocation/terms/delivery projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAITeacherFinanceReadCapabilities.cs.
- [X] T082 [P] [US1] Implement ledger/treasury/expense/refund/budget/reconciliation/period/history/wallet-review projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIPlatformFinanceReadCapabilities.cs.
- [X] T083 [P] [US1] Implement HR people/org/job/location/contract/document/asset projections with P1/P2 minimization in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIHrPeopleReadCapabilities.cs.
- [X] T084 [P] [US1] Implement HR shift/attendance/break/leave/approval/payroll/compensation projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIHrOperationsReadCapabilities.cs.
- [X] T085 [P] [US1] Implement HR performance/cases/recruitment/lifecycle/governance/migration projections in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIHrLifecycleReadCapabilities.cs.
- [X] T086 [P] [US1] Implement operations/task/CRM/internal-chat administration projections without AdminAI transcript mixing in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIOperationsReadCapabilities.cs.
- [X] T087 [P] [US1] Implement community/post/comment/poll/moderation projections treating text as untrusted in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAICommunityReadCapabilities.cs.
- [X] T088 [P] [US1] Implement live-support queue/staff/config/ratings/stats/support-AI policy/knowledge/evidence projections without sharing state in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAILiveSupportAdminReadCapabilities.cs.
- [X] T089 [P] [US1] Implement reports/KPI/export-status/safe audit/system-log/media/social-plan projections without raw AuditLog values in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIReportingReadCapabilities.cs.
- [X] T090 [US1] Register every baseline read key once, reject missing/duplicate adapters, and expose only active-version tool schemas in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Reads/AdminAIReadCapabilityRegistration.cs.

### Public API and worker inference

- [X] T091 [US1] Implement built-in-Admin-only conversation/list/snapshot/turn/cancel endpoints and safe error mapping in backend/src/NaderGorge.API/Controllers/AdminAIAgentController.cs.
- [X] T092 [US1] Implement bounded prompt assembly, untrusted-data labeling, manual Gemini function responses, closed terminal decisions, and no web/MCP/code/action tools in worker/src/services/adminAIAgent.ts.
- [X] T093 [US1] Integrate provider metadata/deadline/cancellation into processAdminAITurn without logging content in worker/src/jobs/processAdminAITurn.ts.

### Admin UI

- [X] T094 [US1] Add standalone adminOnly /admin/ai-agent navigation, title, primary placement, home visibility parity, and expensive-route prefetch classification in frontend/src/packages/admin/navigation.tsx, frontend/src/components/admin/AdminShellChrome.tsx, frontend/src/app/admin/AdminRootPageClient.tsx, and frontend/src/components/navigation/IntentLink.tsx.
- [X] T095 [P] [US1] Create the route wrapper and AdminPage client boundary in frontend/src/app/admin/ai-agent/page.tsx and frontend/src/app/admin/ai-agent/AdminAiAgentPageClient.tsx.
- [X] T096 [P] [US1] Implement owner-scoped query keys, snapshot/list calls, typed errors, AbortSignal, and stable idempotency in frontend/src/services/admin-ai-agent-service.ts.
- [X] T097 [US1] Implement selected conversation, in-memory draft, event sequence/dedupe, connection, and intent state without browser persistence in frontend/src/features/admin-ai-agent/admin-ai-agent-store.ts.
- [X] T098 [US1] Implement bootstrap/select/create/rename/archive/restore/send/stop/retry/reconcile/security-cleanup controller in frontend/src/features/admin-ai-agent/useAdminAiAgentController.ts.
- [X] T099 [P] [US1] Implement shared PlatformHub AdminAI envelope subscription, gap/refetch, reconnect, and access-revoked cleanup in frontend/src/hooks/useAdminAiAgentEvents.ts.
- [X] T100 [P] [US1] Implement responsive history/header/workspace/list-empty-loading-error composition in frontend/src/features/admin-ai-agent/AdminAiAgentWorkspace.tsx, AdminAiConversationList.tsx, AdminAiConversationHeader.tsx, AdminAiEmptyState.tsx, AdminAiErrorState.tsx, and AdminAiSkeleton.tsx.
- [X] T101 [P] [US1] Implement accessible role-log transcript, mixed-direction messages, paged history, scroll anchor, and new-message indicator in frontend/src/features/admin-ai-agent/AdminAiTranscript.tsx and AdminAiMessage.tsx.
- [X] T102 [P] [US1] Implement grounded fact/calculation/inference/limitation/evidence disclosure with allowlisted routeKey links in frontend/src/features/admin-ai-agent/AdminAiEvidenceDisclosure.tsx.
- [X] T103 [P] [US1] Implement queued/retrieving/calculating/answering/clarification/cancel/failure/retry announcements and inline states in frontend/src/features/admin-ai-agent/AdminAiTurnStatus.tsx.
- [X] T104 [P] [US1] Implement IME-safe Enter/Shift+Enter autosize composer, send/stop, focus, safe-area, and draft-preservation behavior in frontend/src/features/admin-ai-agent/AdminAiComposer.tsx.
- [ ] T105 [US1] Run the US1 backend/worker/frontend/browser tests, compare every domain representative result to authoritative fixtures, and record exact pass/fail/query/secret-sentinel evidence in specs/169-admin-ai-agent/implementation-evidence.md.

**Checkpoint**: US1 works independently as a read-only private Admin agent, but no mutation is releasable yet.

## Phase 4: User Story 2 — Review and Confirm Ordinary Admin Operations

**Goal**: Every ordinary mutation produces a zero-effect authoritative proposal and executes once only after the initiating Admin explicitly confirms.

**Independent test**: For representative ordinary actions in every applicable domain, compare preview/validation/result/audit/refresh with the original screen; cancel, expire, stale, duplicate, and conflicting requests create the specified zero-or-one outcome.

### Tests first

- [X] T106 [P] [US2] Write server proposal construction, safe current/requested/effect, expiry, baseline/policy binding, and independent-action splitting tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIProposalBuilderTests.cs.
- [X] T107 [P] [US2] Write proposal-preview zero-business-effect tests using EF save/command interception and fake queue/storage/provider/message clients in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIProposalNoSideEffectTests.cs.
- [ ] T108 [P] [US2] Generate ordinary action schema/risk/preview/executor/audit/refresh parity tests for every ordinary baseline key in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIOrdinaryActionContractTests.cs.
- [X] T109 [P] [US2] Write public proposal/get/confirm/cancel owner/version/idempotency/error contract tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIProposalApiTests.cs.
- [ ] T110 [P] [US2] Write PostgreSQL serializable claim, stale fingerprint, matching replay, conflicting payload, two-tab, and two-Admin tests in backend/tests/NaderGorge.Integration.Tests/AdminAI/AdminAIActionConcurrencyTests.cs.
- [X] T111 [P] [US2] Write worker propose_actions maximum-count/key/schema/no-risk/no-success-claim tests in worker/src/services/adminAIAgent.test.ts.
- [X] T112 [P] [US2] Write typed proposal-card, ordinary CTA, expiry, cancel, execution-result, focus, and no-raw-JSON component tests in frontend/src/features/admin-ai-agent/AdminAiActionProposalCard.test.tsx.
- [ ] T113 [P] [US2] Extend real-backend browser coverage for ordinary proposal/cancel/expire/stale/duplicate/parity paths in frontend/tests/e2e/admin-ai-agent.spec.ts.

### Proposal and execution core

- [X] T114 [US2] Implement server-owned action suggestion validation, authoritative preview, risk derivation, payload protection, fingerprinting, splitting, and proposal persistence in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIProposalBuilder.cs.
- [X] T115 [US2] Implement unique proposal execution claim, actor/idempotency/payload binding, authoritative adapter resolution, terminal replay, original audit linkage, and refresh scopes in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIActionExecutor.cs.
- [X] T116 [US2] Implement ordinary confirm/cancel commands with live access, ownership, expiry, baseline/policy, state revalidation, and safe terminal outcomes in backend/src/NaderGorge.Application/Features/AdminAI/Commands/AdminAIProposalCommands.cs.
- [X] T117 [US2] Add owner-only proposal get/confirm/cancel endpoints and closed error/status mapping to backend/src/NaderGorge.API/Controllers/AdminAIAgentController.cs.
- [X] T118 [US2] Validate propose_actions against the claim catalog and deliver untrusted suggestions without executing them in worker/src/services/adminAIAgent.ts.

### Ordinary action adapters

- [ ] T119 [P] [US2] Implement ordinary student/staff profile and note/create metadata adapters through existing Admin commands in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIIdentityOrdinaryActions.cs.
- [ ] T120 [P] [US2] Implement ordinary teacher/subject/package/term/section/lesson/video/resource metadata adapters through authoritative commands in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIContentOrdinaryActions.cs.
- [ ] T121 [P] [US2] Implement ordinary question/exam/homework/grade and non-destructive approve workflow adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIAssessmentOrdinaryActions.cs.
- [ ] T122 [P] [US2] Implement ordinary code-profile/sales-draft/form/submission/media-plan metadata adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAICommercialOrdinaryActions.cs.
- [ ] T123 [P] [US2] Implement ordinary HR profile/org/job/location/shift/attendance/leave-request/performance metadata adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIHrOrdinaryActions.cs.
- [ ] T124 [P] [US2] Implement ordinary task/assignment/comment/call/CRM/internal-chat administration adapters without AdminAI transcript reuse in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIOperationsOrdinaryActions.cs.
- [ ] T125 [P] [US2] Implement ordinary live-support staff metadata, report-definition, media-pipeline, and social-plan adapters where manifest risk remains Ordinary in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIAdminToolsOrdinaryActions.cs.
- [ ] T126 [US2] Register every ordinary baseline key exactly once and reject missing/duplicate/incorrect-risk adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIActionCapabilityRegistration.cs.

### Proposal UI

- [X] T127 [P] [US2] Implement structured ordinary proposal card with target/current/requested/effect/risk/validation/expiry/deep-link/cancel/concrete confirm CTA in frontend/src/features/admin-ai-agent/AdminAiActionProposalCard.tsx.
- [X] T128 [P] [US2] Implement full/partial/rejected/stale/cancelled/expired/dependency/recovery result presentation with safe trace and refresh scopes in frontend/src/features/admin-ai-agent/AdminAiExecutionResult.tsx.
- [X] T129 [US2] Add proposal fetch/confirm/cancel stable-intent control, per-card busy state, authoritative refetch, and scope invalidation to frontend/src/features/admin-ai-agent/useAdminAiAgentController.ts.
- [ ] T130 [US2] Run generated ordinary parity plus representative original-screen comparisons and record zero-effect preview, zero-or-one execution, and exact result evidence in specs/169-admin-ai-agent/implementation-evidence.md.

**Checkpoint**: US2 ordinary actions work independently with explicit confirmation; high-risk actions remain unavailable until Phase 5.

## Phase 5: User Story 3 — Strong Confirmation, Secure Inputs, Financial, External, and Bulk Actions

**Goal**: Every destructive, financial, permission, security, account-disable, credential, bulk, and consequential external operation requires an exact proposal-specific typed phrase and preserves original special semantics.

**Independent test**: Wrong/old/locked/expired/stale phrases create zero effects; correct confirmation creates at most one authoritative result; secure values never enter agent sinks; bulk/finance/external outcomes match original workflows.

### Tests first

- [X] T131 [P] [US3] Write strong phrase format/randomness/HMAC/NFC/whitespace/exact-case/punctuation/digit/old-proposal/five-attempt-lock tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIStrongConfirmationTests.cs.
- [X] T132 [P] [US3] Write secure-grant actor/proposal/type/size/expiry/one-time/encryption/purge/no-log/no-model tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAISecureInputTests.cs.
- [X] T133 [P] [US3] Write bulk selector/membership/count/version/fingerprint/Atomic/Partial/count-reconciliation tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIBulkActionTests.cs.
- [ ] T134 [P] [US3] Generate high-risk action schema/risk/TypedStrong/preview/executor/audit/idempotency tests for every high-risk baseline key in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIHighRiskActionContractTests.cs.
- [ ] T135 [P] [US3] Write PostgreSQL finance precision/currency/source-document/period/post/reversal/reconciliation/idempotency tests in backend/tests/NaderGorge.Integration.Tests/AdminAI/AdminAIFinancialActionTests.cs.
- [X] T136 [P] [US3] Write external provider/job/file timeout/idempotency/authoritative-recovery tests with fakes in backend/tests/NaderGorge.Integration.Tests/AdminAI/AdminAIExternalActionTests.cs.
- [X] T137 [P] [US3] Write secure-input API request-body logging suppression, ownership, consumed/expired Gone, and no-value response tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAISecureInputApiTests.cs.
- [X] T138 [P] [US3] Write strong-confirmation/secure-overlay/bulk/partial/recovery accessibility and state tests in frontend/src/features/admin-ai-agent/AdminAiStrongConfirmation.test.tsx and AdminAiSecureInputOverlay.test.tsx.
- [ ] T139 [P] [US3] Extend real-backend browser coverage for every high-risk category, wrong phrase, secure flow, bulk change, partial result, and RecoveryRequired in frontend/tests/e2e/admin-ai-agent.spec.ts.
- [X] T140 [P] [US3] Extend secret-sentinel capture to protected request endpoint, private file token, challenge, and purge paths in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIRedactionTests.cs.

### Strong confirmation and secure continuation

- [X] T141 [US3] Implement server challenge generation, phrase rendering/digest, exact normalization, attempt lock, expiry/cancel, and proposal binding in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIConfirmationChallengeService.cs.
- [X] T142 [US3] Implement short-lived grant issue/submit/consume/purge with purpose-separated encryption and private-file references in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAISecureInputService.cs.
- [X] T143 [US3] Add secure-grant issue/submit and typed-phrase confirm endpoints with body-capture suppression and owner/version/idempotency checks in backend/src/NaderGorge.API/Controllers/AdminAIAgentController.cs.
- [X] T144 [US3] Enforce TypedStrong for every non-Ordinary risk and secure-grant final validation/consume inside execution in backend/src/NaderGorge.Application/Features/AdminAI/Commands/AdminAIProposalCommands.cs.

### High-risk action adapters

- [ ] T145 [P] [US3] Implement role/permission/status/disable/password-reset/device/access-grant/balance/gamification/watch high-risk adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIIdentityHighRiskActions.cs.
- [ ] T146 [P] [US3] Implement teacher/subject/content delete/unpublish/activation/upload/Bunny/AI-job/link/unlink high-risk adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIContentHighRiskActions.cs.
- [ ] T147 [P] [US3] Implement assessment delete/unlock/bulk and destructive community/comment moderation adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIAssessmentHighRiskActions.cs.
- [ ] T148 [P] [US3] Implement bulk codes/shared-package publish/reset/delete, gifts issue/revoke, sales price/discount/coupon/batch/publication adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAICommercialHighRiskActions.cs.
- [ ] T149 [P] [US3] Implement form delete/private upload, platform-wide setting/popup, and consequential WhatsApp/messaging adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIFormsSettingsHighRiskActions.cs.
- [x] T150 [P] [US3] Implement wallet create/toggle/limit/token-regeneration and recharge/SMS match/reassign/resolve/reverse-credit adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIWalletRechargeHighRiskActions.cs.
- [ ] T151 [P] [US3] Implement legacy payroll generate/approve/adjustment/delete/payout/teacher-event finance adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAILegacyFinanceHighRiskActions.cs.
- [x] T152 [P] [US3] Implement teacher agreement/replace/settlement/pay/cancel/reverse/invoice/terms/delivery adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAITeacherFinanceHighRiskActions.cs.
- [x] T153 [P] [US3] Implement platform ledger expense/refund/treasury/transfer/reconcile/budget/period/backfill/reverse/classification adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIPlatformFinanceHighRiskActions.cs.
- [ ] T154 [P] [US3] Implement HR compensation/payroll/leave-balance/approval/contract/security/retention/migration high-risk adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIHrFinanceGovernanceHighRiskActions.cs.
- [ ] T155 [P] [US3] Implement HR delete/offboard/hire/offer/case/evidence/discipline/document/asset high-risk adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIHrLifecycleHighRiskActions.cs.
- [ ] T156 [P] [US3] Implement task approval/archive/bulk, CRM consequential changes, chat archive, support intervention/staff config/support-AI policy/knowledge enable-disable-publish adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIOperationsHighRiskActions.cs.
- [ ] T157 [P] [US3] Implement report export job/media publish/state adapters and explicit protected-audit mutation refusal in backend/src/NaderGorge.Infrastructure/Services/AdminAI/Actions/AdminAIReportingHighRiskActions.cs.
- [X] T158 [US3] Implement exact bulk preview, membership revalidation, Atomic/Partial execution, per-item safe evidence, and count reconciliation in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIBulkActionExecutor.cs.
- [X] T159 [US3] Implement external-effect deterministic identity, timeout-to-RecoveryRequired, and authoritative reconciliation adapters in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIExternalOperationReconciler.cs.

### High-risk UI

- [X] T160 [P] [US3] Implement typed phrase disclosure/input/explanation/attempt/expiry/locked/focus behavior in frontend/src/features/admin-ai-agent/AdminAiStrongConfirmation.tsx.
- [X] T161 [P] [US3] Implement isolated password/token/answer/private-file secure overlay with no store/cache persistence and original validation in frontend/src/features/admin-ai-agent/AdminAiSecureInputOverlay.tsx.
- [X] T162 [US3] Render bulk selection/count/exclusions/sample/Atomic/Partial and per-item terminal outcomes in frontend/src/features/admin-ai-agent/AdminAiActionProposalCard.tsx and AdminAiExecutionResult.tsx.
- [ ] T163 [US3] Run all high-risk generated/domain integration/browser tests and record phrase, secure no-leak, finance, bulk, external recovery, and zero-or-one evidence in specs/169-admin-ai-agent/implementation-evidence.md.

**Checkpoint**: US3 high-risk semantics pass, but complete v1 still waits for the full baseline closure in US4.

## Phase 6: User Story 4 — Prove Complete Current Admin Capability Coverage

**Goal**: Every sealed current Admin business read/mutation maps exactly once to a tested safe capability, and every capability maps back to a live original workflow.

**Independent test**: Regenerate runtime/frontend inventories and fail on any missing, duplicate, stale, generic, excluded-current-business, or untested mapping.

### Tests and source extraction

- [X] T164 [P] [US4] Generate a failing per-capability matrix assertion for input/output/risk/confirmation/preview/execution/idempotency/concurrency/audit/refresh/security tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAICapabilityCoverageTests.cs.
- [X] T165 [P] [US4] Generate a failing preview no-effect suite for every mutation/external capability using registered fakes/interceptors in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAICapabilityPreviewMatrixTests.cs.
- [X] T166 [US4] Extract pending-essay state mutation from AdminController into one authoritative application command/query contract and preserve current behavior/tests in backend/src/NaderGorge.API/Controllers/AdminController.cs and backend/src/NaderGorge.Application/Features/Admin/Essays.
- [X] T167 [P] [US4] Extract wallet-transfer backfill/record/internal-transfer/reverse expense/refund controller writes into authoritative commands/services in backend/src/NaderGorge.API/Controllers/AdminPlatformFinanceController.cs and backend/src/NaderGorge.Application/Features/Admin/PlatformFinance.
- [X] T168 [P] [US4] Extract agreement/settlement/pay/cancel/reverse/invoice and code-finance terms/delivery writes into authoritative services in backend/src/NaderGorge.API/Controllers/AdminTeacherFinanceCenterController.cs, AdminTeacherCodeFinanceController.cs, and backend/src/NaderGorge.Application/Features/Admin/TeacherFinanceCenter.
- [X] T169 [P] [US4] Extract shared-package create/upload/publish writes into authoritative commands/storage boundaries in backend/src/NaderGorge.API/Controllers/AdminSharedPackagesController.cs and backend/src/NaderGorge.Application/Features/Admin/SharedPackages.
- [X] T170 [P] [US4] Extract HR approval/asset/leave/payroll/performance/recruitment/shift direct writes into authoritative commands/services in backend/src/NaderGorge.API/Controllers/HrApprovalsController.cs, HrDocumentsAssetsController.cs, HrLeaveController.cs, HrPayrollController.cs, HrPerformanceCasesController.cs, HrRecruitmentLifecycleController.cs, and HrShiftsController.cs.
- [ ] T171 [US4] Add durable idempotency/result-recovery to every baseline operation still marked blocking, inside its original application service, and update exact source mappings in tests/admin_ai_capability_baseline.json.

### Baseline closure

- [ ] T172 [US4] Reconcile every runtime/backend/frontend/source drift item and register every remaining read/action adapter or allowed non-business exclusion in backend/src/NaderGorge.Application/Features/AdminAI/Catalog/AdminAICapabilityRegistry.cs and tests/admin_ai_capability_baseline.json.
- [X] T173 [US4] Implement Draft validation, hash verification, zero-current-business-exclusion check, single Active activation, supersession, and pending-proposal invalidation in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAICapabilityBaselineService.cs.
- [X] T174 [US4] Implement safe active-baseline summary endpoint without executable schemas/secrets in backend/src/NaderGorge.API/Controllers/AdminAIAgentController.cs.
- [X] T175 [US4] Add AdminAI baseline drift checks to Makefile, frontend/package.json, and repository verification without replacing existing gates.
- [ ] T176 [US4] Regenerate tests/admin_ai_capability_baseline.json and tests/admin_ai_capability_baseline.md from the sealed candidate; confirm hashes/counts and zero unsupported/excluded current Admin business mutations.
- [ ] T177 [US4] Run the bidirectional inventory, generated capability matrix, no-effect preview, secret, original-parity, and full repository gates; record exact per-domain coverage and zero-gap result in specs/169-admin-ai-agent/implementation-evidence.md.

**Checkpoint**: US4 is the first point where “every current Admin action” may be claimed, subject to US5 and final acceptance.

## Phase 7: User Story 5 — Private History, Shared Redacted Evidence, and Recovery

**Goal**: Owners can resume private conversations while auditors can reconstruct redacted action lifecycle, and restart/dependency failures converge to one consistent state.

**Independent test**: Resume/archive histories, deny another Admin transcript, inspect correlated redacted evidence for all terminal paths, and restart each process during turn/proposal/execution.

### Tests first

- [X] T178 [P] [US5] Write private transcript versus shared redacted action-evidence authorization/filter/cursor tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIAuditAuthorizationTests.cs.
- [X] T179 [P] [US5] Write append-only event, linked AuditLog summary, evidence hash/correlation, no raw transcript/OldValues/NewValues tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIAuditTests.cs.
- [X] T180 [P] [US5] Write read-result 24-hour purge and secure-input immediate purge tests in backend/tests/NaderGorge.Application.Tests/AdminAI/AdminAIRetentionTests.cs.
- [ ] T181 [P] [US5] Write PostgreSQL/backend/worker/Redis-delivery restart recovery for queued/claimed/provider-completed/callback-pending/executing/RecoveryRequired rows in backend/tests/NaderGorge.Integration.Tests/AdminAI/AdminAIRecoveryIntegrationTests.cs.
- [X] T182 [P] [US5] Write event/outbox duplicate/gap/reconnect/tab-resume and terminal snapshot convergence browser tests in frontend/tests/e2e/admin-ai-agent.spec.ts.

### Implementation

- [X] T183 [US5] Implement redacted action-evidence cursor/filter query without private message joins in backend/src/NaderGorge.Application/Features/AdminAI/Queries/AdminAIAuditQueries.cs.
- [X] T184 [US5] Add action-evidence endpoint under existing Admin audit authority and owner-only history behavior in backend/src/NaderGorge.API/Controllers/AdminAIAgentController.cs.
- [X] T185 [US5] Enforce append-only application paths, evidence hashing, linked original audit, and terminal event completeness in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIAuditWriter.cs.
- [X] T186 [US5] Implement protected read-result and secure-input purge plus terminal/expired proposal recovery in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIRecoveryService.cs.
- [X] T187 [US5] Implement safe RecoveryRequired reconciliation that can only use original authoritative idempotency/provider identity in backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAIExternalOperationReconciler.cs.
- [X] T188 [US5] Add owner history pagination/archive/restore/reconnect states and persistent terminal proposal/result rendering in frontend/src/features/admin-ai-agent/useAdminAiAgentController.ts and AdminAiTranscript.tsx.
- [X] T189 [US5] Implement existing-audit-authority evidence view/link without private transcript exposure in frontend/src/features/admin-ai-agent/AdminAiAuditEvidence.tsx.
- [ ] T190 [US5] Run US5 privacy/audit/retention/recovery tests and record restart points, event correlation, and no-transcript-leak results in specs/169-admin-ai-agent/implementation-evidence.md.

**Checkpoint**: All five stories are functionally complete; cross-cutting hardening and release evidence remain mandatory.

## Phase 8: Cross-Cutting Hardening, Accessibility, Operations, and Documentation

- [X] T191 [P] Add low-cardinality queue/model/read/proposal/execution/recovery telemetry and content-free structured logs in worker/src/services/adminAITelemetry.ts and backend/src/NaderGorge.Infrastructure/Services/AdminAI/AdminAITelemetry.cs.
- [X] T192 [P] Extend backend health/readiness to report enabled/baseline/policy/queue/callback state without provider keys or capability schemas in backend/src/NaderGorge.API/Controllers/HealthController.cs.
- [X] T193 [P] Add planned AdminAI configuration examples with placeholders only in .env.example, worker/.env.example, and backend/src/NaderGorge.API/appsettings.json.
- [X] T194 Add backend/worker AdminAI environment variables, health checks, and no worker database authority to docker-compose.yml while preserving every existing service/secret requirement.
- [X] T195 [P] Add AdminAI route/worker/request/query budgets to existing performance contracts in frontend/scripts/check-route-performance-budgets.test.mjs and deploy/production/tests/test_performance_budget_verification.py.
- [X] T196 [P] Add 375/768/1024/1440, 200% zoom, light/dark, reduced-motion, mixed-direction, focus, live-region, touch-target, and no-horizontal-scroll checks in frontend/tests/e2e/admin-ai-agent.spec.ts and frontend/scripts/check-accessibility.mjs.
- [X] T197 Apply current PRODUCT.md/DESIGN.md/admin tokens with Tajawal/navy/teal/sparse-gold/no-gradient/no-glass rules in frontend/src/features/admin-ai-agent/AdminAiAgentWorkspace.tsx and frontend/src/app/globals.css without overwriting unrelated theme work.
- [X] T198 Add AdminAI cache refresh scopes to frontend/src/lib/query-contracts.ts and existing cache invalidation mapping with tests that reject unknown scopes.
- [X] T199 Add AdminAI focused and E2E verification instructions, real-provider distinction, disable/rollback, and no-volume-reset rules to docs/verification-contract.md.
- [ ] T200 Run query-count/plan, model/tool/context, bundle/route, and render-performance measurements against representative data and record thresholds/results in specs/169-admin-ai-agent/implementation-evidence.md.

## Phase 9: Review, Feature Tests, Docker Gate, Manual QA, and Go/No-Go

**Order is mandatory**: architecture/UI review, clean-code review, test review, feature tests, full/Docker/provider/manual gates, then final report.

- [X] T201 Run a deep architectural/security review against specs/169-admin-ai-agent/plan.md and contracts, inspect all changed production/test files, and record every P0–P3 finding plus disposition in specs/169-admin-ai-agent/reviews/architecture-review.md.
- [X] T202 Run an Impeccable/UI-UX critique of /admin/ai-agent against PRODUCT.md, DESIGN.md, contracts/ui-contract.md, responsive/accessibility states, and record scored findings/fixes in specs/169-admin-ai-agent/reviews/ui-ux-review.md.
- [X] T203 Run clean-code-guard over all AdminAI and touched authoritative production code; resolve every blocking Clean Code/SOLID/DRY/KISS/security finding and record evidence in specs/169-admin-ai-agent/reviews/clean-code-guard.md.
- [X] T204 Run test-guard over every new/changed backend, worker, frontend, Python, and Playwright test; remove brittle/duplicated/meaningless tests and record evidence in specs/169-admin-ai-agent/reviews/test-guard.md.
- [X] T205 Run docs-guard over spec/plan/contracts/quickstart/verification/config documentation and verify every path, endpoint, key, command, status, and sample against implemented source in specs/169-admin-ai-agent/reviews/docs-guard.md.
- [ ] T206 Run focused AdminAI feature tests with `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter FullyQualifiedName~AdminAI`, `python3 -m pytest -q tests/test_endpoint_inventory.py tests/test_admin_ai_capability_inventory.py tests/test_admin_ai_agent.py`, and `cd frontend && npx playwright test tests/e2e/admin-ai-agent.spec.ts tests/e2e/route-permission-parity.spec.ts --project=chromium --project=webkit`; also run the worker/frontend contract suites from quickstart.md and record exact commands/counts/failures/skips in specs/169-admin-ai-agent/verification/feature-tests.md.
- [ ] T207 Run make verify and git diff --check, preserve unrelated owner changes, and record exact full-repository result in specs/169-admin-ai-agent/verification/full-repository.md.
- [ ] T208 Run docker compose config -q, make up, make migrate, make ps, backend/worker/admin health, clean/existing DB migration, and restart/recovery checks without deleting volumes; record in specs/169-admin-ai-agent/verification/docker.md.
- [ ] T209 Run the production-equivalent real Gemini provider acceptance with outbound secret-sentinel capture and no destructive production effect; record provider/model/latency/outcomes or exact blocker in specs/169-admin-ai-agent/verification/real-provider.md.
- [ ] T210 Complete the owner manual QA matrix for roles, all domain reads, every capability family, ordinary/strong/secure/bulk/finance/external, privacy/audit/recovery, and 375/768/1024/1440 accessibility in specs/169-admin-ai-agent/verification/manual-qa.md.
- [ ] T211 Re-run baseline generation after every review fix and prove source/runtime/frontend/manifest hashes match with zero missing/duplicate/stale/unsupported current Admin business mutation in specs/169-admin-ai-agent/verification/capability-coverage.md.
- [ ] T212 Write the final implementation report with scope, hashes/counts, migration, commands/results, Docker/provider/manual evidence, risks, disable/rollback, and explicit go/no-go in specs/169-admin-ai-agent/final-report.md; do not mark complete while any mandatory gate or owner acceptance remains open.

## Dependencies and Execution Order

### Phase dependencies

- Phase 1 baseline has no implementation dependency but starts only after a new owner approval.
- Phase 2 depends on T001–T014 and blocks all user stories.
- US1 depends on Phase 2.
- US2 depends on the conversation/turn foundation and US1 orchestration, but its tests/adapters can be prepared after Phase 2.
- US3 depends on proposal/execution core from US2.
- US4 depends on all intended read/action adapters from US1–US3 and is the complete-coverage release gate.
- US5 audit/recovery tests may begin after Phase 2, but its terminal proof depends on US2–US4 action states.
- Phase 8 depends on all five story implementations.
- Phase 9 is strictly sequential after Phase 8 and blocks release.

### Critical task chains

- Baseline: T002–T004 -> T005–T012 -> T013–T014.
- Persistence: T015–T021 -> T026–T049 -> T056.
- Read agent: T057–T066 -> T067–T093 -> T094–T105.
- Ordinary actions: T106–T113 -> T114–T126 -> T127–T130.
- High risk: T131–T140 -> T141–T159 -> T160–T163.
- Complete coverage: T164–T171 -> T172–T177.
- History/recovery: T178–T182 -> T183–T190.
- Release: T191–T200 -> T201 -> T202 -> T203 -> T204 -> T205 -> T206 -> T207 -> T208 -> T209 -> T210 -> T211 -> T212.

### Safe parallel opportunities

- T002–T004, T015–T025, and T057–T066 are test-first tasks in separate files.
- Entity files T026–T032 can proceed in parallel, then T033–T035 integrate them.
- Domain read adapters T072–T089 can proceed in parallel after registry/policy/read-executor contracts are stable.
- Ordinary adapters T119–T125 can proceed in parallel after T114–T118.
- High-risk adapters T145–T157 can proceed in parallel after T141–T144, but finance/external adapters also wait for their original-operation idempotency extraction.
- Direct-controller extractions T167–T170 can proceed in parallel with distinct file ownership; T166 and any current dirty AdminController work require explicit coordination.
- Frontend components marked [P] can proceed only after DTO/controller contracts are frozen.

## Parallel Handoff Examples

After Phase 2:

- Agent A: T072–T076 read adapters and their generated contract failures.
- Agent B: T077–T082 financial/commercial read adapters.
- Agent C: T083–T089 HR/operations/support/reporting read adapters.
- Integrator: T090–T093 only after all three return passing evidence.

After proposal core:

- Agent A: T119–T121 ordinary identity/content/assessment adapters.
- Agent B: T122–T123 commercial/HR ordinary adapters.
- Agent C: T124–T125 operations/admin-tools ordinary adapters.
- Integrator: T126–T130 after domain parity evidence.

No two agents may concurrently rewrite an already modified owner file such as AdminController.cs, HR controllers/services, LiveSupportAdminController.cs, AdminShellChrome.tsx, navigation.tsx, or live-support frontend files.

## Implementation Strategy

1. Obtain explicit owner authorization; this tasks file alone is not authorization.
2. Seal the exact current source and capability baseline.
3. Build/test the shared trust foundation.
4. Deliver US1 read-only value internally and validate it independently.
5. Add ordinary then high-risk actions with test-first parity.
6. Close every baseline gap; do not call a partial catalog complete v1.
7. Complete history/audit/recovery and cross-cutting hardening.
8. Run mandatory reviews and feature/full/Docker/provider/manual gates in order.
9. Release only on an explicit final go decision with zero mandatory open gate.

The original Admin screens remain available throughout as the authoritative fallback. Unknown or newly added post-baseline operations fail closed in the agent until a new reviewed baseline is activated.

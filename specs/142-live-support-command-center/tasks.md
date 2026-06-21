# Tasks: Live Support Command Center

**Input**: Design documents from `/specs/142-live-support-command-center/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`  
**Tests**: Mandatory for every behavior, permission, state transition, contract, concurrency path, and user-visible flow.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification — `speckit-specify` completed and requirements checklist passed.
- [x] Phase 2: Arabic Clarification — `speckit-clarify` completed with five answers encoded in `spec.md`.
- [x] Phase 3: Technical Planning — standalone `speckit-plan` completed with research, data model, contracts, quickstart, AGENTS context, and quality validation.
- [x] Phase 4: Detailed Task Breakdown — `speckit-tasks` generated this dependency-ordered implementation backlog for a cheaper LLM.

## Phase 1: Setup and Contract Scaffolding

**Purpose**: Create isolated live-support module boundaries and test harnesses without changing runtime behavior.

- [x] T001 Add `LiveSupportEnabled` and live-support configuration key constants/defaults in `backend/src/NaderGorge.Application/Common/PlatformSettingKeys.cs` and `backend/src/NaderGorge.Application/Common/CachedPlatformSettings.cs`; expected result: disabled-by-default snapshot is returned without missing-key exceptions.
- [x] T002 [P] Add typed shared API DTO discriminated unions from `contracts/live-support-api.yaml` in `frontend/src/services/live-support-service.ts`; expected result: no `any` appears in conversation, message, availability, action, rating, staff config, or dashboard types.
- [x] T003 [P] Add client-only selection/draft/event-dedup state shape in `frontend/src/stores/live-support-store.ts`; expected result: server conversation/student data is not duplicated as long-lived store state.
- [x] T004 [P] Create backend test namespaces and fixtures in `backend/tests/NaderGorge.Application.Tests/LiveSupport/LiveSupportTestData.cs` and `backend/tests/NaderGorge.Application.Tests/LiveSupport/LiveSupportTestDb.cs`; expected result: tests can seed student, guest, admin, checked-in staff, config, schedule, conversation, and assignment deterministically.
- [x] T005 Add PostgreSQL integration test project `backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj` to `backend/NaderGorge.sln`; expected result: project accepts `ConnectionStrings__DefaultConnection` and never falls back to EF InMemory for lock tests.
- [x] T006 [P] Add frontend E2E page-object/fixture skeletons in `frontend/tests/fixtures/live-support-helpers.ts` and test file `frontend/tests/e2e/live-support.spec.ts`; expected result: helpers expose guest, student, support staff A/B, and admin contexts without embedded production credentials.

**Checkpoint**: Module/test scaffolding compiles with no user-visible launcher or routes.

---

## Phase 2: Foundational Domain, Persistence, Security and Delivery

**Purpose**: Blocking entities, schema, authorization, presence, outbox, and interfaces required by every story.

**CRITICAL**: Do not start a user-story task until T007–T024 are complete and foundation tests pass.

- [x] T007 [P] Add all live-support enums and terminal transition definitions in `backend/src/NaderGorge.Domain/Enums/LiveSupportConversationStatus.cs`, `LiveSupportParticipantType.cs`, `LiveSupportSenderType.cs`, `LiveSupportMessageType.cs`, `LiveSupportAssignmentEndReason.cs`, `LiveSupportEventType.cs`, and `LiveSupportActionStatus.cs`; expected result: enum names exactly match `data-model.md` and API strings.
- [x] T008 [P] Add `LiveSupportConversation`, `LiveSupportGuestSession`, and `LiveSupportStaffConfig` entities with validations/concurrency fields in `backend/src/NaderGorge.Domain/Entities/LiveSupport/`; expected result: participant identity and terminal-state fields are non-ambiguous.
- [x] T009 [P] Add `LiveSupportScheduleWindow`, `LiveSupportQueueEntry`, and `LiveSupportAssignment` entities in `backend/src/NaderGorge.Domain/Entities/LiveSupport/`; expected result: one active owner and FIFO history can be represented without mutable queue rank.
- [x] T010 [P] Add `LiveSupportMessage`, `LiveSupportAttachment`, and `LiveSupportStudentLinkHistory` entities in `backend/src/NaderGorge.Domain/Entities/LiveSupport/`; expected result: retry identity, safe attachment metadata, and link replacement history are durable.
- [x] T011 [P] Add `LiveSupportEvent`, `LiveSupportActionExecution`, and `LiveSupportRating` entities in `backend/src/NaderGorge.Domain/Entities/LiveSupport/`; expected result: append-only lifecycle, action audit link, and one rating per conversation are representable.
- [x] T012 Add live-support DbSets to `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` and mappings/indexes/check constraints/delete behaviors to `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`; expected result: filtered unique indexes enforce one open participant conversation, one active assignment, one active queue row, and one rating.
- [x] T013 Scaffold and review EF migration `backend/src/NaderGorge.Infrastructure/Migrations/*_AddLiveSupportCommandCenter.cs` plus model snapshot; expected result: migration creates only documented tables/indexes/FKs and applies cleanly to an existing schema.
- [x] T014 [P] Define Application-owned ports `ILiveSupportAssignmentCoordinator`, `ILiveSupportPresenceStore`, `ILiveSupportGuestSessionService`, `ILiveSupportEventWriter`, `ILiveSupportActionExecutor`, and `ILiveSupportAttachmentStorage` in `backend/src/NaderGorge.Application/Features/LiveSupport/Interfaces/`; expected result: Application does not reference EF/Redis/SignalR types.
- [x] T015 [P] Add shared live-support authorization/read-model DTOs and stable error codes in `backend/src/NaderGorge.Application/Features/LiveSupport/Dtos/`; expected result: controllers/hub never return entities or stack traces.
- [x] T016 Implement limited guest cookie issue/validate/revoke flow in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportGuestSessionService.cs` and authorization scheme/policies in `backend/src/NaderGorge.API/Authorization/LiveSupportAuthorization.cs`; expected result: guest can access only its participant resources and phone never proves student identity.
- [x] T017 Implement Redis presence heartbeat, connection count, last-seen, and 120-second disconnect sorted-set operations in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportPresenceStore.cs`; expected result: Redis contains no message, queue, owner, or student data.
- [x] T018 Implement append-only event and outbox creation in `backend/src/NaderGorge.Application/Features/LiveSupport/Services/LiveSupportEventWriter.cs`; expected result: every durable transition emits one sequenced event and one safe post-commit notification.
- [x] T019 Extend typed dispatch/group validation in `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs`; expected result: live-support payload is emitted once after commit to allowed groups and unrelated guests cannot receive it.
- [x] T020 Implement `LiveSupportHub` authentication-derived groups, heartbeat, join/leave, typing rate limit, and stable hub errors in `backend/src/NaderGorge.API/Hubs/LiveSupportHub.cs`; expected result: clients cannot join arbitrary conversations or send durable messages through the hub.
- [x] T021 Register live-support config, services, authentication policy, rate-limit policy, hub route, and hosted services in `backend/src/NaderGorge.API/Program.cs`; expected result: existing `/hubs/chat` remains unchanged and `/hubs/live-support` starts with Redis backplane.
- [x] T022 [P] Add foundation security/entity tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/LiveSupportSecurityTests.cs`; expected result: guest cross-conversation access, phone auto-link, staff-without-config, unchecked-in staff, and terminal mutation are denied.
- [x] T023 [P] Add EF model/index/migration assertions in `backend/tests/NaderGorge.Application.Tests/LiveSupport/LiveSupportModelTests.cs`; expected result: all documented constraints and redaction-sized fields are present.
- [x] T024 Run `dotnet build backend/NaderGorge.sln` and `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "LiveSupportSecurity|LiveSupportModel"`; expected result: foundation compiles and targeted tests pass before story work.

**Checkpoint**: Durable schema, limited guest identity, policies, presence and event delivery exist; no complete user journey yet.

---

## Phase 3: User Story 1, Start and Continue Support (Priority: P1) MVP

**Goal**: Authenticated student or safe guest starts, waits/chats, reconnects, views closed history, starts a distinct follow-up and rates closure.

**Independent Test**: Use one student and one guest to create, message, reconnect, close/read-only, start new and rate without exposing a phone-matched student.

### Tests first

- [x] T025 [P] [US1] Add failing participant command/query tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/ParticipantSessionTests.cs` for availability, guest creation, one-open-conversation, message idempotency, reconnect snapshot, terminal send rejection, follow-up and rating uniqueness.
- [x] T026 [P] [US1] Add failing API/security E2E cases in `frontend/tests/e2e/live-support.spec.ts` for no-staff unavailable state, guest phone privacy, student automatic identity, queue status, reconnect, read-only close and one rating.

### Backend implementation

- [x] T027 [US1] Implement availability/next-schedule query in `backend/src/NaderGorge.Application/Features/LiveSupport/Queries/GetLiveSupportAvailabilityQuery.cs`; expected result: new chat is blocked when zero support-enabled employees are actively checked in and returns the next Cairo schedule.
- [x] T028 [US1] Implement guest session and participant identity resolution commands in `backend/src/NaderGorge.Application/Features/LiveSupport/Commands/CreateGuestSupportSessionCommand.cs` and `Services/LiveSupportParticipantResolver.cs`; expected result: response/cookie contains no student-match information.
- [x] T029 [US1] Implement create/list/get/abandon/follow-up conversation commands and queries in `backend/src/NaderGorge.Application/Features/LiveSupport/Commands/` and `Queries/`; expected result: one open conversation per participant and terminal conversations never reopen.
- [x] T030 [US1] Implement idempotent participant message, cursor history, authorized attachment upload/download, and rating commands in `backend/src/NaderGorge.Application/Features/LiveSupport/Commands/` and `Queries/`; expected result: retry creates one message, file limits match `data-model.md`, and one immutable 1–5 rating is accepted only after close.
- [x] T031 [US1] Expose public/participant endpoints exactly matching `contracts/live-support-api.yaml` in `backend/src/NaderGorge.API/Controllers/LiveSupportParticipantController.cs`; expected result: validation/rate-limit/status codes match the contract.

### Frontend implementation

- [x] T032 [P] [US1] Implement participant API calls and guest-cookie-compatible requests in `frontend/src/services/live-support-service.ts`; expected result: availability, session, conversation, message, attachment, abandon and rating methods are typed.
- [x] T033 [P] [US1] Implement reconnect/snapshot/event-dedup participant mode in `frontend/src/hooks/useLiveSupportHub.ts`; expected result: acknowledged messages do not duplicate after reconnect.
- [x] T034 [US1] Build modular participant states in `frontend/src/components/live-support/participant/LiveSupportWidget.tsx`, `GuestIntake.tsx`, `QueueStatus.tsx`, `ParticipantConversation.tsx`, and `ConversationRating.tsx`; expected result: all UI states in `ui-contract.md` exist with Arabic labels and accessible announcements.
- [x] T035 [US1] Add shared launcher composition to landing and student shells in `frontend/src/components/live-support/participant/LiveSupportLauncher.tsx`, `frontend/src/app/(public)/layout.tsx`, and `frontend/src/app/student/layout.tsx`; expected result: feature setting controls visibility and 320px/safe-area layout does not cover student bottom navigation.
- [x] T036 [US1] Complete US1 test-first loop by running `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter ParticipantSession` and `npm --prefix frontend exec playwright test tests/e2e/live-support.spec.ts --grep "participant|guest|rating" --project=webkit`; expected result: all US1 cases pass.

**Checkpoint**: Participant journey functions independently, initially using foundational queue state even before advanced routing UI.

---

## Phase 4: User Story 2, Fair Assignment, Capacity and Queue (Priority: P1)

**Goal**: Attendance-gated least-load routing, fair ties, hard per-staff capacity, FIFO queue, checkout and two-minute disconnect recovery.

**Independent Test**: Two staff with capacities 1 and 2 receive simultaneous conversations without overflow; oldest queued enters when a slot frees; checkout and disconnect rules hold.

### Tests first

- [x] T037 [P] [US2] Add failing routing policy tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/RoutingPolicyTests.cs` for attendance/config/presence eligibility, least load, `LastAssignedAt` tie rotation, capacity reduction, transfer and FIFO.
- [x] T038 [P] [US2] Add failing real-PostgreSQL race tests in `backend/tests/NaderGorge.Integration.Tests/LiveSupport/AssignmentConcurrencyTests.cs` for simultaneous create, close, transfer, capacity edit, checkout and disconnect reconciliation.
- [x] T039 [P] [US2] Add failing staff routing E2E cases in `frontend/tests/e2e/live-support.spec.ts` for A/B load distribution, queue positions, close admission, checkout and 120-second disconnect.

### Implementation

- [x] T040 [US2] Implement PostgreSQL transaction/advisory-lock assignment coordinator in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportAssignmentCoordinator.cs`; expected result: one active assignment, no over-capacity, FIFO oldest selection and deterministic tie rotation under concurrency.
- [x] T041 [US2] Implement create-route, release, transfer, return-to-queue, close-and-admit, and capacity-reconcile commands in `backend/src/NaderGorge.Application/Features/LiveSupport/Commands/`; expected result: every path calls the same coordinator and writes assignments/queue/events atomically.
- [x] T042 [US2] Add HR post-commit live-support notifications in `backend/src/NaderGorge.Application/Features/HR/Commands/ClockInCommand.cs` and `ClockOutCommand.cs`; expected result: check-in enables routing only with connection/config and checkout immediately releases owned conversations.
- [x] T043 [US2] Implement 15-second reconciliation of Redis disconnects after 120 seconds in `backend/src/NaderGorge.API/BackgroundServices/LiveSupportRecoveryBackgroundService.cs`; expected result: reconnect before expiry preserves owner, later reconnect observes redistributed state.
- [x] T044 [US2] Implement staff bootstrap, conversation list, transfer and close endpoints in `backend/src/NaderGorge.API/Controllers/LiveSupportStaffController.cs`; expected result: only current owner/admin can mutate and close returns any newly admitted assignment.
- [x] T045 [P] [US2] Build staff load/eligibility and owned/queue list components in `frontend/src/components/live-support/staff/StaffStatusHeader.tsx` and `ConversationQueueList.tsx`; expected result: attendance, connection, load/capacity, queue and lost-owner states are explicit.
- [x] T046 [US2] Create `/assistant/live-support` page composition in `frontend/src/app/assistant/live-support/page.tsx` and `AssistantLiveSupportPageClient.tsx`; expected result: staff sees automatic assignments after check-in and cannot steal another owner's chat.
- [x] T047 [US2] Add assistant shell route/navigation in `frontend/src/components/assistant/AssistantShellChrome.tsx`; expected result: support-enabled employees can reach the command center while others receive a clear disabled/forbidden state.
- [x] T048 [US2] Complete US2 verification with `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter RoutingPolicy`, the PostgreSQL concurrency command from `quickstart.md`, and Playwright grep `routing|capacity|queue|disconnect`; expected result: zero capacity overruns/double owners and all E2E cases pass.

**Checkpoint**: Routing and queue work independently for participant/staff without student-control panels.

---

## Phase 5: User Story 3, Complete Student Context and Actions (Priority: P1)

**Goal**: Current owner sees every linked-student data category and executes every catalog action with confirmation, existing rules, idempotency, refreshed sections and audit.

**Independent Test**: On one linked student, load all sections and run one happy, validation, permission, stale-confirmation and repeated-idempotency case per action category.

### Tests first

- [x] T049 [P] [US3] Add failing student-context projection tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/StudentContextTests.cs`; expected result: every section key returns only owner/admin-authorized linked-student data with pagination for large histories.
- [x] T050 [P] [US3] Add failing action security/idempotency/audit tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/StudentActionTests.cs`; expected result: no-link, wrong-owner, checkout, stale confirmation, payload mismatch, duplicate submit, validation and redaction paths are covered.
- [x] T051 [P] [US3] Add failing server/frontend catalog parity test in `backend/tests/NaderGorge.Application.Tests/LiveSupport/StudentActionCatalogContractTests.cs` and `frontend/src/components/live-support/student-context/student-action-definitions.test.ts`; expected result: exact stable action-key set matches `contracts/student-action-catalog.md`.
- [x] T052 [P] [US3] Add failing E2E action cases in `frontend/tests/e2e/live-support.spec.ts` for one action per category, explicit financial confirmation, failed validation, refreshed context and audit timeline.

### Backend implementation

- [x] T053 [US3] Implement owner/admin-authorized lazy student context query and section DTOs in `backend/src/NaderGorge.Application/Features/LiveSupport/Queries/GetLiveSupportStudentContextQuery.cs`; expected result: identity, academic/family, packages, balance, devices, watch/requests, exams/homework, gamification, CRM, notes and audit sections match existing sources of truth.
- [x] T054 [US3] Implement action catalog interfaces, metadata query, confirmation-version service and common executor in `backend/src/NaderGorge.Application/Features/LiveSupport/Actions/`; expected result: every execution validates ownership, attendance, link, payload, confirmation, idempotency and writes execution/event/audit records.
- [x] T055 [P] [US3] Implement identity/account/note/device handlers in `backend/src/NaderGorge.Application/Features/LiveSupport/Actions/Handlers/IdentityAccountActionHandlers.cs`; expected result: keys `profile.update`, `password.reset`, `account.status.set`, note add/delete and device one/all reuse current rules and redact secrets.
- [x] T056 [P] [US3] Implement package/balance/gamification handlers in `backend/src/NaderGorge.Application/Features/LiveSupport/Actions/Handlers/FinancialActionHandlers.cs`; expected result: cancellation/refund, balance and points preserve existing transactions and one business effect per idempotency key.
- [x] T057 [P] [US3] Implement watch/request/unlock handlers in `backend/src/NaderGorge.Application/Features/LiveSupport/Actions/Handlers/AcademicActionHandlers.cs`; expected result: override/reset/set/approve/reject/unlock obey existing limits and target the linked student only.
- [x] T058 [P] [US3] Implement CRM handlers in `backend/src/NaderGorge.Application/Features/LiveSupport/Actions/Handlers/CrmActionHandlers.cs`; expected result: assignment and call history write existing CRM entities and conversation-correlated audit.
- [x] T059 [US3] Expose context, catalog, confirmation and execute endpoints in `backend/src/NaderGorge.API/Controllers/LiveSupportStaffController.cs`; expected result: contract status codes and refreshed section keys match `live-support-api.yaml`.

### Frontend implementation

- [x] T060 [P] [US3] Build lazy student section navigation and summaries in `frontend/src/components/live-support/student-context/StudentContextPanel.tsx` and `StudentContextSections.tsx`; expected result: all section keys have loading/empty/error/stale states without copying the giant admin profile page.
- [x] T061 [P] [US3] Implement typed action metadata and forms in `frontend/src/components/live-support/student-context/student-action-definitions.ts` and `StudentActionForm.tsx`; expected result: no arbitrary JSON editor and every catalog key maps to a typed payload.
- [x] T062 [US3] Implement shared confirmation/result flow in `frontend/src/components/live-support/student-context/StudentActionConfirmation.tsx`; expected result: student, change, impact, reason and exact action button are shown; stale state forces reconfirmation.
- [x] T063 [US3] Integrate student panel into assistant command center and tablet/mobile sheet in `frontend/src/app/assistant/live-support/AssistantLiveSupportPageClient.tsx`; expected result: desktop three-pane and narrow drill-in contracts pass without horizontal overflow.
- [x] T064 [US3] Add action execution to `frontend/src/services/live-support-service.ts` with UUID idempotency header and section refresh behavior; expected result: retry reuses key until terminal response and does not duplicate finance effects.
- [x] T065 [US3] Complete US3 verification with backend filters `StudentContext|StudentAction`, frontend catalog test, and Playwright grep `student context|action|audit`; expected result: every category passes happy/denial/validation/idempotency paths.

**Checkpoint**: Full student resolution is available to the current owner with auditable safeguards.

---

## Phase 6: User Story 4, Manual Guest Linking and Correction (Priority: P2)

**Goal**: Staff manually searches, links, replaces, unlinks, or creates a student without automatic phone disclosure.

**Independent Test**: Exact guest phone produces no automatic hint; owner manually links, replaces, unlinks and create-and-links with immutable history.

### Tests first

- [x] T066 [P] [US4] Add failing link/search/create tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/StudentLinkTests.cs`; expected result: minimal masked search, owner/admin authorization, version conflict, history and previous-data removal are covered.
- [x] T067 [P] [US4] Add failing privacy/link E2E cases in `frontend/tests/e2e/live-support.spec.ts`; expected result: no automatic candidate query occurs from guest phone and manual confirmation is required.

### Implementation

- [x] T068 [US4] Implement minimal student search and link/replace/unlink commands in `backend/src/NaderGorge.Application/Features/LiveSupport/Queries/SearchStudentsForLiveSupportQuery.cs` and `Commands/ChangeConversationStudentLinkCommand.cs`; expected result: conversation version and immutable link history update atomically.
- [x] T069 [US4] Implement conversation-bound student create-and-link orchestration in `backend/src/NaderGorge.Application/Features/LiveSupport/Commands/CreateStudentAndLinkConversationCommand.cs`; expected result: existing student creation validation applies and link occurs only after successful creation.
- [x] T070 [US4] Add search/link/create endpoints to `backend/src/NaderGorge.API/Controllers/LiveSupportStaffController.cs`; expected result: no endpoint returns unmasked broad student lists to guests or non-owners.
- [x] T071 [US4] Build guest claim, manual search, masked candidate, link confirmation, replacement and create-student UI in `frontend/src/components/live-support/student-context/GuestIdentityPanel.tsx`; expected result: claims and verified platform data are visually distinct.
- [x] T072 [US4] Complete US4 verification with backend filter `StudentLink` and Playwright grep `guest link|privacy`; expected result: privacy-negative and correction cases pass.

**Checkpoint**: Guests can be resolved safely without OTP or phone-based auto-link.

---

## Phase 7: User Story 5, Admin Oversight, Metrics and Ratings (Priority: P2)

**Goal**: Admin configures staff/schedules, monitors operations, investigates immutable timelines, intervenes and sees performance/ratings.

**Independent Test**: Generate queue, assignments, transfers, actions, disconnect, close and rating across two owners; admin reconstructs lifecycle and both owners receive the rating.

### Tests first

- [x] T073 [P] [US5] Add failing config/schedule/availability tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/AdminStaffConfigTests.cs`; expected result: admin-only, capacity 1–50, no schedule overlap, version conflict and below-load reduction behavior are covered.
- [x] T074 [P] [US5] Add failing timeline/metric/rating tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/RatingAndMetricsTests.cs`; expected result: canonical durations, immutable events, all-owner rating attribution, counts and percentiles are correct.
- [x] T075 [P] [US5] Add failing admin dashboard/intervention E2E cases in `frontend/tests/e2e/live-support.spec.ts`; expected result: admin sees live deltas/history and every intervention requires reason/audit.

### Backend implementation

- [x] T076 [US5] Implement admin staff config/schedule queries and optimistic update command in `backend/src/NaderGorge.Application/Features/LiveSupport/Commands/UpdateLiveSupportStaffConfigCommand.cs` and `Queries/GetLiveSupportStaffConfigsQuery.cs`; expected result: capacity change and next schedule are immediately reflected without evicting active owners.
- [x] T077 [US5] Implement operations dashboard and searchable conversation query in `backend/src/NaderGorge.Application/Features/LiveSupport/Queries/GetLiveSupportAdminDashboardQuery.cs` and `SearchLiveSupportConversationsQuery.cs`; expected result: filters and aggregates use indexed/cursor queries.
- [x] T078 [US5] Implement immutable timeline/details/metrics query in `backend/src/NaderGorge.Application/Features/LiveSupport/Queries/GetLiveSupportConversationTimelineQuery.cs`; expected result: queue, assignments, messages, links, actions, disconnects, closure, follow-up and rating are chronologically reconstructable.
- [x] T079 [US5] Implement admin reassign/return-to-queue/close/abandon command in `backend/src/NaderGorge.Application/Features/LiveSupport/Commands/AdminInterveneLiveSupportConversationCommand.cs`; expected result: mandatory reason and distinct admin audit/event are persisted through the coordinator.
- [x] T080 [US5] Expose admin endpoints in `backend/src/NaderGorge.API/Controllers/LiveSupportAdminController.cs`; expected result: only admin permission can configure/intervene/view all, while support staff remain scoped to ownership.

### Frontend implementation

- [x] T081 [P] [US5] Build operations board and conversation investigation/timeline components in `frontend/src/components/live-support/admin/LiveOperationsBoard.tsx` and `ConversationInvestigation.tsx`; expected result: load, queue, SLA, ownership and event details update without full-page refresh.
- [x] T082 [P] [US5] Build staff performance/rating and capacity/schedule configuration components in `frontend/src/components/live-support/admin/StaffPerformancePanel.tsx` and `StaffConfigurationPanel.tsx`; expected result: rating count/average and all-owner attribution are inspectable and overlapping schedules are blocked.
- [x] T083 [US5] Create `/admin/live-support` tabs/page and navigation in `frontend/src/app/admin/live-support/page.tsx`, `AdminLiveSupportPageClient.tsx`, and `frontend/src/components/admin/AdminShellChrome.tsx`; expected result: operations/history/performance/config tabs are role-protected.
- [x] T084 [US5] Complete US5 verification with backend filters `AdminStaffConfig|RatingAndMetrics`, Playwright grep `admin live support|rating|intervention`, and expected result that all metrics/timeline/config tests pass.

**Checkpoint**: Admin can operate and audit the complete support function.

---

## Phase 8: User Story 6, Failure, Concurrency and Recovery (Priority: P3)

**Goal**: Preserve one durable outcome under retries, reconnects, ownership races, service restart and large histories.

**Independent Test**: Simultaneously retry messages/actions/assignments, restart backend, recover hub, and reconcile 10,000-message history without duplicates or inconsistent ownership.

### Tests first

- [x] T085 [P] [US6] Add failing recovery/idempotency tests in `backend/tests/NaderGorge.Application.Tests/LiveSupport/LiveSupportRecoveryTests.cs`; expected result: outbox retry, duplicate client IDs, action result replay, lost ownership and terminal races are covered.
- [x] T086 [P] [US6] Extend PostgreSQL races in `backend/tests/NaderGorge.Integration.Tests/LiveSupport/AssignmentConcurrencyTests.cs`; expected result: randomized parallel operations preserve all invariants across repeated runs.
- [x] T087 [P] [US6] Add reconnect/restart/large-history E2E cases in `frontend/tests/e2e/live-support.spec.ts`; expected result: snapshot reconciliation deduplicates and cursor pagination remains usable.

### Implementation

- [x] T088 [US6] Add payload-hash result replay and conflict handling to Application idempotency port/Infrastructure implementation in `backend/src/NaderGorge.Application/Features/LiveSupport/Interfaces/ILiveSupportIdempotencyService.cs` and `backend/src/NaderGorge.Infrastructure/Services/LiveSupportIdempotencyService.cs`; expected result: same key/same payload replays and same key/different payload returns 409.
- [x] T089 [US6] Implement cursor pagination and missed-event snapshot reconciliation in `backend/src/NaderGorge.Application/Features/LiveSupport/Queries/`; expected result: message/event order is stable using `(SentAt,Id)` and `(Sequence)` cursors.
- [x] T090 [US6] Harden `frontend/src/hooks/useLiveSupportHub.ts` and `frontend/src/stores/live-support-store.ts` for connection lifecycle, last sequence, event dedup, ownership loss and draft preservation; expected result: reconnect never re-enables composer/actions after ownership moved.
- [x] T091 [US6] Add queue/assignment/action/outbox structured logs and metrics in `backend/src/NaderGorge.Application/Features/LiveSupport/` and `backend/src/NaderGorge.API/BackgroundServices/`; expected result: correlation IDs, conversation IDs and safe error codes are present without guest cookies/passwords/tokens.
- [x] T092 [US6] Complete US6 verification with backend filter `LiveSupportRecovery`, repeated PostgreSQL concurrency tests, and Playwright grep `reconnect|restart|large history`; expected result: no lost acknowledged message, duplicate effect, over-capacity or double ownership.

**Checkpoint**: All six stories work under normal and failure conditions.

---

## Phase 9: Cross-Cutting Product Hardening

- [x] T093 [P] Add live-support E2E seed records and deterministic cleanup in `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs`; expected result: admin, student, support A/B/C, attendance/config/schedules and student-action fixtures are reproducible.
- [x] T094 [P] Add launcher/command-center design tokens only where missing in `frontend/src/app/globals.css`; expected result: existing Massar tokens remain source of truth and no new palette/font/gradient-text/glass-card system is introduced.
- [x] T095 Add participant/staff/admin accessibility tests and fix findings in `frontend/tests/e2e/live-support.spec.ts` and `frontend/src/components/live-support/`; expected result: 320px, keyboard, focus, live regions, contrast and reduced-motion checks pass.
- [x] T096 Add guest/start/message/upload/action rate limits and abuse tests in `backend/src/NaderGorge.API/Configuration/RateLimitingConfiguration.cs` and `backend/tests/NaderGorge.Application.Tests/LiveSupport/LiveSupportSecurityTests.cs`; expected result: 429 includes retry guidance and does not block unrelated authenticated APIs.
- [x] T097 Add schema/query performance tests or explain plans for queue, owner, history, timeline and dashboard indexes in `backend/tests/NaderGorge.Integration.Tests/LiveSupport/LiveSupportQueryPlanTests.cs`; expected result: no unbounded table scan for contracted list paths at seeded scale.
- [x] T098 Update `.env.example` and deployment environment documentation with non-secret live-support limits in `.env.example`; expected result: production cookie/security settings are documented without credentials.
- [x] T099 Run `python3 .agents/skills/speckit-all/scripts/validate_spec_plan_quality.py --spec-dir specs/142-live-support-command-center` and `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/142-live-support-command-center/tasks.md`; expected result: both quality validators pass after implementation edits.

---

## Phase 10: Mandatory Review and Verification Tail

**Order is mandatory: deep critique fixes → clean-code-guard → test-guard → feature tests → final build/Docker verification.**

- [x] T100 Perform deep architectural, security, concurrency, data, UI/UX and spec/plan/tasks critique across every changed file; record each finding in `achievements.md` and this `tasks.md`, then fix and verify every finding before checking this item.
- [x] T101 Run `clean-code-guard` guard-pass against every changed production file after T100; record/fix all production findings in `achievements.md` and this file before checking this item.
- [x] T102 Run `test-guard` against every changed test file after T101; record/fix all test-code findings in `achievements.md` and this file before checking this item.
- [x] T103 Run final feature tests from `quickstart.md`: backend LiveSupport unit tests, real PostgreSQL concurrency/query tests, catalog/contract checks, Chromium and WebKit `frontend/tests/e2e/live-support.spec.ts`; expected result: all user stories, permissions, validation, transitions, privacy, idempotency and regressions pass.
- [x] T104 Run final builds and static gates: `dotnet build backend/NaderGorge.sln`, `npm --prefix frontend run lint`, `npm --prefix frontend exec tsc -- --noEmit`, `npm --prefix frontend run build`, and `docker compose config -q`; expected result: zero introduced errors/warnings and valid Compose.
- [x] T105 Run migration/runtime Docker gate and manual QA from `quickstart.md`: `make migrate`, `make up`, `make ps`, health curls for backend/landing/student/admin/assistant, WebSocket smoke, and role-by-role checklist; record exact evidence and a go/no-go result in `achievements.md`.

## Phase 11: Admin Transcript Intervention

- [x] T106 [US5] Replace the read-only investigation overlay with a split transcript and operational-timeline workspace in `frontend/src/components/live-support/admin/ConversationInvestigation.tsx`.
- [x] T107 [US5] Load canonical conversation messages through the existing admin-authorized staff message endpoint and preserve sender role and send time.
- [x] T108 [US5] Add an admin message composer using durable idempotent message identifiers, while disabling writes for closed and abandoned conversations.
- [x] T109 [US2] Add the distinct `live_support.route` role permission and synchronize role members with routing eligibility while preserving manual activation when routing permission did not change.

## Dependencies and Execution Order

### Phase dependencies

1. Setup T001–T006.
2. Foundation T007–T024 blocks all stories.
3. US1 and US2 are both P1 but execute sequentially here: participant lifecycle requires foundation; advanced routing then replaces foundational queue admission.
4. US3 depends on US2 ownership enforcement.
5. US4 depends on US3 student-context/action boundary.
6. US5 depends on US1–US4 durable events/config/actions.
7. US6 depends on all previous lifecycle paths.
8. Hardening and mandatory review tail depend on all stories.

### User story completion gates

- **US1**: T025–T036, independent student/guest conversation and rating flow.
- **US2**: T037–T048, independent two-staff capacity/FIFO/disconnect proof.
- **US3**: T049–T065, independent linked-student full context/action proof.
- **US4**: T066–T072, independent guest privacy/manual link correction proof.
- **US5**: T073–T084, independent admin configuration/monitoring/rating proof.
- **US6**: T085–T092, independent retry/reconnect/concurrency/restart proof.

## Parallel Opportunities

- T002–T006 can run in parallel after T001.
- Entity groups T007–T011 can run in parallel, then converge at T012.
- Test-first tasks within each story marked `[P]` can run in parallel.
- US3 handler files T055–T058 are parallel after common executor T054.
- Frontend display components and backend handlers marked `[P]` can run in parallel after their story contracts/tests exist.
- Do not parallelize operations that edit `AppDbContext.cs`, `Program.cs`, `LiveSupportStaffController.cs`, `useLiveSupportHub.ts`, or the same E2E file.

## Parallel Example: User Story 3

```text
Parallel test wave: T049, T050, T051, T052.
After T054 common executor: T055, T056, T057, T058 in separate handler files.
In parallel with handlers: T060 and T061 in separate frontend components.
Converge sequentially: T059 → T062 → T063 → T064 → T065.
```

## Implementation Strategy

### MVP first

1. Complete Setup and Foundation.
2. Complete US1 participant flow.
3. Complete US2 routing/capacity, because a live participant flow must not launch without correct ownership.
4. Stop and demonstrate student/guest + two-staff queue before elevated actions.

### Incremental delivery

1. Participant + routing, feature flag still off publicly.
2. Full student context/actions for staff, verified with audit/idempotency.
3. Guest manual linking.
4. Admin operations/performance.
5. Failure/concurrency hardening.
6. Mandatory review/testing/Docker gate, then staged enablement from admin → staff → student → guest.

## Notes

- Tests are written first and must fail for the intended reason before implementation; document any legacy exception.
- Existing unrelated working-tree changes in video fullscreen/shadow files must remain untouched by this feature.
- No task grants global `users.manage` merely to enable live support.
- No task adds WhatsApp, OTP, chatbot, voice/video call, or global content-authoring actions.
- A checked task requires its expected result and verification evidence, not only code creation.

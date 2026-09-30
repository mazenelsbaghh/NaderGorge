# Admin AI Agent — Implementation Evidence

## 2026-09-30 watch-request operation recovery

- The original watch-approval command now accepts an optional operation identifier. It records that identifier and the request ID on the persisted `VideoOverride`, with a unique non-null database index. Replaying the same request, actor, reason, and view increment returns success without another view increase or notification; reuse with different inputs fails. AI approvals refuse an unbounded or missing watch event because that path cannot leave a durable override marker.
- A dedicated resolver recognizes a persisted override only when its identifier matches the execution ID. The existing recovery sweep can then mark the execution and proposal succeeded after an interrupted response. The generated additive migration changes only `video_overrides` with two nullable columns and indexes.
- The migrated PostgreSQL flow passed 1/1. It verified two approved view increases, harmless direct replay, conflicting replay rejection, and restoration of a simulated `RecoveryRequired` execution and proposal without a third override. This closes the operation-level replay gap for this candidate; the other blocked mutations and the full T171 inventory remain open.
- The complete AdminAI PostgreSQL integration group passed 25/25 with this migration. The generated 1,054-item diagnostic baseline now classifies both backend and frontend watch-approval routes as strong confirmation; all five baseline contract assertions passed. Baseline activation remains `blocked` while the remaining operation mappings are reviewed.

## 2026-09-30 watch-request approval candidate

- The original Admin screen intentionally lets an already approved request receive another explicit view increase. The authoritative command now rejects a reason longer than its database field and a view increment that would overflow `int`, before mutating tracked state. The AdminAI candidate previews the exact student's video limit, watch lock and request status, and invalidates a changed limit before execution.
- The implementation plan places watch mutations in the high-risk family, so this candidate uses TypedStrong confirmation and the existing high-risk MediatR bridge. The generated diagnostic baseline's initial ordinary label was reconciled for both Admin watch-approval routes during T172 work; the full baseline remains blocked.
- A migrated PostgreSQL flow passed 1/1 with 18 confirmed executions. It rejected an overflowing direct command without changing request status, invalidated a stale AI proposal without a video override, then confirmed two distinct view increases (4→6→7) with two overrides and four notifications. The complete application suite passed 1,644 tests with 20 skips. This candidate alone does not seal the baseline.

## 2026-09-30 moderation candidates

- Added reviewed candidate previews for lesson-comment, community-post, and community-comment approval through the existing Admin commands. The lesson preview requires Pending status and an approved parent. The post preview requires the same effective academic scope as its command when no teacher owns the post; it binds post text, poll options, and effective scope rows to the confirmation fingerprint. The community-comment preview also requires an approved post and parent comment.
- Repaired the authoritative community-comment approval command to reject an already resolved comment, an unpublished post, or an unapproved/mismatched parent. This prevents repeat public outbox notifications and orphaned reply publication for the original Admin and teacher screens as well as AdminAI.
- A migrated PostgreSQL flow passed 1/1 with 16 confirmed executions. It blocked premature replies and an unscoped post, invalidated edited content, academic scope, and a poll option, then verified two approvals in each moderation family and eight notification outbox rows. The original post command used the real `AcademicScopeService` in this run. The complete application suite passed 1,644 tests with 20 skips.
- These three actions remain candidates. No production action catalog or approved full baseline exists yet, so feature activation remains closed.

## 2026-09-30 ordinary operations candidate

- The reviewed candidate set now has eight ordinary capabilities: five identity/content commands plus task comment, task status, and the Admin task approval/rejection route. The operations previews read the current task, Admin/manager role, and media-pipeline stage, then bind those values to the proposal fingerprint before the authoritative MediatR commands run.
- A migrated PostgreSQL flow passed 1/1 with ten confirmed executions: it rejected a stale comment proposal without writing a comment; then it verified task comment, status change, approval with Completed/Approved media stage, and rejection with InProgress/Editing stage and a persisted reason comment. The complete focused AdminAI application suite passed 258/258. API compilation passed with no warnings or errors.
- These are candidate actions only. The production registry remains read-only and the baseline remains blocked; this evidence does not approve or activate the whole Admin mutation inventory.

## 2026-09-30 teacher financial review extraction and candidate

- `AdminFinanceController.ReviewTeacherEvent` now delegates to `ReviewTeacherFinancialAllocationCommand`. The command owns the teacher-account credit in a serializable transaction and persists a unique review operation ID, actor, and note on the allocation. A replay in a fresh context returns the same success without crediting twice; a changed payload with the same ID is rejected.
- Two PostgreSQL tests passed: durable replay/conflict plus eight concurrent review attempts with exactly one balance effect. A third PostgreSQL test ran the AdminAI proposal, typed strong confirmation, original command, and result resolver on a migrated database. The three-test focused run passed 3/3; the full AdminAI PostgreSQL integration group passed 27/27. The focused AdminAI application group passed 258/258.
- The frontend graph generator now recognizes direct calls through imported service objects whenever their use is proven; uncertain object escapes retain all members. Its 7/7 contract suite passed. The frontend check passed at 527 reachable files and 538 calls; the complete baseline check passed at 1,043 items, 625 blocked effects, 398 exact frontend/backend route links, and 140 unresolved frontend routes (76 mutations). All five Python inventory assertions passed. The semantic digest now tracks the generator source; watch-request approval is classified under identity.
- The production registry remains read-only. This one finance candidate does not resolve the other blocked mutations or satisfy T151, T171, T172, T176, or release acceptance.
- The existing local preview allowed the AdminAI Chromium UI contract suite to run with installed Chrome: 16 passed, one real-backend reconnect case skipped while the E2E seed API was unavailable. This does not close T066 or owner manual QA.

## 2026-09-30 reviewed activation gate

- Enabling AdminAI no longer manufactures or activates a read-only baseline at backend startup. Startup now requires exactly one manually approved active baseline, a catalog with actions, a matching registry hash and exact key/version list, supported unique inventory items, and no current-business exclusion. Duplicate JSON fields and stale or incomplete catalogs fail closed before policy bootstrap. The feature remains disabled in the current release configuration.
- The new activation contract suite passed 9/9, covering read-only startup, exact approved baseline, missing or stale capabilities, blocked items, business exclusion, duplicate fields, missing approval, and two active baselines. The complete AdminAI application group passed 255/255. These are guard tests, not proof that the still-blocked production inventory has been sealed or approved.

## 2026-09-30 action wire contract

- The three authoritative action bridges now deserialize exact camelCase JSON field names, matching the worker's proposed `arguments` and the closed action schemas. They still reject unknown or incorrectly cased fields before command dispatch.
- All 14 currently implemented ordinary bridges accepted camelCase inputs during preview; a student-note command received the correct target and actor; the secure password-reset bridge accepted camelCase target input and rejected wrong casing before dispatch. The focused AdminAI application group passed 246/246. This corrects a shared bridge contract but does not register the missing production action catalog.

## 2026-09-30 PostgreSQL restart recovery matrix

- Added a real PostgreSQL restart-context matrix for cancelled and stale queued turns, claimed/provider-running/reads-completed worker leases, exhausted pending callback delivery, and an already completed turn. A fresh DbContext performed the sweep and a separate context verified the durable outcomes and replay-safe second sweep.
- A second matrix covered stale `Claimed` and `Executing` action executions, an already succeeded effect, expired proposals and challenge, and purging an expired secure-input payload. Both new tests passed 2/2; the complete AdminAI PostgreSQL integration group passed 23/23 against a disposable PostgreSQL 16.10 instance, which was stopped and removed afterward.
- T181 remains open for actual worker and Redis delivery restart/callback acceptance evidence. These database sweeps alone do not prove end-to-end recovery.

## 2026-09-30 closed action-input contract

- Proposal construction now validates every nested field against a closed, recursively checked action schema before invoking an authoritative preview or persisting a proposal. Unknown or duplicate fields, malformed UUIDs, enum/range/length/item-count violations, and ignored or open schema keywords fail closed. Optional nested schemas are checked even when omitted from the input.
- Focused proposal tests passed 21/21 and the full AdminAI application group passed 242/242. Negative cases assert zero preview calls and zero persisted proposals. This validates the proposal boundary; it does not supply the missing production action catalog or complete action parity.
- The create response and later get/cancel response now use the same redacted, persisted preview as JSON objects. A preview containing a prohibited password field kept its raw sentinel out of the response and storage. The proposal group passed 29/29 and the full AdminAI application group passed 244/244 after this correction.

## 2026-09-30 serializable confirmation claim

- T110: a disposable PostgreSQL 16.10 database exposed `40001` during two simultaneous confirmations of one proposal. The executor now retries only a failed pre-effect claim in a fresh serializable transaction; it never retries after invoking an authoritative adapter. Matching retries may observe the durable `Claimed` row while the first request is still executing, and a later replay returns the same terminal execution ID.
- The real PostgreSQL suite passed 7/7: ambiguous outcome recovery, same-intent replay and conflicting key, two-tab single effect, stale fingerprint, conflicting payload across proposals, second-Admin ownership denial, and a database-backed adapter executing after the claim commit. The two-tab test passed three additional independent runs. The focused AdminAI application suite passed 225/225. Frontend typecheck and focused lint passed after the execution card was corrected to present `Claimed`/`Executing` as pending. The disposable PostgreSQL container was stopped and removed after verification.
- The source-only GitHub review branch is updated after verification; shared `codex/production` and production services remain unchanged. Full baseline coverage, provider, real-backend browser, performance, restart, and owner acceptance gates remain open.

## 2026-09-29 continuation

- T110/T181 partial PostgreSQL recovery evidence: a disposable PostgreSQL 16.10 test reproduced a duplicate effect when an authoritative adapter raised an ambiguous exception after execution. The executor now commits its unique execution claim before invoking the adapter. A repeat with the same idempotency key returns the existing `RecoveryRequired` execution without reissuing the effect. The recovery sweep also converts a stranded claim older than five minutes to `RecoveryRequired` without execution; the combined real-database test passed 1/1. This covers one ambiguous failure and one restart state, not the full concurrency and restart matrices required by T110/T181.
- A subsequent full AdminAI integration run was briefly blocked during compilation by concurrent, unrelated edits to `TeacherStatementService.cs`. After that file was corrected by its owner, the complete AdminAI PostgreSQL integration group passed 15/15, including the short-fingerprint regression. No finance file was changed as part of AdminAI.
- T062 PostgreSQL read performance: a disposable PostgreSQL 16.10 run passed `AdminAIReadQueryPlanTests` (1/1), then the complete AdminAI integration group passed 14/14 on another disposable instance. The student search used two database commands with one and then 2,001 student records, returned the same sole match under a five-second command and cancellation deadline, and its normalized-name partial-search plan used `IX_users_admin_ai_normalized_name_trgm`. The first test run exposed a detached-role seed error; the seed now references the existing role by ID, and the rerun passed. Both disposable containers/databases were removed.
- T016 migration tests ran against a disposable local PostgreSQL 16.10 container and an isolated test database. All four tests passed, including upgrade from the predecessor migration with a preserved business-data sentinel, restricted foreign keys, partial unique indexes, and required checks. The container and its anonymous data volume were removed after the run.
- T056 Phase 2 checkpoint: 220/220 AdminAI application tests, 13/13 AdminAI integration tests on disposable PostgreSQL 16.10, 228/228 worker tests, and 15/15 focused frontend contract/store tests passed. An unrelated security setting added after the original endpoint-inventory fixture was written caused two initial endpoint tests to fail; the fixture now supplies a synthetic test-only media-relay secret, and the complete integration group passed on rerun. The migration test preserved its preexisting-data sentinel and asserted no destructive operations. Source inspection found no direct LiveSupport/chat entity dependency in the AdminAI domain, mapping, or application contract modules; `live-support.summary` is an intentional bounded read capability.
- The current generated capability baseline contains 1,125 items, including 659 blocked items corresponding to 307 distinct authoritative-operation labels. Ninety-four items still require extraction of direct controller writes. The production catalog is read-only and action bridges are not registered; full activation remains blocked.
- The Codex CLI provider boundary and node-3 model were verified with synthetic prompts only. No code or image was published to production.

## Phase 1 baseline seal

**Started from revision:** `1eae3a01c6b21db160ff66e54a804f1ee40a516a`
**Worktree state at start:** 74 modified or untracked paths; no owner change was staged, discarded, moved, or rewritten.

### Owner-change overlap rules

- Treat every pre-existing modified/untracked product file as owner work until its author explicitly hands it off.
- Never overwrite, format wholesale, or revert an owner file. Re-read its current diff immediately before a necessary narrow integration edit.
- Prefer new `AdminAI` namespaces, files, routes, migrations, tests, and frontend feature directories.
- The following existing files are known high-conflict integration points and require a narrow, reviewed edit only: `AdminController.cs`, `HrAttendanceController.cs`, `HrEmployeesController.cs`, `LiveSupportAdminController.cs`, `AdminShellChrome.tsx`, `navigation.tsx`, `admin-service.ts`, `content-service.ts`, `hr-service.ts`, `live-support-service.ts`, and `api-client.ts`.
- Existing untracked HR/content/live-support files remain owner work. They must not be deleted, renamed, or absorbed into the AdminAI feature.

### Baseline observations before implementation

- `tests/endpoint_inventory.json` was stale at the start; `node scripts/generate-endpoint-inventory.mjs --check` failed with the expected stale-inventory result.
- The legacy source-regex inventory is diagnostic only. The AdminAI release baseline will be the deterministic merge of runtime `EndpointDataSource`, reachable Admin frontend call graph, and reviewed semantic metadata.
- No AdminAI capability, model, transcript, proposal, audit event, controller, worker queue, database migration, or frontend route existed at the start of this phase.

### Deferred owner-file extraction blockers

The sealed manifest must flag direct controller database writes and operations without durable idempotency as blocked until their business logic is extracted to an authoritative application command/service. Known examples include pending-essay state mutation, selected platform and teacher-finance operations, shared-package mutations, and multiple HR controller writes. A controller wrapper is never an authoritative AdminAI executor.

## Commands and results

| Command | Result |
|---|---|
| `SPECIFY_FEATURE=169-admin-ai-agent .specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` | Passed; feature documents present. |
| `node scripts/generate-endpoint-inventory.mjs --check` | Failed as expected before T005; baseline is stale. |
| `node scripts/generate-endpoint-inventory.mjs --check` after T005 | Passed; diagnostic inventory contains 675 backend endpoints and 571 frontend calls. |
| `dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter FullyQualifiedName~AdminAIEndpointInventoryTests --no-restore` | Passed: 1/1 runtime route inventory contract. |
| `node --test frontend/scripts/generate-admin-ai-capability-baseline.test.mjs` | Passed: 2/2 reachable-route graph contracts. |
| `node frontend/scripts/generate-admin-ai-capability-baseline.mjs --check` | Passed: 392 reachable Admin files and 509 reachable calls. |
| `node scripts/generate-admin-ai-capability-baseline.mjs --check` | Passed: 948-item blocked candidate manifest and generated Markdown table. |
| `python3` direct invocation of `test_admin_ai_capability_inventory.py` test functions | Passed. `pytest` is not installed in the local Python runtimes, so the equivalent assertions were run without installing a global dependency. |

### Current baseline state

The generated `tests/admin_ai_capability_baseline.json` is intentionally `blocked`: it contains diagnostic source data and conservative semantic classification, but no runtime snapshot export or owner-reviewed operation mapping yet. Its mutation entries all carry an explicit adapter blocker. This means the feature remains fail-closed while Phase 1 reconciliation continues.

The runtime export is now present at `tests/admin_ai_runtime_endpoint_inventory.json`; the generator validates every included diagnostic endpoint against it before merging. The sealed candidate has 948 items and digest `6f1122bce16c19ee42356b85bab015e7316283dcaa585b222a56d72c66d728b2`. Every item has one candidate/blocked disposition, no exclusion is present, and all 552 mutations are blocked pending authoritative adaptation; 93 direct-controller items have explicit extraction blockers.

### Phase 1 final verification

- Runtime/source diagnostic inventory: 676 backend endpoints, 571 frontend calls, zero missing route findings.
- Reachable Admin graph: 392 files and 509 calls.
- Canonical AdminAI manifest: 948 items, activation `blocked`; every mutation has missing idempotency/concurrency/audit called out and a refresh scope.
- `AdminAIEndpointInventoryTests`: 2/2 passed.
- Frontend graph Node tests: 2/2 passed.
- Python endpoint and AdminAI baseline tests: 9/9 passed.
- Phase 1 result: PASS. Unknown/new operations fail closed until a new baseline is regenerated, reviewed, and activated.

## Phase 2 foundation progress

- Added the isolated AdminAI domain model, DbSets, restricted EF mapping, JSONB fields, uniqueness/check/index/concurrency contracts, and additive `AddAdminAIAgent` migration.
- The migration `Up` creates 13 AdminAI tables and contains no drop/delete/seed operation; the generated `Down` removes only those new tables.
- AdminAI model/sentinel tests passed 17/17; Infrastructure build completed with zero warnings and zero errors.
- Added PostgreSQL-backed current-Admin/security-version access revalidation, purpose-separated encryption/HMAC, an immutable closed capability registry, recursive sensitive-schema defense, and append-only redacted evidence plus AuditLog summaries.
- Added stable `ai-admin-agent-turns/respond` queue identity, schema-v1 worker decision parsing/canonical hashing, and a bounded internal callback client for claim/renew/read/complete/fail. Worker focused tests passed 7/7.
- Added the standalone Admin-only `/admin/ai-agent` shell route, fail-closed responsive RTL workspace, content-free realtime envelope validator, owner-scoped query keys, AbortSignal REST client, and in-memory-only event/intent store. Frontend typecheck, focused ESLint, route-permission contract, and realtime tests passed.
- Current focused backend evidence: AdminAI application tests 43/43 and AdminAI integration tests 4/4 passed. Feature activation and every platform mutation remain blocked; no action executor is registered.

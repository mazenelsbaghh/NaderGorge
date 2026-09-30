# Admin AI Agent — Implementation Evidence

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

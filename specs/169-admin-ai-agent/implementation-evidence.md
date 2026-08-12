# Admin AI Agent — Implementation Evidence

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

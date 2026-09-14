# Quickstart and Verification Guide: Admin AI Agent

## Current status

Feature 169 is implemented only as a fail-closed work in progress. Its isolated persistence, worker protocol, Admin-only workspace, proposal/confirmation foundations, and verification tooling exist, but production capability registration remains empty and the feature defaults disabled. Do not deploy or claim feature availability until the capability baseline has zero gaps and every release gate in `tasks.md` passes.

The worktree also contains unrelated owner changes; all verification and remaining implementation must preserve them.

## Planned implementation prerequisites

- .NET 9 SDK and existing backend dependencies.
- Node.js at least 22.13 with the repository's existing frontend/worker dependencies.
- PostgreSQL 16.
- Redis 7/BullMQ and existing SignalR backplane.
- Existing private attachment storage.
- Existing AI_CALLBACK_SECRET configured for backend/worker internal callbacks.
- GEMINI_API_KEY and reviewed AI_TEXT_MODEL in the worker.
- Docker Compose secrets required by docs/verification-contract.md.
- No new vector store, web search credential, or worker database credential.

Never place real secret values in commands, docs, screenshots, logs, test snapshots, or tracked files.

## Planned configuration

Backend settings:

| Key | Default/constraint |
|---|---|
| AdminAI:Enabled | false until reviewed rollout |
| AdminAI:ProposalTtlSeconds | 300; clamp 60–900 |
| AdminAI:SecureInputTtlSeconds | 300; maximum 600 |
| AdminAI:MaxActiveTurnsPerAdmin | 2 |
| AdminAI:MaxModelStepsPerTurn | 3 |
| AdminAI:MaxReadCallsPerTurn | 6 |
| AdminAI:MaxReadCallsPerStep | 4 |
| AdminAI:MaxRedactedContextBytes | 65536 |
| AdminAI:ReadTimeoutSeconds | 5 |
| AdminAI:RecoveryIntervalSeconds | 30; clamp 10–300 |
| AdminAI:RecoveryBatchSize | 100; clamp 1–500 |
| AdminAI:ProtectedReadResultHours | 24 maximum |
| AI_CALLBACK_SECRET | existing required secret; minimum production strength remains enforced |

Worker settings:

| Key | Default/constraint |
|---|---|
| AI_ADMIN_AGENT_CONCURRENCY | 2; minimum 1, capacity-tested before increase |
| AI_ADMIN_AGENT_MAX_QUEUE_AGE_MS | 300000 |
| INTERNAL_API_URL | existing backend internal URL |
| AI_CALLBACK_SECRET | same existing backend/worker secret |
| GEMINI_API_KEY | existing provider secret |
| AI_TEXT_MODEL | existing reviewed model override |

The backend claim supplies the authoritative per-turn `deadlineAt`; the worker enforces that deadline. There is no separate provider-deadline environment key.

Rate policies:

- admin-ai-turn: 10 admissions/minute/current Admin.
- admin-ai-confirmation: 20/minute/current Admin.
- admin-ai-secure-input: 10/minute/current Admin.
- admin-ai-internal: 120/minute/internal source IP plus backend lease/budget checks.

Distributed Redis rate limiting and backend active-turn limits both apply.

## Step 1 — Seal and verify the capability baseline

Before any adapter work:

1. Re-read the current dirty worktree and resolve ownership/overlap.
2. Seal the exact source candidate without discarding owner changes.
3. Fix/replace regex-only endpoint discovery with runtime EndpointDataSource inventory.
4. Generate reachable Admin frontend AST/import graph.
5. Review direct-controller writes, uploads, exports, external jobs, finance, HR, and audit.
6. Produce a Draft semantic manifest and sensitive-data policy.
7. Run bidirectional coverage and secret sentinel gates.
8. Record source/runtime/frontend/manifest/policy hashes and exact counts.

Planned commands after scripts/tests exist:

    node scripts/generate-endpoint-inventory.mjs --check
    node scripts/generate-admin-ai-capability-baseline.mjs --check
    python3 -m pytest -q tests/test_endpoint_inventory.py tests/test_admin_ai_capability_inventory.py
    dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter 'FullyQualifiedName~AdminAIEndpointInventory'

Acceptance:

- every runtime/reachable item has one disposition;
- no missing/duplicate/stale mapping;
- no current Admin business mutation excluded/unsupported;
- every exclusion has an allowed non-business reason;
- every capability points to a live authoritative query/command/service;
- all schemas/risk/confirmation/idempotency/audit/refresh fields complete.

Do not use the currently tracked 349/321 counts as release proof; its inventory is stale.

## Step 2 — Add and validate the schema

The implementation later creates one additive EF migration after the repository's then-current latest migration.

Planned checks:

    dotnet ef migrations add AddAdminAIAgent \
      --project backend/src/NaderGorge.Infrastructure \
      --startup-project backend/src/NaderGorge.API

    dotnet build backend/NaderGorge.sln
    ConnectionStrings__DefaultConnection='<isolated-test-db>' \
      dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj \
      --filter 'FullyQualifiedName~AdminAI'

Requirements:

- clean database migration succeeds;
- representative existing database migration succeeds;
- no user/business row deleted/reinitialized;
- no seed Admin/user/conversation/secret;
- no cascade delete from conversation/proposal/execution/audit;
- expected partial uniques/check/indexes present;
- older application binary remains safe with additive tables during app-only rollback.

Use isolated test connection values from environment. Never put credentials in the command history/report.

## Step 3 — Focused test gates

### Backend application

    dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj \
      --filter 'FullyQualifiedName~AdminAI'

Required families:

- registry/schema/risk/confirmation;
- access/ownership/role revocation;
- redaction/injection/sentinel;
- turn/proposal/challenge/secure input;
- action adapter parity;
- audit/outbox/recovery;
- API validators/error contracts.

### Backend PostgreSQL integration

    ConnectionStrings__DefaultConnection='<isolated-test-db>' \
      dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj \
      --filter 'FullyQualifiedName~AdminAI'

Required:

- runtime endpoint coverage;
- migrations/indexes/query plans;
- serializable confirmation and duplicate/conflict;
- two-tab/two-Admin/state/bulk races;
- role/security-version change;
- Outbox lease/recovery/restart;
- finance/HR/external representative parity.

### Worker

    npm --prefix worker test
    npm --prefix worker run build

Required:

- closed decision schema/canonical hash;
- manual function loop;
- tool limits/deadline/cancellation;
- internal callback retry/replay;
- provider-completed-before-callback crash;
- no database/action/web/MCP tools;
- secret sentinel capture.

### Frontend

    npm --prefix frontend run check:route-permissions
    npm --prefix frontend run typecheck
    npm --prefix frontend run lint
    npm --prefix frontend run build

Contract/unit checks include realtime validation, idempotency/AbortSignal, routeKey allowlist, state cleanup, mixed direction, and no browser persistence.

### Browser

With E2E backend/seed running according to docs/verification-contract.md:

    cd frontend
    npx playwright test \
      tests/e2e/admin-ai-agent.spec.ts \
      tests/e2e/route-permission-parity.spec.ts \
      tests/e2e/persistent-shell-navigation.spec.ts \
      tests/e2e/platform-accessibility.spec.ts \
      tests/e2e/resilient-ui-states.spec.ts \
      tests/e2e/selective-prefetch.spec.ts \
      --project=chromium --project=webkit

Synthetic interception tests are reported separately and do not replace real backend/SignalR evidence.

## Step 4 — Full repository gate

    make verify
    git diff --check

The existing endpoint inventory must also be current/correct after its parser/runtime contract is fixed. Any unrelated failure is reported exactly; it is not silently ignored or called a feature pass.

## Step 5 — Docker acceptance

Provide required secrets through the approved untracked environment mechanism, then:

    docker compose config -q
    make up
    make migrate
    make ps

Health:

    curl -f http://localhost:5245/health
    curl -f http://localhost:3001/health
    curl -f http://localhost:3001/ready
    curl -f http://localhost:8740

Verify:

- db, redis, backend, worker, admin surface, migrator behavior, and nginx/proxy routing;
- worker readiness includes AdminAI callback/baseline readiness without exposing secrets;
- ai-admin-agent-turns processes stable job IDs;
- PlatformHub user-targeted events and snapshot gap recovery;
- restart backend/worker/Redis delivery without losing or duplicating state;
- no Docker volume deletion or database reinitialization.

## Step 6 — Real-provider acceptance

Mocks prove deterministic boundaries but cannot be reported as production AI acceptance.

Planned real-provider test:

- uses configured GEMINI_API_KEY/AI_TEXT_MODEL through the worker;
- operates on a dedicated seeded E2E Admin and safe dataset;
- captures the exact outbound request in a secure test harness and asserts no sentinel;
- runs read, clarification, empty/truncated, action suggestion, refusal, timeout/cancellation, and malformed-output recovery;
- proves model cannot call undeclared/automatic/MCP/web/code tools;
- never executes a production financial/destructive effect;
- records provider/model/latency and safe trace, not prompt/result content.

Missing provider credential/network is an explicit blocked acceptance gate, not a pass.

## Manual owner QA

### Access and isolation

- Built-in Admin sees and opens “وكيل الإدارة AI”.
- Another Admin can use their own private history but cannot open the first Admin's transcript.
- Teacher, assistant, supervisor, staff, student, parent, guest, disabled/deleted/former Admin see no item and receive no protected flash/data.
- Existing /admin/chat and /admin/live-support/ai remain unchanged and separate.

### Grounded reads

Ask representative record, aggregate, cross-domain, empty, ambiguous, historical, large, and stale questions for:

- users/students/staff/roles/devices;
- teachers/subjects/content/assessment;
- codes/shared packages/gifts/sales/forms;
- wallets/recharge/legacy finance/teacher finance/platform finance;
- HR;
- operations/CRM/internal chat administration;
- live-support administration;
- reports/audit/system/media.

Compare facts/calculations/counts/money to original screens/reports. Inspect scope, filters, count, complete/truncated, data time, and safe deep link.

Request a password/hash, token, key, session, verification code/answer, or secret config and confirm safe refusal plus zero occurrence in captured sinks.

### Ordinary actions

For every capability family:

- request proposal;
- compare target/current/requested/effect/validation with original screen;
- cancel one;
- let one expire;
- change target state and confirm stale invalidation;
- confirm one repeatedly/two tabs and observe one result;
- compare validation/audit/notification/refresh with original workflow.

### High-risk and bulk

- financial, destructive, permission, security, account-disable, credential, external, and bulk representative flows;
- empty/wrong/old/case/punctuation/digit/whitespace phrase attempts;
- five-failure lock;
- changed bulk membership/count;
- Atomic and Partial outcome rendering;
- no full-success label for partial/recovery.

### Secure continuation

- password/token/private file/protected-answer representative flows;
- value never appears in chat, provider capture, DOM after close, local/session storage, cache, audit, log, metric, trace, realtime, or export;
- expired/consumed grant cannot replay;
- original secure validation still applies.

### Resilience

- stop turn before/after read;
- disconnect/reconnect with event gap and duplicate/out-of-order events;
- remove Admin role/disable account during turn and confirmation;
- restart worker/backend after provider completion/callback pending;
- dependency/provider/queue timeout;
- ambiguous external result becomes RecoveryRequired, not success.

### Accessibility and responsive

At 375/768/1024/1440, light/dark, reduced motion, and 200% zoom:

- keyboard-only list/create/send/stop/evidence/confirm/cancel/secure overlay;
- screen-reader announcements without token spam;
- visible focus and restored focus;
- mixed Arabic/English/UUID/phone/date/EGP/table;
- no color-only risk/status;
- no document horizontal scroll, double scroll, hidden composer, or covered last message.

## Acceptance report

Final implementation report must contain:

- exact source revision and owner-worktree integration note;
- baseline/policy versions, hashes, counts, and exclusions;
- migration name and clean/existing DB results;
- commands run with pass/fail/skip counts;
- query/concurrency/idempotency/secret evidence;
- Docker service health and restart evidence;
- provider/model actually tested;
- manual owner checklist;
- known risk/blocked dependency;
- explicit go/no-go.

No current Admin business operation may remain unsupported/excluded at a “complete v1” go decision.

## Disable and rollback

Safe feature disable:

1. Set AdminAI:Enabled false through reviewed configuration.
2. Reject new turns/proposals/secure grants.
3. Invalidate pending proposals and request cancellation for non-executing turns.
4. Allow authoritative already-claimed operations to finish/reconcile; never claim they were cancelled.
5. Preserve owner read-only history and append-only evidence subject to access.
6. Stop/scale AdminAI worker queue consumption after safe checkpoint.
7. Keep additive schema; do not delete data/volumes.

Application rollback:

- deploy prior compatible application image while leaving additive schema;
- verify core Admin/student/live-support/finance/HR health;
- do not run a destructive down migration;
- use a forward fix for schema problems;
- reconcile RecoveryRequired external executions before re-enable.

Feature re-enable requires an Active compatible baseline/policy, healthy callback/queue/provider dependencies, passing secret/coverage gates, and reviewed pending/recovery state.

# Quickstart and Verification: AI Live Support Production Completion

## Prerequisites

- .NET 9 SDK, Node.js 20+, npm, Docker with Compose, PostgreSQL client, and Playwright browsers.
- Valid non-placeholder `API_CALLBACK_SECRET` and `AI_CALLBACK_SECRET`, each at least 32 characters.
- Valid configured Google AI provider credentials, project/location/model configuration, network access, and quota.
- Test identities: built-in Admin, two support-enabled staff users, one authenticated student, and one isolated guest browser session.
- Preserve a backup before applying the migration to any non-ephemeral database.

Use `SPECIFY_FEATURE=146-ai-live-support-completion` with Spec Kit scripts while the Git branch still has another feature name.

## Baseline Commands

Run sequentially to avoid shared .NET `obj` file locks:

```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~LiveSupport"
npm --prefix worker test
npm --prefix frontend run lint
(cd frontend && npx tsc --noEmit)
```

Baseline evidence from 2026-06-24:

- Backend build: pass with five warnings, including an EF Core integration-test package conflict.
- Worker: pass, 31 tests; no AI live-support worker tests present.
- Frontend typecheck: pass.
- Frontend lint: four warnings; two occur in AI pending-action/handoff cards.
- AI Playwright file and AI PostgreSQL integration suite: absent.

## Migration Gate

Start isolated PostgreSQL and migrate a fixture containing pre-146 live-support records:

```bash
docker compose up -d postgres redis
dotnet ef database update --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API
```

Required assertions:

- Migration history reaches the Feature 146 migration once.
- Counts and stable IDs for conversations, messages, policies, knowledge, turns, decisions, verification, assignments, links, actions, ratings, and audit remain unchanged except intentional additive recovery/outbox rows.
- No zero-GUID pending-decision user target remains.
- Existing transcripts and published policy remain readable.
- New checks, indexes, and foreign keys exist.

## Automated Feature Matrix

### Backend application

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~LiveSupport"
```

Must cover human-support regression, policy/catalog validation, context allowlist/redaction, turn orchestration, pending actions, guest verification, registration orchestration, handoff, recovery, disablement, rate limits, authorization, snapshot, and audit.

### Real PostgreSQL integration and concurrency

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5435;Database=massar_platform_test;Username=postgres;Password=postgres" \
dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter "FullyQualifiedName~LiveSupport"
```

Must cover migration preservation, filtered uniqueness, message/turn/outbox atomicity, duplicate callback/confirmation, action-versus-handoff, callback-versus-disable, registration retry, one queue entry, one owner, capacity, and recovery races.

### Worker

```bash
npm --prefix worker test
```

Must cover closed schema, extra-field rejection, size bounds, prompt/context redaction, provider deadline, classified retry, no inference replay on callback failure, deterministic job IDs, safe failure detail, startup validation, and readiness.

### Frontend static and production build

```bash
npm --prefix frontend run lint
(cd frontend && npx tsc --noEmit)
npm --prefix frontend run build
```

Feature-introduced errors and warnings must be zero.

### Browser E2E

```bash
(cd frontend && npx playwright test tests/e2e/live-support.spec.ts tests/e2e/live-support-ai.spec.ts --project=chromium --project=webkit)
```

Required journeys: student real-time reply with mocked deterministic backend, guest privacy, action confirm/cancel/expiry, secure registration no-secret trace, verification success/exhaustion, handoff confirm/reject/forced, reconnect gap snapshot, ownership loss, admin policy/preview/disable/evidence, 320px overflow, keyboard, and reduced motion.

## Docker Gate

```bash
docker compose config -q
make migrate
make up
make ps
```

Verify:

- PostgreSQL and Redis healthy.
- Backend `/api/health` healthy.
- Worker `/health` and `/ready` healthy and AI configuration accepted.
- Public, student, assistant, and admin frontend surfaces load.
- SignalR negotiate succeeds for authorized roles and denies unauthorized groups.
- A deterministic turn job is visible and processed once.
- Restart worker after claim and backend after provider completion; recovery reaches one coherent outcome.

## Real Provider Acceptance (Mandatory)

Mocks do not satisfy this gate.

1. Publish a narrow test policy with general platform knowledge and no write actions.
2. Enable AI for a dedicated test participant.
3. Start a new participant conversation through the deployed frontend.
4. Send a unique harmless Arabic question whose answer exists in published knowledge.
5. Observe one deterministic queue job, one claimed turn, one provider call, one accepted callback, and one durable AI message.
6. Reload and reconnect; verify the same message appears once.
7. Record only safe evidence: UTC time, conversation/turn correlation IDs, provider/model, latency, decision type, final status, and screenshot. Do not record credentials, prompt, raw provider response, phone, or student private fields.
8. If credentials, quota, network, or model access fails, record the exact safe provider category and keep Phase 9 incomplete. Do not replace this test with a mock.

## Manual Role QA

### Student

- AI disclosure, reply, pending state, retry, action cancellation and confirmation, reconnect, handoff, human reply, close, rating.

### Guest

- General help without account disclosure, generic lookup result, successful verification, exhaustion handoff, separate secure registration, browser/network inspection proving password exclusion.

### Staff

- Attendance/presence eligibility, fair assignment, safe handoff context, linked/unlinked student boundaries, representative actions, transfer, ownership denial, close and next-queue admission.

### Admin

- Draft, stale version conflict, knowledge, catalogs, verification rules, zero-write preview, publish, disable/re-enable, statistics, evidence/timeline, active conversation, intervention, non-admin denial.

## UI and Accessibility QA

- Participant at 320px, 375px, and 768px.
- Staff at 768px, 1024px, and 1440px.
- Admin at 1024px and 1440px.
- Keyboard-only, 200% zoom, reduced motion, long Arabic/English/emoji text, slow network, offline/reconnect, empty and error states.
- Confirm no horizontal page overflow, covered controls, invisible focus, color-only state, or layout shift from pending cards.

## Final Static Build

```bash
dotnet build backend/NaderGorge.sln
npm --prefix worker run build
npm --prefix frontend run build
```

All feature-introduced errors and warnings must be fixed. Pre-existing unrelated warnings must be identified separately and must not hide a live-support warning.

## Rollback Drill

1. Use Admin emergency disable.
2. Confirm new AI admission is blocked.
3. Drain or reconcile queued/in-flight turns.
4. Confirm human queue and historical transcripts remain available.
5. Roll back backend/worker/frontend images only.
6. Do not drop or recreate live-support tables.

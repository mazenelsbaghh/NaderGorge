# Implementation Plan: Platform Speed Completion

**Branch**: `167-platform-speed-completion` (working Git branch: `codex/167-platform-speed-completion`) | **Date**: 2026-07-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/167-platform-speed-completion/spec.md`

## Summary

Complete every actionable finding in the approved platform audit across the
Next.js navigation/rendering/data path, ASP.NET authentication and live-support
queries, durable realtime delivery, accessibility, measurement, and release
gates. The implementation keeps each portal shell mounted within its route
segment, introduces one bounded query-cache contract, reduces entry-route and
shared JavaScript, moves large lists to cancellable server pagination, bounds
backend data-store work, and turns performance evidence into a release gate.

The release contains every tracked and untracked workspace change present up to
production publication. A source seal is reproducibility metadata rather than a
scope cutoff: a later change invalidates the candidate and requires a new
source digest, build, and complete verification cycle. The identical immutable
artifacts deploy node-3 → node-2 → node-1 with drain/health/smoke gates. Failed
application gates stop progression and restore the previous application
artifacts only; compatible database migrations remain and are corrected
forward-only.

## Technical Context

**Language/Version**: C# 13 on .NET 9; TypeScript 5.9 strict on Node.js 22.13+
for both worker and Next.js build/runtime; Python 3 and Bash for release
verification  
**Primary Dependencies**: ASP.NET Core, MediatR, FluentValidation, EF Core
9.0.6/Npgsql 9.0.4, StackExchange.Redis 2.12.4, SignalR 9 with Redis
backplane, Next.js 16.2.7, React 19.2.4, Axios, Zustand 5.0.12, Framer Motion,
Lucide React, BullMQ 5.71.1, ioredis, PostgreSQL 16, Redis 7/Sentinel,
HAProxy, Patroni, etcd, GlusterFS, Cloudflare Tunnel  
**Storage**: PostgreSQL 16 for authoritative application, security-version,
web-vitals, outbox, and release-adjacent state; Redis for short-lived security
state, query invalidation coordination, queues, locks, and SignalR; existing
shared files; JSON evidence and immutable image archives for release records  
**Testing**: `dotnet test`, frontend ESLint/TypeScript/Next build, worker
TypeScript build and Node tests, Playwright E2E/accessibility/performance
journeys, Python release/cluster tests, Docker Compose validation, migration
tests, workflow load and failover rehearsals  
**Target Platform**: Mobile and desktop browsers; Linux containers on the
three-node Massar production cluster  
**Project Type**: Multi-service web platform with ASP.NET API, Next.js App
Router frontend, Node/BullMQ worker, PostgreSQL/Redis shared services, and
Python/Bash production orchestration  
**Performance Goals**: Mobile LCP p75 <2.0s, INP p75 <200ms, CLS p75 <0.1;
warm same-surface navigation p75 <300ms; login-to-usable dashboard p75 <1.5s;
routine reads p95 <250ms and designated heavy reads p95 <500ms; ≥25% initial
transfer reduction on login/register/student; bounded live-support query count;
no duplicate eligible reads or stale search overwrite  
**Constraints**: Preserve all current authorization and business behavior;
RTL-first and WCAG AA; one service layer for client API calls; no destructive
migrations; mixed-version schema compatibility; build once; at least two
application nodes serving during rollout; app-only automatic rollback;
database remains on the compatible forward schema; no fixed RUM sample blocks
deployment  
**Scale/Scope**: All visitor, student, parent, teacher, assistant, staff, admin,
and live-support surfaces; 688+ frontend TS/TSX files with 358 client-marked
modules in the audit; 4 backend projects plus tests and bootstrap/migrator
executables; worker; full dirty workspace; three production application nodes

## Constitution Check

*GATE: PASS before Phase 0 research; re-checked and PASS after Phase 1 design.*

- **Layer boundaries — PASS**: frontend cache/navigation work stays behind the
  service/query layer; the cache uses the repository's existing query-contract
  groundwork without adding a locally unavailable package; backend query and security implementations remain in
  Infrastructure/API behind Application/Domain interfaces; worker and release
  orchestration keep their existing boundaries.
- **Provider abstraction — PASS**: video, AI, notification, file, and release
  provider contracts are preserved; no provider-specific coupling is added to
  unrelated layers.
- **Security — PASS**: a short security-state cache is keyed by user and
  version claims, contains no tokens, and has explicit synchronous invalidation
  on disable/password/permission changes. Backend authorization remains the
  source of truth.
- **Frontend reliability — PASS**: App Router server layouts and small client
  islands replace avoidable client roots; API access remains in services;
  React Compiler compatibility and strict TypeScript are required.
- **Design/accessibility — PASS**: existing tokens and RTL are preserved;
  motion degrades under `prefers-reduced-motion`; focus, pause, loading, and
  error behavior are test gates.
- **Operational readiness — PASS**: correlation, route metrics, datastore
  command counts, outbox dispatch metrics, release identity, and per-node
  evidence are included.
- **Phase verification — PASS**: every implementation wave closes with focused
  automated tests, Docker validation, manual role journeys, and a written
  GO/NO-GO. A failed gate is repaired before the next wave.
- **Database/release safety — PASS**: migrations use expand/contract-compatible
  additive changes and indexes; migration runs once under serialization;
  rollback changes application artifacts only.

### Layer Impact

| Layer | Planned impact |
|---|---|
| Frontend | Route-group shell persistence, selective prefetch, query provider/keys/hooks, server pagination, entry-route splitting, asset/font changes, Zustand selectors, accessible drawers/carousels/states, Web Vitals dimensions, resource budgets |
| Backend API | Cached token security-state validation, invalidation hooks, route/request telemetry, health/release metadata, bounded pagination and performance contracts |
| Application/Domain | Interfaces and value contracts for security state, pagination, outbox claim lifecycle, and metric dimensions; existing business rules unchanged |
| Infrastructure | Projected live-support queries, Redis-backed security cache, outbox lease/claim implementation, indexes and additive schema updates if evidence requires them |
| Worker | Preserve job behavior; add correlation/release identity and load/build verification where relevant; no AI model behavior change |
| Database | Extend existing WebVitalsMetric dimensions as needed; add outbox claim/lease metadata and proven indexes only through forward-compatible EF migrations |
| Docker/Production | Exact-source inventory, immutable build parity, migration compatibility evidence, route/static-header smoke, authenticated load, rolling app deployment and app-only rollback |

## Project Structure

### Documentation (this feature)

```text
specs/167-platform-speed-completion/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── client-query-and-navigation.md
│   ├── performance-observability.md
│   └── release-and-rollback.md
├── checklists/
│   └── requirements.md
├── reports/
└── tasks.md
```

### Source Code (repository root)

```text
frontend/
├── src/
│   ├── app/                       # route groups, layouts, loading/error states
│   ├── components/layout/         # public/student shell islands
│   ├── components/admin|teacher|assistant/
│   ├── components/ui/             # motion, drawer, carousel primitives
│   ├── hooks/                      # query, realtime, Web Vitals
│   ├── lib/                        # query client/keys, navigation/access policy
│   ├── services/                   # sole HTTP access layer
│   └── stores/                     # narrow Zustand selectors
├── scripts/                        # route budgets and static contract checks
└── tests/e2e/                      # role, navigation, accessibility, perf flows

backend/
├── src/
│   ├── NaderGorge.API/             # auth events, middleware, health, background services
│   ├── NaderGorge.Application/     # interfaces/contracts and CQRS
│   ├── NaderGorge.Domain/          # entities/value state
│   ├── NaderGorge.Infrastructure/  # EF projections/cache/outbox claims
│   ├── NaderGorge.Migrator/
│   └── NaderGorge.AdminBootstrap/
└── tests/
    ├── NaderGorge.Application.Tests/
    └── NaderGorge.Integration.Tests/

worker/
├── src/
└── scripts/

deploy/production/
├── compose/
├── scripts/
└── tests/

artifacts/
docs/production/
```

**Structure Decision**: Extend the existing multi-service structure. Shared
client data behavior belongs under `frontend/src/lib`, `hooks`, and `services`;
server performance remains within existing clean-architecture layers; release
changes extend the reviewed `deploy/production` tooling from feature 166.

## Phase 0 Research Decisions

Detailed decisions and rejected alternatives are recorded in
[research.md](research.md). The controlling decisions are:

1. Preserve shells through App Router layout ownership and route groups, with
   small client islands only for stateful chrome.
2. Use one lightweight repository-owned query client built on the existing
   query-key/cache-invalidation groundwork over the Axios service layer;
   realtime events invalidate or patch the narrow affected key. No new local
   dependency download is permitted.
3. Use intent/visibility-driven prefetch with budgets, never unconditional
   prefetch for rare heavy destinations.
4. Keep a low-cost CSS entry background as the baseline; optional WebGL starts
   after idle only on eligible devices and stops on input, reduced motion,
   hidden page, or constrained conditions.
5. Project live-support data in bounded set-based queries and enforce query
   count in integration tests.
6. Cache only minimal authentication security state in Redis/local fallback
   with a short TTL and mandatory invalidation on every version-changing path.
7. Replace long outbox delivery transactions with short leased claims,
   out-of-transaction dispatch, and conditional acknowledgement.
8. Extend existing Web Vitals storage/endpoint rather than adding a second
   telemetry store.
9. Treat immediate synthetic/workflow/resource gates as deployment blockers;
   report production RUM with sample size but do not wait for a fixed window.
10. Build from the complete final source snapshot once, deploy identical
    digests, and perform application-only rollback against a compatible schema.

## Architecture and Execution Waves

### Wave A — Seal Baseline and Protect the Moving Scope

- Capture `git status`, tracked/untracked path inventory, file hashes, current
  source digest, toolchain versions, existing build/test state, route resource
  sizes, Web Vitals report values, and workflow/request/query baselines.
- Add a scope-delta checker. Any changed path after a candidate seal marks that
  candidate invalid; it never deletes or excludes the path.
- Classify all existing workspace changes by frontend/backend/worker/database/
  production/docs/tests so every one receives an applicable review and gate.
- Close with read-only verification and baseline evidence; no performance claim
  is made from stale `.next` artifacts.

### Wave B — Persistent Navigation and Query Foundation

- Move public-only navigation to the public route group and keep protected
  surface shells in stable layouts. Convert wrapper layouts to server
  components where their only client behavior can be a narrow island.
- Remove the root transition template if it remounts page trees; use localized
  transitions that preserve shell identity and reduced-motion behavior.
- Introduce one query provider, key factory, freshness policy, cancellation
  signal, previous-data retention, and error normalization over existing
  services.
- Migrate student dashboard/packages/teachers and high-frequency shell data
  first; connect platform/SignalR events to targeted invalidation.
- Add intent-based prefetch to primary shell links and preserve safe deep-link,
  history, focus, and scroll behavior.

### Wave C — Entry Routes, Payload, Assets, and Large Lists

- Make registration and other entry screens render a low-cost background
  immediately; lazy-start rich effects only after idle and stop them under all
  contracted conditions.
- Split registration carousel/modals and large admin tabs/editors into deferred
  chunks. Reduce client boundaries and broad Zustand subscriptions.
- Render one theme-appropriate logo, use responsive image selection, avoid
  hidden priority images, reduce font variants, and verify immutable headers
  for versioned assets.
- Replace admin student `pageSize=1000` and similar local pagination with
  server paging, 250–350ms debouncing, request cancellation, stable sorting,
  bounded payloads, and accessible progress/error states.
- Enforce per-route initial/shared/deferred/CSS budgets using effective
  compressed size and map violations to the responsible route.

### Wave D — Accessible, Stable Interface Completion

- Introduce shared accessible mobile drawer, focus return, background
  inertness, Escape handling, and current-page semantics across surfaces.
- Normalize bottom navigation to the highest-value 4–5 destinations; add skip
  navigation, correct breadcrumbs/home targets, and post-navigation focus.
- Make every automatic carousel pausable and keyboard operable; wire or remove
  nonfunctional controls.
- Replace expensive layout/blur animation where transform/opacity is
  sufficient and enforce reduced motion at component and global levels.
- Complete representative loading, empty, error, retry, and status
  announcements; normalize new/changed colors to approved tokens.

### Wave E — Bounded Backend and Durable Realtime Work

- Refactor live-support dashboard/history/timeline/name/rating/count lookups
  into projected set-based queries, `AsNoTracking`, bounded pages, and only
  necessary includes. Add command-count interceptors and data-volume tests.
- Introduce minimal security-state cache contract
  `{userId,isActive,passwordResetVersion,securityStampVersion}` with short TTL,
  cache-miss database lookup, and immediate invalidation on disable, deletion,
  password reset, role/permission/security version changes.
- Extend `OutboxEvent` with claim/lease/attempt metadata if needed; claim a
  batch in one short transaction, dispatch after commit, then conditionally
  acknowledge or record retry/dead-letter. Preserve stable event IDs,
  idempotency, ordering expectations, and crash recovery.
- Add safe request/query/outbox timing dimensions and release/node identity;
  never log tokens, payload bodies, or private support content.

### Wave F — Complete-Tree Integration and Release Qualification

- Re-inventory the entire workspace, fix every in-scope compile/test/review
  failure regardless of author, and reconcile migrations/model snapshot.
- Run code, test, documentation, accessibility, design-token, event-contract,
  migration, Docker, workflow load, static-header, and production orchestration
  suites. Record results per component and role.
- Exercise migration on empty and production-like snapshots; prove all schema
  changes are non-destructive and compatible with both current and candidate
  applications.
- Generate the final source digest. If any file changes, discard candidate
  eligibility and rerun this wave from inventory through artifacts.

### Wave G — Three-Node Zero-Downtime Production Rollout

- Use the reviewed production tooling and `ssh-server` operating contract for
  read-only preflight, backup/restore readiness, quorum, clocks, capacity,
  dependency leaders, current-release manifest, and node health.
- Build the four application images once from the sealed complete source,
  record digests, distribute and verify byte/digest parity on all nodes.
- Serialize the migration once. Never invoke an automatic down migration or
  database restore during application rollback.
- Drain, deploy, smoke, health-check, and undrain node-3, then node-2, then
  node-1. Advance only after the node rejoins healthy and the remaining nodes
  stay serving.
- On a critical application gate failure: stop and automatically restore the
  prior compatible application artifact on the failed node and every node that
  already advanced, in reverse advancement order; verify and undrain each;
  preserve the schema; issue a forward migration if schema remediation is
  required before any new attempt.
- After convergence verify release identity, traffic balance, authenticated
  workflows, cross-node SignalR, queues, shared files, dependency health,
  one-node application failure tolerance, and post-release RUM ingestion.

### Local no-download execution rule

- Do not run local `npm install`, `npm ci`, `dotnet restore`, Playwright browser
  install, Docker image pull/build that needs a missing base, package-manager
  update, or tool/SDK installer.
- Local checks may use only already present modules, SDK assets, caches, and
  images.
- A check requiring absent material is marked locally blocked and is executed
  by the reviewed remote builder/production environment against the exact
  sealed source; its evidence remains mandatory before deployment.

## Phase 1 Design Outputs

- [data-model.md](data-model.md) defines existing and extended metric,
  security-cache, outbox-claim, query-cache, performance-budget, manifest, and
  deployment-gate records and state transitions.
- [contracts/client-query-and-navigation.md](contracts/client-query-and-navigation.md)
  defines query keys, cancellation, freshness, invalidation, navigation, focus,
  and prefetch behavior.
- [contracts/performance-observability.md](contracts/performance-observability.md)
  defines privacy-safe RUM, server correlation, query-count, and budget evidence.
- [contracts/release-and-rollback.md](contracts/release-and-rollback.md) defines
  complete-tree sealing, artifact parity, node gates, and app-only rollback.
- [quickstart.md](quickstart.md) gives the required implementation and
  verification sequence.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `make verify` as the repository-wide contract, executed on the reviewed
  remote builder because its backend target performs `dotnet restore`.
- `dotnet test backend/NaderGorge.sln` plus focused auth cache/invalidation,
  live-support query-count/data-volume, outbox claim/crash/idempotency, migration
  compatibility, and cluster tests.
- `cd frontend && npm run lint && npm run typecheck && npm run build` plus
  platform-event, live-support, design-token, accessibility, and route-budget
  checks. The production build runs remotely because `next/font/google` may
  fetch font assets during a cold build; local lint/type/static checks use only
  existing dependencies.
- Focused Playwright journeys for login/deep-link, registration typing and
  reduced motion, student shell/history/packages, admin paginated search,
  role denial, drawers/carousels/focus, realtime reconnect, and production
  domain smoke.
- `cd worker && npm run build` and its Node test suite.
- Python production-tool tests, source-manifest delta tests, Compose validation,
  migration dry runs, authenticated workflow load, 300-request distribution
  sample, 30-minute 2× baseline run, realtime reconnect, and failure drills.

**Docker Gate Required**:

1. `docker compose config -q`.
2. Build the exact complete source with frontend/backend/worker/migrator images.
3. `make up`; apply `make migrate` for the isolated environment.
4. Verify PostgreSQL, Redis, backend `/api/health`, worker `/ui`, frontend,
   static headers, release identity, queues, SignalR, and shared files.
5. Repeat migrations against an empty database and a sanitized production-like
   schema; exercise old and new app compatibility.
6. No wave closes while an applicable service or external-dependency gate is
   silently skipped.

**Manual QA Required**:

- Visitor/student: landing, login, validated deep-link return, registration
  typing, theme logo, reduced motion, dashboard, packages/teachers, history and
  back/forward scroll.
- Admin/teacher/assistant/staff: persistent shell, active navigation,
  permission visibility and denied route, large search, tab/modal/drawer,
  loading/error/retry.
- Live support: queue, dashboard, conversation, history/timeline, reconnect,
  duplicate prevention, targeted refresh, AI recovery.
- Accessibility: keyboard-only, skip link, focus visible/contained/restored,
  320px, 200% zoom, long Arabic, light/dark, pause controls.
- Operations: per-node health/release identity, distribution, realtime across
  nodes, queue execution, shared-file read/write, drain/rejoin, and app rollback
  with unchanged compatible schema.

**End-of-Phase Report Format**: Each wave records exact source digest and dirty
inventory delta, implemented scope, commands and timestamps, pass/fail output,
Docker evidence, manual QA matrix, before/after measurements, remaining risks,
and explicit GO/NO-GO. Failed gates are repaired in the same wave. Production
starts only on GO; a post-seal delta automatically returns the state to NO-GO.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| None | The work extends existing projects and production orchestration | No new architectural layer or independent service is required |

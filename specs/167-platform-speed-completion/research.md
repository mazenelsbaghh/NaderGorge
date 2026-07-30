# Research Decisions: Platform Speed Completion

## 1. Persistent App Router shells

**Decision**: Own each authenticated surface shell from its stable App Router
layout and isolate browser-only behavior in small client components. Keep the
public navigation inside the public route group and remove global page-template
behavior that remounts protected page content.

**Rationale**: A layout survives child-route transitions, preserving navigation
state and avoiding repeated hydration. The current root carries public
navigation and transition work even where it ultimately renders nothing.

**Alternatives considered**:

- Persist shell state only in browser storage: restores values but still pays
  remount and bootstrap cost.
- Keep one global client shell: increases shared JavaScript and crosses role
  boundaries.
- Full document navigation: loses state, cache, focus, and warm-navigation
  performance.

## 2. Client query cache and realtime invalidation

**Decision**: Implement a single lightweight repository-owned query provider
above authenticated route groups using the existing `query-keys`,
`query-contracts`, cache-invalidation, and Axios groundwork. Define canonical
role-aware keys, bounded freshness, cancellation signals, retained previous
data, mutation invalidation, and SignalR event mappings without downloading a
new local dependency.

**Rationale**: The current repeated `useEffect` reads have no shared
deduplication or lifecycle, while the repository already contains much of the
required contract groundwork. A focused query cache supplies one in-flight
request per key, cancellation, retry control, and narrow invalidation without
replacing the service layer or violating the user's local no-download rule.

**Alternatives considered**:

- TanStack Query: mature and otherwise suitable, but it is not present locally
  and adopting it would require a prohibited device download.
- Zustand for server data: mixes local state with asynchronous authoritative
  state and makes stale/invalidating behavior implicit.
- Server Components only: valuable for initial render, but insufficient for
  authenticated client transitions, mutations, and realtime updates.

## 3. Selective navigation prefetch

**Decision**: Prefetch primary destinations at intent (`pointerenter`,
`focus`, touch intent) or when an eligible primary item becomes visible; keep
rare/heavy routes disabled. Couple data prefetch to the same canonical query
keys and cancel unused speculative work.

**Rationale**: This shortens click-to-content without returning to uncontrolled
background downloads.

**Alternatives considered**:

- Prefetch every link: wastes bandwidth and memory on large admin surfaces.
- Disable all prefetch: preserves transfer but makes every common transition
  cold.
- Time-based prefetch of the whole portal: unpredictable and not intent-aware.

## 4. Entry animation and WebGL budget

**Decision**: CSS is the default registration/entry background. Optional WebGL
loads after idle only when reduced motion, save-data, constrained hardware,
hidden document, and active typing checks all permit it. The render loop pauses
immediately when eligibility changes.

**Rationale**: Input is the primary task; a continuously scheduled high-DPI
renderer competes for main/GPU time and hurts constrained devices.

**Alternatives considered**:

- Reduce pixel ratio only: still maintains a continuous loop.
- Lazy import but render immediately: moves network timing but not runtime cost.
- Remove the visual everywhere: unnecessarily discards the richer experience
  on capable idle devices.

## 5. Image, logo, font, and static-cache behavior

**Decision**: Render one theme-correct brand asset, use responsive source
selection, avoid priority for viewport-hidden images, minimize loaded font
weights, and configure immutable long-lived caching only for content-addressed
or versioned assets. Verify effective origin and Cloudflare headers.

**Rationale**: The logo is small, so its observed LCP points to request/render
delay rather than bytes alone. Loading both theme variants and hidden priority
images creates avoidable competition.

**Alternatives considered**:

- Keep two images and hide one with CSS: both remain request candidates.
- Mark all hero media priority: competes with the actual mobile LCP.
- Cache every path immutably: risks stale mutable user/configuration assets.

## 6. Large-list request contract

**Decision**: Use stable server-side cursor or page-number pagination according
to existing endpoint semantics, with 25–50 default rows, deterministic sort,
250–350ms search debounce, `AbortSignal`, and response identity protection.

**Rationale**: Fetching 1,000 students to show eight rows increases response,
serialization, transfer, and memory costs; rapid input creates obsolete work.

**Alternatives considered**:

- Client virtualization over 1,000 fetched records: reduces DOM work only.
- Debounce without server pagination: request frequency improves but payload
  remains excessive.
- Search only on explicit submit: cheaper, but regresses current interactive
  behavior.

## 7. Live-support query bounding

**Decision**: Replace per-record lookups with EF Core projections and bounded
set queries, using `AsNoTracking`, grouped aggregates, batch user maps, and
explicit page limits. Count executed commands with an interceptor in integration
tests and enforce a fixed ceiling independent of displayed row count.

**Rationale**: A command-count contract catches N+1 regression more reliably
than elapsed time alone and remains meaningful across hardware.

**Alternatives considered**:

- Add cache around current N+1 behavior: masks but does not bound cold-path
  work.
- One enormous include graph: risks cartesian growth and excess materialization.
- Database stored procedures: unnecessary coupling before projected EF queries
  are proven insufficient.

## 8. Authentication security-state cache

**Decision**: Cache only `{userId,isActive,passwordResetVersion,
securityStampVersion}` for a short bounded TTL in shared Redis, with in-process
fallback only when it cannot weaken revocation. Every state-changing command
that disables/deletes a user or changes password/roles/permissions/security
version invalidates the key before reporting success. Cache outage falls back
to PostgreSQL.

**Rationale**: Token validation currently queries PostgreSQL for each eligible
request. Version claims already provide a compact comparison contract, while
explicit invalidation keeps revocation immediate.

**Alternatives considered**:

- Trust JWT until expiry: violates immediate revocation.
- Long TTL without invalidation: unsafe stale access.
- Cache full user/permission objects: broad sensitive data and more invalidation
  complexity.

## 9. Outbox claim, dispatch, and acknowledgement

**Decision**: Claim a bounded batch using `FOR UPDATE SKIP LOCKED`, record
owner/lease/attempt, and commit immediately. Dispatch outside the database
transaction. Acknowledge with a conditional owner/lease update; on failure,
record retry/dead-letter state. Expired claims are recoverable. Stable event IDs
support downstream/client idempotency.

**Rationale**: The current transaction stays open through network/queue/SignalR
delivery. A lease makes ownership durable without holding row locks across
external I/O.

**Alternatives considered**:

- Keep transaction and lower batch size: reduces but does not remove lock
  duration.
- Mark processed before dispatch: can lose events on crash.
- Delete events after dispatch: loses audit/recovery evidence.

## 10. Performance observability

**Decision**: Extend the existing Web Vitals pipeline with normalized route
template, surface, device class, effective connection class, navigation type,
release identity, and sample timestamp. Correlate server requests with safe IDs
and record route duration, EF command count/time, outbox latency, and node
identity. Never capture token, URL query secrets, request bodies, or support
content.

**Rationale**: Existing aggregate measurements cannot attribute slow journeys.
The platform already has a metric entity/controller, avoiding a parallel store.

**Alternatives considered**:

- Cloudflare aggregate metrics alone: lacks application route and server
  correlation.
- Full browser/session replay: expands privacy and operational scope.
- Raw URLs: risk identifiers and query-string secrets.

## 11. Performance acceptance timing

**Decision**: Block deployment on immediate synthetic, browser workflow,
resource, query-count, error-rate, and health gates. Continue RUM after
production and always publish sample count/segment coverage; no fixed duration
or count blocks rollout.

**Rationale**: This implements the user's decision while preventing
under-sampled RUM from being presented as statistically conclusive.

**Alternatives considered**:

- Wait seven days/1,000 samples per route: explicitly rejected by the user.
- Ignore RUM after deployment: would leave real-device regressions invisible.
- Treat a tiny sample as conclusive: misleading.

## 12. Complete moving workspace

**Decision**: Inventory every tracked and untracked path throughout
implementation. Seal the exact full source only immediately before artifact
build. Any later path/content change invalidates eligibility and forces
inventory, build, and full-gate repetition.

**Rationale**: The user requires every change to ship, including concurrent
changes. Reproducibility still requires artifacts to map to one exact digest.

**Alternatives considered**:

- Freeze the original dirty tree and exclude later changes: rejected by the
  user.
- Copy only feature-owned paths: violates release completeness.
- Mutate already built images in place: destroys provenance and node parity.

## 13. Migration and rollback policy

**Decision**: Use additive expand/contract-compatible migrations, verify against
empty and production-like schemas and both current/candidate applications, and
apply once under serialization. Automated rollback restores application images
only. The applied schema remains; schema faults use a forward-only corrective
migration before rollout resumes.

**Rationale**: Mixed versions coexist during rolling deploy. Automatic down
migrations can lose new writes and invalidate the still-running candidate/current
mix.

**Alternatives considered**:

- Automatic down migration: explicitly rejected and unsafe.
- Automatic PITR restore: rewinds valid concurrent production writes.
- No rollback: unnecessarily extends an application fault.

## 14. Rolling production order

**Decision**: Build once, distribute identical digests, then drain/deploy/
verify/undrain node-3, node-2, and node-1 sequentially. Keep two nodes serving,
stop before the next node on any critical gate, and verify convergence and
failure tolerance after rollout.

**Rationale**: This follows the established feature-166 cluster operating
contract and the user's zero-downtime instruction.

**Alternatives considered**:

- Parallel update: reduces capacity and removes safe progressive evidence.
- Rebuild per node: permits image drift.
- Database rollback coupled to node rollback: conflicts with forward-compatible
  schema policy.

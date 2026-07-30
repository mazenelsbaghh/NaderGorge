# Phase 7 / US5 — Verification and RUM status

Status: **NO-GO FOR RELEASE UNTIL REMOTE IMMEDIATE GATES PASS**

Date: 2026-07-29

## Locally completed

- Web Vitals schema, normalization, forbidden-data sentinel, validation, and
  dedicated rate-limit tests.
- Browser payload, correlation, request/EF timing, outbox telemetry, cache,
  route-budget, and performance-matrix contracts.
- API build, frontend typecheck/focused lint, Python compile/contracts, Node
  syntax, and whitespace checks.
- Accessibility browser matrix discovery without installing browser runtimes.

No package, browser, image, SDK, or external runtime was downloaded. Existing
local cached dependencies were used only for non-network checks.

## Remote immediate gates

The following gates block deployment and must run against the exact sealed
candidate:

1. Full frontend production build, compressed route-resource budgets, and
   browser baseline.
2. Chromium/WebKit accessibility, navigation, loading/error, and carousel
   matrix.
3. Full backend application/integration suite with PostgreSQL, including
   migration compatibility and outbox lease/crash/retry/dead-letter scenarios.
4. Worker tests and queue processing.
5. All six authenticated k6 journeys: login, student dashboard, packages,
   admin search, live support, and SignalR reconnect.
6. Exact-image Docker smoke, cache headers, release/node identity, health, and
   cross-node behavior.
7. API/error/query-count/navigation/resource budgets and secret-free evidence.

## RUM policy

Post-release RUM always records the observed sample count and segmentation by
route template, surface, device, connection, and release. There is no fixed RUM
sample count or elapsed duration that delays an otherwise qualified rollout.

- `sampleCount = 0` or a sparse segment is reported as
  **pending/observational**.
- A sufficiently populated segment may be described as **qualified** by the
  summary endpoint's descriptive flag.
- Neither state overrides a failed synthetic, workflow, resource, query,
  health, error-rate, or cluster gate.
- Aggregate Cloudflare values are not claimed as route-balanced evidence, and
  the secondary-results route remains excluded from prioritization.

T099 remains open until every remote immediate gate and exact-image Docker gate
passes and their immutable evidence paths are attached.

## Production addendum

Passed release-bound gates include the remote four-image build and
distribution, real PostgreSQL backup/restore/migration/N-1 compatibility,
rolling exact-image health, three-node status/audit, Cloudflare Tunnel status,
backup schedules, source/image parity, and a read-only browser smoke for
landing, carousel, login, and registration. The backend Application, worker,
and production/SSH Python runs reported 556 passed with one pre-existing skip,
75/75, and 426 passed with six environment-dependent skips. Those counts do
not yet have a release-bound `verification.json` and are not treated as final
acceptance evidence.

T099 remains open. No disposable load-test account or mode-0600 workflow and
WebSocket token files were available, so the six authenticated k6 journeys
were not run and no load success is claimed. RUM remains observational without
a fixed waiting window.

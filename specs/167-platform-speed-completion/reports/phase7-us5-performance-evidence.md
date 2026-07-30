# Phase 7 / US5 — Immediate performance evidence

Status: **LOCAL CONTRACT PASS — SEALED BEFORE/AFTER RUN PENDING**

Date: 2026-07-29

## Evidence policy

Only measurements from the same reviewed workflow, environment class, and
sealed source are eligible for a numeric before/after claim. Structural code
changes and local contract tests are evidence of implementation, not proof of a
production speedup. The attached Cloudflare report remains the RUM baseline,
with the secondary-results route excluded from prioritization.

## Baseline

| Area | Before evidence | Limitation |
|---|---|---|
| Cloudflare LCP | p50 1,572 ms; p75 2,380 ms; p90 3,668 ms; p99 14,828 ms | Aggregate 24-hour sample is dominated by the excluded secondary-results route |
| Cloudflare INP | 66% good, 25% needs improvement, 9% poor | Not route/device balanced |
| Cloudflare CLS | 98% good, 1% needs improvement, 0% poor | Aggregate only |
| Existing ingress load | 20 RPS for 30 minutes, 0% error, p95 13.33 ms, equal node share | No authenticated workflows, database-heavy flows, or websocket VUs |
| Existing route build | `/register` about 870 KB raw entry chunks; `/student` about 661 KB; `/login` about 531 KB | Existing unsealed `.next`; raw size, not compressed transfer |

Canonical baseline details:
`artifacts/performance-167/baseline/rum-baseline.json`.

## Implemented measurement and budget controls

- Browser RUM sends only normalized metric, route template, surface, device,
  connection, navigation, and immutable release dimensions. It sends no URL,
  query, user agent, token, name, phone, message, or content.
- RUM sampling is independent of authentication bootstrap and uses a dedicated
  public rate limit.
- Authorized summaries report sample count, p50/p75/p90/p99, good rate, and a
  descriptive `sampleQualified` flag.
- Request metrics expose route template, method, status/outcome, duration,
  serving node, release, and request-scoped EF command count/time.
- Outbox metrics expose claim, dispatch, retry, and dead-letter outcomes without
  payload content.
- `/_next/static` is immutable for one year. Mutable public assets use bounded
  one-day revalidation; private application routes are not publicly cached.
- The release budget gate now covers compressed initial/shared/deferred
  resources, duplicate requests, warm navigation p75, live-support command
  count, and six real workflow probes.

## Local evidence (no downloads)

| Gate | Result |
|---|---:|
| Web Vitals application contracts | PASS 7/7 |
| Browser reporter sandbox payload/privacy | PASS 1/1 |
| Correlation/request/outbox telemetry | PASS 4/4 |
| Cache contracts | PASS 3/3 |
| Route budget unit tests | PASS 4/4 |
| Production performance-budget contracts | PASS 15/15 |
| Backend API build `--no-restore` | PASS, 0 warnings/errors |
| Frontend TypeScript and focused ESLint | PASS |
| k6 workflow JavaScript syntax | PASS |

## Sealed after-measurement table

These cells intentionally remain pending until the reviewed remote builder runs
the exact baseline and candidate against sealed source/images:

| Area | Before | Candidate after | Decision |
|---|---:|---:|---|
| Landing/login/register/student compressed route resources | baseline artifact | PENDING | Blocking |
| Warm client navigation p75 | baseline artifact | PENDING | Blocking |
| Duplicate GET/request count | baseline artifact | PENDING | Blocking |
| Admin search API p50/p75/p90/p99 and cancellation | baseline artifact | PENDING | Blocking |
| Live-support query count for 1/20/100 rows | baseline artifact | PENDING | Blocking |
| Authenticated workflow error rate/latency | ingress-only evidence | PENDING | Blocking |
| SignalR reconnect and outbox delivery | not measured | PENDING | Blocking |
| Static/mutable/private cache headers on exact image | contract only | PENDING | Blocking |

T098 remains open until this table is populated by immutable artifacts from the
sealed candidate.

## Production addendum

Immediate exact-image evidence now proves three-node health, release/image
parity, cache configuration contracts, successful route-budget checks, and a
rendered public/auth browser smoke. Production RUM remains
pending/observational and must report its real sample count by release, route
template, surface, device, and connection. No fixed RUM duration or sample
count blocks the rollout.

T098 remains open because authenticated workflow percentiles and a comparable
sealed before/after browser dataset were not produced.

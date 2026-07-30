# Phase 9 Independent Architecture, Concurrency, and Security Review

## Scope and method

The initial review was a read-only review of the feature-167 query cache, authentication
security-state cache, outbox leasing, telemetry/Web Vitals, forward migrations,
and three-node rolling deployment/application-only rollback paths. The
dispositions below were added after the separate T125 remediation changed
production code and tests.

The review used the feature contracts as the acceptance boundary and inspected
the implementation and targeted tests. The existing frontend query/Web Vitals
contract tests passed under Node. During remediation, the already-present
repository virtual environment ran the production deployment suite: 418 tests
passed and 6 were skipped. No dependency was downloaded.

The `Evidence` line references in each finding identify the initial
pre-remediation working tree and can shift after the recorded fix. Each
`Disposition` describes the current implementation and the remaining remote
gate.

## P0

No P0 finding was identified.

## P1

### P1-1 — Parent JWTs bypass live revocation for their full one-year lifetime

**Evidence:** `backend/src/NaderGorge.API/Program.cs:225-231` returns from
`OnTokenValidated` for every `Parent` token with a `StudentId`, before the active
user, password-reset version, and security-stamp checks at lines 233-263.
`backend/src/NaderGorge.Infrastructure/Services/TokenService.cs:124-143` creates
that token without a user/security-version claim and expires it after one year.
`backend/src/NaderGorge.Application/Features/Parent/Commands/VerifyParentCodeCommand.cs:40-47`
also issues it for a matching student profile without requiring the linked user
to be active.

**Impact:** Suspending/deleting a student, rotating credentials, or changing the
student security stamp cannot revoke an already issued parent token. Parent
access to protected student information can continue for up to one year.

**Required disposition:** Bind parent tokens to a revocable security subject
and validate active/version state on every authenticated request (or use a
separately revocable parent-session record). Add an acceptance test proving
immediate revocation.

**Disposition:** Resolved in the T125 implementation. Parent JWTs now carry the
linked student user's subject and password/security versions, use the shared
live security-state validation path, and cannot be issued for an inactive or
deleted user. Focused token/handler regression tests pass; end-to-end revocation
against the remote PostgreSQL/Redis environment remains part of the remote gate.

### P1-2 — Claimed outbox batches can be dispatched twice after the fixed lease expires

**Evidence:** `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs:23`
sets one two-minute lease. Lines 59-80 claim as many as 50 events and dispatch
them sequentially; lines 139-145 perform the single batch claim.
`backend/src/NaderGorge.Infrastructure/Background/OutboxLeaseStore.cs:19-107`
offers claim, acknowledge, and failure operations but no lease renewal.

**Impact:** If earlier dispatches or a slow external destination consume the
lease window, a second node can claim later rows while the first node is still
dispatching them. The first acknowledgement is then rejected, but the external
SignalR/queue side effect has already happened. This permits concurrent
duplicate delivery and ordering inversions across the three nodes.

**Required disposition:** Renew ownership during long batches, claim smaller
work units, or atomically fence each dispatch. Prove the behavior with a
multi-worker test whose destination delay exceeds the lease.

**Disposition:** Resolved in the T125 implementation. The processor claims one
event at a time and renews its lease every third of the lease interval while a
dispatch is active. Lost ownership cancels the dispatch token and prevents
acknowledgement. PostgreSQL acceptance coverage now includes a destination-delay
window longer than the original lease; executing that test remains a remote
PostgreSQL gate.

### P1-3 — Production migrations take write-blocking locks on live, high-write tables

**Evidence:** `backend/src/NaderGorge.Infrastructure/Migrations/20260729193000_AddOutboxClaims.cs:15-26`
uses ordinary `ALTER TABLE` and `CREATE INDEX` on `outbox_events`.
`backend/src/NaderGorge.Infrastructure/Migrations/20260729151000_RepairVideoTypeCodeGrantSchema.cs:25-44`
drops/re-adds a validated check constraint and builds ordinary indexes on live
grant tables; lines 46-61 may drop and rebuild another index.
`backend/src/NaderGorge.Infrastructure/Migrations/20260729220000_AddWebVitalsDimensions.cs:71-82`
also builds a wide ordinary index. The production migrator applies these before
the rolling application update in
`deploy/production/scripts/migrate_release.py:153-171`.

**Impact:** PostgreSQL ordinary index builds block writes, and validated
constraint creation scans/locks the target table. Outbox writes participate in
normal application transactions, so a sufficiently large production table can
stall user workflows or exceed request timeouts before any node is rolled. This
violates the no-interruption rolling-release objective.

**Required disposition:** Split online DDL from transactional EF migrations,
use concurrent index creation and `NOT VALID`/later validation where applicable,
set bounded lock timeouts, and capture production-like lock-duration evidence.

**Disposition:** Resolved in code in the T125 implementation. The three cited
migrations now bound lock acquisition, execute index drop/build operations
outside the EF transaction with `CONCURRENTLY`, and add the grant-shape check as
`NOT VALID` before explicit validation. Additive defaults retain N-1 writes and
application rollback retains the forward schema. Static migration contract tests
pass; production-like lock-duration evidence remains a remote PostgreSQL gate.

### P1-4 — Web Vitals are attributed to `unknown`, not the immutable release

**Evidence:** `frontend/src/hooks/useWebVitalsReporter.ts:183-185` reads
`NEXT_PUBLIC_RELEASE_ID`, which Next.js must inline at build time.
`frontend/Dockerfile:18-31` declares and exports other public build arguments but
does not declare `NEXT_PUBLIC_RELEASE_ID`. A repository search finds no
production build assignment for it. Runtime compose sets the release only for
backend/worker/gateway
(`deploy/production/compose/compose.app.yml:39-65,115-121`), not as a frontend
build input.

**Impact:** Production browser samples fall back to `unknown`, so release-level
regression comparisons and post-rollout attribution are not trustworthy. The
Web Vitals feature can appear operational while failing its central release
correlation purpose.

**Required disposition:** Seal the immutable release ID into the frontend image
at build time and assert the emitted browser payload carries the deployed
release ID in production smoke.

**Disposition:** FIXED. `frontend/Dockerfile`, both local/remote release-image
builders, and a remote-builder contract test now pass the immutable
`NEXT_PUBLIC_RELEASE_ID` build argument. Focused production tests pass 54/54;
exact-image browser smoke remains a remote release gate.

### P1-5 — A recovery-marker cleanup failure creates a non-resumable rollout that can produce a mixed cluster

**Evidence:** `deploy/production/scripts/deploy_release.py:1005-1016` sets
`rollout_complete = True` before clearing per-node recovery markers. If marker
cleanup fails, the outer handler at lines 1017-1047 deliberately skips rollback
because rollout is marked complete. On a retry, lines 917-929 classify that
marker as resumable, but the node deploy script still requires that the marker
not exist at lines 709-714. The retry therefore enters failed-node recovery at
lines 951-990 and restores only that node to its previous application release,
while the other nodes remain on the candidate.

**Impact:** A transient cleanup error followed by the documented retry path can
leave one node on N-1 and two nodes on N while reporting rollout failure. That
breaks the one-release cluster invariant and makes traffic behavior dependent
on which node serves the request.

**Disposition: FIXED.** `deploy/production/scripts/deploy_release.py:383-393`
detects that all three nodes already run the immutable candidate, while lines
929-942 require the normal undrained quorum before treating the invocation as a
completed-rollout retry. Marker deletion is missing-file idempotent and still
rejects symlinks/invalid content at lines 647-671; cleanup then converges over
all three nodes at lines 1043-1054 without redeploying or restoring N-1.
`deploy/production/tests/test_deploy_release.py:271-289` injects cleanup failure
on the first and second marker and proves a same-release retry removes the
remaining markers without another deployment. The complete deployment test
directory passes: 422 passed, 6 skipped.

## P2

### P2-1 — Realtime invalidation can be lost behind an older in-flight response

**Evidence:** `frontend/src/lib/query-client.ts:102-113` unconditionally commits
an in-flight result with a fresh `updatedAt`. Lines 162-167 implement
invalidation only by setting `updatedAt` to zero. If invalidation arrives while
the request is running, the hook attempts another fetch, but line 83 returns the
same old promise; its eventual result overwrites the invalidation. The current
test (`frontend/src/lib/query-contracts.test.mts:35-75`) checks deduplication and
invalidation separately, not this ordering.

**Impact:** A mutation/realtime event can be followed by stale pre-event data
that is considered fresh for the full configured stale time (30 seconds on the
student dashboard at
`frontend/src/app/student/StudentDashboardClient.tsx:36-46`).

**Required disposition:** Track an invalidation generation/request epoch or
cancel and replace the active fetch. Add the exact
fetch-start → invalidate → old-fetch-resolve race test.

**Disposition:** FIXED. Each query entry now owns an invalidation generation.
A pre-event response cannot commit after the generation advances, and the hook
retries after the stale in-flight request ends. The exact race contract passes.

### P2-2 — Protected query state is not cleared at logout or identity/role transition

**Evidence:** The application uses the module singleton at
`frontend/src/lib/query-client.ts:215` through
`frontend/src/components/providers/QueryProvider.tsx:24-28`.
`frontend/src/stores/auth-store.ts:81-106` replaces/clears authentication without
calling `removeQueries`; the production search has no such call outside the
query client itself. Several canonical protected keys also omit an identity
boundary (`frontend/src/lib/query-keys.ts:13-64`). This contradicts the explicit
contract at
`specs/167-platform-speed-completion/contracts/client-query-and-navigation.md:39-42,105`.

**Impact:** Protected snapshots survive logout in browser memory. Current
student reads use a user boundary, which limits immediate cross-student
exposure, but same-identity role changes and present/future admin/support/HR
consumers can render data acquired under the previous authorization context.

**Required disposition:** Clear/cancel all protected queries synchronously on
logout and before an identity or role boundary is made renderable; include the
identity/authorization boundary in every private key. Add a real auth-store
transition test rather than calling `removeQueries` directly in the test.

**Disposition:** FIXED for the current cache consumers. `setAuth`, logout,
storage bootstrap, cookie refresh, and authorization-version/role/permission
refresh now synchronously remove query state before the new boundary renders.
The runtime query contract and auth-store wiring gate both pass. Exact
logout/user-switch behavior remains in the remote browser matrix.

### P2-3 — Outbox ownership compares database leases against application-node clocks

**Evidence:** Claim time comes from PostgreSQL at
`backend/src/NaderGorge.Infrastructure/Background/OutboxLeaseStore.cs:43-44,109-112`,
but acknowledgement and failure ownership compare `LeaseExpiresAt` with
application `DateTime.UtcNow` at lines 63-79 and 88-105.

**Impact:** Clock skew across three application nodes can reject a valid owner
early or extend its perceived ownership relative to the database clock. This
adds avoidable acknowledgement/retry anomalies to the lease-expiry duplicate
window.

**Required disposition:** Use one database-clock domain for claim, renew,
acknowledge, and failure fencing, and test with deliberately skewed application
clocks.

**Disposition:** Resolved in the T125 implementation. Claim, renewal,
acknowledgement, and failure ownership predicates now use PostgreSQL `NOW()`;
application timestamps no longer decide lease ownership. Tests cover expired
owners being unable to renew or record a late failure, with real execution
pending the remote PostgreSQL gate.

### P2-4 — A failure after drain but before deployment can leave a node drained indefinitely

**Evidence:** `deploy/production/scripts/deploy_release.py:931-944` drains the
target and then runs the drained-quorum assertion before the inner deployment
`try` starts at line 945. If that assertion fails, `advanced_nodes` is still
empty, so the outer recovery condition at lines 1017-1034 does not undrain or
recover the target.

**Impact:** A transient post-drain verification failure stops the rollout with
only two nodes serving and no automated restoration of the untouched node.
Repeated events reduce operational headroom and violate the expected
fail-safe state transition.

**Disposition: FIXED.** The rollout tracks an unchanged drained target
independently at
`deploy/production/scripts/deploy_release.py:927,965-986`. Every exceptional
exit restores and re-verifies that target before returning the failure at lines
1086-1109; nodes with a recovery marker remain on the existing application
rollback path and are not unsafely undrained. The fault-injection regression at
`deploy/production/tests/test_deploy_release.py:292-306` proves a failed
post-drain/pre-deploy quorum gate performs `drain → undrain`, invokes no deploy,
and leaves every node UP.

## P3

### P3-1 — Telemetry sanitization is syntactic, not a cardinality bound

**Evidence:** `backend/src/NaderGorge.Application/Features/Realtime/Services/RealtimeTelemetry.cs:38-45,80-104`
accepts every syntactically safe event type, node, and release string up to 64
characters. There is no event-type allowlist or bounded mapping even though the
values become metric tags.

**Impact:** New or malformed outbox event names can create unbounded time-series
cardinality and observability cost/noise.

**Required disposition:** Map event types to a finite taxonomy (with `other`)
and keep exact event names only in structured logs.

**Disposition:** Resolved in the T125 implementation. Metric `event_type` is
now selected from a finite allowlist and every unrecognized value maps to
`other`; structured dispatch logs retain the exact event name. The bounded
taxonomy regression test is included.

## Positive controls observed

- Query functions use abort signals, deduplicate identical in-flight reads, and
  normalize parameter ordering.
- Web Vitals payload fields and route templates are allowlisted/bounded, and
  summary reads are permission protected.
- Outbox claims use `FOR UPDATE SKIP LOCKED`, worker-specific ownership, and
  conditional acknowledgements.
- Cluster scheduler leases heartbeat and fence operations.
- Rollback code explicitly retains the migrated schema and contains no automatic
  database `Down`/restore path.

## Decision

All P1/P2 findings from this static review now have a resolved or `FIXED`
disposition with local regression coverage. This clears the architecture-review
finding gate only. The release remains **NO-GO** until the explicitly pending
remote PostgreSQL, exact-image browser, build, load, migration, and
three-node acceptance gates pass against one sealed candidate.

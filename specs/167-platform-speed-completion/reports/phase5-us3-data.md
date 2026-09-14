# Phase 5 — Data-Heavy Workflow Evidence

Date: 2026-07-29  
Status: **PARTIAL LOCAL PASS / DATABASE AND THREE-NODE RUNTIME GATES PENDING**

## Verified implementation

- Datastore command metrics expose operation, success, and duration without
  recording SQL text, parameters, user data, or other high-cardinality values.
- Outbox workers claim rows in a short PostgreSQL transaction with
  `FOR UPDATE SKIP LOCKED`, dispatch outside that transaction, and acknowledge
  or record failure only while the same worker still owns an active lease.
- Retry and dead-letter transitions clear claim ownership. Expired leases are
  eligible for reclamation, while `NextAttemptAt` prevents premature retries.
- Realtime event IDs remain stable across outbox retries. The recovery path now
  places the same event ID in the durable event row and its outbox payload.
- Frontend realtime invalidation deduplicates by stable event ID and invalidates
  only the query families mapped to the received scope.
- Live-support dashboard, history, timeline, names, ratings, and AI summary
  mappings use bounded reads and fixed-query batch projections rather than
  row-by-row database lookups.

## Local no-download verification

| Gate | Result |
|---|---|
| Backend API build with `--no-restore` | PASS — 0 warnings, 0 errors |
| Datastore metrics integration contract | PASS — 1/1 |
| Stable outbox/realtime identity contracts | PASS — 5/5 |
| Frontend realtime invalidation contracts | PASS — 10/10 |
| Focused frontend ESLint | PASS |
| PostgreSQL-backed outbox lease scenarios | PENDING — no reachable local PostgreSQL |

The PostgreSQL-backed test source covers two-worker exclusivity, lease expiry
and reclaim, crash-before-ack recovery, scheduled retry eligibility, and
dead-letter exclusion. Those scenarios compile, but they are not counted as
passing evidence: the configured local endpoint at `127.0.0.1:5436` refused
connections and no alternate database connection was supplied.

No package, SDK, browser, container image, or dependency was downloaded. No
restore operation was run.

## Open implementation items

- T056 remains open until all PostgreSQL-backed outbox lease tests pass against
  the reviewed database runtime.
- T070 remains open until the complete Phase 5 verification matrix below
  passes.

## Required database and three-node gates

1. Run all outbox lease tests against PostgreSQL and retain the test result
   artifact.
2. Run the old/new application migration compatibility suite against both an
   empty database and a production-like snapshot.
3. Exercise concurrent outbox processors on separate application nodes,
   terminate the current lease owner before acknowledgement, and verify that
   another node reclaims exactly after lease expiry without duplicate external
   effects.
4. Verify retry scheduling, dead-letter exclusion, and conditional
   acknowledgement after ownership transfer.
5. Run the focused backend and frontend suites, Docker entry smoke, and
   SignalR cross-node delivery/reconnect smoke from the sealed candidate.
6. Confirm immediate authorization revocation during Redis hit, miss, and
   outage paths.
7. Manually inspect representative 1-, 20-, and 100-row live-support
   dashboard/history responses for bounded query count, payload size, and
   correct pagination.

A source change after candidate sealing invalidates this evidence and requires
a fresh verification run. Automatic application rollback must retain the
forward-compatible database schema, as required by the release policy.

## Production addendum

The release migration gate completed a fresh encrypted backup, isolated
restore, target migration with real-data validation, and N-1 backend readiness.
The production migration then succeeded once under the serialized cluster
command. All rolling deploy health checks verified the backend, worker
readiness, shared-file write/delete probe, and seven routed surfaces on each
node. The schema was retained during the exercised application rollback.

T056 and T070 remain open because the complete two-node outbox failure matrix
and an authenticated cross-node SignalR delivery journey were not executed as
release-bound runtime evidence.

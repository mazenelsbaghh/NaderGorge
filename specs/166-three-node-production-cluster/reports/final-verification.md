# Final Verification

## Local automated checks

| Check | Result |
|---|---|
| Production operations and SSH skill pytest | 397 passed, 6 externally gated skipped |
| Isolated remote T053 coordination integration | 6 passed after 129 migrations |
| Real PostgreSQL database/Admin/migration-lock integration | 10 passed |
| Backend application tests | 540 passed, 1 skipped |
| Backend PostgreSQL/Redis integration tests | 35 passed |
| Worker build and tests | 73 passed |
| Frontend ESLint | passed |
| Frontend TypeScript check | passed |
| Frontend production build | passed, 118 routes collected |
| Production-domain Playwright discovery | passed, 10 gated tests |
| Repository `make verify` | passed |
| Root Docker Compose config | passed |
| Production Compose config with immutable digest placeholders | passed with service env resolution intentionally disabled locally |
| Python compile, tracked and rendered migrate/deploy shell syntax, Git whitespace | passed |
| Clean PostgreSQL 16 migration through latest model | passed |
| Database schema/data audit | zero critical findings |

The database audit covered 203 relations, 2,245 columns, 713 indexes, 634
constraints, 203 primary keys and 361 foreign keys. It found zero invalid
indexes, unvalidated constraints, tables without primary keys, duplicate index
definitions, ownership mismatches, orphan foreign-key rows, duplicate
constrained keys or forbidden bootstrap rows.

## Live cluster checks

- A fresh strict-SSH `clusterctl status` passed on all three nodes.
- Chrony, Docker, etcd, Patroni, HAProxy, Redis server, Redis Sentinel and
  Gluster are active on every node; the shared mount is active.
- Every node has a healthy backend container and routed API/landing readiness.
- HAProxy configuration checksum is identical on all three nodes.
- Prior 300-request and per-ingress distribution evidence reached all three
  nodes equally; wrong Host returned HTTP 421.
- The running release is
  `src-bdf19804cf29d19634b131a16d3e519d26f0d425` on all three nodes.
- Production `docker compose config -q` passed on all three nodes for that
  release.
- An earlier 30-minute 20 RPS run completed 36,000 public HTTP requests with zero
  errors, zero drops, p95 13.33 ms and exact 12,000/12,000/12,000
  distribution. Its capacity artifact failed only the CPU-steal gate, peaking
  at 15.32% on node-2. The release-bound final run below supersedes this
  capacity result.
- PostgreSQL and Redis acknowledged-write failover drills remain green.
- Garage reports three healthy members with replication factor 3; the bucket
  and localhost TLS endpoint are healthy on all three nodes.
- Encrypted Restic backup and cross-node isolated file restore are green.
- pgBackRest stanza/full backup/WAL archive are green, and an isolated PITR to
  a fresh target within five minutes passed migration, role, probe and index
  checks.
- All six database/file backup, retention and restore timers are enabled,
  active and bound to the primary/cluster-safe services on all three nodes.
- The initial Production Admin was created atomically through the protected
  bootstrap using a compliant owner-supplied password. The password was never
  placed in argv, SQL, evidence or a tracked file. Login returned the `Admin`
  role, and the same token authorized a protected Admin endpoint directly on
  node-1, node-2 and node-3. Remote build staging was removed afterward.
- A bounded `node-3` gateway readiness failure preserved 60/60 requests with
  zero errors through `node-1` and `node-2` (30/30), then recovered `node-3`
  healthy and `UP` on all three ingresses. Final strict status and audit passed.

## 2026-07-29 final release verification

- Remote immutable build, migration backup/isolated-restore gate, migration and
  rolling deployment all passed for
  `src-bdf19804cf29d19634b131a16d3e519d26f0d425`.
- Final strict status, audit, database archive status, file backup and all
  backup-schedule status checks passed.
- Current operations/SSH suite: 397 passed, 6 infrastructure-gated skipped.
- Focused frontend ESLint and full TypeScript check passed. The remote
  Production image build completed the frontend, backend and worker builds.
- Browser QA passed for public, Student, Admin, Teacher and Staff surfaces.
- 30-minute 2× baseline: 3,600/3,600 requests passed, zero errors/drops,
  p95 20.12 ms, WebSocket hold success 100%, and all three nodes observed.
- Resource capacity status is failed only for provider CPU steal (maximum
  24.04% versus 5%). PostgreSQL, Redis, queues, HTTP and WebSocket gates passed.
- Connector-loss and worker-loss drills passed and recovered. The final
  three-node status/audit and three-connector readiness checks passed.

## Owner-accepted external exception

Only the provider CPU-steal capacity gate is red. Its original signed
acceptance result remains `NO-GO` with exactly one reason:
`failed:load.json`, preserving the measured evidence. The platform owner
explicitly accepted this metric as a non-blocking risk on 2026-07-29, so the
operational conclusion is `GO WITH OWNER WAIVER`. Phase 9 and T113 were closed
after recording that exception, and `validate_run.py` passed.

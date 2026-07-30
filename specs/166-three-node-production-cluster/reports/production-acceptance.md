# Production Acceptance

## Decision

**GO WITH OWNER WAIVER — تم قبول مؤشر CPU steal الخارجي كاستثناء موثّق.**

The immutable release
`src-bdf19804cf29d19634b131a16d3e519d26f0d425` is running and healthy on
all three nodes. The original automated signed decision is stored at
`artifacts/production/acceptance-20260729/decision.json`; it remains
cryptographically unchanged as `NO-GO` with the single reason
`failed:load.json`. On 2026-07-29 the platform owner explicitly accepted that
provider-controlled metric as a non-blocking risk and authorized closing
Production acceptance. This management waiver does not rewrite or conceal the
original measurement.

## Green evidence

- all three nodes run the full application stack and strict live status passes;
- 300-request distribution was exactly 100/100/100;
- one gateway loss continued through the other two nodes;
- PostgreSQL has one writer/two replicas and preserved an acknowledged write
  through a 9-second failover;
- Redis Sentinel failover completed in 9 seconds;
- Gluster is connected with zero heal or split-brain backlog;
- internal application/data ports are closed publicly;
- migration-from-zero through 129 migrations and the detailed audit have zero
  critical findings and no fixed users;
- local backend, worker, frontend, operations, Compose and real PostgreSQL/Redis
  suites pass.
- internal Garage has three healthy members and replication factor 3;
- encrypted Restic backup and isolated cross-node file restore passed;
- pgBackRest stanza, WAL archiving and the first encrypted full backup passed.
- isolated five-minute PostgreSQL PITR passed with probe, schema, role and
  index verification;
- all six backup/retention/restore timers are enabled and active on all three
  nodes, with primary/cluster locks preventing duplicate work.
- the immutable rollback/forward-redeploy and bounded `node-3` readiness
  failure both passed; 60/60 requests continued through the two healthy nodes
  and `node-3` recovered to `UP` on every ingress.
- disposable Student, Teacher and Staff accounts passed API and real-browser
  login/navigation, wrong-permission denials and were deleted with all related
  profiles, devices, roles and tokens;
- a real upload was read byte-identically through all three storage nodes and
  deleted;
- authenticated SignalR reconnect survived a backend loss, and the final
  30-minute run held ten WebSockets with a 100% success rate;
- all eight Cloudflare routes are live, every connector has four HA
  connections, and a one-connector loss preserved 30/30 requests before full
  recovery;
- a one-worker loss preserved both remaining workers and 30/30 API requests,
  then recovered in 16.29 seconds;
- current operations and SSH tests pass: 397 passed, 6 externally gated
  skipped.

## Accepted exception

The application portion passed: 3,600 requests, zero errors, zero drops,
p95 20.12 ms, 100% WebSocket hold success and request distribution of
1,197/1,191/1,212. CPU steal exceeded the 5% contract threshold and remains
visible in the evidence. The owner accepted this single infrastructure risk;
provider remediation and a future rerun are recommended but no longer block
the cluster plan or T113.

Cloudflare routing is complete and healthy. Operational readiness is therefore
`GO WITH OWNER WAIVER`; the machine-generated signed decision remains retained
as the immutable strict-gate result.

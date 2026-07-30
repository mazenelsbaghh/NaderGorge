# US3 Coordination Evidence

- Redis topology: one master, two replicas and three Sentinels
- Sentinel quorum: 2
- Failover drill: master moved from node-1 to node-3 in 9 seconds
- Durability probe: preserved through failover
- Rejoin: former master returned as a replica
- Client configuration: backend cache, SignalR and worker/BullMQ use Sentinel
  discovery instead of a fixed Redis host
- All three Sentinels agree on the current master
- A PostgreSQL `cluster_leases` table now records owner token, fencing
  generation, expiry, renewal and last outcome.
- Recharge expiry, HR escalation, live-support recovery, AI recovery and the
  worker nightly sweep now claim a database lease before running.
- Worker completion updates include the claimed fencing generation, so a stale
  owner cannot overwrite the outcome of a newer lease.
- Outbox-to-BullMQ dispatch carries the durable outbox ID into the stable job
  ID; retry and stream recovery tests remain green.
- Real PostgreSQL lease tests passed for single claimant, expired takeover,
  generation increment and stale-owner renewal refusal.

The code above is deployed in the currently running immutable release
`src-bdf19804cf29d19634b131a16d3e519d26f0d425`.

Live account-backed SignalR is now green. A disposable Student account opened
the real `/hubs/platform` path through the Production `ws` hostname. During the
probe one application backend was stopped after the first HTTP 101 upgrade.
The client reconnected through another node with another HTTP 101 upgrade and
a valid SignalR handshake. Both checks passed, and the stopped backend was
restored before the final three-node status check. Evidence:
`artifacts/production/signalr-live-20260729/reconnect-proven-complete-20260729T183045Z.log`.

The HAProxy SignalR backend is intentionally stateless and no longer uses the
old node cookie: every browser client uses WebSockets with negotiation skipped,
while the Redis backplane carries application events between backend nodes.
This removes the stale-cookie failure that previously routed a reconnect to a
dead backend.

The Redis acknowledged-write failover checkpoint, durable outbox job-ID replay,
PostgreSQL lease fencing, and triple-scheduler single-owner integration coverage
are green. The temporary SignalR account and all remote probe directories from
that run were deleted.

The missing T053 source coverage has now been added at
`backend/tests/NaderGorge.Integration.Tests/ClusterCoordinationTests.cs`:

- two real Kestrel/SignalR nodes share the Sentinel-derived Redis backplane,
  and a client connected to the first must receive a group message published
  through the second;
- replaying one real PostgreSQL outbox row twice through the real Redis
  enqueuer must preserve the exact outbox-derived external `jobId`, which is
  the consumer deduplication boundary.

The tests use real PostgreSQL, Redis and Kestrel boundaries and contain no
internal mocks. Test Guard found no blocking rule violation. A non-local
Remote Verifier on node-1 created disposable PostgreSQL 16, one Redis master
and three Sentinels, applied all 129 migrations, restored and built the test
project, and passed all 6/6 `ClusterCoordinationTests`. It then removed every
test container, network and source-staging path. No Production secret or data
service was supplied to the verifier. T053 is complete.

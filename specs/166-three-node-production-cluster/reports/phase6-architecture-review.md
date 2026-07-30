# Architecture, Security and Operations Review

## P0

No active split brain, public data-service exposure, fixed production bootstrap
account or critical schema finding was observed.

## P1

1. **Cloudflare is not connected.** The exact locally managed tunnel
   configuration exists, but no mode-0600 tunnel credentials JSON has been
   supplied and no protected rehearsal has run.
2. **Authenticated coordination acceptance is incomplete.** PostgreSQL leases,
   fenced worker outcomes, stable outbox job IDs and the shared storage
   abstraction are deployed in the current immutable release, but cross-node
   SignalR, queue replay and triple-scheduler evidence still require accounts.

The internal repository, encrypted database/file backups, isolated database
PITR, cross-node file restore and all six schedules are now live. The bounded
brick isolation/heal rehearsal and the compatibility-bound rollback,
forward-redeploy and injected-readiness-failure recovery are also green.

## P2

1. A final parity check found that node-2 and node-3 still had the
   PostgreSQL-only HAProxy file. The combined database/application configuration
   was validated and gracefully reloaded on both. All three files now have the
   same checksum, and each local ingress distributes 20/20/20 over 60 requests.
   Config checksum drift should become a monitored acceptance gate.
2. The 30-minute load scenario and complete eight-domain browser/WebSocket/
   upload suite remain pending until the tunnel rehearsal.
3. The acceptance suite still needs live cross-node SignalR, queue replay,
   scheduler ownership and file quorum-loss evidence after the new release.

## P3

- `clusterctl` now dispatches build, bootstrap foundation, status, drain,
  migrate, deploy, rollback, failover, backup, restore, Admin bootstrap,
  Cloudflare status and acceptance. Dry-runs with missing mandatory arguments
  are blocked instead of reporting a false planned success.
- Health checks deliberately convert dependency exceptions into an unhealthy
  response. This broad boundary handling is acceptable only because it fails
  readiness and never reports success.

## Decision

The cluster foundation is sound enough to continue controlled commissioning,
but the P1 items keep the production acceptance result at `NO-GO`.

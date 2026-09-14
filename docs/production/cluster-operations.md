# Massar production cluster operations

The application runs on all three nodes. Every local HAProxy balances the eight
approved hosts across all three node gateways. PostgreSQL is one Patroni
cluster with one writer and two replicas; Redis is one Sentinel-discovered
master with two replicas; Gluster is `replica 3 arbiter 1` with full data on
node-1/node-2 and arbitration on node-3.

Use only `deploy/production/scripts/clusterctl.py` with the tracked production
inventory. Routine SSH is key-only as `massar-ops`, with pinned host keys.
State changes require a dry run followed by `--yes`. Never act on two quorum
members at once.

The rolling order is node-3, node-2, node-1. Drain one node from every ingress,
deploy exact image digests, wait for local readiness and smoke checks, then
undrain it before touching the next node. Database migrations run once through
the dedicated advisory-locked migrator. Rollback changes application images
only after a current-schema compatibility check; it never runs a down migration.

Health evidence is collected every minute. Investigate any loss of exactly one
PostgreSQL writer, one Redis master, etcd quorum, Gluster quorum, zero
split-brain entries, identical release digests, three healthy Garage members,
or fresh encrypted backups.

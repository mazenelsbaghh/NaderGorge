# Cluster Operations Contract

All commands are non-interactive by default, use the secret-free inventory, pin
SSH host keys, emit JSON evidence, and fail closed. The implementation may use
shell entry points under `deploy/production/scripts`; the operator-facing names
and semantics below are stable.

## Common flags

```text
--inventory PATH       required production inventory
--node node-1|node-2|node-3|all
--release RELEASE_ID
--evidence-dir PATH    must be outside secret directories
--dry-run
--yes                  required for a reviewed state-changing action
```

Exit codes: `0` success, `2` validation/usage, `3` preflight blocked, `4`
partial node failure, `5` safety/quorum refusal, `6` verification failure.
Secret values must never appear in stdout, stderr, JSON, process arguments, or
command history.

## `cluster audit`

Read-only. Reports host identity, OS/resources, time sync, Docker/runtime,
WireGuard reachability, firewall exposure, disk/inodes, current release, roles,
quorum, backup age and secret-reference presence.

Success requires all three exact approved nodes and no duplicate hostname or
overlay address.

## `cluster bootstrap`

Installs/configures the non-root operator, key-only SSH, pinned host identities,
WireGuard, firewall, Docker, system services and production directories.

Safety:

- refuses unknown host keys unless `enroll-host-key` was separately reviewed;
- never disables the current bootstrap access until key/sudo rescue checks pass;
- supports repeat execution and reports `changed` versus `unchanged`;
- never initializes a second production cluster over existing data.

## `cluster status`

Read-only JSON plus human summary. Includes all node/app versions, HAProxy
backends, Patroni/etcd roles, Redis/Sentinel roles, Gluster heal/quorum, latest
backup/WAL age, and active alerts.

## `cluster build`

Builds each image once, records OCI digests, exports archives and verifies the
same digests after import on all nodes. No state rollout occurs.

## `cluster migrate`

Requires a successful clean-database audit. Obtains a PostgreSQL advisory lock,
prints only migration IDs, applies once to the current Patroni writer, verifies
the target and releases the lock. Concurrent invocation fails or waits within a
bounded timeout; it never runs from normal backend startup.

## `cluster deploy`

Preflight -> build/import parity -> backup gate -> migrate -> node-3 -> node-2
-> node-1. Each node is drained, replaced, checked and undrained before the next.
First failure stops the sequence and emits the failed node/reason.

## `cluster drain`

Marks one node not ready and waits until active HTTP/WebSocket work reaches the
configured threshold. It must not stop data quorum members merely because the
application is drained.

## `cluster failover-test`

Requires one service and one target:

```text
--service ingress|app|postgres|redis|files
--target node-N
--restore
```

It captures pre-state, injects one bounded failure, measures RTO/data integrity,
restores the component, waits for replication/heal, and captures post-state.
It refuses a second simultaneous data-member failure.

## `cluster backup`

`--kind db-full|files-hourly|all`. Validates internal three-node repository encryption,
runs the appropriate backup, verifies manifest/checksum, and updates evidence.
It does not treat a Patroni replica, live Gluster copy, or Hostinger snapshot as
the backup repository.

## `cluster restore-test`

Creates an isolated namespace, restores a selected backup/PITR time, runs
migration/integrity/login/file smoke, records results, and cleans only that
namespace. It cannot target the production database or mounted production
volume.

## `cluster rollback`

Accepts an existing immutable release ID, drains and rolls application images
back one node at a time. It performs no automatic down-migration. If the prior
application is incompatible with the current schema, rollback is blocked and a
forward fix/isolated restore procedure is required.

## `cluster bootstrap-admin`

Reads phone/password from a no-echo protected input channel, generates an
application-compatible BCrypt hash, and executes one parameterized SQL
transaction against the current DB writer:

1. assert no existing conflicting user;
2. insert the user with approved normalized identity fields;
3. select the existing Admin role;
4. insert the role relation;
5. insert a non-secret audit record;
6. commit;
7. verify login and role;
8. remove temporary material.

It never accepts a password as a CLI flag, never persists it in SQL files, and
refuses to create a permanent default seed.

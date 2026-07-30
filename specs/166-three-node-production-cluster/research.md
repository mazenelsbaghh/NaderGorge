# Phase 0 Research: Three-Node Production Cluster

## 1. Edge ingress without paid Cloudflare Load Balancing

**Decision**: Use one Cloudflare Tunnel with a `cloudflared` replica on each
server. Each connector targets its local HAProxy; every HAProxy balances across
all three application nodes over WireGuard.

**Rationale**: Cloudflare documents that multiple tunnel replicas on distinct
hosts provide high availability and that a failed connector is removed while
remaining replicas continue. Cloudflare also documents that replicas do not
perform intelligent round-robin steering. The two-layer design therefore uses
tunnel replicas for edge continuity and HAProxy for deterministic health-aware
load distribution across all application nodes.

**Alternatives considered**:

- Multiple orange-cloud A records: rejected because standard DNS/proxy records
  do not prove health-aware origin removal.
- Paid Cloudflare Load Balancing: explicitly excluded.
- VRRP/floating public IP: no private L2 network or documented provider
  floating-IP capability was found on the current hosts.
- One tunnel connector: rejected as a single point of failure.

**Sources**:

- [Cloudflare Tunnel availability and failover](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/configure-tunnels/tunnel-availability/)
- [Cloudflare Tunnel configuration and replicas](https://developers.cloudflare.com/tunnel/configuration/)
- [Cloudflare Tunnel routing](https://developers.cloudflare.com/tunnel/routing/)

## 2. Encrypted inter-node network

**Decision**: Build a full-mesh WireGuard overlay with fixed internal addresses.
Bind PostgreSQL, etcd, Redis, Sentinel, GlusterFS, HAProxy peer checks and
management endpoints only to WireGuard/loopback. UFW/nftables denies those
ports on the public interface.

**Rationale**: The inspected servers expose only a public `eth0`; inter-node
latency is below 2 ms but the network is not private. WireGuard supplies
authenticated encryption and stable logical addressing without depending on a
provider private-network feature.

**Alternatives considered**:

- Plain public-IP allowlists: encrypted identity and confidentiality are absent.
- Provider private LAN: not visible on the hosts and no official capability was
  established.
- OpenVPN: valid, but WireGuard has a smaller host configuration and direct
  kernel support.

## 3. One logical PostgreSQL database

**Decision**: PostgreSQL 16 with Patroni and a three-member etcd DCS. Enable
data checksums, `wal_log_hints`, `pg_rewind`, quorum synchronous mode with one
required synchronous standby, strict refusal when that durability cannot be
met, and HAProxy health checks against Patroni's primary endpoint.

**Rationale**: Patroni coordinates exactly one leader through a quorum DCS and
supports synchronous failover. Requiring one synchronous standby makes an
acknowledged ordinary commit present on two nodes, so loss of one node does not
lose that commit. Applications always use one stable writer endpoint and never
address replicas directly.

**Alternatives considered**:

- PostgreSQL streaming replication alone: no automatic safe election.
- Async replication: can lose acknowledged writes.
- Multi-primary PostgreSQL: unnecessary conflict surface and violates one
  authoritative writer.
- Docker Compose health order as HA: does not provide consensus or promotion.

**Caveat**: Strict synchronous systems deliberately reject writes when the
required replica/quorum is unavailable. Patroni documents a rare PostgreSQL
edge involving cancellation while a backend waits for synchronous
acknowledgement; failure drills must include client retry/idempotency and verify
the actual transaction outcome.

**Sources**:

- [Patroni replication modes](https://patroni.readthedocs.io/en/latest/replication_modes.html)
- [Patroni documentation](https://patroni.readthedocs.io/en/latest/)

## 4. Shared Redis, BullMQ and SignalR

**Decision**: Redis primary plus two replicas, Sentinel on all three nodes with
quorum two. Enable AOF every second, require one healthy replica for writes, and
configure both StackExchange.Redis and ioredis/BullMQ through Sentinel-aware
settings. Keep all workers active, but use durable idempotency keys and
renewable distributed ownership for schedules.

**Rationale**: Sentinel monitors, elects, promotes and reconfigures Redis
members. Three Sentinels avoid a single decision maker. SignalR already uses a
Redis backplane; BullMQ is at-least-once, so failover safety also requires
idempotent application effects.

**Alternatives considered**:

- One Redis instance: queue/backplane SPOF.
- Redis Cluster: sharding is unnecessary at this scale and complicates BullMQ
  key-slot behavior.
- Running scheduled services once by deployment placement only: node loss would
  stop them and an accidental second instance could duplicate effects.

**Source**:

- [Redis Sentinel](https://redis.io/docs/latest/operate/oss_and_stack/management/sentinel/)

## 5. One active file store with a live copy

**Decision**: GlusterFS 11.2 `replica 3 arbiter 1` across the three nodes:
full-data preferred brick on node-1, full-data live replica on node-2, metadata
arbiter on node-3. Mount the same logical volume on all app nodes. Keep client
quorum enabled; use atomic temp-write/fsync/rename and fail uploads on loss of
quorum.

**Rationale**: This layout stores file bytes on exactly the primary and its live
copy, while the third server stores only arbitration metadata. Gluster's
arbiter exists specifically to prevent split-brain while using the disk space
of two data copies. Writes are synchronously replicated before success returns;
the standby copy can be used automatically after primary loss with arbiter
quorum.

**Alternatives considered**:

- Node-local Docker volumes: files diverge across app nodes.
- `rsync`/lsyncd/Syncthing async copy: can acknowledge a file before the live
  copy has it.
- Replica 2 Gluster: official guidance shows quorum either sacrifices HA or
  permits unsafe split-brain behavior.
- DRBD + Pacemaker: no separate block devices exist on the inspected VPSs, and
  shrinking the live root filesystem or using loopback backing files is not a
  production-safe bootstrap.
- Distributed MinIO/Ceph: valid shared stores but do not match the approved
  primary/full-live-copy/arbiter operating model and add a larger migration.

**Sources**:

- [Gluster arbiter volumes and quorum](https://docs.gluster.org/en/latest/Administrator-Guide/arbiter-volumes-and-quorum/)
- [Gluster volume setup](https://docs.gluster.org/en/latest/Administrator-Guide/Setting-Up-Volumes/)
- [Ubuntu 26.04 GlusterFS 11.2 package](https://packages.ubuntu.com/resolute/admin/glusterfs-server)

## 6. Database and file backup policy

**Decision**: pgBackRest with an encrypted S3-compatible Garage repository
self-hosted across all three cluster nodes at replication factor 3; continuous WAL archive with PostgreSQL
`archive_timeout=300s`, daily differential and weekly full backups in quiet windows, 30-day rolling
retention, and monthly isolated PITR restore. Back up Gluster content hourly by
checksum/version to a separate encrypted internal prefix and restore a sampled
public/private set monthly.

**Rationale**: Replication protects availability, not accidental deletion or
corruption. WAL archiving supplies point-in-time recovery without running a
full backup every five minutes. Three replicas survive one-node loss; the owner
accepts that this topology does not survive loss or compromise of all three
servers. Restore evidence, not backup command success, is the acceptance proof.

**Alternatives considered**:

- Full backup every five minutes: unnecessary I/O and storage load.
- Hostinger snapshots only: provider documentation says snapshots are manual,
  only one is kept, it expires after 20 days, and restore overwrites the current
  server. This cannot meet 30-day PITR or monthly isolated restore.
- Backup only on node-3: one-node loss can destroy it.
- External S3/R2: rejected by the owner for this phase in favor of storage on
  the three approved servers.

**Sources**:

- [pgBackRest user guide](https://pgbackrest.org/user-guide.html)
- [pgBackRest configuration reference](https://pgbackrest.org/configuration.html)
- [Hostinger VPS backup and snapshot behavior](https://support.hostinger.com/en/articles/1583232-how-to-back-up-or-restore-a-vps)

## 7. Build once and rolling release

**Decision**: Build the release images once on a designated amd64 build node,
tag with Git commit plus content digest, export OCI archives, and transfer the
same archives to the other nodes over the encrypted overlay. Record and compare
digests before rollout. Drain/deploy/check/undrain one node at a time.

**Rationale**: An external registry credential is not currently available.
OCI archive distribution still proves all nodes run identical bytes and avoids
building slightly different images on each host. The mechanism can later push
the same digests to GHCR or another registry without changing the rollout
contract.

**Alternatives considered**:

- Build separately on each node: no immutable parity.
- `latest` tags: not traceable or rollback-safe.
- Add a single-node self-hosted registry: creates a new deployment SPOF without
  improving release immutability.

## 8. Migration and schema audit

**Decision**: A single migrator image obtains a PostgreSQL advisory lock,
applies the full EF migration chain to an empty audit database, asserts no
pending model changes, inventories schema/constraints/indexes/extensions/seeds,
then applies the identical chain to production. A failed or partial migration
blocks rollout.

**Rationale**: The repository has a long migration history and existing demo
seed code. Starting production empty is the best opportunity to detect ordering
errors, schema drift, unintended defaults and data before real traffic.

**Confirmed critical finding**:

- `20260613154904_AddIbrahimAdmin.cs` embeds a fixed Admin identity and password
  hash and inserts the account during a fresh migration.
- `20260607200637_AddMultiTeacherSubjectArchitecture.cs` inserts a fixed teacher
  user, subject, profile and their relations while also using them during
  architecture conversion.

Therefore the present chain does **not** produce the approved empty Production
database. Implementation must first inventory every raw SQL/data migration,
remove the embedded Admin creation from future clean installs without exposing
its old secret, and add a new forward cleanup migration for environments where
it is already applied. The teacher/subject migration requires dependency-aware
cleanup: retain structural data needed to complete the historical transform,
then delete only the known legacy bootstrap rows in a forward migration after
verifying no approved rows reference them. Clean-DB tests assert all known
legacy identities/GUIDs and unapproved catalog rows are absent.

**Alternatives considered**:

- Let every backend call `Database.Migrate`: creates concurrent startup races.
- Restore the test database: explicitly excluded and carries unknown data.
- Compare table names only: misses constraints, indexes, defaults and seeds.
- Rewrite all applied migration history wholesale: rejected because it makes
  existing-environment behavior opaque; changes are limited to removal of the
  embedded secret/bootstrap path plus an explicit forward cleanup migration
  that is safe for already-applied databases.

## 9. First administrator bootstrap

**Decision**: Generate the application-compatible BCrypt hash in a no-history,
no-echo helper; pass values via protected stdin/temporary root-only secret file;
run one SQL transaction that inserts/updates the user, associates the Admin
role, records audit evidence without the secret, verifies login, and securely
removes temporary material.

**Rationale**: The user selected manual database creation. A documented atomic
transaction prevents a half-created identity and avoids a permanent default
admin seed. The actual credentials never enter tracked files or command
arguments.

**Alternatives considered**:

- Hard-coded seed/default password: unacceptable production secret.
- Public one-time setup endpoint: adds an attack surface.
- Plain SQL with password in shell history: leaks the secret.

## 10. Secret and SSH operations

**Decision**: Replace the single test-server skill with a secret-free
three-node inventory, pinned `known_hosts`, key-based non-root `massar-ops`,
least-privilege sudo, and explicit audit/bootstrap/status/deploy/migrate/drain/
failover/backup/restore-test/rollback commands. Rotate all exposed passwords and
tokens before acceptance; do not rewrite unrelated history during this feature.

**Rationale**: The existing skill contains a shared root password, a fixed test
target and disabled host verification. Those properties are incompatible with
production and make accidental destructive targeting likely.

**Alternatives considered**:

- Continue password/root SSH: a shared credential cannot be attributed or
  safely revoked per operator.
- Store passwords in `.env`: `.env` is still a file secret and easy to leak.
- Rewrite the entire Git history immediately: disruptive and not sufficient by
  itself; rotation is the mandatory security action, while coordinated history
  cleanup can be a separate approved maintenance event.

## Resolved Unknowns

No `NEEDS CLARIFICATION` item remains in the technical plan. Cloudflare account
access remains an execution prerequisite for cutover. The backup repository is
the explicitly approved internal three-node Garage cluster; no external S3/R2
credential is required.

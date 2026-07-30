# Production Backup and Restore

## Recovery policy

| Data | Mechanism | Schedule | Retention target |
|---|---|---|---|
| PostgreSQL WAL | pgBackRest continuous archive | Continuous; alert if older than 5 minutes | 30 days |
| PostgreSQL differential | pgBackRest | Daily at 02:30 Cairo | 30 days |
| PostgreSQL full | pgBackRest | Sunday at 03:30 Cairo | 30 days |
| Shared files | Restic encrypted incremental | Hourly with up to 10 minutes random delay | 30 days |
| File prune | Restic forget/prune | Daily | Keep within 30 days |
| Restore proof | Isolated database and file sample restore | Monthly | Evidence retained |

Continuous WAL archiving gives the database point-in-time recovery target. It
does not create a full backup every five minutes. This is substantially lighter
than repeated full backups while still targeting recovery to a point within the
last five minutes. Hourly file backups are incremental and content-addressed.

## Internal three-node repository

Garage provides one S3-compatible bucket across the three approved servers with
replication factor 3 and a bounded initial capacity of 50 GB per node.
pgBackRest and Restic encrypt payloads before upload. Access keys, RPC/admin
tokens, TLS private material, the pgBackRest cipher passphrase and Restic
password stay in root-only files.

This protects against one server loss. By explicit owner decision it is not an
off-site disaster copy and does not protect against simultaneous loss or
compromise of all three servers.

The timers were activated only after repository connectivity, encrypted writes,
checksums and both isolated restores passed. PostgreSQL archive mode was also
verified before schedule activation; enabling it against a broken repository
could retain WAL indefinitely and fill the database disk.

## Activation sequence

1. Place secret values in root-only files referenced by the manifest.
2. Validate all three Garage members and the bucket from one node without
   printing credentials.
3. Create the pgBackRest stanza and run the first full backup.
4. Restore that backup into an isolated temporary PostgreSQL instance.
5. Verify schema, migration history, row integrity and application login smoke.
6. Enable archive mode and verify WAL reaches the repository within five
   minutes.
7. Run one Restic backup and restore a checksum-known sentinel into an isolated
   directory.
8. Enable the timers only after both isolated restores pass.
9. Record backup age, restore evidence and repository growth in monitoring.

Current state: all six timers are enabled and active on all three nodes.
PostgreSQL backup and monthly PITR jobs self-select the Patroni primary. File
backup and restore jobs use shared locks and freshness markers, so installing
the timers on all three nodes adds availability without tripling the workload.

## Restore rules

- Never restore over the live data directory or shared mount.
- Resolve every restore target to an explicit temporary path first.
- Stop if the requested timestamp predates the retained backup chain.
- For PostgreSQL, restore the base backup, replay WAL to the requested timestamp,
  start on an isolated port, then run schema and application smoke checks.
- For files, restore to an isolated directory and compare the stored SHA-256
  manifest before any controlled promotion.
- A backup is not considered valid until a restore has passed.

## Load control

Backup jobs run with low CPU and I/O priority and a maximum of two repository
processes. The daily differential and weekly full run outside the expected busy
window. File backup uses a randomized delay so it does not always collide with
the database job.

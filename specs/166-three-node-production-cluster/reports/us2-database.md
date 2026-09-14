# US2 Database Evidence

- PostgreSQL: version 16 under Patroni
- Topology: one writer and two streaming replicas
- Coordination: authenticated TLS etcd quorum with three members
- Application endpoint: node-local HAProxy writer port
- Clean migration: 129 migrations applied through
  `20260726182136_EnsureSystemRoles`; second run had zero pending
- Schema audit: 203 relations, 2,245 columns, 713 indexes, 634 constraints,
  203 primary keys and 361 foreign keys
- Integrity findings: zero invalid indexes, unvalidated constraints,
  tables without primary keys, duplicate index definitions, ownership
  mismatches, orphan foreign-key rows, duplicate constrained keys, forbidden
  bootstrap rows and critical findings
- Bootstrap roles: Admin, Teacher, Assistant and Student exist after
  migration-from-zero; no fixed user or credential is created
- Admin helper integration: atomic creation, BCrypt hash, duplicate refusal and
  rollback on missing role all passed against disposable PostgreSQL
- Failover drill: acknowledged probe survived writer loss; replacement writer
  elected in 9 seconds; former writer rejoined as a replica
- Credential-rotation restart: replacement writer elected in 23 seconds and the
  restarted member rejoined
- Split-brain protection: a deliberately unreadable TLS dependency caused
  election refusal instead of a second writer; permissions were corrected and
  the bootstrap code was hardened

The internal Garage bucket is healthy with replication factor 3. pgBackRest
stanza creation, TLS access, WAL archiving with a 300-second timeout, and the
first encrypted full backup succeeded. A fresh probe was committed on the live
writer, archived, restored to an isolated temporary PostgreSQL instance, and
verified at the requested timestamp with migration history, roles and zero
invalid indexes. The live data directory was never a restore target.

Evidence:

- `artifacts/production/internal-backup-restore/20260726T210338.316090Z-prepare-pitr-probe.json`
- `artifacts/production/internal-backup-restore/20260726T210427.228612Z-restore-test.json`
- `artifacts/production/internal-backup-audit/20260726T211033.526606Z-database-archive-status.json`

Result: T051 is complete.

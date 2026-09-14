# Monitoring and owner handoff

The owner handoff includes the exact inventory, pinned host-key fingerprints,
release manifest, `NO-GO`/`GO` acceptance evidence, backup repository ownership,
Cloudflare tunnel ownership, and the locations—not values—of root-only secrets.

Operational alerts cover node/app readiness, image digest drift, disk and
inodes, NTP drift, PostgreSQL role/lag/WAL age, etcd quorum, Redis
master/Sentinel quorum, Gluster heal/split brain, queue backlog, tunnel
connectors, database backup age (five-minute WAL bound), hourly file snapshot
age, and monthly restore evidence.

Keep 30 days of encrypted backups in the internal three-node bucket and retain
operational evidence. Run a
monthly isolated database PITR and file restore, and retain checksums and schema
smoke results. A replica, an untested backup, or an installed-but-disabled timer
does not count as recovery.

# Operations Handoff Validation

## Verified artifacts

- `quickstart.md` uses the real `clusterctl.py` command surface and documents
  the required protected operator-file references.
- Production topology, incident response, Admin bootstrap, credential rotation,
  backup/restore, Cloudflare and monitoring runbooks exist under
  `docs/production/`.
- Database WAL/differential/full, hourly file backup, daily prune, monthly
  database restore and monthly file restore systemd units/timers are tracked.
- Cluster health evidence collection is scheduled every minute.
- The acceptance command requires release-bound evidence and a mode-0600
  signing key, signs the SHA-256 digest of every required evidence file, and
  returns `NO-GO` when any gate is missing, malformed, stale or failed.

## Owner handoff bundle

The owner receives the inventory, pinned host fingerprints, release manifest,
acceptance decision, backup bucket ownership, Cloudflare tunnel ownership,
evidence retention location and only the paths—not values—of secrets.

## Activation status

The internal three-node repository, encrypted backups, isolated database/file
restores and all six timers are enabled and healthy on all three nodes. Database
jobs run only on the Patroni primary; file jobs use shared locks and freshness
markers to avoid tripling load.

Tunnel installation and domain tests remain blocked until pre-DNS `GO` and
Cloudflare credentials exist. The first Admin has been created through the
protected bootstrap and verified against all three application nodes without
recording its password in operational artifacts. The owner must rotate the
initial value before `GO` because it was supplied through conversation.

Result: backup activation and handoff are validated; Cloudflare activation
remains a post-acceptance cutover gate.

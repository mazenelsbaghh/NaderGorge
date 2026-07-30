# Production incident response

Start with a read-only `clusterctl.py status` capture and preserve its evidence.
Resolve current roles from Patroni and Sentinel; never assume a fixed primary.

- One app/ingress failure: drain that node globally, keep the other two serving,
  inspect its release and health, then repair and undrain.
- PostgreSQL writer loss: allow Patroni quorum to elect once. Confirm exactly
  one writer and the last acknowledged probe before rejoining the old primary.
  Stop if quorum is absent; never force a second writer.
- Redis master loss: confirm all three Sentinels agree and quorum is two. Verify
  the durable application state in PostgreSQL and rejoin the old master only as
  a replica.
- File brick loss: keep one failure only, verify client quorum and checksums,
  restore the brick, and wait for zero heal/split-brain backlog.
- Suspected compromise: isolate only the affected node, rotate the relevant
  credential with overlap where supported, invalidate sessions if JWT material
  changed, and record the rotation and verification evidence.
- Data loss/corruption: stop writes if necessary and restore only to an isolated
  target first. Never restore directly over Production.

Escalate immediately if a host key changes, a second node becomes unhealthy,
two writers/masters appear, backups are stale, or recovery evidence fails.

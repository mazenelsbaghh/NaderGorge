# Legacy data migration policy

Status: locally verified policy; Production cutover remains NO-GO.

The migration preserves all durable legacy state. This includes refresh tokens,
registered devices, parent push tokens, all outbox history, Web Vitals rows and
HR idempotency records. Only `VideoPlaybackSessions` and `cluster_leases` are
reset in isolated staging. A candidate is refused unless pending non-dead-letter
outbox events equal zero.

The generated reconciliation evidence is
`artifacts/production/commissioning-20260727/legacy-production-reconciliation.json`.
It classifies the six durable tables present in the captured catalog as
`DURABLE_PRESERVE`; the policy contract also covers `cluster_leases` even though
that table was not present in the old source catalog.

The generated file inventory is
`artifacts/production/commissioning-20260727/legacy-file-reference-inventory.json`.
It discovers every public text column whose name carries URL, URI or path
semantics. Thirty columns and 5,417 references were inspected: 5,156 were
external/provider references, 258 local references existed, and three missing
local references were already blocked live-support attachments. Missing
unblocked local references were zero and remain a critical candidate gate.

Migration acceptance was regenerated from a disposable PostgreSQL 16 clone by
`deploy/production/scripts/generate_local_migration_evidence.py`. It exposed and
fixed missing EF discovery metadata on two manual migrations. A clean database
then applied all 129 migrations, a second migrator run reported zero pending,
and `specs/166-three-node-production-cluster/evidence/database/post-migration-audit.json`
reported exact model match with zero critical findings.

No server, SSH, candidate build or cutover action was used to produce this
evidence.

If a downstream step fails after an authoritative capture leaves the old
writers stopped, `deploy/production/scripts/resume_legacy_writers.py` is the
reviewed recovery action. It requires the exact backup ID and captured
host/user binding, resumes only the containers listed in the capture evidence,
uses a locked remote journal with per-writer progress, and verifies every writer
is running before committing success. Interrupted attempts can resume with new
immutable attempt evidence; a committed recovery or mismatched owner is refused.
Its dry-run performs no SSH.

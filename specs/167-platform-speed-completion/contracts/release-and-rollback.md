# Contract: Complete Release, Rolling Deploy, and Rollback

## Complete workspace source

- The candidate inventory enumerates every tracked modification/deletion and
  every untracked file present before production publication, including files
  not authored by this feature.
- Directory summaries from `git status` are insufficient; the manifest records
  actual included file paths and hashes.
- Sensitive material is never published. A secret finding is a blocking defect
  that must be resolved with the owner; it is not silently omitted.
- Generated/cache artifacts are classified explicitly. They may be represented
  as evidence rather than copied into runtime images only when their
  classification is recorded and the user-required source change remains
  traceable.
- The release `SourceDigest` covers the complete releasable snapshot, not only
  `backend/`, `frontend/`, `worker/`, and `deploy/`.

## Candidate invalidation

- Seal an exact source state before build.
- Compare source state before and after every candidate-building/verification
  stage.
- Any workspace content/path delta invalidates the candidate.
- Invalid candidates and their artifacts remain immutable evidence; create a
  new release ID, rebuild all four images, and rerun the complete gate set.
- No already built image may be patched in place.

## Artifact and migration contract

- Build backend, frontend, worker, and migrator images once.
- Record local image IDs/digests, archive digests, source digest, migration set,
  and verification evidence.
- All three nodes verify identical digests before rollout.
- Migrations are additive/forward-compatible and verified with current (N-1)
  and candidate applications against empty and production-like schemas.
- Apply migrations once under cluster serialization after fresh backup/restore
  readiness evidence.

## Rolling state machine

```text
Preflight → BackupReady → ArtifactsDistributed → Migrated
→ node-3 Drained → Deployed → Healthy/SmokePass → Undrained
→ node-2 Drained → Deployed → Healthy/SmokePass → Undrained
→ node-1 Drained → Deployed → Healthy/SmokePass → Undrained
→ ClusterAcceptance → Complete
```

- At least two application nodes remain serving while one is drained.
- The next node cannot start until the current node passes health, release
  identity, smoke, and rejoin checks.
- Critical failures stop advancement immediately.

## Automatic application rollback

When a critical post-update gate fails:

1. Stop all further advancement.
2. Record the failed gate and current advanced-node set.
3. Drain and restore the failed node and every already advanced node to the
   prior verified application release, in reverse advancement order.
4. Verify prior image identity and compatibility with the applied new schema.
5. Health/smoke the restored node and return it to service before continuing
   the reverse sequence.
6. Verify the cluster is consistently serving the prior application release.

Database behavior:

- Do not run `Down`.
- Do not automatically restore PostgreSQL/PITR.
- Keep the compatible applied schema.
- If the schema itself is defective, remain stopped and produce a reviewed
  forward-only corrective migration before another candidate attempt.

## Final acceptance

- One application release identity on node-1/node-2/node-3.
- Healthy HAProxy/Tunnel connectors and balanced eligible request sample.
- One logical PostgreSQL writer and healthy replicas/quorum.
- Healthy Redis master/Sentinels/backplane and BullMQ processing.
- Shared-file read/write consistency.
- Cross-node SignalR delivery/reconnect.
- Critical visitor/auth/student/admin/support workflows.
- One-node application failure tolerance.
- Complete source, artifact, migration, test, deployment, and rollback evidence
  with no secrets.

## Required orchestration tests

- Full-workspace source digest changes for a tracked or untracked delta outside
  application directories.
- A post-seal delta blocks artifact reuse.
- Manifest v2 accepts complete new candidates and rejects incomplete/mixed
  source evidence.
- Node-2 failure after node-3 success rolls back both application nodes in
  reverse order.
- Node-1 failure rolls back node-1, node-2, and node-3 applications.
- Rollback invokes no database down/restore command.
- Prior app smoke passes against the retained new schema.

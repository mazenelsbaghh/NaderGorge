# US5 Release Evidence

- Release identifier:
  `src-0541078d8f68c5f05df6cf21f665e6714390d4e4`
- The fixed remote builder on node-3 built each image once and distributed
  verified archives without a local Docker build or image tar.
- Backend, frontend and worker image IDs are identical on all three nodes.
- All eight application services are healthy on every node.
- Database migration ran once after a fresh backup, isolated restore,
  target-migration and N-1 compatibility gate.
- Rolling deployment completed node-3, node-2, node-1.
- `/opt/massar/current` and the immutable manifest identify the same release on
  all nodes.
- Production `docker compose config -q` passed on all three nodes for this
  release.

The repository now contains digest-validated release manifests, a backup and
restore gate, all-ingress drain/undrain convergence, node-3/node-2/node-1
rolling deployment, stop-with-failed-node-drained behavior and an
application-only rollback that requires current-schema compatibility and never
runs a down migration. Contract tests pass.

The normal rolling path is proven in Production. PostgreSQL 16's random
`\restrict`/`\unrestrict` session-token lines are now excluded from the
canonical schema hash. A fresh encrypted full backup, isolated restore, target
migration and explicit N-1 readiness run proved
`src-f8369c56e77d9fb0c6c75d2ef7502d25343d5113` against the current schema.
The live compatibility-bound rollback then completed node-3/node-2/node-1,
all three nodes passed status on the previous release, and the current release
was redeployed in the same rolling order with no down-migration.

The bounded readiness-failure rehearsal then drained `node-3` on all three
HAProxy ingresses and stopped only its gateway. Direct `node-3` readiness
failed as injected while 60/60 requests through the remaining ingress path
continued without error: 30 were served by `node-1` and 30 by `node-2`, all on
the expected release. The gateway recovered healthy, `node-3` converged back
to `UP` on all three ingresses, and the post-drill cluster status and audit
both passed. Evidence:

- `artifacts/production/readiness-failure-node3-live-20260728/20260728T125426.741255Z-readiness-failure.json`
- `artifacts/production/final-status-after-readiness-drill-20260728/`
- `artifacts/production/final-audit-after-readiness-drill-20260728/`

This completes the live rollout, injected-readiness-failure recovery, rollback
and forward-redeploy evidence required by T089.

An initial operator-side rehearsal harness did not persist its JSON because it
passed an unsupported writer argument, and its recovery flag was set too late
to cover an output-validation exception. The next strict status check detected
the still-stopped gateway while the other two nodes continued serving. The
gateway was started, verified healthy, explicitly undrained, and the full
cluster status passed before the corrected rehearsal above was allowed to run.
No database, Redis, worker or file service was stopped, and no request-loss
evidence was observed.

# Release build and transfer improvements

Owner request: implement all four proposed speed improvements. Build scope: `all`.
Affected components: backend/migrator Dockerfile, frontend/worker Dockerfiles,
production builder, distribution, registry infrastructure, and release contracts.
No EF model or database migration change.

The release now supports input-verified image reuse, persistent dependency/build
caches, a shared backend/migrator compiled stage, and missing-layer distribution
through a private mTLS registry over WireGuard. All four images remain bound to
one release and verified on all three nodes. Historical archive releases remain
readable. Deployment backup, migration and rollback gates remain mandatory.

Validation covers input changes/deletions, base-image changes, wrong registry
origins, image digest mismatch, per-node parity, and stable certificate issuance.
Live installation, build and warm-cache timing evidence is retained under
`artifacts/production`; performance claims require the actual resulting evidence.

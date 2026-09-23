# Incident repair release — 2026-09-23

## Deployed source

- Shared production source: `codex/production` at `6b62893c6686264f2d62403bb63ebdfa790b3675` (parent `ae04812ede2cd3f751f5ccf0921c0dbbaaf4d054`).
- Release ID: `git-6b62893c6686264f2d62403bb63ebdfa790b3675`.
- The source candidate contained the worker PostgreSQL idle-client error handler, the staff worker proxy's production HTTPS authorization check, narrowly scoped Patroni needrestart policy, and diagnostic improvements for WhatsApp Cloud, Bunny HLS, and Redis Sentinel.
- The local `docker/nginx/massar.conf` diagnostic change and unrelated QR/Mim edits were excluded from the published candidate.

## Gate and rollout evidence

| Stage | Result | Evidence |
| --- | --- | --- |
| Source publication | Published under rollout lock with parent compare-and-swap | `codex/production` commit above |
| Build and distribution | Four images built; digest parity on node-1, node-2, and node-3 | `artifacts/production/build/git-6b62893c6686264f2d62403bb63ebdfa790b3675/manifest.json`, `artifacts/production/build/20260923T151549.084724Z-build.json` |
| Backup and migration gate | Encrypted backup, isolated checksum-verified restore, restored-copy migration, real-data validation, and N-1 compatibility succeeded | `artifacts/production/migration-gates/git-6b62893c6686264f2d62403bb63ebdfa790b3675.json` |
| Migration | Succeeded once | `artifacts/production/20260923T152635.339885Z-migrate.json` |
| Rolling deploy | Succeeded in order node-3, node-2, node-1 | `artifacts/production/20260923T153102.206615Z-deploy.json` |
| Final health | Succeeded across the cluster | `artifacts/production/status/20260923T153108.227403Z-status.json` |

Image digests: backend `sha256:7276db95c427f5d2b10ab4451b421cb1b13b1d9f3b86ecd7b5f183ccde21754e`; frontend `sha256:4e7ffd60b18821f2f294c6366217bff9cd5a59c1f298439813f0607bcb6234fe`; worker `sha256:e4ede736831d283315c3b0fc9738379d97606f13afc58ca07f5a07ca3382f34e`; migrator `sha256:6b9a82f1f7849169318a6232a7592c186ffa3f0f885b24358d9521442d131baa`.

## Post-release checks

- All three nodes reported the release ID above in `/opt/massar/current/manifest.json`, and their running worker containers matched the published worker digest with zero restarts at inspection time.
- The narrowly scoped needrestart policy was installed on all three nodes with matching SHA-256 `9c6f336e0fa40c9572b3c7a516a36d9b4c28a20e376b9475f2ffd90d61419590`; syntax checks passed. This installation did not restart Patroni or PostgreSQL.
- Each running staff container had `NEXT_PUBLIC_API_URL=https://api.massar-academy.net/api`; its unauthenticated `/auth/session` request returned 401 without redirect. The public unauthenticated endpoint also returned 401 without redirect.
- Bounded worker and staff log inspection after deployment showed normal startup and queue activity and no recurrence of the idle Pool crash or staff authorization fetch failure in that interval.

## Remaining investigation

The historical WhatsApp Cloud `132001` response still requires an approved template and language configuration. Earlier Bunny CDN timeouts and Redis availability incidents are not proven resolved by this release. The excluded local Nginx diagnostic change did not alter production gateway behavior. No outbound test message or load test was sent during post-release verification.

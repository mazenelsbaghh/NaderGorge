# Production rollout evidence

- Branch: `168-platform-financial-center`.
- Final release: `src-eaa987d7da12d3735f5cecdd372ff883ce21f5e1`.
- Component scope: `all` under the immutable release contract.
- Migration evidence: `artifacts/production/20260805T062713.541516Z-migrate.json`.
- Deploy evidence: `artifacts/production/20260805T063051.477447Z-deploy.json`.
- Final cluster status: `artifacts/production/status/20260805T063055.506179Z-status.json`.
- All three nodes passed rolling health checks.

An earlier release was safely rolled back after its seed exposed a 33-character wallet account code against the 32-character database constraint. The fix uses the compact GUID as the account code and was verified before this final release.

# Production rollout evidence

- Branch: `168-platform-financial-center`.
- Source commit: `c65ec93ea` (`feat: group platform finance in admin shell`).
- Final release: `src-32786777d269aff8d8284782c9858c38f885b87e`.
- Component scope: `all` under the immutable release contract.
- Remote build evidence: `artifacts/production/build/20260805T114835.551776Z-build.json`.
- Migration gate: `artifacts/production/migration-gates/src-32786777d269aff8d8284782c9858c38f885b87e.json`.
- Migration evidence: `artifacts/production/20260805T115228.238122Z-migrate.json`.
- Deploy evidence: `artifacts/production/20260805T115605.478761Z-deploy.json`.
- Final cluster status: `artifacts/production/status/20260805T115609.840297Z-status.json`.
- All three nodes passed rolling health checks.

An earlier release was safely rolled back after its seed exposed a 33-character wallet account code against the 32-character database constraint. The fix uses the compact GUID as the account code and was verified before this final release.

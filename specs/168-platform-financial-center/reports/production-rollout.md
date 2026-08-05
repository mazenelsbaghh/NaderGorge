# Production rollout evidence

- Branch: `168-platform-financial-center`.
- Source commit: `c62724dca` (`feat: complete platform financial center workflows`).
- Final release: `src-e6442e3323d8a6d031ea57f51098f2d2e1a2ba62`.
- Component scope: `all` under the immutable release contract.
- Remote build evidence: `artifacts/production/build/20260805T073033.832213Z-build.json`.
- Migration gate: `artifacts/production/migration-gates/src-e6442e3323d8a6d031ea57f51098f2d2e1a2ba62.json`.
- Migration evidence: `artifacts/production/20260805T073405.601666Z-migrate.json`.
- Deploy evidence: `artifacts/production/20260805T073736.005850Z-deploy.json`.
- Final cluster status: `artifacts/production/status/20260805T073744.641522Z-status.json`.
- All three nodes passed rolling health checks.

An earlier release was safely rolled back after its seed exposed a 33-character wallet account code against the 32-character database constraint. The fix uses the compact GUID as the account code and was verified before this final release.

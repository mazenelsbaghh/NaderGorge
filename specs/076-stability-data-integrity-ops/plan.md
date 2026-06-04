# Implementation Plan: Stability, Data Integrity, CI, and Operations

**Branch**: `076-stability-data-integrity-ops` | **Date**: 2026-06-04 | **Spec**: `specs/076-stability-data-integrity-ops/spec.md`

## Summary

Implement Phase 2 remediations from `docs/audit-remediation-phase-2-stability-data-integrity.md` with conservative, verifiable changes:

- CI uses .NET 9, port 8738, worker build, and required E2E secrets.
- Production compose keeps internal services private; dev port publishing moves to an override.
- Backend adds forwarded headers, security headers, public endpoint rate limits, and claim extraction fail-fast.
- AI processing locks use atomic EF `ExecuteUpdateAsync`.
- Role and balance mutations get validation and stronger data integrity.
- Worker stops storing code hash as plaintext, uses configured subtitle storage, and redacts sensitive logs.
- Frontend moves from deprecated `middleware.ts` to `proxy.ts`, fixes API fallback port, and removes old brand domains.

## Constitution Check

- Backend changes stay within API/Application/Infrastructure boundaries.
- Worker operational logic remains in worker, not frontend.
- Frontend proxy/routing change follows the official Next.js 16 proxy convention.
- UI/UX design scope is limited: no new layout work, only routing/copy safety and no visible regressions.
- Security-by-default principle is maintained: production avoids hidden fallbacks and public internal ports.

## Official Reference

Next.js official docs state that in v16 the middleware file convention is deprecated and renamed to Proxy; `proxy.ts` should be located at the same level as `app` when using `src` layout.

## Source Changes

```text
.github/workflows/e2e-tests.yml [MODIFY]
.github/workflows/deploy.yml [MODIFY]
docker-compose.yml [MODIFY]
docker-compose.override.yml [NEW]

backend/src/NaderGorge.API/
├── Configuration/RateLimitingConfig.cs [MODIFY]
├── Configuration/SecurityHeadersMiddleware.cs [NEW]
├── Controllers/* selected controllers [MODIFY]
├── Extensions/ClaimsPrincipalExtensions.cs [NEW]
└── Program.cs [MODIFY]

backend/src/NaderGorge.Application/Features/Admin/Commands/
├── AnalyzeVideoAICommand.cs [MODIFY]
├── MindmapOps/GenerateChapterMindmapsCommand.cs [MODIFY]
├── UpdateUserRoleCommand.cs [MODIFY]
└── AdjustBalanceCommand.cs [MODIFY]

frontend/src/
├── proxy.ts [NEW]
├── middleware.ts [DELETE]
└── services/api-client.ts [MODIFY]

worker/src/
├── index.ts [MODIFY]
├── logging.ts [NEW]
└── jobs/analyzeVideoChapters.ts [MODIFY]
```

## Risk Decisions

- Dependency upgrades are audited and documented if they require risky major changes; forced audit upgrades are avoided.
- Balance updates are validated and single-save; fully serializable SQL locking can be added later if needed.
- Timestamp legacy behavior is documented as a migration plan rather than removed abruptly.

## Verification

```bash
dotnet restore backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-restore
cd frontend && npm audit --omit=dev && npm run build && npm run lint
cd worker && npm audit --omit=dev && npm run build
docker compose config -q
```

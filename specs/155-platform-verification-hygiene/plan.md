# Implementation Plan: Platform Verification Hygiene and Phase 1 Closure

**Branch**: `[155-platform-verification-hygiene]` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `specs/155-platform-verification-hygiene/spec.md`

## Summary

Close Phase 0 from the platform remediation plan by making verification commands, Playwright local runtime, repository hygiene, generated artifact tracking, Docker config, and deploy safety explicit and enforceable. Then close the remaining Phase 1 browser-readiness gaps by replacing the local `app.localhost` ↔ `localhost` cookie mismatch with a documented same-site E2E domain strategy and repeatable smoke commands. The implementation stays operational/tooling-focused and must not revert the completed auth/session implementation from `154-auth-session-permission-safety`.

## Technical Context

**Language/Version**: C# 13 / .NET 9 backend; TypeScript 5.9 / Next.js 16.2.7 frontend; Node.js 20 worker; Makefile/Docker/Playwright tooling.  
**Primary Dependencies**: ASP.NET Core, EF Core, Next.js App Router, Playwright, Docker Compose, npm scripts, worker TypeScript build.  
**Storage**: No new application persistence; repository tracking state and generated artifacts are affected.  
**Testing**: `dotnet build/test`, `npm run lint`, `npm run build`, `npm run build` in `worker`, `docker compose config -q`, Playwright smoke.  
**Target Platform**: Local developer machine, CI-like verification environment, Docker Compose services.  
**Project Type**: Multi-surface web platform with backend, frontend, worker, mobile folders, and Docker orchestration.  
**Performance Goals**: Verification command should fail fast with clear command boundaries; no runtime performance change expected.  
**Constraints**: Do not delete source files; do not revert unrelated dirty work; remove hardcoded deploy secrets; do not mark E2E/manual tasks complete unless verified or explicitly recorded as blocked.  
**Scale/Scope**: Phase 0 checklist plus remaining Phase 1 checklist items in `docs/full-platform-defects-remediation-phases-2026-06-29.md`.

## Constitution Check

- **Layer impact**: Backend auth cookie settings and tests may be inspected but no schema change is planned. Frontend changes focus on Playwright config, package scripts, and already-completed auth runtime safeguards. Worker change is expected to be package/build contract only. Docker changes are limited to config defaults/requirements and verification.
- **Automated tests required**: backend solution build/test or documented subset; frontend lint/build; worker build; Docker compose config; Playwright Phase 1 smoke.
- **Manual QA required**: verification command from root, deploy target inspection, generated artifact status check, browser auth/session smoke on aligned local domains.
- **Docker gates**: `docker compose config -q`; optional startup smoke only if required secrets/local services are available.
- **No-next-phase rule**: Phase 2 financial remediation stays out of scope until Phase 0 and remaining Phase 1 evidence are green or blockers are explicitly owner-accepted.

Gate status: PASS. This feature is tooling/security hygiene with no planned architecture violation.

## Project Structure

### Documentation (this feature)

```text
specs/155-platform-verification-hygiene/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── verification-contract.md
│   └── e2e-auth-surface-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
Makefile
.gitignore
AGENTS.md
docs/
├── full-platform-defects-remediation-phases-2026-06-29.md
└── verification-contract.md
frontend/
├── package.json
├── playwright.config.ts
├── tests/e2e/
│   ├── auth.spec.ts
│   ├── admin-users.spec.ts
│   └── parent-report.spec.ts
└── tests/fixtures/global-setup.ts
worker/
├── package.json
└── tsconfig.json
docker-compose.yml
```

**Structure Decision**: Use existing root tooling and documentation surfaces. Do not add a new application module; the feature changes verification contracts and small runtime/test config surfaces only.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet restore backend/NaderGorge.sln`
- `dotnet build backend/NaderGorge.sln --no-restore`
- `dotnet test backend/NaderGorge.sln --no-build` or a documented focused subset if full solution tests require unavailable services.
- `cd frontend && npm run lint`
- `cd frontend && npm run build`
- `cd worker && npm run build`
- `docker compose config -q`
- `cd frontend && npx playwright test tests/e2e/auth.spec.ts tests/e2e/admin-users.spec.ts tests/e2e/parent-report.spec.ts --project=chromium -g "Phase 1|Parent report"` with aligned local domain environment.

**Docker Gate Required**:

- `docker compose config -q` must pass.
- Startup smoke can be run only when required secrets (`API_CALLBACK_SECRET`, `AI_CALLBACK_SECRET`, `WORKER_ADMIN_TOKEN`, `PARENT_REPORT_SIGNING_SECRET`, credential mounts) are present.

**Manual QA Required**:

- Run root verification contract command and inspect output.
- Inspect `git status --short` after Playwright and confirm generated reports are ignored/untracked as intended.
- Inspect Makefile deploy targets and confirm no hardcoded password and no automatic `git add .`/commit/merge/push deploy workflow remains.
- Browser-check student auth hydration and assistant/admin direct-route denial on the aligned E2E domain strategy.

**End-of-Phase Report Format**:

- Implemented scope
- Files changed
- Commands run and results
- Generated artifact cleanup status
- Secret/deploy safety status and rotation note
- Phase 1 E2E/manual status
- Remaining blockers, if any

## Complexity Tracking

No constitution violations requiring complexity justification.

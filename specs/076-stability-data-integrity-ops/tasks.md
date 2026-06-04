# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

# Tasks: Stability, Data Integrity, CI, and Operations

Target prompt: create the tasks file so that a cheaper llm model can implement without problems

## CI and Compose

- [x] T001 In `.github/workflows/e2e-tests.yml`, change `dotnet-version` to `9.0.x`.
- [x] T002 In `.github/workflows/e2e-tests.yml`, wait for frontend at `http://localhost:8738`.
- [x] T003 In `.github/workflows/e2e-tests.yml`, add worker dependency install/build and frontend build/lint gates.
- [x] T004 In `.github/workflows/e2e-tests.yml`, set required E2E env secrets for backend startup and E2E test seeding.
- [x] T005 In `.github/workflows/deploy.yml`, run migration before restart, add health checks, and avoid destructive prune.
- [x] T006 In `docker-compose.yml`, remove production DB/Redis host port publishing.
- [x] T007 Add `docker-compose.override.yml` with dev-only DB/Redis port mappings.

## Backend Stability and Integrity

- [x] T008 In `Program.cs`, require Redis configuration outside development and remove raw multiplexer fallback.
- [x] T009 In `Program.cs`, add forwarded headers before rate limiting/auth.
- [x] T010 Add `SecurityHeadersMiddleware.cs` and register it in `Program.cs`.
- [x] T011 In `RateLimitingConfig.cs`, add public policies for WhatsApp, public forms, and parent reports.
- [x] T012 In `WhatsAppController.cs`, apply public rate limit and mask returned phone number.
- [x] T013 In `PublicFormsController.cs`, apply rate limit and reject excessive answer dictionaries.
- [x] T014 In `AnalyzeVideoAICommand.cs`, acquire `IsProcessingAI` with atomic `ExecuteUpdateAsync` and release on enqueue failure.
- [x] T015 In `GenerateChapterMindmapsCommand.cs`, acquire `IsProcessingMindmaps` with atomic `ExecuteUpdateAsync` and release on enqueue failure.
- [x] T016 In `UpdateUserRoleCommand.cs`, reject empty role lists, unknown roles, and removing the last admin.
- [x] T017 In `AdjustBalanceCommand.cs`, validate amount/reason and avoid intermediate save before the final balance transaction.
- [x] T018 Add shared claim helper and replace `Guid.Empty` fallback in selected controllers.

## Frontend Routing and Config

- [x] T019 Replace `frontend/src/middleware.ts` with `frontend/src/proxy.ts` exporting `proxy`.
- [x] T020 In `frontend/src/proxy.ts`, move old hardcoded domains to env-based defaults and remove old brand strings.
- [x] T021 In `frontend/src/services/api-client.ts`, change fallback API base URL to `http://localhost:5245/api`.

## Worker Runtime

- [x] T022 In `worker/src/index.ts`, hash generated access codes for `CodeHash` instead of storing plaintext as hash.
- [x] T023 In `worker/src/jobs/analyzeVideoChapters.ts`, use `SUBTITLE_STORAGE_PATH` and `PUBLIC_SUBTITLE_BASE_URL`.
- [x] T024 Add `worker/src/logging.ts` and remove raw Redis payload/PII logs from active worker paths.
- [x] T025 In `worker/.env.example`, document subtitle storage configuration.

## Verification

- [x] T026 Run `dotnet restore backend/NaderGorge.sln && dotnet test backend/NaderGorge.sln --no-restore`.
- [x] T027 Run `cd frontend && npm audit --omit=dev && npm run build && npm run lint`.
- [x] T028 Run `cd worker && npm audit --omit=dev && npm run build`.
- [x] T029 Run `docker compose config -q` with required dummy secrets.

## Warnings and Issues / تحذيرات ومشاكل

- [x] T030 Frontend lint reports 105 warnings and 0 errors; classify as Phase 3 product/UX/code-quality cleanup scope.
- [x] T031 Verify frontend and worker production dependency audits report 0 vulnerabilities.

## Critique & Architectural Issues / مشاكل الانتقاد والبنية

- [x] T032 In `backend/src/NaderGorge.API/Program.cs`, gate HSTS/HTTPS redirection to Production or `Security:RequireHttps=true` so Docker health checks stay HTTP-compatible.
- [x] T033 In `backend/src/NaderGorge.API/Controllers/WhatsAppController.cs`, mask phone numbers in degraded 503 responses.
- [x] T034 In `backend/src/NaderGorge.API/Controllers/PublicFormsController.cs`, reject null form submissions and null answer values.
- [x] T035 In `worker/src/index.ts`, remove residual raw mindmap payload logging.
- [x] T036 In `frontend/src/app/api/qr/[codeHash]/route.ts`, replace the old brand fallback domain with `NEXT_PUBLIC_APP_DOMAIN`/current default.

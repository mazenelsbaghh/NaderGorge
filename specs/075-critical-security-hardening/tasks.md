# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

# Tasks: Critical Security Hardening

Target prompt: create the tasks file so that a cheaper llm model can implement without problems

## Backend Startup and Authorization

- [x] T001 In `backend/src/NaderGorge.API/Configuration/SecurityConfigurationValidator.cs`, add a static validator with methods for required secrets, unsafe placeholder detection, and environment-aware checks.
- [x] T002 In `backend/src/NaderGorge.API/Program.cs`, call the validator before building the app.
- [x] T003 In `backend/src/NaderGorge.API/Program.cs`, register authorization policy `RequireStudent` requiring role `Student`.
- [x] T004 In `backend/src/NaderGorge.API/Program.cs`, gate `Seeder.SeedAsync(db)` behind `SeedDefaults:Enabled=true` and environment `Development` or `E2e`.
- [x] T005 In `backend/src/NaderGorge.Infrastructure/Data/Seeder.cs`, keep role seeding available but make default user creation controlled by an explicit `seedDefaultUsers` parameter.

## Backend Internal and Parent Report Security

- [x] T006 In `backend/src/NaderGorge.API/Configuration/ServiceTokenValidator.cs`, implement constant-time bearer/header token validation.
- [x] T007 In `backend/src/NaderGorge.API/Controllers/InternalController.cs`, replace all inline `secretxyz` fallback checks with `ServiceTokenValidator`.
- [x] T008 In `backend/src/NaderGorge.API/Controllers/ParentController.cs`, require a signed token query parameter and reject missing/invalid tokens before sending `GetParentReportQuery`.
- [x] T009 In `backend/src/NaderGorge.API/Controllers/ParentController.cs`, add a token creation endpoint for admins so existing copy-link UI can request a signed link later.
- [x] T010 In `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs`, require E2E environment, `X-E2E-Token`, configured `E2E_TEST_TOKEN`, and E2E DB marker before destructive seeding.
- [x] T011 In `backend/src/NaderGorge.Application/Features/Auth/Commands/ResetPasswordCommand.cs`, revoke all existing refresh tokens for the user after password hash update.

## Frontend XSS and Proxy Hardening

- [x] T012 In `frontend/src/lib/sanitize-html.ts`, add a strict allowlist sanitizer for rich text HTML.
- [x] T013 In `frontend/src/components/exams/ExamViewer.tsx`, sanitize all question/option HTML before `dangerouslySetInnerHTML`.
- [x] T014 In `frontend/src/app/student/mistakes/page.tsx`, sanitize question HTML before rendering.
- [x] T015 In `frontend/src/app/admin/content/exams/[id]/dashboard/page.tsx`, sanitize question text before rendering.
- [x] T016 In `frontend/src/app/api/video/embed/route.ts`, replace watermark `innerHTML` construction with text node/span construction using safely JSON-encoded strings.
- [x] T017 In `frontend/src/app/api/worker/[...path]/route.ts`, enforce an allowlist for `status/:id` and `status/:id/retry`, reject unsupported methods, and forward `WORKER_ADMIN_TOKEN` as a bearer token.
- [x] T018 In `frontend/src/services/report-service.ts`, require a parent report token argument when fetching report summary.
- [x] T019 In `frontend/src/components/admin/CopyParentLinkButton.tsx`, stop copying raw `/parent-report/{studentId}` links unless a signed token is available.

## Worker and Config Hardening

- [x] T020 In `worker/src/security.ts`, add token validation helpers and required secret validation.
- [x] T021 In `worker/src/index.ts`, remove open production CORS and protect status/cancel/retry routes plus Bull Board with worker token auth.
- [x] T022 In `worker/src/jobs/analyzeVideoChapters.ts`, remove `secretxyz` fallback for callback secret.
- [x] T023 In `worker/src/jobs/generateChapterMindmaps.ts`, remove `secretxyz` fallback for callback secret.
- [x] T024 In `worker/src/jobs/evaluateEssay.ts`, remove `secretxyz` fallback for callback secret.
- [x] T025 In `docker-compose.yml`, remove `secretxyz` fallbacks and avoid publishing worker port by default.
- [x] T026 In `worker/.env.example`, replace secrets with placeholders.

## Checkpoints

- [x] T027 Run backend tests: `dotnet test backend/NaderGorge.sln --no-restore`.
- [x] T028 Run frontend verification: `cd frontend && npm run build && npm run lint`.
- [x] T029 Run worker verification: `cd worker && npm run build`.
- [x] T030 Run security search: `rg -n "secretxyz|watermark.innerHTML" backend frontend worker docker-compose.yml worker/.env.example`.

## Warnings and Issues

- [x] T031 Fix worker TypeScript build failure by converting `req.params.id` to a local string before BullMQ lookups.
- [x] T032 Fix backend test MSB3277 warning by aligning EF Core relational package version in the test project.
- [x] T033 Add `/parent/reports` to the auth refresh bypass list so invalid public report tokens show the report error instead of redirecting to login.

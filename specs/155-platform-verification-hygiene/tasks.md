# Tasks: Platform Verification Hygiene and Phase 1 Closure

**Input**: Design artifacts from `specs/155-platform-verification-hygiene/`  
**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification
- [x] Phase 2: Arabic Clarification
- [x] Phase 3: Technical Planning
- [x] Phase 4: Detailed Task Breakdown
- [x] T001 Run `speckit-specify` equivalent workflow and create `specs/155-platform-verification-hygiene/spec.md`
- [x] T002 Run `speckit-clarify` equivalent workflow and add clarification session to `specs/155-platform-verification-hygiene/spec.md`
- [x] T003 Run `speckit-plan` setup and create `specs/155-platform-verification-hygiene/plan.md`
- [x] T004 [P] Create `specs/155-platform-verification-hygiene/research.md`
- [x] T005 [P] Create `specs/155-platform-verification-hygiene/data-model.md`
- [x] T006 [P] Create `specs/155-platform-verification-hygiene/contracts/verification-contract.md`
- [x] T007 [P] Create `specs/155-platform-verification-hygiene/contracts/e2e-auth-surface-contract.md`
- [x] T008 [P] Create `specs/155-platform-verification-hygiene/quickstart.md`
- [x] T009 Update `AGENTS.md` Spec Kit marker with `155-platform-verification-hygiene`

## Phase 1: Setup and Baseline Evidence

**Purpose**: Capture dirty worktree, generated artifacts, and current command gaps without reverting unrelated user work.

- [x] T010 Record current dirty-worktree caveats in `achievements.md`
- [x] T011 [P] Inspect tracked generated files with `git ls-files frontend/playwright-report frontend/test-results worker/dist mobile/payment-listener-android/gradle-8.14-all.zip`
- [x] T012 [P] Inspect command scripts in `Makefile`, `frontend/package.json`, `worker/package.json`, and `frontend/playwright.config.ts`
- [x] T013 [P] Inspect deploy safety risks in `Makefile`
- [x] T014 [P] Inspect Docker required secret defaults in `docker-compose.yml`

## Phase 2: Foundational Hygiene

**Purpose**: Shared repo hygiene and command contract changes required before story-specific verification.

- [x] T015 Update `.gitignore` to ignore Playwright reports/results, Python caches, mobile Gradle caches, mobile build outputs, downloaded Gradle distributions, and generated worker `dist/`
- [x] T016 Remove tracked Playwright report files from source control using `git rm --cached -r frontend/playwright-report frontend/test-results`
- [x] T017 Remove tracked generated worker `dist/` files from source control if `git ls-files worker/dist` returns files
- [x] T018 Remove tracked downloaded mobile Gradle archives/cache outputs from source control if `git ls-files mobile | rg 'gradle-.*\\.zip|\\.gradle|/build/'` returns files
- [x] T019 Add `docs/verification-contract.md` documenting root verification, E2E verification, known environment requirements, and unavailable script substitutions
- [x] T020 Update `AGENTS.md` Commands section from `npm test && npm run lint` to the new verification contract
- [x] T021 Update `docs/full-platform-defects-remediation-phases-2026-06-29.md` Phase 0 notes to reference the new verification contract

## Phase 3: User Story 1 - Reliable Verification Contract (Priority: P1)

**Goal**: Developers can run a real project verification contract instead of stale commands.

**Independent Test**: Run `make verify` and confirm it executes documented backend/frontend/worker/docker gates or records a documented blocker.

- [x] T022 [P] Add `test` and `typecheck` scripts to `frontend/package.json` without adding a new test framework
- [x] T023 Add `verify` target to `Makefile` that runs backend restore/build/test, frontend lint/build, worker build, and `docker compose config -q`
- [x] T024 Add `verify-backend` target to `Makefile` for `dotnet restore backend/NaderGorge.sln`, `dotnet build backend/NaderGorge.sln --no-restore`, and backend test command
- [x] T025 Add `verify-frontend` target to `Makefile` for `cd frontend && npm run lint && npm run build`
- [x] T026 Add `verify-worker` target to `Makefile` for `cd worker && npm run build`
- [x] T027 Add `verify-docker` target to `Makefile` for `docker compose config -q`
- [x] T028 Update `Makefile` `.PHONY` list and `help` descriptions for all new verify targets
- [x] T029 Run `make verify-docker` and record result in `achievements.md`
- [x] T030 Run `make verify-frontend` and record result in `achievements.md`
- [x] T031 Run `make verify-worker` and record result in `achievements.md`
- [x] T032 Run `make verify-backend` or documented focused backend substitute and record result in `achievements.md`

## Phase 4: User Story 2 - Repository Hygiene and Secret Safety (Priority: P1)

**Goal**: Generated files and unsafe deploy secrets do not remain in source control or executable deploy paths.

**Independent Test**: Run hygiene commands from `quickstart.md` and confirm generated files are untracked/ignored and deploy commands contain no hardcoded password or auto-commit flow.

- [x] T033 Replace dangerous `deploy` target in `Makefile` with a non-mutating instruction that refuses to auto-stage/commit/merge/push
- [x] T034 Replace `deploy-production` in `Makefile` with explicit key-based remote command requirements and no dependency on unsafe `deploy`
- [x] T035 Remove `sshpass -p` usage from `logs-production` in `Makefile`
- [x] T036 Remove `sshpass -p` usage from `logs-production-backend` in `Makefile`
- [x] T037 Add production secret rotation note to `docs/verification-contract.md` for the previously hardcoded SSH password
- [x] T038 Verify `rg -n "sshpass|MazenElsbagh\\.12|git add \\.|git commit -m \"deploy|git merge \\$\\$CURRENT_BRANCH" Makefile` has no unsafe matches
- [x] T039 Verify `git ls-files frontend/playwright-report frontend/test-results` returns no tracked files
- [x] T040 Verify generated Playwright output remains ignored by running `git check-ignore frontend/playwright-report/index.html frontend/test-results/.last-run.json`
- [x] T041 Update Phase 0 generated-artifact checklist items in `docs/full-platform-defects-remediation-phases-2026-06-29.md`
- [x] T042 Update Phase 0 secret/deploy checklist items in `docs/full-platform-defects-remediation-phases-2026-06-29.md`

## Phase 5: User Story 3 - Remaining Phase 1 Browser Readiness (Priority: P1)

**Goal**: Phase 1 browser smoke uses a same-site local domain strategy and no longer fails because API cookies are issued to a different site.

**Independent Test**: Run the Phase 1 Playwright smoke in `quickstart.md` with backend E2E environment using `*.lvh.me`.

- [x] T043 Update `frontend/playwright.config.ts` to use `app.lvh.me:3000` as `baseURL`
- [x] T044 Add Playwright `webServer` config in `frontend/playwright.config.ts` to run `next dev -p 3000` with `NEXT_PUBLIC_API_URL=http://api.lvh.me:5245/api` and `NEXT_PUBLIC_BACKEND_URL=http://api.lvh.me:5245`
- [x] T045 Update `frontend/tests/fixtures/global-setup.ts` to seed through `http://api.lvh.me:5245/api/e2e/seed`
- [x] T046 Update `frontend/tests/e2e/auth.spec.ts` URLs from `*.localhost:3000` to `*.lvh.me:3000` and API calls from `localhost:5245` to `api.lvh.me:5245`
- [x] T047 Update `frontend/tests/e2e/admin-users.spec.ts` URLs from `*.localhost:3000` to `*.lvh.me:3000` and E2E API calls to `api.lvh.me:5245`
- [x] T048 Update `frontend/tests/e2e/parent-report.spec.ts` URLs from `localhost/app.localhost` to the `lvh.me` E2E contract where applicable
- [x] T049 Add backend E2E environment instructions to `docs/verification-contract.md` and `specs/155-platform-verification-hygiene/quickstart.md`
- [x] T050 Run Phase 1 targeted Playwright smoke or record exact backend/environment blocker in `achievements.md`
- [x] T051 Update remaining Phase 1 checklist items in `docs/full-platform-defects-remediation-phases-2026-06-29.md` only for checks that pass

## Phase 6: Polish and Cross-Cutting Verification

**Purpose**: Final docs, report, and broad checks.

- [x] T052 Update `achievements.md` with implementation evidence for every user story
- [x] T053 Run `docker compose config -q` and record result in `achievements.md`
- [x] T054 Run `cd frontend && npm run lint` and record result in `achievements.md`
- [x] T055 Run `cd frontend && npm run build` and record result in `achievements.md`
- [x] T056 Run `cd worker && npm run build` and record result in `achievements.md`
- [x] T057 Run backend build/test verification and record result in `achievements.md`
- [x] T058 Run `python3 .agents/skills/speckit-all/scripts/validate_spec_plan_quality.py --spec-dir specs/155-platform-verification-hygiene`
- [x] T059 Run `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/155-platform-verification-hygiene/tasks.md`

## Phase 7: Required Final Quality Gates

**Purpose**: Enforce the exact final order required by `speckit-all`.

- [x] T060 Perform deep critique fixes against `specs/155-platform-verification-hygiene/spec.md`, `plan.md`, `tasks.md`, and changed production/test files
- [x] T061 Run `clean-code-guard` against changed production/tooling files after deep critique fixes
- [x] T062 Run `test-guard` against changed test files after `clean-code-guard`
- [x] T063 Read `speckit-all/references/feature-test-matrix.md`
- [x] T064 Run `python3 .agents/skills/speckit-all/scripts/extract_test_commands.py --spec-dir specs/155-platform-verification-hygiene`
- [x] T065 Run feature tests from the final feature test matrix after `test-guard`
- [x] T066 Run final build verification after feature tests: backend, frontend, worker, and Docker gates
- [x] T067 Update `achievements.md` with feature tests, guard results, failures fixed, and final readiness
- [x] T068 Run `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/155-platform-verification-hygiene`

## Dependencies & Execution Order

- Phase 1 setup can run immediately.
- Phase 2 hygiene blocks reliable Phase 3 and Phase 4 verification.
- Phase 3 command contract can proceed in parallel with Phase 4 deploy cleanup after `.gitignore` is updated.
- Phase 5 E2E readiness depends on the Playwright and verification contract decisions.
- Phase 7 must run in exact order: deep critique fixes, `clean-code-guard`, `test-guard`, feature tests, final build verification.

## Parallel Execution Examples

- T011, T012, T013, and T014 can run in parallel because they only inspect files.
- T022 can run while T023-T028 are edited if package and Makefile edits are coordinated.
- T035 and T036 can be edited together because both remove `sshpass` from adjacent production log targets.
- T046, T047, and T048 can be updated in parallel because they touch separate Playwright spec files.

## Implementation Strategy

1. Complete hygiene first: ignore/untrack generated artifacts and remove deploy secrets.
2. Add reliable verification commands.
3. Align Playwright/browser domains for remaining Phase 1.
4. Run focused gates, then final guards and validation.

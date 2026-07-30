# Tasks: Platform Speed Completion

**Input**: Design documents from `specs/167-platform-speed-completion/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/`, `quickstart.md`

**Tests**: Mandatory for every behavior, data, authorization, worker, deployment,
or user-visible change.

**Organization**: Tasks are grouped by user story; setup/foundation establish
reproducible evidence and shared contracts.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel without writing the same files or depending on an
  unfinished task.
- **[Story]**: Maps to the six user stories in `spec.md`.

## Phase 1: Setup and Complete-Workspace Baseline

**Purpose**: Protect the moving all-changes scope and capture trustworthy
before-state evidence.

- [x] T001 Enumerate every tracked modification/deletion and actual untracked file with classification and hashes in `artifacts/performance-167/baseline/workspace-manifest.json`
- [x] T002 Add a fail-closed secret and sensitive-path audit for the complete inventory in `deploy/production/scripts/source_manifest.py`
- [x] T003 [P] Record frontend route-specific initial/shared/deferred compressed resource baselines in `artifacts/performance-167/baseline/frontend-routes.json`
- [ ] T004 [P] Record login/register/student navigation, request-count, and Android-profile interaction baselines in `frontend/tests/e2e/platform-performance-baseline.spec.ts`
- [ ] T005 [P] Record live-support SQL command counts and representative API percentiles in `backend/tests/NaderGorge.Integration.Tests/Performance/PlatformPerformanceBaselineTests.cs`
- [x] T006 [P] Record the attached Cloudflare PDF metrics, sample limitations, and excluded secondary-results route in `artifacts/performance-167/baseline/rum-baseline.json`
- [x] T007 Add deterministic workspace delta detection and candidate invalidation tests in `deploy/production/tests/test_source_manifest.py`
- [x] T008 Write Phase 1 baseline evidence and GO/NO-GO in `specs/167-platform-speed-completion/reports/phase1-baseline.md`

**Checkpoint**: Exact before-state and complete workspace inventory are
reproducible; no stale `.next` artifact is accepted as the sealed baseline.

## Phase 2: Foundational Shared Contracts

**Purpose**: Build the shared primitives required by navigation, data, metrics,
and release stories.

- [x] T009 Confirm the local no-download dependency boundary and document existing query groundwork in `specs/167-platform-speed-completion/reports/phase2-local-dependencies.md`
- [x] T010 [P] Define canonical query keys, freshness policies, and identity-boundary clearing in `frontend/src/lib/query-keys.ts` and `frontend/src/lib/query-contracts.ts`
- [x] T011 [P] Define one typed navigation/permission policy without broadening access in `frontend/src/packages/admin/route-permissions.ts` and the corresponding surface navigation modules
- [x] T012 [P] Define version-controlled route/workflow budgets in `frontend/performance-budgets.json`
- [x] T013 Implement one repository-owned browser query client and provider with cancellation-safe defaults in `frontend/src/lib/query-client.ts` and `frontend/src/components/providers/QueryProvider.tsx`
- [x] T014 Implement a root provider island for query, motion preference, auth bootstrap, and toaster ownership in `frontend/src/app/providers.tsx`
- [x] T015 [P] Add query-key, retry, cancellation, cache-clear, and invalidation-map unit contracts in `frontend/src/lib/query-contracts.test.mts`
- [x] T016 [P] Add permission navigation/display/enforcement parity checks in `frontend/scripts/check-route-permission-contracts.mjs`
- [x] T017 Integrate shared providers without adding public-only code to protected surfaces in `frontend/src/app/layout.tsx` and `frontend/src/app/(public)/layout.tsx`
- [x] T018 Run focused frontend strict checks and document Phase 2 foundation results in `specs/167-platform-speed-completion/reports/phase2-foundation.md`

**Checkpoint**: Shared contracts compile, tests pass, and protected/public
boundaries remain unchanged.

## Phase 3: User Story 1 — Fast, Persistent Navigation (Priority: P1)

**Goal**: Same-surface navigation keeps one shell, state, sensible history
scroll, safe deep-link return, targeted prefetch, and correct focus.

**Independent Test**: Navigate between two high-frequency pages on each
protected surface and verify unchanged shell identity, no duplicate bootstrap,
correct state/history/focus, and click-to-usable evidence.

### Tests for User Story 1

- [x] T019 [P] [US1] Add persistent shell identity, navigation state, scroll, focus, and back/forward Playwright tests in `frontend/tests/e2e/persistent-shell-navigation.spec.ts`
- [x] T020 [P] [US1] Add safe deep-link return and open-redirect rejection tests in `frontend/tests/e2e/auth-return-navigation.spec.ts`
- [x] T021 [P] [US1] Add selective prefetch eligible/denied/save-data contract tests in `frontend/tests/e2e/selective-prefetch.spec.ts`
- [x] T022 [P] [US1] Add negative role-route/navigation parity tests in `frontend/tests/e2e/route-permission-parity.spec.ts`

### Implementation for User Story 1

- [x] T023 [US1] Remove or scope the global remounting transition from `frontend/src/app/template.tsx`
- [x] T024 [US1] Move public navigation and public live-support launcher ownership into `frontend/src/app/(public)/layout.tsx` and simplify `frontend/src/components/layout/GlobalNav.tsx`
- [x] T025 [US1] Make the student shell the sole persistent frame and convert student loading/error output to content regions in `frontend/src/app/student/layout.tsx`, `frontend/src/app/student/loading.tsx`, and `frontend/src/app/student/error.tsx`
- [x] T026 [US1] Make the assistant shell the sole persistent frame and remove page/loading shell duplication in `frontend/src/app/assistant/layout.tsx`, `frontend/src/app/assistant/loading.tsx`, and assistant route clients
- [x] T027 [US1] Make the teacher shell the sole persistent frame and remove page/loading shell duplication in `frontend/src/app/teacher/layout.tsx`, `frontend/src/app/teacher/loading.tsx`, and teacher route clients
- [x] T028 [US1] Make the admin shell the sole persistent frame and remove page/loading shell duplication in `frontend/src/app/admin/layout.tsx`, `frontend/src/app/admin/loading.tsx`, and admin route clients
- [x] T029 [P] [US1] Implement bounded per-surface navigation state and history-scroll restoration in `frontend/src/lib/navigation-state.ts` and shell components
- [x] T030 [P] [US1] Implement skip navigation and post-route focus behavior in `frontend/src/components/navigation/NavigationFocusManager.tsx` and surface layouts
- [x] T031 [P] [US1] Implement validated same-origin `returnUrl` handling in `frontend/src/lib/safe-return-url.ts`, `frontend/src/components/forms/LoginForm.tsx`, and auth redirects
- [x] T032 [US1] Implement connection/permission-aware `IntentLink` and apply it to primary shell links in `frontend/src/components/navigation/IntentLink.tsx` and the four shell components
- [x] T033 [US1] Remove blanket `prefetch={false}` from eligible primary routes while preserving heavy-route intent rules in surface navigation components
- [ ] T034 [US1] Re-run all role navigation tests, frontend build, Docker surface smoke, and document results/manual QA in `specs/167-platform-speed-completion/reports/phase3-us1-navigation.md`

**Checkpoint**: US1 passes independently on student, assistant, teacher, and
admin surfaces without permission regression.

## Phase 4: User Story 2 — Responsive Entry and Student Experience (Priority: P1)

**Goal**: Login, registration, and student entry become lighter and responsive
without hidden-priority assets or continuous expensive effects.

**Independent Test**: Use entry/student flows on representative mobile and
desktop profiles and verify input responsiveness, one theme asset, reduced
motion, no duplicated shell, and route resource targets.

### Tests for User Story 2

- [x] T035 [P] [US2] Add registration constrained-device, typing, hidden-tab, and reduced-motion tests in `frontend/tests/e2e/registration-performance.spec.ts`
- [x] T036 [P] [US2] Add single-logo, theme, mobile hero request, and image-priority tests in `frontend/tests/e2e/entry-assets.spec.ts`
- [x] T037 [P] [US2] Add login-to-dashboard transition and duplicate-shell/request tests in `frontend/tests/e2e/login-dashboard-performance.spec.ts`
- [x] T038 [P] [US2] Add effective compressed route budget tests for login/register/student in `frontend/scripts/check-route-performance-budgets.test.mjs`

### Implementation for User Story 2

- [x] T039 [US2] Render the CSS entry background by default and defer optional WebGL eligibility in `frontend/src/app/(public)/register/RegisterPageClient.tsx` and `frontend/src/app/(public)/auth.css`
- [x] T040 [US2] Add reduced-motion/save-data/device/visibility/input gates in `frontend/src/hooks/useConstrainedMotion.ts`
- [x] T041 [US2] Add an explicit active lifecycle that stops RAF and releases resources in `frontend/src/components/ui/ripple-grid.tsx`
- [x] T042 [P] [US2] Lazy-split registration carousel, optional modal, and below-fold fields in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T043 [US2] Migrate student dashboard/packages/teachers reads to canonical queries in `frontend/src/app/student/StudentDashboardClient.tsx`, `frontend/src/app/student/packages/PackagesPageClient.tsx`, and `frontend/src/app/student/teachers/StudentTeachersPageClient.tsx`
- [x] T044 [US2] Forward `AbortSignal` and normalize cancellation without toasts in `frontend/src/services/api-client.ts`, `frontend/src/services/student-service.ts`, and `frontend/src/services/content-service.ts`
- [x] T045 [US2] Map student platform/realtime events to narrow query invalidation in `frontend/src/lib/realtime-invalidation-map.ts` and `frontend/src/hooks/usePlatformEvents.ts`
- [x] T046 [P] [US2] Render exactly one active theme logo and remove duplicate priority behavior in `frontend/src/components/shared/PlatformLogo.tsx` and `frontend/src/components/ui/resizable-navbar.tsx`
- [x] T047 [P] [US2] Make hero media responsive without loading hidden mobile assets in `frontend/src/components/landing/HeroSection.tsx`
- [x] T048 [P] [US2] Remove unused root Montserrat loading and reduce verified Tajawal weights in `frontend/src/app/layout.tsx`
- [x] T049 [P] [US2] Narrow broad Zustand auth subscriptions with selectors in `frontend/src/components/layout/GlobalNav.tsx`, `frontend/src/hooks/useHasPermission.ts`, and realtime hooks
- [x] T050 [US2] Implement compressed initial/shared/deferred route budgets in `frontend/scripts/check-route-performance-budgets.mjs` and package scripts
- [ ] T051 [US2] Run mobile/desktop Playwright performance, build/resource, Docker entry smoke, and document results/manual QA in `specs/167-platform-speed-completion/reports/phase4-us2-entry-student.md`

**Checkpoint**: US2 meets immediate route/input gates; long-window RUM remains
sample-qualified but does not block the next phase.

## Phase 5: User Story 3 — Efficient Data-Heavy Workflows (Priority: P1)

**Goal**: Large searches and live-support/backend work have bounded requests,
payloads, datastore commands, and durable event lock time.

**Independent Test**: Run student search and live-support dashboard/history at
representative volumes and verify bounded pagination, cancellation, fixed query
count, immediate session revocation, and recoverable outbox delivery.

### Tests for User Story 3

- [x] T052 [P] [US3] Add ListUsers page-size clamp, stable ordering, filters, and payload integration tests in `backend/tests/NaderGorge.Integration.Tests/Admin/ListUsersPaginationTests.cs`
- [x] T053 [P] [US3] Add rapid admin search cancellation/retained-page E2E tests in `frontend/tests/e2e/admin-student-search-performance.spec.ts`
- [x] T054 [P] [US3] Add live-support fixed command-count tests for 1, 20, and 100 rows in `backend/tests/NaderGorge.Integration.Tests/LiveSupport/LiveSupportQueryBudgetTests.cs`
- [x] T055 [P] [US3] Add security-cache hit/miss/outage and immediate revocation integration tests in `backend/tests/NaderGorge.Integration.Tests/Auth/SecurityStateCacheTests.cs`
- [ ] T056 [P] [US3] Add two-node outbox claim, lease expiry, crash, retry, idempotency, and dead-letter tests in `backend/tests/NaderGorge.Integration.Tests/Realtime/OutboxLeaseTests.cs`
- [x] T057 [P] [US3] Add old/new application compatibility migration tests in `backend/tests/NaderGorge.Integration.Tests/Migrations/PerformanceSchemaCompatibilityTests.cs`

### Implementation for User Story 3

- [x] T058 [US3] Clamp and validate ListUsers pagination with deterministic ordering in `backend/src/NaderGorge.Application/Features/Admin/Queries/ListUsersQuery.cs`
- [x] T059 [US3] Convert admin student search to 25–50 row server pages, 300ms debounce, cancellation, and previous data in `frontend/src/app/admin/students/AdminStudentsPageClient.tsx`
- [x] T060 [US3] Separate interactive list paging from bulk export in `frontend/src/services/admin-service.ts` and the matching admin API application contract
- [x] T061 [US3] Refactor live-support dashboard/history/timeline/user/rating/count lookups into bounded projections in `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs`
- [x] T062 [P] [US3] Add datastore command-count instrumentation for tests/metrics in `backend/src/NaderGorge.Infrastructure/Observability/DbCommandMetricsInterceptor.cs`
- [x] T063 [P] [US3] Define minimal user security-state cache interfaces and DTOs in `backend/src/NaderGorge.Application/Interfaces/IUserSecurityStateCache.cs`
- [x] T064 [US3] Implement shared Redis security-state cache with DB fallback in `backend/src/NaderGorge.Infrastructure/Cache/RedisUserSecurityStateCache.cs`
- [x] T065 [US3] Replace per-request auth database reads with the cache contract in `backend/src/NaderGorge.API/Program.cs`
- [x] T066 [US3] Invalidate security-state keys on user disable/delete/password/role/permission/security-version mutations across `backend/src/NaderGorge.Application/Features/` and `backend/src/NaderGorge.API/Controllers/`
- [x] T067 [P] [US3] Add additive outbox claim/lease/next-attempt fields and indexes in `backend/src/NaderGorge.Domain/Entities/OutboxEvent.cs`, `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`, and a new EF migration
- [x] T068 [US3] Refactor outbox processing into short claim, external dispatch, and conditional acknowledgement in `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs`
- [x] T069 [US3] Preserve stable event IDs and targeted frontend invalidation through outbox/realtime mappings in backend dispatchers and `frontend/src/lib/realtime-invalidation-map.ts`
- [ ] T070 [US3] Run backend/front-end focused tests, migration compatibility, Docker/realtime smoke, and document results/manual QA in `specs/167-platform-speed-completion/reports/phase5-us3-data.md`

**Checkpoint**: US3 passes independently under representative volume,
concurrency, failure, and authorization changes.

## Phase 6: User Story 4 — Stable, Accessible Screens and Motion (Priority: P2)

**Goal**: Keyboard, assistive-technology, mobile, zoomed, and motion-sensitive
users can complete critical flows with truthful controls and safe state feedback.

**Independent Test**: Run keyboard/axe/mobile/200%-zoom/reduced-motion journeys
through drawers, dialogs, carousels, loading, empty, error, and retry states.

### Tests for User Story 4

- [x] T071 [P] [US4] Add axe-based critical route matrix in `frontend/tests/e2e/platform-accessibility.spec.ts`
- [x] T072 [P] [US4] Add drawer/dialog focus containment, Escape, inert background, and trigger restore tests in `frontend/tests/e2e/accessible-overlays.spec.ts`
- [x] T073 [P] [US4] Add carousel pause/keyboard/real-controls/reduced-motion tests in `frontend/tests/e2e/accessible-carousels.spec.ts`
- [x] T074 [P] [US4] Add safe error, region loading/status, 320px, 200% zoom, long Arabic, and theme tests in `frontend/tests/e2e/resilient-ui-states.spec.ts`

### Implementation for User Story 4

- [x] T075 [US4] Add one shared accessible drawer/dialog primitive and migrate mobile surface drawers in `frontend/src/components/ui/AccessibleOverlay.tsx` and shell components
- [x] T076 [US4] Add global/user reduced-motion policy and replace layout/blur-heavy motion in `frontend/src/app/providers.tsx`, `frontend/src/lib/motion.ts`, and animated navigation components
- [x] T077 [US4] Implement pause/focus/visibility/keyboard/current-item behavior in `frontend/src/components/landing/CircularGallerySection.tsx` and other automatic carousels
- [x] T078 [US4] Wire real previous/next behavior or remove false controls in `frontend/src/components/landing/TestimonialsSection.tsx` and the registration carousel component
- [x] T079 [US4] Limit mobile bottom navigation to 4–5 primary destinations with current-page semantics in `frontend/src/components/layout/StudentBottomNav.tsx` and other surface mobile navigation
- [x] T080 [P] [US4] Add region-level accessible loading/empty/error/retry primitives in `frontend/src/components/ui/AsyncRegionState.tsx`
- [x] T081 [US4] Migrate representative admin/student/assistant/teacher loading and error boundaries to safe region primitives without exposing `error.message`
- [x] T082 [P] [US4] Enforce no-new raw design-token violations in `frontend/scripts/check-design-tokens.mjs`
- [x] T083 [US4] Replace regex-only accessibility coverage with the browser matrix while retaining static contract checks in `frontend/scripts/check-accessibility.mjs`
- [ ] T084 [US4] Run accessibility/browser/Docker surface gates and document role-by-role manual QA in `specs/167-platform-speed-completion/reports/phase6-us4-accessibility.md`

**Checkpoint**: US4 has zero critical automated violations and all manual
keyboard journeys are completable.

## Phase 7: User Story 5 — Trustworthy Performance Evidence (Priority: P1)

**Goal**: Route/device/surface/connection metrics correlate safely with server,
database, outbox, node, and release work; budgets block bad candidates.

**Independent Test**: Produce a privacy-safe before/after evidence set for core
routes and authenticated/load/realtime workflows with sample counts and no
secret/private payloads.

### Tests for User Story 5

- [x] T085 [P] [US5] Add Web Vitals schema, privacy-sentinel, normalization, and rate-limit tests in `backend/tests/NaderGorge.Application.Tests/Metrics/WebVitalsContractTests.cs`
- [x] T086 [P] [US5] Add route/surface/device/connection/release browser payload tests in `frontend/src/hooks/useWebVitalsReporter.test.mts`
- [x] T087 [P] [US5] Add correlation propagation and no-sensitive-log integration tests in `backend/tests/NaderGorge.Integration.Tests/Observability/PerformanceCorrelationTests.cs`
- [x] T088 [P] [US5] Add production cache-header contract tests in `deploy/production/tests/test_static_cache_contract.py`
- [x] T089 [P] [US5] Add authenticated login/student/packages/admin-search/live-support/reconnect load journeys in `deploy/production/load/platform-workflows.js`

### Implementation for User Story 5

- [x] T090 [US5] Extend `WebVitalsMetric` with low-cardinality dimensions and an additive migration in `backend/src/NaderGorge.Domain/Entities/WebVitalsMetric.cs`, `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`, and migrations
- [x] T091 [US5] Validate privacy-safe ingest and authorized percentile/sample-count summary in `backend/src/NaderGorge.Application/Features/Metrics` and `backend/src/NaderGorge.API/Controllers/WebVitalsController.cs`
- [x] T092 [US5] Decouple sampled RUM from authentication bootstrap and report normalized dimensions in `frontend/src/hooks/useWebVitalsReporter.ts` and the root provider island
- [x] T093 [US5] Propagate safe correlation and record normalized route/node/release/query timing in `backend/src/NaderGorge.API/Middleware/` and `backend/src/NaderGorge.Infrastructure/Observability/`
- [x] T094 [P] [US5] Add outbox claim/dispatch/retry/dead-letter metrics without payload content in `backend/src/NaderGorge.Application/Features/Realtime/Services/RealtimeTelemetry.cs`
- [x] T095 [US5] Add immutable caching for `/_next/static` and bounded/revalidated caching for mutable public assets in `deploy/production/config/nginx/massar-node.conf.template`
- [x] T096 [US5] Enforce route resource/request/navigation/query-count budgets in `Makefile`, frontend scripts, backend tests, and production verification scripts
- [x] T097 [US5] Require real workflow probes in the production performance matrix in `deploy/production/config/performance-matrix.json`
- [ ] T098 [US5] Generate immediate before/after route, API, query, load, and cache-header evidence in `specs/167-platform-speed-completion/reports/phase7-us5-performance-evidence.md`
- [ ] T099 [US5] Run frontend/backend/worker/load/Docker gates and document RUM sample counts as observational or qualified without delaying release in `specs/167-platform-speed-completion/reports/phase7-us5-verification.md`

**Checkpoint**: US5 immediate gates pass and all performance claims link to
reproducible evidence.

## Phase 8: User Story 6 — Zero-Downtime Complete Release (Priority: P1)

**Goal**: Every workspace change ships in one exact verified release across
three nodes with zero planned downtime and app-only full-advanced-set rollback.

**Independent Test**: Seal the whole workspace, prove image/source parity, test
post-seal invalidation and multi-node rollback, then progressively deploy
node-3 → node-2 → node-1 with all cluster acceptance gates.

### Tests for User Story 6

- [x] T100 [P] [US6] Add full-workspace tracked/untracked source digest and post-seal delta tests in `deploy/production/tests/test_release_images.py`
- [x] T101 [P] [US6] Add release manifest v2 completeness, path classification, secret blocking, and artifact parity tests in `deploy/production/tests/test_release_contract.py`
- [x] T102 [P] [US6] Add node-2/node-1 failure tests that roll back every advanced app node in reverse order in `deploy/production/tests/test_deploy_release.py`
- [x] T103 [P] [US6] Assert automatic rollback never runs database down/restore and verifies prior app against the retained schema in `deploy/production/tests/test_rollback_release.py`
- [x] T104 [P] [US6] Add empty/production-like/N-1 migration audit tests for every current migration and model snapshot in `backend/tests/NaderGorge.Integration.Tests/Migrations/`

### Implementation for User Story 6

- [x] T105 [US6] Expand release provenance and source snapshots from application subdirectories to the complete releasable workspace in `deploy/production/scripts/release_images.py` and `deploy/production/scripts/source_manifest.py`
- [x] T106 [US6] Version the release manifest with complete source/path/evidence/migration compatibility fields in `deploy/production/scripts/release_contract.py`
- [x] T107 [US6] Invalidate and refuse artifact reuse after any post-seal workspace delta in `deploy/production/scripts/clusterctl.py`
- [x] T108 [US6] Track all advanced nodes and implement automatic reverse application rollback on critical failure in `deploy/production/scripts/deploy_release.py`
- [x] T109 [US6] Enforce retained-schema app-only rollback and forward-fix evidence in `deploy/production/scripts/rollback_release.py` and `deploy/production/scripts/migrate_release.py`
- [x] T110 [US6] Reconcile and verify all modified/untracked migrations, snapshot, migrator, bootstrap, cluster, shared-file, Docker, and release changes across `backend/`, `docker-compose.yml`, and `deploy/production/`
- [ ] T111 [US6] Re-inventory and review/fix every frontend, backend, worker, test, documentation, skill, specification, and artifact change in `artifacts/performance-167/final/workspace-manifest.json`
- [x] T112 [US6] Run clean-code, test, documentation, accessibility, performance, migration, and production-tool reviews and record them in `specs/167-platform-speed-completion/reports/phase8-review-gates.md`
- [ ] T113 [US6] Run locally available checks without downloads, then run `make verify`, full frontend/E2E, backend, worker, Python, and Docker gates on the reviewed remote builder with evidence in `artifacts/releases/<release-id>/verification.json`
- [ ] T114 [US6] Seal the final complete source; rebuild all four images once; rerun every gate if the source changes; write candidate evidence in `artifacts/releases/<release-id>/`
- [ ] T115 [US6] Use the reviewed `ssh-server` preflight to verify all nodes, quorum, clocks, capacity, current release, secrets, backup, and restore readiness in `artifacts/releases/<release-id>/preflight/`
- [ ] T116 [US6] Distribute and verify identical backend/frontend/worker/migrator digests on all nodes and record `artifacts/releases/<release-id>/distribution.json`
- [ ] T117 [US6] Apply the forward-compatible migration set once under serialization and record `artifacts/releases/<release-id>/migration-evidence.json`
- [ ] T118 [US6] Drain/deploy/health/smoke/undrain node-3 and record or reverse-roll back through `artifacts/releases/<release-id>/nodes/node-3.json`
- [ ] T119 [US6] Drain/deploy/health/smoke/undrain node-2 and record or reverse-roll back through `artifacts/releases/<release-id>/nodes/node-2.json`
- [ ] T120 [US6] Drain/deploy/health/smoke/undrain node-1 and record or reverse-roll back through `artifacts/releases/<release-id>/nodes/node-1.json`
- [ ] T121 [US6] Verify cluster identity, traffic, workflows, SignalR, BullMQ, shared files, dependencies, and failure tolerance in `artifacts/releases/<release-id>/cluster-acceptance.json`
- [ ] T122 [US6] Publish the release GO/NO-GO, per-node timestamps, complete source manifest, artifact/migration/test evidence, RUM sample status, and rollback outcome in `specs/167-platform-speed-completion/reports/phase8-us6-production.md`

**Checkpoint**: US6 is complete only after production convergence and evidence;
a new workspace delta returns T111–T122 to incomplete.

## Phase 9: Polish, Deep Review, and Final Verification

**Purpose**: Cross-story cleanup and the mandatory Speckit-All review sequence.

- [x] T123 [P] Run independent architecture and backend concurrency/security review and record findings in `specs/167-platform-speed-completion/reports/phase9-architecture-review.md`
- [x] T124 [P] Run independent UI/UX critique across all roles, themes, breakpoints, reduced motion, and accessibility in `specs/167-platform-speed-completion/reports/phase9-ui-ux-review.md`
- [x] T125 Fix every P0/P1/P2 review finding across `frontend/`, `backend/`, `worker/`, and `deploy/production/` and add regression coverage
- [x] T126 Run `clean-code-guard` on all changed production code and fix every actionable finding
- [x] T127 Run `test-guard` on all changed tests and fix every actionable finding
- [x] T128 Run `docs-guard` on all changed documentation and fix every actionable finding
- [ ] T129 Re-run all feature tests, full inventory, secret scan, source digest, build/Docker/load gates, and production health into `artifacts/performance-167/final/verification.json`
- [ ] T130 Update `achievements.md`, all task checkboxes, and the final implementation/test/deployment summary with exact evidence paths

## Dependencies & Execution Order

### Phase dependencies

- Phase 1 has no dependency.
- Phase 2 depends on the sealed baseline and blocks all user stories.
- US1 precedes query migration so shell remounts do not obscure duplicate-read
  causes.
- US2 depends on the shared query provider and the student shell portion of US1.
- US3 can begin after Phase 2, but its frontend admin search integration depends
  on canonical query contracts.
- US4 can run after the shell/provider foundation; overlay migrations may run
  per surface in parallel.
- US5 depends on baseline IDs and benefits from US2/US3 final dimensions.
- US6 depends on all stories and every review/build/test gate.
- Phase 9 review fixes invalidate any previously sealed candidate and therefore
  must run before the final production seal, or force the entire US6 seal/gate
  sequence to repeat.

### Parallel opportunities

- Baseline resource, browser, backend, and RUM evidence tasks T003–T006.
- Shared query, route policy, budgets, and tests T010–T012/T015–T016.
- US1 test tasks T019–T022 and surface shell migrations T025–T028 when file
  ownership is separated.
- US2 tests and asset/font/Zustand work T035–T038/T046–T049.
- US3 backend tests T052–T057 and independent interface/schema work.
- US4 test matrix T071–T074 and primitives/token tooling.
- US5 tests T085–T089 and independent telemetry/cache/load work.
- US6 production-tool tests T100–T104 before their owning implementations.

## Parallel execution examples

### US1

```text
Agent A: T025 student shell
Agent B: T026 assistant shell
Agent C: T027 teacher shell
Then: T028 admin shell and T032–T034 integration
```

### US3

```text
Agent A: T052/T058/T059 admin search
Agent B: T054/T061/T062 live-support queries
Agent C: T055/T063–T066 security-state cache
Then: T056/T067–T069 outbox integration
```

### US6

```text
Agent A: T100/T101/T105–T107 source and manifest
Agent B: T102/T103/T108–T109 rollback orchestration
Agent C: T104/T110 migration compatibility
Then: T111–T122 serialized qualification and rollout
```

## Implementation Strategy

1. Establish evidence and the moving-scope invalidation gate.
2. Deliver persistent navigation as the first independently testable user value.
3. Add query/data/payload improvements without mixing shell-remount causes.
4. Complete backend hot paths, security cache, and durable outbox separately.
5. Finish accessibility and observability, then qualify the entire workspace.
6. Seal/build once only after every review fix.
7. Roll through node-3 → node-2 → node-1; any failure rolls all advanced
   application nodes back in reverse order while retaining the compatible
   schema.

## Format Validation

- Every task uses `- [ ] T###`.
- User-story tasks use exactly one `[US#]` label.
- `[P]` appears only on tasks intended for independent files/work.
- Every task identifies an exact file or directory/evidence path.

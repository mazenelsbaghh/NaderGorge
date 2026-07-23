# Tasks: Frontend Design System Unification

## Preparation

- [ ] T001 [P] Create the production UI route inventory in `docs/design-system-route-inventory.md`, excluding `frontend/src/app/api/**` and non-user-facing internals.
- [ ] T002 [P] Add the exact raw-color exception schema in `frontend/config/design-color-allowlist.json`.

## Foundation

- [ ] T003 Define the complete light/dark semantic token types in `frontend/src/lib/design-tokens.ts`.
- [ ] T004 Migrate `frontend/src/app/globals.css` and theme bootstrap in `frontend/src/app/layout.tsx` to the semantic token contract without changing theme storage or selection.
- [ ] T005 Add `frontend/scripts/check-design-tokens.mjs` and an npm script that rejects raw color forms outside the allowlist.
- [ ] T006 Add token scanner fixtures and accessibility assertions under `frontend/tests/`.

## Shared primitives (US2)

- [ ] T007 Consolidate button, icon-button, field, select, surface, badge, alert, skeleton, empty-state, and table variants under `frontend/src/components/ui/`.
- [ ] T008 Consolidate `AccessibleDialog`, `AdminModal`, and direct dialog shells while preserving focus trap, inert, Escape, restore-focus, and reduced-motion behavior.
- [ ] T009 Migrate `AdminStatCard`, `AdminDataTable`, `AdminTabBar`, `AdminSearchToolbar`, and `AdminPageSkeleton` to shared semantic variants.

## Public and student (US1)

- [ ] T010 [P] Migrate public landing, teacher, package, FAQ, forms, and parent-report surfaces to semantic tokens.
- [ ] T011 [P] Migrate student dashboard, packages, lessons, exams, community, notifications, profile, balance, and shared packages to student/public primitives.
- [ ] T012 Extract and use shared student lesson, content thumbnail, status badge, and action-button patterns without changing flow or copy.

## Staff surfaces (US1)

- [ ] T013 [P] Migrate teacher route forms, finance, exams, packages, students, profile, chat, and activity surfaces.
- [ ] T014 [P] Migrate assistant dashboard, tasks, CRM, attendance, vacations, content, questions, notifications, and chat surfaces.
- [ ] T015 Migrate admin users, content, finance, reports, media, operations, gifts, codes, exams, and settings surfaces to shared primitives/tokens.

## Live support and governance (US1/US3)

- [ ] T016 Migrate live-support participant, staff, admin, AI, student-context, and shared state components without changing realtime services/effects.
- [ ] T017 Add route-by-role light/dark evidence and targeted Playwright theme/focus/empty/error/permission checks.
- [ ] T018 Run deep critique, clean-code-guard, and test-guard; fix all findings in `achievements.md` and this task file.
- [ ] T019 Run feature tests, frontend lint/typecheck/build, token/accessibility/contract checks, and Docker gates; record evidence in `achievements.md`.

Expected result: every task produces a verifiable file, check result, or route evidence entry; the final run passes all required gates.

## Dependencies

T001-T006 → T007-T009 → T010-T016 → T017-T019.

## Definition of Done

All tasks checked; no unapproved raw colors; WCAG 2.2 AA evidence recorded; existing behavior/copy/order preserved; all required gates pass or blockers are explicitly documented.

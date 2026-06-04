# Feature Specification: Product UX, Frontend Quality, and Polish

Feature ID: 077-product-ux-quality-polish  
Source audit: `docs/project-deep-audit-2026-06-04.md`  
Phase brief: `docs/audit-remediation-phase-3-product-ux-quality.md`  
Created: 2026-06-04

## Overview

This feature removes trust-breaking mock analytics, cleans frontend lint warnings, improves student and admin task flows, removes unfinished video player state, standardizes visible Arabic UX copy, and verifies responsive behavior on core pages.

The product is a controlled learning system. Student-facing screens must be mobile-first and answer one question immediately: "What do I need to finish now?" Admin-facing screens must show only trustworthy operational data and clear actions.

## Goals

- Remove production-visible mock/random analytics.
- Reduce frontend lint warnings to zero or a tightly documented near-zero set.
- Make the student shell task-oriented, not a copy of the admin shell.
- Polish admin screens so data is trustworthy, scannable, and not visually crowded.
- Remove unfinished SecureVideoPlayer state and controls.
- Keep heavy decorative components out of student-first routes unless lazy loaded and useful.
- Standardize user-visible Arabic copy and remove old brand drift.
- Keep generated build artifacts untracked.
- Verify mobile, tablet, and desktop behavior for core student/admin pages.

## Non-Goals

- Do not redesign the full product from scratch.
- Do not introduce a new design system or token vocabulary.
- Do not change backend APIs unless a frontend page already has a real data contract to use.
- Do not add fake analytics endpoints.
- Do not disable lint rules broadly.
- Do not claim video anti-download as absolute protection.

## Design Context Requirements

Apply the project design context from `PRODUCT.md`, `DESIGN.md`, and the `impeccable` product register:

- Arabic-first, RTL-first.
- Student priority is mobile, one-handed use, short focused sessions.
- Admin/assistant priority is desktop scanability.
- Use existing warm editorial surfaces and brand gold tokens.
- Prefer clarity, progress, and controlled pressure over decoration.
- Avoid generic dashboard visuals, nested cards, visual clutter, and unnecessary borders.
- Use familiar product UI patterns. Do not invent new affordances for standard actions.

## User Stories

### US1: Admin trusts analytics surfaces

As an admin, I want content detail pages to show only real analytics or a clear empty state so I do not make decisions from random or mock numbers.

Acceptance criteria:

- Refreshing a content detail page does not change analytics randomly.
- Production does not show mock analytics by default.
- If no analytics API/data exists, the UI shows an explicit empty state such as `لا توجد بيانات تحليلية بعد`.
- Mock analytics, if retained for local experiments, are behind `NEXT_PUBLIC_ENABLE_MOCK_ANALYTICS=true` and default to disabled.

### US2: Student sees the next task first

As a student, I want the student home and package/lesson surfaces to show the next useful action first so I can finish studying quickly.

Acceptance criteria:

- `/student` displays a clear primary next action in the first mobile viewport.
- Student navigation is limited and study-focused.
- Locked/unlocked states explain what is available and why without long text.
- Student shell does not feel like a compressed admin dashboard.

### US3: Admin screens are scannable and actionable

As an admin, I want operational screens to show meaningful statuses and primary actions clearly so I can manage users, AI processing, and content without decoding raw worker internals.

Acceptance criteria:

- AI monitor displays user-facing status labels: `في الانتظار`, `قيد المعالجة`, `اكتمل`, `فشل`.
- Dangerous admin actions such as cancel/retry are visually clear and confirmed where already supported by the page pattern.
- User detail and content pages do not introduce layout shifts from unstable/random values.

### US4: Frontend quality warnings do not hide real issues

As a developer, I want lint warnings reduced to zero or a documented near-zero set so new warnings are visible and actionable.

Acceptance criteria:

- `cd frontend && npm run lint` exits with 0 errors.
- Unused imports/variables are removed.
- Hook dependency warnings are fixed with `useCallback`, `useMemo`, stable refs, or safe dependency arrays.
- `<img>` warnings are resolved with `next/image` where appropriate, or documented if the source is intentionally not compatible.
- Any remaining warnings are listed in `achievements.md`, checked off only with a justified exception.

### US5: Video player has no unfinished visible state

As a student, I want video controls that are stable and understandable so studying is not interrupted by broken or unfinished controls.

Acceptance criteria:

- `SecureVideoPlayer` has no unused quality switching state or handlers.
- `onEnded` is either wired to the video element or removed from props.
- `PlayerControls` chapter memoization is stable.
- Player controls keep stable dimensions on mobile and desktop.
- UX copy avoids claiming absolute anti-download protection.

### US6: Heavy visual components do not slow core study routes

As a student on mobile, I want core study routes to stay fast and focused so decorative UI does not slow the first screen.

Acceptance criteria:

- Core student first screens do not import unused heavy decorative components.
- Unused generic UI components stay unimported or are removed only if clearly dead.
- Any heavy component used on a study route is justified by workflow value or lazy loaded.

### US7: Visible copy is consistent and brand-clean

As a student/admin, I want clear Arabic labels and status messages so the interface feels coherent and trustworthy.

Acceptance criteria:

- Visible frontend copy does not contain old brand strings such as `basma`, `bsma`, or `acadmy`.
- Technical worker/BullMQ terms are not exposed where a user-facing processing label is better.
- Error messages are short, actionable, and Arabic-first.

### US8: Generated artifacts remain outside Git

As a maintainer, I want build output and dependency folders to stay untracked so commits remain reviewable.

Acceptance criteria:

- `git ls-files | rg '(^|/)(node_modules|\\.next|dist|bin|obj)(/|$)'` returns no tracked generated artifacts.
- `.gitignore` covers common frontend, worker, and backend outputs.

### US9: Core pages are responsive

As a mobile-first student and desktop-first admin, I want core pages to avoid overflow, overlap, and unreachable controls.

Acceptance criteria:

- Mobile viewport `390x844`, tablet `768x1024`, and desktop `1440x900` have no horizontal overflow on key routes.
- Touch targets for primary student actions are at least approximately 44px high.
- Text does not overflow buttons/cards.
- Navigation does not cover core content.

## Functional Requirements

- FR-001: Search and remove or gate `mockStats`, `Math.random`, and hardcoded analytics in admin content surfaces.
- FR-002: Replace analytics cards with real props where available, otherwise render an empty state component.
- FR-003: Remove old brand text and hardcoded old domain fallbacks from user-visible frontend code.
- FR-004: Resolve all lint warnings that are caused by unused variables/imports.
- FR-005: Resolve hook dependency warnings without creating render loops.
- FR-006: Resolve `next/no-img-element` warnings using `next/image` where image dimensions and sources are compatible.
- FR-007: Remove or wire unused `SecureVideoPlayer` quality state, current quality state, `onEnded`, and related handlers.
- FR-008: Stabilize video chapter memoization in `PlayerControls`.
- FR-009: Verify generated build artifacts are not tracked.
- FR-010: Run frontend build and lint after implementation.

## Edge Cases

- If a page has no analytics API, do not fabricate numbers. Show an empty state.
- If a hook dependency fix causes repeated requests, refactor the loader into `useCallback`.
- If `next/image` cannot safely handle a dynamic URL, document the warning and do not suppress globally.
- If dev-only mock analytics are needed, they must be behind an explicit env flag with default false.
- If a student route requires real backend data and no local data exists, preserve loading and empty states without inventing backend data.

## Verification Commands

Required:

```bash
cd frontend && npm run build && npm run lint
git ls-files | rg '(^|/)(node_modules|\.next|dist|bin|obj)(/|$)'
```

If backend or worker files are touched:

```bash
dotnet test backend/NaderGorge.sln --no-restore
cd worker && npm run build
```

## Completion Definition

The phase is complete when:

- Production-visible mock analytics are gone or disabled by default.
- Frontend lint has 0 errors and zero or documented near-zero warnings.
- Student home/shell clearly prioritizes the next study action.
- Admin pages no longer show random metrics or raw processing internals.
- Video player warnings and unfinished state are cleaned.
- Core pages are checked for responsive overflow/overlap.
- All generated artifacts remain untracked.

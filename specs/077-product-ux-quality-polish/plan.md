# Implementation Plan: Product UX, Frontend Quality, and Polish

Feature ID: 077-product-ux-quality-polish  
Spec: `specs/077-product-ux-quality-polish/spec.md`  
Date: 2026-06-04

## Constitution Check

- Modular clean architecture: keep changes in frontend routes/components/services unless backend/worker is strictly needed.
- Security by default: do not suppress lint broadly and do not reintroduce raw secrets, raw worker internals, or unsafe HTML.
- Academic content integrity: do not fabricate academic or analytics data.
- Premium editorial design: preserve Arabic-first, RTL-first, warm editorial design tokens, mobile-first student focus, and admin scanability.
- Performance: avoid adding heavy components to student first screens.

## Technical Strategy

1. Start with trust and correctness:
   - Remove or disable mock/random analytics.
   - Replace non-real metrics with empty states.
   - Remove old brand strings and stale domain fallbacks.

2. Then reduce lint warnings:
   - Fix unused imports and variables first because these are low-risk.
   - Fix hook dependency warnings with `useCallback`/`useMemo` where necessary.
   - Convert compatible `<img>` usage to `next/image`.
   - Document only truly justified exceptions.

3. Then clean video UX:
   - Remove unused quality switching state if no real quality source list exists.
   - Wire or remove `onEnded`.
   - Stabilize `PlayerControls` memo dependencies.

4. Then apply UX polish:
   - Keep student pages task-oriented.
   - Keep admin pages dense but readable.
   - Avoid nested cards and excess border-driven separation.
   - Standardize user-facing Arabic labels for AI processing statuses.

5. Finish with verification:
   - Frontend build and lint.
   - Artifact tracking check.
   - Browser smoke checks where local pages are accessible.

## File-Level Plan

### Analytics and Admin Trust

- `frontend/src/components/admin/EntityOverviewDashboard.tsx`
  - Remove unused imports.
  - Ensure analytics stats come from props only.
  - Render explicit empty state when metrics are unavailable.

- `frontend/src/app/admin/content/packages/[id]/page.tsx`
- `frontend/src/app/admin/content/sections/[id]/page.tsx`
- `frontend/src/app/admin/content/lessons/[id]/page.tsx`
- `frontend/src/app/admin/content/terms/[id]/page.tsx`
  - Remove `mockStats` and any `Math.random` analytics.
  - Pass `undefined` or an empty stats list to the dashboard until real API data exists.
  - Preserve real content lists and management forms.

- `frontend/src/components/admin/AttachedExamViewer.tsx`
  - Remove `Math.random()`-based exam analytics.
  - Use known counts from props if present, otherwise empty state.

### Lint Quality

Fix warnings from the current `npm run lint` output:

- Remove unused imports/variables in public, admin, student, video, and shared UI files.
- Change unused caught errors to `catch { ... }` where the value is intentionally unused.
- Convert hook loaders to stable callbacks before adding them to dependency arrays.
- Use `next/image` for static image URLs where dimensions are known.
- Keep tests lint-clean by removing unused fixture arguments/values.

### Student UX

- `frontend/src/components/layout/StudentShellChrome.tsx`
  - Verify student navigation is study-focused and not admin-like.
  - Keep touch targets stable and mobile-friendly.

- `frontend/src/app/student/page.tsx`
  - Keep next action visible above secondary dashboard details.
  - Use concise Arabic copy.

- `frontend/src/app/student/packages/page.tsx`
- `frontend/src/app/student/mistakes/page.tsx`
  - Preserve clear empty states and locked/unlocked cues.

### Admin UX

- `frontend/src/app/admin/ai-monitor/page.tsx`
  - Remove unused code.
  - Convert raw processing states to user-facing Arabic labels where rendered.
  - Avoid exposing worker internals in visible copy.

- `frontend/src/app/admin/users/[id]/page.tsx`
  - Fix hook dependencies and unused errors.
  - Preserve admin actions.

### Video UX

- `frontend/src/components/video/SecureVideoPlayer.tsx`
  - Remove unused `qualityLevels`, `currentQuality`, `handleQualityChange`, `user`, and unused imports unless fully wired.
  - Wire `onEnded` to the media element if the prop is part of the public component contract.

- `frontend/src/components/video/PlayerControls.tsx`
  - Memoize `displayChapters` so the chapter list dependency is stable.

### Heavy UI Components and Artifacts

- Run import search for:
  - `circular-gallery`
  - `feature-carousel`
  - `ripple-grid`
  - `resizable-navbar`
- Do not add them to student first screens.
- Run tracked artifact search and update `.gitignore` only if needed.

## Data and API Decisions

- No new backend API is required.
- No mock analytics endpoint will be added.
- Existing frontend services remain the source of truth.
- Dev-only mock analytics, if retained anywhere, must require `NEXT_PUBLIC_ENABLE_MOCK_ANALYTICS=true`.

## UX Decisions

- Physical scene: a student opens the platform on a phone between study sessions and wants one clear action without browsing.
- Theme: use the existing warm editorial light/dark tokens. Do not add a new palette.
- Product register: familiar task UI wins over novelty.
- Copy: Arabic-first, direct, short.

## Verification Plan

Required:

```bash
cd frontend && npm run build && npm run lint
git ls-files | rg '(^|/)(node_modules|\.next|dist|bin|obj)(/|$)'
rg -n "mockStats|Math\\.random|basma|bsma|acadmy" frontend/src || true
```

Browser checks:

- Start the frontend dev server if no suitable server is already running.
- Check at least `/student`, `/student/packages`, `/student/mistakes`, `/admin/ai-monitor`, and one admin detail route that can render without special seeded IDs.
- Use mobile and desktop viewport checks where possible.

Conditional:

```bash
dotnet test backend/NaderGorge.sln --no-restore
cd worker && npm run build
```

Run conditional commands only if backend or worker files are modified during this phase.

## Risk Mitigation

- Lint hook fixes can create render loops. Convert async loaders to `useCallback` first.
- `next/image` may require width/height. Use explicit dimensions and preserve styling.
- Removing mock stats can make pages look emptier. Use intentional empty states rather than fake numbers.
- Avoid broad visual refactors that obscure the functional cleanup.

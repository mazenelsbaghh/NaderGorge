# Phase 4 — Entry and Student Performance Evidence

Date: 2026-07-29  
Status: **LOCAL CONTRACT PASS / REMOTE RUNTIME GATES PENDING**

## Before-state findings addressed

- Registration no longer starts a continuous WebGL loop by default.
- Optional WebGL is loaded after an idle/input quiet period only when the page
  is visible, reduced motion and Save-Data are off, the connection is not 2G,
  and the device meets bounded memory/CPU eligibility.
- Hiding the page or losing eligibility unmounts the effect, cancels RAF, and
  releases observers, handlers, canvas, and WebGL context.
- Registration carousel, academic fields, and instructions modal are separate
  deferred chunks; the modal is not rendered until opened.
- Student dashboard, packages, and teachers use identity-scoped canonical query
  keys with in-flight deduplication, retained data, cancellation, and narrow
  realtime invalidation.
- Public navigation renders one active logo asset per theme.
- Mobile does not request desktop hero artwork; desktop selects the current
  theme asset through CSS without hydration-dependent source swapping.
- Unused root Montserrat was removed. All five Tajawal weights remain because
  current UI utilities use 400, 500, 700, 800, and 900; removing a used weight
  would create synthetic font rendering and is not a verified optimization.
- Broad auth-store subscriptions in public navigation, permission checks, and
  realtime hooks were replaced with narrow selectors.

## Local no-download verification

| Gate | Result |
|---|---|
| Next route type generation | PASS |
| TypeScript strict check | PASS |
| Focused ESLint | PASS |
| Query client/cancellation/realtime contracts | PASS |
| Brotli route-budget unit contracts | PASS — 4/4 |
| Entry/student browser test discovery | PASS |

No package, browser, SDK, or container image was downloaded.

## Required remote gates before release

1. Execute registration constrained-device, hidden-tab, reduced-motion, and
   typing responsiveness tests in installed Chromium/WebKit.
2. Execute single-logo, active-theme hero, mobile no-request, and
   login-to-dashboard duplicate-request tests.
3. Produce fresh compressed initial/shared/deferred artifacts from the sealed
   production build and run the fail-closed route budget CLI.
4. Run public/student Docker surface smoke and manual Android/desktop checks.

T051 remains open until these remote artifacts pass. RUM after release remains
sample-qualified and observational; immediate build/browser gates are blocking.

## Production addendum

The exact production image rendered the student login and four-step
registration UI with labelled fields, controls, stages, and the constrained
student birth-date maximum. Browser navigation and rendering completed without
application console errors. The remote build passed TypeScript, ESLint, route
budgets, and image health. T051 remains open because the full mobile/desktop
Chromium/WebKit performance matrix was not run.

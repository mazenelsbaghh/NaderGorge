# Phase 6 / US4 — Accessibility and resilient UI evidence

Status: **PARTIAL LOCAL PASS — REMOTE BROWSER AND DOCKER GATES PENDING**

Date: 2026-07-29  
Candidate state: unsealed working tree; this is implementation evidence, not
production acceptance evidence.

## Delivered

- One portal-based accessible overlay primitive now owns focus containment,
  Escape handling, inactive background, scroll lock, labelling, and trigger
  restoration for student, teacher, assistant, and admin mobile drawers.
- Reduced-motion policy is global and shared. Navigation no longer relies on
  width or backdrop-filter layout animation, and expensive motion pauses when
  the document/user policy requires it.
- Automatic galleries and carousels pause on hover, focus, hidden documents,
  and reduced motion; expose current-item semantics; support keyboard
  navigation; and use real previous/next controls.
- Mobile primary navigation is bounded to at most five destinations and exposes
  `aria-current`.
- Shared loading/empty/error/retry regions are used by representative admin,
  student, assistant, and teacher boundaries. Error boundaries do not render
  `error.message`.
- The design-token gate rejects raw colors introduced by tracked changes and
  untracked source files. A single WebGL uniform has a local, documented
  concrete-color exception.
- The former regex-only accessibility script now retains static contracts and
  also requires the four-file Playwright browser matrix.

## Local checks (no downloads)

| Gate | Result | Evidence |
|---|---:|---|
| TypeScript `tsc --noEmit` | PASS | Existing `node_modules` only |
| Focused ESLint | PASS | Overlay, async states, galleries, mobile navigation |
| Carousel/mobile-nav static contract | PASS | `check-carousel-navigation-accessibility.mjs` |
| Accessibility static + matrix-definition contract | PASS | `check-accessibility.mjs` |
| No-new raw design tokens | PASS | `check-design-tokens.mjs --check` |
| Browser test discovery | PASS | 22 Chromium/WebKit cases in four specs |
| `git diff --check` | PASS | No whitespace errors in reviewed changes |

No package, browser, image, SDK, or runtime was downloaded or installed.

## Browser matrix awaiting the remote builder

The following are release-blocking immediate gates and have not been called
successful locally:

1. Axe critical-route scans for landing, login, registration, student,
   assistant, and admin.
2. Drawer focus trap, Escape, inactive background, and trigger restoration.
3. Carousel previous/next, keyboard, pause, visibility, and reduced-motion
   behavior.
4. Safe error and labelled loading regions.
5. 320 px viewport, 200% zoom, long Arabic feedback, light/dark themes, and
   Chromium/WebKit execution.
6. Production Docker surface smoke with the exact sealed images.

Run remotely:

```bash
npm run check:accessibility:browser
```

The remote workflow must use the reviewed builder environment and must not
install anything on the user's workstation.

## Role-by-role manual QA checklist

| Surface | Keyboard/focus | Mobile/zoom | Loading/error | Motion |
|---|---|---|---|---|
| Public/auth | Skip/focus order and real carousel controls | 320 px, 200%, long Arabic | Generic retry with no internal detail | Pause and reduced motion |
| Student | Drawer trap/restore and current bottom item | Five-or-fewer primary items | Labelled dashboard/package states | Shell and gallery policy |
| Teacher | Drawer trap/restore and current destination | No horizontal content loss | Safe teacher boundary | Carousel pause/keyboard |
| Assistant | Drawer trap/restore and current destination | Touch targets and long labels | Safe assistant boundary | No layout-heavy transition |
| Admin | Drawer trap/restore and current destination | Dense tables remain operable | Retained data plus safe retry | No layout-heavy transition |
| Live support | Dialog semantics and focus return | Composer remains usable | No prompt/private error leakage | Status changes remain perceivable |

## Release decision

Phase 6 implementation and local static gates pass. T084 remains open until the
remote browser matrix and exact-image Docker smoke pass and the role checklist
is attached to the sealed release evidence.

## Production addendum

The exact production image passed the deployment surface smoke. A read-only
browser smoke confirmed semantic headings, regions, labelled form controls,
real carousel controls, current-item announcements, the student login route,
and the multi-step registration route. No application console errors were
observed. T084 remains open because axe, 320 px, 200% zoom, reduced-motion,
focus-trap, and full Chromium/WebKit role coverage were not executed.

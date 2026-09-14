# Phase 3 — Persistent Navigation Evidence

Date: 2026-07-29  
Status: **LOCAL PASS / REMOTE RUNTIME GATES PENDING**

## Implemented

- Removed the root `template.tsx` remount boundary.
- Moved public navigation and participant live-support ownership into the
  `(public)` route group, so protected surfaces no longer load public chrome.
- Made student, assistant, teacher, and admin layouts the sole owners of their
  persistent shells.
- Replaced page-owned shell wrappers with descriptor regions that preserve
  titles, actions, accessories, sub-navigation, and floating actions.
- Added bounded per-surface navigation/scroll state, skip navigation,
  post-route focus, safe return URLs, and permission/connection-aware intent
  prefetch.
- Kept authorization display and direct-route enforcement on one canonical
  route policy.

## Local no-download verification

| Gate | Result |
|---|---|
| Next route type generation | PASS |
| TypeScript strict check | PASS |
| Focused ESLint for four surfaces/navigation | PASS |
| Query client contracts | PASS |
| Admin route permission contracts | PASS — 24 routes |
| Pure navigation contracts | PASS — 9/9 |
| Browser test discovery | PASS — 10 role/shell tests across Chromium/WebKit |

No package, browser, SDK, or container image was downloaded.

## Required remote gates before release

The following remain blocking and must run on the reviewed remote builder,
because the required browser/runtime/container materials are not installed
locally and the user prohibited local downloads:

1. Run the persistent-shell and negative role-route tests in Chromium and
   WebKit against the E2E backend.
2. Run the complete frontend production build and compressed route budgets.
3. Run the four protected Docker surface smokes and public route smoke.
4. Manually verify keyboard focus, back/forward scroll, mobile drawer state,
   and logout/login identity-boundary cache clearing for every role.

T034 remains open until those remote artifacts pass. A source change after
candidate sealing invalidates this evidence and requires a fresh run.

## Production addendum

The progressively deployed production candidate passed exact-image surface
health on all three nodes. A read-only browser smoke also verified the public
landing page, the landing-to-student-login transition, and the
login-to-registration transition. The teacher carousel changed its announced
current item through its real next control and exposed labelled previous/next
controls. No application console error was observed during these journeys.

T034 remains open because the complete authenticated four-role back/forward,
focus, scroll, and permission matrix was not executed with disposable role
accounts.

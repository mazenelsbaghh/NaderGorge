# Phase 9 — Independent UI/UX Review

Date: 2026-07-29  
Scope: T124 only  
Status: **STATIC FIXES COMPLETE / REMOTE BROWSER MATRIX PENDING**

## Review boundary

This is an independent code/static-evidence review of the public, student,
teacher, assistant/staff, and admin surfaces. It covers RTL, light/dark themes,
320 px and desktop layouts, 200% zoom/reflow, reduced motion, drawers,
carousels, loading/error states, and navigation.

The initial review was read-only. The dispositions below were added after the
separate T125 remediation changed production code and static contracts. No
browser, package, image, SDK, or tool was downloaded or installed. Browser
behavior and computed rendered contrast are not claimed as passing:
Chromium/WebKit execution on the reviewed remote builder remains a blocking
follow-up.

Design intent came from `.impeccable.md`: Arabic RTL users, a clear and
dependable operational tone, restrained Massar navy tokens, explicit state,
progressive disclosure, and keyboard/responsive support.

The `Evidence` line references below identify the initial pre-remediation
working tree and can shift after the recorded fix. Each `Disposition` describes
the current static implementation and its pending remote confirmation.

## Executive summary

- Original audit health: **12/20 — Acceptable; significant remediation required**
- Original Nielsen design health: **25/40 — Acceptable**
- Original findings: **P0 0 / P1 5 / P2 4 / P3 1**
- Current disposition: **all 10 findings fixed by static contract; all 10 still
  require remote browser confirmation**
- Original cognitive-load checklist: **2/8 failures (moderate)**. Grouping and
  search reduced the burden, but admin/assistant navigation presented many
  destinations and the student desktop rail hid labels until pointer hover.
  The rail issue is fixed statically; browser confirmation remains pending.
- Original anti-pattern verdict: **Fail, but not an “AI slop gallery.”** The operational
  shells are coherent; the student lesson/carousel layer drifts toward generic
  rounded glass cards, gradients, large shadows, hover glow, and repeated
  motion. The cited lesson surface is now flattened by the recorded
  remediation; the broader visual verdict remains a pre-remediation baseline.

No P0 blocker is proven by static evidence. The five P1 issues are release
risks because they can make navigation or critical operations inaccessible to
keyboard, screen-reader, low-vision, or dark-theme users.

## Original audit health score

| Dimension | Score | Key finding |
|---|---:|---|
| Accessibility | 2/4 | Legacy dialogs and a click-only proof image bypass the shared accessible primitives |
| Performance | 3/4 | Persistent shells and bounded async states are strong; decorative blur/motion remains widespread |
| Responsive design | 2/4 | Core shells reflow, but the student drawer has no bounded vertical scroll path |
| Theming | 2/4 | Main shells use tokens; staff live-support remains a hard-coded light island |
| Anti-patterns | 3/4 | Mostly coherent, with localized glass/gradient/card excess |
| **Total** | **12/20** | **Acceptable** |

## Original Nielsen design health

| # | Heuristic | Score | Key issue |
|---|---|---:|---|
| 1 | Visibility of system status | 3 | Shared loading/error/live regions are good; unread state is not named reliably |
| 2 | Match system / real world | 3 | Arabic operational copy is mostly direct; two carousel landmarks remain English |
| 3 | User control and freedom | 2 | Some custom dialogs do not guarantee Escape, focus containment, or restoration |
| 4 | Consistency and standards | 2 | Shared primitives coexist with legacy custom overlays and hard-coded themes |
| 5 | Error prevention | 3 | Disabled states and confirmations are common |
| 6 | Recognition rather than recall | 2 | Student desktop labels are pointer-hover dependent |
| 7 | Flexibility and efficiency | 3 | Admin search, grouped navigation, table paging, and keyboard carousel controls help |
| 8 | Aesthetic and minimalist design | 2 | Student/card surfaces carry more blur, gradients, shadows, and animation than needed |
| 9 | Error recovery | 3 | Role-level error boundaries preserve safe retry/home actions |
| 10 | Help and documentation | 2 | Useful hints exist, but contextual guidance is inconsistent in dense workflows |
| **Total** |  | **25/40** | **Acceptable** |

## Findings

### [P1] Student mobile drawer can make lower destinations and actions unreachable

- **Evidence**:
  `frontend/src/components/layout/StudentShellChrome.tsx:493` opens a
  body-locking overlay; its drawer at `:500` has `h-full` but no
  `overflow-y-auto`. The descendants at `:503-504` do not establish a bounded
  scroll container, while navigation, balance/gamification, theme, and logout
  continue through `:527-603`.
- **Impact**: at 320 px, short landscape viewports, 200% zoom, or long Arabic
  labels, content can extend below the viewport while body scrolling is locked.
  A user may be unable to reach notifications, balance, theme, or logout.
- **Standard**: WCAG 1.4.10 Reflow; 2.1.1 Keyboard.
- **Recommendation**: make the dialog panel a bounded flex column and put
  `min-h-0 overflow-y-auto` on the content/navigation region; keep header and
  critical footer actions intentionally sticky only if they still fit at 200%.
- **Suggested command**: `/adapt`.
- **Disposition**: **FIXED / PENDING REMOTE**. The complete drawer panel now
  owns vertical scrolling and overscroll containment at
  `StudentShellChrome.tsx:508`; the remote 320 px/200% zoom test verifies that
  logout remains reachable.

### [P1] Legacy operational dialogs bypass focus containment and restoration

- **Evidence**:
  `frontend/src/components/live-support/admin/ConversationInvestigation.tsx:80-81`
  creates a modal dialog without initial focus, Tab containment, Escape
  handling, inert background, or trigger restoration.
  `frontend/src/components/admin/ImageZoomModal.tsx:64-70` renders another
  overlay without dialog semantics; its effect at `:17-34` only handles Escape
  and body scroll, and its controls at `:81-97` are not focus-contained.
- **Impact**: keyboard and screen-reader users can move behind a supposedly
  modal surface, lose their place after closing, or not be told that a dialog
  opened. This affects high-value admin support investigation and lesson
  mind-map viewing.
- **Standard**: WCAG 2.1.1 Keyboard, 2.4.3 Focus Order, 2.4.11 Focus Not
  Obscured, 4.1.2 Name/Role/Value; WAI-ARIA modal-dialog pattern.
- **Recommendation**: migrate both to `AccessibleOverlay`, `AccessibleDialog`,
  or the reviewed `AdminModal`; preserve labelled titles and explicit trigger
  refs.
- **Suggested command**: `/harden`.
- **Disposition**: **FIXED / PENDING REMOTE**. Both components now delegate
  dialog semantics, focus containment, Escape, inertness, scroll lock, and
  restoration to `AccessibleOverlay`.

### [P1] Recharge proof image is pointer-only and has an unhelpful accessible name

- **Evidence**:
  `frontend/src/app/admin/recharge-verification/RechargeVerificationPageClient.tsx:279`
  attaches `onClick` to a non-focusable `div` with no keyboard handler or
  button role. The image alt at `:281-284` is the English generic word
  `"proof"`.
- **Impact**: a keyboard-only administrator cannot open the transaction proof;
  a screen-reader user receives neither a meaningful action name nor useful
  image purpose during a financial verification task.
- **Standard**: WCAG 1.1.1 Non-text Content, 2.1.1 Keyboard, 4.1.2
  Name/Role/Value.
- **Recommendation**: use a real button around the thumbnail, provide a
  request-specific Arabic accessible name, and keep the image alt concise or
  empty when the button name already carries the purpose.
- **Suggested command**: `/harden`.
- **Disposition**: **FIXED / PENDING REMOTE**. The proof trigger is now a real
  button with a request-specific Arabic accessible name and a decorative empty
  thumbnail alt.

### [P1] Student unread notification link can be announced only as a number

- **Evidence**:
  `frontend/src/components/layout/StudentShellChrome.tsx:422-433` gives the
  icon-only desktop link a `title` but no `aria-label`; when unread items exist,
  the only text content is the badge count at `:428-431`.
- **Impact**: its computed name can become “3” instead of “الإشعارات، 3 غير
  مقروءة,” so screen-reader users cannot identify the destination or state.
- **Standard**: WCAG 2.4.4 Link Purpose, 4.1.2 Name/Role/Value, 4.1.3 Status
  Messages.
- **Recommendation**: supply a localized dynamic `aria-label`; hide the visual
  badge from accessibility APIs if the count is already included in that name.
- **Suggested command**: `/clarify`.
- **Disposition**: **FIXED / PENDING REMOTE**. The link now announces the
  destination and dynamic unread count, while its visual badge is hidden from
  the accessibility tree.

### [P1] User-selected support bubble colors are not guaranteed to meet contrast

- **Evidence**:
  `frontend/src/components/live-support/staff/StaffChatSettings.tsx:36` permits
  arbitrary message colors. Text is selected with a binary YIQ threshold in
  `frontend/src/components/live-support/staff/StaffConversationWorkspace.tsx:45-49`,
  not a WCAG relative-luminance contrast calculation.
- **Impact**: valid color choices such as saturated mid-tone colors can produce
  less than 4.5:1 contrast with both selected text outcomes, making chat
  messages hard to read for low-vision staff.
- **Standard**: WCAG 1.4.3 Contrast (Minimum).
- **Recommendation**: constrain choices to tested semantic swatches or compute
  WCAG contrast and reject/adjust combinations below 4.5:1.
- **Suggested command**: `/colorize`.
- **Disposition**: **FIXED / PENDING REMOTE**. `accessibleColorPair` now uses
  WCAG relative luminance, selects the stronger brand text color, and adjusts
  unsafe requested backgrounds until they reach at least 4.5:1. Unit contracts
  cover white, black, mid-gray, saturated red, teal, shorthand hex, and invalid
  input.

### [P2] Staff live support is a hard-coded light-theme island

- **Evidence**:
  `frontend/src/components/live-support/staff/StaffConversationLayout.tsx:18-20`,
  `StaffConversationWorkspace.tsx:29-37`, and
  `StaffChatSettings.tsx:30-39` use `bg-white`, `slate-*`, `cyan-*`, `red-*`,
  and `amber-*` directly with no dark variants.
- **Impact**: switching the assistant/admin shell to dark mode leaves a bright
  embedded workspace with inconsistent focus, border, and semantic colors,
  causing glare and breaking visual continuity during long support shifts.
- **Recommendation**: map the support workspace to the existing admin semantic
  tokens and validate every status pair in both themes.
- **Suggested command**: `/normalize`.
- **Disposition**: **FIXED / PENDING REMOTE**. The staff layout, workspace,
  settings, queue, status cards, handoff summary, and canned-reply dialog now
  use the shared admin semantic tokens.

### [P2] Student desktop navigation reveals labels only on pointer hover

- **Evidence**:
  the rail expands via `hover:w-64` at
  `frontend/src/components/layout/StudentShellChrome.tsx:251`; visible labels
  rely on `group-hover/sidebar:block` at `:267`, `:283`, `:311`, `:339`,
  `:354`, and `:367`, with no parallel `focus-within` behavior.
- **Impact**: sighted keyboard and switch users see only icons while tabbing,
  increasing recognition and memory burden even though ARIA labels protect
  screen-reader naming.
- **Standard**: WCAG 2.4.7 Focus Visible and Nielsen recognition over recall.
- **Recommendation**: expand/reveal labels on `focus-within`, or provide an
  explicit persistent collapsed/expanded control like the other role shells.
- **Suggested command**: `/adapt`.
- **Disposition**: **FIXED / PENDING REMOTE**. The rail and every conditional
  label/count now respond to `focus-within` as well as pointer hover.

### [P2] The staff composer becomes overly compressed at narrow reflow widths

- **Evidence**:
  `frontend/src/components/live-support/staff/StaffConversationWorkspace.tsx:37`
  puts canned replies, upload, input, and send into one non-wrapping row.
- **Impact**: at the 320 CSS-pixel equivalent of 200% zoom, fixed 44 px actions
  and the Arabic canned-reply label consume most of the row, leaving a very
  narrow text field and increasing typing errors.
- **Recommendation**: move secondary controls to a compact second row or
  collapse canned replies behind a labelled icon at the narrow container
  breakpoint.
- **Suggested command**: `/adapt`.
- **Disposition**: **FIXED / PENDING REMOTE**. The composer uses a narrow
  three-column grid with canned replies on its own row, then switches to the
  four-column operational layout at `sm`.

### [P2] Student lesson surfaces drift from the restrained utilitarian direction

- **Evidence**:
  `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel.tsx:208-220`
  layers animated cards, gradients, blur, hover borders, and long transitions;
  the video frame at `:347` adds a large shadow and ring. Similar rounded/glass
  treatment appears in the shell header at
  `frontend/src/components/layout/StudentShellChrome.tsx:387-392`.
- **Impact**: decoration competes with lesson state, exam locks, and the video
  itself, and makes this surface feel less consistent with the calm operational
  Massar system.
- **Recommendation**: retain motion only for state change, flatten nested
  surfaces, and use border/spacing hierarchy before blur, gradients, or large
  shadows.
- **Suggested command**: `/quieter`.
- **Disposition**: **FIXED / PENDING REMOTE**. The lesson container now uses a
  moderate-radius token surface and short color transition; the decorative
  mouse-follow layer, gradient, blur, wide shadow, and ring were removed.

### [P3] Carousel landmarks use English microcopy inside Arabic flows

- **Evidence**:
  `frontend/src/components/ui/feature-carousel.tsx:432` and
  `frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/components/LessonCarousel.tsx:58`
  expose `aria-label="Progress"`.
- **Impact**: screen-reader landmark lists mix languages and feel unfinished,
  although item names and navigation remain understandable.
- **Recommendation**: use contextual Arabic names such as “مراحل العرض” and
  “فيديوهات الدرس.”
- **Suggested command**: `/clarify`.
- **Disposition**: **FIXED / PENDING REMOTE**. The landmarks are now named
  “مراحل العرض” and “فيديوهات الدرس.”

## Role and state matrix

| Surface | Static strengths | Original finding and current static disposition | Browser evidence |
|---|---|---|---|
| Public/auth | Route-group ownership, shared theme and reduced-motion policy | Full 320 px/zoom and carousel behavior not executable locally | Pending remote Chromium/WebKit |
| Student | Persistent shell, four-item bottom nav, focus manager, safe async boundaries | Drawer overflow, unread naming, focus label reveal, and lesson decoration fixed statically | Pending |
| Teacher | Labelled bottom nav/drawer, bounded drawer scrolling, safe loading/error boundary | Dense page-specific tables/forms still require computed reflow and contrast checks | Pending |
| Assistant/staff | Grouped/searchable navigation, accessible shared drawer, safe role boundary | Theme tokens, contrast-safe bubble pairing, and composer reflow fixed statically | Pending |
| Admin | Searchable/grouped navigation, responsive table region, shared modal and async primitives | Legacy dialogs and pointer-only proof fixed statically through shared primitives and a real button | Pending |

## Original persona red flags and dispositions

- **Sam — keyboard/screen-reader/low vision**: the original proof activation,
  dialog boundary, unread-name, and chat-contrast risks are fixed by static
  contracts; keyboard and screen-reader confirmation remains remote.
- **Casey — distracted mobile user**: drawer reachability and composer reflow
  are fixed structurally; 320 px and 200% zoom confirmation remains remote.
- **Alex — operational power user**: benefits from admin search, grouped
  navigation, paging, and retained shells; shared overlay migration fixes the
  cited dialog inconsistency statically.
- **Arabic academy operator — project-specific**: direct Arabic labels and RTL
  placement are strong; carousel landmarks are now Arabic and the support
  workspace now uses shared theme tokens.

## Positive findings

- `AccessibleOverlay` provides initial focus, Tab containment, Escape,
  background inertness, body scroll lock, and trigger restoration.
- `NavigationFocusManager` and skip links establish predictable post-route
  focus across persistent shells.
- `AsyncRegionState` supplies labelled loading, polite/assertive live regions,
  safe Arabic errors, retry, and a home escape without exposing internal error
  text.
- Admin tables identify horizontal scrolling, are keyboard-focusable, preserve
  row actions, and adapt lower-priority columns.
- Root `MotionConfig reducedMotion="user"` plus global reduced-motion CSS form
  a strong baseline, and the feature carousel pauses autoplay for reduced
  motion, focus, hover, and hidden documents.
- Mobile primary navigation is bounded and uses `aria-current`; teacher/admin
  drawers have explicit bounded vertical scrolling.
- Main role shells predominantly use shared semantic tokens in both light and
  dark modes.

## Static verification

| Gate | Result |
|---|---|
| `npm run check:accessibility` | PASS — static contracts and browser-matrix definition |
| `node scripts/check-carousel-navigation-accessibility.mjs` | PASS |
| `npm run check:design-tokens -- --check` | PASS — no newly introduced raw colors |
| `npm run check:accessible-colors` | PASS — all tested pairs are at least 4.5:1 |
| `npm run check:ui-review-fixes` | PASS — all ten dispositions are structurally enforced |
| `npm run typecheck` | PASS |
| Focused ESLint for all T125 source/tests | PASS — zero warnings/errors |
| Browser execution | **PENDING REMOTE** |

The first combined command attempted a nonexistent
`check:carousel-navigation` npm alias after the accessibility gate passed. No
download or install occurred; the repository script was then executed directly
and passed.

## Recommended remediation order

1. **[P1] `/harden`** — migrate legacy operational overlays to the reviewed
   accessible primitives and replace the click-only recharge proof.
2. **[P1] `/adapt`** — make the student drawer and staff composer survive 320 px,
   short viewports, long Arabic, and 200% zoom.
3. **[P1] `/clarify`** — correct notification naming and Arabic carousel
   landmarks.
4. **[P1/P2] `/colorize` then `/normalize`** — enforce contrast-safe support
   bubbles and map the staff workspace to theme tokens.
5. **[P2] `/quieter`** — reduce glass, gradient, shadow, and motion competition
   on lesson surfaces.
6. **[P3] `/polish`** — run the final cross-role consistency pass.

## Remote browser gate

This review does not close the runtime gate. The reviewed remote builder must
execute the existing Chromium/WebKit matrix in light and dark themes, RTL,
320 px, 200% zoom, reduced motion, long Arabic content, and keyboard-only mode.
It must cover every role plus drawer trap/restoration, carousel pause and
controls, loading/error announcements, focus after navigation, horizontal
table operation, and the exact P1 paths above. Attach screenshots, axe output,
and keyboard observations to the sealed candidate evidence.

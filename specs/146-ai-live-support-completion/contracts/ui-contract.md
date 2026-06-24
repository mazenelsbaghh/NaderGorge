# Contract: AI Live Support UI and UX

## Shared Design Rules

- Arabic-first RTL, Tajawal, existing Massar tokens: Deep Navy, Teal, Warm Gold, Off White, Dark Gray, Light Gray, Soft Gray.
- Preserve current public, student, assistant, and admin shells.
- Use Lucide line icons; no emoji controls, decorative glass, gradient text, dark generic AI theme, or nested card grids.
- Product surfaces use 12–16px container radii. Pills are limited to compact status/chip controls.
- Every interactive control has default, hover, focus-visible, active, disabled, loading, and error behavior.
- Motion is 150–250ms and communicates state. Reduced motion removes pulsing/translation while preserving meaning.
- Errors use text plus icon and `role=alert`; status changes use a scoped polite live region.
- Color is never the only state cue. Body contrast is at least 4.5:1. Participant touch targets are at least 44px.

## Participant Widget

### Layout

- Minimum supported viewport: 320px.
- One column; no horizontal page scrolling.
- Launcher respects safe areas and student bottom navigation.
- Header contains service identity, AI/human mode text, connection state, and one clear close/minimize action.
- Message list owns remaining height and preserves scroll position when older history loads.
- Pending decision/verification/registration occupies a stable region above the composer so its appearance does not cover messages.
- Composer is disabled with a textual reason when pending decision policy blocks new input, during handoff transition, or after terminal state.

### Required States

- launcher loading/unavailable/next schedule
- guest intake idle/submitting/validation failure
- connecting/reconnecting/offline with retained draft
- AI disclosure and active policy mode
- queued/processing/retrying/failed turn
- reply delivered
- action pending/confirming/succeeded/cancelled/expired/invalidated/failed
- verification lookup/challenge/success/failure/exhausted
- secure registration proposed/editing/submitting/succeeded/failed
- handoff proposed/confirming/rejected/forced/queued/assigned
- human active/ownership transfer
- closed/abandoned/read-only/rating available/rating submitted
- empty history and paginated older history

### Accessibility

- Opening the widget moves focus to the heading; closing returns focus to launcher.
- New AI/human messages are announced without rereading the full transcript.
- Confirmation dialogs/cards identify action, target, effect, consequence, and expiry.
- Secure inputs have visible labels, correct `autocomplete`/`inputMode`, field-level errors, and clear values on success/unmount.
- Enter sends only from the message composer; multiline and IME composition do not submit accidentally.

## Staff Command Center

### Desktop

- Three task regions: assignment/queue list, active transcript, linked-student context.
- Transcript is the primary region. Ownership/mode and participant identity boundary remain visible.
- AI handoff summary shows safe reason, policy version, verification/link status, attempted actions, and safe failures. It never shows system instructions, expected answers, raw lookup values, or secrets.

### Tablet/Narrow

- Use list → conversation → student-context drill-in rather than squeezed three columns.
- Back navigation preserves selection, scroll, draft, and unread state.

### Required States

- eligibility/attendance/connection/capacity
- no assignment/useful empty state
- queue loading/empty/error/retry
- ownership gained/lost/transferred
- transcript loading/pagination/reconnect
- student context unlinked/loading/partial/empty/error
- action confirmation/executing/success/failure/stale
- close/transfer validation and completion

## Administrator Operations

- Existing `/admin/live-support` retains operations, history, performance, and staff configuration.
- Operations use accessible tables/lists with filters, deterministic empty/error states, and direct investigation access.
- Timeline differentiates participant, AI, system, worker/recovery, staff, and administrator events with text labels, not color alone.

## Administrator AI Settings

Route: `/admin/live-support/ai`, visible and operable only to built-in Admin.

Modules:

1. Overview and emergency enable/disable with readiness and active-work impact.
2. Instructions and immutable published-version history.
3. Knowledge entries, revisions, publication, and policy selection.
4. Readable-data and action catalogs with descriptions and validation.
5. Lookup and verification policy with privacy explanation.
6. Zero-write preview with explicit `No production changes` state.
7. Activity/evidence filters and redacted detail.
8. Statistics with period filter and definitions.
9. Active AI conversations and investigation.

Draft and published state must never be communicated by color alone. Save, publish, enable, and disable are distinct verbs with distinct confirmation and completion text. Sticky actions must not cover the final fields at any supported height.

## UX Copy Rules

- Use concise Egyptian/Modern Standard Arabic appropriate to the existing platform.
- Button labels state the result: `حفظ المسودة`, `نشر وتفعيل`, `تأكيد الإجراء`, `إلغاء الإجراء`, `التحويل لموظف`, `إعادة المحاولة`.
- Do not claim success before durable backend confirmation.
- Provider/internal failure text explains what the user can do next without exposing technical internals.
- Guest lookup text never confirms whether an account exists.

## Browser Verification Matrix

| Surface | Widths | Modes | Required evidence |
|---|---|---|---|
| Participant | 320, 375, 768 | student, guest, AI, human | no overflow/overlap; keyboard; reconnect; cards/forms |
| Staff | 768, 1024, 1440 | empty, assigned, transferred | structural drill-in; ownership clarity; context/actions |
| Admin operations | 1024, 1440 | live, history, failure | filters, timeline, intervention, tables |
| Admin AI | 768, 1024, 1440 | draft, published, disabled, error | tabs/modules, zero-write preview, evidence, focus |

Test Chromium and WebKit, RTL, long mixed Arabic/English content, 200% zoom, reduced motion, keyboard-only, and light theme. Test any supported dark theme only if it already exists in the current shell.

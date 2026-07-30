---
target: كل صفحات Staff / المساعدين
total_score: 20
p0_count: 0
p1_count: 2
timestamp: 2026-07-23T12-19-28Z
slug: frontend-src-app-assistant
---
## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---:|---|
| 1 | Visibility of system status | 3 | Loading exists, but refresh, save, and read states are inconsistent across workspaces. |
| 2 | Match system and real world | 3 | Operational language is clear, though legacy HR terminology conflicts with the new HR model. |
| 3 | User control and freedom | 2 | Task and HR flows have limited undo, withdrawal, and recovery affordances. |
| 4 | Consistency and standards | 2 | Four Staff routes render Admin pages directly and lose the Staff shell. |
| 5 | Error prevention | 2 | Leave balance and leave requests are sourced from two different workflows. |
| 6 | Recognition rather than recall | 2 | Dense route labels and permission-dependent navigation offer little in-context guidance. |
| 7 | Flexibility and efficiency | 1 | No discoverable keyboard shortcuts, saved filters, or bulk paths for common operational queues. |
| 8 | Aesthetic and minimalist design | 2 | Repeated oversized rounded cards and broad shadows weaken hierarchy. |
| 9 | Error recovery | 2 | Several failures use generic toasts without an inline recovery path. |
| 10 | Help and documentation | 1 | No contextual help for CRM, moderation, attendance, or HR decisions. |
| **Total** | | **20/40** | **Acceptable, significant improvement needed** |

## Anti-Patterns Verdict

The Staff surface has a competent operational foundation, but it still reads as an assembled set of admin widgets rather than one deliberate employee workspace. The recurring `rounded-3xl` and `rounded-[24px]` card treatment, soft shadows, and isolated metric cards make dense work feel decorative instead of precise.

The deterministic scan found one warning: gray `text-slate-800` on `bg-cyan-50` in `AssistantLiveSupportPageClient.tsx:229`. This is a valid contrast and visual-language concern. It introduces cyan and slate outside the navy, teal, gold system and creates a weaker state cue than a tokenized surface.

## Overall Impression

The shell gives Staff a recognizable home and the live-support workspace is the strongest task-focused area. The biggest opportunity is to make every Staff route feel native to that home, then simplify the visual vocabulary around real operational decisions.

## What's Working

- The navigation is permission-aware and labels routes in Arabic instead of relying on icon-only discovery.
- Live support has deliberate status, queue, conversation, and context regions rather than a single overloaded panel.
- Tasks, attendance, notifications, and leave each expose loading or empty states instead of failing silently.

## Priority Issues

- **[P1] Split Staff identity on moderation and content routes**: `content`, `community`, `questions`, and `watch-requests` directly render Admin page clients. The Staff user can lose the Staff shell, current-route context, and a consistent mobile navigation pattern. Wrap these experiences in a Staff-native page shell or extract their content workspace from the Admin shell. Suggested command: `$impeccable shape`.

- **[P1] Two competing leave workflows**: the leave page displays balances from the new HR ledger but lists and submits requests using the legacy `VacationDto` and `VacationRequestModal`. A user can see one balance while creating a request in another system. Replace the legacy modal/history with `listMyLeaveRequests` and `submitLeaveRequest`, including leave type, workdays, balance impact, and withdrawal. Suggested command: `$impeccable harden`.

- **[P2] Dashboard does not establish the next action**: the dashboard headline promises an operational workspace, but initially only presents tabs. It lacks a ranked “do this now” queue, overdue signal, and short explanation of what changed. Put the actionable queue first and relegate secondary dashboards behind it. Suggested command: `$impeccable layout`.

- **[P2] Product UI vocabulary is over-rounded and inconsistent**: cards and empty states repeatedly use 24px, 32px, and `rounded-3xl`, combined with borders and soft shadows. This flattens hierarchy and conflicts with the system’s 12–20px surface rule. Standardize panels to 12–16px, reserve pills for filters/status, and use either a divider or a small shadow, not both. Suggested command: `$impeccable polish`.

- **[P2] Operational recovery and help are too generic**: several pages only show a toast on failure; the user cannot see what failed, retry in place, or learn the correct next step. Add inline error panels with retry to queue pages, and contextual guidance for CRM, moderation, attendance exceptions, and leave. Suggested command: `$impeccable clarify`.

## Persona Red Flags

**Alex, power user**: moderation, CRM, watch requests, and tasks lack a shared quick-action model, keyboard shortcuts, saved views, or bulk triage. Alex is forced through route-by-route work.

**Sam, keyboard and screen-reader user**: the shell is generally improved, but status changes such as notification-read, queue refresh, and task updates do not consistently announce completion. Dense tabs and repeated card buttons need a shared semantic contract.

**Casey, distracted mobile user**: the Staff-specific mobile shell disappears on routes that reuse Admin clients. The user must relearn navigation mid-task, and the leave flow cannot explain which balance will be affected before submission.

## Minor Observations

- Use the HR leave type rather than the broad label “annual balance” when multiple balances exist.
- The all-card notification list needs an explicit unread label, not opacity alone.
- Replace uppercase utility labels in Arabic with normal Arabic labels.
- Tokenize the cyan/slate live-support alert to the established navy/teal semantic palette.

## Questions to Consider

- Should a Staff member ever see the Admin shell, or should every permitted capability be embedded in one Staff workspace?
- Which queue is more important at the top of the dashboard: overdue tasks, live-support conversations, or assigned CRM follow-ups?
- Is the goal for Staff to handle leave requests entirely in the new HR system now, with the legacy vacation workflow retired?

---
target: شاشات الإدارة كلها بعد اكتمال الحماية والتجاوب
total_score: 24
p0_count: 0
p1_count: 3
timestamp: 2026-07-23T12-41-22Z
slug: frontend-src-app-admin
---
## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|---|---:|---|
| 1 | Visibility of system status | 3/4 | Shared loading is strong; save states vary by domain. |
| 2 | Match system / real world | 3/4 | HR approvers cannot see leave facts before deciding. |
| 3 | User control and freedom | 2/4 | Live-support master toggle remains immediate; unsaved edits lack recovery. |
| 4 | Consistency and standards | 2/4 | Finance and live support still fork shared admin vocabulary. |
| 5 | Error prevention | 2/4 | Rejection reason is good; decision context and some guards are absent. |
| 6 | Recognition rather than recall | 3/4 | Search/Ctrl+K and groups help; collapsed/mobile navigation still hides choices. |
| 7 | Flexibility and efficiency | 3/4 | Shortcut, priority columns, pagination help; finance tabs are not mobile-safe. |
| 8 | Aesthetic and minimalist design | 3/4 | Core admin is restrained; finance legacy glass/pills remain. |
| 9 | Error recovery | 2/4 | Shared retry exists, but page-level mutations lack inline recovery. |
| 10 | Help and documentation | 1/4 | High-impact operational decisions lack policy/evidence context. |
| **Total** | | **24/40** | **Usable, not yet a trustworthy command center** |

## Anti-patterns verdict

The core shell, shared table, and confirmation dialog are now coherent and restrained. Deterministic scan is clean: 0 findings. Remaining drift is product UX, not an AI-style visual anti-pattern: finance retains blur/pill/shadow legacy treatment and live support duplicates table/form conventions.

## Priority issues

### [P1] HR decisions lack the facts being approved

Approval cards show requester/workflow metadata but not leave dates, type, duration, remaining balance, or coverage. Add a compact facts grid, request-detail drawer, policy/balance context, and audit evidence before approve/reject. Suggested command: `$impeccable harden`.

### [P1] Not every high-impact mutation has the same safety model

Live support’s master switch executes immediately, and support configuration/replies lack dirty-state, retry, and rollback clarity. Apply confirmation to availability changes and show per-action pending/success/retry states. Suggested command: `$impeccable harden`.

### [P1] Finance tabs do not structurally adapt to phones

Four long tabs have no semantic tablist or narrow-screen overflow strategy. Use a scrollable tab rail with cue, or a compact select on small screens. Suggested command: `$impeccable adapt`.

### [P2] Component vocabulary is not fully consolidated

Finance keeps glass/pill/shadow legacy styles and live support still uses a custom table/form dialect. Migrate them to shared tokenized components. Suggested command: `$impeccable polish`.

### [P2] Collapsed and mobile navigation hide useful choices

Collapsed groups are icons without child discovery, and mobile quick destinations are array-order driven rather than role/task driven. Add tooltips/flyout and role-scoped quick actions or recent pages. Suggested command: `$impeccable layout`.

## Persona red flags

- **HR approver:** can confirm a decision without verifying the leave details.
- **Finance controller:** irreversible payroll approval lacks employee/amount/period/audit rationale in the confirmation view.
- **Support supervisor:** instant service toggle and no aggregate unsaved indicator make accidental or incomplete configuration plausible.

## Minor observations

- Arabic section letter spacing remains excessive.
- Finance filters retain 24px rounded/glass styling outside the admin standard.
- Validate the RTL visual action order in confirmation dialog footer.
- Live Support custom table lacks the shared mobile scroll cue and column-priority API.

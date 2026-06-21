# UI Contract: Live Support

## Design context

- Arabic-first RTL educational product.
- Student/guest surfaces are mobile-first and one-handed.
- Staff/admin surfaces are desktop-first, dense, and task-oriented.
- Preserve Massar Deep Navy, Teal, Gold, Off White, Tajawal/Montserrat and existing product tokens.
- Do not apply the generic dark/green palette or Noto fonts returned by broad UI search.

## Participant launcher and widget

### Placement

- Shared launcher appears on permitted landing and student surfaces when `LiveSupportEnabled`.
- Minimum 44×44px touch target, not covering bottom navigation or browser safe area.
- Launcher badge indicates unread messages, not decorative animation.

### State machine

1. `checking-availability`: compact skeleton.
2. `unavailable`: next support time or “لا يوجد موعد دعم محدد”; no start form.
3. `guest-intake`: labeled name/phone inputs; explicit statement that phone does not verify identity.
4. `ready-to-start`: first-message field and start action.
5. `waiting`: queue position, waiting since, cancel/close widget actions.
6. `assigned`/`active`: messages, staff name, send/attachment controls, reconnect status.
7. `closed`: read-only history, 1–5 stars, optional comment, “بدء محادثة جديدة”.
8. `abandoned`: read-only history and new-conversation action.
9. `error`/`reconnecting`: preserve draft and acknowledged messages; retry snapshot.

### Responsive behavior

- 320–767px: full-height bottom sheet using dynamic viewport and safe-area padding.
- ≥768px: anchored panel up to 420px wide and 70dvh high.
- No horizontal scroll; message content wraps; attachments show bounded previews.

## Staff command center `/assistant/live-support`

### Desktop ≥1024px

```text
┌──────────────────┬────────────────────────────┬──────────────────────────┐
│ Owned/queue list │ Conversation + composer    │ Student context/actions  │
│ load/capacity    │ assignment/status/timeline │ lazy section navigation  │
└──────────────────┴────────────────────────────┴──────────────────────────┘
```

- List width 280–340px; conversation flexible; student panel 340–440px.
- One selected conversation; URL query contains selection for refresh continuity.
- Queue count is visible but staff cannot manually steal another owner's chat.
- Header shows checked-in eligibility, connection, active/capacity count, and checkout consequences.

### Tablet 768–1023px

- Conversation and list use master-detail.
- Student context opens as a full-height sheet/side panel, not a clipped dropdown.

### Mobile <768px

- Drill-in screens: conversations → chat → student context/actions.
- Back controls preserve selected conversation/draft.
- Critical actions remain accessible; nothing essential is hidden behind hover.

### Student context

- Lazy sections: Summary, Identity, Academic/Family, Packages, Balance, Devices, Watch/Requests, Exams/Homework, Gamification, CRM, Notes, Audit.
- Each section owns loading, empty, error, stale and refresh states.
- Actions use typed forms and a shared confirmation dialog showing student, change, reason and impact.
- Financial/high-risk actions require explicit button labels such as “تأكيد تعديل الرصيد”, never “موافق”.
- After execution, refresh only contract-declared sections and append execution status to timeline.

## Admin operations `/admin/live-support`

Tabs:

1. **العمليات الآن**: staff eligibility/load/capacity, active conversations, queue, oldest wait, SLA warnings.
2. **المحادثات والسجل**: filter/search, immutable timeline, messages, assignments, links, actions, rating.
3. **أداء الموظفين**: handled count, wait/first response/handling medians and percentiles, transfers, action failures, rating average/count. One conversation rating counts for every owner.
4. **إعداد الموظفين**: enable, capacity 1–50, weekly schedule windows, current attendance and effective eligibility.

Admin intervention always requests a reason and visually distinguishes observation from ownership.

## Loading, empty and failure states

- Skeleton rows/panels for initial data; no full-page spinner after shell load.
- Empty queue: “لا توجد محادثات منتظرة”.
- No owned chat: explain that assignments arrive automatically after attendance check-in.
- Unlinked guest: show guest claims separately and a manual link/search workflow; do not show matching account hints automatically.
- Stale action: show updated state and require reconfirmation.
- Lost ownership: immediately disable composer/actions and show current owner.
- Disconnect: persistent reconnect banner with elapsed grace; do not imply ownership after 120 seconds.

## Accessibility

- Semantic headings/regions for list, conversation, and student context.
- `aria-live="polite"` for new messages, queue position, assignment, reconnect, and action result; avoid reading the full transcript repeatedly.
- Keyboard list navigation; Enter opens; Escape closes sheet/dialog; focus returns to trigger.
- Visible 2px focus ring using Teal; body text ≥4.5:1; status not communicated by color alone.
- Reduced motion converts slide/scale to instant or opacity-only state transitions.
- Message sender and canonical time available to assistive technology.

## Performance

- Cursor-page messages and histories; load older on explicit scroll/action.
- Virtualize only when message/list size justifies it and preserve screen-reader fallbacks.
- Dynamically load heavy student sections/charts/action forms.
- Deduplicate hub events by durable ID and avoid replacing the full conversation array on unrelated dashboard deltas.

# UI Contract: AI Live Support Agent

## Admin route `/admin/live-support/ai`

Built-in Admin only. Use a route-level guard and backend enforcement.

Tabs:

1. **نظرة عامة**: enabled state, published/draft version, queue/latency/outcome summary, worker readiness, emergency disable.
2. **التعليمات والمعرفة**: instruction editor, knowledge list/revisions, draft/published/expired states, validation.
3. **البيانات والإجراءات**: searchable server catalogs, descriptions, risk/effect, enabled selections. All AI actions show “requires participant confirmation”.
4. **التحقق من الحساب**: safe lookup keys, safe questions, required correct count, maximum attempts, prohibition explanations.
5. **المعاينة**: synthetic participant type/context, prompt, safe context categories, decision/evidence. Clear “no real writes” status.
6. **النشاط والتدقيق**: filters, outcome table, conversation evidence timeline, safe errors and handoff reasons.

Required states: loading skeleton, empty knowledge teaching state, unsaved changes, draft differs from published, validation error with field focus, version conflict/reload, publish confirmation, disabling progress, provider/worker unavailable, preview timeout, success toast. Standard controls, 44px minimum mobile actions, visible keyboard focus, RTL, reduced motion.

## Participant widget

- AI disclosure/badge is visible before first answer and on every AI message label.
- AI-thinking state is announced politely and never blocks “تحدث مع موظف”.
- Pending action is a structured card: action, target, exact effect, consequence, expiry, `تأكيد الإجراء`, `إلغاء`.
- Verification uses server-rendered prompts with no hints. Remaining attempts may be shown without correctness details.
- Account creation uses structured fields and a secure password control excluded from transcript.
- Handoff state removes message/AI action controls, shows human availability/queue, and never displays AI-thinking again.
- Inactivity warning shows deadline and `متابعة المحادثة` to cancel closure.
- Closed AI-resolved state offers rating and `محادثة جديدة`.

## Staff handoff panel

Show safe AI summary, full normal transcript, handoff reason, verification state (not answers), linked account, policy version, attempted/pending action outcomes, and safe failure codes. Staff ownership/capacity/action rules remain unchanged.

## Design register

Use Massar product tokens: navy authority, teal active/progress, gold only for milestone/attention, off-white canvas, Tajawal Arabic. Avoid generic AI gradients, glass, oversized radii, identical card grids, decorative animation, or hidden critical actions.

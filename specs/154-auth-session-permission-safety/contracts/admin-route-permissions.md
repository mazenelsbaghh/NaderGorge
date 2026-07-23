# Admin Route Permission Contract

## Rule Source

Admin page access must use one shared frontend source:

- `frontend/src/packages/admin/route-permissions.ts`

The source must be generated from or aligned with:

- `frontend/src/packages/admin/navigation.tsx`

## Required Rules

Known admin paths must include exact or parameterized entries for:

- `/admin`
- `/admin/subjects`
- `/admin/teachers`
- `/admin/teachers/:id`
- `/admin/students`
- `/admin/users`
- `/admin/users/:id`
- `/admin/assistants`
- `/admin/assistants/:id`
- `/admin/admins`
- `/admin/content`
- `/admin/content/video-types`
- `/admin/community`
- `/admin/ai-monitor`
- `/admin/codes`
- `/admin/codes/:groupId`
- `/admin/codes/templates`
- `/admin/gifts`
- `/admin/gifts/:id`
- `/admin/sales`
- `/admin/public-exams`
- `/admin/public-exams/:id`
- `/admin/questions`
- `/admin/overrides`
- `/admin/finance`
- `/admin/wallets`
- `/admin/recharge-verification`
- `/admin/reports`
- `/admin/hr`
- `/admin/hr/my-attendance`
- `/admin/operations`
- `/admin/media`
- `/admin/forms`
- `/admin/forms/new`
- `/admin/chat`
- `/admin/live-support`
- `/admin/live-support/ai`
- `/admin/settings`
- `/admin/watch-requests`

## Deny-by-default Behavior

- Full Admin/Supervisor roles may access admin routes unless an explicit future rule denies them.
- Staff/Assistant users may access only routes matching an allow rule for their permissions, allowed navbar items, or explicitly allowed role exception.
- Any unknown `/admin/*` path denies for staff-like users and must not render protected page content.

# Feature Specification: Assistant Permissions Navigation Integration

**Feature Branch**: `120-assistant-permissions-nav-integration`  
**Created**: 2026-06-11  
**Status**: Approved  
**Input**: User description: "المفروض دي صلاحيتوا بس مش باينه ف النيف بتاعوا لي" (pointing to why the granted permissions of assistants do not show in their sidebar/navigation).

## User Scenarios & Testing

### User Story 1 - Assistant Sidebar Navigation Integration (Priority: P1)
As an Assistant or Staff logged into the staff portal (`staff.massar-academy.net`), I should see the menu items corresponding to my granted permissions in the sidebar navigation:
- **إدارة تعليقات الدروس** (if I have `comments.manage` or `content.manage` permission).
- **إدارة مجتمع الطلاب** (if I have `community.manage` permission).
- **إدارة الامتحانات والأسئلة** (if I have `exams.manage` permission).
- **طلبات إعادة المشاهدة** (if I have `watch_requests.manage` permission).

**Independent Test**: Log in with an assistant account that has these permissions. Check that the sidebar navigation displays these four menu items.

---

### User Story 2 - Smooth Navigation to Admin Pages on Staff Subdomain (Priority: P1)
As an Assistant or Staff, when I click any of these new navigation items in the staff portal, I should be routed to the respective pages (e.g. `/admin/community`, `/admin/questions`, `/admin/watch-requests`, `/admin/content`).
The system's routing middleware (next.js middleware) must allow access to `/admin/*` paths on `staff.massar-academy.net` instead of rewriting them to `/not-found`.

**Independent Test**: Click "إدارة مجتمع الطلاب" on `staff.massar-academy.net` and verify that the community page is rendered correctly without getting a 404/not-found error.

---

### User Story 3 - Role-Based Admin Guard Bypass & Strict Page Authorization (Priority: P1)
As an Assistant or Staff, when I navigate to `/admin/*` routes, the `AdminGuard` must allow me to pass if my role is `Assistant` or `Staff` (in addition to `Admin` and `Supervisor`).
However, if I attempt to access a specific route for which I do **not** have permissions (e.g. `/admin/settings` or `/admin/students`), the `PermissionGuard` must intercept and redirect me to the unauthorized page (`/admin/unauthorized`).

**Independent Test**: Manually type `staff.massar-academy.net/admin/settings` and verify that the page redirects to `/admin/unauthorized`.

---

### User Story 4 - Return to Assistant Workspace Link (Priority: P2)
As an Assistant or Staff viewing the admin pages (which render using the `AdminShellChrome` layout), I should see a clear return link in the admin sidebar that says "مساحة المساعدين" (pointing to `/assistant/dashboard`). This link must only be visible to users with `Assistant` or `Staff` roles.

**Independent Test**: When navigating to `/admin/community`, check that a button labeled "مساحة المساعدين" is visible at the top of the sidebar, and clicking it returns you to `/assistant/dashboard`.

---

### Edge Cases
- **Bypassing the /admin Root Page**: When an Assistant accesses `staff.massar-academy.net/admin`, they should only see cards matching the pages they have permissions for. Other cards must be hidden to prevent confusing unauthorized redirects.
- **Session Sharing across Domains**: The system uses shared cookie-based auth. When the user navigates between the domains, session must persist. We must verify that auth stores read correctly.

## Success Criteria

- **SC-001**: 100% of granted admin permissions for an Assistant/Staff user are displayed as matching navigation items in the assistant portal sidebar.
- **SC-002**: Navigating to `/admin/*` routes on `staff.massar-academy.net` works correctly without throwing a `not-found` page.
- **SC-003**: Access is restricted strictly: attempting to access unauthorized admin paths blocks the user and redirects them to `/admin/unauthorized`.
- **SC-004**: Assistants/Staff can easily return to the assistant dashboard from any admin layout page via the sidebar link.

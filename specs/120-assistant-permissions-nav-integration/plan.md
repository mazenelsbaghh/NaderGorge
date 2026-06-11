# Technical Implementation Plan: Assistant Permissions Navigation Integration

## Proposed Changes

---

### Middleware and Surface Config

#### [MODIFY] [config.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/packages/surface-runtime/config.ts)
Change the surface condition for `assistant` to NOT block `/admin` routes. This will allow the `staff.` subdomain to route `/admin` requests instead of rewriting them to `/not-found`.

```diff
-    if (pathname.startsWith('/student') || pathname.startsWith('/admin') || pathname.startsWith('/teacher')) {
+    if (pathname.startsWith('/student') || pathname.startsWith('/teacher')) {
```

---

### Route Protection

#### [MODIFY] [AdminGuard.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/layout/AdminGuard.tsx)
Expand access to `/admin` routes to `Assistant` and `Staff` roles.

```diff
 function hasAdminAccess(roles: string[] | undefined) {
-  return !!roles?.length && (roles.includes("Admin") || roles.includes("Supervisor"));
+  return !!roles?.length && (
+    roles.includes("Admin") ||
+    roles.includes("Supervisor") ||
+    roles.includes("Assistant") ||
+    roles.includes("Staff")
+  );
 }
```

---

### Admin Layout & Sidebar Menu Filtering

#### [MODIFY] [layout.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/layout.tsx)
1. Redefine `ROUTE_PERMISSIONS` to store an array of `permissions` instead of a single string, mapping `/admin/content` to require either `content.manage` or `comments.manage`.
2. Refactor `PermissionGuard` to check if the user has **any** of the matched permissions.
3. Update `adminMenuItems` filter in `AdminLayout` to show `/admin/content` if user has either `content.manage` or `comments.manage`.

---

### Admin Dashboard Homepage Filtering

#### [MODIFY] [AdminRootPageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/AdminRootPageClient.tsx)
Filter the cards list `adminRootLinks` rendered on the main page of the admin workspace based on user permissions.

---

### Assistant Portal Sidebar Menu

#### [MODIFY] [AssistantShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/assistant/AssistantShellChrome.tsx)
1. Add new items to `navItems` matching `/admin/content`, `/admin/community`, `/admin/questions`, and `/admin/watch-requests` with their respective permissions.
2. Update the type `AssistantShellRoute`.
3. Import the icons `Shield`, `Star`, and `BookOpen` from `lucide-react`.

---

### Return Workspace Sidebar Link

#### [MODIFY] [AdminShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AdminShellChrome.tsx)
Render a "مساحة المساعدين" return link at the top of the admin sidebar layout if the user role includes `Assistant` or `Staff`.

---

## Verification Plan

### Automated Tests
Run lint check:
```bash
npm run lint
```

### Manual Verification
1. Log in on `staff.massar-academy.net` as assistant with permissions: `comments.manage`, `community.manage`, `exams.manage`, `watch_requests.manage`.
2. Confirm the 4 new items show in the sidebar.
3. Confirm clicking each routes to the admin page correctly.
4. Verify they see "مساحة المساعدين" link in the admin sidebar.
5. Attempt accessing `/admin/settings` directly and verify it redirects to `/admin/unauthorized`.

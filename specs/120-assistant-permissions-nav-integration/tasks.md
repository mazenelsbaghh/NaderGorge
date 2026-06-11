# Tasks Checklist: Assistant Permissions Navigation Integration

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

## Implementation Tasks

### 1. Middleware Surface Config
- [x] **Modify [config.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/packages/surface-runtime/config.ts)**:
  - In `getRouteBoundaryDecision()`, change the condition for `surface === 'assistant'` to allow `/admin` paths. Remove `|| pathname.startsWith('/admin')` from the rewrite condition.

### 2. Admin Route Protection
- [x] **Modify [AdminGuard.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/layout/AdminGuard.tsx)**:
  - In `hasAdminAccess()`, allow `"Assistant"` and `"Staff"` roles.

### 3. Route Permission & Sidebar Filtering in Admin Layout
- [x] **Modify [layout.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/layout.tsx)**:
  - Change `ROUTE_PERMISSIONS` array items to have `permissions: string[]` instead of `permission: string`.
  - Map `/admin/content` (and subjects/forms/etc.) to `permissions: ['content.manage', 'comments.manage']` or their respective requirements.
  - In `PermissionGuard`, check `matchedRoute.permissions.some(p => hasPermission(p))`.
  - In `AdminLayout`, filter `adminMenuItems` using:
    ```typescript
    const filteredMenuItems = adminMenuItems.filter((item) => {
      if (item.href === '/admin/content') {
        return hasPermission('content.manage') || hasPermission('comments.manage');
      }
      return hasPermission(item.permission);
    });
    ```

### 4. Admin Main Dashboard Cards Filtering
- [x] **Modify [AdminRootPageClient.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/AdminRootPageClient.tsx)**:
  - Import `useHasPermission`.
  - Use `useHasPermission` to filter `adminRootLinks` rendered in the homepage cards grid, matching links to their respective permissions.
  - Ensure '/admin/content' allows access if user has `content.manage` or `comments.manage`.

### 5. Assistant Portal Sidebar Menu Update
- [x] **Modify [AssistantShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/assistant/AssistantShellChrome.tsx)**:
  - Update `AssistantShellRoute` union type to include `/admin/content`, `/admin/community`, `/admin/questions`, and `/admin/watch-requests`.
  - Import `Shield`, `Star`, and `BookOpen` from `lucide-react`.
  - Insert the four new navigation items into `navItems` array with the permissions:
    - `/admin/content` (label: 'إدارة تعليقات الدروس', permission: 'comments.manage')
    - `/admin/community` (label: 'إدارة مجتمع الطلاب', permission: 'community.manage')
    - `/admin/questions` (label: 'إدارة الامتحانات والأسئلة', permission: 'exams.manage')
    - `/admin/watch-requests` (label: 'طلبات إعادة المشاهدة', permission: 'watch_requests.manage')

### 6. Return Workspace Link in Admin Layout
- [x] **Modify [AdminShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AdminShellChrome.tsx)**:
  - If user roles include `Assistant` or `Staff`, render a link labeled "مساحة المساعدين" pointing to `/assistant/dashboard` at the top of the navigation sidebar.

## Verification & Quality Gates

### Quality Gate Phase 6: Clean Code Guard
- [x] Run `clean-code-guard` against changed files.
- [x] Resolve any clean code comments or findings.

### Quality Gate Phase 7: Test Guard
- [x] Run `test-guard` against any changed test files.
- [x] Run `npm run lint` and verify clean build.

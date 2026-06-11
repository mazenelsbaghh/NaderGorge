# Validation Checklist: Assistant Permissions Navigation Integration

- [ ] **Middleware Surface Routing**
  - [ ] Update `getRouteBoundaryDecision` to permit `/admin/*` paths on the `assistant` surface.
  - [ ] Ensure students and landing pages cannot access `/admin/*` still.

- [ ] **Admin Route Guards**
  - [ ] Modify `AdminGuard` / `hasAdminAccess` to allow `"Assistant"` and `"Staff"` roles.
  - [ ] Ensure that `PermissionGuard` in `AdminLayout` intercepts page requests correctly and redirects unauthorized users to `/admin/unauthorized`.

- [ ] **Admin Layout and Sidebar filtering**
  - [ ] Modify `ROUTE_PERMISSIONS` pattern for `/admin/content` to allow `content.manage` or `comments.manage`.
  - [ ] Update `adminMenuItems` filter in `AdminLayout` to show `/admin/content` if user has either `content.manage` or `comments.manage`.

- [ ] **Assistant Portal Sidebar Navigation**
  - [ ] Add `comments.manage` (`/admin/content`), `community.manage` (`/admin/community`), `exams.manage` (`/admin/questions`), and `watch_requests.manage` (`/admin/watch-requests`) items to `navItems` in `AssistantShellChrome.tsx`.
  - [ ] Import the required Lucide icons (`Shield`, `Star`, `BookOpen`) in `AssistantShellChrome.tsx`.
  - [ ] Update type `AssistantShellRoute` to include the new paths.

- [ ] **Admin Main Dashboard Page Filtering**
  - [ ] Update `AdminRootPageClient` to filter links in `adminRootLinks` based on the user's permissions, ensuring assistants only see cards they can access.

- [ ] **Return to Assistant Workspace Sidebar Link**
  - [ ] Add a prominent "مساحة المساعدين" return link at the top of the admin sidebar in `AdminShellChrome.tsx` when user role is `Assistant` or `Staff`.

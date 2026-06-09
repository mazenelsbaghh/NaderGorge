# Tasks: Separate User Lists & Remove General Users Page

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`) completed.
- [x] Phase 2: Technical Planning (`speckit-plan`) completed.
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed.

---

## Technical Tasks Checklist

### Task 1: Navigation & Sidebar Config (Modify Shared Packages)
- [ ] In `frontend/src/packages/admin/navigation.tsx`:
  - Replace the general "المستخدمين" menu item with three separate items:
    1. `الطلاب` (Students) -> `/admin/students` with `Users` icon from Lucide
    2. `المساعدين` (Assistants) -> `/admin/assistants` with `Briefcase` icon
    3. `المديرين` (Admins) -> `/admin/admins` with `UserCog` icon
  - Update `adminRootLinks` to reflect the same separation.
- [ ] In `frontend/src/components/admin/AdminShellChrome.tsx`:
  - Update `AdminShellRoute` union type to include:
    - `'/admin/students'`
    - `'/admin/assistants'`
    - `'/admin/admins'`
  - Update `navItems` array:
    - Remove the entry for `/admin/users`.
    - Add `/admin/students` (label: "الطلاب", icon: Users, permission: "users.manage").
    - Add `/admin/assistants` (label: "المساعدين", icon: Briefcase, permission: "users.manage").
    - Add `/admin/admins` (label: "المديرين", icon: UserCog, permission: "users.manage").

### Task 2: Page Route Redirection & Profile Layout Sync
- [ ] In `frontend/src/app/admin/users/page.tsx`:
  - Change the component body to perform a client-side redirect (using Next.js `useRouter` or `redirect`) to `/admin/students`.
- [ ] In `frontend/src/app/admin/users/[id]/page.tsx`:
  - Modify `AdminShellChrome` props so that `activePath` is set to `'/admin/students'` instead of `'/admin/users'`, highlighting the "الطلاب" item in the sidebar.
  - Update the "العودة للقائمة" (Back to list) button click handler to navigate to `/admin/students` instead of `/admin/users`.

### Task 3: Create Students Management Page
- [ ] Create `frontend/src/app/admin/students/page.tsx`:
  - Display the student list page.
  - Reuse original layout from `/admin/users/page.tsx` but filter data client-side for `roles.includes('Student')`.
  - Retain student-specific filter selectors: Stage, Grade, Track, Gender, Governorate.
  - Set page title to "الطلاب".
  - Remove the role filter tabs (الكل, المديرين, المساعدين, الطلاب) as this page only shows students.
  - Update the row click handler to navigate to `/admin/users/${u.id}` (profile).
  - Enforce role 'Student' when using the `AddUserDrawer` to create a new user.
  - Update the Export CSV functionality to only export students.

### Task 4: Create Assistants Management Page
- [ ] Create `frontend/src/app/admin/assistants/page.tsx`:
  - Display the assistant/employee list page.
  - Filter user data client-side for `roles.includes('Assistant') && !roles.includes('Admin')`.
  - Display table columns: Assistant Name, Phone, Status, Last Activity, and Actions. Do not display student stage/grade.
  - Remove student-specific filter selectors (Grade, Stage, etc.). Keep the search bar and status toggle.
  - Set page title to "المساعدين".
  - Remove role filter tabs.
  - On row click, open `AssistantProfileModal`.
  - Enforce role 'Assistant' when using `AddUserDrawer` to create a user.
  - Update the Export CSV functionality to export only assistants.

### Task 5: Create Admins Management Page
- [ ] Create `frontend/src/app/admin/admins/page.tsx`:
  - Display the administrator list page.
  - Filter user data client-side for `roles.includes('Admin')`.
  - Display columns: Name, Phone, Status, Last Activity, Actions.
  - Remove student-specific filter selectors. Keep search and status toggle.
  - Set page title to "المديرين".
  - Remove role filter tabs.
  - On row click, do not perform any action (or open an edit drawer if available).
  - Enforce role 'Admin' when using `AddUserDrawer`.
  - In `handleToggleStatus`, add client-side check to prevent disabling the logged-in administrator's account or general safety rules.
  - Update the Export CSV functionality to export only admins.

### Task 6: E2E Tests Alignment
- [ ] In `frontend/tests/e2e/admin-users.spec.ts`:
  - Update E2E tests to point to `/admin/students` instead of `/admin/users`.
  - Verify that the E2E tests pass.

---

## Quality-Gate Tail Tasks
- [ ] Execute `clean-code-guard` on all created and modified files.
- [ ] Execute `test-guard` on modified E2E test files.
- [ ] Verify frontend build successfully compiles without warnings or errors.
- [ ] Run endpoint inventory check: `node scripts/generate-endpoint-inventory.mjs --check`
- [ ] Run python endpoint inventory test: `python3 -m pytest tests/test_endpoint_inventory.py -v`


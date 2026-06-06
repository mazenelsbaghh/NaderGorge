# Feature Specification: Admin Roles and Settings / الأدوار وإعدادات المنصة للادمن

**Feature Branch**: `085-admin-roles-settings`  
**Created**: 2026-06-06  
**Status**: Draft  
**Input**: User description: "ظبطلي صفحه الاعدات بتاعت الادمن بقي و ضفلي انو يعمل ادوار معينه علشان لمي اضيف مستدخم احدد انا عايز يعمل اي بالظبط و اخحدد وانا بعملوا ياخد انهي رصلاحيه من اللي انشتها و ضفلي اهم حاجات لازم تبقي ف الاعدات بتاعت الادمن"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Platform Settings Management / إدارة إعدادات المنصة العامة (Priority: P1)

Admin wants to customize the platform-wide information, support links, device limits, video protection, and platform status.

**Why this priority**: It is essential for platform administration, branding, student support, security (device limits), and maintenance.

**Independent Test**: Admin navigates to Settings page, modifies settings values, clicks Save, and sees a success feedback. Reloading the page or visiting the homepage reflects the new settings values.

**Acceptance Scenarios**:

1. **Given** Admin is logged in and visits `/admin/settings`, **When** the page loads, **Then** all existing settings values are displayed.
2. **Given** Admin modifies values (Platform Name, Max Active Devices, WhatsApp Link, YouTube/Telegram links, Enable Watermark, Maintenance Mode), **When** Admin clicks "حفظ الإعدادات", **Then** the updates are saved, cache is invalidated, and a success toast is shown.
3. **Given** Maintenance Mode is enabled and a maintenance message is set, **When** a student visits the site, **Then** they are blocked by a fullscreen maintenance page with the specified message.

---

### User Story 2 - Roles & Permissions Creation / إنشاء وإدارة الأدوار والصلاحيات (Priority: P1)

Admin wants to define distinct roles with selected permissions so that assistant accounts can have restricted access to specific modules.

**Why this priority**: Enables fine-grained authorization control, removing full-admin risk from assistant staff.

**Independent Test**: Admin navigates to Settings → Roles section, clicks "إنشاء دور جديد", enters "مساعد مراجعة", selects "إدارة وتصفية التعليقات" and "إدارة مجتمع الطلاب", and submits. The role is saved and listed.

**Acceptance Scenarios**:

1. **Given** Admin is in the Roles tab, **When** they click "إضافة دور جديد", **Then** a drawer/modal opens with a Title field and a checklist of permissions.
2. **Given** Admin selects a list of permissions and clicks save, **When** the request completes, **Then** the role is saved and added to the database roles list.
3. **Given** Admin edits an existing role, **When** they modify its permissions and save, **Then** the role updates and any users assigned to this role will have their access levels updated.
4. **Given** Admin attempts to delete a role, **When** no users are currently assigned to this role, **Then** the role is deleted. If users are assigned, **Then** deletion is prevented with an appropriate warning message.

---

### User Story 3 - Select Custom Role when Adding User / اختيار الدور عند إضافة مستخدم (Priority: P1)

Admin wants to assign a custom role when creating a new administrative user, so they inherit the specific permissions defined for that role.

**Why this priority**: Required to actually bind created roles to users during creation/editing.

**Independent Test**: Admin opens the "إضافة مستخدم" drawer, selects user type "مساعد/إداري", is presented with a dropdown of all roles (Admin, Teacher, Student, plus custom roles like "مساعد مراجعة"), selects the custom role, and creates the user.

**Acceptance Scenarios**:

1. **Given** Admin clicks "إضافة مستخدم" in `/admin/users`, **When** the user role field is selected as Admin or Assistant, **Then** a list of dynamically fetched Roles is displayed in a dropdown.
2. **Given** Admin selects a custom role and fills the required info, **When** they click submit, **Then** the user is created with the exact selected role assigned in the database.

---

### Edge Cases

- **Duplicate Role Name**: Admin tries to create a role with a name that already exists (e.g. "Admin" or "مساعد"). System must return a clear validation error "اسم الدور مسجل بالفعل".
- **Deleting System Roles**: System must protect default system roles ("Admin", "Teacher", "Student") from being deleted or edited.
- **Permission Check Latency**: When a logged-in assistant's role permissions are modified, their token or session needs to be updated. Since JWT token roles are used, the user will get updated access upon token refresh or relogging (acceptable behavior), or we validate permissions dynamically per request in backend filters.
- **Maintenance Mode Bypass**: Administrators and Teachers must be able to bypass the maintenance screen so they can continue testing and managing the platform.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support platform-wide configuration settings stored in `PlatformSettings` table:
  - `PlatformName` (string)
  - `SupportPhoneNumber` (string)
  - `SupportWhatsAppUrl` (string)
  - `YouTubeChannelUrl` (string)
  - `TelegramChannelUrl` (string)
  - `MaxActiveDevicesPerStudent` (integer)
  - `EnableWatermark` (boolean)
  - `WatermarkOpacity` (decimal)
  - `MaintenanceMode` (boolean)
  - `MaintenanceMessage` (string)
- **FR-002**: Student frontend MUST display the fullscreen maintenance page if `MaintenanceMode` is set to true (unless the user is an Admin/Teacher).
- **FR-003**: System MUST store Roles in the database. Each Role has a Name, RoleType (e.g., Assistant for custom roles), and a JSON-encoded array of permissions: `PermissionsJson`.
- **FR-004**: System MUST define the following system permissions (in Arabic/English):
  - `users.manage` (إدارة الطلاب والمستخدمين)
  - `content.manage` (إدارة الكورسات والمحتوى)
  - `exams.manage` (إدارة الامتحانات والأسئلة)
  - `settings.manage` (إدارة إعدادات المنصة)
  - `codes.manage` (توليد وإدارة الأكواد)
  - `watch_requests.manage` (إدارة طلبات المشاهدة)
  - `community.manage` (إدارة مجتمع الطلاب)
  - `comments.manage` (إدارة وتصفية التعليقات)
- **FR-005**: Admin Settings UI MUST allow:
  - Listing all roles.
  - Adding a new role with a list of selected permissions.
  - Editing an existing role.
  - Deleting a custom role.
- **FR-006**: Add User drawer MUST fetch the list of dynamic roles from `GET /admin/roles` and list them for Admin/Assistant creation, assigning the chosen Role ID or Name to the user.
- **FR-007**: Backend API endpoints MUST perform permission checks based on the logged-in user's assigned roles and permissions (e.g., checking if the user's role has the required permission for the action, or fallback to Admin/Teacher checks).

### Key Entities *(include if feature involves data)*

- **PlatformSetting**: Represents a system-wide setting with key and value.
- **Role**: Represents a user role, with:
  - `Id` (Guid)
  - `Name` (string, unique name of role, e.g. "مساعد مراجعة")
  - `Type` (RoleType enum: Admin, Teacher, Assistant, Student)
  - `PermissionsJson` (string, JSON list of assigned permission keys like `["community.manage", "comments.manage"]`)
- **UserRole**: Junction table linking `User` and `Role`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can customize settings and see changes take effect instantly across the platform.
- **SC-002**: Creating and assigning a custom role restricts or grants access to the specified modules without exceptions.
- **SC-003**: Dynamic roles are fetched and listed in under 200ms in the user management drawers.

## Assumptions

- We will store the permissions as a JSON array string directly in the existing `roles` table using a new column `PermissionsJson` to avoid creating a new table and joins, maintaining performance.
- Database schema migration will be performed using Entity Framework Core migrations.
- Permissions checks in the backend will inspect the database user roles and their associated `PermissionsJson` to authorize endpoint calls.

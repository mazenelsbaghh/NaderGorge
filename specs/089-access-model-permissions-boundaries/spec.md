# Feature Specification: Phase 1 - Access Model, Staff Surfaces, and Permission Boundaries

**Feature Branch**: `089-access-model-permissions-boundaries`  
**Created**: 2026-06-07  
**Status**: Draft  
**Input**: User description: "Phase 1 - Access Model, Staff Surfaces, and Permission Boundaries. Setup permissions and roles (Super Admin, Supervisor, Staff, Teacher, Assistant, Student) before building HR/CRM/Finance/Chat modules. Protect routes and data based on fine-grained permissions."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add Supervisor and Staff System Roles (Priority: P1)

As an Administrator, I want the system to recognize `Supervisor` and `Staff` as standard roles in the database and enum lists so that I can create users with these roles.

**Why this priority**: Necessary to bootstrap the operational user hierarchy before building HR, CRM, and financial tracking pages.

**Independent Test**: Admin opens the "إضافة مستخدم" drawer in the Admin portal, selects "مشرف العمليات (Supervisor)" or "موظف المنصة (Staff)", and successfully creates the user.

**Acceptance Scenarios**:

1. **Given** the user management panel, **When** creating a new user, **Then** "Supervisor" and "Staff" are available in the role dropdown.
2. **Given** a new user created with role "Supervisor", **When** checking database `user_roles`, **Then** the user is correctly linked to the `Supervisor` role type.

---

### User Story 2 - Register Permissions for HR, CRM, Finance, Tasks, Chat, Media & Audits (Priority: P1)

As an Administrator, I want to define and manage new operational permissions in the roles page so I can grant granular access for upcoming modules.

**Why this priority**: Required to restrict access to sensitive HR files, financial logs, and CRM records.

**Independent Test**: Admin navigates to `/admin/settings` → "الأدوار والصلاحيات" tab, edits or creates a role, and sees the new checkboxes (e.g. `hr.manage`, `finance.manage`, `crm.manage`).

**Acceptance Scenarios**:

1. **Given** Admin is editing a role, **When** the permission checklist loads, **Then** all 8 new permissions are displayed with clear Arabic labels and descriptions:
   - `hr.manage` (إدارة الموارد البشرية والموظفين)
   - `tasks.manage` (إدارة مهام المساعدين والعمليات)
   - `chat.manage` (المحادثات وإشعارات الموظفين)
   - `crm.manage` (إدارة علاقات الطلاب والمكالمات)
   - `payments.manage` (أتمتة المدفوعات والرسائل)
   - `media.manage` (خط إنتاج الحصص والنشر)
   - `finance.manage` (الماليات والرواتب وأكواد المدرسين)
   - `reports.manage` (سجل الرقابة والتقارير التشغيلية)
2. **Given** Admin saves the modified role permissions, **When** calling the backend API, **Then** the updated list of permissions is serialized and stored in `PermissionsJson` for the role.

---

### User Story 3 - Role-Based Interface and Menu Restrictions (Priority: P2)

As a Staff or Supervisor user, I want the Admin sidebar and navigation options to only display items that my role has permissions to access.

**Why this priority**: Improves user experience and enforces security by hiding unauthorized links before users click them.

**Independent Test**: Log in as a staff member whose role lacks the `settings.manage` permission and verify the "الإعدادات" link is hidden from the sidebar.

**Acceptance Scenarios**:

1. **Given** a logged-in user, **When** rendering the Admin sidebar, **Then** menu links (Users, Content, Settings, etc.) are filtered using the user's permission claims.
2. **Given** a user is logged in with custom permissions, **When** they try to access a route directly (e.g. `/admin/settings`) that they lack permission for, **Then** they are redirected to a "غير مصرح" (Unauthorized) page or the login screen.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Backend MUST update `RoleType` enum to include:
  - `Supervisor = 7`
  - `Staff = 8`
- **FR-002**: Database seeder and EF Core migrations MUST register the new roles in the `roles` table.
- **FR-003**: Frontend MUST register the new permission definitions in `PERMISSION_DEFINITIONS`:
  - `hr.manage`
  - `tasks.manage`
  - `chat.manage`
  - `crm.manage`
  - `payments.manage`
  - `media.manage`
  - `finance.manage`
  - `reports.manage`
- **FR-004**: Frontend MUST implement a check/helper hook (e.g. `useHasPermission`) that verifies if a user has a specific permission claim.
- **FR-005**: Admin Sidebar menu definition MUST support optional `permission` fields to dynamically filter items.
- **FR-006**: Backend API Controllers MUST support `[HasPermission("...")]` filtering for the new permission keys.

### Key Entities

- **Role / UserRole**: Exists. Updates enum value fields and default db records.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Dynamic role creation and assignment saves the new role and permissions successfully.
- **SC-002**: Sidebar filtration and route access changes take effect immediately on login / token refresh.
- **SC-003**: API requests return `403 Forbidden` if the user lacks the required permission claim.

## Assumptions

- Admins (`RoleType.Admin`) and Teachers (`RoleType.Teacher`) continue to bypass all fine-grained permission checks, having unrestricted access.
- Permissions are stored in the JWT access token as claims.
- Front-end routes are protected using Next.js page guards.

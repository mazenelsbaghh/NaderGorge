# Feature Specification: Separate User Lists & Remove General Users Page (إدارة الطلاب والموظفين المفصلة)

**Feature Branch**: `104-separate-user-lists`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "انا عايز اشيل اليوزر دي اصلا تبقي طلاب بقي و البروفيل بتاع الطالب زي ماهو فاهمني يعني اشيل حوار اليوزر لاني هفصلهم كلهم عن بعض"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Dedicated Students Management Page (Priority: P1)

As an Admin, I want to manage student accounts on a dedicated page titled "الطلاب" (Students) under `/admin/students`, which replaces the general users page. This page should list ONLY students, show student-specific stats, and have student-specific filters, with no generic "User Role" tabs.

**Why this priority**: Removing the generic "Users" list and replacing it with a clean "Students" list makes the page dedicated to student affairs, simplifying the most common admin workflow.

**Independent Test**: Admin clicks on "الطلاب" in the sidebar, verifies only students are shown (no role tabs for Admins/Assistants), and filters them by grade.

**Acceptance Scenarios**:

1. **Given** Admin is on `/admin/students`, **When** they view the page, **Then** they see the title "الطلاب", student count stats, student list table, and NO role filters.
2. **Given** Admin is on `/admin/students`, **When** they click on a student row, **Then** they are navigated to the student profile page `/admin/users/[id]`.
3. **Given** Admin is on `/admin/students`, **When** they click "إضافة طالب" (or "إضافة مستخدم"), **Then** the add drawer opens with the "Student" role pre-selected/enforced.

---

### User Story 2 - Dedicated Assistants & Admins Pages (Priority: P2)

As an Admin, I want to manage internal assistants and system administrators on separate dedicated pages (`/admin/assistants` and `/admin/admins`), completely decoupled from the student database.

**Why this priority**: Separating employees/staff from students ensures clean role-based operations and avoids exposing student-specific UI filters on staff lists.

**Independent Test**: Admin navigates to `/admin/assistants`, verifies that no student-specific filters exist, and sees only assistant users.

**Acceptance Scenarios**:

1. **Given** Admin is on `/admin/assistants`, **When** they add a user, **Then** the role is set to `Assistant`.
2. **Given** Admin is on `/admin/admins`, **When** they view the list, **Then** they only see users with the `Admin` role.

---

### User Story 3 - Navigation & Redirects Cleanup (Priority: P1)

As an Admin, I want the sidebar and breadcrumbs to completely remove any reference to "المستخدمين" (Users), replaced by "الطلاب", "المساعدين", and "المديرين". Any direct request to `/admin/users` must redirect to `/admin/students`.

**Why this priority**: Ensures users do not see broken links or outdated layout screens.

**Independent Test**: Admin enters `/admin/users` in the URL bar and is redirected to `/admin/students`.

**Acceptance Scenarios**:

1. **Given** Admin tries to navigate to `/admin/users`, **When** the page loads, **Then** they are redirected automatically to `/admin/students`.
2. **Given** Admin is on the Student Profile page (`/admin/users/[id]`), **When** they click "العودة للقائمة" (Back to list), **Then** they are taken to `/admin/students`.

### Edge Cases

- **Student Profile Path stability**: The path `/admin/users/[id]` is preserved exactly as is to prevent breaking links elsewhere (e.g. from access codes), but the sidebar has "الطلاب" highlighted, and the breadcrumbs show it is nested under "الطلاب".
- **Disabling own admin account**: Admin cannot disable their own account on the `/admin/admins` page.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Redirect**: Access `/admin/users`, verify redirection to `/admin/students`.
- **Manual QA Sidebar**: Verify sidebar has "الطلاب", "المساعدين", "المديرين", and "المعلمين" and has NO general "المستخدمين".
- **Docker Acceptance**: Verify build success, clean routing, and no warnings/errors on container launch.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Route `/admin/students` MUST show only `Student` accounts.
- **FR-002**: Route `/admin/assistants` MUST show only `Assistant` accounts (excluding Admins).
- **FR-003**: Route `/admin/admins` MUST show only `Admin` accounts.
- **FR-004**: Route `/admin/users` MUST redirect to `/admin/students`.
- **FR-005**: Sidebar menu and breadcrumbs MUST NOT contain "المستخدمين" and MUST include separate items for "الطلاب", "المساعدين", "المديرين", and "المعلمين".
- **FR-006**: Student Profile page `/admin/users/[id]` back-button MUST navigate to `/admin/students`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Sidebar has zero references to "المستخدمين".
- **SC-002**: Redirection from `/admin/users` works under 100ms.
- **SC-003**: No TypeScript build errors or warnings in Next.js build.

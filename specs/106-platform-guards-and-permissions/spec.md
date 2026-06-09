# Feature Specification: Platform Guards and Permissions

**Feature Branch**: `106-platform-guards-and-permissions`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 1 - Guards, Permissions, Routing, Domains: Block wrong access and separate domains before developing pages."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Surface Boundary Enforcement (Priority: P1)

As the platform operator, I want each user role (Student, Teacher, Assistant/Staff, Admin, Supervisor) to be restricted exclusively to their corresponding domain surface, so that users cannot access administrative or educational interfaces they are not authorized for.

**Why this priority**: Crucial for system security and role-based access isolation. This forms the foundational barrier separating students, teachers, assistants, and administrators.

**Independent Test**: Can be tested by logging in as a Student and attempting to navigate to the Admin, Teacher, or Staff domains/ports, verifying that the user is blocked or redirected.

**Acceptance Scenarios**:

1. **Given** a logged-in user with the "Student" role, **When** they try to access the Admin domain (`admin.localhost:8740`) or Teacher domain (`teacher.localhost:8741`) or Staff domain (`staff.localhost:8742`), **Then** they must be redirected back to the Student domain (`app.localhost:8739`) or receive a 403 Forbidden.
2. **Given** a logged-in user with the "Teacher" role, **When** they try to access the Admin domain or Student domain or Staff domain, **Then** they must be redirected to the Teacher domain or receive a 403 Forbidden.
3. **Given** a logged-in user with the "Assistant" or "Staff" role, **When** they try to access the Admin domain (without Supervisor permissions) or Student/Teacher domains, **Then** they must be redirected to the Staff/Assistant domain or receive a 403 Forbidden.
4. **Given** a logged-in user with the "Admin" or "Supervisor" role, **When** they try to access the Student, Teacher, or Staff domains, **Then** they must be allowed or redirected to the Admin domain based on their request, but their access to the Admin surface must be verified.

---

### User Story 2 - strict Permission Evaluation and Admin Bypass (Priority: P2)

As a platform owner, I want only users with the exact "Admin" role to bypass permission checks automatically, whereas all other roles (including Supervisor, Teacher, Assistant, and Staff) must have their permissions explicitly checked against their stored scopes/claims.

**Why this priority**: Prevents privilege escalation where non-Admin roles (like Teacher or Supervisor) bypass permission checks, ensuring fine-grained access control is strictly enforced.

**Independent Test**: Log in as a Supervisor/Teacher with limited permissions, try to perform a restricted action (e.g., viewing settings), and verify that access is denied unless the specific permission is assigned.

**Acceptance Scenarios**:

1. **Given** a user with the "Admin" role, **When** they access any administrative component or perform any action, **Then** the permission check must automatically pass (bypass).
2. **Given** a user with the "Supervisor" role, **When** they access a component requiring a specific permission (e.g., `ViewSettings`), **Then** the check must only pass if they explicitly possess that permission in their profile.
3. **Given** a user with the "Teacher" role, **When** they access a backend API endpoint requiring permissions, **Then** the check must evaluate their explicit permissions rather than granting an automatic bypass.

---

### User Story 3 - Role-Based Login Redirects (Priority: P3)

As a user logging into the platform, I want to be automatically redirected to my designated surface domain/port based on my account role immediately after a successful login.

**Why this priority**: Enhances the user experience by routing users directly to their workspace, preventing them from landing on incorrect or forbidden surfaces.

**Independent Test**: Perform login with different accounts (Student, Teacher, Assistant, Admin) from the common login page and verify the final URL domain/port matches their role.

**Acceptance Scenarios**:

1. **Given** a Student logs in successfully, **When** the login process completes, **Then** the user must be redirected to the Student domain (`app.localhost:8739`).
2. **Given** a Teacher logs in successfully, **When** the login process completes, **Then** the user must be redirected to the Teacher domain (`teacher.localhost:8741`).
3. **Given** an Assistant or Staff logs in successfully, **When** the login process completes, **Then** the user must be redirected to the Assistant/Staff domain (`staff.localhost:8742`).
4. **Given** an Admin or Supervisor logs in successfully, **When** the login process completes, **Then** the user must be redirected to the Admin/Supervisor domain (`admin.localhost:8740`).

---

### Edge Cases

- **Session Expiry / Unauthenticated Access**: If a user's session expires or they are unauthenticated, they must be redirected to the login page on the Student domain, regardless of which domain they tried to access, and the system must securely preserve their target redirect path.
- **Multiple Roles Assigned**: If a user somehow holds multiple roles (e.g., Staff and Supervisor), the system must prioritize the highest-privileged surface (Admin/Supervisor) or provide a fallback surface based on a deterministic priority order (Admin > Supervisor > Staff/Assistant > Teacher > Student).
- **Subdomain Routing in Local Development vs. Production**: In local development, port-based routing is used (`:8738` for landing, `:8739` for student, etc.). In production, subdomain-based routing is used (`massar-academy.net`, `app.massar-academy.net`, `admin.massar-academy.net`, etc.). The routing guards must handle both configurations transparently.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin, navigate to `/admin/students` on `admin.localhost:8740`, verify page loads. Try to open `teacher.localhost:8741/teacher` as Admin, verify redirect or 403.
- **Manual QA Role/Flow 2**: Student, navigate to `app.localhost:8739`, login, verify redirect to `/student`. Try to open `admin.localhost:8740/admin`, verify redirect back to `app.localhost:8739` or 403.
- **Manual QA Role/Flow 3**: Teacher, login on `teacher.localhost:8741`, verify redirect to `/teacher`. Try to open `admin.localhost:8740/admin`, verify redirect or 403.
- **Manual QA Negative Check**: A Supervisor without `ManageUsers` permission tries to load `/admin/users` or call the corresponding API endpoint, and must receive a 403 Forbidden.
- **Docker Acceptance**: `docker compose ps` shows services `landing`, `student`, `admin`, `teacher`, and `staff` (or `assistant`) running. Nginx configuration successfully routes `teacher.localhost` to the teacher service, and `staff.localhost` or `assistant.localhost` to the staff/assistant service.
- **External Dependencies**: Local Docker network, Nginx configurations, and localhost hosts entries (`127.0.0.1` mapping for subdomains).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST enforce role boundaries at the frontend routing level. Specific guards (`StudentGuard`, `TeacherGuard`, `AssistantGuard`, `StaffGuard`, `AdminGuard`) must be active for their respective layout hierarchies.
- **FR-002**: The frontend helper/hook `useHasPermission` MUST evaluate permissions strictly for all roles except "Admin", which has automatic bypass.
- **FR-003**: The backend authorization filter (`HasPermissionAttribute`) MUST evaluate permissions strictly for all roles except "Admin", which has automatic bypass.
- **FR-004**: Login redirection logic MUST evaluate the authenticated user's role and redirect to the corresponding public origin (domain/port) defined in the configuration.
- **FR-005**: Docker Compose configuration MUST run five independent frontend surface containers (`landing`, `student`, `admin`, `teacher`, `staff`) with distinct public ports and environment variables.
- **FR-006**: Nginx proxy configuration MUST route incoming domains to their corresponding backend/frontend services.
- **FR-007**: Verification script `scripts/verify-surface-separation.mjs` MUST be updated to validate the 5-surface configuration and test boundary enforcement.

### Key Entities *(include if feature involves data)*

- **User**: Represents a registered user on the platform. Has a `Role` (Student, Teacher, Assistant, Staff, Supervisor, Admin) and a collection of `Permissions`.
- **LessonVideo**: Represents educational content, which must enforce access constraints based on user role and enrollment.
- **AccessCode**: Relates to package redemption and access control validation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of unauthorized route accesses between domains must be blocked and redirected to the appropriate home domain or rejected with a 403 status code.
- **SC-002**: All backend API endpoints decorated with role-based permission checks must correctly reject requests from Supervisors/Teachers/Assistants lacking explicit permissions, with a 403 status code.
- **SC-003**: The verification script `scripts/verify-surface-separation.mjs` must run successfully and pass all 5-surface domain checks.
- **SC-004**: The complete frontend test and production builds (`npm run build`) must compile without any warnings or errors.

## Assumptions

- We assume the database has a reliable mechanism to assign/store roles and permissions for users (which is already implemented via the `Role` column and `Permissions` JSON properties).
- We assume that localhost subdomains (e.g., `student.localhost`, `teacher.localhost`, `admin.localhost`) can be resolved locally or that port-based routing is sufficient for local development verification.

# Feature Specification: Surface Login and Access Contract

**Feature Branch**: `112-surface-login-access-contract`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 1 - Surface Login and Access Contract"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Surface-Specific Login Gate (Priority: P1)

As an unauthenticated visitor to a subdomain (e.g., student, teacher, staff, or admin portal), I want to be redirected to the login page of that specific subdomain, and see a login screen customized for that portal.

**Why this priority**: Crucial for security and role isolation. Users must not see a generic login screen that accepts any credentials without indicating which portal they are entering.

**Independent Test**:
Visit `teacher.massar-academy.net/teacher/packages` while logged out. The system should redirect to `teacher.massar-academy.net/login` and show "بوابة المعلم" (Teacher Gateway).

**Acceptance Scenarios**:
1. **Given** the user is not logged in, **When** they visit `app.massar-academy.net/student/packages`, **Then** they are redirected to `app.massar-academy.net/login` with the title "بوابة الطالب".
2. **Given** the user is not logged in, **When** they visit `teacher.massar-academy.net/teacher/finance`, **Then** they are redirected to `teacher.massar-academy.net/login` with the title "بوابة المعلم".
3. **Given** the user is not logged in, **When** they visit `staff.massar-academy.net/assistant/tasks`, **Then** they are redirected to `staff.massar-academy.net/login` with the title "بوابة المساعدين والموظفين".
4. **Given** the user is not logged in, **When** they visit `admin.massar-academy.net/admin/users`, **Then** they are redirected to `admin.massar-academy.net/login` with the title "بوابة الإدارة".

---

### User Story 2 - Role-Based Dashboard Redirection & Session Re-entry (Priority: P1)

As a logging-in user or a user with an active session visiting `/login`, I want to be directed to the correct dashboard matching my role, and prevented from entering portals that do not belong to my role.

**Why this priority**: Essential to ensure correct landing after login and to handle users returning to the login page with an active session.

**Independent Test**:
Log in as a Student. Visited `app.massar-academy.net/login` while logged in. The system should redirect you to `app.massar-academy.net/student`.

**Acceptance Scenarios**:
1. **Given** a user is logged in as a Teacher, **When** they visit `teacher.massar-academy.net/login`, **Then** they are automatically redirected to `teacher.massar-academy.net/teacher`.
2. **Given** a user is logged in as an Assistant/Staff, **When** they visit `staff.massar-academy.net/login`, **Then** they are automatically redirected to `staff.massar-academy.net/assistant`.
3. **Given** a user is logged in as a Student, **When** they visit `app.massar-academy.net/login`, **Then** they are automatically redirected to `app.massar-academy.net/student`.
4. **Given** a user is logged in as an Admin/Supervisor, **When** they visit `admin.massar-academy.net/login` or `super.massar-academy.net/login`, **Then** they are automatically redirected to `admin.massar-academy.net/admin` (or `super` mapping).

---

### User Story 3 - Return URL Validation (Priority: P2)

As a user logging in, I want the `returnUrl` query parameter to be validated so that I am only redirected to a route within the current surface that is permitted for my role.

**Why this priority**: Prevents open redirect vulnerabilities and cross-role boundary leaks via query parameters.

**Independent Test**:
Visit `app.massar-academy.net/login?returnUrl=https://admin.massar-academy.net/admin/users`. Log in as a Student. The system should ignore the external/unauthorized returnUrl and redirect to the Student dashboard instead.

**Acceptance Scenarios**:
1. **Given** a student is logging in at `app.massar-academy.net/login?returnUrl=/student/packages`, **When** they log in successfully, **Then** they are redirected to `/student/packages`.
2. **Given** a student is logging in at `app.massar-academy.net/login?returnUrl=https://admin.massar-academy.net/admin/finance`, **When** they log in successfully, **Then** the returnUrl is rejected and they are redirected to `/student`.

---

### User Story 4 - Cross-Surface Access Prevention & Custom 404/Error (Priority: P2)

As a logged-in user or visitor, if I attempt to access a route belonging to another surface on my current subdomain (e.g. accessing `/admin` on `app.massar-academy.net`), I want the server/client to return a branded Not Found/Forbidden error page rather than silently redirecting me to the other domain.

**Why this priority**: Prevents information disclosure and keeps the user within their domain boundaries.

**Independent Test**:
Navigate to `app.massar-academy.net/admin`. The system should display a branded "الصفحة غير موجودة أو لا تخص هذا الحساب" (Page not found or does not belong to this account) error page on the current subdomain.

**Acceptance Scenarios**:
1. **Given** a student is logged in, **When** they navigate to `app.massar-academy.net/admin`, **Then** they are shown a custom 404 error page on `app.massar-academy.net` instead of being redirected to `admin.massar-academy.net`.
2. **Given** a teacher is logged in, **When** they navigate to `teacher.massar-academy.net/admin`, **Then** they are shown a custom 404 error page on `teacher.massar-academy.net` instead of being redirected to `admin.massar-academy.net`.

### Edge Cases

- **Session Expiry**: When a session expires while on a protected page, the user should be redirected to the login page of the *current* subdomain.
- **Supervisor Role Boundaries**: The supervisor role behaves as a restricted admin. They must access via `admin.massar-academy.net` or `super.massar-academy.net`, and their access is routed to `/admin` dashboard but with restricted permissions.
- **Root Domain Login**: When a user visits the root landing domain `/login`, they should be redirected to the student app domain login (`app.massar-academy.net/login`).

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Student Flow**: Visit `app.massar-academy.net/login`. Verify the screen is branded "بوابة الطالب". Log in, verify redirection to `/student`. Try to open `/admin` in the address bar; verify a 404/not-found screen appears.
- **Manual QA Teacher Flow**: Visit `teacher.massar-academy.net/login`. Verify the screen is branded "بوابة المعلم". Log in, verify redirection to `/teacher`.
- **Manual QA Negative Check**: Log in as a Student. Go to `app.massar-academy.net/login`. Verify it automatically redirects to `/student` without showing the form. Try to visit `/login` on `teacher.massar-academy.net` and verify it keeps you in the login flow or redirects appropriately based on roles.
- **Docker Acceptance**: Run `docker compose up -d` and ensure all 5 frontend configurations start properly and load local configs correctly.
- **External Dependencies**: None. All logic is self-contained.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST show subdomain-specific branding (Student, Teacher, Assistant/Staff, Admin) on the login page based on the current domain/surface.
- **FR-002**: System MUST validate `returnUrl` query parameter: it must only point to routes belonging to the active surface and role of the authenticated user.
- **FR-003**: System MUST redirect logged-in users to the correct dashboard matching their role when visiting `/login` or after a successful login.
- **FR-004**: System MUST NOT perform silent redirects across domains when a wrong surface route is requested. Instead, it MUST return a 404/Not Found or a custom Forbidden screen.
- **FR-005**: Custom Not Found/Forbidden error pages MUST be created for each surface displaying a clear message: "الصفحة غير موجودة أو لا تخص هذا الحساب".

### Key Entities

This feature operates on the existing `User` and `Role` models. No new database entities are introduced.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of unauthorized cross-surface route requests on subdomains yield a 404/Forbidden page on that subdomain instead of a redirect.
- **SC-002**: 100% of unauthenticated requests to protected subdomains redirect to the login page of the exact same subdomain.
- **SC-003**: 100% of `returnUrl` redirects are validated, preventing open-redirection and cross-surface privilege escalation.

## Assumptions

- We assume that `APP_SURFACE` (or `NEXT_PUBLIC_APP_SURFACE`) is correctly configured for each Docker service in production.
- We assume that the user's role is stored in the client-side `useAuthStore` state and is validated against the backend.

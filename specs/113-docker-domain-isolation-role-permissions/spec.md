# Feature Specification: Docker/Domain Isolation and Role Permissions

**Feature Branch**: `113-docker-domain-isolation`  
**Created**: 2026-06-10  
**Status**: Draft  
**Input**: User description: "Phase 2 - Docker/Domain Isolation and Role Permissions"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Environment and Port Isolation (Priority: P1)

As a system administrator, I want each surface (Student, Teacher, Assistant, Admin, and Landing) to run on its own domain, port, and environment settings, so that they are fully decoupled and isolated from each other.

**Why this priority**: Core infrastructure prerequisite. Domain separation is critical to enforce role boundaries and protect user data across surfaces.

**Independent Test**:
Deploy using the isolated configurations and verify that each surface (e.g., student at `app.massar-academy.net`, teacher at `teacher.massar-academy.net`, etc.) loads with its respective environment variables.

**Acceptance Scenarios**:
1. **Given** the Docker Compose setup is running, **When** a user accesses the landing page, **Then** it serves from the root domain `massar-academy.net` (port 8738 in dev).
2. **Given** the Docker Compose setup is running, **When** a user accesses the student page, **Then** it serves from `app.massar-academy.net` (port 8739 in dev).
3. **Given** the Docker Compose setup is running, **When** a user accesses the admin page, **Then** it serves from `admin.massar-academy.net` (port 8740 in dev).
4. **Given** the Docker Compose setup is running, **When** a user accesses the teacher page, **Then** it serves from `teacher.massar-academy.net` (port 8741 in dev).
5. **Given** the Docker Compose setup is running, **When** a user accesses the assistant page, **Then** it serves from `staff.massar-academy.net` (port 8742 in dev).

---

### User Story 2 - Cross-Surface Access Boundaries (Priority: P1)

As a user logged into a specific subdomain, I want to be strictly blocked from accessing other portals (e.g., a student visiting admin pages, or a teacher visiting assistant tasks) so that my session is confined to my role's boundary.

**Why this priority**: Critical security requirement to prevent privilege escalation and unauthorized route leakage.

**Independent Test**:
Log in as a Student and attempt to navigate directly to `/admin/content` or `/teacher/packages`. Verify that a custom 404 page is rendered on the student subdomain.

**Acceptance Scenarios**:
1. **Given** a student is logged in, **When** they attempt to load `app.massar-academy.net/admin`, **Then** the page loads a custom 404 on `app.massar-academy.net` and does not leak admin information.
2. **Given** a teacher is logged in, **When** they attempt to load `teacher.massar-academy.net/assistant/tasks`, **Then** the page loads a custom 404 on `teacher.massar-academy.net`.
3. **Given** an assistant is logged in, **When** they attempt to load `staff.massar-academy.net/admin`, **Then** the page loads a custom 404 on `staff.massar-academy.net`.

---

### User Story 3 - Role-Based Permission Enforcement (Priority: P1)

As a logged-in staff/assistant or teacher, I want the system to restrict my page access and API actions to only those permitted by my permission matrix, so that I cannot perform unauthorized administrative tasks.

**Why this priority**: Required to validate granular access control rules for employee roles (e.g., Assistant vs. Supervisor vs. Admin) and teacher bindings.

**Independent Test**:
Run Playwright tests simulating different assistant permissions and verify that operations (like CRM status updates or package creation) are permitted or forbidden based on their roles.

**Acceptance Scenarios**:
1. **Given** an Assistant with restricted CRM permissions, **When** they try to access the CRM lead management dashboard, **Then** they are blocked or shown an unauthorized message.
2. **Given** a Teacher profile, **When** they try to access content that they are not bound to, **Then** they are blocked.

---

### Edge Cases

- **CORS Configuration Cleanup**: Old domains and configurations must be removed from CORS settings in both backend API configuration and Nginx setup. They must either be entirely rejected or redirected.
- **Supervisor Role Handling**: Supervisors act as restricted administrators. They must access the admin portal (`admin.massar-academy.net`) but their permission claims must be strictly evaluated to restrict access to sensitive settings.
- **Local Dev Port Fallbacks**: Local development configurations must properly map subdomains (e.g., `app.localhost:3000`, `admin.localhost:3000`) and handle port isolation cleanly without causing cross-origin session collision.

---

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Domain Separation**: Start the services using Docker. Visit `app.massar-academy.net` and ensure it responds on port 8739. Repeat for all 5 subdomains.
- **Docker Compose Validation**: Run `docker compose config -q` to verify separate Docker configs, environments, and host rules.
- **E2E Role Verification**: Run Playwright test suite to verify student, teacher, assistant, and supervisor permission boundaries are fully enforced.
- **External Dependencies**: None. All mock database users must be seeded to test roles (Student, Teacher, Assistant, Admin, Supervisor).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Docker configurations MUST separate environments, domains, and ports for each of the 5 surfaces (Landing, Student, Teacher, Assistant, Admin).
- **FR-002**: OLD CORS/Nginx configurations MUST be cleaned up, leaving only `massar-academy.net` domains active, or configuring legacy domains as 301 redirects.
- **FR-003**: The E2E testing suite MUST include tests verifying that:
  - Students cannot open admin, teacher, or assistant routes.
  - Teachers cannot open admin, student, or assistant routes.
  - Assistants cannot open admin, student, or teacher routes.
  - Admins and Supervisors are restricted according to their specific permissions.
- **FR-004**: The system MUST verify the Assistant permission matrix in E2E tests.
- **FR-005**: The system MUST verify Teacher binding (a teacher can only access lessons/packages they teach).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the 5 surfaces are successfully separated into distinct subdomains and environment files with no shared ports.
- **SC-002**: 100% of CORS requests from old domains are rejected or permanently redirected.
- **SC-003**: E2E test coverage asserts cross-surface route blocking with 100% success rate.

## Assumptions

- We assume that DNS configurations for `massar-academy.net` are correctly pointed to the environment.
- We assume that the existing database seeder supports seeding distinct users for Student, Teacher, Assistant, Admin, and Supervisor roles.

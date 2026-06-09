# Feature Specification: Assistant/Staff Surface and Task Workflow

**Feature Branch**: `107-assistant-surface-and-tasks`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 2 - Assistant/Staff Surface and Task Workflow: Build assistant and staff space independently of Admin."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Assistant/Staff Space Isolation (Priority: P1)

As an assistant or staff member, I want a dedicated, secure portal at `staff.localhost:8742` that is isolated from the Admin portal, containing a tailored navigation menu (Dashboard, Tasks, CRM, Chat, Attendance, Vacations, Notifications) matching my roles and permissions, so that I can perform my daily duties without exposing admin-only surfaces or controls.

**Why this priority**: Core layout boundary for the Assistant/Staff space. This prevents assistants from accessing supervisor/admin routes and provides a clean, tailored workspace.

**Independent Test**: Can be tested by logging in as an Assistant, accessing `staff.localhost:8742/assistant/dashboard`, and verifying that the sidebar/navbar displays only authorized menus, and attempting to access `/admin` or `/super` endpoints results in a redirect or 403.

**Acceptance Scenarios**:
1. **Given** an authenticated user with the "Assistant" role, **When** they load the Assistant workspace, **Then** they must see the dedicated Assistant Navbar containing links to Dashboard, Tasks, CRM, Chat, Attendance, Vacations, and Notifications.
2. **Given** an Assistant, **When** they try to access any `/admin/*` routes or views, **Then** they must be redirected back to `/assistant` or receive a 403 Forbidden page.
3. **Given** an Assistant, **When** they view the sidebar/navbar menu, **Then** menu items representing features they lack permissions for (e.g., Financials or Admin Settings) must be dynamically hidden.

---

### User Story 2 - Task Workflow and Ownership Checks (Priority: P2)

As an assistant, I want to see, comment on, and update the status of operational and academic tasks assigned to me. As a supervisor or admin, I want to view tasks under my jurisdiction and approve completed tasks, so that we ensure high-quality operations with proper oversight.

**Why this priority**: Standardizes the task management workflow and secures it by enforcing ownership checks at the API/DB layer.

**Independent Test**: Log in as Assistant A, view the task list, and confirm that only tasks assigned to Assistant A or created by Assistant A are listed. Try to view/update Assistant B's task and verify a 403 Forbidden is returned.

**Acceptance Scenarios**:
1. **Given** a task assigned to Assistant A, **When** Assistant A views `/assistant/tasks`, **Then** the task must appear in their list.
2. **Given** a task assigned to Assistant B, **When** Assistant A attempts to view the details of that task via direct URL (`/assistant/tasks/123`), **Then** the page must load a 403 Forbidden error.
3. **Given** a task in "InProgress" status, **When** the assignee marks it as "Completed", **Then** the status must change to "Review" (awaiting supervisor/admin approval).
4. **Given** a task in "Review" status, **When** a Supervisor or Admin approves the task, **Then** the status must change to "Completed" and be finalized. If a regular Assistant tries to approve it, the request must fail with a 403 Forbidden.

---

### User Story 3 - HR Core: Attendance and Vacations (Priority: P3)

As an assistant, I want to view my monthly attendance logs, see my vacation balance, and submit vacation requests directly from my portal, so that I can manage my employment metrics easily.

**Why this priority**: Provides the interface for the HR features introduced in earlier phases, allowing employees to see their specific records.

**Independent Test**: Log in as Assistant, navigate to `/assistant/attendance` to view current month attendance calendar, and `/assistant/vacations` to view balance and submit a vacation request form.

**Acceptance Scenarios**:
1. **Given** an Assistant, **When** they view `/assistant/attendance`, **Then** the system must render their personal sign-in/sign-out times and calculated daily working hours.
2. **Given** an Assistant, **When** they view `/assistant/vacations`, **Then** they must see their remaining vacation balance (e.g., "15 days") and be able to fill a request form specifying Start Date, End Date, and Reason.
3. **Given** a submitted vacation request, **When** the Assistant views their request history, **Then** the status must show as "Pending" until a Supervisor/Admin approves or rejects it.

---

### Edge Cases

- **Task Transition to Completed by Assignee**: If an assignee attempts to mark a task as "Completed" directly, the system must intercept this and move it to "Review" status first. It cannot bypass the supervisor approval phase.
- **Supervisor-only Task List**: A Supervisor needs to see all tasks assigned to assistants under their team. The backend must map supervisors to their assistants or allow supervisors to see tasks based on the `ManageTasks` or `ViewAllTasks` permissions.
- **Offline/No Network on Layout Load**: The layout must handle API connection failures gracefully with offline indicators, showing cached menus or a localized error screen instead of crashing the Next.js app.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Assistant, navigate to `staff.localhost:8742/assistant/dashboard`, verify it loads. Check navbar has Dashboard, Tasks, CRM, Chat, Attendance, Vacations, and Notifications.
- **Manual QA Role/Flow 2**: Assistant A, try to load `/assistant/tasks/B-taskId` of Assistant B, verify 403 Forbidden is shown.
- **Manual QA Role/Flow 3**: Assistant, update a task status to Completed. Verify that the task status is set to "Review" in the UI, and they cannot click "Approve" (button is disabled or hidden). Log in as Admin/Supervisor, approve the task, verify status becomes "Completed".
- **Manual QA Negative Check**: Assistant, attempt to access `admin.localhost:8740/admin`, verify redirect to `app.localhost:8739` or `staff.localhost:8742` or 403 Forbidden.
- **Docker Acceptance**: `docker compose ps` shows `massar_assistant` (running frontend on port 8742), `massar_backend` (running API), and `nginx` proxying requests correctly.
- **External Dependencies**: Local Database (PostgreSQL) with mock users (Assistant, Supervisor, Admin, Student) and tasks created.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Assistant/Staff workspace MUST be mounted under `/assistant/*` routes and wrapped in `AssistantGuard` to verify user roles.
- **FR-002**: A dedicated, responsive Assistant Navbar component MUST be created, including links for Dashboard, Tasks, CRM, Chat, Attendance, Vacations, and Notifications.
- **FR-003**: The Navbar MUST dynamically hide navigation links if the user's role/permissions do not grant access to those specific pages (e.g. Chat or CRM).
- **FR-004**: The Assistant Task List view MUST list tasks assigned to or created by the logged-in user.
- **FR-005**: The Task Detail view MUST support:
  - Viewing task specifications, checklist items, and history.
  - Adding and displaying comments/updates.
  - Updating task status (`Pending` -> `InProgress` -> `Review`).
- **FR-006**: Backend API handlers for operations tasks MUST enforce strict ownership validation checks:
  - Allow access if the user is the Assignee, Creator, Admin, or holds `ManageTasks` permission.
  - Reject access with a 403 Forbidden response if the check fails.
- **FR-007**: Only users with Admin role or holding the `ApproveTasks` / `ManageTasks` permission CAN transition a task status from `Review` to `Completed`.
- **FR-008**: The Attendance page MUST query the backend HR endpoints for the logged-in user's attendance log.
- **FR-009**: The Vacations page MUST show user vacation balance and allow submission of vacation requests to the backend HR service.

### Key Entities *(include if feature involves data)*

- **AdminTask**: Represents a task in Nader Gorge platform. Key attributes:
  - `Id` (Guid)
  - `Title` (string)
  - `Description` (string)
  - `Status` (Enum: Pending, InProgress, Review, Completed)
  - `AssigneeId` (Guid, references User)
  - `CreatorId` (Guid, references User)
  - `DueDate` (DateTime?)
  - `Comments` (Collection of AdminTaskComment)
- **AdminTaskComment**: Represents a comment on a task. Key attributes:
  - `Id` (Guid)
  - `TaskId` (Guid)
  - `AuthorId` (Guid)
  - `Content` (string)
  - `CreatedAt` (DateTime)
- **AttendanceLog**: Represents daily attendance. Key attributes:
  - `Id` (Guid)
  - `UserId` (Guid)
  - `Date` (DateOnly)
  - `SignInTime` (DateTime?)
  - `SignOutTime` (DateTime?)
- **VacationRequest**: Represents a request for leave. Key attributes:
  - `Id` (Guid)
  - `UserId` (Guid)
  - `StartDate` (DateOnly)
  - `EndDate` (DateOnly)
  - `Status` (Enum: Pending, Approved, Rejected)
  - `Reason` (string)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of routes under `/assistant` are protected and refuse access to non-staff roles, returning 403 or redirecting.
- **SC-002**: 100% of task read and write API endpoints correctly enforce assignee or supervisor ownership, returning 403 for unauthorized resource access.
- **SC-003**: Regular assistants are blocked from setting task status to `Completed` directly; they must only be able to submit tasks for `Review` status.
- **SC-004**: The Next.js frontend builds without any errors or styling warnings.
- **SC-005**: All E2E smoke tests for assistant boundary access pass.

## Assumptions

- We assume the database schemas for tasks, attendance, and vacation requests exist or have been scaffolded in previous HR modules, requiring only frontend binding and backend permission checks.
- We assume that `AssistantGuard` will redirect unauthorized users to `/login`.
- Mobile views will use a collapsible sidebar/navbar for proper Tajawal/Cairo typography rendering.

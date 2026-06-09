# Feature Specification: Operations Task Manager and Approval Pipeline

**Feature Branch**: `091-operations-task-manager`  
**Created**: 2026-06-07  
**Status**: Draft  
**Input**: User description: "Phase 3 - Operations Task Manager and Approval Pipeline. Build daily tasks manager, assign roles, comments, attachments, overdue triggers, and supervisor approval pipelines."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Task Creation & Assignment (Priority: P1) 🎯 MVP

As a Manager or Administrator, I want to create daily operational tasks and assign them to specific staff/assistants, defining details such as title, description, priority (Low, Medium, High, Critical), and due date, so that staff workloads are organized and tracked.

**Why this priority**: Core task scheduling is the foundational pillar of the operational queue; we cannot comment or approve tasks without their records first.

**Independent Test**: Admin navigates to `/admin/operations`, clicks "Create Task", inputs details (Title: "Review call logs", Priority: High, Assignee: "Assistant User", Due Date: tomorrow), saves successfully, and verifies the task appears in the assistant's queue.

**Acceptance Scenarios**:

1. **Given** an authenticated Administrator or Supervisor, **When** they fill the task creation form and submit, **Then** the task is created with status `New` and the assignee's list is updated.
2. **Given** a new task is created, **When** the assignee logs in, **Then** they see the task on their dashboard with the correct priority badge.

---

### User Story 2 - Task Collaboration & Progress Transition (Priority: P1) 🎯 MVP

As a staff member (Assignee), I want to update the status of my tasks (InProgress, Review, Paused), add textual comments, and include attachments/links, so that managers can monitor execution progress.

**Why this priority**: Essential for daily teamwork, tracking active execution, and sharing updates or deliverables.

**Independent Test**: Assignee opens a task, clicks "Start Work" to change status to `InProgress`, writes a comment "I have started reviewing the files", adds a link, and submits successfully.

**Acceptance Scenarios**:

1. **Given** a task assigned to a staff user, **When** they click "Start Work", **Then** the status transitions to `InProgress` and a log is recorded.
2. **Given** a user writes a comment on a task, **When** they submit it, **Then** the comment is stored with the user's name and UTC timestamp.
3. **Given** a task is in `Review` or `Completed` status, **When** a non-manager attempts to downgrade the status back to `InProgress`, **Then** the system blocks the action.

---

### User Story 3 - Operations Approval Pipeline (Priority: P2)

As a Manager or Supervisor, I want to review tasks marked as `Review` or sensitive operation request flags (like task completion approvals), and approve or reject them to complete the operational cycle.

**Why this priority**: Guarantees quality assurance and controls sensitive operational actions by requiring a supervisor's sign-off before closure.

**Independent Test**: Assignee marks task as complete, which submits it for approval. Supervisor logs in, views the pending approvals queue, reviews comments, and clicks "Approve Completion". The task status transitions to `Completed`.

**Acceptance Scenarios**:

1. **Given** a task requiring manager approval, **When** the assignee requests completion, **Then** the task status transitions to `Review` and a pending approval request is logged.
2. **Given** a pending completion approval request, **When** an assistant attempts to approve it, **Then** the system rejects it with `403 Forbidden`.
3. **Given** an approved completion request, **When** a manager approves it, **Then** the task transitions to `Completed` with the approver's name and resolution timestamp.

---

## Edge Cases

- **Task Closing without Assignee**: If a user attempts to close or submit a task for approval without an assignee, the system must block the request.
- **Dynamic Overdue Transition**: If a task due date passes without the task being marked as `Completed` or `Resolved`, the system should dynamically evaluate and display its status as `Overdue`.
- **Assignee Role Verification**: Tasks can only be assigned to users with Staff, Supervisor, or Admin roles. Attempting to assign a task to a `Student` user is blocked.

## Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin logs in, creates a task assigned to an Assistant. Assistant logs in, starts the task, adds a comment and a link, and clicks "Request Completion". Supervisor logs in, reviews the task, and approves it. Verify that status changes to `Completed` and is marked resolved.
- **Manual QA Negative Check**: Assistant attempts to approve their own completion request or change another user's task status without permissions. Verify that the action is blocked.
- **Docker Acceptance**: Run `make migrate` to apply new database schemas for tasks and comments. Verify health endpoint returns `200`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support tasks with statuses: New, InProgress, Review, Completed, Paused, Overdue.
- **FR-002**: Tasks MUST support priority levels: Low, Medium, High, Critical.
- **FR-003**: System MUST enforce that tasks are only assigned to non-student roles (Admin, Supervisor, Staff).
- **FR-004**: System MUST allow adding comments, links, and text attachments to tasks.
- **FR-005**: Tasks marked for closure MUST require approval from a user with the `hr.manage` or supervisor claim (or bypass for Admin/Teacher) if flagged as restricted.
- **FR-006**: System MUST record UTC timestamps and performed-by user IDs for all status transitions and comments.
- **FR-007**: Overdue status MUST be computed dynamically based on the due date passing the current time.

### Key Entities *(include if feature involves data)*

- **TaskItem**:
  - `Id` (GUID)
  - `Title` (String)
  - `Description` (String)
  - `AssigneeId` (GUID, references User)
  - `CreatedById` (GUID, references User)
  - `Status` (Enum: New, InProgress, Review, Completed, Paused, Overdue)
  - `Priority` (Enum: Low, Medium, High, Critical)
  - `DueDate` (DateTime, Nullable)
  - `CompletedAt` (DateTime, Nullable)
  - `ApprovedById` (GUID, Nullable)
- **TaskComment**:
  - `Id` (GUID)
  - `TaskId` (GUID, references TaskItem)
  - `UserId` (GUID, references User)
  - `Content` (String)
  - `AttachmentUrl` (String, Nullable)
  - `CreatedAt` (DateTime)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Task assignments and updates trigger on-screen toast alerts or notifications to the assignee within 1.5 seconds.
- **SC-002**: 100% of status changes for tasks requiring approval must be verified and logged in the database before transition.
- **SC-003**: System calculates task overdue status dynamically, guaranteeing search results show correct overdue states instantly.

## Assumptions

- Task comments and links are simple text fields for the MVP (rich files upload is out of scope).
- Assigning a task doesn't automatically trigger email/SMS notifications; on-platform notification logs are sufficient.

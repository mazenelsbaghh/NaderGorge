# Feature Specification: Call Center CRM and Student Follow-Up

**Feature Branch**: `094-call-center-crm`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 6 - Call Center CRM and Student Follow-Up"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Call List Assignment and Follow-Up Queue (Priority: P1)

As an Administrator or Supervisor, I want to view all students, assign agents to them, set priorities, and manage next follow-up dates. As an Agent (Assistant/Staff), I want to see only my assigned student queue so that I can focus on my follow-ups without distractions.

**Why this priority**: Core MVP functionality. Staff must be partitioned so that agents only see their workload, and supervisors can assign/reassign students.

**Independent Test**:
- Admin logs in, views CRM dashboard, assigns a student to "Agent A".
- Agent A logs in, views their assigned follow-up queue, and sees the student.
- Agent B logs in, views their queue, and does not see the student.

**Acceptance Scenarios**:

1. **Given** an authenticated Admin, **When** they view the CRM page, **Then** they see all students and a dropdown to assign or reassign agents to each student.
2. **Given** an authenticated Assistant with `crm.manage` permission, **When** they view their follow-up dashboard, **Then** they only see students assigned to them.
3. **Given** a student is assigned to an Agent with a "High" priority, **When** the Agent views their queue, **Then** they see the priority badge clearly displayed, and the list can be sorted by priority.

---

### User Story 2 - Logging Calls and Scheduling Reschedules (Priority: P2)

As a Call Center Agent, I want to record call logs with outcomes (Completed, Pending, NoAnswer, Postponed, Closed) and set a next follow-up date, so that we have a full history of all interactions with the student/parent.

**Why this priority**: Required for tracking progress and ensuring students are followed up on schedule.

**Independent Test**:
- Agent opens a student's profile inside the CRM, clicks "Log Call", inputs call outcome "NoAnswer", writes notes, and submits. The log is appended to the timeline, and the student's status changes accordingly.

**Acceptance Scenarios**:

1. **Given** an agent viewing a student's details, **When** they submit a call log with outcome "NoAnswer", **Then** the student's CRM status is updated to `InProgress`, the call log is saved, and the system prompts to schedule the next follow-up.
2. **Given** an agent submitting a call log with outcome "Postponed", **When** they save the log, **Then** the next follow-up date is required and updated in the student's CRM status.
3. **Given** an agent viewing a student's call log history, **When** they review the timeline, **Then** they see logs sorted chronologically with agent name, status, next follow-up date, and notes.

---

### User Story 3 - Quick Communication Actions & Evolution API Link (Priority: P3)

As a Call Center Agent, I want to click a quick WhatsApp icon to generate a pre-filled chat link for the student's phone number, allowing me to send follow-up messages instantly.

**Why this priority**: Improves agent efficiency. WhatsApp links speed up communication when calls fail (e.g., "NoAnswer").

**Independent Test**:
- Agent clicks the WhatsApp icon on the student card, opening a browser tab to `https://wa.me/201234567890?text=...` with the pre-filled template.

**Acceptance Scenarios**:

1. **Given** a student with phone number `01012345678`, **When** the agent clicks the WhatsApp quick action, **Then** a new tab opens pointing to `https://wa.me/201012345678?text=...` prefilled with: "مرحباً {studentName}، معك {agentName} من منصة مسار...".
2. **Given** a student with a custom phone number format, **When** the link is generated, **Then** the phone number is normalized to international standard (`+20` prefix for Egypt) automatically.

---

### User Story 4 - Agent Performance and CRM Metrics Dashboard (Priority: P3)

As a Supervisor or Admin, I want to view performance charts (calls made, outcomes breakdown, conversion rate from cold to active) per agent and overall, so that I can evaluate call center efficiency.

**Why this priority**: Essential management analytics, but not critical for core data logging (P3).

**Independent Test**:
- Supervisor views CRM Analytics tab and sees a breakdown table of call outcomes per agent.

**Acceptance Scenarios**:

1. **Given** a Supervisor viewing CRM dashboard, **When** they switch to the "Reports" tab, **Then** they see graphs or tables showing Total Calls, Call Outcomes Breakdown (Completed, NoAnswer, etc.), and active task load per Agent.

---

### Edge Cases

- **Next Follow-Up Date in the Past**: If the next follow-up date passes without a call, the student's record must be marked as "Overdue" in the list with a red indicator.
- **Reassigning an Agent with Pending Calls**: When a student is reassigned to a new agent, the call log history must remain intact with original logging agent names preserved.
- **Student Access Attempt**: If a user logged in as a Student tries to navigate to `/admin/crm` or `/assistant/crm`, they must be redirected with an unauthorized access code immediately.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1 (Supervisor)**: Log in as Admin/Supervisor, navigate to `/admin/crm`. Search for a student, set priority to "High", assign to "Assistant X", and set next follow-up date.
- **Manual QA Role/Flow 2 (Agent/Assistant)**: Log in as Assistant X, navigate to `/assistant/crm`. Verify that only assigned students are shown. Click on a student, log a call as "NoAnswer" with notes "Failed to reach", and verify the history timeline updates.
- **Manual QA Negative Check**: Log in as a Student, attempt to access `/admin/crm`. The system must redirect to `/login` or display the unauthorized screen.
- **Docker Acceptance**: Verify backend migrates cleanly after adding `CrmStudentStatus` and `CrmCallLog` tables. Run `dotnet ef migrations add AddCrmEntities` and test that local Docker instances start up correctly.
- **External Dependencies**: Standard browser-level WhatsApp redirects (`wa.me`) require no API credentials. Optional integration with Evolution API can be added as a fallback toggled in platform settings.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST store CRM status attributes for each student including `Status`, `AssignedAgentId`, `Priority`, and `NextFollowUpDate`.
- **FR-002**: System MUST allow Admins and Supervisors to view all students and assign/reassign agents to students.
- **FR-003**: System MUST restrict Call Center Agents (Assistants without admin rights) to viewing only students assigned to them, unless they are granted a role with full read bypass.
- **FR-004**: System MUST allow agents to log calls on students with outcomes: `Completed`, `Pending`, `NoAnswer`, `Postponed`, `Closed`, and save comments.
- **FR-005**: System MUST validate that next follow-up date is required and set in the future if the outcome is `Postponed` or `Pending`.
- **FR-006**: System MUST normalize student phone numbers to E.164 Egypt format (`+20...`) for the WhatsApp quick action link.
- **FR-007**: System MUST provide Supervisor-only analytics displaying call volumes, outcome statistics, and activity charts per agent.

### Key Entities *(include if feature involves data)*

- **CrmStudentStatus**:
  - `StudentId` (FK to User/Student, Unique)
  - `Status` (Enum: `Unassigned`, `Assigned`, `InProgress`, `Cold`, `Closed`)
  - `AssignedAgentId` (FK to User, Nullable)
  - `Priority` (Enum: `Low`, `Medium`, `High`, `Critical`)
  - `NextFollowUpDate` (Nullable DateTime)
  - `Notes` (String)
  - `LastCalledAt` (Nullable DateTime)
- **CrmCallLog**:
  - `Id` (Guid, PK)
  - `StudentId` (FK to User/Student)
  - `AgentId` (FK to User/Agent)
  - `CallDate` (DateTime)
  - `Outcome` (Enum: `Completed`, `Pending`, `NoAnswer`, `Postponed`, `Closed`)
  - `Notes` (String)
  - `NextFollowUpDate` (Nullable DateTime)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Agents can log a call and schedule a follow-up in under 30 seconds.
- **SC-002**: 100% of student lists shown on the agent's screen are restricted strictly to their assigned records.
- **SC-003**: Supervisors can view live call volume stats and agent metrics refreshed on dashboard load.

## Assumptions

- CRM features are strictly for internal staff; students will never see any CRM tabs, call logs, or follow-up details.
- Evolution API integration is optional and defaults to browser-based `wa.me` links unless API credentials are explicitly configured in the database settings.
- The timezone for call logging is local Egypt time (UTC+2 or UTC+3 depending on daylight savings).

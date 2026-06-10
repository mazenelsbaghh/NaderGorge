# Feature Specification: Assistant Profile & Attendance Refinements

**Feature Branch**: `114-assistant-profile-attendance-refinements`  
**Created**: 2026-06-10  
**Status**: Draft  
**Input**: User description: "Prevent multiple attendance registrations per day and warn on early checkout. Full profile page for assistant with all details."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single Attendance Session Per Day (Priority: P1)

As an Assistant (Staff Member), I should only be allowed to check in once per calendar day (Egypt Time), preventing duplicate or redundant attendance logs.

**Why this priority**: Crucial for database integrity and accurate payroll calculations. Prevents staff from creating duplicate shifts accidentally.

**Independent Test**: Can be tested by checking in, checking out, and attempting to check in again on the same day. The second attempt must be blocked.

**Acceptance Scenarios**:

1. **Given** I have no attendance logs for today, **When** I click "تسجيل الحضور" (Clock In), **Then** a new attendance log is created successfully.
2. **Given** I already have a completed attendance log (checked in and out) for today, **When** I attempt to click "تسجيل الحضور" (Clock In) or call the clock-in endpoint, **Then** I receive an error message: "لقد قمت بتسجيل الحضور بالفعل اليوم." and no duplicate record is created.

---

### User Story 2 - Early Checkout Warning Safeguard (Priority: P1)

As an Assistant, when checking out, if I have worked less than my profile's required target daily hours, I should receive a warning dialog to prevent accidental early departures.

**Why this priority**: Helps staff self-correct and avoid penalties for incomplete shifts.

**Independent Test**: Can be tested by clocking in, and immediately attempting to clock out. An interactive warning modal must appear.

**Acceptance Scenarios**:

1. **Given** I have been clocked in for less than the `TargetDailyHours` (e.g. 2 hours out of 8), **When** I click "تسجيل الانصراف" (Clock Out), **Then** I am presented with a confirmation warning: "تحذير: لم تكمل ساعات العمل المطلوبة اليوم بعد (8 ساعات). هل أنت متأكد من تسجيل الانصراف؟"
2. **Given** the early checkout warning is shown, **When** I click "إلغاء" (Cancel), **Then** the checkout is aborted and I remain clocked in.
3. **Given** the early checkout warning is shown, **When** I click "نعم، تسجيل الانصراف" (Confirm), **Then** the checkout proceeds, and my session is closed.
4. **Given** I have worked equal to or more than the `TargetDailyHours` (e.g. 8.5 hours), **When** I click "تسجيل الانصراف", **Then** the checkout proceeds immediately without a warning.

---

### User Story 3 - Full Assistant Profile Page for Admins (Priority: P1)

As an Administrator, I want a dedicated, comprehensive page for each assistant displaying their identity, employee settings, attendance history, audit logs, and vacation requests, so I can review and manage their work from a single screen.

**Why this priority**: Core administrative requirement for tracking staff performance, modifying salaries/working schedules, and auditing actions.

**Independent Test**: Can be tested by clicking on any assistant in the assistants table, which redirects to `/admin/assistants/[id]`, displaying all details.

**Acceptance Scenarios**:

1. **Given** I am on the assistants list, **When** I click on an assistant row, **Then** I am redirected to `/admin/assistants/[id]`.
2. **Given** I am on `/admin/assistants/[id]`, **Then** I can see the following sections:
   - **Basic Info**: Name, Phone, Role, Joined Date, Status (Active/Suspended) with toggle capability.
   - **Employee Settings**: Basic Salary, Standard Start Time, Target Daily Hours with inline form to update them.
   - **Attendance History**: A table of all their past clock-ins/outs with IP, user agent, duration, and status.
   - **Audit Logs**: A timeline of all system actions performed by this assistant.
   - **Vacation Requests**: A list of their vacation requests with inline Approve/Reject action buttons.

---

### Edge Cases

- **Cairo Time Zone Date Boundary Check**: If an assistant checks in at 11:30 PM Cairo time and checks out at 1:30 AM the next day, they should be allowed to check in again on the new day.
- **Empty Employee Profile**: If an assistant does not have an employee profile configured yet, accessing the profile page should show an option to initialize it with default values (Salary = 0, Hours = 8, Start Time = 09:00).
- **Audit Logging for Profile Updates**: Any updates to the employee profile settings (Basic Salary, Start Time, Target Daily Hours) must generate an audit log under `UpdateEmployeeProfile`.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Flow 1**: Log in as an assistant, go to `/assistant/attendance`. Clock in, then immediately click Clock Out. Verify that a warning dialog is displayed. Cancel the dialog and confirm you remain clocked in. Then clock out and confirm. Attempt to clock in again and verify it is blocked.
- **Manual QA Flow 2**: Log in as an admin, go to `/admin/assistants`. Click on an assistant. Verify redirection to `/admin/assistants/[id]` and check that all sections load correct data. Update their target hours to 6 hours, click save, and verify the change persists.
- **Docker Acceptance**: Run `docker compose ps` to ensure all containers are healthy.
- **External Dependencies**: Standard system database and Cairo timezone configurations are required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST prevent an employee from creating more than one attendance log entry per calendar date (Cairo Local Time).
- **FR-002**: System MUST retrieve `TargetDailyHours` in the `/api/hr/attendance/my` API response to let the frontend determine early checkout thresholds.
- **FR-003**: Frontend MUST calculate elapsed hours of the active session and compare them with `TargetDailyHours` before proceeding with clock-out.
- **FR-004**: Frontend MUST display an interactive warning dialog when an early checkout is detected.
- **FR-005**: System MUST support a dedicated route `/admin/assistants/[id]` for the assistant's full profile page.
- **FR-006**: The assistant profile page MUST support loading user details, employee profile settings, Cairo timezone logs, and audit logs.
- **FR-007**: The assistant profile page MUST support inline editing and updating of employee profile settings.
- **FR-008**: The assistant profile page MUST display all vacation requests submitted by the assistant and allow the admin to approve or reject them inline.

### Key Entities

- **AttendanceLog**: Represents a single daily attendance record for an employee. Key attributes: Date, ClockIn, ClockOut, LateMinutes, Status, IpAddress, UserAgent.
- **EmployeeProfile**: Job configuration profile. Key attributes: BasicSalary, StandardStartTime, TargetDailyHours.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Employees are 100% blocked from creating multiple shifts on the same day.
- **SC-002**: Checkout warning prevents accidental checkout, reducing manual attendance adjustments by admins.
- **SC-003**: Admins can view and configure an assistant's complete payroll, attendance, and audit timeline on a single page in under 3 seconds.

## Assumptions

- We assume Egypt Standard Time (Cairo Zone) is the standard timezone for evaluating calendar dates.
- Mobile support is handled responsively using standard tailwind classes.

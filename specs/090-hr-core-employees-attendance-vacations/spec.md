# Feature Specification: HR Core - Employees, Attendance, Vacations

**Feature Branch**: `090-hr-core-employees-attendance-vacations`  
**Created**: 2026-06-07  
**Status**: Draft  
**Input**: User description: "Phase 2 - HR Core: Employees, Attendance, Vacations. Build employee profile, attendance, and vacation request features. Protect and restrict access based on HR permissions."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Employee Profile Setup & Management (Priority: P1) 🎯 MVP

As an Administrator, I want to create and manage employee profiles linked to `Staff` or `Supervisor` users, specifying job data such as basic salary and standard working hours, so that their job terms are recorded.

**Why this priority**: Necessary to model employee parameters (like salary and start time) before tracking their active attendance or payroll.

**Independent Test**: Admin navigates to the user management panel, edits a user with role `Staff` or `Supervisor`, fills in the HR Profile fields (Basic Salary: 5000 EGP, Standard Start Time: 09:00 AM, Target Daily Hours: 8), saves successfully, and verifies it persists in the DB.

**Acceptance Scenarios**:

1. **Given** an authenticated Admin, **When** they view a user with the `Staff` or `Supervisor` role, **Then** they see an option to configure their HR Profile.
2. **Given** a user configured with an Employee Profile, **When** attempting to create another Employee Profile for the same user, **Then** the system blocks the duplicate creation with a unique constraint error.

---

### User Story 2 - Employee Attendance Logging (Clock-in/out) (Priority: P1) 🎯 MVP

As a Staff or Supervisor employee, I want to clock-in and clock-out from my daily workspace panel, capturing device and IP metadata, so that my working hours and late minutes are automatically computed.

**Why this priority**: Core tracking functionality needed to calculate work hours and late logs.

**Independent Test**: Employee logs in, clicks the "Clock In" button. The system registers active attendance. Later, they click "Clock Out", and the system logs work hours and finishes the session.

**Acceptance Scenarios**:

1. **Given** an employee logging in after their standard start time (e.g. at 09:15 AM when standard start is 09:00 AM), **When** they clock-in, **Then** the system registers status `Late` and logs `15` late minutes.
2. **Given** a clock-in record exists, **When** clocking out, **Then** the system updates the record with the clock-out timestamp and calculates total working hours.

---

### User Story 3 - Vacation Request & Approvals (Priority: P2)

As a Staff or Supervisor employee, I want to submit vacation requests. As an HR Manager (Admin or Supervisor with `hr.manage`), I want to approve or reject these requests, automatically marking the approved days as vacation leaves in the attendance record.

**Why this priority**: Integrates scheduled absences with the attendance calendar.

**Independent Test**: Employee submits a vacation request for a date range. Admin reviews it in the HR portal, approves it, and verifies the employee's attendance log for those dates is auto-populated with `Leave` status.

**Acceptance Scenarios**:

1. **Given** an employee submits a vacation request, **When** a supervisor without `hr.manage` permission attempts to approve it, **Then** the request is rejected with `403 Forbidden`.
2. **Given** an approved vacation request, **When** the start date is reached, **Then** the system automatically marks the employee's attendance log for those dates as `Leave` (Vacation).

---

### Edge Cases

- **Double Clock-in**: If a user attempts to clock-in while an active clock-in session is already open for the same day, the system should prevent the request and prompt to clock-out of the active session first.
- **Clock-out without Clock-in**: If a user calls the clock-out endpoint but has no active clock-in session, the system returns a validation error.
- **Overlapping Vacations**: If an employee requests a vacation that overlaps with an existing approved vacation request, the system blocks submission.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin logs in, accesses `/admin/hr`, selects a staff user, creates their HR profile setting standard start time to `09:00 AM`.
- **Manual QA Role/Flow 2**: Staff user logs in at `09:10 AM`, clicks "Clock In". Verify that the dashboard shows `Late` status and `10` minutes late.
- **Manual QA Negative Check**: Staff user attempts to navigate to `/admin/hr` (Admin HR Settings) and is redirected to `/admin/unauthorized` or blocked.
- **Docker Acceptance**: Run `make migrate` to apply database tables for employees and logs. Verify API health endpoint returns `200`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST establish a 1-to-1 relationship between `User` and `EmployeeProfile` models.
- **FR-002**: EmployeeProfile MUST record basic salary, standard daily start time, and target daily working hours.
- **FR-003**: System MUST capture clock-in and clock-out timestamps, IP address, and browser User-Agent metadata.
- **FR-004**: System MUST calculate late minutes relative to the employee's configured standard start time.
- **FR-005**: System MUST prevent multiple concurrent active clock-in sessions per employee.
- **FR-006**: Employees MUST be able to request vacations with a start date, end date, and reason.
- **FR-007**: Approval or rejection of vacations MUST require the `hr.manage` permission claim (Admin/Teacher bypassed).
- **FR-008**: System MUST log audit events for sensitive HR actions: salary modifications, attendance overrides, and vacation status changes.

### Key Entities *(include if feature involves data)*

- **EmployeeProfile**:
  - `Id` (GUID)
  - `UserId` (GUID, 1-to-1 link to User)
  - `BasicSalary` (Decimal)
  - `StandardStartTime` (TimeSpan)
  - `TargetDailyHours` (Integer)
- **AttendanceLog**:
  - `Id` (GUID)
  - `EmployeeId` (GUID)
  - `Date` (DateOnly)
  - `ClockIn` (DateTime)
  - `ClockOut` (DateTime, Nullable)
  - `LateMinutes` (Integer)
  - `Status` (Enum: Present, Late, Absent, Sick, Leave)
  - `IpAddress` (String)
  - `UserAgent` (String)
- **EmployeeVacation**:
  - `Id` (GUID)
  - `EmployeeId` (GUID)
  - `StartDate` (DateOnly)
  - `EndDate` (DateOnly)
  - `Status` (Enum: Pending, Approved, Rejected)
  - `Reason` (String)
  - `HandledBy` (GUID, Nullable)
  - `HandledAt` (DateTime, Nullable)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Employees can complete clock-in or clock-out in under 3 seconds from their dashboard.
- **SC-002**: 100% of approved vacation records automatically register as `Leave` status in the corresponding attendance log dates.
- **SC-003**: All modifications to employee profiles or attendance logs generate an audit log record within 1 second.

## Assumptions

- Geolocation metadata is optional for MVP, IP address and browser User-Agent are mandatory.
- Standard daily work hour target defaults to 8 hours if not customized.

# Feature Specification: Role Pages and Permissions Completion

**Feature Branch**: `110-role-pages-and-permissions`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: platform_expansion_gap_report_2026-06-09.md (Phase 2)

## User Scenarios & Testing

### User Story 1 - Teacher Learner Activity & Watch Stats Dashboard (Priority: P1)
As a Teacher, I want a dedicated dashboard page `/teacher/activity` that displays:
- Active students (recently watched a video).
- Latest watch log.
- Student progress inside teacher packages.
- Most watched videos/lessons.
- Student alerts for late/inactive students (inactive for > 7 days).
So that I can monitor student engagement and identify who needs follow-up.

* **Independent Test**: Log in as Teacher, navigate to `/teacher/activity`, and verify the dashboard loads the student activity logs, most watched videos, and inactive student alerts correctly.
* **Acceptance Scenarios**:
  1. **Given** a Teacher with active students, **When** they view `/teacher/activity`, **Then** the page must render a list of recent student watch events for the teacher's packages.
  2. **Given** a student inactive for 8 days, **When** the teacher views the activity dashboard, **Then** that student must appear under the "Inactive Student Alerts" section with an indication of "8 days inactive".
  3. **Given** several lesson videos, **When** the teacher views the activity dashboard, **Then** the videos must be ordered by total watch count under "Most Watched Videos".

---

### User Story 2 - Student Profile Management (Priority: P1)
As a Student, I want to access my personal profile page at `/student/profile` where I can view my registration details (name, phone, governorate, district, date of birth, education stage, grade level, and active devices count vs limit) and edit contact fields (address, school name, secondary phone, parent phone, and mother phone) so that my profile information is kept up to date.

* **Independent Test**: Log in as Student, navigate to `/student/profile`, verify it lists all personal details, modify the secondary parent phone or school name, click Save, and confirm the updated data persists.
* **Acceptance Scenarios**:
  1. **Given** an authenticated Student, **When** they view `/student/profile`, **Then** they must see their name, primary phone, birth date, governorate, grade level, and device limit status.
  2. **Given** a student profile page, **When** the student changes their "School Name" and clicks "Save Changes", **Then** the system must update the profile and display a success message.
  3. **Given** a student profile page, **When** the student views the registered devices count, **Then** it must correctly show the number of active devices linked to their account out of the system limit (e.g. "1 of 2 devices").

---

### User Story 3 - Student In-App Notifications (Priority: P2)
As a Student, I want a dedicated portal page `/student/notifications` and an unread notification count badge in my dashboard header so that I can see announcements and mark notifications as read.

* **Independent Test**: Send an in-app notification to Student A, log in as Student A, verify the notification count badge updates in the header, click the notifications tab to read it, click "Mark as Read", and verify the badge count decrements.
* **Acceptance Scenarios**:
  1. **Given** unread notifications for a Student, **When** they log in to the Student dashboard, **Then** they must see an unread badge indicating the count of new notifications in the header.
  2. **Given** a Student, **When** they navigate to `/student/notifications`, **Then** they must see the full list of in-app notifications.
  3. **Given** an unread notification, **When** the student clicks "Mark as Read", **Then** the notification status must update to read, and it must visually style as read.

---

### User Story 4 - Assistant Tasks Endpoint Authorization (Priority: P1)
As a security administrator, I want all task query/command endpoints under `api/v1/assistant/tasks/my/*` in `AssistantController` to be explicitly protected with role authorization attributes, rejecting requests from students or teachers.

* **Independent Test**: Send a request to `/api/v1/assistant/tasks/my` using a Student token, and confirm the API returns a `403 Forbidden` or `401 Unauthorized` status.
* **Acceptance Scenarios**:
  1. **Given** a Student, **When** they request `/api/v1/assistant/tasks/my`, **Then** the response must be `403 Forbidden` or `401 Unauthorized`.
  2. **Given** a Teacher, **When** they request `/api/v1/assistant/tasks/my`, **Then** the response must be `403 Forbidden` or `401 Unauthorized`.
  3. **Given** an Assistant or Supervisor, **When** they request `/api/v1/assistant/tasks/my`, **Then** the response must return `200 OK`.

---

## Requirements

### Functional Requirements
* **FR-001**: Add the `GET /api/teacher/activity` API endpoint in `TeacherController` returning active student watch events, most watched videos, and alerts for inactive students (>7 days).
* **FR-002**: Create the `/teacher/activity` Next.js frontend page displaying stats, recent watch logs, and alerts in a premium RTL Arabic layout.
* **FR-003**: Add the `GET /api/student/profile` and `PUT /api/student/profile` API endpoints in `StudentController` allowing retrieval and updates of student profile details.
* **FR-004**: Create the `/student/profile` Next.js page displaying registration info, device limits, and forms to update contact info.
* **FR-005**: Add `GET /api/student/notifications` and `POST /api/student/notifications/{id}/read` in `StudentController`.
* **FR-006**: Create the `/student/notifications` Next.js page to manage in-app notifications.
* **FR-007**: Restrict all `my/*` actions in `AssistantController` by applying `[Authorize(Roles = "Admin,Supervisor,Assistant,Staff,AssistantAcademic,AssistantReviewer")]`.
* **FR-008**: Implement C# integration tests in `TeacherIsolationTests.cs` and python integration tests in `tests/` verifying the assistant permission matrix and teacher binding constraints.

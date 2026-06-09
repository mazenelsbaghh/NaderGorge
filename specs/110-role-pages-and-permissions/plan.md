# Technical Plan: Role Pages and Permissions Completion

**Branch**: `110-role-pages-and-permissions` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)

## Proposed Changes

### Backend Queries and Commands

#### [NEW] [TeacherActivity.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Teacher/TeacherActivity.cs)
Create MediatR query `GetTeacherActivityQuery` and handler returning:
- `ActiveStudents`: Students with watch events in teacher packages.
- `MostWatchedVideos`: Top lesson videos by watch count.
- `InactiveStudentAlerts`: Students enrolled in teacher's packages inactive for > 7 days.

#### [NEW] [GetStudentProfileQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentProfileQuery.cs)
Create MediatR query returning:
- Personal details (full name, phone, DateOfBirth, Governorate, District, Address, Gender, GradeLevel, SchoolName).
- Device Limits: Registered device count vs system limit.

#### [NEW] [UpdateStudentProfileCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Commands/UpdateStudentProfileCommand.cs)
Create MediatR command to update editable student contact fields (Address, SecondaryPhone, ParentPhone, MotherPhone, SchoolName).

#### [NEW] [GetStudentNotificationsQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentNotificationsQuery.cs)
Create MediatR query returning all in-app notifications for the logged-in student.

#### [NEW] [MarkNotificationAsReadCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Commands/MarkNotificationAsReadCommand.cs)
Create MediatR command to mark a specific notification as read by setting `ReadAt` timestamp.

---

### Controllers

#### [MODIFY] [TeacherController.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Controllers/TeacherController.cs)
Expose:
- `GET /api/teacher/activity` -> `GetTeacherActivityQuery`

#### [MODIFY] [StudentController.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Controllers/StudentController.cs)
Expose:
- `GET /api/student/profile` -> `GetStudentProfileQuery`
- `PUT /api/student/profile` -> `UpdateStudentProfileCommand`
- `GET /api/student/notifications` -> `GetStudentNotificationsQuery`
- `POST /api/student/notifications/{id}/read` -> `MarkNotificationAsReadCommand`

#### [MODIFY] [AssistantController.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Controllers/AssistantController.cs)
Apply explicit `[Authorize(Roles = "Admin,Supervisor,Assistant,Staff,AssistantAcademic,AssistantReviewer")]` to:
- `GET my` (GetMyTasks)
- `GET my/{id:guid}` (GetTaskDetails)
- `POST my/{id:guid}/status` (UpdateStatus)
- `POST my/{id:guid}/comments` (AddComment)

---

### Frontend Pages and Layouts

#### [NEW] [activity/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/teacher/activity/page.tsx)
Add teacher activity log page with cards for active students, top watched videos, and inactivity alerts.

#### [NEW] [profile/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/profile/page.tsx)
Add student profile page showing registration parameters and forms to modify editable fields.

#### [NEW] [notifications/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/notifications/page.tsx)
Add student notifications view listing announcements with mark-as-read toggles.

#### [MODIFY] [StudentShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/layout/StudentShellChrome.tsx)
Integrate `/student/profile` and `/student/notifications` inside the navigation items and render an unread notification count badge in the header/mobile nav.

---

### Integration Tests

#### [NEW] [test_assistant_permissions.py](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/test_assistant_permissions.py)
Write Python integration tests verifying that student/teacher roles cannot access assistant task APIs.

#### [MODIFY] [TeacherIsolationTests.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/tests/NaderGorge.Application.Tests/MultiTeacher/TeacherIsolationTests.cs)
Add unit/integration test assertions verifying teacher binding rules for package ownership, exam boundaries, and revenue association.

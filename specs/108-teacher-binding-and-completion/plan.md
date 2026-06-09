# Implementation Plan: Teacher Binding and Teacher/Student Completion

This document outlines the proposed changes to enforce strict teacher/subject resource binding, implement the teacher dashboard and workspace pages (students list, profile updates, and essay grading), and display proper teacher branding in the student portal.

## User Review Required

> [!IMPORTANT]
> **No Default Fallback Enforcement**: We will remove the default fallback Guid checks in `AdminExamCommands.cs` and `BulkGenerateCodesCommand.cs` and return validation errors instead. Ensure that Admins explicitly specify the correct teacher and program for all codes and exams.

---

## Proposed Changes

### [Backend Component]

Enforce strict validation, prevent fallback GUIDs, and add the new teacher surface APIs.

#### [NEW] [TeacherController.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Controllers/TeacherController.cs)
- Expose the teacher workspace endpoints:
  - `GET /api/v1/teacher/dashboard/stats`
  - `GET /api/v1/teacher/students`
  - `GET /api/v1/teacher/essays`
  - `POST /api/v1/teacher/essays/{id}/grade`
  - `GET /api/v1/teacher/profile`
  - `PUT /api/v1/teacher/profile`

#### [NEW] [TeacherQueriesAndCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Teacher/QueriesAndCommands.cs)
- Implement queries:
  - `GetTeacherDashboardStatsQuery`: Computes active student count, packages, exams, and pending essays count.
  - `GetTeacherStudentsQuery`: Lists students enrolled in the teacher's packages.
  - `GetPendingTeacherEssaysQuery`: Lists submissions with status `WaitTeacher` for this teacher's questions.
  - `GetTeacherProfileQuery`: Reads teacher profile details.
- Implement commands:
  - `UpdateTeacherProfileCommand`: Updates bio, specialization, contact, and profile image.

#### [MODIFY] [AdminExamCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs)
- Remove fallback default teacher ID and default subject ID. Throw an exception or return a Fail response if the teacher or subject cannot be resolved.

#### [MODIFY] [BulkGenerateCodesCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/BulkGenerateCodesCommand.cs)
- Remove fallback default teacher ID. Return `ApiResponse.Fail` if the target resource does not belong to a teacher and cannot be resolved.

#### [MODIFY] [GetPackageDetailsQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Queries/GetPackageDetailsQuery.cs) (or similar queries)
- Ensure the package details includes the teacher's profile properties (FullName, Bio, Specialization, ProfileImageUrl).

---

### [Frontend Component]

Implement the teacher pages and student-side branding.

#### [MODIFY] [teacher-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/teacher-service.ts)
- Add new endpoints:
  - `getDashboardStats`
  - `getStudents`
  - `getEssays`
  - `gradeEssay`
  - `getMyProfile`
  - `updateMyProfile`

#### [NEW] [teacher/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/teacher/page.tsx) (Overwrite or enhance)
- Replace static placeholders with real dashboard statistics and recent activity items.

#### [NEW] [teacher/students/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/teacher/students/page.tsx)
- Render the list of students associated with the teacher's packages.

#### [NEW] [teacher/essays/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/teacher/essays/page.tsx)
- Renders the list of essays awaiting grading. Allows teachers to click, view answers, and submit grades.

#### [NEW] [teacher/profile/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/teacher/profile/page.tsx)
- Profile details form to edit Bio, Specialization, Contact Info, and Profile Image URL.

#### [MODIFY] [student/packages/[packageId]/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/student/packages/%5BpackageId%5D/page.tsx)
- Render a dedicated "عن المدرس" (About the Teacher) card showing the teacher's avatar, name, and specialization.

---

## Verification Plan

### Automated Tests
- Run unit tests to check fallback restrictions:
  `dotnet test`
- Run integration tests to verify multi-teacher isolation:
  `python3 -m pytest tests/`

### Manual Verification
- Log in as Teacher, check dashboard stats, edit profile, and verify changes reflect on student package details pages.
- Grade an essay as a teacher and verify the student can see their updated score.

# Feature Specification: Teacher Binding and Teacher/Student Completion

**Feature Branch**: `108-teacher-binding-and-completion`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 3 - Teacher Binding and Teacher/Student Completion: Build teacher surface dashboard with actual data, bind all resources to specific teachers and subjects, prevent fallbacks, list and grade essays, and show teacher branding in student portals."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Real-Data Teacher Dashboard and Statistics (Priority: P1)

As a teacher, when I log in to the teacher portal, I want to see real-time statistics (total active students, total packages, total exams, pending essays to grade, and recent activity feed), so that I can monitor my work and earnings at a glance.

**Why this priority**: Core landing surface for teachers. Replacing hardcoded values with real statistics is vital for usability and trust.

**Independent Test**:
- Log in as Teacher A.
- Navigate to the teacher portal.
- Verify that statistics like "عدد الطلاب النشطين" (Active Students), "باقاتي الدراسية" (My Packages), and "امتحاناتي" (My Exams) show real counts from the database instead of hardcoded placeholders.
- Verify that a list of recent activities/logs is loaded.

**Acceptance Scenarios**:
1. **Given** an authenticated Teacher, **When** they load the teacher dashboard, **Then** the system MUST fetch and display their specific metrics (active student count, package count, exam count, and pending essays count) from the database.
2. **Given** a Teacher who has no packages or students, **When** they load the dashboard, **Then** the stats MUST display `0` rather than crashing or showing null/empty values.

---

### User Story 2 - Forced Teacher Binding & Fallback Prevention (Priority: P1)

As a teacher, when I create packages, generate access codes, add questions, or design exams, I want the system to bind these resources strictly to my profile and prevent any fallback to default teachers, so that my data is completely isolated and secure.

**Why this priority**: Critical for multi-teacher isolation. Orphaned resources or automatic defaults lead to data leaks and accounting errors.

**Independent Test**:
- Log in as Admin, create a package or exam without assigning a teacher, and verify that the API returns a validation error instead of defaulting.
- Log in as Teacher, generate access codes, and verify in the database that `TeacherId` matches the logged-in teacher's profile.

**Acceptance Scenarios**:
1. **Given** any resource creation command (Package, Exam, Question, Code Group), **When** no teacher ID can be resolved, **Then** the system MUST reject the command with a validation error (no default fallback allowed).
2. **Given** Teacher A generates access codes or creates a question, **When** persisted, **Then** the resource's `TeacherId` / `CreatedByTeacherId` MUST be set to Teacher A's ID.

---

### User Story 3 - Teacher Management Pages: Student List & Essay Grading (Priority: P2)

As a teacher, I want to view my student roster with their progress logs, edit my public profile (bio, avatar, specialization), and view/grade essay submissions from my portal, so that I can manage my courses and evaluate student performance.

**Why this priority**: Required workflow tools for daily teacher interactions.

**Independent Test**:
- Log in as Teacher A, navigate to `/teacher/students` and check the student list.
- Navigate to `/teacher/exams` or `/teacher/essays`, click on a pending student essay, enter a score/feedback, and verify that status changes to `TeacherGraded` and the student receives their score.
- Navigate to `/teacher/profile`, edit details, save, and verify they update.

**Acceptance Scenarios**:
1. **Given** a student who activated Teacher A's package, **When** Teacher A views `/teacher/students`, **Then** the student MUST appear in their roster list.
2. **Given** a student who submitted an essay exam, **When** Teacher A views pending essays, **Then** only essays matching questions created by Teacher A MUST be shown.
3. **Given** a pending essay submission, **When** Teacher A submits a grade and feedback, **Then** the submission status MUST transition to `TeacherGraded` and save the teacher's profile ID as the grader.

---

### User Story 4 - Student Portal Teacher Branding (Priority: P3)

As a student, when I browse my active packages, terms, or lessons, I want to see the name, profile picture, and subject of the teacher who created the course, so that I have a personalized, branded experience.

**Why this priority**: Enhances visual appeal and clarifies course ownership in a multi-teacher platform.

**Independent Test**:
- Log in as Student.
- Open package details.
- Verify that the teacher's avatar and name are displayed at the top of the package details and lesson pages.

**Acceptance Scenarios**:
1. **Given** a package details view, **When** loaded, **Then** the system MUST display the teacher's profile details (Name, Bio, Avatar, Specialization).
2. **Given** a lesson view, **When** loaded, **Then** it MUST display the package creator's teacher identity badge.

---

### Edge Cases

- **Teacher Profile Missing/Un-onboarded**: If a user has the "Teacher" role but no `TeacherProfile` record, they must be redirected to a profile completion form rather than crashing the portal.
- **Cross-Teacher Resource Updates**: If Teacher A tries to update a package, code, question, or exam owned by Teacher B, the backend MUST reject it with a `403 Forbidden` exception.
- **Essay Re-grading**: If an essay has already been graded, the grading workspace must display it as graded and prevent multiple submissions unless explicitly permitted.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin/Teacher Flow**: Log in as Admin, create a Teacher, link them to a Subject, create a package. Log in as that Teacher, verify the package shows on their dashboard, generate codes for it, and verify the codes are linked to the teacher.
- **Manual QA Student Flow**: Log in as Student, activate code, open lessons, verify that the correct teacher's name and avatar show.
- **Docker Acceptance**: `docker compose ps` shows all containers are healthy, Nginx routes subdomains correctly, and E2E static/runtime tests pass.
- **External Dependencies**: Local Database (PostgreSQL).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Introduce `GET /api/v1/teacher/dashboard` returning real statistics (active students, packages, exams, pending essays) and recent transactions/logs for the logged-in teacher.
- **FR-002**: Introduce `GET /api/v1/teacher/students` listing students who activated packages belonging to the logged-in teacher.
- **FR-003**: Introduce `GET /api/v1/teacher/essays` listing pending essay submissions (`WaitTeacher` status) for questions owned by the logged-in teacher.
- **FR-004**: Introduce `POST /api/v1/teacher/essays/{id}/grade` allowing the teacher to grade an essay.
- **FR-005**: Introduce `GET /api/v1/teacher/profile` and `PUT /api/v1/teacher/profile` to view and update bio, specialization, profile image, and contact details.
- **FR-006**: Remove hardcoded fallbacks to default teacher GUID (`b4b82937-293e-48a3-a002-decf9a1efab8`) and default subject GUID (`d9b8a342-990a-4286-905e-fdebb2e3895e`) in `AdminExamCommands.cs` and `BulkGenerateCodesCommand.cs`. Raise a validation error if teacher or subject cannot be resolved.
- **FR-007**: Ensure `GetTeacherAccountQuery` and other finance endpoints strictly authorize that the logged-in teacher can only view their own account details.
- **FR-008**: Update frontend student package details and lesson pages to render teacher info (name, bio, specialization, avatar URL).

### Key Entities *(include if feature involves data)*

- **TeacherProfile**: Profile metadata for a teacher. Key attributes:
  - `Bio` (string)
  - `Specialization` (string)
  - `ProfileImageUrl` (string?)
  - `ContactInfo` (string)
- **EssaySubmission**: Student essay exam answer. Key attributes:
  - `TeacherFinalScore` (decimal?)
  - `TeacherFeedback` (string?)
  - `GradedByTeacherId` (Guid?, references TeacherProfile)
  - `Status` (Enum: WaitAI, AIScored, WaitTeacher, TeacherGraded)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of teacher dashboard statistics are fetched dynamically from the database using the teacher's profile.
- **SC-002**: Backend throws `ValidationException` or returns 400 Bad Request instead of defaulting when teacher/subject cannot be resolved in exam or code generation.
- **SC-003**: Teacher A is blocked from reading, modifying, or grading resources (packages, questions, exams, essays) owned by Teacher B, returning `403 Forbidden` in 100% of test cases.
- **SC-004**: Next.js frontend builds without errors or warnings.

## Assumptions

- We assume the `TeacherProfile` table already has entries or is scaffolded.
- We assume that `TeacherAuthorizationService` already provides base helper checks like `CanAccessPackageAsync`.

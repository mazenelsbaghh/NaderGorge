# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Multi-Teacher Multi-Subject Architecture and Teacher Isolation

**Input**: Design documents from `/specs/092-multi-teacher-multi-subject-architecture/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

## Phase 1: Foundational (Database & Seeding)

**Goal**: Establish the DB tables, relationships, and data migration.

- [x] T001 Create `Subject` model in `backend/src/NaderGorge.Domain/Entities/Subject.cs` with fields: `Name`, `NormalizedName`, `Description`, `TeacherSubjects`, `Programs`, `QuestionBankItems`.
- [x] T002 Create `TeacherProfile` model in `backend/src/NaderGorge.Domain/Entities/TeacherProfile.cs` with fields: `UserId`, `User`, `Bio`, `Specialization`, `CommissionRate`, `ProfileImageUrl`, `ContactInfo`, `TeacherSubjects`.
- [x] T003 Create `TeacherSubject` join model in `backend/src/NaderGorge.Domain/Entities/TeacherSubject.cs` with fields: `TeacherId`, `Teacher`, `SubjectId`, `Subject`.
- [x] T004 Update `User` model in `backend/src/NaderGorge.Domain/Entities/User.cs` to add `TeacherProfile? TeacherProfile` navigation property.
- [x] T005 Update `Program` model in `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs` to add `SubjectId` and `Subject` navigation.
- [x] T006 Update `Package` model in `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs` to add `TeacherId` and `Teacher` (TeacherProfile) navigation.
- [x] T007 Update `CodeGroup` model in `backend/src/NaderGorge.Domain/Entities/CodeEntities.cs` to add `TeacherId` and `Teacher` (TeacherProfile) navigation.
- [x] T008 Update `Exam` model in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs` to add `CreatedByTeacherId` and `Teacher` (TeacherProfile) navigation.
- [x] T009 Update `QuestionBankItem` model in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs` to add `CreatedByTeacherId` (TeacherProfile) and `SubjectId` (Subject) navigation.
- [x] T010 Update `EssaySubmission` model in `backend/src/NaderGorge.Domain/Entities/EssaySubmission.cs` to add `GradedByTeacherId` (TeacherProfile, nullable) navigation.
- [x] T011 Update `IAppDbContext` interface in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` to expose `DbSet<Subject>`, `DbSet<TeacherProfile>`, and `DbSet<TeacherSubject>`.
- [x] T012 Update DbContext configuration in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`:
  - Expose DbSet properties.
  - Define primary keys and relationships in `OnModelCreating` (e.g. composite key for `TeacherSubject`, configure foreign keys).
- [x] T013 Create EF Core migration: run `dotnet ef migrations add AddMultiTeacherSubjectArchitecture` and verify generated migration file.
- [x] T014 Implement Data Migration in the migration file:
  - Add SQL commands to create a default "History" Subject and a default Teacher Profile linked to the default Admin user (or a seeded default Teacher user).
  - Update existing Programs to point to the default Subject ID.
  - Update existing Packages, CodeGroups, Exams, and QuestionBankItems to point to the default Teacher Profile ID.
  - Set the foreign keys to NOT NULL.
- [x] T015 Run database migration: `dotnet ef database update`.

**Checkpoint**: Foundational database structure is complete.

---

## Phase 2: User Story 1 - Admin Subject and Teacher Onboarding (US1)

**Goal**: Enable administrators to create and edit Subjects and Teacher Profiles.

### Tests for User Story 1
- [x] T016 Write unit tests in `backend/tests/NaderGorge.Application.Tests/MultiTeacher/SubjectTests.cs` to verify Subject CRUD operations.
- [x] T017 Write unit tests in `backend/tests/NaderGorge.Application.Tests/MultiTeacher/TeacherProfileTests.cs` to verify Teacher onboarding operations.

### Backend Implementation
- [x] T018 Create MediatR Commands & Handlers for Subject in `backend/src/NaderGorge.Application/Features/Admin/Commands/`: `CreateSubjectCommand`, `UpdateSubjectCommand`, `DeleteSubjectCommand`.
- [x] T019 Create MediatR Queries & Handlers for Subject in `backend/src/NaderGorge.Application/Features/Admin/Queries/`: `GetSubjectsQuery`, `GetSubjectByIdQuery`.
- [x] T020 Create MediatR Commands & Handlers for Teacher in `backend/src/NaderGorge.Application/Features/Admin/Commands/`: `CreateTeacherProfileCommand`, `UpdateTeacherProfileCommand`.
- [x] T021 Create MediatR Queries & Handlers for Teacher in `backend/src/NaderGorge.Application/Features/Admin/Queries/`: `GetTeachersQuery`, `GetTeacherByIdQuery`.
- [x] T022 Expose endpoints in `backend/src/NaderGorge.API/Controllers/AdminController.cs` for managing Subjects and Teachers.

### Frontend Implementation
- [x] T023 Create reusable Axios API Service in `frontend/src/services/teacher-service.ts` for subjects and teachers endpoints.
- [x] T024 Create Subjects admin component in `frontend/src/app/admin/subjects/page.tsx` for viewing, creating, and editing subjects.
- [x] T025 Create Teachers admin component in `frontend/src/app/admin/teachers/page.tsx` for viewing, creating, and editing teacher profiles.
- [x] T026 Update admin navigation links in `frontend/src/app/admin/layout.tsx` to include "Subjects" and "Teachers" links.

**Checkpoint**: Admin can fully manage Subjects and Teachers.

---

## Phase 3: User Story 2 - Complete Content and Code Isolation (US2)

**Goal**: Enforce absolute data isolation so that Teachers can only view and manage their own resources.

### Tests for User Story 2
- [x] T027 Write unit tests in `backend/tests/NaderGorge.Application.Tests/MultiTeacher/TeacherIsolationTests.cs` verifying that a handler executing under Teacher A receives a 403/404 when querying Teacher B's package, code, or exam.

### Backend Implementation
- [x] T028 Update all Content Command and Query handlers (e.g., packages, programs, sections, lessons) to:
  - Extract the current user's ID and Role.
  - If the user is a Teacher, automatically filter queries by the Teacher's `TeacherId`.
  - Validate that any modification command is targeting a package owned by the current Teacher.
- [x] T029 Update Code Group handlers to filter and authorize by the current Teacher's ID.
- [x] T030 Update Exam, Question Bank, and Essay Submission handlers to filter and authorize by the current Teacher's ID.
- [x] T031 Create generic authorization behavior or interceptor in backend to reject unauthorized resource access.

### Frontend Implementation
- [x] T032 Build the Teacher Dashboard layout under `frontend/src/app/teacher/layout.tsx` with sidebar navigation.
- [x] T033 Build the Teacher Dashboard home page under `frontend/src/app/teacher/page.tsx` showing basic stats (active students, active packages, generated codes).
- [x] T034 Build the Teacher Packages page under `frontend/src/app/teacher/packages/page.tsx` to manage packages owned by the logged-in teacher.
- [x] T035 Build the Teacher Codes page under `frontend/src/app/teacher/codes/page.tsx` to view/generate codes for packages owned by the teacher.
- [x] T036 Build the Teacher Exams page under `frontend/src/app/teacher/exams/page.tsx` to create and grade exams.
- [x] T037 Update admin content views (packages, codes, exams lists) to display a dropdown filter for Subject and Teacher, allowing admins to filter content by teacher.

**Checkpoint**: Complete data isolation is enforced, and the teacher dashboard is operational.

---

## Phase 4: User Story 3 - Multi-Teacher Student Dashboard (US3)

**Goal**: Group student package list by subject, show teacher info, and adapt landing page/FAQ.

### Backend Implementation
- [x] T038 Create public query handler `GetActiveTeachersQuery` in Application layer to return all teacher profiles and bios for the landing page.
- [x] T039 Update public packages query to group/categorize by Subject and return Teacher details (name, photo) inside Package DTO.

### Frontend Implementation
- [x] T040 Update `frontend/src/components/landing/data.ts` and Landing/FAQ pages to fetch teacher list dynamically from the new public endpoint, and remove hardcoded "History only" text.
- [x] T041 Update Packages index page `frontend/src/app/(public)/packages/page.tsx` to display packages categorized by Subject with teacher bio cards.
- [x] T042 Update Student Dashboard `frontend/src/app/student/page.tsx` to display active packages categorized by subject, showing the teacher's profile picture and name.
- [x] T043 Update registration code activation modal to display the target teacher's photo and name before confirming activation.

**Checkpoint**: Student-facing screens display teachers and subjects dynamically.

---

## Phase 5: Polish & Final Verification

**Goal**: Review, compile and run testing suite.

- [x] T044 Run backend test suite: `dotnet test backend/NaderGorge.sln`.
- [x] T045 Build the frontend: run `npm run lint && npm run build` inside `frontend/`.
- [x] T046 Verify docker compose health: run `make up && make migrate`.
- [x] T047 Write final verification report and update progress markers.

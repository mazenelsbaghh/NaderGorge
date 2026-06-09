# Feature Specification: Multi-Teacher & Multi-Subject Architecture Enforcement

**Feature Branch**: `102-multi-teacher-enforcement`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "تطبيق متطلبات ربط أي إضافة بالمدرس والمادة وإصلاح نواقص هيكلية المواد المتعددة والمدرسين المتعددين وتحديث صفحة الأسئلة والـ Landing والـ FAQ وتشغيل Launch Drill كامل."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Explicit Teacher/Subject Assignment during Admin Package Creation (Priority: P1)
As an Admin, when creating a study package, I want to explicitly select the Teacher and the Program/Subject from a dropdown menu, so that the package is correctly linked to the chosen teacher rather than falling back to a default teacher.

**Why this priority**: High priority as it is the foundation of multi-teacher separation.

**Independent Test**:
- Log in as Admin.
- Navigate to package creation dashboard.
- Create a package and verify that the package lists the explicitly selected teacher and subject.
- Verify that saving fails if no teacher or program/subject is selected.

**Acceptance Scenarios**:
1. **Given** an authenticated Admin user, **When** they fill the package creation form, **Then** they MUST be forced to select a Teacher and a Program (which is tied to a Subject).
2. **Given** an invalid combination (e.g., a teacher who does not teach the subject of the selected program), **When** submitted, **Then** the system MUST reject the creation with a clear validation error.

---

### User Story 2 - Program Scope Restriction for Teacher Package Creation (Priority: P1)
As a Teacher, when creating a package, I want to only select from Programs/Subjects that I am authorized to teach, so that I cannot mistakenly link my packages to other teachers' programs.

**Why this priority**: High priority to prevent cross-teacher data contamination.

**Independent Test**:
- Log in as Teacher A.
- Navigate to package creation.
- Check the program dropdown list and confirm it only displays programs/subjects linked to Teacher A.

**Acceptance Scenarios**:
1. **Given** an authenticated Teacher, **When** they load the package creation form, **Then** the system MUST populate the program/subject dropdown list with only those programs/subjects associated with their `TeacherProfile`.
2. **Given** a Teacher submits a package creation request for a program they do not teach, **When** evaluated, **Then** the backend MUST reject it with a `403 Forbidden` or validation error.

---

### User Story 3 - Forced Subject and Teacher Binding in Question Creation (Priority: P1)
As an Admin or Teacher, when adding a new question to the question bank, I want to select the Subject and Teacher (if Admin) or have the system automatically set the Teacher (if Teacher), so that questions are never orphaned or linked to default teachers/subjects.

**Why this priority**: High priority to maintain clean data categorization.

**Independent Test**:
- Log in as Admin, create a question, select a subject and teacher, and verify that the created question is linked to both.
- Log in as Teacher, create a question, select a subject, and verify that the created question is automatically linked to that teacher.

**Acceptance Scenarios**:
1. **Given** an Admin creating a question, **When** they fill the form, **Then** the form MUST require selecting a Subject and a Teacher.
2. **Given** a Teacher creating a question, **When** they fill the form, **Then** the form MUST require selecting a Subject (restricted to subjects they teach) and automatically assign their `TeacherProfile` ID.

---

### User Story 4 - General Landing and FAQ Localization Fixes (Priority: P2)
As a platform visitor, I want to read up-to-date information on the landing and FAQ pages that accurately reflects a multi-teacher, multi-subject platform (not history-exclusive), so that I know the platform covers all subjects.

**Why this priority**: Medium priority for brand alignment.

**Independent Test**:
- View the FAQ page and landing page copy and verify that there is no history-exclusive wording.

**Acceptance Scenarios**:
1. **Given** a visitor loading `/faq`, **When** the page renders, **Then** all statements regarding "التاريخ فقط" (History only) MUST be removed or updated to reflect multiple subjects.
2. **Given** the teachers section on the landing page, **When** loaded, **Then** it MUST dynamically fetch active teachers from `/api/public/teachers`, and fallback gracefully to localized placeholders only if the API returns empty list.

---

### Edge Cases

- **Teacher Profile Missing**: If a newly registered teacher has no `TeacherProfile` record yet, the system MUST prevent them from creating packages or questions, prompting them to contact support.
- **De-linking Subjects**: If a subject is unassigned from a teacher, existing packages/questions MUST remain linked, but new packages/questions for that subject MUST be blocked for that teacher.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Flow**: Log in as Admin (`20000000000`/`password`), create a package and question, explicitly assigning different teachers and subjects, and verify the correct associations in the database/UI.
- **Manual QA Teacher Isolation Flow**: Log in as Teacher A, verify they cannot see or modify packages/questions created by Teacher B.
- **Docker Acceptance**: Run a cold-start rebuild (`make down && docker compose build --no-cache && make up && make migrate`) and confirm Kestrel and Next.js start successfully with clean database state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `CreatePackageCommand` and API request MUST accept `TeacherId` and `ProgramId` explicitly from Admins.
- **FR-002**: Backend validators MUST verify that the selected Teacher is associated with the Subject of the selected Program.
- **FR-003**: Frontend Package creation modal/page for Admins MUST include dropdowns for `Teacher` and `Program`/`Subject`.
- **FR-004**: Frontend Package creation for Teachers MUST filter the `Program`/`Subject` dropdown to only display subjects linked to them.
- **FR-005**: Question creation UI (`admin/questions/page.tsx`) MUST force selection of a `Subject` and a `Teacher` (if Admin).
- **FR-006**: API routes `createQuestion` and `createPackage` in `adminService` MUST enforce explicit parameter schemas instead of `any`.
- **FR-007**: FAQ page (`faq/page.tsx`) MUST be updated to remove history-exclusive copy.
- **FR-008**: Landing page `CircularGallerySection` MUST dynamically render teachers fetched from `/api/public/teachers` and avoid static fallback unless empty.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of newly created packages and questions have non-default, explicitly assigned `TeacherId` and `SubjectId`.
- **SC-002**: Attempting to create a package or question without selecting a teacher or subject results in a validation failure.
- **SC-003**: All 33 Python E2E integration tests and 77 MediatR unit tests pass cleanly after a cold-start database migration.

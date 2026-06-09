# Feature Specification: Multi-Teacher Multi-Subject Architecture and Teacher Isolation

**Feature Branch**: `092-multi-teacher-multi-subject-architecture`  
**Created**: 2026-06-07  
**Status**: Draft  
**Input**: User description: "Phase 4 of the platform expansion plan: Multi-Teacher Multi-Subject Architecture and Teacher Isolation"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Subject and Teacher Onboarding (Priority: P1)

An administrator needs to define the subjects taught on the platform (e.g., Physics, History, Math) and onboard teachers. The admin creates teacher profiles linked to user accounts, defines their commission rates, and associates them with one or more subjects.

**Why this priority**: It is the baseline prerequisite for the multi-teacher system. Without onboarding teachers and subjects, we cannot assign content or codes.

**Independent Test**: Can be tested via the Admin panel by creating a new Subject and a new TeacherProfile, linking them together, and verifying they are listed correctly in the database and API.

**Acceptance Scenarios**:

1. **Given** an authenticated Administrator, **When** they navigate to the Subjects management page and add a new subject (e.g., "Physics"), **Then** the subject is saved and appears in the list of available subjects.
2. **Given** an authenticated Administrator, **When** they navigate to the Teachers management page, select an existing user with the "Teacher" role, and fill in bio, specialization, commission rate, and link them to "Physics", **Then** the TeacherProfile is successfully created.
3. **Given** an authenticated Administrator, **When** they view the list of teachers, **Then** they see each teacher's name, linked subjects, and bio details.

---

### User Story 2 - Complete Content and Code Isolation for Teachers (Priority: P1)

A teacher logs into the platform and can only see and manage their own packages, programs, codes, and exams. They must not see or modify any items created by or assigned to another teacher.

**Why this priority**: Data isolation is the core business requirement to support multiple independent teachers on a single platform.

**Independent Test**: Create two teacher accounts (Teacher A and Teacher B). Log in as Teacher A and create a Package, CodeGroup, and Exam. Log in as Teacher B and verify that none of these items are visible or accessible via queries.

**Acceptance Scenarios**:

1. **Given** Teacher A is logged in, **When** they view the packages list, **Then** only packages with `TeacherId` matching Teacher A are displayed.
2. **Given** Teacher A is logged in, **When** they view the Code Groups list, **Then** only code groups with `TeacherId` matching Teacher A are displayed.
3. **Given** Teacher A is logged in, **When** they attempt to retrieve a Package detail or Code Group detail belonging to Teacher B by direct ID query, **Then** the system returns a 403 Forbidden or 404 Not Found error.
4. **Given** Teacher A is logged in, **When** they view exam submissions or question banks, **Then** only items belonging to Teacher A's subjects/exams are shown.

---

### User Story 3 - Multi-Teacher Student Dashboard and Registration (Priority: P1)

A student visits the platform and sees packages categorized by subject and teacher. When the student activates a registration code, they gain access to the specific teacher's package, and their dashboard reflects the active teachers they are studying with.

**Why this priority**: Ensures the customer-facing side of the application works seamlessly with the new multi-teacher data architecture.

**Independent Test**: Log in as a student, activate a code for a package belonging to Teacher A, and verify that the package and teacher info (name, image) appear on the student dashboard, while packages from Teacher B remain locked or purchaseable.

**Acceptance Scenarios**:

1. **Given** a student is on the Landing or Packages page, **When** they browse available packages, **Then** the packages are grouped or filtered by Subject and show the name and photo of the associated Teacher.
2. **Given** a student has an inactive package, **When** they enter a registration code generated for Teacher A's package, **Then** the package is activated for the student and they are granted access.
3. **Given** a student logs into their dashboard, **When** they view their active studies, **Then** they see a list of teachers they have active packages with, alongside their profiles.

---

### Edge Cases

- **Teacher Deactivation**: If an administrator deactivates a teacher's account, new students should not be able to purchase or activate packages for that teacher. However, students who already have active subscriptions/activated codes for that teacher's packages must retain access until their package access duration expires.
- **Subject Removal**: An admin cannot delete a subject if it is currently linked to any active packages or programs.
- **Cross-Teacher Code Activation**: If Teacher A generates a code group, and a student attempts to activate that code against Teacher B's package, the system must reject the activation with a clear error.
- **Direct Resource Access via URL**: If a teacher or assistant manually crafts an API request to view or edit an exam, question bank item, or package belonging to another teacher, the backend must strictly authorize the request at the database/repository level and deny it.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin logs in, creates a new Subject (e.g., "Mathematics"), creates a new User with role "Teacher", creates a TeacherProfile for that user, and links them to "Mathematics".
- **Manual QA Role/Flow 2**: Admin creates a Package, links it to the newly created teacher. Creates a CodeGroup under that package.
- **Manual QA Role/Flow 3**: Log in as the new Teacher, navigate to `/teacher` dashboard. Verify they can see the package and codes they own, and can create an Exam. Verify they cannot see any packages or codes owned by other teachers.
- **Manual QA Negative Check**: Log in as Teacher B. Make a direct API call (or navigate via URL) to access Teacher A's exam edit page or code group list. Confirm that the page/request fails with a 403 or 404.
- **Docker Acceptance**: Run `make up` and `make migrate`. Run all backend unit and integration tests. Ensure `curl -f http://localhost:5245/api/health` returns status `Healthy`.
- **External Dependencies**: None. All features are self-contained.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support the creation, updating, and deletion of `Subject` entities by Administrators.
- **FR-002**: System MUST support the creation and updating of `TeacherProfile` entities by Administrators, linked to a user account that has the `Teacher` role.
- **FR-003**: System MUST support linking teachers to multiple subjects via a many-to-many relationship.
- **FR-004**: System MUST enforce that each `Program` is associated with a single `SubjectId`.
- **FR-005**: System MUST enforce that each `Package` is associated with a single `TeacherId` (which points to the User/TeacherProfile).
- **FR-006**: System MUST enforce that each `CodeGroup` is associated with a `TeacherId`.
- **FR-007**: System MUST enforce complete data isolation: a teacher (and their assistants, if any) can only query, edit, or delete packages, programs, codes, exams, question bank items, and student submissions that are associated with their `TeacherId`.
- **FR-008**: System MUST support a new dashboard surface (`/teacher`) specifically designed for teachers to manage their own students, packages, codes, and grades.
- **FR-009**: Student-facing package listings and registration flows MUST display the associated teacher's name and profile image, and group packages by subject.
- **FR-010**: System MUST include a database migration script to:
  - Create a default `Subject` and default `TeacherProfile` representing the current platform configuration.
  - Link all existing programs to the default subject.
  - Link all existing packages, code groups, exams, and question bank items to the default teacher.
  - Apply the foreign key constraints as required (nullable during migration, then non-nullable).
- **FR-011**: Landing Page FAQ and copy MUST be updated to remove references to the platform being dedicated to a single subject/teacher (e.g., Nader Gorge History).
- **FR-012**: Admin Dashboard MUST include filters for Subject and Teacher on all content management pages, and provide dedicated management views for Subjects and Teachers.

### Key Entities

- **Subject**: Represents an academic subject (e.g. History, Physics). Fields: `Id`, `Name`, `NormalizedName`, `Description`, `CreatedAt`.
- **TeacherProfile**: Profile metadata for a teacher. Fields: `Id`, `UserId` (FK to User), `Bio`, `Specialization`, `CommissionRate`, `ProfileImageUrl`, `ContactInfo`, `CreatedAt`.
- **TeacherSubject**: Many-to-many join table between `TeacherProfile` and `Subject`.
- **Program (Modified)**: Represents a course program. Added field: `SubjectId` (FK to Subject).
- **Package (Modified)**: Represents a purchaseable group of content. Added field: `TeacherId` (FK to User/TeacherProfile).
- **CodeGroup (Modified)**: Group of access codes. Added field: `TeacherId` (FK to User/TeacherProfile).
- **Exam (Modified)**: Represents an assessment. Added field: `CreatedByTeacherId` (FK to User/TeacherProfile).
- **QuestionBankItem (Modified)**: Pool of questions. Added fields: `SubjectId` (FK to Subject), `CreatedByTeacherId` (FK to User/TeacherProfile).
- **EssaySubmission (Modified)**: Essay exam submission. Added field: `GradedByTeacherId` (FK to User/TeacherProfile).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing packages, codes, and content are successfully migrated to the default teacher and subject without data loss.
- **SC-002**: Teachers can log in and manage their packages, codes, and exams. Cross-teacher access attempts yield a 403 Forbidden or 404 Not Found error in 100% of cases.
- **SC-003**: The landing page lists all active teachers dynamically from the API, and page load time remains under 2 seconds.
- **SC-004**: Student dashboard accurately displays the teachers of all active packages the student has joined.

## Assumptions

- We assume that teachers are registered as users in the system with the role "Teacher".
- We assume that administrative users (Admin role) retain overarching access to read and write all data across all subjects and teachers.
- We assume that the platform name and domain will remain unified, but subdomains/landing sections might be adapted in a future phase (e.g., Phase 11).

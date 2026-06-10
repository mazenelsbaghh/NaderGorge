# Feature Specification: Remove Programs and Link Packages Directly to Subjects

**Feature Branch**: `115-remove-programs-link-subject`  
**Created**: 2026-06-10  
**Status**: Draft  
**Input**: User description: "انا عايز يربط الماده بالصف الدراسي عادي فكك من البرجرام اصلا و غيرها كلها" (I want to link the subject to the grade level normally, forget about the program altogether and change it all)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Simplified Package Setup and Direct Subject/Grade Association (Priority: P1)

As a Teacher or Administrator, I want to create and manage packages by directly selecting the Subject and the Target Grade Level, without having to configure or select a "Program" (البرنامج الدراسي), so that the package creation process is faster, simpler, and less error-prone.

**Why this priority**: High priority because it removes a redundant domain concept (Program) that was causing confusion, 403 authorization issues, and unnecessary database layers.

**Independent Test**: Can be fully tested by creating a package for a teacher, selecting a subject and grade level directly, and verifying the package is successfully created and active.

**Acceptance Scenarios**:

1. **Given** a teacher or administrator is on the package creation form, **When** they fill in the package details, **Then** they should see a direct "Subject" (المادة) selection and a "Grade Level" (الصف الدراسي) selection instead of any "Program" dropdown.
2. **Given** a teacher is creating a package, **When** they view the Subject dropdown, **Then** only the subjects they are authorized/assigned to teach should be visible.
3. **Given** the form is submitted with a selected Subject and Grade Level, **When** the package is created, **Then** the package must be stored in the database with the direct Subject ID and Grade Level string, and all subsequent access logs and lesson associations function correctly.

---

### User Story 2 - Legacy Package Data Migration and Continuity (Priority: P2)

As an Administrator, I want all existing packages that were linked to "Programs" to be migrated to reference the corresponding "Subject" and "Grade Level" directly, so that no access grants, student purchases, or lessons are lost.

**Why this priority**: Essential to maintain data integrity and prevent breaking active student content access or course structures in production.

**Independent Test**: Verify that existing packages before migration still exist after migration, now linked directly to the subject and grade level, and can still be accessed by enrolled students.

**Acceptance Scenarios**:

1. **Given** packages in the database linked to programs containing specific subject IDs and grade levels, **When** the database migration runs, **Then** those packages must be updated with `SubjectId` and `TargetGrade` derived from the deleted programs, and the `programs` table is dropped.

---

### Edge Cases

- **What happens when a package has no associated grade level during migration?**  
  It will default to `"All"` (عام).
- **What happens if a teacher requests packages?**  
  The endpoint must return packages filtered by the teacher's allowed subjects directly, rather than filtering via programs, ensuring no 403 Forbidden errors.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin Role, URL `/admin/content` (or package management tab), create a package: verify you can select a Teacher, then select a Subject assigned to that teacher, select a Grade Level ("1st Secondary", "2nd Secondary", "3rd Secondary", or "All"), and successfully save.
- **Manual QA Role/Flow 2**: Teacher Role, URL `/teacher/packages`, create a package: verify you can select from the teacher's assigned subjects directly, select a Grade Level, and save.
- **Manual QA Negative Check**: A Teacher must not be able to create a package for a Subject they are not assigned to teach, and they must not see subjects belonging to other teachers.
- **Docker Acceptance**: `docker compose up` starts up all containers successfully, DB migrations apply cleanly without errors, and the backend service container is healthy.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST drop the `programs` table/entity entirely from the domain model and database.
- **FR-002**: System MUST add `SubjectId` (pointing to `subjects.Id`) and `TargetGrade` (string/varchar) directly to the `packages` table.
- **FR-003**: System MUST execute a database migration that populates the new `SubjectId` and `TargetGrade` columns on existing packages from their related programs before dropping the `programs` table.
- **FR-004**: System MUST update the C# backend Package domain model to reference `SubjectId` and `TargetGrade` instead of `ProgramId`.
- **FR-005**: System MUST update the package creation commands (both Admin and Teacher) to accept `SubjectId` and `TargetGrade` directly in their request DTOs.
- **FR-006**: System MUST update the teacher authorization checks to verify package ownership/access by checking the package's `SubjectId` directly against the teacher's assigned subjects.
- **FR-007**: System MUST update the Frontend forms for package creation to list subjects (filtered by teacher) and a grade level dropdown containing:
  - `"1st Secondary"` (الصف الأول الثانوي)
  - `"2nd Secondary"` (الصف الثاني الثانوي)
  - `"3rd Secondary"` (الصف الثالث الثانوي)
  - `"All"` (عام)

### Key Entities *(include if feature involves data)*

- **Package**: Represents a bundle of educational content. Now directly references:
  - `SubjectId` (foreign key to Subject)
  - `TargetGrade` (string designating grade level, e.g. "1st Secondary")
- **Subject**: Represents an academic subject (e.g. Mathematics, Physics). Each Package belongs to one Subject.
- **TeacherProfile**: Represents a teacher. Teachers are mapped to Subjects via a join table/relationship, which determines which packages they can manage.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Packages can be created in the Admin or Teacher dashboards with a direct Subject and Grade association in under 1 minute.
- **SC-002**: 100% of existing packages in the production database are migrated successfully without data loss or broken foreign keys.
- **SC-003**: No `403 Forbidden` errors occur when teachers view or create packages for their authorized subjects.

## Assumptions

- We assume the target grade level names in Arabic and English can be represented as strings in the database.
- We assume that any existing packages with invalid or missing program associations can default their `TargetGrade` to `"All"` during migration.

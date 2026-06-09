# Feature Specification: direct-teacher-creation

**Feature Branch**: `105-direct-teacher-creation`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "شيل التخصص التعليمي * وعايز احط المراحل الدراسيه و السنني بتاعتها انو بيدرس لمين من دول"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Direct Teacher Onboarding (Priority: P1)

Administrators can add a new teacher directly from the teachers management dashboard (`/admin/teachers`) by inputting both their user account credentials and their teacher profile details in a single form. This form includes a checklist to select the educational stages and years (grade levels) they teach. The legacy "Specialization" field is completely removed.

**Why this priority**: Core request. Enables fast onboarding of teaching staff with accurate academic stage assignment.

**Independent Test**: Administrators click "إضافة معلم جديد", fill in user credentials (Name, Phone, Password) and teacher details (Contact Info, Bio, Grade Checklist, Subjects), click save, and verify that the user account is created and the teacher profile is initialized with the selected grades.

**Acceptance Scenarios**:

1. **Given** the administrator is on the Teachers page,  
   **When** they click "إضافة معلم جديد" and enter a unique phone number, valid password, select one or more grade levels, contact email, and select one or more subjects, and click submit,  
   **Then** the system creates both the user account and the teacher profile, and shows a success toast.

---

### User Story 2 - Teacher List Table UX (Priority: P1)

The `/admin/teachers` dashboard presents the registered teachers in a clean, consistent data table format, showing their assigned educational stages/grades and subjects as badges, with no specialization or commission rate columns shown.

**Why this priority**: Standardizes the look-and-feel and presents the relevant stages/grades taught by each teacher.

**Acceptance Scenarios**:

1. **Given** multiple teachers are registered,  
   **When** the administrator loads `/admin/teachers`,  
   **Then** the teachers are listed in a table containing columns for the Teacher name & avatar, Phone number, Grades taught (badges), and Subjects (badges).

---

### User Story 3 - Complete Teacher Profile Modal (Priority: P2)

Clicking on a teacher in the table opens a comprehensive profile modal showing all details of the teacher, including their assigned educational stages and years, subjects, bio, and a chronological timeline of recent audit logs.

**Acceptance Scenarios**:

1. **Given** the teacher list table is loaded,  
   **When** the administrator clicks a teacher row,  
   **Then** a modal opens displaying the teacher's profile details along with badges for the educational stages and grades they teach, and their system activity log.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow adding a teacher in a single modal form by providing Full Name, Phone Number, Password, Contact Info, Bio (optional), Grades Checklist, and Subjects.
- **FR-002**: System MUST completely hide or remove the "Specialization" and "Commission Rate" fields from the forms and tables.
- **FR-003**: The system MUST store the selected educational stages and grade levels as a comma-separated list of grade keys inside the backend `Specialization` string field to avoid schema modifications.
- **FR-004**: The Teachers table and profile modal MUST parse the `Specialization` string back into grade keys and render them as friendly Arabic badges.

## Success Criteria *(mandatory)*

- **SC-001**: 100% of Specialization and Commission Rate inputs are removed from the frontend Teachers workspace.
- **SC-002**: Teachers' assigned grades are displayed as friendly badges.

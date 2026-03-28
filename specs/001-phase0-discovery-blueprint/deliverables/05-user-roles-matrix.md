# 05 — User Roles Matrix

## Role Definitions
The platform supports five primary roles:
1. **Student:** The end-user consuming educational content and taking assessments.
2. **Parent:** Guardian with read-only access to their linked Student(s)' progress and attendance (Future portal; currently data-model only).
3. **Assistant:** Staff member assisting the Teacher with operations and follow-up. (See Sub-Roles below).
4. **Teacher:** Content creator and primary educator. Has full rights to create/edit lessons, packages, and exams, but does *not* necessarily manage financial codes or platform-wide settings.
5. **Admin:** Super-user with absolute control over everything, including user management, code generation, and financial reporting. Initially held by Nader George.

## Multi-Role Model
A user account is not limited to a single role. The system implements a **multi-role assignment model**.
- Example: Nader George's account will have both `Teacher` and `Admin` roles.
- Example: A top student might be granted an `Assistant` (Support) role while still retaining their `Student` role to study.

## Assistant Sub-Roles
Because the physical center employs dozens of assistants, their digital access must be strictly scoped to prevent unauthorized data access or accidental deletions:
1. **Academic Assistant:** Can create lessons, upload videos, and write quizzes. Cannot publish immediately without Teacher approval.
2. **Homework Reviewer:** Can only access the grading pipeline. They view submitted essays, assign scores, and leave feedback.
3. **Follow-up Assistant:** Can view student watch logs, exam scores, and warning flags. Allowed to trigger WhatsApp notifications. Cannot edit content.
4. **Support Assistant:** Can reset passwords, check code validity, and resolve login issues. Cannot view academic performance.

## Permission Matrix
*(A high-level matrix mapping Role to Feature Access)*

| Feature / Domain | Admin | Teacher | Assistant (Academic) | Assistant (Reviewer) | Assistant (Follow-up) | Assistant (Support) | Student |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **System Settings** | Full | None | None | None | None | None | None |
| **Code Generation** | Full | None | None | None | None | Read | None |
| **Content Creation**| Full | Full | Draft | None | None | None | None |
| **Publish Content** | Full | Full | None | None | None | None | None |
| **Student Grading** | Full | Full | None | Full | Read | None | Own |
| **Student Tracking**| Full | Full | None | None | Full | None | Own |
| **Support Tools**   | Full | None | None | None | None | Full | None |

## Role Extension Procedure
To add new roles or sub-roles in future phases:
1. Define the new `Role` entity and insert it via migration.
2. Map the required granular `Permission` enums to the new role using the system configuration UI (Admin only).
3. Code changes are not required for arbitrary role combinations, as UI rendering and API endpoints check for `Permissions`, not hardcoded role names.

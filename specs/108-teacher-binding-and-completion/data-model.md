# Data Model: Teacher Binding and Resource Constraints

This document defines the relationships, validations, and constraints on the database models for the Teacher Binding and Completion features.

## Entity Relationships & Constraints

### 1. TeacherProfile
- `Packages` (1-to-many): A teacher profile owns multiple packages. Foreign key: `Package.TeacherId`.
- `Exams` (1-to-many): A teacher profile designs multiple exams. Foreign key: `Exam.CreatedByTeacherId`.
- `CodeGroups` (1-to-many): Access codes generated for their packages. Foreign key: `CodeGroup.TeacherId`.
- `QuestionBankItems` (1-to-many): Question bank items created by this teacher. Foreign key: `QuestionBankItem.CreatedByTeacherId`.
- `EssaySubmissions` (1-to-many): Essay answers graded by this teacher. Foreign key: `EssaySubmission.GradedByTeacherId`.

## Validations and Restrictions

1. **Fallback Prevention**:
   - `Package.TeacherId` MUST be a valid `TeacherProfile.Id` (non-nullable Guid).
   - `Exam.CreatedByTeacherId` MUST be a valid `TeacherProfile.Id` (non-nullable Guid).
   - `CodeGroup.TeacherId` MUST be a valid `TeacherProfile.Id` (non-nullable Guid).
   - `QuestionBankItem.CreatedByTeacherId` MUST be a valid `TeacherProfile.Id` (non-nullable Guid).

2. **Cross-Teacher Safety (403 Forbidden)**:
   - When updating or deleting a resource (Package, Term, Section, Lesson, Video, Exam, Question, CodeGroup), the backend must check that the requesting teacher's profile ID matches the resource's owner ID.
   - Throws `UnauthorizedAccessException` if they do not match.

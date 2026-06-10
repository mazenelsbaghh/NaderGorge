# Research & Decisions: Remove Programs and Link Packages Directly to Subjects

## Decision 1: Database Migration Strategy
- **Decision**: Write an EF Core database migration containing raw SQL helper steps or custom C# migration logic to copy `SubjectId` and `TargetGrade` from `programs` to `packages` based on `ProgramId` before dropping the `programs` table.
- **Rationale**: Ensures zero data loss or broken associations for existing packages in the production database.
- **Alternatives Considered**: 
  - Dropping the database/tables and re-creating (rejected because this is a production environment with active users).
  - Manual database migration via pgAdmin (rejected because automated EF Core migrations are safer and trackable).

## Decision 2: Exposing Subjects to Teacher
- **Decision**: Update `GetSubjectsQuery` to optionally accept `teacherId`. Add `GET /api/teacher/subjects` (which returns the subjects assigned to the logged-in teacher).
- **Rationale**: Since programs are removed, teachers need a way to list their assigned subjects in the package creation dropdown. Exposing `/api/teacher/subjects` resolved via their teacher profile is clean, secure, and utilizes the existing `TeacherSubjects` association.
- **Alternatives Considered**:
  - Letting teachers request all subjects (rejected as it violates the multi-teacher isolation principles of the platform).

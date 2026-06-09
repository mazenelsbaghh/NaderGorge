# Technical Research: Multi-Teacher Multi-Subject Architecture and Teacher Isolation

## Key Decisions

### DB Entities & FKs
- **Subject**: New table. Will map academic subjects.
- **TeacherProfile**: One-to-one relationship with `User` where `Role` is `Teacher`. Holds bio, image url, commission rate.
- **TeacherSubject**: Many-to-many relationship using a composite primary key `(TeacherId, SubjectId)`.
- **Fk additions**: 
  - `Program` -> `SubjectId` (non-nullable after migration)
  - `Package` -> `TeacherId` (non-nullable after migration)
  - `CodeGroup` -> `TeacherId` (non-nullable after migration)
  - `Exam` -> `CreatedByTeacherId` (non-nullable after migration)
  - `QuestionBankItem` -> `CreatedByTeacherId` & `SubjectId` (non-nullable after migration)
  - `EssaySubmission` -> `GradedByTeacherId` (nullable, indicating graded or ungraded by a teacher)

### Data Migration
- We will construct a migration that:
  1. Inserts a default subject: "History".
  2. Inserts a default teacher profile: linked to the default Admin user, or creates a default Teacher user if one doesn't exist, or links to a default Teacher user. Let's inspect the database seed to see if a Teacher user exists. If it does, we link to that user. Let's make sure.
  3. Updates existing programs to point to the History subject ID.
  4. Updates existing packages, code groups, exams, and question bank items to point to the default teacher's ID.

### Isolation Rules
- To prevent one teacher from accessing another teacher's packages, we will intercept requests in MediatR command and query handlers:
  - If the logged-in user is a Teacher, we automatically append a filter on queries (e.g. `x.TeacherId == currentTeacherId`) or check ownership on commands (e.g. `package.TeacherId == currentTeacherId`).
  - Assistant roles will also be mapped to a specific Teacher (either linked to a Teacher or through an assistant profile table). Let's verify how assistants are linked. Let's look at `EmployeeProfile` and see if there are columns for Teacher link or if they are general employees.

Let's do a quick search in the domain folder for employee profile.

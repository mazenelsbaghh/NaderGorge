# Research: Inline Lesson Exams

## 1. Exam Entity and Question Types
**Context**: The current `QuestionBankItem` entity in `NaderGorge.Domain.Entities.ExamEntities.cs` lacks an explicit "Type" field for distinguishing between Essay and Multiple Choice (MCQ). It relies implicitly on the presence of `Options` to denote an MCQ. The specification mandates explicit support for MCQ vs Essay.
**Decision**: Introduce an explicit enum `QuestionType` (e.g., `MCQ = 0`, `Essay = 1`) on `QuestionBankItem`.
**Rationale**: Explicit typing allows validation rules (e.g., MCQ must have at least one correct option, Essay requires none) without ambiguous data analysis.
**Alternatives considered**: Inferring type based on whether `Options.Count > 0`. This was rejected because a drafted MCQ might temporarily have 0 options, corrupting the type inference.

## 2. Exam Target Attachment
**Context**: The specification requires attaching an exam conceptually to either the `Lesson` or a `LessonVideo`. Currently, `Lesson` has an `ExamId` field (introduced in `LinkLessonExamCommand`), but `LessonVideo` lacks this relationship.
**Decision**: Add an `ExamId` nullable foreign key to `LessonVideo` in `ContentEntities.cs`.
**Rationale**: Enables straightforward navigation from a video to its associated "pop quiz" exam. Maintains referential integrity through EF Core features.
**Alternatives considered**: Using a generic "ExamTarget" mapping table (e.g., TargetType="Video", TargetId=...). This was rejected as it violates strict relational modeling and is over-engineered given only two specific targets exist.

## 3. UI Paradigm for Inline Editor
**Context**: The goal is to quickly create an exam spanning multiple question types without leaving the Lesson Cockpit.
**Decision**: Implement a dynamic, client-side React form (using an array of questions) that submits a single comprehensive payload to create the Exam, QuestionBankItems, Options, and ExamQuestions in one unified transaction.
**Rationale**: Meets the P1 requirement for optimal User Experience. A multi-step server-redirect flow would violate the "Inline" specification constraint.
**Alternatives considered**: Creating elements sequentially directly on the server (Create Exam -> redirect -> Create Question -> etc). Rejected as it clutters the UX and results in potentially orphaned entities if the process is abandoned mid-way.

## 4. Cleaning Up Old Exam Routes
**Context**: "System MUST remove or deprecate standalone exam creation interfaces from other parts of the admin dashboard".
**Decision**: Search for existing `frontend/src/app/admin/exams` or standalone exam pages and replace them with a redirect to the content/lessons hierarchy, or entirely remove them if they haven't been heavily used for other purposes yet.
**Rationale**: Fulfills FR-002 directly.

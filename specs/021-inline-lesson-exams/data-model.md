# Data Model: Inline Lesson Exams

## 1. Enum: QuestionType
To support both MCQ and Essay options without ambiguity in parsing, introduce this enum.
- `MCQ = 0`
- `Essay = 1`

## 2. Updated Entity: QuestionBankItem
Modify `QuestionBankItem` in `ExamEntities.cs` to include a `Type`.
- **`Type`** (`QuestionType` enum)
- **`Options`** (`ICollection<QuestionOption>`) -> Validated only for MCQ type.

## 3. Updated Entity: LessonVideo
Modify `LessonVideo` in `ContentEntities.cs` to map to an `Exam`.
- **`ExamId`** (`Guid?`): Nullable foreign key targeting the standard `Exam` aggregate root.
- **`Exam`** (`Exam` navigation property).

## 4. Entity: Exam (unchanged conceptual, but utilized heavily here)
- `Id` (`Guid`)
- `Title` (`string`)
- `Description` (`string`)
- `PassingScore` (`decimal`)
- `TotalScore` (`decimal`)

## Constraints and Validations
- If `QuestionType` is `MCQ`, there MUST be at least 2 associated `QuestionOption` objects, and exactly equal or greater than 1 `IsCorrect = true` options.
- If `QuestionType` is `Essay`, there SHOULD be 0 `QuestionOption` objects.
- An `Exam` is created via the inline editor and immediately assigned to `LessonVideo.ExamId` or `Lesson.ExamId` depending on the user's target selection.

## Migration Requirements
- Generate EF Core Migration for `AddQuestionTypeToQuestionBankItem`.
- Generate EF Core Migration for `AddExamIdToLessonVideo`.

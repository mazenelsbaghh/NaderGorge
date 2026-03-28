# Quickstart: Inline Lesson Exams 

## Backend Setup

1. Add `QuestionType` to `NaderGorge.Domain.Entities`:
```csharp
public enum QuestionType
{
    MCQ = 0,
    Essay = 1
}
```

2. Add `ExamId` to `LessonVideo`:
```csharp
public Guid? ExamId { get; set; }
public Exam? Exam { get; set; }
```

3. Generate migration:
```bash
dotnet ef migrations add InlineExamsAndQuestions --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API
dotnet ef database update --project backend/src/NaderGorge.Infrastructure --startup-project backend/src/NaderGorge.API
```

4. Implement `CreateInlineExamCommand` and API endpoint `POST /api/admin/exams/inline`.

## Frontend Setup

1. Add explicit routing or modal for creating an inline exam.
2. Build Form State using React `useState` to manage an array of Questions and their respective arrays of Options.
3. Validate client-side that ALL MCQ questions have at least 1 correct answer.
4. Hook up the dynamic list to standard Cockpit interface.

**Dependency:** Requires NaderGorge.API and Postgres to be running locally (`make dev`).

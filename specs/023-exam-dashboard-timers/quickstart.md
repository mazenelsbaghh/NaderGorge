# Quickstart Guide

## 1. Domain Modifications
Navigate to `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`.
- Add `public int? DurationMinutes { get; set; }` to `Exam`.
- Add `public int? DurationSeconds { get; set; }` to `ExamQuestion`.
- Add `public DateTime? StartedAt { get; set; }` and `public bool IsTimeExpired { get; set; }` to `StudentExamAttempt`.

Run EF Core migration:
```bash
cd backend/src/NaderGorge.Infrastructure
dotnet ef migrations add AddExamTimers -s ../NaderGorge.API --context AppDbContext
dotnet ef database update -s ../NaderGorge.API
```

## 2. Admin API
- Modify `CreateInlineExamCommand` to accept durations.
- Create a new read model query `GetExamDashboardQuery` fetching attempt counts, stats, and a list of students.

## 3. Student API
- Create a new endpoint `POST /exams/{examId}/attempts` (or `start`) to register the `StartedAt` timestamp on the server BEFORE returning the questions.
- Update `SubmitExamCommand` to query the given attempt by ID, verify `StartedAt`, calculate if it was expired relative to `DurationMinutes`, set `IsTimeExpired = true`, but still grade the responses to not lose data. (We may reject answers in the future, but marking them "Expired" is better for grading clarity).

## 4. Admin Frontend
- Update `InlineExamEditor.tsx` to include an input for "مدة الامتحان (بالدقائق)" and another input inside the `QuestionEditor` for "مدة السؤال (بالثواني)".
- Create a new nested page `admin/exams/[examId]/dashboard` with the stats and data tables for attempts.

## 5. Student Frontend
- Build a generic un-stoppable countdown timer component referencing the `StartedAt` timestamp returned from the `StartExam` API.
- Create logic to auto-submit when the local calculation reaches zero.

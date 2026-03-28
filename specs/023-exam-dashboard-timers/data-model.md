# Data Model: Exam Dashboard & Timers

This document defines the schema changes and new API contracts required to fulfill the requirements of the 023-exam-dashboard-timers specifications within the Nader Gorge platform.

## Entity Modifications (EF Core Migrations)

### 1. `Exam`

New property in `NaderGorge.Domain/Entities/ExamEntities.cs`:

```csharp
public class Exam : AuditableEntity
{
    // ... existing fields ...
    
    // New Feature fields
    public int? DurationMinutes { get; set; } // Nullable indicating no global time limit
}
```

### 2. `ExamQuestion`

New property in `NaderGorge.Domain/Entities/ExamEntities.cs`:

```csharp
public class ExamQuestion : AuditableEntity
{
    // ... existing fields ...
    
    // New Feature fields
    public int? DurationSeconds { get; set; } // Time allowed per specific question (e.g., 60 seconds)
}
```

### 3. `StudentExamAttempt`

New properties for handling the start time absolute boundary:

```csharp
public class StudentExamAttempt : AuditableEntity
{
    // ... existing fields ...
    
    // New Feature fields
    public DateTime? StartedAt { get; set; } // Set via a new "StartExamCommand" BEFORE submitting.
    public bool IsTimeExpired { get; set; }  // Flag to denote if a submitted exam exceeded its timer boundary
}
```

## API Interfaces & DTO Changes

1. **`CreateInlineExamCommand`**
   - Add `int? DurationMinutes` payload property.
   - For `InlineExamQuestionDto`, add `int? DurationSeconds`.

2. **`GetExamCockpitQuery` / `GetStudentExamQuery`**
   - Must return the configured `DurationMinutes` and per-question `DurationSeconds`.
   - If a student has an unfinished attempt, return `StartedAt`.

3. **`StartExamCommand`**
   - **New Endpoint**: `POST /exams/{examId}/start`
   - Purpose: Instead of just submitting all answers at the end, the system must establish the "StartedAt" timestamp first to start the server-side countdown. Returns `attemptId` and `StartedAt` to the client.

4. **`SubmitExamCommand`**
   - Takes `attemptId` or derives it by picking the active attempt for that user.
   - Calculates expiry: `if (StartedAt.HasValue && Exam.DurationMinutes.HasValue) { var absoluteExpiry = StartedAt.Value.AddMinutes(Exam.DurationMinutes); if (CurrentUtcNow > absoluteExpiry) IsTimeExpired = true; }`

## Dashboard Data Structures

### Admin Panel Table Projection

When viewing an exam's dashboard, the controller will return an `ExamDashboardDto` model:

```csharp
public record ExamDashboardDto(
    Guid ExamId,
    string Title,
    string TargetType, // Lesson or Video
    int QuestionCount,
    decimal TotalScore,
    decimal PassingScore,
    int? DurationMinutes,
    List<StudentExamResultSummaryDto> Attempts
);

public record StudentExamResultSummaryDto(
    Guid StudentId,
    string StudentName,
    DateTime SubmittedAt,
    decimal ScoreAchieved,
    string Evaluation,
    bool IsPassed,
    bool IsTimeExpired
);
```

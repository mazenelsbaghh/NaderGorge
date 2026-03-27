# Data Model: Phase 2 — Structured Learning and Academic Operations

## Core Entities Added / Modified

> **Note**: These schemas represent the core domain models added to Entity Framework Core representing physical storage in PostgreSQL.

### 1. Homework & Submissions

**`Homework`**
- `Id`: Guid (PK)
- `LessonId`: Guid (FK) (Every homework belongs to one lesson)
- `Title`: string
- `Description`: string?
- `IsMandatory`: bool (Defaults to true, prevents progression if skipped)
- `PassingScoreThreshold`: decimal?
- `CreatedAt`/`UpdatedAt`: DateTimes

**`HomeworkQuestion`**
- `Id`: Guid (PK)
- `HomeworkId`: Guid (FK)
- `Order`: int
- `QuestionType`: Enum (`MCQ`, `Essay`)
- `BodyText`: string
- `PossibleAnswers`: string[]? // For MCQs
- `CorrectAnswerKey`: string? // For MCQs
- `PointsActive`: int 

**`HomeworkSubmission`**
- `Id`: Guid (PK)
- `HomeworkId`: Guid (FK)
- `StudentId`: Guid (FK)
- `StartedAt`: DateTime
- `SubmittedAt`: DateTime?
- `GradedAt`: DateTime?
- `Status`: Enum (`InProgress`, `PendingReview`, `Graded`, `Missed`)
- `AssistantReviewerId`: Guid? (FK to Admin assigned)
- `AssistantNotes`: string?
- `OverallScore`: decimal

**`HomeworkAnswer`**
- `Id`: Guid (PK)
- `HomeworkSubmissionId`: Guid (FK)
- `QuestionId`: Guid (FK)
- `ProvidedAnswer`: string // text block for essay or selected key for MCQ
- `ScoreReceived`: int?

---

### 2. Gamification

**`StudentGamification`**
- `StudentId`: Guid (PK & FK to AspNetUsers)
- `TotalPoints`: int (Redis sorted set source of truth for ranks)
- `CurrentStreakCount`: int
- `LongestStreakCount`: int
- `LastTaskCompletedAt`: DateTime?
- `LevelName`: string

**`GamificationActionLog`**
- `Id`: Guid (PK)
- `StudentId`: Guid (FK)
- `EventType`: Enum (`HomeworkSubmittedOnTime`, `PerfectExam`, `StreakMaintained`)
- `PointsAwarded`: int
- `CreatedAt`: DateTime

**`StudentBadge`**
- `Id`: Guid (PK)
- `StudentId`: Guid (FK)
- `BadgeName`: string (e.g. "Early Bird", "Perfect Score")
- `UnlockedAt`: DateTime

---

### 3. Commitment Engine & Warnings

**`StudentStatusTracker`**
- `StudentId`: Guid (PK & FK)
- `CurrentStatus`: Enum (`Committed`, `Average`, `AtRisk`)
- `ConsecutiveMissedHomeworks`: int
- `ConsecutiveFailedExams`: int
- `LastActiveAt`: DateTime
- `LastEvaluatedAt`: DateTime

**`WarningEvent`**
- `Id`: Guid (PK)
- `StudentId`: Guid (FK)
- `Severity`: Enum (`Low`, `Medium`, `Critical`)
- `TriggerReason`: string ("Missed 3 assignments", "Inactive for 10 days")
- `IsResolved`: bool
- `ResolvedByAssistantId`: Guid? (FK)
- `ResolutionNotes`: string?
- `CreatedAt`: DateTime

---

### 4. Notifications

**`NotificationEvent`**
- `Id`: Guid (PK)
- `UserId`: Guid (FK to recipient: Student, Parent or Assistant)
- `ChannelType`: Enum (`InApp`, `SMS`)
- `Title`: string
- `Body`: string
- `Status`: Enum (`Pending`, `Sent`, `Failed`)
- `ReadAt`: DateTime?
- `CreatedAt`: DateTime

---

### 5. Assistant Workflow

**`AssistantTaskQueue`**
- (Note: Built dynamically, potentially stored in DB or Redis via BullMQ, but for tracking history a standard DB table is viable)
- `Id`: Guid (PK)
- `TaskType`: Enum (`GradeEssay`, `FollowUpAtRisk`, `ResolvePaymentIssue`)
- `ReferenceEntityId`: Guid (FK polymorphic - e.g., HomeworkSubmissionId)
- `StudentId`: Guid
- `AssignedAssistantId`: Guid?
- `Status`: Enum (`Open`, `InReview`, `Done`)
- `CreatedAt`: DateTime
- `CompletedAt`: DateTime?

# Data Model: Phase 2 Data Integrity Fixes

## 1. Lesson Watch Event

### Purpose

Represents the persisted watch progress for one student on one lesson video and determines when additional counted views are earned.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | UUID | Yes | Existing watch-event identifier |
| `UserId` | UUID | Yes | Student owner |
| `LessonVideoId` | UUID | Yes | Target lesson video |
| `TimeWatchedInSeconds` | Integer | Yes | Cumulative tracked watch time |
| `WatchCount` | Integer | Yes | Number of counted watches earned so far |
| `IsLocked` | Boolean | Yes | Indicates whether the video has exceeded its allowed counted watches |
| `CreatedAt` | DateTime | Yes | Existing audit timestamp |
| `UpdatedAt` | DateTime | Yes | Existing audit timestamp |

### Validation Rules

- New records start with `WatchCount = 0`.
- Counted-watch evaluation requires a valid lesson duration.
- The same tracked request must not increment `WatchCount` more than once.
- Threshold calculations must be identical across all watch-tracking flows.
- `IsLocked` may become true only after counted watches exceed the configured lesson limit.

### Relationships

- Many lesson watch events belong to one student.
- Many lesson watch events belong to one lesson video.
- Threshold behavior depends on one platform setting for watch percentage.

### State Transitions

| Current State | Action | Next State |
|---------------|--------|------------|
| `WatchCount = 0`, unlocked | Student watches below threshold | `WatchCount = 0`, unlocked |
| `WatchCount = 0`, unlocked | Student reaches first threshold | `WatchCount = 1`, unlocked or locked depending on video limit |
| `WatchCount = N`, unlocked | Student reaches next threshold | `WatchCount = N+1`, unlocked or locked depending on video limit |
| Any unlocked state | Counted watches exceed allowed maximum | Locked |

## 2. Student Profile Theme Preference

### Purpose

Represents persisted student-specific theme preferences, including the currently active display mode and selected palette IDs.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `UserId` | UUID | Yes | One-to-one link to the student user |
| `LightThemePaletteId` | String | No | Selected light palette identifier |
| `DarkThemePaletteId` | String | No | Selected dark palette identifier |
| `CurrentMode` | String | Yes | Current active mode, limited to `light` or `dark` |

### Validation Rules

- `CurrentMode` must be either `light` or `dark`.
- If a profile does not exist, the update flow must create one before saving the new values.
- Existing valid palette selections must be preserved unless the student explicitly changes them.

### Relationships

- One student profile belongs to one user.
- Theme-preference DTOs are derived from the profile plus the fixed palette catalog.

## 3. Essay Submission

### Purpose

Represents a student's answer to one essay question, including typed content, optional audio evidence, and grading metadata already used elsewhere in the exam workflow.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | UUID | Yes | Existing essay submission identifier |
| `StudentId` | UUID | Yes | Student owner |
| `QuestionId` | UUID | Yes | Essay question being answered |
| `StudentExamAttemptId` | UUID | Yes | Parent attempt |
| `AnswerText` | Text | Yes | Existing written response |
| `AudioUrl` | Text | No | Optional uploaded audio reference tied to the answer |
| `Status` | Enum | Yes | Existing grading lifecycle state |
| `AIScore` / `TeacherScore` | Decimal | No | Existing grading outputs remain unchanged by this feature |

### Validation Rules

- `AudioUrl` is optional and must not invalidate typed-only submissions.
- If present, `AudioUrl` must persist with the same essay answer across retrieval and grading flows.
- Adding `AudioUrl` must not alter essay grading-state transitions.

### Relationships

- Many essay submissions belong to one exam attempt.
- Each essay submission belongs to one student and one question.

## 4. Exam Result Review Item

### Purpose

Represents one question's review data in a student-visible exam result, including selected answer details and any correction text that becomes available after completion.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `ExamQuestionId` | UUID | Yes | Question reference within the exam |
| `SelectedText` | Text | No | Student-visible submitted answer representation |
| `IsAnswered` | Boolean | Yes | Whether the student submitted an answer |
| `IsCorrect` | Boolean | Yes | Result correctness |
| `PointsAwarded` | Decimal | Yes | Awarded points |
| `CorrectAnswerText` | Text | No | Existing review field shown only when reveal rules permit |
| `WrittenCorrection` | Text | No | Student-facing written correction shown only after exam completion |

### Validation Rules

- `WrittenCorrection` must not be returned while the exam is still in progress.
- Completed-result responses must include `WrittenCorrection` for applicable questions when it exists.
- Review visibility rules must remain server-side.

## 5. Extra Watch Request

### Purpose

Represents a student's request for additional viewing allowance on a lesson video, including the latest decision state and any rejection explanation meant for the student.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | UUID | Yes | Existing request identifier |
| `UserId` | UUID | Yes | Student owner |
| `LessonVideoId` | UUID | Yes | Related lesson video |
| `Status` | Enum | Yes | `Pending`, `Approved`, or `Rejected` |
| `ResolvedAt` | DateTime | No | Existing resolution timestamp |
| `RejectionReason` | Text | No | Optional explanation set only when rejected |

### Validation Rules

- `RejectionReason` may be stored only for rejected requests.
- Student-facing status lookups return `RejectionReason` only when the latest request is rejected.
- Pending and approved requests must not expose stale rejection text.

### Relationships

- Many extra watch requests belong to one student.
- Many extra watch requests belong to one lesson video.
- Student-facing status responses are derived from the latest request by creation time.

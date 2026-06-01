# Data Model: Student Birthday Greetings & Video Exam Progression

This document specifies the entities and fields involved in this feature. All entities are pre-existing; no database migrations or new schemas are required, as we are reusing the existing domain structure.

## Entities Involved

### 1. StudentProfile (Table: `student_profiles`)
Used to determine student birthdays.
- `UserId` (Guid, FK to `users.Id`): Matches the user details.
- `DateOfBirth` (DateTime): The student's birth date.

### 2. User (Table: `users`)
Used to retrieve student personal information.
- `Id` (Guid, PK): Unique identifier.
- `FullName` (string): Student name for personalized greetings.
- `PhoneNumber` (string): Student phone number for WhatsApp greetings.
- `IsActive` (bool): Checked to only congratulate active accounts.

### 3. NotificationEvent (Table: `notification_events`)
Used to record the birthday greeting sent in-app.
- `Id` (Guid, PK): Unique identifier.
- `UserId` (Guid, FK to `users.Id`): Target student.
- `ChannelType` (Enum): Set to `NotificationChannelType.InApp` (value `0`).
- `Title` (string): e.g., "عيد ميلاد سعيد! 🎉"
- `Body` (string): Warm personalized birthday wish.
- `Status` (Enum): Set to `NotificationStatus.Sent` (value `1`).
- `CreatedAt` (DateTime): Date of record creation (UTC).

### 4. LessonVideo (Table: `lesson_videos`)
Used for sorting and determining pop-quiz locks.
- `Id` (Guid, PK): Unique identifier.
- `LessonId` (Guid, FK to `lessons.Id`): Associated lesson.
- `Order` (int): Sort key for video sequence.
- `ExamId` (Guid?, nullable FK to `exams.Id`): Associated assessment.

### 5. StudentExamAttempt (Table: `student_exam_attempts`)
Used to check if a student completed a video's exam.
- `Id` (Guid, PK): Unique identifier.
- `UserId` (Guid, FK to `users.Id`): Student who attempted the exam.
- `ExamId` (Guid, FK to `exams.Id`): Exam code.
- `IsPassed` (bool): Truth flag indicating if the student achieved the passing score.

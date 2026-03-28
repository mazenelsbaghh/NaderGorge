# Feature Specification: Exam Dashboard & Timers

**Feature Branch**: `023-exam-dashboard-timers`  
**Created**: 2026-03-28  
**Status**: Draft  
**Input**: User description: "خليه جدول يظهر ف الامتحان و يظهر فيه عدد الاسئله و كل حاجه عندر و يبقي ليه بروفيل واعرف الطلاب اللي دخلتوا و دراجتهم و كل ده و عايز اخلي لكل سوال تايمر و للامتحان تايمر برضو و عايز التايمر لو طلعت او اي خحاجه يتعد عادي"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Exam Dashboard & Student Results (Priority: P1)

As an administrator, I want to access a dedicated profile/dashboard for each exam that shows a summary of the exam (number of questions, total points) and a table of all students who took it along with their grades, so I can easily track participation and performance.

**Why this priority**: Essential for administrators to monitor student progress and ensure the exams are fulfilling their educational purpose before enforcing strict time rules.

**Independent Test**: Can be fully tested by navigating to an exam's profile page as an admin and viewing the summary cards (questions count, etc.) and the list of student submissions with proper grades.

**Acceptance Scenarios**:

1. **Given** an exam with published questions, **When** the admin views the exam profile, **Then** they see a dashboard displaying the total number of questions, total score, and passing score.
2. **Given** an exam that students have completed, **When** the admin views the exam profile, **Then** they see a table listing student names, their achieved scores, evaluations, and submission times.

---

### User Story 2 - Admin Defines Time Limits (Priority: P2)

As an administrator creating or editing an exam, I want to set a time limit for the entire exam, and optimally an individual time limit for each question, so I can control the pace of the assessment.

**Why this priority**: Critical to satisfying the requirement of having time-constrained exams, but depends on basic exam structures being in place.

**Independent Test**: Can be fully tested by opening the exam editor, setting the exam timer and question timers, and verifying these constraints are saved and displayed correctly.

**Acceptance Scenarios**:

1. **Given** the exam settings view, **When** the admin creates an exam, **Then** they can specify an optional "Exam Duration" in minutes.
2. **Given** the question editor view, **When** the admin adds a question, **Then** they can specify an optional "Question Duration" in seconds/minutes.

---

### User Story 3 - Resilient Student Exam Timers (Priority: P3)

As a student taking an exam, I want to see a countdown timer corresponding to the limits set by the admin. The timer must be resilient and synced with the server so that if I refresh the page, lose connection, or navigate away, the timer continues ticking "in the background" without resetting.

**Why this priority**: Implements the core complex technical requirement of absolute elapsed time calculation.

**Independent Test**: Can be fully tested by a student starting a timed exam, closing the browser window, waiting 2 minutes, reopening the browser, and seeing the timer has decreased by exactly 2 minutes.

**Acceptance Scenarios**:

1. **Given** a timed exam, **When** a student starts the exam, **Then** a server-side timestamp is recorded indicating the start time and the clock begins ticking.
2. **Given** an ongoing exam, **When** the student refreshes the page, **Then** the remaining time is calculated based on `(Duration) - (CurrentTime - StartTime)`.
3. **Given** the timer expires, **When** the student tries to submit an answer, **Then** the system forcibly submits the exam and prevents further changes.

---

### Edge Cases

- What happens when a student's local computer clock is out of sync? (Timers must rely on server time or elapsed offsets to avoid cheating).
- How does the system handle an exam submission if the timer expired while the student was disconnected from the internet? (The backend should reject or auto-score answers submitted beyond the absolute `StartTime + Duration` threshold).
- What happens if an admin changes the time limit while students are actively taking the exam? (Should only apply to new attempts).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide an Admin Exam Dashboard view for each exam showing metadata (question count, total score, pass score).
- **FR-002**: System MUST display a paginated/sortable table of student attempts (Student Name, Score, Pass/Fail, Evaluation, Date) on the Exam Dashboard.
- **FR-003**: System MUST allow admins to set a `DurationMinutes` at the Exam level.
- **FR-004**: System MUST allow admins to set a `DurationSeconds` at the Question level.
- **FR-005**: System MUST record a `StartedAt` server timestamp for `StudentExamAttempt` when a student begins an exam.
- **FR-006**: System MUST calculate remaining time strictly as `ExpiryTime - CurrentServerTime` to prevent local tampering or refresh-resets.
- **FR-007**: System MUST automatically submit and lock the exam when the overall exam timer expires.
- **FR-008**: System MUST lock individual questions and auto-advance when the per-question timer expires.

### Key Entities 

- **Exam**: Needs new property `DurationMinutes` (int, nullable).
- **ExamQuestion**: Needs new property `DurationSeconds` (int, nullable).
- **StudentExamAttempt**: Needs new property `StartedAt` (DateTime) to track the absolute start time, and `IsTimeExpired` (bool).
- **StudentAnswer**: Might need `AnsweredAt` (DateTime) to enforce per-question time limits on the backend.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin can view exam statistics and student scores in a single unified dashboard without needing to query the database manually.
- **SC-002**: Student timers cannot be artificially extended by refreshing the browser tab, clearing cache, or altering local system clocks.
- **SC-003**: 100% of exams submitted past their absolute deadline are automatically handled by the server (either marked expired or auto-submitted based on what was saved).

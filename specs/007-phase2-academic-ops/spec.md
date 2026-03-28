# Feature Specification: Phase 2 — Structured Learning and Academic Operations

**Feature Branch**: `007-phase2-academic-ops`  
**Created**: 2026-03-26  
**Status**: Draft  
**Input**: User description: "Phase 2 — Structured Learning and Academic Operations"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Homework Submission and Review Flow (Priority: P1)

Students need to complete required homework assignments (MCQ and textual essays) to progress to the next lesson, and assistants need to grade these submissions.

**Why this priority**: Homework is the core academic addition in Phase 2, essential for enforcing the learning progression rules and assessing student understanding practically before exams.

**Independent Test**: Can be fully tested by a student completing an assignment and an assistant reviewing and grading the submission.

**Acceptance Scenarios**:

1. **Given** a student is watching a lesson with attached mandatory homework, **When** they finish the lesson, **Then** they cannot proceed to the next module until the homework is submitted.
2. **Given** an assigned essay question, **When** the student submits text, **Then** the submission is marked as "Pending Review" and routed to the Assistant Dashboard.
3. **Given** a pending homework submission, **When** an assistant grades it and leaves a comment, **Then** the student's status updates and they receive an in-platform notification.
4. **Given** a deadline-based homework, **When** the due date passes without submission, **Then** the assignment is marked "Missed" and a warning is triggered.

---

### User Story 2 - Automated Commitment Engine & Warnings (Priority: P2)

The system automatically classifies students (Committed, Average, At Risk) and dispatches warnings based on their academic behavior (inactivity, missed tasks, low scores).

**Why this priority**: Automating follow-up reduces manual work for assistants and ensures early intervention for failing students.

**Independent Test**: Can be fully tested by simulating a student missing three assignments in a row and verifying that their status drops to "At Risk" and a warning alert is produced.

**Acceptance Scenarios**:

1. **Given** a student misses two consecutive homework assignments, **When** the nightly evaluation job runs, **Then** their status changes to "Average" (or "At Risk").
2. **Given** an "At Risk" student, **When** a warning event is triggered, **Then** an in-platform notification is sent to the student and flagged in the Assistant Dashboard.
3. **Given** a critical event (e.g., failing a major unit exam 3 times), **When** the event happens, **Then** an SMS is queued to be sent to the parent's phone number.

---

### User Story 3 - Assistant Operational Dashboard (Priority: P2)

Assistants need a dedicated panel to handle grading, monitor at-risk students, and resolve pending alerts efficiently.

**Why this priority**: Enables Nader George's team to scale operations, dividing work among homework reviewers, academic assistants, and follow-up support.

**Independent Test**: Can be fully tested by logging in as an Assistant, viewing a queue of pending tasks, filtering by assigned students, and resolving an alert.

**Acceptance Scenarios**:

1. **Given** an assistant logs in, **When** they view their dashboard, **Then** they see organized queues for Pending Homework, At-Risk Students, and General Alerts.
2. **Given** role-based access, **When** a "Homework Reviewer" logs in, **Then** they can grade essay questions but cannot modify student account permissions.
3. **Given** an assistant resolves an "At Risk" alert, **When** they submit a resolution note, **Then** the alert is cleared from the queue and logged in the student's history.

---

### User Story 4 - Parent Reporting Layer (Priority: P3)

Parents need a readable summary format to track the student's academic progress, attendance (watch activity), and behavioral warnings.

**Why this priority**: Keeps parents engaged in the student's success cycle, which is a major selling point for educational platforms.

**Independent Test**: Can be fully tested by generating a parent report link or email that correctly calculates the student's attendance, average grade, and active warnings.

**Acceptance Scenarios**:

1. **Given** a parent requests a status update, **When** they access the parent report view, **Then** they see clear metrics on videos watched, homework completed, and exam averages.
2. **Given** a student receives a critical behavioral warning, **When** the warning is generated, **Then** the parent report clearly flags this issue.

---

### User Story 5 - Gamification Engine (Priority: P4)

Students earn points, unlock badges, and climb leaderboards upon successfully completing academic tasks on time.

**Why this priority**: Essential to maintain student motivation and platform stickiness, transforming studying from a chore into a rewarding loop.

**Independent Test**: Can be fully tested by submitting a perfect exam and verifying that the student's point balance increases and a "Perfect Score" badge is awarded.

**Acceptance Scenarios**:

1. **Given** a student completes a lesson and its homework a day before the deadline, **When** graded, **Then** they receive standard experience points plus an "early bird" multiplier.
2. **Given** a student finishes 5 consecutive assignments, **When** the last is graded, **Then** they unlock a "Streak" badge.
3. **Given** the leaderboard page, **When** students view it, **Then** they see their rank based on points alongside top-performing peers.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support creating Homework entities tied to specific Lessons, consisting of MCQ, Essay, or Mixed questions.
- **FR-002**: System MUST enforce progression rules where completion of homework can be a prerequisite to unlock the next Lesson.
- **FR-003**: System MUST route essay submissions to an Assistant review queue for manual grading and feedback.
- **FR-004**: System MUST evaluate student behavior nightly based on defined metrics (watch time, missed tasks, grades) and assign a status (Committed, Average, At Risk).
- **FR-005**: System MUST trigger notification events (in-app and targeted SMS via BullMQ background jobs) upon critical academic warnings.
- **FR-006**: System MUST provide Role-Based Access Control (RBAC) specifically tailored for Assistants (Academic, Follow-up, Reviewer, Support).
- **FR-007**: System MUST provide an Assistant Dashboard exposing alerts, pending tasks, and assigned student rosters.
- **FR-008**: System MUST generate read-only Parent Reports aggregating student progress, exam results, and warnings.
- **FR-009**: System MUST support awarding Points and Badges based on academic triggers (e.g., passing exams, timely homework).
- **FR-010**: System MUST track student streaks and display a competitive global or package-specific Leaderboard.
- **FR-011**: System MUST generate Admin analytical reports (Hardest Lessons, Average Grade by class, Declining Students).

### Key Entities

- **Homework**: Represents an assignment attached to a lesson. Includes due date logic, question lists, and passing criteria.
- **HomeworkSubmission**: A student's answers to a Homework instance. Contains auto-graded MCQ scores, pending Essay answers, and final Assistant review comments.
- **StudentStatus / CommitmentEngine**: A recalculated state of a student's health (Committed, Average, At Risk) based on EngagementMetrics.
- **WarningEvent**: A generated alert when a student hits negative thresholds (e.g., missed 3 homeworks in a row).
- **GamificationProfile**: Holds a student's total Points, unlocked Badges, active Streaks, and current Leaderboard Rank.
- **AssistantTaskQueue**: Work items dynamically created for Assistants based on system triggers (new essays, At Risk warnings).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: **Assistant Efficiency**: Assistants can grade an essay and resolve an alert in under 2 minutes per student on average using the Dashboard.
- **SC-002**: **Automation Scaling**: The nightly Commitment Engine evaluates 100% of active students and produces correct statuses under 5 minutes without affecting peak API performance.
- **SC-003**: **Student Accountability**: The progression enforcement successfully blocks 100% of students attempt to skip mandatory homework.
- **SC-004**: **System Communication**: 99% of triggered critical warnings successfully emit an SMS job to the BullMQ queue within 10 seconds of the event.
- **SC-005**: **Engagement Growth**: Activation of the Gamification layer increases daily average student session time or active completion rates by at least 15% within the first month.

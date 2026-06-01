# Feature Specification: Exam UI Refinements and Locked Reasons

**Feature Branch**: `030-exam-ui-refinements`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "عايز في ui ان يبان انا مقفول الحصه لي علاشن محلتش اي بالظبط اني واجب او انهي امتحان. واعملي ترم بقي و كل حاجه و امحان بس يبقي السعر بتاعت السنه ٠ علشن اجرب بقي فيه. وعايز ف الماتحان يبقي دواير و عليها رقم السوال السوال وضيف زرا تخطي و اعرف ارجلوا و تبقي الدواير شبه التشيك بوكس ف الش؛ل و الانيمشن و اللي التالي يبقي ازرق و اللي اعملوا تخطي يبقي استروك و اعمل count down"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Clear Locked Lesson Reasons (Priority: P1)

As a student, I want to see exactly why a lesson is locked (e.g., "Complete Homework X" or "Complete Exam Y"), so I know what action to take to unlock it.

**Why this priority**: It directly reduces student frustration and support requests by providing clear, actionable feedback when content is gated.

**Independent Test**: Can be tested by navigating to a locked lesson and verifying the exact name of the blocking homework or exam is displayed.

**Acceptance Scenarios**:

1. **Given** a lesson is locked due to an unpassed homework, **When** the student views the locked lesson card, **Then** the UI clearly states the specific name of the homework that needs to be passed.
2. **Given** a lesson is locked due to an unpassed exam, **When** the student views the locked lesson card, **Then** the UI clearly states the specific name of the exam that needs to be passed.

---

### User Story 2 - Enhanced Exam Navigation UI (Priority: P1)

As a student taking an exam, I want an intuitive navigation bar with circles containing question numbers, showing which questions I've answered, which I'm currently on, and which I've skipped, so I can easily manage my exam progress.

**Why this priority**: Improves the core assessment experience, giving students better control over their exam strategy.

**Independent Test**: Can be tested by starting an exam and interacting with the question navigation indicators.

**Acceptance Scenarios**:

1. **Given** the exam view, **When** observing the progress indicator, **Then** each question is represented by a circle containing its number.
2. **Given** the progress indicator, **When** I answer a question, **Then** its corresponding circle assumes a filled, primary color (like a checkbox) with an animation.
3. **Given** the progress indicator, **When** I skip a question, **Then** its corresponding circle assumes a stroked (outlined) style to indicate it was visited but left unanswered.
4. **Given** a skipped question, **When** I click its circle in the navigation, **Then** I am navigated back to that question to answer it.

---

### User Story 3 - Exam Countdown Timer (Priority: P2)

As a student taking a timed exam, I want to see a clear, dynamic countdown timer, so I can manage my time effectively across all questions.

**Why this priority**: Essential for timed assessments to keep the user aware of remaining time.

**Independent Test**: Can be tested by starting a timed exam and verifying the countdown UI components update every second accurately.

**Acceptance Scenarios**:

1. **Given** a timed exam is active, **When** viewing the timer, **Then** a countdown is displayed using the specific UI component style (DaisyUI-like flipping/animated numbers or robust CSS transition).
2. **Given** the countdown runs out, **When** it hits 0, **Then** the exam is automatically submitted.

---

### User Story 4 - Free Test Content Seeding (Priority: P3)

As an administrator/tester, I want a complete educational structure (Term, Package, Lesson, Exam) priced at 0, so I can fully test the student enrollment and progression flows without payment barriers.

**Why this priority**: Facilitates frictionless end-to-end testing of the platform's core features.

**Independent Test**: Can be tested by a new test student enrolling in the free package and accessing its contents.

**Acceptance Scenarios**:

1. **Given** the system database, **When** the seeding script/endpoint is executed, **Then** a full hierarchy (Term -> Section -> Package -> Lesson -> Exam/Homework) is created with a price of 0.

### Edge Cases

- What happens when a user skips the last question? The submit button should still be available or prompt them to review skipped questions.
- What happens if the referenced blocking homework/exam is somehow deleted? The locked reason should gracefully fallback to a generic message.
- How does the UI handle very long homework/exam titles in the locked state? Text should wrap or truncate gracefully.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST return the specific ID and Title of the blocking assessment (Homework or Exam) when denying access to a locked lesson.
- **FR-002**: Frontend UI MUST display the specific assessment title in the locked lesson view.
- **FR-003**: Exam navigation MUST utilize circular indicators showing the question index.
- **FR-004**: Exam navigation MUST visually differentiate between "Answered" (Primary fill), "Current" (Primary outline/focus), "Skipped" (Stroke/outline), and "Unvisited" (Subtle background) states.
- **FR-005**: Users MUST be able to click a "Skip" button on a question to move to the next without answering, marking it as skipped.
- **FR-006**: Users MUST be able to navigate back to any specific question by clicking its indicator in the stepper.
- **FR-007**: Exam view MUST display a lively countdown timer using CSS variables (`--value`) for animated transitions.
- **FR-008**: System MUST provide a mechanism (e.g., automated script or API endpoint) to generate a complete free test course structure (Price = 0).

### Key Entities

- **LessonDetailDto**: Must be updated to include `BlockingAssessmentName` to provide context to the frontend.
- **StudentExamAttempt**: Tracks which questions have been answered vs skipped.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of locked lessons successfully identify the exact prerequisite needed instead of a generic message.
- **SC-002**: Users can successfully answer, skip, and return to questions using the new exam navigation UI.
- **SC-003**: A complete end-to-end test can be run by a newly registered user on the zero-priced test content without encountering payment walls.
- **SC-004**: The exam countdown accurately reflects remaining duration and automatically triggers submission when expired.

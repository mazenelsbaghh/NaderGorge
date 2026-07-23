# Feature Specification: Parent Tracking Accuracy

**Feature Branch**: `[149-parent-tracking-accuracy]`  
**Created**: 2026-06-25  
**Status**: Draft  
**Input**: User description: "Fix parent app tracking so schedules become watch logs; parent selects a teacher and sees watch history for all purchased lessons for that teacher; homework must match exams with teacher filtering, grades, mistakes, and corrections; balance and balance details must be accurate."

## Clarifications

### Session 2026-06-25

- Q: ما مصدر المدرس المعتمد عند فلترة المشاهدات والامتحانات والواجبات؟ → A: مدرس الحصة أو الباقة التي اشتراها الطالب أو يملك صلاحية نشطة عليها.
- Q: هل تظهر الامتحانات والواجبات غير المحلولة؟ → A: تظهر العناصر المرتبطة بدروس مشتراة مع حالة واضحة، ولا تظهر أخطاء أو تصحيح إلا بعد محاولة أو تسليم.
- Q: كيف تُحسب مشاهدات درس يحتوي على أكثر من فيديو؟ → A: تُجمع كل أحداث المشاهدة لفيديوهات الدرس على مستوى الدرس.
- Q: ما مصدر الرصيد وتفاصيله؟ → A: الرصيد الحالي وسجل المعاملات الرسمي، مرتب من الأحدث للأقدم.
- Q: ما سطح العميل المشمول؟ → A: تطبيق ولي الأمر Android مع API الباك إند الداعم له.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Teacher-Based Watch Logs (Priority: P1)

As a parent, I want to choose a teacher and see the student's watch history for lessons purchased from that teacher, so I can understand what the student actually watched.

**Why this priority**: This replaces the current schedules/attendance surface and fixes the primary incorrect-teacher behavior.

**Independent Test**: Can be fully tested by granting a student access to lessons from two teachers, recording watch events, selecting each teacher in the parent app, and verifying only that teacher's purchased lessons and watch totals appear.

**Acceptance Scenarios**:

1. **Given** a student has purchased lessons from Teacher A and Teacher B, **When** the parent opens watch logs and selects Teacher A, **Then** only Teacher A's purchased lessons are shown.
2. **Given** a purchased lesson has no watch activity, **When** the parent selects that lesson's teacher, **Then** the lesson still appears with zero or empty watch metrics instead of disappearing.
3. **Given** a lesson belongs to a teacher whose package the student has not purchased, **When** the parent views watch logs, **Then** that lesson and teacher are not shown.

---

### User Story 2 - Review Exams by Purchased-Lesson Teacher (Priority: P1)

As a parent, I want to choose a teacher and see the student's exams tied to purchased lessons for that teacher, then open each exam to review grades, mistakes, and corrections.

**Why this priority**: Exam review is a core academic follow-up workflow and must use the same teacher source as watch logs.

**Independent Test**: Can be tested by attaching exams to lessons from different teachers, submitting attempts, and confirming teacher selection controls visibility and detail review.

**Acceptance Scenarios**:

1. **Given** a student has an exam linked to a purchased lesson under Teacher A, **When** the parent selects Teacher A in exams, **Then** the exam appears with a clear solved, unsolved, grading, or graded status.
2. **Given** the parent opens a submitted exam detail, **When** the attempt has incorrect answers, **Then** each mistake shows the question, the student's answer, the correct answer, and the correction/explanation when available.
3. **Given** an exam was created by Teacher B but is attached to a purchased lesson under Teacher A, **When** the parent filters by teacher, **Then** the exam appears under Teacher A.

---

### User Story 3 - Review Homework Like Exams (Priority: P1)

As a parent, I want homework to behave like exams: choose a teacher, see homework for that teacher's purchased lessons, and open details to review grades, mistakes, and corrections.

**Why this priority**: The requested homework behavior must match the exam experience and use the same teacher filtering rule.

**Independent Test**: Can be tested by creating homework for lessons from two teachers, submitting one homework, leaving another unsubmitted, and verifying both list and details by teacher.

**Acceptance Scenarios**:

1. **Given** homework belongs to a purchased lesson under Teacher A, **When** the parent selects Teacher A in homework, **Then** the homework appears regardless of submitted, unsubmitted, grading, or graded state.
2. **Given** a submitted homework contains wrong answers, **When** the parent opens the homework details, **Then** mistakes and corrections are displayed in the same review pattern as exams.
3. **Given** no homework exists for the selected teacher, **When** the parent opens homework, **Then** a clear empty state appears and the app does not crash.

---

### User Story 4 - View Correct Balance and Balance Details (Priority: P1)

As a parent, I want to see the student's current balance and transaction details accurately, so I can understand remaining credit and recent financial movements.

**Why this priority**: Incorrect balance undermines trust and blocks payment/support workflows.

**Independent Test**: Can be tested by creating a student balance with credit and debit transactions, opening the parent balance screen, and comparing the displayed balance and transaction list with the authoritative account record.

**Acceptance Scenarios**:

1. **Given** a student has a current balance and transactions, **When** the parent opens the balance screen, **Then** the displayed balance matches the authoritative balance and official transactions are shown from newest to oldest.
2. **Given** a student has no transactions, **When** the parent opens the balance screen, **Then** the current balance is still shown with an empty transaction state.
3. **Given** a transaction is a debit, credit, refund, or purchase, **When** it appears in the details list, **Then** the direction, amount, date, balance after transaction when available, and description are understandable.

---

### User Story 5 - No-Crash Empty, Missing, and Failure States (Priority: P2)

As a parent, I want the app to stay stable when data is empty, partial, or temporarily unavailable, so tracking remains usable.

**Why this priority**: The current workflow has reported crashes when opening watch logs, exams, or homework.

**Independent Test**: Can be tested by calling the parent tracking API with missing optional fields, empty lists, and API failure responses, then opening each screen.

**Acceptance Scenarios**:

1. **Given** optional tracking fields are absent or null, **When** the parent opens any tracking screen, **Then** the app uses safe defaults and does not crash.
2. **Given** the API request fails, **When** the parent opens a tracking screen, **Then** an error state is shown with the previous navigation intact.
3. **Given** a teacher has no data in the selected section, **When** that teacher is selected, **Then** the app shows a section-specific empty state.

### Edge Cases

- A student has direct lesson or video access instead of a full package; the teacher must still resolve from the lesson's package hierarchy.
- A purchased package contains multiple lessons and some lessons have no videos or no watch events.
- An exam or homework was created by one teacher but attached to a lesson owned by another teacher; the owner teacher of the purchased lesson/package controls filtering.
- A student's access grant is inactive, expired, cancelled, or disabled at request time; related content must not appear.
- Balance exists without transactions, or transactions exist with sparse descriptions.
- Backend returns older payloads without newly added optional fields; the Android app must avoid crashes.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Parent Android app; link or open an existing linked student; navigate to المشاهدات; select each teacher; verify only purchased lessons for that teacher appear with correct watch counts and last watch data.
- **Manual QA Role/Flow 2**: Parent Android app; navigate to الامتحانات and الواجبات; select a teacher; open submitted items; verify score, mistakes, student answers, correct answers, and explanations/corrections.
- **Manual QA Role/Flow 3**: Parent Android app; navigate to الرصيد; verify current balance and transaction details against an admin/backend record for the same student.
- **Manual QA Negative Check**: A teacher with no active purchased lessons for the student must not expose exams, homework, or watch rows.
- **Docker Acceptance**: Backend container builds and starts; API endpoint for parent student academic details returns 200 for a linked student and contains teacher-filterable watch, exam, homework, and balance data.
- **External Dependencies**: A test student with active access grants, lesson/video watch events, exam attempts, homework submissions, and balance transactions is needed for full manual verification.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST treat "teacher for parent tracking" as the teacher who owns the purchased lesson or package that grants the student access.
- **FR-002**: System MUST NOT use the creator of an exam or homework as the primary teacher filter when the item is attached to a purchased lesson or package.
- **FR-003**: System MUST list only teachers who have active, non-expired, non-cancelled, and non-disabled purchased or accessible lessons for the student.
- **FR-004**: System MUST replace the parent "schedules" experience with a "watch logs" experience.
- **FR-005**: System MUST show every purchased lesson for the selected teacher in watch logs, including lessons with no watch activity.
- **FR-006**: System MUST show watch totals per lesson aggregated from all watch events for videos in that lesson, including watched video count, watch count, watched duration when available, completion state, and latest watch timestamp when available.
- **FR-007**: System MUST show exams for the selected teacher when those exams are tied to lessons or videos the student has active access to under that teacher, including unsolved exams with a clear status.
- **FR-008**: System MUST allow the parent to open exam details and review score/status plus mistakes, student answers, correct answers, and correction/explanation text when available.
- **FR-009**: System MUST make homework follow the same teacher selection, list, detail, mistake, and correction review behavior as exams.
- **FR-010**: System MUST show submitted, unsubmitted, grading, and graded homework states clearly.
- **FR-011**: System MUST hide content connected only to inactive, expired, or unrelated access grants.
- **FR-012**: System MUST show the student's authoritative current balance.
- **FR-013**: System MUST show official balance transaction details from newest to oldest with amount, direction/type, date, balance after transaction when available, and description when available.
- **FR-014**: System MUST handle empty teacher lists, empty section data, missing optional fields, and failed loads without crashing.
- **FR-015**: System MUST preserve the parent navigation flow and allow returning from each detail screen to the same section.
- **FR-016**: System MUST preserve parent-student authorization boundaries so a parent can only retrieve tracking details for the linked student represented by the parent access context.

### Key Entities *(include if feature involves data)*

- **Parent Tracking Teacher**: A teacher visible to the parent because the student has active access to at least one lesson/package owned by that teacher.
- **Purchased Lesson**: A lesson available to the student through an active, non-expired, non-cancelled, and non-disabled package, term, section, lesson, video, or equivalent access grant.
- **Watch Log Lesson**: A purchased lesson with aggregated watch metrics and latest watch timestamp.
- **Exam Review**: An exam visible through a purchased lesson/video with attempt score/status and mistake review details.
- **Homework Review**: A homework item visible through a purchased lesson with submission status, grade, feedback, and mistake review details.
- **Balance Detail**: The student's current balance and ordered financial transactions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a test dataset with two teachers and mixed purchases, selecting a teacher shows zero rows from other teachers across watch logs, exams, and homework.
- **SC-002**: Purchased lessons with no watch activity still appear in watch logs for the correct teacher in 100% of covered test cases.
- **SC-003**: Submitted exams and homework show all available incorrect-answer review details without requiring parent access to admin screens.
- **SC-004**: The balance displayed in the parent app matches the authoritative balance record for the same student in every tested fixture.
- **SC-005**: Opening watch logs, exams, homework, and balance with empty or partial payloads produces no app crash in smoke testing.
- **SC-006**: The parent student details API returns the tracking payload in under 500 ms for normal seeded parent test data.

## Assumptions

- The existing parent linking and student selection flow remains unchanged.
- Existing access grants are the source of truth for which lessons/packages the student has purchased or can access.
- Existing balance and transaction records are the source of truth for the balance screen.
- Android may receive older backend payloads during rollout, so newly added fields should be treated as optional on the client.
- iOS and any web parent surface are out of scope for this feature unless explicitly requested later.

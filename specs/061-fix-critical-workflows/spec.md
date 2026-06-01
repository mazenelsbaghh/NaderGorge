# Feature Specification: Restore Critical Learning Workflows

**Feature Branch**: `[061-fix-critical-workflows]`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: User description: "Phase 1 — Critical: وظائف مكسورة بشكل كامل من FIXES_PLAN.md، وتشمل مراجعة تعليقات Community قبل النشر، تصحيح تقييم أسئلة اكتشف الغلطة، وإكمال سير عمل تصحيح الأسئلة المقالية."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Moderate Community Comments Before Publication (Priority: P1)

As an administrator, I need new community comments to stay unpublished until reviewed so that students only see approved content and inappropriate replies never appear publicly by default.

**Why this priority**: Unmoderated comments create immediate trust, safety, and content quality risk across the student community experience.

**Independent Test**: Can be fully tested by submitting a new comment, verifying it is hidden from students, then approving or rejecting it through the moderation flow and confirming the student-facing result changes correctly.

**Acceptance Scenarios**:

1. **Given** a student submits a new community comment, **When** the comment is created, **Then** it is stored in a pending moderation state and is not visible in student-facing community discussions.
2. **Given** an administrator reviews a pending comment and approves it, **When** students load the related discussion, **Then** the approved comment is visible in the correct position.
3. **Given** an administrator rejects a pending comment and records a reason, **When** the moderation action is completed, **Then** the comment remains hidden from students and the rejection reason is retained for administrative review.
4. **Given** an administrator opens the moderation queue, **When** there are pending community comments, **Then** only comments awaiting moderation are listed for review.

---

### User Story 2 - Grade Find-The-Mistake Answers Correctly (Priority: P2)

As a student taking an exam, I need find-the-mistake questions to be graded using their own answer format so that my score reflects the mistake I actually selected rather than unrelated multiple-choice logic.

**Why this priority**: Incorrect grading breaks exam fairness and invalidates assessment outcomes for a question type already exposed to students.

**Independent Test**: Can be fully tested by submitting correct and incorrect find-the-mistake answers and verifying that scoring, correctness labels, and result details match the intended answer format.

**Acceptance Scenarios**:

1. **Given** an exam contains a find-the-mistake question, **When** a student submits the exact expected mistake selection, **Then** the question is marked correct and its score is added to the final result.
2. **Given** an exam contains a find-the-mistake question, **When** a student submits a different mistake selection, **Then** the question is marked incorrect and no score is awarded for that question.
3. **Given** a submitted exam includes find-the-mistake questions, **When** the result is generated, **Then** the student sees correctness details consistent with the evaluated answer and not with multiple-choice behavior.

---

### User Story 3 - Track Essay Grading Through Completion (Priority: P3)

As a student or teacher, I need essay questions to move through a clear grading lifecycle so that pending, AI-reviewed, and teacher-finalized states are visible and final exam outcomes are only finalized when grading is complete.

**Why this priority**: Broken essay grading creates incomplete exam outcomes, blocks teacher workflows, and leaves students without trustworthy result status.

**Independent Test**: Can be fully tested by submitting an exam with essay answers, progressing each answer through pending, AI-reviewed, and teacher-reviewed states, and verifying grading status and final results update at each step.

**Acceptance Scenarios**:

1. **Given** a student submits an exam with essay answers, **When** automatic grading has not yet completed, **Then** each essay answer shows a waiting-for-AI status and the exam result is marked as partial or pending instead of final.
2. **Given** an essay answer has received AI scoring, **When** a teacher opens grading for that answer, **Then** the teacher can see that the answer is ready for teacher review.
3. **Given** an essay answer is still waiting for AI scoring, **When** a teacher attempts to finalize grading, **Then** the system prevents final teacher grading and indicates that AI review must complete first.
4. **Given** a teacher finalizes grading for all essay answers in an attempt, **When** the final grading action is saved, **Then** the overall exam result is recalculated and the attempt is marked with completed grading information.

### Edge Cases

- A comment is approved or rejected after the student has already loaded the page; the next refresh must reflect the latest moderation decision without showing stale unpublished content as approved.
- A rejected community comment must remain excluded from student views even if it previously appeared in a pending list or moderation export.
- A find-the-mistake answer arrives without the expected selection data; the submission must be treated as unanswered or invalid rather than being graded with another question type's rules.
- A find-the-mistake question stores its expected answer in one supported representation while the submission uses the other supported representation; the evaluation result must still be deterministic and documented.
- AI grading finishes for only some essay answers in an exam attempt; the attempt must expose partial grading progress without presenting the whole exam as fully finalized.
- A repeated AI grading callback or teacher grading action must not move an essay answer backward in status or produce duplicated final-result recalculations.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST place every newly submitted community post comment into a pending moderation state before it can appear in student-facing discussions.
- **FR-002**: System MUST store the moderation outcome for each community comment, including whether it was approved or rejected.
- **FR-003**: System MUST allow administrators to review pending community comments in a dedicated moderation queue.
- **FR-004**: System MUST allow administrators to approve a pending community comment and make it visible to students.
- **FR-005**: System MUST allow administrators to reject a pending community comment and record an optional rejection reason.
- **FR-006**: System MUST exclude pending and rejected community comments from all student-facing community comment listings.
- **FR-007**: System MUST evaluate find-the-mistake answers using the answer format defined for that question type rather than applying multiple-choice evaluation rules.
- **FR-008**: System MUST mark a find-the-mistake answer correct only when the student's selected mistake matches the question's expected mistake.
- **FR-009**: System MUST include the correct correctness status and awarded score for each find-the-mistake question in the generated exam result.
- **FR-010**: System MUST assign each essay answer a grading status when an exam is submitted and initialize that status as waiting for automated review.
- **FR-011**: System MUST update an essay answer's grading status when automated scoring completes and retain the automated score for later review.
- **FR-012**: System MUST expose essay grading status for an exam attempt so students and staff can determine whether results are pending, partially graded, or fully graded.
- **FR-013**: System MUST prevent teacher final grading of an essay answer until automated scoring for that answer has completed.
- **FR-014**: System MUST update an essay answer's grading status to teacher graded when final teacher grading is completed.
- **FR-015**: System MUST recalculate the overall exam result after teacher grading changes the final score of any essay answer.
- **FR-016**: System MUST return a partial or pending exam-result state whenever one or more essay answers in an attempt have not yet reached final grading.

### Key Entities *(include if feature involves data)*

- **Community Comment Moderation Record**: Represents a student comment on a community post together with its moderation state, submission details, reviewer outcome, and optional rejection reason.
- **Find-The-Mistake Answer**: Represents the student's selected mistake for a question designed around identifying a specific incorrect item or text fragment, along with the evaluated correctness and awarded score.
- **Essay Submission**: Represents a student's written or spoken essay answer, its automated grading output, its teacher-reviewed score, and its current grading lifecycle state.
- **Exam Attempt Result State**: Represents the current aggregate outcome of an exam attempt, including whether the result is final, partially graded, or still pending because of essay answers.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of newly submitted community comments remain invisible to students until an administrator explicitly approves them.
- **SC-002**: 100% of approved community comments appear in student-facing discussion views after approval, and 100% of rejected comments remain hidden.
- **SC-003**: In validation tests covering correct and incorrect find-the-mistake submissions, grading accuracy for that question type is 100%.
- **SC-004**: For exam attempts containing essay questions, users can retrieve the current grading status for each essay answer within one interaction from the relevant exam result or grading view.
- **SC-005**: 100% of exam attempts with unfinished essay grading are labeled as pending or partial rather than final.
- **SC-006**: After all essay answers in an attempt are teacher graded, the final exam result is recalculated and reflected to users in the same grading cycle without requiring manual data repair.

## Assumptions

- The existing community experience already distinguishes between student-facing and administrator-facing views, so this feature extends current moderation behavior rather than creating a new community product.
- Administrators are the only users allowed to approve or reject pending community comments in this phase.
- Find-the-mistake questions already have a single canonical expected answer per question, even if submissions may represent the student's selection in more than one supported format.
- Automated essay scoring remains part of the intended grading flow and is expected to complete before teachers finalize essay grades.
- Student-facing exam results may surface a partial or pending state for incomplete essay grading without requiring a redesign of the broader exam experience in this phase.

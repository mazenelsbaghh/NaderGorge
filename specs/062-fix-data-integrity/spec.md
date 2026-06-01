# Feature Specification: Phase 2 Data Integrity Fixes

**Feature Branch**: `[062-fix-data-integrity]`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: User description: "Phase 2 — High: مشاكل تؤثر على صحة البيانات / FIXES_PLAN.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Accurate lesson watch tracking (Priority: P1)

As a student, I need lesson watch progress and watch counts to be calculated consistently so that completion, eligibility, and usage-related decisions are based on correct viewing data.

**Why this priority**: Watch tracking affects whether a lesson is considered watched, whether extra viewing is justified, and whether student behavior is recorded correctly. Incorrect counts or thresholds can corrupt core academic and operational data.

**Independent Test**: Can be fully tested by recording watch activity for videos with known durations, including first-time views and missing-duration cases, and verifying that threshold decisions and watch counts are consistent across all watch-tracking entry points.

**Acceptance Scenarios**:

1. **Given** a lesson video with a known duration and a configured watch threshold, **When** the student reaches the threshold through any supported watch-tracking flow, **Then** the system records the lesson as counted using the same threshold rule in every flow.
2. **Given** a first-time watch event for a lesson video, **When** the student has not yet reached the required threshold, **Then** the system keeps the watch count at zero until the threshold is met.
3. **Given** a first-time watch event where the threshold is met during that request, **When** the event is processed, **Then** the watch count increases exactly once.
4. **Given** a watch-tracking request that does not include the lesson duration, **When** the system cannot determine the threshold safely, **Then** it rejects the request with a clear validation result instead of substituting a default duration.

---

### User Story 2 - Reliable persistence of student and exam data (Priority: P2)

As a student, I need my theme preference, essay submission data, and exam results to be saved and returned accurately so that the system reflects my real choices and submissions.

**Why this priority**: These issues affect saved preferences, essay evidence, and exam feedback. Incorrect or missing values reduce trust in the platform and can cause academic disputes or repeated support requests.

**Independent Test**: Can be fully tested by updating theme preferences for a student with and without an existing profile, submitting essay answers with audio attachments, and retrieving a completed exam result to verify that all expected fields are preserved and returned.

**Acceptance Scenarios**:

1. **Given** a student updates theme preferences, **When** a current display mode is included, **Then** the system saves that mode and returns the same mode in future preference reads.
2. **Given** a student without an existing profile updates theme preferences, **When** the request is processed, **Then** the system creates the missing profile and persists the new preferences in the same flow.
3. **Given** a student submits an essay answer with an audio attachment, **When** the submission is stored, **Then** the audio reference is retained with that essay answer.
4. **Given** an exam has ended and result details are available to the student, **When** the result is requested, **Then** the response includes the written correction for applicable questions.

---

### User Story 3 - Transparent outcomes for extra watch requests (Priority: P3)

As a student, I need rejected extra watch requests to include a visible rejection reason so that I understand the outcome and know whether to retry or seek help.

**Why this priority**: Request outcomes are incomplete without a reason. Students and support staff both need clear explanations to avoid confusion and repeated follow-up.

**Independent Test**: Can be fully tested by rejecting an extra watch request with a reason and verifying that subsequent status checks return both the rejection status and the same explanation.

**Acceptance Scenarios**:

1. **Given** an extra watch request is rejected with an explanation, **When** the student checks the request status, **Then** the status response includes the stored rejection reason.
2. **Given** an extra watch request is still pending or has been approved, **When** the student checks the request status, **Then** no rejection reason is returned.

### Edge Cases

- A video duration is missing, zero, or otherwise unusable when watch progress is recorded.
- A student reaches the watch threshold exactly at the boundary value.
- Multiple progress updates arrive for the same lesson view in close succession after the threshold is crossed.
- A student updates theme preferences before any profile record has ever been created.
- An essay answer is submitted without an audio attachment; the response must still remain valid.
- A result is requested before the exam is finished; written corrections must not be exposed early.
- A rejected extra watch request has an empty or whitespace-only explanation; the system must not expose a meaningless reason.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST use one shared watch-threshold rule for all lesson watch-tracking flows so that the same video duration and threshold percentage always produce the same threshold outcome.
- **FR-002**: The system MUST reject lesson watch-tracking requests when a valid video duration is not available, rather than using a substitute duration.
- **FR-003**: The system MUST initialize a newly created watch record with a zero watch count before applying threshold evaluation.
- **FR-004**: The system MUST evaluate whether a first-time watch record should be counted within the same request that creates it.
- **FR-005**: The system MUST ensure that a single watch-tracking request cannot increase the watch count more than once for the same lesson view.
- **FR-006**: The system MUST persist the student's current theme mode as part of theme preferences and return the persisted value on subsequent reads.
- **FR-007**: The system MUST create a student profile automatically during a theme preference update if no profile exists for that student.
- **FR-008**: The system MUST preserve all previously supported theme preference values when creating a missing student profile during an update.
- **FR-009**: The system MUST accept and store an optional audio reference for each essay answer when the student includes one in a submission.
- **FR-010**: The system MUST return stored written corrections in exam results only after the exam is no longer in progress.
- **FR-011**: The system MUST allow an extra watch request rejection to store an optional human-readable rejection reason.
- **FR-012**: The system MUST return the stored rejection reason when the student checks the status of a rejected extra watch request.
- **FR-013**: The system MUST omit rejection reasons from status responses for requests that are not rejected.
- **FR-014**: The system MUST preserve existing watch, preference, exam, and extra watch records during rollout without requiring students to resubmit data that is already valid.

### Key Entities *(include if feature involves data)*

- **Lesson Watch Record**: Represents a student's tracked viewing activity for a lesson video, including the evaluated threshold state and the number of counted watches.
- **Student Profile**: Represents persisted student-level preferences and profile data, including the currently selected theme mode.
- **Essay Answer Submission**: Represents a student's response to an essay question, including written content, optional audio evidence, and grading-related context.
- **Exam Result Detail**: Represents the information returned to a student after an exam, including per-question feedback such as written corrections when eligible.
- **Extra Watch Request**: Represents a student's request for additional viewing access, including request status and any rejection explanation shown to the student.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In validation testing, equivalent lesson watch events processed through different watch-tracking flows produce identical threshold decisions in 100% of tested cases.
- **SC-002**: In validation testing, first-time lesson watch requests produce zero duplicate count increments within a single request across 100% of tested boundary and repeat-update scenarios.
- **SC-003**: In validation testing, 100% of theme preference updates for students without an existing profile complete successfully and return the saved current mode on the next read.
- **SC-004**: In validation testing, 100% of essay submissions that include an audio reference can be retrieved later with that same audio reference still attached.
- **SC-005**: In validation testing, 100% of completed exam results for applicable questions return written corrections, while 0% of in-progress exam results expose them.
- **SC-006**: In validation testing, 100% of rejected extra watch requests with a recorded explanation return that explanation to the student on status lookup.

## Assumptions

- This feature covers the Phase 2 data-integrity issues listed in `FIXES_PLAN.md` and does not expand into performance or moderation work planned for later phases.
- Existing business rules for when a lesson should count as watched remain unchanged; only consistency and correctness of persisted results are being fixed.
- Existing student identity and authorization flows remain in place and are reused for all preference, exam, and request-status actions.
- Audio references supplied with essay answers are assumed to point to already accepted upload outcomes; this feature only guarantees persistence and retrieval.
- Written corrections already exist for the relevant exam content and only need to be returned at the correct stage of the exam lifecycle.
- Rejection reasons are intended for student visibility and support clarity, not as internal-only moderation notes.

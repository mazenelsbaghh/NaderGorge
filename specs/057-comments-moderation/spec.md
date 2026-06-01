# Feature Specification: Lesson Comments Moderation

**Feature Branch**: `057-comments-moderation`  
**Created**: 2026-04-08  
**Status**: Draft  
**Input**: User description: "تظبيط قسم التعليقات (Comments). عايزها تكون تحت الفيديو و يكون فيه مراعاة من صفحة الكورس عند المدرس اني اقبل الكومنت ولا لا"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Student posts a lesson comment (Priority: P1)

As a student, I want to find the comments section directly under the lesson video and submit a comment there, so that I can ask about the lesson without leaving the lesson page.

**Why this priority**: The visible comment area under the video is the core user-facing value. Without it, the feature does not solve the student's need to discuss the lesson in context.

**Independent Test**: Can be fully tested by opening a lesson page, confirming the comments section appears below the video, submitting a comment, and verifying the submission enters the expected review flow.

**Acceptance Scenarios**:

1. **Given** a student is viewing a lesson page with a video, **When** the page finishes loading, **Then** the comments section is displayed beneath the video player.
2. **Given** a student enters a valid comment and submits it, **When** the submission succeeds, **Then** the system records the comment for that lesson and shows the student that the comment is awaiting review.
3. **Given** a student has comments awaiting review, **When** the student views the public comment list for the lesson, **Then** only approved comments are visible in that public list.

---

### User Story 2 - Teacher reviews lesson comments from the course page (Priority: P2)

As a teacher, I want to review comments from the course page and decide whether to accept or reject each one, so that only suitable comments appear publicly under the lesson video.

**Why this priority**: Moderation is explicitly required by the request. It protects lesson quality and gives the teacher control over what appears to students.

**Independent Test**: Can be fully tested by opening the course page as a teacher, locating pending lesson comments, approving one comment and rejecting another, then confirming the public lesson page reflects only the approved comment.

**Acceptance Scenarios**:

1. **Given** a course contains lesson comments awaiting review, **When** the teacher opens the course management page, **Then** the teacher can see pending comments grouped within the relevant course context.
2. **Given** a pending comment is under review, **When** the teacher approves it, **Then** the comment becomes publicly visible under the related lesson video.
3. **Given** a pending comment is under review, **When** the teacher rejects it, **Then** the comment remains hidden from the public comment list.

---

### User Story 3 - Teacher monitors comment state clearly (Priority: P3)

As a teacher, I want each lesson comment to show a clear moderation status, so that I can quickly understand what still needs action and avoid reviewing the same comment multiple times.

**Why this priority**: Once comments begin arriving, clarity around status becomes necessary to keep moderation manageable and prevent mistakes.

**Independent Test**: Can be fully tested by creating comments in different states and verifying the teacher view distinguishes pending, approved, and rejected comments without ambiguity.

**Acceptance Scenarios**:

1. **Given** comments exist in multiple moderation states, **When** the teacher views the course comment management area, **Then** each comment displays its current status clearly.
2. **Given** a teacher has already moderated a comment, **When** the teacher returns to the course page later, **Then** the comment still shows its latest moderation status and action history is not ambiguous.

### Edge Cases

- If a lesson has no approved comments yet, the comments section still appears beneath the video with an empty-state message rather than disappearing.
- If a student submits an empty or whitespace-only comment, the system blocks the submission and explains that valid text is required.
- If the teacher opens the course page and there are no pending comments, the moderation area shows that no action is currently needed.
- If a previously approved or rejected comment is viewed later, its moderation state remains consistent and the public lesson page does not show rejected comments.
- If a student refreshes the lesson page immediately after posting, the system continues to show the correct status for that student's newly submitted comment.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a comments section directly beneath the lesson video on the lesson page.
- **FR-002**: System MUST allow authenticated students enrolled in the lesson's course to submit a text comment from the lesson page.
- **FR-003**: System MUST associate each submitted comment with the relevant course, lesson, author, submission time, and moderation status.
- **FR-004**: System MUST place every newly submitted comment into a pending review state before it becomes publicly visible.
- **FR-005**: System MUST show students a clear submission result and indicate when their comment is awaiting review.
- **FR-006**: System MUST display only approved comments in the public comments list beneath the lesson video.
- **FR-007**: System MUST provide the teacher with a comments review area within the course management page.
- **FR-008**: System MUST allow the teacher to approve a pending comment from the course management page.
- **FR-009**: System MUST allow the teacher to reject a pending comment from the course management page.
- **FR-010**: System MUST update the comment's status immediately after a teacher decision and preserve that status for future views.
- **FR-011**: System MUST present each comment in the teacher view with enough context to identify the related lesson, student author, submission time, comment text, and current moderation status.
- **FR-012**: System MUST prevent rejected comments from appearing in the public lesson comment list.
- **FR-013**: System MUST preserve approved comments across future visits so they continue to appear under the associated lesson video until intentionally removed by a later platform action.
- **FR-014**: System MUST provide an empty state for both the student lesson page and the teacher moderation area when no comments or no pending comments exist.

### Key Entities *(include if feature involves data)*

- **Lesson Comment**: A student-authored message attached to a specific lesson, including comment text, author identity, submission time, and moderation status.
- **Comment Moderation Decision**: The teacher's action on a lesson comment, including the decision outcome, decision time, and the teacher who performed the action.
- **Course Comment Queue**: The set of lesson comments within a course that require teacher review, organized so the teacher can process them from the course page.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 95% of sampled lesson pages, students can locate the comments section beneath the video without needing to navigate to another page.
- **SC-002**: In acceptance testing, 100% of newly submitted comments remain hidden from the public comment list until a teacher explicitly approves them.
- **SC-003**: Teachers can review and decide on a pending comment from the course page in under 30 seconds during moderated usability testing.
- **SC-004**: In end-to-end validation, 100% of approved comments become visible under the related lesson and 100% of rejected comments remain hidden.

## Assumptions

- Only teachers or course owners with existing course management access can moderate comments for their courses.
- The first release covers comment submission and approve/reject moderation only; editing comments, deleting comments, replies, and notifications are out of scope.
- Students are already authenticated and their course enrollment can be determined by the platform's current access rules.
- A rejected comment stays hidden from the public list and does not require a custom rejection reason in this phase.
- The lesson page already contains the video player, so the comments section can be positioned in relation to that existing lesson content.

# Feature Specification: Phase 3 Logic and Performance Fixes

**Feature Branch**: `063-fix-logic-performance`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: User description: "## Phase 3 — Medium: منطق خاطئ وأداء / FIXES_PLAN.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reliable watch tracking and request limits (Priority: P1)

As a student, I want watch tracking and extra watch requests to behave consistently so that my viewing progress is counted fairly and I receive clear feedback when I cannot request more access.

**Why this priority**: Viewing access and progress tracking directly affect whether students can continue lessons and whether support teams need to intervene.

**Independent Test**: Can be fully tested by submitting watch events, viewing progress updates, and repeated extra watch requests for one lesson, then verifying that the same settings are reused consistently and that request limits are enforced with a clear response.

**Acceptance Scenarios**:

1. **Given** platform watch rules have already been loaded recently, **When** a student submits a watch progress update or watch event, **Then** the system reuses the recent settings instead of reloading them for each request.
2. **Given** a student sends a watch event without a valid video duration, **When** the event is processed, **Then** the system rejects the request with a clear validation error and does not apply a fallback duration.
3. **Given** a student has reached the allowed number of extra watch requests for a video, **When** they submit another request, **Then** the system rejects it with a clear limit-reached message.

---

### User Story 2 - Predictable comment and moderation behavior (Priority: P2)

As an administrator or student, I want lesson and community moderation views to return the correct records and load efficiently so that moderation decisions are accurate and pages remain responsive.

**Why this priority**: Incorrect filtering exposes the wrong records to moderators or students, and inefficient moderation queries slow down daily operations.

**Independent Test**: Can be fully tested by loading moderation views and student comment views with mixed statuses, then confirming that status filters return only the intended records and that moderation summaries do not degrade noticeably as the number of posts grows.

**Acceptance Scenarios**:

1. **Given** lesson comments exist with multiple statuses, **When** an administrator filters by a specific status, **Then** the moderation list returns only comments matching that status.
2. **Given** a student has past lesson comments including rejected ones, **When** the student opens their own comment history, **Then** rejected items are either hidden or clearly identified as rejected.
3. **Given** the administrator opens the community posts moderation list, **When** the list is loaded, **Then** reaction and comment totals are returned without issuing separate per-post lookups.

---

### User Story 3 - Fair exam scoring and time enforcement (Priority: P3)

As a student or teacher, I want hint usage and question time limits to affect scoring consistently so that exam results match the actual attempt behavior.

**Why this priority**: Exam trust depends on accurate penalties and time enforcement, but these fixes are narrower in scope than lesson access and moderation correctness.

**Independent Test**: Can be fully tested by starting an exam attempt, using help tools on one question, allowing one timed question to expire, and then reviewing the final result to confirm penalties and timeout behavior are applied.

**Acceptance Scenarios**:

1. **Given** a student uses a help tool on a question, **When** the final result is generated, **Then** the question score reflects the configured penalty.
2. **Given** a timed question has a defined time allowance, **When** the student submits an answer after the allowance expires, **Then** the attempt records that the answer timed out and the question receives no score.
3. **Given** a question is answered within its allowed time and without help tools, **When** results are generated, **Then** the score is calculated normally with no penalty.

### Edge Cases

- What happens when the platform settings cache has expired and multiple requests arrive at the same time? The system must still return consistent settings-derived outcomes within the same request.
- How does the system handle an invalid or unsupported status filter in a moderation request? The system must reject the filter clearly instead of silently returning misleading results.
- What happens when a student used a help tool on a question that would otherwise earn zero points? The penalty must not reduce the score below zero.
- How does the system handle a question that has no configured time allowance? The system must not mark the answer as timed out solely because timing metadata is absent.
- What happens when historic extra watch requests include approved, rejected, and pending records? All prior requests for the same student and video must count toward the maximum if they are within scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST reuse the current platform settings for watch tracking and watch event evaluation for up to 10 minutes before reloading them.
- **FR-002**: The system MUST invalidate any cached platform settings immediately after an administrator updates platform settings so that new requests use the latest rules.
- **FR-003**: The system MUST reject any watch event or watch progress evaluation that is missing a valid video duration instead of substituting a default duration.
- **FR-004**: The system MUST apply lesson comment status filters using direct status matching so that moderation results are accurate for the requested status.
- **FR-005**: The system MUST ensure a student's lesson comment history does not surface rejected comments as normal visible comments.
- **FR-006**: The system MUST enforce a configurable maximum number of extra watch requests per student per video and return a clear error once the limit is reached.
- **FR-007**: The system MUST provide moderation summaries for community posts without requiring a separate record count lookup for each post in the returned list.
- **FR-008**: The system MUST record when a student uses any exam help tool that carries a penalty so the final score can reflect that behavior.
- **FR-009**: The system MUST apply the configured help-tool penalty percentage to the earned score of a question and MUST NOT reduce the score below zero.
- **FR-010**: The system MUST store the time at which a student starts a timed question when timing enforcement is enabled for that question.
- **FR-011**: The system MUST compare the answer submission time against the allowed question duration and mark late answers as timed out.
- **FR-012**: The system MUST award zero score to answers marked as timed out while preserving the attempt record for audit and review.
- **FR-013**: The system MUST continue to score questions normally when no penalty-triggering help tool was used and the answer was submitted within the allowed time.
- **FR-014**: The system MUST expose configuration defaults that allow administrators to manage the extra watch request limit and help-tool penalty without changing feature scope.

### Key Entities *(include if feature involves data)*

- **Platform Settings**: Global operational rules that define watch tracking behavior, extra watch request limits, and exam help-tool penalties.
- **Lesson Comment**: A student-authored lesson discussion item whose visibility to moderators and students depends on its moderation status.
- **Extra Watch Request**: A student request for additional access to a specific video, including its current status and count toward the per-video limit.
- **Community Moderation Post Summary**: An administrator-facing representation of a community post with precomputed engagement totals needed for moderation decisions.
- **Exam Attempt Answer**: A student's answer record for one exam question, including whether a help tool was used, when the question started, whether the answer timed out, and the final awarded score.
- **Watch Event**: A reported viewing action that depends on valid duration data and current platform rules to determine whether it should affect progress or watch counts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of watch event and watch progress requests that omit video duration are rejected with a user-visible validation message and no progress change.
- **SC-002**: 100% of extra watch request attempts beyond the configured per-video limit are blocked consistently across repeated submissions by the same student.
- **SC-003**: In moderation testing with mixed comment statuses, administrators receive only records matching the requested status in 100% of sampled filter cases.
- **SC-004**: The administrator community moderation list loads with no observable per-post count amplification, and median load time improves by at least 30% on a dataset of 100 posts compared with the pre-fix baseline.
- **SC-005**: 100% of exam questions answered after their allowed time are marked timed out and receive zero score in the final result.
- **SC-006**: 100% of penalized exam questions apply the configured deduction without driving the awarded score below zero.

## Assumptions

- Phase 3 is scoped only to the seven items listed under the "Medium: Logic and Performance" section in [FIXES_PLAN.md](/Users/mazenelsbagh/mazen mac/apps/nader gorge/FIXES_PLAN.md).
- Existing administrator workflows for updating platform settings remain in place; this feature only changes how updated rules are applied and enforced.
- Rejected lesson comments should not appear as ordinary visible comments to students; if product UI needs to show them later, they must be clearly marked as rejected.
- When a timed answer arrives after the allowed duration, the system keeps the submitted record for traceability but awards zero points instead of discarding the submission entirely.
- Existing moderation, watch tracking, and exam result surfaces remain the primary consumer interfaces for these fixes, with no new end-user flow introduced beyond clearer validation outcomes.

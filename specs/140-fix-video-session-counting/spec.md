# Feature Specification: Session-Safe Video View Counting

**Feature Branch**: `140-fix-video-session-counting`  
**Created**: 2026-06-18  
**Status**: Draft  
**Input**: User description: "Prevent seeking or continued playback in one video session from consuming multiple views while preserving accumulated incomplete progress across refreshes."

## Clarifications

### Session 2026-06-18

- Q: إذا انتهت مدة صلاحية جلسة التشغيل أثناء مشاهدة فيديو طويل، ما السلوك المطلوب؟ → A: تمديد صلاحية نفس الجلسة تلقائيًا مع استمرار المشاهدة الفعلية.
- Q: بعد فتح نفس الفيديو في تبويب أو جهاز أحدث، ماذا يحدث في التبويب القديم؟ → A: يتوقف المشغل وتظهر رسالة أن الفيديو فُتح في مكان أحدث، مع خيار إعادة التحميل.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One View Per Playback Session (Priority: P1)

As a student, I want one playback session to consume at most one allowed view so seeking, rewinding, or continuing playback after the threshold cannot consume additional views.

**Why this priority**: The current behavior can exhaust a student's purchased viewing allowance during a single continuous playback session.

**Independent Test**: Start one playback session, reach the configured threshold, continue playing and seek repeatedly, then verify exactly one view is registered.

**Acceptance Scenarios**:

1. **Given** an active playback session that has not registered a view, **When** its accepted watched time reaches the configured threshold, **Then** exactly one view is registered for that session.
2. **Given** an active playback session that already registered a view, **When** the student continues playback, seeks forward, or rewinds, **Then** no additional tracked time or view is recorded from that session.
3. **Given** a progress update is retried or duplicated, **When** the same update is processed again, **Then** neither tracked time nor view count is duplicated.

---

### User Story 2 - Continue Incomplete Progress Across Sessions (Priority: P2)

As a student, I want legitimate watched time below the threshold to remain accumulated after a refresh or reopen so I do not lose progress toward the next view.

**Why this priority**: Refreshes and interruptions are normal and must not force a student to repeat already watched time.

**Independent Test**: Watch three accepted seconds, refresh into a new session, watch three more accepted seconds, and verify total tracked progress is six seconds.

**Acceptance Scenarios**:

1. **Given** a session ends before reaching the view threshold, **When** a new session reports additional accepted watched time, **Then** the new time is added to the prior incomplete progress.
2. **Given** a session registers one view, **When** the student refreshes or reopens the video and a new session starts, **Then** accepted time in the new session can accumulate toward exactly one subsequent view.

---

### User Story 3 - Reject Stale Concurrent Sessions (Priority: P3)

As a student, I want concurrent playback attempts resolved consistently so duplicate tabs or devices cannot corrupt or unexpectedly consume my viewing allowance.

**Why this priority**: Concurrent updates can otherwise bypass the one-view-per-session rule or double count progress.

**Independent Test**: Open two sessions for the same student and video, then verify only the newest session can change tracked progress or view count.

**Acceptance Scenarios**:

1. **Given** a newer session exists for the same student and video, **When** an older session submits progress, **Then** the update is rejected without changing tracked seconds, view count, or lock state.
2. **Given** the newest valid session submits progress, **When** the update is accepted, **Then** normal accumulation and threshold rules apply.
3. **Given** an older player's progress update is rejected because a newer session exists, **When** the player receives that result, **Then** playback stops and the student sees that the video was opened in a newer tab or device plus an option to reload.

### Edge Cases

- A session ending below the threshold preserves accepted incomplete progress for a later session.
- Additional playback time after a session registers its view is ignored for view-counting progress.
- Seeking forward or backward does not itself add watched time or reset the session's one-view limit.
- Duplicate or retried progress updates do not duplicate accepted seconds.
- A stale, expired, consumed, invalid, or superseded session cannot modify watch progress.
- A valid session receiving regular accepted playback updates has its expiry extended without becoming a new view-eligible session; abandoned sessions still expire normally.
- Reaching the configured maximum view count still locks playback without granting an extra view.
- Approved extra views, administrative resets, and lesson repurchase continue to establish the existing unlocked/reset state.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: As a student, open a lesson video, reach its view threshold, continue playback and seek within the same page, and confirm the displayed view count increases only once.
- **Manual QA Role/Flow 2**: As a student, watch below the threshold, refresh, continue watching, and confirm the pre-refresh and post-refresh accepted time is combined.
- **Manual QA Negative Check**: Open the same video in two tabs and confirm progress from the older tab is rejected after the newer session is created.
- **Manual QA Superseded-Session Check**: Confirm the older player stops, explains that a newer tab or device superseded it, and offers a reload action.
- **Docker Acceptance**: Start the existing application stack, confirm backend and student frontend health, execute the session-counting regression flow, and verify no new migration is required unless planning proves otherwise.
- **External Dependencies**: No new external service is expected; validation uses the existing authenticated video playback and persistence infrastructure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST associate each accepted watch-progress update with an authenticated playback session belonging to the same student and video.
- **FR-002**: The system MUST register at most one view from a single playback session.
- **FR-003**: Once a session registers a view, the system MUST ignore subsequent watch-progress contribution from that session toward another view.
- **FR-004**: Starting a new session through refresh or reopening the video MUST allow accepted watched time to accumulate toward the next view.
- **FR-005**: Accepted watched time below the threshold MUST remain cumulative across valid sequential sessions.
- **FR-006**: Seeking forward, seeking backward, or replaying within the same session MUST NOT independently register a view or bypass the one-view-per-session rule.
- **FR-007**: For the same student and video, creating a newer session MUST supersede older active sessions, and superseded sessions MUST NOT change watch state.
- **FR-008**: Duplicate or retried progress updates MUST be idempotent and MUST NOT duplicate tracked seconds or views.
- **FR-009**: The existing configurable watch threshold, maximum/custom maximum limits, locking behavior, extra-watch approvals, administrative resets, and repurchase resets MUST remain unchanged.
- **FR-010**: Invalid, expired, consumed, superseded, unauthorized, or mismatched sessions MUST fail without changing tracked seconds, view count, or lock state.
- **FR-011**: Reaching the applicable maximum view count MUST continue to lock the video without registering more than the permitted count.
- **FR-012**: The system MUST preserve a consistent watch state when valid progress updates arrive concurrently.
- **FR-013**: The system MUST extend the same playback session while valid actual-playback updates continue, without granting that session eligibility to register another view.
- **FR-014**: When a player learns its session was superseded, it MUST stop playback, display a clear newer-tab-or-device message, and provide an action to reload into a new session.

### Key Entities

- **Playback Session**: A time-bounded authorization for one student to play one video; it has an identity, ownership, video association, lifecycle state, renewable activity/expiry timing, and whether it has already registered its permitted view. Regular valid playback activity extends the same session, while inactivity allows it to expire.
- **Video Watch State**: The student's cumulative state for one video, including accepted watched seconds, registered view count, applicable maximum, and lock state.
- **Progress Update**: An idempotent report of actual playback time associated with one playback session and ordered or uniquely identifiable within that session.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested sessions, continued playback and seeking after the threshold registers no more than one view for that session.
- **SC-002**: In 100% of tested refresh/reopen flows below the threshold, accepted watched time before and after the new session is preserved and combined accurately.
- **SC-003**: In 100% of duplicate-update tests, tracked seconds and view count change at most once.
- **SC-004**: In 100% of concurrent-session tests, only the newest valid session can change watch state.
- **SC-005**: Existing maximum-limit locking, extra-view approval, reset, and repurchase regression scenarios continue to produce their prior observable outcomes.
- **SC-006**: In 100% of superseded-session UI tests, the older player stops and displays the explanatory message and reload action.
- **SC-007**: A valid progress update or stale-session rejection becomes visible in the player within 2 seconds under normal connectivity.

## Assumptions

- Only authenticated students can create playback sessions or report progress for their own video access.
- “Watched time” means accepted actual playback duration under the existing anti-inflation rules; timeline position changes are not watched time.
- Refreshing or reopening creates a distinct playback session and intentionally permits progress toward the next view.
- Existing configured threshold percentages and view limits remain the source of truth.
- This fix does not redesign the video player or change pricing, entitlement, approval, reset, or repurchase policies.

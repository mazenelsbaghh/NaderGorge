# Feature Specification: Real-time Speed Remaining Completion

**Feature Branch**: `123-realtime-speed-remaining-completion`  
**Created**: 2026-06-11  
**Status**: Draft  
**Input**: Complete all remaining items from the realtime-platform-speed plan (audit 2026-06-11): missing outbox events, network/API optimizations, rate limiting, and performance logging.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Full Outbox Events Coverage (Priority: P1)

As a Platform User, when any significant modification is made to packages, terms, sections, lessons, access codes, homework, exams, or community posts, I want the system to emit outbox events so that my UI updates in real-time without manual refresh.

**Why this priority**: Without these events, the user interface remains out of sync for a large portion of the administrative and student workflows.

**Independent Test**: Perform an action (e.g. update a term, lock a lesson, grade a homework, or like a community post) and verify that the corresponding OutboxEvent is created in the database and processed.

**Acceptance Scenarios**:
1. **Given** an admin locks a lesson, **When** the command executes, **Then** a `LessonLocked` outbox event is created.
2. **Given** a teacher grades homework, **When** saved, **Then** a `HomeworkGraded` event is created with the student's ID.
3. **Given** a student posts in the community, **When** published, **Then** a `CommunityPostCreated` event is created.

---

### User Story 2 - Lesson Detail Split & Optimized Shell updates (Priority: P2)

As a Student, when I open a lesson page, I want the page to load instantly by only retrieving critical lesson summary, and loading resources/comments lazily or via separate smaller API requests.

**Why this priority**: The lesson detail page currently fetches a massive payload (including all videos, chapters, resources, homework, and comments), causing visible latency.

**Independent Test**: Measure the page load time of a lesson page with hundreds of comments; verify that the initial query size is significantly reduced and comments/nested resources are fetched asynchronously.

**Acceptance Scenarios**:
1. **Given** a student navigates to a lesson page, **When** the page loads, **Then** only the metadata and core video details are loaded initially.
2. **Given** the lesson page has finished rendering, **When** the comments section is scrolled into view, **Then** the comments are fetched asynchronously.

---

### User Story 3 - Client Web Vitals & Redis Rate Limiting (Priority: P2)

As an Admin, I want to track user-perceived performance metrics (LCP, INP, CLS) dynamically in the backend and ensure that heavy operations (like AI jobs and teting/activation endpoints) are rate-limited on a per-user basis.

**Why this priority**: Web Vitals are critical for identifying real-world performance bottlenecks. Fine-grained rate limits protect resources.

**Independent Test**: Trigger a rate-limited endpoint multiple times within the limit window, verify that a 429 response is returned.

**Acceptance Scenarios**:
1. **Given** a user triggers code activation 10 times in 10 seconds, **When** rate limiting is active, **Then** subsequent requests return HTTP 429.
2. **Given** a student completes page loading, **When** web vitals are calculated, **Then** they are sent to `/api/v1/metrics/web-vitals` endpoint.

---

### Edge Cases

- **Outbox Event processing failure**: If an outbox event fails to publish to SignalR, the background service should retry it with exponential backoff up to the retry limit.
- **Client Offline during Web Vitals report**: If the client is offline, Web Vitals metrics should be queued in localStorage and sent once online.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Outbox Check**: Perform admin changes (like adding a resource or deleting a video), verify SignalR pushes the update to clients.
- **Manual QA Rate Limit**: Send rapid POST requests to code activation, verify 429 status.
- **Docker Acceptance**: Run frontend and backend, verify no compilation warnings and all unit/integration tests pass.
- **External Dependencies**: Redis for rate limits and outbox lock tracking.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Backend MUST emit outbox events for all remaining commands that modify user-facing data (Packages, Terms, Sections, Lessons, Access Codes, Billing, Notifications, Homeworks, Exams, and Community).
- **FR-002**: Split the lesson detail endpoint into smaller query routes (`GetLessonDetail` for core metadata, `GetLessonComments` for paginated comments, and `GetLessonResources` for resources).
- **FR-003**: Optimize the student shell store to handle incremental updates (like notification badge counts) rather than performing full shell refreshes.
- **FR-004**: Enforce per-user Redis rate limiting on heavy actions: code activation, starting AI analysis, and signed URL generation.
- **FR-005**: Add `/api/v1/metrics/web-vitals` POST endpoint on backend to collect performance metrics.
- **FR-006**: Report LCP, INP, and CLS metrics from Next.js frontend to the metrics endpoint.
- **FR-007**: Audit all major relational queries in the backend using `EXPLAIN` and add missing indexes to PostgreSQL.

### Key Entities

- **OutboxEvent**: Existing database entity representing events to be processed.
- **WebVitalsMetric**: Represents a single client performance report (Metric Name, Value, Page URL, UserAgent).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All planned outbox event types (47+ types) are implemented and verifiable in backend.
- **SC-002**: Lesson detail initial payload size is reduced by at least 60% on lessons with many comments.
- **SC-003**: Per-user rate limiting returns HTTP 429 when limits are exceeded.
- **SC-004**: Web vitals metrics are logged in the database/logs upon page loads.
- **SC-005**: Zero compilation warnings on both backend and frontend.

## Assumptions

- **A-001**: The existing `OutboxProcessorBackgroundService` can handle the expanded event volume without performance degradation.
- **A-002**: Redis container is running and accessible by both API and Worker.

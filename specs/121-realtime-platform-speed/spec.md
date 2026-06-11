# Feature Specification: Real-time Platform Speed & Sync

**Feature Branch**: `121-realtime-platform-speed`  
**Created**: 2026-06-11  
**Status**: Draft  
**Input**: User description: "Implementing the comprehensive platform acceleration and real-time synchronization plan as detailed in docs/realtime-platform-speed-plan-2026-06-11.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Instant Notification & Balance Updates (Priority: P1)

As a Student, when I receive a new notification or my account balance changes (due to purchase or code activation), I want the changes to reflect instantly on my screen without having to refresh the page.

**Why this priority**: Immediate feedback on financial changes and system notifications is critical for trust and smooth user experience.

**Independent Test**: Can be tested by triggering a balance change from the admin panel and verifying that the student's active session UI updates the balance display and displays a notification toast immediately.

**Acceptance Scenarios**:
1. **Given** a student is logged in and viewing the dashboard, **When** their balance is updated on the server, **Then** the balance in the sidebar updates instantly without page reload.
2. **Given** a student is on any page, **When** a notification is created for them, **Then** the notification counter increments and a temporary toast notification is shown on screen.

---

### User Story 2 - Real-time Lesson and Content Updates (Priority: P1)

As a Student, when a teacher publishes a new lesson, updates a package, or when a file/video becomes ready in my active lesson, I want the content to appear immediately so I don't have to reload.

**Why this priority**: Enhances educational engagement and prevents students from missing newly added content or having to refresh multiple times to check if a lesson is available.

**Independent Test**: Can be tested by adding a new lesson to a package from the admin dashboard and verifying that a student viewing that package page sees the new lesson card appear instantly.

**Acceptance Scenarios**:
1. **Given** a student is viewing the curriculum list of an active package, **When** an admin publishes a new lesson in that package, **Then** the new lesson appears in the student's list within 1 second without a page reload.
2. **Given** a student is viewing a lesson page where a video is processing, **When** the video processing completes, **Then** the video player is displayed instead of the processing message without page reload.

---

### User Story 3 - Real-time AI Analytics Monitoring (Priority: P2)

As a Teacher or Admin, when I start an AI video analysis or chaptering task, I want to see the progress bar update in real-time on the monitor without rapid polling.

**Why this priority**: Saves database and network resources (by eliminating 2-second polling) and provides a highly responsive UI.

**Independent Test**: Can be tested by initiating an AI analysis job and watching the job monitor page update progress percentages dynamically.

**Acceptance Scenarios**:
1. **Given** an admin is viewing the AI job monitor, **When** a job progresses, **Then** the progress percentage updates in real-time.

---

### User Story 4 - Secure, Fast and Idempotent Operations (Priority: P1)

As a Student, when I perform sensitive actions like code activation, purchasing, or exam submission, I want the system to process it safely, preventing double charges or duplicate submissions if I double-click or have a flaky connection.

**Why this priority**: Ensures data integrity and financial safety under network instability.

**Independent Test**: Can be tested by submitting the same code activation request twice simultaneously with the same idempotency key and verifying that only one activation completes, and the second returns the cached response.

**Acceptance Scenarios**:
1. **Given** a student is activating a package code, **When** they click the activate button twice quickly, **Then** only one transaction is processed, and no duplicate balance is credited.

---

### Edge Cases

- **Flaky SignalR connection**: What happens when the user's internet drops? The system MUST fall back to slow polling (30-60 seconds) and automatically reconnect SignalR when the connection is restored.
- **Outbox Event processing failure**: What if an outbox event fails to send via SignalR? The background processor must retry sending with exponential backoff up to a max retry limit, and log failures for admin monitoring.
- **Concurrent identical BullMQ jobs**: What if two requests start the same video processing job? The system must reject the duplicate job using a unique job ID (e.g. `video-process:{lessonVideoId}`).

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Student Updates**: Log in as a student, open a package page. On another browser window, log in as an admin and publish a lesson to that package. Verify the student page updates instantly.
- **Manual QA Idempotency Check**: Submit code activation with the same `Idempotency-Key` header twice. Verify the second request returns the same response as the first without doing double-activation.
- **Docker Acceptance**: Verify that the PostgreSQL migrations for `OutboxEvents` run successfully, and the Redis Docker service handles rate-limiting and idempotency keys correctly.
- **External Dependencies**: Requires a running SignalR hub connection and Redis service.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST establish a unified platform hub `/hubs/platform` for real-time events.
- **FR-002**: Users MUST be grouped automatically upon connection based on their ID, role, and active package/lesson subscriptions.
- **FR-003**: The backend MUST use the Outbox Pattern to persist events to the database (`OutboxEvents` table) during business transaction commits, and a background service must process and deliver them via SignalR.
- **FR-004**: System MUST handle events on the frontend: invalidate cache keys (React Query/Axios caches) and trigger localized UI refreshes rather than reloading the entire page or using `router.refresh()`.
- **FR-005**: System MUST provide short-lived signed URLs for secure downloads, supporting HTTP range requests for videos. Use `X-Accel-Redirect` for local storage in Docker.
- **FR-006**: System MUST enforce user rate limits in Redis for resource-intensive operations (code activation, video sessions, download requests, AI jobs).
- **FR-007**: System MUST support idempotency keys (`Idempotency-Key` header) for critical POST requests (purchase, code activation, exam submission, extra watch requests).
- **FR-008**: System MUST prevent duplicate BullMQ jobs using unique job IDs.
- **FR-009**: Backend MUST optimize database queries (use `AsNoTracking()`, DTO projections, pagination, and restrict loops over queries).

### Key Entities *(include if feature involves data)*

- **OutboxEvent**: Represents an event to be processed and dispatched.
  - `Id`: GUID (Primary Key)
  - `Type`: String (e.g., "BalanceChanged")
  - `PayloadJson`: JSON string containing event details
  - `TargetGroup`: String (optional target group)
  - `TargetUserId`: String (optional target user id)
  - `CreatedAt`: DateTime
  - `ProcessedAt`: DateTime (nullable)
  - `RetryCount`: Integer
  - `LastError`: String (optional)

- **IdempotentRequest**: Represents a recorded request to prevent duplicate execution.
  - `IdempotencyKey`: String (Primary Key)
  - `ResponseStatusCode`: Integer
  - `ResponseBody`: String
  - `CreatedAt`: DateTime
  - `ExpiresAt`: DateTime

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new lesson published by a teacher shows up in the student's active view in under 1.5 seconds.
- **SC-002**: Balance and notification updates display in the user's interface in under 1 second.
- **SC-003**: Elimination of rapid polling (every 2-3 seconds) for AI monitoring and video status, replaced with SignalR events. Fallback polling must run at 30-60 seconds interval.
- **SC-004**: API P95 latency for reads is under 300ms, and lesson/dashboard loads under 800ms.
- **SC-005**: 100% of double-submitted POST requests with the same idempotency key are deduplicated successfully.

## Assumptions

- **A-001**: Next.js client has access to the SignalR JS client library.
- **A-002**: Redis is running and available in Docker for rate limiting and idempotency caching.
- **A-003**: The Nginx configuration supports `X-Accel-Redirect` headers from the ASP.NET Core backend proxy.

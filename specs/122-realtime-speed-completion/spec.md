# Feature Specification: Real-time Speed Completion — Phase 2

**Feature Branch**: `122-realtime-speed-completion`  
**Created**: 2026-06-11  
**Status**: Draft  
**Input**: Complete the remaining items from the realtime-platform-speed plan (audit 2026-06-11): cache invalidation registry, remove refresh/reload patterns, expand outbox events coverage, lazy-load heavy JS libraries, expand idempotency, and add monitoring/tooling.
**Predecessor**: `121-realtime-platform-speed` (infrastructure layer — 100% complete)

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Cache Invalidation Registry & Refresh Elimination (Priority: P1) 🎯 MVP

As a Student, when I purchase content, activate a code, or any real-time event arrives, I want the relevant parts of the page to update instantly without a full page reload so the experience feels seamless.

**Why this priority**: 3 screens still force `router.refresh()` or `window.location.reload()`, breaking the real-time contract. A unified cache invalidation registry is the prerequisite for all other real-time improvements.

**Independent Test**: Activate a code on QR redeem page; verify the page updates packages/balance without any visible full reload.

**Acceptance Scenarios**:
1. **Given** a student just purchased content via `PurchaseContentModal`, **When** the purchase succeeds, **Then** the balance, packages, and shell data update in-place without `router.refresh()`.
2. **Given** a student scans and redeems a QR code, **When** activation succeeds, **Then** the page updates reactively without `window.location.reload()`.
3. **Given** an admin is viewing the students list and clicks reload, **When** the data refreshes, **Then** it uses targeted data refetch, not `window.location.reload()`.
4. **Given** any SignalR event is received, **When** the frontend processes it, **Then** only the affected cache keys are invalidated via `invalidate("key")` function.

---

### User Story 2 — Expanded Outbox Events Coverage (Priority: P1)

As a Platform User, when any significant data change occurs (lesson updates, exam submissions, homework grading, video failures, community posts), I want real-time notifications so I always see the latest state.

**Why this priority**: Only 11 of ~47 planned events are implemented. The remaining 36 events leave many workflows without real-time updates.

**Independent Test**: Update a lesson from admin, verify the student on that lesson page sees the update arrive via SignalR without refresh.

**Acceptance Scenarios**:
1. **Given** an admin updates an existing lesson, **When** the update is saved, **Then** a `LessonUpdated` outbox event is created and dispatched via SignalR.
2. **Given** a video processing fails, **When** the failure is recorded, **Then** a `VideoFailed` event is sent to the lesson group and the admin sees an error indicator.
3. **Given** a student submits an exam, **When** submission succeeds, **Then** an `ExamSubmitted` event is created.
4. **Given** a student submits homework, **When** submission succeeds, **Then** a `HomeworkSubmitted` event is created.
5. **Given** a community post is created, **When** saved, **Then** a `CommunityPostCreated` event is dispatched.

---

### User Story 3 — Polling Elimination & Backoff (Priority: P1)

As an Admin or Teacher, when monitoring AI jobs or video processing, I want the system to use SignalR events exclusively when connected, and fall back to slow polling (30-60s) only when disconnected, so the server isn't overloaded.

**Why this priority**: Fast polling at 2.5s wastes server resources and contradicts the real-time architecture.

**Independent Test**: Open AI monitor, verify no polling intervals below 30s when SignalR is connected. Disconnect SignalR, verify fallback polling runs at 30-60s intervals.

**Acceptance Scenarios**:
1. **Given** SignalR is connected, **When** viewing the AI monitor, **Then** zero `setInterval` runs below 30 seconds.
2. **Given** SignalR disconnects, **When** fallback polling activates, **Then** the polling interval is 30-60 seconds with exponential backoff.
3. **Given** video processing status in `LessonVideoList`, **When** SignalR is connected, **Then** no polling occurs; status updates arrive via events.

---

### User Story 4 — Lazy Loading Heavy JS Libraries (Priority: P2)

As a Student or Visitor, when loading pages, I want the initial JavaScript bundle to be as small as possible so pages load faster, especially on slower connections.

**Why this priority**: OGL (3D), GSAP, and QR Scanner are imported eagerly, increasing the initial bundle size for all users even if they don't need these features.

**Independent Test**: Run `ANALYZE=true npm run build`, verify OGL/GSAP/QR scanner are NOT in the main chunks but in their own lazy-loaded chunks.

**Acceptance Scenarios**:
1. **Given** a user loads a page that doesn't use 3D effects, **When** the page renders, **Then** `ogl` library code is not loaded.
2. **Given** a user opens the QR scanner modal, **When** the modal opens, **Then** the `@yudiel/react-qr-scanner` library is loaded dynamically at that point.
3. **Given** a user scrolls to a section using GSAP SplitText animation, **When** the section enters viewport, **Then** GSAP loads via dynamic import.

---

### User Story 5 — Expanded Idempotency & Monitoring (Priority: P2)

As a Student, when I submit an exam or request extra watch and have a flaky connection, I want the system to protect me from duplicate submissions. As an Admin, I want monitoring tools (bundle analyzer, web vitals) to track performance.

**Why this priority**: Financial and academic integrity depends on idempotency. Monitoring is needed for ongoing performance tracking.

**Independent Test**: Submit exam twice with same Idempotency-Key, verify only one submission processes.

**Acceptance Scenarios**:
1. **Given** a student submits an exam, **When** they double-click submit, **Then** only one submission is processed.
2. **Given** a student requests extra watch, **When** the request is sent twice, **Then** only one request is created.
3. **Given** an admin runs `ANALYZE=true npm run build`, **When** the build completes, **Then** a bundle analysis report is generated.

---

### Edge Cases

- **Cache invalidation collision**: Two events for the same cache key arrive within milliseconds. The registry must deduplicate fetches using a debounce/throttle mechanism.
- **Outbox event explosion**: Many events generated in rapid succession. The background processor must batch-process efficiently and not overload SignalR.
- **Dynamic import failure**: What if a lazy-loaded chunk fails to load? The component must show a fallback/error boundary, not crash the page.
- **Idempotency key expiry**: What happens if the idempotency window (5 minutes) expires between retries? The system should process as a new request.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Purchase Flow**: Purchase content as student, verify balance and packages update without any visible page reload.
- **Manual QA QR Redeem**: Scan a code, verify the page updates reactively.
- **Manual QA Admin Students**: Refresh the admin students list, verify no `window.location.reload()`.
- **Manual QA AI Monitor**: Open AI monitor with active jobs, verify no rapid polling when SignalR connected.
- **Docker Acceptance**: Run `cd frontend && npm run build` with zero errors. Run `cd backend && dotnet test` with all tests passing.
- **External Dependencies**: Redis for idempotency, PostgreSQL for outbox events, SignalR for real-time events.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Frontend MUST implement a centralized `invalidate(key)` function that accepts cache key patterns and triggers targeted refetches.
- **FR-002**: All 3 remaining `router.refresh()` / `window.location.reload()` calls MUST be replaced with optimistic updates + cache invalidation + platform events.
- **FR-003**: Backend MUST emit outbox events for all remaining command handlers that modify user-facing data (lessons, exams, homework, community, videos, resources, terms, sections, codes groups).
- **FR-004**: Frontend `usePlatformEvents` MUST be updated to handle new event types and route them through the cache invalidation registry.
- **FR-005**: Polling in `AIMonitorPageClient.tsx` and `LessonVideoList.tsx` MUST be eliminated when SignalR is connected, with fallback polling at minimum 30s.
- **FR-006**: Heavy JS libraries (OGL, GSAP, QR Scanner) MUST be loaded via `next/dynamic` or `React.lazy` with appropriate fallbacks.
- **FR-007**: `[Idempotent]` attribute MUST be added to exam submission, homework submission, extra watch request, and AI job start endpoints.
- **FR-008**: Bundle analyzer script (`ANALYZE=true npm run build`) MUST be added to `package.json`.

### Key Entities

- **OutboxEvent**: Already exists. No schema changes needed; new event _types_ are added in command handlers.
- **Cache Invalidation Registry**: New frontend utility mapping event types to cache keys for targeted invalidation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero instances of `router.refresh()` or `window.location.reload()` in the codebase (verified by `rg` search).
- **SC-002**: At minimum 25 outbox event types are implemented (up from current 11).
- **SC-003**: No polling interval below 30 seconds exists anywhere in the frontend when SignalR is connected.
- **SC-004**: OGL, GSAP, and QR Scanner libraries are NOT present in the main bundle chunk (verified by bundle analysis).
- **SC-005**: All sensitive POST endpoints (exam submit, homework submit, extra watch, AI job start) have `[Idempotent]` attribute.
- **SC-006**: Frontend build completes with zero errors and backend tests all pass.

## Assumptions

- **A-001**: The infrastructure from `121-realtime-platform-speed` (PlatformHub, OutboxEvent, usePlatformEvents, OutboxProcessorBackgroundService, RedisIdempotencyService) is fully functional and tested.
- **A-002**: SignalR connection is stable in production Docker environment.
- **A-003**: The existing `usePlatformEvents` singleton pattern correctly handles multiple concurrent component subscriptions.
- **A-004**: Adding outbox events to existing command handlers does not require database schema changes beyond the existing `OutboxEvents` table.

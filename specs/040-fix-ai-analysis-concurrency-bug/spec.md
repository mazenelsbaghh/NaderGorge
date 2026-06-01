# Feature Specification: Fix AI Analysis Concurrency Bug

**Feature Branch**: `040-fix-ai-analysis-concurrency-bug`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: DbUpdateConcurrencyException during AiAnalysisCompletedCommandHandler

## Clarifications

### Session 2026-04-01

- Q: آلية حل التزامن وإعادة المحاولات (Concurrency Resolution Strategy) → A: جعل الطلبات آمنة من التكرار (Idempotent) واعتماد التحديثات الجزئية (Partial Updates).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reliable AI Analysis Completion (Priority: P1)

The system must successfully save the results of the AI video chapter analysis to the database without throwing a DbUpdateConcurrencyException. When the background job completes the analysis and sends the results to the backend webhook, the backend should update the video and chapter records reliably.

**Why this priority**: The bug causes the background job to fail explicitly stating "Webhook failed with status 500" reducing the reliability of the AI components. This is critical for users waiting for video processing to finish.

**Independent Test**: Can be tested by triggering the video analysis job and observing the backend logs to ensure no `DbUpdateConcurrencyException` is thrown and the job completes successfully.

**Acceptance Scenarios**:

1. **Given** an AI analysis job is running for a video, **When** the job completes and submits the data to the backend webhook, **Then** the backend correctly updates the database and returns a successful response (200 OK) to the worker.

---

### Edge Cases

- What happens when multiple webhook events arrive for the same video simultaneously? -> The webhook processing will use an idempotent approach and partial entity updates to prevent clashes.
- How does the system handle retries from the background worker if the initial webhook update genuinely fails due to database locks? -> Since the endpoint is idempotent, retried webhooks will simply re-apply the update without corrupting data or crashing EF Core unnecessarily.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST process the AI analysis completion webhook payload without a `DbUpdateConcurrencyException`.
- **FR-002**: System MUST correctly save the AI generated chapters and subtitles back to the `lesson_videos` and `video_chapters` tables.
- **FR-003**: System MUST handle concurrent data modification scenarios seamlessly so that the background worker does not crash.

### Key Entities

- **LessonVideo**: The core entity that represents the video being processed.
- **VideoChapter**: The generated chapters for the video resulting from the AI processing.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The video AI analysis job successfully completes 100% of the time without database concurrency exceptions from the webhook.
- **SC-002**: The webhook endpoint returns an HTTP 200 Success status for valid payloads.

## Assumptions

- The DbUpdateConcurrencyException is likely caused because the EF Core tracked entities are either out of sync or updated inappropriately in the DbContext.
- The `bullmq` worker will retry the job if the webhook fails, so the webhook should handle retries safely.

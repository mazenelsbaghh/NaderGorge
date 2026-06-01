# Feature Specification: Cancel AI Analysis and Provider Handling

**Feature Branch**: `038-cancel-ai-analysis`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "Add button to cancel AI analysis job and handle Telegram/Youtube URL formats"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Graceful Job Cancellation (Priority: P1)

As an administrator, I want to be able to cancel an ongoing AI video analysis job from the dashboard so that I can stop a stuck or unnecessary process without waiting for it to fail or finish.

**Why this priority**: Long-running audio extractions or Gemini analyses might get stuck or consume unnecessary quota. An abort mechanism provides critical operational control.

**Independent Test**: Can be fully tested by triggering an AI job and clicking "Cancel", verifying the job is removed from the queue and the UI reverts to idle state.

**Acceptance Scenarios**:

1. **Given** a video is actively being processed by the AI worker, **When** the administrator clicks the "Cancel" button, **Then** the worker aborts the FFmpeg extraction or Gemini request, and the job status is set to failed/cancelled.
2. **Given** a canceled job, **When** the frontend polls the status, **Then** it receives the cancellation state and restores the "Generate AI Chapters" button.

---

### User Story 2 - Automated URL Normalization for Third-Party Providers (Priority: P2)

As an administrator, I want the system to automatically handle videos hosted on YouTube (which only provide an ID) and Telegram (which provide an embed URL) when extracting audio, so that I don't see "Invalid input data" errors in the queue.

**Why this priority**: Without this normalization, YouTube and Telegram videos will perpetually fail the AI chaptering process, breaking the feature for non-BunnyNet videos.

**Independent Test**: Can be fully tested by triggering an AI job on a YouTube video (providing the video ID) and confirming the audio extraction successfully initiates and completes.

**Acceptance Scenarios**:

1. **Given** a YouTube video ID (e.g., `lQSFol67eW0`), **When** the worker processes the job, **Then** it automatically structures the URL as a valid YouTube watch link for extraction.
2. **Given** a Telegram embed URL (e.g., `https://t.me/c/id/msg`), **When** the worker attempts extraction, **Then** it appropriately routes it through `yt-dlp` or handles it gracefully if inaccessible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a red "Cancel/Stop" button next to the AI progress bar in the admin video list.
- **FR-002**: System MUST expose an API endpoint (`/api/v1/internal/bullmq/{videoId}/cancel` or similar) to remove active/waiting jobs from the AI queue.
- **FR-003**: The Node.js Worker MUST support graceful abortion of `yt-dlp` child processes if a job is cancelled mid-extraction.
- **FR-004**: System MUST check if the `sourceUrl` is exactly 11 characters (typical YouTube ID) and automatically prepend `https://www.youtube.com/watch?v=` before passing it to the extractor.
- **FR-005**: System MUST fail gracefully with a specific, friendly error if a Telegram URL cannot be scraped due to privacy restrictions.

### Key Entities

- **AnalyzeVideoJob**: The BullMQ job representing the analysis task. Must be trackable and cancellable by its ID (which matches the `LessonVideoId`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Administrators can successfully cancel at least 95% of active AI jobs within 3 seconds of clicking the stop button.
- **SC-002**: 100% of valid publicly accessible YouTube video IDs correctly initiate the extraction process without throwing "Invalid format" errors.
- **SC-003**: Cancelled jobs do not continue to consume CPU/API resources in the background.

## Assumptions

- We assume that `ffmpeg` or `yt-dlp` processes run as child processes that can be killed safely using OS signals (e.g., `SIGTERM`).
- We assume Telegram links provided are either public or supported by the installed extraction engine, otherwise, the extraction will fail gracefully.

# Feature Specification: Telegram Large Media Fix

**Feature Branch**: `045-telegram-large-media`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: "عايز حل لمشكله media is to big لتلجرام \n\nGET /api/video/embed?t=... 404 in 1792ms"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reliable Playback of Large Videos (Priority: P1)

As a student, I want to be able to play video lessons hosted on Telegram regardless of their file size, so that my learning process is not interrupted by technical failures.

**Why this priority**: Core value of the platform is delivering educational video content; inability to load large videos directly blocks this objective.

**Independent Test**: Can be fully tested by uploading a known large video (e.g., > 100MB) to Telegram, embedding it in a lesson, and successfully streaming it through the `/api/video/embed` endpoint without encountering a 404 timeout.

**Acceptance Scenarios**:

1. **Given** a lesson containing a large Telegram-hosted video, **When** the user attempts to play the video via the application, **Then** the video begins streaming successfully without timing out.
2. **Given** a large video embedded from Telegram, **When** the `/api/video/embed` endpoint is called, **Then** it returns a valid video response instead of a 404 error.

---

### User Story 2 - Graceful Error Handling for Unsupported Media (Priority: P2)

As a user, if a video is fundamentally corrupted or impossible to load due to constraints we cannot bypass, I want to see a clear error message explaining the issue, so I don't wait indefinitely or see cryptic 404 pages.

**Why this priority**: While fixing playback is P1, if a file genuinely cannot be loaded, failing gracefully prevents user confusion and support tickets.

**Independent Test**: Can be fully tested by purposely trying to load a broken/missing video ID and verifying the UI shows a friendly error message rather than breaking the page.

**Acceptance Scenarios**:

1. **Given** a video that cannot be loaded under any circumstances, **When** the player attempts to fetch it, **Then** a localized, user-friendly error message is displayed indicating the media is unavailable.

### Edge Cases

- What happens when the user's internet connection is too slow to stream the large initial chunks of the video?
- How does the system handle concurrent requests for the same large video file across multiple users?
- What happens if the Telegram hosting server is temporarily unreachable while fetching the large file?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support retrieving or streaming Telegram-hosted media files that exceed the standard Bot API file size limits.
- **FR-002**: System MUST process `/api/video/embed` requests for large files within acceptable response times (e.g., without relying on downloading the entire file synchronously first).
- **FR-003**: System MUST NOT return a 404 error solely due to the media file being classified as "too large" by the Telegram source.
- **FR-004**: System MUST display localized, user-friendly error messages if the media fetching pipeline definitively fails.
- **FR-005**: System MUST resolve a direct public URL for the client's browser to stream the media, ensuring bandwidth consumption remains on Telegram's servers.

### Key Entities

- **LessonVideo**: Represents the video entity being requested, containing the Telegram-specific metadata needed to locate and fetch the file.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Telegram videos exceeding standard size limits (e.g., 50MB+) successfully load and stream for 99% of requests.
- **SC-002**: 404 errors related to fetching large Telegram videos from `/api/video/embed` drop to 0.
- **SC-003**: Initial playback (Time to First Byte) starts in under 3 seconds for large media files.

## Assumptions

- We have permission and control over the Telegram source (Bot or Channel) to utilize alternative fetching mechanisms if standard fetching fails.
- This fix targets the backend pipeline for fetching files, and the frontend player requires no functional changes other than surfacing existing error states gracefully.
- The 404 error encountered is directly caused by Telegram rejecting the download request internally due to constraints like media size limits.

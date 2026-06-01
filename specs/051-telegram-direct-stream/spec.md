# Feature Specification: Telegram Direct Stream

**Feature Branch**: `051-telegram-direct-stream`  
**Created**: 2026-04-02  
**Status**: Draft  
**Input**: User description: "Remove OK.ru and replace it with a Telegram Direct Stream micro-service that fetches direct mp4 links of large files from Telegram via a bot and issues a 302 Redirect to the video player (Video.js/custom) in the frontend for a fast, headless, large bandwidth experience with a purely custom academy player."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Headless Video Playback via Telegram (Priority: P1)

As a student, I want to watch educational videos within a fully customized, branded video player without any external platform logos (like OK.ru or Youtube), so that I remain entirely within the academy's immersive learning environment.

**Why this priority**: Delivering a seamless learning experience without external platform branding or broken controls is the core objective of replacing OK.ru. It requires complete programmatic control over the player.

**Independent Test**: Can be fully tested by playing a lesson video and observing that the video loads natively in the browser via an MP4 stream (originating from Telegram) and reacts instantly to custom control buttons (Play, Pause, Progress Bar).

**Acceptance Scenarios**:

1. **Given** a student loads a lesson with a Telegram Direct Video, **When** the page renders, **Then** the video plays seamlessly using the academy's custom player interface without external platform branding.
2. **Given** a student is watching a video, **When** they click the custom "Pause" or slide the progress bar, **Then** the video pauses or seeks accurately, proving full headless control.

---

### User Story 2 - Administrator Providing Telegram Videos (Priority: P2)

As an administrator, I want to upload a large video to a Telegram channel as a file, copy its direct stream link, and add it to the platform, so that I can leverage Telegram's unlimited bandwidth without relying on poorly controlled video hosts.

**Why this priority**: Enables the upload and management of the video asset. Without this, the headless playback in P1 cannot be utilized.

**Independent Test**: Can be fully tested by opening the Admin Dashboard, adding a lesson, selecting "Telegram Direct", and providing the Telegram file link, saving successfully.

**Acceptance Scenarios**:

1. **Given** an admin on the add video page, **When** they paste a direct Telegram file link, **Then** the system accepts and saves the video provider appropriately.

---

### User Story 3 - Redirect Service to Bypass Link Expiration (Priority: P3)

As a system, I need to fetch fresh, unexpired Telegram direct `.mp4` URLs on the fly when a student requests a video, so that long-lived platform links don't break when Telegram cycles its temporary tokens.

**Why this priority**: Direct Telegram links may have limited lifespans. Re-fetching or issuing dynamic 302 Redirects ensures the videos are perpetually playable without manual admin updates.

**Independent Test**: Can be fully tested by simulating a request to the backend microservice endpoint with a video identifier and confirming it returns a `302 Found` response pointing to the raw Telegram CDN stream.

**Acceptance Scenarios**:

1. **Given** a student requests a video stream, **When** the frontend queries the `/api/video/stream-proxy`, **Then** the service fetches the raw URL and redirects the browser's video element directly to the `.mp4` source on Telegram's CDN.

## Functional Requirements *(mandatory)*

### Core Functionality
1. Remove all trace of the deprecated `okru` Video Provider from the UI, Enums, and processing pipelines.
2. The system must support a "telegram" video provider variant (or adapt the existing `telegram` provider) to handle direct streaming.
3. The frontend custom player (`SecureVideoPlayer`) must render an HTML5 `<video>` element natively instead of an iframe when playing Telegram direct streams.
4. The system must proxy the stream through a robust backend or Node.js microservice (`/api/video/stream-proxy`) that intelligently resolves the actual Telegram stream URL and issues a `302 Redirect` to the client.

### Error Handling & Edge Cases
1. **Link Expiration**: The proxy service must be able to gracefully handle or renew the streaming link if Telegram rotates CDN tokens.
2. **Network Failures**: The custom HTML5 player must display a user-friendly error overlay if the stream is temporarily inaccessible.

## Success Criteria *(mandatory)*

- 100% of the academy's video controls (Play, Pause, Timeline, Fullscreen) work flawlessly via a fully custom player.
- The `ok.ru` provider is entirely removed and no longer selectable by admins.
- Video playback draws bandwidth directly from Telegram CDN servers (`cdnX.telesco.pe`), validating that the primary host servers are not bottlenecked.
- Large videos (up to 2GB) stream smoothly without buffering pauses exceeding standard tolerance.

## Key Entities & Roles *(optional)*

- **VideoProvider Enum**: Must be updated to retire OK.ru.
- **LessonVideo**: Entity holding the original URL/Token of the telegram file.

## Assumptions *(optional)*

- Admin will manually generate the initial direct download URL or provide a Telegram Post ID using an external bot before adding it to the platform.
- Telegram's CDN will tolerate cross-origin (CORS) or direct `video src` fetching via redirection.
- `Next.js App Router API` or C# `.NET 9 API` will serve as the redirect microservice.

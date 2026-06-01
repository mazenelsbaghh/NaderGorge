# Feature Specification: Google Drive Custom Player

**Feature Branch**: `047-google-drive-custom-player`  
**Created**: 2026-04-02  
**Status**: Draft  
**Input**: User description: "اشتغل بس عايزوا يشتغل بقي بللاير بتاعنا و السيكور و dom زي اليوتيوب"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Watch Google Drive Videos with Custom Controls (Priority: P1)

Students watch a Google Drive video securely using the academy's customized control player (`PlayerControls.tsx`) with the same UX as YouTube and Telegram videos.

**Why this priority**: The user explicitly requested to unify the user experience and ensure the Google Drive videos look professional and secure by leveraging the system's custom player interface rather than the native Google Drive controls.

**Independent Test**: Can be fully tested by playing a Google Drive video and verifying that the custom play/pause overlay, seek bar, and volume controls work flawlessly, and the native Google Drive UI is completely hidden.

**Acceptance Scenarios**:

1. **Given** a student opens a lesson containing a Google Drive video, **When** the video loads, **Then** it is presented within a standard `<video>` environment managed by the custom `SecureVideoPlayer` and `PlayerControls`.
2. **Given** the student interacts with the video (e.g., clicks play, pauses, seeks), **When** these actions are performed, **Then** the custom controls respond correctly without any cross-origin framing issues.
3. **Given** the student tries to inspect the DOM, **When** they look at the player, **Then** the video is protected within the customized Shadow DOM and IDM shields.

---

### Edge Cases

- What happens when a Google Drive file is too large and Google requires a "virus scan" confirmation? Will the stream proxy handle bypassing this warning to extract the raw stream?
- How does the system handle bandwidth if the server proxies the Google Drive video stream?
- What happens if the stream is interrupted, does the custom player resume gracefully?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST render Google Drive videos using the academy's standardized custom UI (`PlayerControls`).
- **FR-002**: System MUST stream or extract the raw `.mp4` (or video stream) from Google Drive so it can be played inside a standard HTML5 `<video>` tag, avoiding the iframe-based `preview` page which blocks custom controls.
- **FR-003**: System MUST resolve the direct Google Drive media link for the client and use a 302 redirect via the `stream-proxy` route. This ensures the `<video src="...">` points to our API, keeping the Google Drive link out of the HTML payload, while delegating the video bandwidth entirely to Google's servers.
- **FR-004**: System MUST maintain the existing Shadow DOM security measures (anti-download overlays, IDM blocking, dynamic watermarks).
- **FR-005**: System MUST support native video DOM events (play, pause, timeupdate, ended) to synchronize with the `SecureVideoPlayer` state manager.

### Key Entities

- **LessonVideo**: Remains unchanged, uses `Provider = "google_drive"`.
- **Stream Proxy Endpoint**: A dedicated backend or Next.js route that translates a Google Drive File ID into a continuous byte stream for the HTML5 `<video>` tag.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of Google Drive lesson videos play using the custom `PlayerControls.tsx` interface.
- **SC-002**: Playback controls (seek, pause, volume) respond instantly (< 200ms) over the Google Drive stream.
- **SC-003**: No native Google Drive branding or player controls are visible to the student at any point.
- **SC-004**: The Google Drive File ID remains completely obfuscated from the client browser.

## Assumptions

- We assume that the files hosted on Google Drive are set to "Anyone with the link can view".
- We assume that bypassing the Google Drive "virus scan" warning for large files is technically feasible within the streaming proxy logic.
- We assume the server infrastructure can handle the bandwidth if proxying the video stream is chosen as the method to hide the URL.

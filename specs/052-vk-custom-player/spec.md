# Feature Specification: VK Custom YouTube-like Video Player

**Feature Branch**: `052-vk-custom-player`
**Created**: 2026-04-03
**Status**: Draft
**Input**: User description: "VK YouTube-like custom video player using VK Iframe JS API with full playback controls"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Student Watches VK Video with Native Controls (Priority: P1)

A student opens a lesson whose video is hosted on VK. Instead of seeing the default VK player interface (with VK branding, recommendations, and social features), they see a clean, branded player that matches the platform's design — with custom play/pause, seek bar, volume, fullscreen, and speed controls — all controlled programmatically via the VK JS API.

**Why this priority**: This is the core value proposition: replacing the raw VK iframe embed with a fully branded, distraction-free playback experience. All other features depend on this working.

**Independent Test**: Can be tested by embedding a VK video in a lesson page and verifying that custom controls appear and function correctly.

**Acceptance Scenarios**:

1. **Given** a lesson page with a VK-hosted video, **When** the student opens the page, **Then** a custom branded player is displayed with no visible VK social controls (likes, comments, share) in the control bar.
2. **Given** the custom player is loaded, **When** the student clicks the play button, **Then** the video begins playing immediately.
3. **Given** the video is playing, **When** the student clicks pause, **Then** playback stops at the current position.
4. **Given** the video is playing, **When** the student drags the seek bar to a new position, **Then** the video jumps to that time using `seekTime()`.
5. **Given** the custom player, **When** the student adjusts the volume slider, **Then** audio level changes accordingly using `setVolume()`.
6. **Given** the custom player, **When** the student clicks the fullscreen button, **Then** the player expands to fullscreen.
7. **Given** the custom player, **When** the student changes the playback speed (e.g., 1.5×), **Then** the video plays at that speed.

---

### User Story 2 - Playback State Synchronization (Priority: P2)

The custom control bar accurately reflects the current playback state at all times — including progress, buffering, volume, and mute status — so the student always has a clear and accurate view of where they are in the video.

**Why this priority**: Without accurate state synchronization, the custom controls are cosmetic. This ensures the UI and the underlying VK player stay in sync.

**Independent Test**: Can be tested by playing a video to a midpoint, refreshing state via the API event listeners, and verifying that the seek bar and time counter match the video's actual position.

**Acceptance Scenarios**:

1. **Given** the video is playing, **When** the current time updates, **Then** the seek bar and elapsed time indicator move in sync with the video.
2. **Given** the video is buffering, **When** loading stalls, **Then** a loading indicator is displayed until playback resumes.
3. **Given** the player is muted, **When** the student views the volume icon, **Then** the icon reflects the muted state.
4. **Given** the video reaches the end, **When** playback ends, **Then** the player shows a replay option or resets to the beginning.

---

### User Story 3 - Player Integration with Lesson Chapter Navigation (Priority: P3)

The custom VK player integrates with the existing AI-generated chapter system, allowing the student to click a chapter in the chapter list and have `seekTime()` called automatically to jump to that chapter's timestamp.

**Why this priority**: This deepens the educational value of the custom player by enabling seamless chapter-based navigation, which already exists for other providers. VK should achieve parity.

**Independent Test**: Can be tested by loading a lesson with chapters and clicking each chapter entry to verify the video seeks to the correct timestamp.

**Acceptance Scenarios**:

1. **Given** a lesson with AI-generated chapters and a VK-hosted video, **When** the student clicks a chapter in the chapter panel, **Then** the video seeks to that chapter's start time.
2. **Given** the student has sought to a chapter midpoint, **When** the current chapter changes, **Then** the active chapter indicator in the UI highlights the correct chapter.

---

### Edge Cases

- What happens when the VK video URL is invalid or the video is deleted from VK?
- How does the player handle a VK video that is age-gated or region-restricted?
- What happens if the `VK.VideoPlayer` JS API fails to initialize (e.g., network error loading the VK SDK)?
- How does the player behave if the user's browser blocks third-party iframes?
- What happens when the student seeks beyond the buffered portion of the video?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST embed VK videos using the official VK `video_ext.php` URL format with the `js_api=1` parameter to enable programmatic control.
- **FR-002**: The system MUST load the official VK VideoPlayer JS library (`https://vk.com/js/api/videoplayer.js`) to initialize the `VK.VideoPlayer` instance.
- **FR-003**: The player MUST expose custom controls for: play, pause, seek, volume, mute/unmute, fullscreen, and playback speed.
- **FR-004**: The VK iframe MUST be visually hidden or masked so that VK's native control bar is not visible to the student; only the custom controls are presented.
- **FR-005**: The system MUST call `player.play()`, `player.pause()`, `player.seekTime(seconds)`, `player.setVolume(level)`, and `player.setMuted(bool)` in response to user control interactions.
- **FR-006**: The player MUST reflect accurate real-time playback state (current time, duration, buffered, volume, isPlaying) by consuming VK API events.
- **FR-007**: The player MUST display the video title and lesson context (e.g., lesson name) in the player chrome.
- **FR-008**: The system MUST gracefully handle VK API initialization failure by falling back to displaying the raw VK iframe with native controls, together with a user-facing notice.
- **FR-009**: The player MUST support chapter-based seeking: when a chapter timestamp is selected externally (e.g., from the chapter panel), `seekTime()` is called to navigate to it.
- **FR-010**: The admin MUST be able to attach a VK video to a lesson by providing the VK video owner ID (`oid`) and video ID (`id`), which are stored and used to construct the embed URL.

### Key Entities

- **LessonVideo (VK)**: Stores VK-specific identifiers (`oid`, video ID) alongside the generic `provider = "vk"` field. Used to construct the `video_ext.php` embed URL at runtime.
- **VK Player Instance**: A runtime object created from `VK.VideoPlayer(iframeElement)`; manages playback state and exposes control methods and events.
- **Custom Player Control Bar**: A UI component displayed over or beside the hidden VK iframe, housing all playback controls and displaying sync'd state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Students experience zero visible VK branding or social controls during video playback in the lesson player.
- **SC-002**: All custom player controls (play, pause, seek, volume, speed, fullscreen) respond within 300ms of user interaction.
- **SC-003**: The seek bar and time counters stay synchronized with actual video playback position within ±1 second at all times.
- **SC-004**: 100% of chapter navigation clicks result in the video seeking to within ±2 seconds of the intended chapter timestamp.
- **SC-005**: The player gracefully degrades to native VK controls in 100% of cases where the JS API fails to initialize, with no blank/broken screen shown to students.
- **SC-006**: The custom VK player achieves visual and behavioral parity with the existing custom players for other supported providers (Telegram, Google Drive, Rutube).

## Assumptions

- The existing `LessonVideo` entity in the backend already supports `provider = "vk"` (introduced in feature 048-vk-video-provider); this feature focuses solely on the custom frontend player experience.
- The VK SDK (`videoplayer.js`) is loaded at runtime via a `<script>` tag; no npm package is required unless a suitable typed wrapper is identified.
- The `oid` (owner ID) and video ID values stored in the database are sufficient to construct the VK `video_ext.php` embed URL without additional API calls.
- Playback speed control availability depends on VK's API support; if unavailable, this control will be hidden rather than shown as broken.
- The custom player will follow the same dark-themed, branded design language as the existing custom players in the codebase (feature 033-custom-video-player patterns).
- Mobile responsiveness is in scope and should follow the responsive breakpoints already established in the project.
- DRM or download protection for VK content is out of scope for this feature; VK manages its own content protection.

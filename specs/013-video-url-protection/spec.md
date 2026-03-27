# Feature Specification: Video URL Protection

**Feature Branch**: `013-video-url-protection`  
**Created**: 2026-03-26  
**Status**: Draft  
**Input**: User description: "لينك الفيديو سهل يتجاب من الـ DevTools — المفروض مايبانش"

## User Scenarios & Testing

### User Story 1 — Student Cannot Extract Video URL (Priority: P1)

A student opens a lesson page containing a video. The video plays normally within the platform. The student opens the browser DevTools (Inspect Element). **The raw video hosting URL is not visible** in the DOM, network tab requests, or iframe attributes in a way that allows direct access outside the platform.

**Why this priority**: This is the core problem. If the video URL is extractable, paid content can be downloaded and redistributed, directly undermining the business model.

**Independent Test**: Open any lesson with a video, use DevTools to search for the video hosting URL — it must not appear in cleartext or be usable outside the platform.

**Acceptance Scenarios**:

1. **Given** a student is watching a lesson video, **When** they inspect the page DOM, **Then** no directly usable video URL is visible in any iframe `src`, `<video>` tag, or embedded element.
2. **Given** a student monitors the Network tab, **When** the video loads, **Then** the requests do not reveal a URL that can be opened in a new tab to view/download the video without authentication.
3. **Given** a student copies any URL fragment found in DevTools, **When** they paste it into a new browser tab, **Then** the video does not play (access is denied or the URL has expired).

---

### User Story 2 — Admin Uploads Video Securely (Priority: P2)

When an admin adds a video to a lesson, the system stores the video reference securely so that video content is served through a protected channel rather than a publicly accessible direct link.

**Why this priority**: Without secure storage of video references, any protection on the frontend can be bypassed by accessing the raw storage URL.

**Independent Test**: After uploading a video, verify the stored reference is not a publicly accessible URL. Attempting to access the stored reference directly should fail.

**Acceptance Scenarios**:

1. **Given** an admin uploads a video to a lesson, **When** the video is saved, **Then** the system stores a non-public reference (not a direct public URL).
2. **Given** a stored video reference, **When** someone attempts to access it directly, **Then** access is denied unless the request comes through the authenticated platform.

---

### User Story 3 — Seamless Playback Experience (Priority: P2)

Despite the URL protection, the student's video playback experience remains smooth, with no additional login prompts, loading delays, or playback quality reduction.

**Why this priority**: Security measures must not degrade the user experience. Students should not notice any difference in how videos play.

**Independent Test**: Play a video as an enrolled student — it should start within 3 seconds, play without buffering issues, and require no extra steps compared to the current experience.

**Acceptance Scenarios**:

1. **Given** an enrolled student opens a lesson, **When** the video loads, **Then** playback starts within 3 seconds with no extra authentication prompts.
2. **Given** a video is protected, **When** the student watches it, **Then** video quality is the same as before protection was added.
3. **Given** a student navigates between lessons, **When** each video loads, **Then** there is no noticeable delay compared to unprotected videos.

---

### Edge Cases

- What happens when a video URL token/session expires mid-playback? (The system should transparently refresh without interrupting the student.)
- How does the system handle students using browser extensions that intercept network requests? (The system should make extraction significantly harder but does not need to defeat every possible tool — reasonable deterrence is the goal.)
- What happens if the video hosting service is unavailable? (Graceful error message is shown to the student.)

## Requirements

### Functional Requirements

- **FR-001**: System MUST NOT expose raw video hosting URLs in the page source, DOM, or iframe attributes visible to the student.
- **FR-002**: System MUST serve video content through a time-limited, authenticated delivery mechanism. Direct URL access without valid authentication MUST fail.
- **FR-003**: System MUST restrict video playback to the platform's domain — embedding the video player on external sites MUST NOT work.
- **FR-004**: System MUST maintain existing video playback quality and performance (start within 3 seconds for enrolled students).
- **FR-005**: System MUST support the existing admin video upload workflow without requiring manual URL obfuscation steps.
- **FR-006**: System MUST log or detect repeated failed attempts to access video URLs directly (basic abuse detection).

## Success Criteria

### Measurable Outcomes

- **SC-001**: 0% of video URLs are extractable via standard browser DevTools inspection (DOM, Network tab, Console).
- **SC-002**: Protected videos start playback within 3 seconds for enrolled students (no degradation from current experience).
- **SC-003**: Any extracted URL fragment expires and becomes unusable within 5 minutes.
- **SC-004**: Video playback is restricted to the platform domain — attempts to embed on external sites fail 100% of the time.
- **SC-005**: Admin video upload workflow requires 0 additional steps compared to current process.

## Assumptions

- The current video hosting is YouTube (based on the iframe embed seen in DevTools). The protection strategy will depend on the hosting platform's capabilities.
- "Reasonable deterrence" is the goal — the system should prevent casual extraction via DevTools. Defeating advanced screen recording or stream-ripping tools is out of scope.
- The platform already has student authentication in place, which can be leveraged for video access control.

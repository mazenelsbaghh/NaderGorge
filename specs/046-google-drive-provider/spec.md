# Feature Specification: Google Drive Video Provider

**Feature Branch**: `046-google-drive-provider`
**Created**: 2026-04-02
**Status**: Draft
**Input**: User description: "Google Drive video provider — admins upload lesson videos to Google Drive, store the file ID securely in the database, and students watch them through our embedded player with session-based access control. The Google Drive file ID must never be exposed to the student's browser."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Admin Registers a Google Drive Video for a Lesson (Priority: P1)

An admin or content manager uploads a lesson video to Google Drive, then registers it on the platform by entering the Google Drive File ID in the lesson editing form. The system stores the ID securely and associates it with the lesson.

**Why this priority**: Without this, no Google Drive videos can appear on the platform. All subsequent stories depend on it.

**Independent Test**: An admin can open a lesson's edit form, choose "Google Drive" as the video provider, paste a File ID, save the lesson, and see it persisted with no errors.

**Acceptance Scenarios**:

1. **Given** the admin is on the lesson edit page, **When** they select "Google Drive" as the video provider and enter a valid File ID, **Then** the lesson is saved successfully and the provider is stored as `google_drive`.
2. **Given** the admin submits an empty File ID, **When** the save action is triggered, **Then** a validation error is shown and the lesson is not saved.
3. **Given** a lesson already has a Telegram video, **When** the admin switches the provider to Google Drive and saves, **Then** the new provider overrides the old one.

---

### User Story 2 — Student Watches a Google Drive Video (Priority: P1)

An enrolled student opens a lesson page. The platform verifies their session and enrollment, then serves a secure embedded video player backed by Google Drive — without leaking the underlying File ID or any direct Google Drive URL to the student's browser.

**Why this priority**: Core student experience. Equal priority to P1 because both admin input and student playback must work for the feature to be useful.

**Independent Test**: An enrolled student can navigate to a lesson with a Google Drive video and watch it play smoothly in the embedded player with no Google Drive URLs visible in the browser DOM.

**Acceptance Scenarios**:

1. **Given** a student has an active session and access to a lesson, **When** they open the lesson page, **Then** the video player loads and begins playback within 5 seconds without any Google Drive File ID appearing in the DOM or network requests visible to the student.
2. **Given** a student's session has expired, **When** they try to access the embed endpoint, **Then** they receive an access-denied response and the video does not load.
3. **Given** a student is not enrolled in the course, **When** they request the video embed, **Then** they are blocked with an appropriate error.
4. **Given** the lesson has a Google Drive video, **When** the student inspects the page's DOM (including Shadow DOM), **Then** the File ID is not visible in any attribute or URL in plain text.

---

### User Story 3 — Student Cannot Extract or Share the Direct Google Drive Link (Priority: P2)

Even a technically sophisticated student who inspects network traffic or page source should not be able to reconstruct a direct, reusable Google Drive link to share the video with unenrolled users.

**Why this priority**: Privacy and content protection are important for the business model but secondary to core playback functionality.

**Independent Test**: A student who copies any URL from the network tab finds that it requires a valid, non-transferable session token to load.

**Acceptance Scenarios**:

1. **Given** a student copies the embed endpoint URL, **When** they open it in an incognito window without a valid session, **Then** they receive an access-denied response.
2. **Given** a student inspects the page source or Shadow DOM, **When** they look for Google Drive identifiers, **Then** no plain-text File ID or drive.google.com file URL is present.

---

### Edge Cases

- What happens when the Google Drive file is deleted or its sharing permissions are revoked after registration?
- What happens if the admin accidentally pastes a full Google Drive URL instead of just the File ID? (System should extract the ID automatically or show a helpful error.)
- What happens if a student's device does not support the embedded Drive player (e.g., very old browser with iframe restrictions)?
- What if multiple students access the same lesson simultaneously — does the system create one session per user properly?

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admins MUST be able to select "Google Drive" as the video provider when creating or editing a lesson video.
- **FR-002**: Admins MUST be able to input a Google Drive File ID; the system MUST also accept and auto-extract the ID from a full Google Drive shareable URL.
- **FR-003**: The system MUST store the Google Drive File ID in the database in a way that is not directly accessible to unauthenticated or unenrolled users.
- **FR-004**: When a student requests a lesson's video embed, the system MUST validate the student's session and enrollment before returning any video content.
- **FR-005**: The video embed response MUST hide the Google Drive File ID from the student's browser using a Shadow DOM or equivalent isolation technique.
- **FR-006**: The embed URL provided to the student's player MUST use a short-lived encrypted session token — not the raw File ID.
- **FR-007**: The system MUST serve video bandwidth directly from Google's infrastructure; the platform's servers MUST NOT proxy the video byte stream.
- **FR-008**: Admins MUST be able to verify a Drive video is accessible (e.g., a preview check) before publishing the lesson.
- **FR-009**: The platform MUST support "google_drive" alongside existing providers ("telegram", "youtube") without breaking existing lessons.
- **FR-010**: If a Google Drive file becomes unavailable (deleted, permissions revoked), the player MUST display a friendly error message to the student instead of a broken iframe.

### Key Entities

- **LessonVideo**: Represents a video attached to a lesson. Gains a new provider value `google_drive` and stores a `google_drive_file_id` (encrypted at rest).
- **VideoPlaybackSession**: Short-lived token issued per student per lesson view. Used to authenticate the embed request; already exists and will be reused.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Students can start watching a Google Drive lesson video within 5 seconds of opening the lesson page on a standard connection.
- **SC-002**: Zero Google Drive File IDs or direct drive.google.com URLs appear in the student's browser DOM, page source, or visible network requests.
- **SC-003**: 100% of embed requests without a valid student session are rejected before any Drive content is returned.
- **SC-004**: Admins can register a new Google Drive video for a lesson in under 2 minutes from the platform's admin panel.
- **SC-005**: Switching a lesson's provider between Telegram, YouTube, and Google Drive does not break any existing lessons on the other providers.
- **SC-006**: The Google Drive video player renders and plays correctly on Chrome, Firefox, and Safari on both desktop and mobile.

---

## Assumptions

- Google Drive files shared with "Anyone with the link — Viewer" will be served by Google's CDN; no Google API credentials are required by the platform server for playback.
- Admins are responsible for setting the correct sharing permissions on the Google Drive file before registering it on the platform.
- The existing session validation and encryption infrastructure (VideoPlaybackSession, AES-256-GCM token) will be reused without modification.
- The embedded Google Drive player may show a minimal Google UI header; this is acceptable for v1.
- Mobile support is in scope; the Drive embedded player is responsive by default.
- Downloading from within the Drive embed is disabled via the embed URL parameters (`rm=minimal`).
- AI chaptering and mindmap generation remain scoped to Telegram and YouTube videos only in this feature; Google Drive videos are playback-only for v1.

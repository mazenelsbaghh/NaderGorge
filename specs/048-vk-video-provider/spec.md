# Feature Specification: VK.com Video Provider

**Feature Branch**: `048-vk-video-provider`  
**Created**: 2026-04-02  
**Status**: Draft  
**Input**: User description: "VK.com وخليه يشتغل بالبلاير بتاعنا و شيل الدرايف و شيل التلجيرام"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Watch VK.com Videos with Custom Player (Priority: P1)

Students watch lesson videos securely delivered from VK.com servers, using the academy's customized control player (`PlayerControls.tsx`) and progress tracking overlay.

**Why this priority**: Migrating to VK.com is the primary objective to offload video streaming bandwidth from the server while avoiding restrictive constraints from YouTube or proxy payloads. It's the core functionality.

**Independent Test**: Can be fully tested by providing a VK `oid`, `id`, and `hash` to a lesson, playing the video, verifying that the actual media originates from vk.com, the native VK player controls are overridden/hidden, and the custom player accurately tracks progress.

**Acceptance Scenarios**:

1. **Given** a student opens a lesson containing a VK.com video link, **When** the media is embedded, **Then** the custom player UI renders over the VK embed, establishing a communication bridge with the VK Iframe API.
2. **Given** a student watches a VK video, **When** the video plays, **Then** the custom progress bar increments in parallel with the VK `currentTime` events.

---

### User Story 2 - Remove Google Drive Video Proxy Mechanisms (Priority: P2)

Administrators and the System securely remove all Google Drive specific proxy code, database constraints, and routing meant to bypass Drive headers.

**Why this priority**: To clean up tech debt and unnecessary server load logic since Google Drive is being deprecated as a video provider immediately.

**Independent Test**: Can be fully tested by completely removing `/api/video/drive-proxy` and attempting to compile the system. Search the codebase for `google_drive` and remove conditional handlers.

**Acceptance Scenarios**:

1. **Given** the new VK architecture, **When** developers clean the codebase, **Then** all legacy Google Drive byte-range stream proxy files are deleted.

---

### User Story 3 - Remove Telegram Local Proxy Mechanisms (Priority: P3)

Administrators and the System remove all Telegram Bot API video streaming endpoints from the backend/nginx rules meant to pipe Telegram streams.

**Why this priority**: To clean up tech debt and reduce server bandwidth requirements to pure zero, as Telegram streaming uses VPS traffic.

**Independent Test**: Can be tested by searching for the "telegram" provider references in the Lesson Video codebase and Nginx proxy rules and safely removing them.

**Acceptance Scenarios**:

1. **Given** the new VK architecture, **When** developers clean the codebase, **Then** all Telegram video scraping and proxy paths are cleanly removed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept VK embed parameters or parsed VK embed links as valid `VideoUrl` entries for the `LessonVideo` entity.
- **FR-002**: System MUST render lessons using a VK `iframe` containing the proprietary VK player (hiding native controls if possible) within the Secure Shadow DOM.
- **FR-003**: System MUST utilize the VK Javascript Iframe API (`window.postMessage`) to bi-directionally communicate state (play, pause, current time) to the `SecureVideoPlayer.tsx`.
- **FR-004**: System MUST successfully remove all traces of `Google Drive` and `Telegram` proxy logic and database provider enumerations.

### Key Entities

- **LessonVideo**: Needs to be updated to recognize `"vk"` as the required/supported Enum string, and deprecate `"google_drive"` and `"telegram"`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of newly uploaded videos run through the VK network using 0% of local server streaming bandwidth.
- **SC-002**: Academy's custom player interface (`PlayerControls.tsx`) successfully captures time updates from the VK iframe accurately tracking lesson watch progression.
- **SC-003**: All references, code paths, and configurations pertaining exclusively to Telegram and Google Drive video delivery are eradicated from the repository without breaking legacy videos (if legacy videos are to be manually re-uploaded).

## Assumptions

- We assume the user has existing VK.com accounts and intends to upload all past and future videos there.
- We assume that VK.com Iframe APIs are fully supported across all major browser clients without CORS issues.
- We assume the user is willing to manually overwrite or re-upload previous Telegram/Drive lessons to VK, since changing the provider string without migrating the files will result in dead DB entries.

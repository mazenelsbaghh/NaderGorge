# Feature Specification: Rutube Video Provider Integration

**Feature Branch**: `049-rutube-video-provider`  
**Created**: 2026-04-02  
**Status**: Draft  
**Input**: User description: "شيل vk وخليه Rutube (روتيوب): المنصة الروسية البديلة ليوتيوب، تتميز بمرونة عالية في رفع الفيديوهات دون قيود صارمة للحقوق. تمتلك Player API محترم يدعم التخاطب عبر الـ postMessage ويمكننا بسهولة قراءة الوقت ومسار الفيديو وإخفاء الأزرار لدمجه في مشغلك التفاعلي بحماية تامة."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Watch Course Videos via Rutube (Priority: P1)

As a student, I want to watch my lessons seamlessly through the academy's secure custom video player while the actual video is hosted and streamed efficiently from Rutube.

**Why this priority**: Core streaming mechanism. Replacing VK with Rutube directly affects all new video content delivery. Rutube provides the necessary bandwidth and flexibility to bypass strict copyright takedowns.

**Independent Test**: Can be fully tested by creating a lesson video with a Rutube embed URL or ID, opening the student lesson viewer, and validating that the video buffers and plays inside the custom overlay without exposing Rutube's native branding or downloading links.

**Acceptance Scenarios**:

1. **Given** a student is on a lesson page with a Rutube-hosted video, **When** they click the custom play button, **Then** the Rutube iframe receives a play command via `postMessage` and begins playback.
2. **Given** the video is playing, **When** the Rutube player progresses, **Then** it broadcasts `timeupdate` `postMessage` events which are captured by the custom player to track view metrics correctly.

---

### User Story 2 - Add/Manage Rutube Videos in Admin Panel (Priority: P2)

As an administrator, I want to be able to select "Rutube" as the video provider and enter Rutube links or IDs so that I can easily attach new videos to lessons.

**Why this priority**: Necessary for content ingestion.

**Independent Test**: Can be tested by navigating to the Content management page, adding a video, selecting Rutube from the provider dropdown, and seeing it correctly saved and displayed as a Rutube provider.

**Acceptance Scenarios**:

1. **Given** the administrator is adding a video, **When** they open the provider dropdown, **Then** they see "روتيوب (Rutube.ru)" instead of VK.
2. **Given** the admin enters a generic rutube URL, **When** the input is parsed, **Then** the system auto-selects Rutube as the provider.

---

### Edge Cases

- What happens when a user uses an adblocker that strictly blocks Russian domains like rutube.ru?
- How does the system handle rapid seeking on Rutube which might delay the `timeupdate` event?
- What happens to existing videos that were recorded with `vk` as a provider string?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support `rutube` as a valid provider enum/string in the backend database.
- **FR-002**: System MUST render an iframe utilizing Rutube's embed structure in the `embed` API proxy route.
- **FR-003**: System MUST attach the necessary URL query parameters for Rutube embeds to ensure JS API availability.
- **FR-004**: System MUST intercept Rutube's `postMessage` protocol (which might differ from VK/YouTube) and translate these events into the standard custom `video-embed` payload used by `SecureVideoPlayer.tsx`.
- **FR-005**: System MUST send appropriate JSON `postMessage` commands to the Rutube iframe (e.g., `play`, `pause`, `setCurrentTime`) to drive playback.
- **FR-006**: System MUST remove/hide or replace the VK options in the administrator dashboard with Rutube variants.

### Key Entities

- **LessonVideo**: Needs to accept `"rutube"` as a valid `Provider` option.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of newly added Rutube videos successfully stream and track progress on the student end.
- **SC-002**: Rutube integration covers play, pause, seek, and volume adjustments identically to the existing YouTube implementation.
- **SC-003**: Video player completely masks native Rutube branding and controls safely under an anti-theft Z-index wrapper.

## Assumptions

- We assume no legacy data migration is specifically requested for VK videos at this exact moment (they might fail to play or will manually be re-uploaded to Rutube).
- Rutube's Iframe API allows standard volume and tracking manipulation (which is standard according to documentation).
- Rutube does not aggressively rate-limit iframe `postMessage` dispatches.

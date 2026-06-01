# Feature Specification: YouTube-like player chapters

**Feature Branch**: `042-video-player-chapters`
**Created**: 2026-04-01
**Status**: Draft
**Input**: User description: "عايز القيصر الفصول تظهر علي الفيديو بلار بتاعنا شبه اليوتيوب https://support.google.com/youtube/answer/9884579?hl=en"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Segmented Progress Bar (Priority: P1)

As a student watching a lesson video, I want to see the progress bar divided into distinct segments representing chapters, so I can visually grasp the structure of the video and easily navigate between main topics.

**Why this priority**: The segmented progress bar is the core visual indicator of chapters, essential for replicating the requested YouTube-like experience.

**Independent Test**: Can be fully tested by playing a video with chapters; the progress bar should visually split at the exact timestamps of each chapter boundary.

**Acceptance Scenarios**:

1. **Given** a video loaded with chapter data, **When** the player renders, **Then** the timeline scrubber bar displays visible gaps/markers at each chapter's start time forming clickable segments.
2. **Given** a video without chapters, **When** the player renders, **Then** the timeline scrubber remains a single continuous bar.

---

### User Story 2 - Chapter Hover Preview (Priority: P1)

As a student hovering over the video's progress bar, I want to see the title of the chapter I am hovering over, so I know what content to expect if I seek to that point.

**Why this priority**: Immediate feedback during seeking is crucial for navigation.

**Independent Test**: Can be fully tested by mousing over different segments of the video progress bar and verifying the tooltip/overlay text matches the chapter title.

**Acceptance Scenarios**:

1. **Given** a video with chapters, **When** the user hovers their cursor over a specific segment on the progress bar, **Then** a tooltip or text label appears displaying the corresponding chapter's title.
2. **Given** a running video, **When** the user clicks the segment, **Then** the video seeks exactly to the chapter's start time bounding that segment.

---

### User Story 3 - Interactive Chapter Tracking (Priority: P2)

As a student, I want to see the current chapter name displayed persistently or semi-persistently in the player controls (e.g., next to the timestamp), so I always know which section I am currently studying.

**Why this priority**: Enhances educational context so the student doesn't lose track of what topic is currently playing.

**Independent Test**: Can be tested by seeking to different parts of the video and ensuring a UI element representing the "Current Chapter" updates accurately.

**Acceptance Scenarios**:

1. **Given** a playing video, **When** the video plays naturally across a chapter boundary, **Then** the "Current Chapter" UI label updates automatically to the new chapter's title.

### Edge Cases

- What happens when a video has only one chapter starting at `00:00:00`? (Should render as standard video without gaps).
- How does the system handle extremely short chapters (e.g., under 5 seconds)? (Segments on the progress bar shouldn't overlap visually).
- What happens if chapter data fails to load from the API? (Player must gracefully fallback to a standard continuous progress bar without crashing).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST inject chapter metadata (title, start time, end time) into the video player component upon initialization.
- **FR-002**: System MUST render visual dividers (gaps) on the video progress bar corresponding to chapter timestamps, turning the bar into isolated segments.
- **FR-003**: System MUST display the chapter title as a tooltip when hovering over a specific segment of the progress bar.
- **FR-004**: System MUST track the current playback time and identify the active chapter.
- **FR-005**: System MUST update the UI to reflect the active chapter title during playback in the control bar.
- **FR-006**: System MUST allow users to click exactly on a chapter segment to seek to that chapter's starting timestamp, or within the chapter to seek to that explicit point.

### Key Entities

- **VideoChapter**: Represents a logical segment of a video. Key attributes: Chapter Title, Start Timestamp, End Timestamp.
- **VideoPlayer**: The frontend video rendering component responsible for UI overlays and timeline manipulation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of videos possessing valid chapter data in the database display a segmented progress bar.
- **SC-002**: Hovering over the progress bar displays the chapter title instantly with no visual delay.
- **SC-003**: The player UI successfully handles edge cases (like missing data or very short chapters) without throwing frontend exceptions.
- **SC-004**: Students can click on any chapter segment to immediately seek to that point in the video.

## Assumptions

- We are modifying the existing custom video player component (`VideoPlayer` in the frontend app), not a standard browser `<video>` element fallback or a locked iframe that restricts custom UI overlays.
- Chapter visual thumbnails (preview images on hover) are out of scope for the MVP due to the latency and storage costs of generating seek-preview thumbnails; the MVP will rely on text-based chapter titles on hover.
- The `LessonCockpitQuery` API already provides the `VideoChapters` data to the frontend, so no major backend adjustments are scoped for this feature.

# Feature Specification: Dynamic Video Watermark

**Feature Branch**: `036-dynamic-video-watermark`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "عايز احط dynamic watermark علي البلار اللي احنا عاملينوا يبقي في حته و يبقي حته تانيه كل فتره كده يكون باسمو و تحتوا رقم تلفونوا"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Deterring Screen Recording (Priority: P1)

As an instructor, I want a dynamic watermark displaying the student's name and phone number to overlay the video player, so that any leaked screen recordings can be traced back to the specific student.

**Why this priority**: Core objective. Intellectual property protection is vital for the platform's business model.

**Independent Test**: Can be tested by playing any video as a logged-in student and observing the watermark moving across the player without breaking the playback experience.

**Acceptance Scenarios**:

1. **Given** a student is watching a video, **When** the video is playing, **Then** a semi-transparent watermark should appear on top of the video displaying their name and phone number.
2. **Given** the watermark is visible, **When** a specific time interval passes, **Then** the watermark should randomly change its position within the video frame boundaries.
3. **Given** the watermark is displayed, **When** the student interacts with the video (pause, seek, volume), **Then** the watermark must not capture clicks or prevent the interaction from working.

---

### Edge Cases

- What happens if the student's name is extremely long?
- How does the system handle responsive design on very small mobile screens? Does the watermark scale smoothly or risk overlapping the entire screen?
- What happens if the student doesn't have a phone number registered?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a semi-transparent text-based watermark over the video player during playback.
- **FR-002**: System MUST include the currently authenticated student's full name and phone number in the watermark text.
- **FR-003**: System MUST automatically reposition the watermark to a random coordinate within the player boundaries at periodic intervals (e.g., every 10-15 seconds).
- **FR-004**: System MUST ensure the watermark remains strictly within the visual bounds of the video player without clipping outside.
- **FR-005**: System MUST ensure the watermark is visually distinguishable against both dark and light video backgrounds (e.g., using text shadows or outlines).
- **FR-006**: System MUST ensure the watermark does not block pointer events (clicks) so that users can interact with player controls beneath it.

### Key Entities

- **Student Identity**: Source for the Name and Phone Number data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Watermark is visible and legible on 100% of video playbacks for authenticated users.
- **SC-002**: Watermark dynamically changes position at least 4-6 times per minute.
- **SC-003**: Implementation has zero negative impact on video playback performance (no dropped frames or lag).
- **SC-004**: Zero user reports of inability to click video controls due to watermark obstruction.

## Assumptions

- The video player frontend architecture allows for absolute positioning of overlay elements over the secure video iframe.
- The student's phone number is mandatory during registration and is always available in the user session/claims.
- A 10-15 second randomized transition interval is a reasonable default for "every once in a while".

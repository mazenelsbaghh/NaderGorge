# Feature Specification: Remove Bunny and Telegram Video Providers

**Feature Branch**: `071-remove-bunny-telegram-providers`  
**Created**: 2026-06-03  
**Status**: Draft  
**Input**: User description: "Remove Bunny and Telegram completely now from everything and everywhere and verify they are removed, leaving only VK and YouTube."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Watch Lesson Video (Priority: P1)

As a Student, I want to watch a lesson video using only YouTube or VK, so that I can study course content without encountering playback or loading errors from unsupported providers.

**Why this priority**: Watching videos is the core feature of the platform. Restricting to YouTube and VK ensures students do not encounter playback failures from deleted or unsupported providers.

**Independent Test**: Can be tested by navigating to a lesson containing YouTube or VK videos, playing the video, and ensuring playback works properly.

**Acceptance Scenarios**:

1. **Given** a student is logged in and has access to a package, **When** they open a lesson with a YouTube video, **Then** the video loads in the player and plays correctly.
2. **Given** a student is logged in and has access to a package, **When** they open a lesson with a VK video, **Then** the video loads in the player and plays correctly.

---

### User Story 2 - Admin Video Management (Priority: P2)

As an Admin, I want to add or update lesson videos selecting only VK or YouTube as providers, so that I cannot configure invalid providers.

**Why this priority**: Preventing configuration errors at the admin level prevents issues from reaching students.

**Independent Test**: Can be tested by opening the Admin Content Management page, creating or updating a video, and checking that the provider dropdown only offers "YouTube" and "VK".

**Acceptance Scenarios**:

1. **Given** an admin is creating a new lesson video, **When** they open the provider selection dropdown, **Then** only "YouTube" and "VK" are available as options.
2. **Given** an admin is editing an existing lesson video, **When** they change the provider, **Then** they can only select "YouTube" or "VK".

---

### Edge Cases

- **Existing Bunny Videos in Database**: What happens if there are existing videos in the database configured with the `bunny` provider?
  - *Resolution*: A migration or seed check must update any existing `bunny` or `telegram` video provider fields in the database to a valid provider (e.g., `youtube` or `vk`, or blank/default) so they do not cause 500 errors on load.
- **Unsupported provider requests**: What happens if a client attempts to create a video session with a provider other than `youtube` or `vk`?
  - *Resolution*: The backend must reject the request with a clean error response instead of throwing unhandled exceptions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support `youtube` and `vk` as the only valid video providers.
- **FR-002**: The backend API MUST reject video session creation requests if the video provider is not `youtube` or `vk`.
- **FR-003**: The admin video creation and edit forms MUST only allow selecting `youtube` and `vk` as providers.
- **FR-004**: Any existing videos in the database using the `bunny` or `telegram` provider MUST be migrated or updated to prevent application errors.
- **FR-005**: All references, routes, services, and endpoints specifically dedicated to `telegram` streaming/proxying or `bunny` stream embeds MUST be removed or cleaned up.

### Key Entities

- **LessonVideo**: Represents the video details.
  - `Provider`: String field. Must only contain "youtube" or "vk" (case-insensitive checks or normalization).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The admin interface only lists "YouTube" and "VK" in the video provider dropdown list (100% compliance).
- **SC-002**: Attempting to watch a video with a valid provider (YouTube or VK) works successfully without server errors (100% success rate).
- **SC-003**: Attempting to query or create a session for any video with an invalid provider returns a validation or bad request response instead of a 500 internal server error.

## Assumptions

- We assume existing videos in the database that were configured with `bunny` can be migrated to `youtube` or another valid provider (e.g. updating the "بانييي" video to YouTube).
- VK integration is already fully implemented and working.
- No other video providers (like Rutube, Google Drive, OK, etc.) are active or requested to remain at this moment. The active set is strictly restricted to VK and YouTube.

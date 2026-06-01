# Feature Specification: Odnoklassniki (ok.ru) Video Integration

## 1. Description
This feature enables the integration of videos hosted on Odnoklassniki (ok.ru) into the Nader Gorge educational platform. It aims to support `ok.ru` as a recognized video provider, allowing administrators to add lessons using ok.ru video links. The system will correctly parse these links, securely embed the videos using a custom proxy player (to protect against direct downloads/theft), and synchronize playback controls, state tracking, and view count constraints with the existing `SecureVideoPlayer` component.

## 2. User Scenarios & Testing

### User Scenarios
- **Scenario 1: Adding a new ok.ru lesson (Admin)**: An administrator creates a new lesson and provides a link to a video hosted on ok.ru. The system parses the link, identifies it as an ok.ru video, and successfully stores it in the database with the correct provider flag.
- **Scenario 2: Watching an ok.ru lesson (Student)**: A student navigates to a lesson containing an ok.ru video. The platform loads the video inside the custom `SecureVideoPlayer`, completely hiding ok.ru's native player controls.
- **Scenario 3: Controlling playback (Student)**: A student clicks the custom "Play", "Pause", or "Seek" buttons on the platform's video player. The ok.ru video responds smoothly to these controls, and when paused, the custom overlay appears instead of ok.ru's native paused state.
- **Scenario 4: View limits tracking (System)**: As a student watches the ok.ru video, the system accurately tracks their watched time and increments their view count correctly, ensuring view limits are enforced perfectly.

### Testing Scenarios
- Validate ok.ru URL parsing capabilities for various link formats (e.g., standard links, mobile links, embed links).
- Verify that the frontend `SecureVideoPlayer` successfully proxies the ok.ru embed without exposing the direct URL.
- Test that custom player commands (play, pause, seek, volume, mute) properly interface with the ok.ru iframe via cross-origin postMessage bridging.
- Ensure ok.ru's native branding and controls (such as the timeline, play buttons, and overlays) are completely masked or hidden by the platform's custom overlays.

## 3. Functional Requirements
- **FR1 URL Parsing**: The backend must parse and normalize `ok.ru` video URLs to extract the required video ID.
- **FR2 Database Provider String**: The `provider` field for a `LessonVideo` must accommodate the `"okru"` identifier.
- **FR3 Embed Proxy**: The frontend proxy endpoint (`/api/video/embed/route.ts`) must generate the correct HTML shell for embedding ok.ru videos securely inside `SecureVideoPlayer`.
- **FR4 Message Bridge**: The `SecureVideoPlayer` must communicate bidirectionally with the ok.ru player API (often via `postMessage`) to execute play, pause, seek, and volume commands and receive playback timing updates.
- **FR5 UI Masking**: The wrapper must securely mask ok.ru's native bottom timeline, top branding, and central pause UI, replacing them with the Nader Gorge branded UI overlay.
- **FR6 Playback Tracking**: The bridge must regularly sync the video's current playback time, duration, and playing status to allow the existing student progress tracking features to function properly.

## 4. Success Criteria
- **Functional Outcomes**: Administrators can add ok.ru videos seamlessly.
- **Technical Excellence**: Students cannot directly interact with the underlying iframe container or copy the ok.ru link easily. Playing ok.ru videos offers an identical aesthetic experience as YouTube or Rutube integration.
- **User Satisfaction**: The user reports that clicking the custom Pause button legitimately pauses the ok.ru player, without native player controls flashing unexpectedly.

## 5. Assumptions & Dependencies
- **Assumptions**: 
  - The ok.ru player supports an HTML5 `postMessage` based Javascript API for remotely controlling the iframe.
  - The ok.ru iframe supports cross-origin interactions.
- **Dependencies**: 
  - Depends on `LessonVideo` backend parsing schema and `LessonCarousel.tsx` for playback rendering.

## 6. Key Entities (Optional)
- **LessonVideo**: Needs configuration and mapping checks for standard video tracking.

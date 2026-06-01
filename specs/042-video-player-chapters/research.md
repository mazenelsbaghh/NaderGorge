# Research: YouTube-like player chapters

## Technical Context Resolution

- **Language/Version**: TypeScript (Next.js 16.2.1 / React 19)
- **Primary Dependencies**: `framer-motion` (for tooltips and smooth playhead movement), `lucide-react` (for icons if needed), existing custom video components (`SecureVideoPlayer.tsx`, `PlayerControls.tsx`, `InteractiveTimeline.tsx`).
- **Target Platform**: Web browsers (Desktop and Mobile)
- **Project Type**: Web Application Frontend Feature
- **Performance Goals**: Tooltips and chapter seeking must respond with <100ms latency without interrupting the video playback stream.
- **Constraints**: No raw DOM manipulation that bypasses React/Framer Motion state, except where explicitly required by the video embedded postMessage API.

## Design Decisions

### Decision 1: Progress Bar Implementation
- **Decision**: Extend the existing `InteractiveTimeline.tsx` component to handle current chapter display, rather than installing a 3rd party video player package (like video.js).
- **Rationale**: `SecureVideoPlayer.tsx` relies heavily on an IFrame abstraction (`/api/video/embed`) communicating via `postMessage` to hide the YouTube URL and deter piracy. Switching to a standard video player would break this critical security architecture. The `InteractiveTimeline` is already partially wired to accept `chapters` and render gaps.
- **Alternatives considered**: Using `react-player` or `video.js` plugins, which was rejected due to incompatibility with the custom iframe embedding strategy.

### Decision 2: Current Chapter UI Sync
- **Decision**: Calculate the "active chapter" based on `currentTime` within `PlayerControls.tsx` and display the chapter title prominently in the Control Bar next to the current timestamp.
- **Rationale**: User Story 3 requires the current chapter to be visible permanently or semi-permanently, not just on hover. `PlayerControls.tsx` already tracks `currentTime` and has access to `chapters` props, making it the ideal component to perform this derived state calculation and render the label.
- **Alternatives considered**: Passing the active chapter state all the way up to `LessonCarousel.tsx`, which was rejected as it unnecessarily inflates the render tree for a feature that only the player controls care about visually.

### Decision 3: Precision Chapter Seeking
- **Decision**: When a user clicks on a chapter segment in `InteractiveTimeline.tsx`, the `computePercent` math will calculate the exact timestamp clicked. Clicking an individual chapter segment *will seek to the clicked point within the chapter*, rather than forcing the video to jump to the very beginning of the chapter.
- **Rationale**: This matches standard YouTube behavior perfectly — hovering shows the boundary, but clicking seeks exactly to your cursor. Clicking "Chapter list items" elsewhere on the page seeks to the chapter start.
- **Alternatives considered**: Forcing segment clicks to seek to `chapter.startTime`, which was rejected as it breaks expected user intuitions for scrubbing.

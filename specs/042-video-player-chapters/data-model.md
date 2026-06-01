# Data Model & State Transitions: YouTube-like player chapters

The state models for `InteractiveTimeline` and `PlayerControls` govern how chapters interact with the continuous progression of the `currentTime`.

## Component Local State & Derivations

### `InteractiveTimeline.tsx` State
- **Input**:
  - `chapters`: Array of `VideoChapterDto` metadata
  - `duration`: Entire video length in seconds (`number`)
  - `progress`: Scrubber thumb fill percentage `0-100` (`number`)
- **Derived State**: 
  - `segments`: Given the `duration` and `chapters`, derived map partitioning the 100% video bar into:
    - `{ startPct: number, widthPct: number, chapter: VideoChapterDto | undefined }`
    - Contiguous rendering gap arrays between `startTime` and `endTime` metrics.
  - `hoverTime`: The percentage distance normalized back to seconds to locate bounding chapter text.
- **Events**:
  - `handleMouseMove`: Calculate cursor X vs the timeline rect width (`computePercent`).
  - `hoverChapter`: Locates the specific `chapter` based on if `hoverTime` falls inside its timestamps.
  - `onSeek(percent)`: Bubble to parent `PlayerControls`.

### `PlayerControls.tsx` State
- **Input**: 
  - `currentTimeFormatted`: e.g., "02:15" 
  - `durationFormatted`: e.g., "12:00"
  - `chapters`: Array of `VideoChapterDto`.
- **Derived State**:
  - `currentTimeSeconds`: `parse(currentTimeFormatted)` (Needed to match the time).
  - `activeChapter`: `chapters.find(c => c.startTime <= currentTimeSeconds && c.endTime >= currentTimeSeconds)`
- **Render Output**:
  - UI Text label next to the timestamp or playhead controls showing `activeChapter?.title`.

## External Interfaces / API

The `VideoChapterDto` interface imported from `@/services/content-service` defines:
- `id` (string)
- `title` (string)
- `startTime` (number - seconds)
- `endTime` (number - seconds)
- `summary` (string - optional)

No new external/backend REST interfaces are required. All information is already retrieved via `/api/app/lesson-cockpit`.

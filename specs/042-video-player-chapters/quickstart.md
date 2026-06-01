# Quickstart for Implementation

## Overview
This feature relies purely on enriching the **frontend video player components**. There are no backend database or API adjustments needed. The `GetLessonCockpit` query already retrieves the necessary `VideoChapterDto` structures and delivers them as `LessonVideoList` and `chapters?: import("@/services/content-service").VideoChapterDto[]`. Therefore, the work lives entirely natively within React.

## How to Test and Iterate

1. **Prerequisites**: Ensure the local environment is running via `npm run dev` in the `/frontend` directory. Locate a video that has chapters associated with it — most easily by checking the `video_chapters` table in postgres.
2. **Accessing the Target Video**: Navigate deep into the student view, past a package and section until arriving at the video screen.
3. **Inspecting Tooltips**: Hover over different sections of the scrub bar. Gaps and titles should appear. Check that hovering near boundaries accurately changes the title tooltip without jitter.
4. **Inspecting Current Chapter Syc**: Look down at `currentTime` display in the controls wrapper bottom-left text. It should have the title, e.g., "02:15 • Chapter 3: Complex Variables".

## Common Pitfalls
1. **Framer Motion Desync**: Avoid nesting `motion.div` structures inside standard elements doing aggressive native DOM repaints for scrubbing. `InteractiveTimeline` has custom logic preventing spring behavior on drag to eliminate desync.
2. **Duration and Time Format mismatch**: Data entering `PlayerControls` is an unformatted scalar (from postMessage) and formatted visually (`mm:ss`). Any code finding the active chapter needs raw seconds `duration` and `currentTime`, rather than parsing string formats backward. We must parse it back correctly if only `currentTimeFormatted` is available, or better yet, tap into raw states.
3. **Hover calculations off by 1-2 pixels**: Always remember `getBoundingClientRect()` does not inherently account for border-box sizing perfectly without padding. The visual bounding element where `handleMouseMove` tracks must span the entire interactive surface to prevent edge cases at 0% or 100%.

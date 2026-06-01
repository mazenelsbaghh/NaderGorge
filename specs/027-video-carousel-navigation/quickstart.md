# Video Carousel Navigation: Quickstart

## Overview
This feature replaces the standard vertical or static video list inside a student's Lesson viewport (`/student/packages/[packageId]/lessons/[lessonId]`) with an interactive, animated `FeatureCarousel`.

## Testing the Implementation

### 1. Identify a Multi-Video Lesson
Ensure your local database has a Package -> Term -> ContentSection -> Lesson hierarchy where the target `Lesson` contains at least *two videos* (e.g., "Video 1", "Video 2").

### 2. View the Video Carousel
1. Log in as a student enrolled in that package.
2. Navigate to the target lesson page.
3. Observe the `FeatureCarousel` appearing below the video player (or in the designated sidebar area).
4. **Interactive Action**: Click on the second step in the carousel.
   - **Expectation**: The carousel animates smoothly (using framer-motion) to highlight step 2. The main video player updates to stream the video associated with step 2.

### 3. Test Auto-Sync (Video Completion)
1. Select the first video in the carousel.
2. Fast-forward the player to the very end of the video.
3. Allow the video to naturally complete.
   - **Expectation**: Depending on your player configuration, the active video index increments. As the player loads the next video, the `FeatureCarousel` should *automatically* animate to the next step without requiring a user click. Note: This relies on the parent component triggering the `currentStep` update.

## Important Constraint Reminders
- **Hydration/FOUC**: Do NOT use `next/image` inside the new `FeatureCarousel` component. Use native `<img>` tags inside the `framer-motion` animators to avoid Turbopack drops.
- **Styling**: Convert any arbitrary widths (e.g. `w-[280px]`) in the `cult-ui` source to inline styles or standard tailwind constraints `w-72`.

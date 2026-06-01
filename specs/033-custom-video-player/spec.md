# Feature Specification: Custom Animated Video Player Controls

**Feature Branch**: `033-custom-video-player`
**Created**: 2026-03-31
**Status**: In Progress (Updated)
**Input**: Replace the existing PlayerControls with a premium animated floating pill control bar, add a full-screen pause overlay with blur + play button, and upgrade the progress/volume sliders to support dragging.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Animated Floating Controls on Hover (Priority: P1)

A student hovering over a playing video sees a floating, pill-shaped control bar animate in from below the video. When they move the mouse away, it smoothly disappears. The controls are always fully visible and never obscure page content below the player.

**Why this priority**: This is the core interaction surface — nothing else works without the controls appearing correctly.

**Independent Test**: Hover over a lesson video → floating pill appears with blur+spring animation → move mouse away → pill disappears.

**Acceptance Scenarios**:

1. **Given** a video is loaded and playing, **When** the cursor enters the video area, **Then** the floating pill control bar animates in from below with blur+spring transition (`circInOut`, 0.6s).
2. **Given** the pill is visible, **When** the cursor leaves, **Then** the pill animates out in reverse with blur.
3. **Given** the video is paused (not started), **When** the page renders, **Then** controls are visible by default (always-on when paused).
4. **Given** the control bar is visible, **Then** the content below the video player is never obscured or pushed down.

---

### User Story 2 - Draggable Progress & Volume Sliders (Priority: P2)

A student can **click OR drag** on the animated progress slider to seek, and click or drag the volume slider to adjust audio. Both sliders respond with spring-animation feedback.

**Why this priority**: Click-only sliders are frustrating. Drag support is expected behavior on any modern video player.

**Independent Test**: Click-and-drag on the progress bar → video seeks in real time while dragging. Release → seek locks to final position.

**Acceptance Scenarios**:

1. **Given** the controls are visible, **When** the user clicks a point on the progress bar, **Then** the video seeks to that position.
2. **Given** the controls are visible, **When** the user presses mouse-down and drags the progress slider, **Then** the slider fill tracks the pointer in real time and seeking is applied continuously.
3. **Given** the user releases the mouse after dragging, **Then** the final seek position is applied and dragging stops.
4. **Given** the controls are visible, **When** the user drags the volume slider, **Then** volume adjusts in real time with the spring fill tracking the pointer.
5. **Given** the user clicks the mute button, **When** toggled, **Then** the volume icon changes and the slider fill snaps to 0 or restores.

---

### User Story 3 - Full-Screen Pause Blur Overlay with Play Button (Priority: P3)

When a video is paused (after having started), the entire video area is covered by a frosted-glass blur overlay with a centred animated play button, replacing the current bottom-only anti-suggestions blur.

**Why this priority**: Creates a premium pause experience and hides YouTube's "More Videos" suggestions across the full player area.

**Independent Test**: Start a video → pause → full-screen frosted overlay appears with centred play button → click play → overlay disappears and playback resumes.

**Acceptance Scenarios**:

1. **Given** the video has started and is then paused, **When** the pause state is detected, **Then** a full-screen `backdrop-blur` overlay covers the entire video area with `animate-in fade-in`.
2. **Given** the overlay is visible, **Then** a centred play button (styled like the idle state: gold-ringed circle) is displayed.
3. **Given** the overlay is visible, **When** the student clicks the play button, **Then** playback resumes and the overlay disappears.
4. **Given** the overlay is visible, **Then** the floating pill controls can still appear on hover above the overlay (z-index layering is correct).

---

### User Story 4 - Playback Speed Selection (Priority: P4)

A student can change the playback speed (0.5x, 1x, 1.5x, 2x) using chip buttons in the control bar, and adjust video quality via the settings (gear) popover.

**Why this priority**: Speed and quality are secondary controls; useful but not blocking the core experience.

**Independent Test**: Click each speed chip → button highlights → playback rate changes. Open settings → choose quality → quality changes.

**Acceptance Scenarios**:

1. **Given** the controls are visible, **When** the user clicks a speed chip, **Then** the playback rate changes and that chip shows the active highlight.
2. **Given** the settings (gear) icon is clicked, **When** the popover opens, **Then** quality options are shown in an animated dropdown.
3. **Given** a quality option is selected, **Then** the postMessage `setQuality` command is sent and the popover closes.

---

### Edge Cases

- What happens when the video has no duration yet (loading)? Progress slider shows 0% fill and seeking is a no-op.
- What happens when the user mutes then drags volume? Muted state clears and volume follows the drag.
- What happens when the user drags outside the slider bounds? Fill clamps to 0% or 100%.
- What happens if the user starts dragging and moves outside the slider div? Drag should continue tracking until `mouseup` is fired on the document.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The control bar MUST appear as a floating overlay pill over the video on mouse enter, and disappear on mouse leave.
- **FR-002**: The control bar MUST animate in/out using `circInOut` spring transition with blur (`filter: blur(10px)` → `blur(0)`), matching the original component exactly.
- **FR-003**: The progress slider MUST support both click-to-seek and click-and-drag-to-seek, clamped to `[0, 100]`.
- **FR-004**: The volume slider MUST support both click-to-change and click-and-drag, clamped to `[0, 100]`.
- **FR-005**: While dragging a slider, the parent slider div MUST capture pointer events globally (via `document` `mousemove`/`mouseup`) so that dragging outside the element still works.
- **FR-006**: The mute button MUST toggle mute state; dragging the volume slider above 0 while muted MUST automatically unmute.
- **FR-007**: The play/pause `Button` (shadcn ghost variant) MUST reflect current playback state.
- **FR-008**: Current time and total duration MUST be displayed in `M:SS` format in the pill.
- **FR-009**: Speed chip `Button`s (0.5x, 1x, 1.5x, 2x) MUST display with the active one highlighted (`bg-[#111111d1]`).
- **FR-010**: A settings (gear) icon `Button` MUST open a quality popover (animated `AnimatePresence`) with YouTube quality options.
- **FR-011**: A fullscreen `Button` MUST trigger the existing `onToggleFullscreen` callback.
- **FR-012**: When `isPlaying` is `false` AND `currentTime > 0` AND `status === 'ready'`, the **full video area** MUST be covered by a frosted-glass `backdrop-blur` overlay with a centred animated play button.
- **FR-013**: The pause overlay MUST cover `inset-0` (full `aspect-video` area), replacing the previous bottom-only `h-[40%]` anti-suggestions blur in `SecureVideoPlayer.tsx`.
- **FR-014**: The pause overlay play button MUST dispatch `togglePlay` when clicked.
- **FR-015**: The `PlayerControlsProps` interface MUST remain unchanged so `SecureVideoPlayer.tsx` requires zero prop-level changes.

### Key Entities

- **`CustomSlider`**: Internal sub-component. Props: `value (0–100)`, `onChange (value) => void`, `className?`. Supports click + drag with global `mousemove`/`mouseup` document listeners while dragging.
- **`PlayerControls`**: Exported component. Full pill UI. Unchanged props interface.
- **`SecureVideoPlayer`**: Modified only for the pause overlay (replace bottom-blur with full-screen blur).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Controls appear within 150ms of mouse entering the video area.
- **SC-002**: Dragging the progress bar seeks the video continuously with no visual lag; the slider fill tracks the pointer in real time.
- **SC-003**: All 4 playback speed chips function correctly with the correct one highlighted at all times.
- **SC-004**: The pause overlay covers 100% of the video area (not just the bottom 40%), hiding all YouTube suggestion overlays.
- **SC-005**: The pause overlay play button correctly resumes playback when clicked.
- **SC-006**: TypeScript strict-mode compilation passes with zero errors after all changes.
- **SC-007**: Content below the video player is never pushed down or obscured by the control pill.

## Assumptions

- `PlayerControlsProps` interface stays the same — `SecureVideoPlayer` needs no prop changes.
- The pause overlay change (`FR-012`, `FR-013`) IS a modification to `SecureVideoPlayer.tsx` (only the anti-suggestions blur section).
- Drag support uses standard `onMouseDown` / `document.mousemove` / `document.mouseup` pattern (no third-party drag library needed).
- Mobile touch drag is out of scope for this iteration; click-to-seek on mobile is sufficient.
- The `Button` component from `@/components/ui/button` is used for all interactive buttons in `PlayerControls`.

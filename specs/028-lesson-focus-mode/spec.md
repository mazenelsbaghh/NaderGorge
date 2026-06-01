# Feature Specification: Lesson Focus Mode

**Feature Branch**: `028-lesson-focus-mode`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "عايز اخلي المين بتاعي هو ليسون فيور و اشيل الحاجات الباقي لمي اخش مثلا النيف بار سلير علي اليمين و بتاع. يفتح الفيديوهات علشان اعمل فوكس اكتر للحصه"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enter Focus Mode (Priority: P1)

As a student, I want the lesson viewer to be the main focus of the screen without distractions (like navbars or sidebars) so that I can concentrate fully on the lesson videos.

**Why this priority**: Focus is critical for learning retention. Distractions reduce the educational value of the platform.

**Independent Test**: Can be tested independently by opening a lesson page and verifying that headers, navigation bars, and sidebars are automatically hidden or collapsible, leaving the video and primary lesson content as the central focus.

**Acceptance Scenarios**:

1. **Given** a student opens a lesson page, **When** the page loads, **Then** the global navigation bar and sidebars are not visible, maximizing the video player area.
2. **Given** the user is in focus mode, **When** they need to navigate back or access other parts of the system, **Then** there is a clear, minimal way to exit focus mode or return to the package curriculum.

---

### User Story 2 - Toggle Focus Elements (Priority: P2)

As a student watching a lesson, I want the ability to easily bring back the hidden navigation elements if I need them without leaving the page.

**Why this priority**: While focus is important, preventing users from navigating causes frustration. A seamless toggle improves usability.

**Independent Test**: Can be tested by clicking a "toggle menu" or exiting focus mode to reveal the standard page layout with navigation.

**Acceptance Scenarios**:

1. **Given** the lesson viewer is in focus mode, **When** the student hovers near the top/side or clicks a designated "Show Menu" button, **Then** the navigation elements smoothly reappear.
2. **Given** the navigation elements are visible, **When** the student clicks "Hide Menu" or begins interacting with the lesson video again, **Then** the elements smoothly hide.

### Edge Cases

- What happens when a user navigates directly to the lesson via a shared link? (Focus mode should still apply).
- How does the system handle extreme aspect ratios (e.g., ultrawide monitors) when in focus mode?
- Does focus mode interfere with native browser full-screen capabilities on mobile devices?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST hide the global layout elements (e.g., top navbar, side navigation) when rendering a lesson detail view.
- **FR-002**: System MUST provide a clear "Back to Curriculum" or "Exit Focus" control that allows the user to return to the package view.
- **FR-003**: System MUST NOT compromise accessibility (screen-readers must still be able to navigate the page).
- **FR-004**: System MUST allow users to interact with lesson resources and homework without exiting focus mode.
- **FR-005**: Smooth transitions MUST be used when hiding/showing the distracting elements to prevent jarring UI shifts.

### Key Entities

- **Lesson View State**: Determines whether the UI is in 'minimal/focus' mode or 'standard' mode (though it might just be achieved by a specific layout wrapper for the route).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Students spend 15% more time consecutively watching videos without navigating away.
- **SC-002**: 100% of global navigation elements are removed from the viewport when a lesson initially loads.
- **SC-003**: Users can trigger an exit or go back in under 1 second using an obvious control.
- **SC-004**: No regressions in mobile usability or viewport scaling.

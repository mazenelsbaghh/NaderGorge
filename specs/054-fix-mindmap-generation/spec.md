# Feature Specification: Fix Mindmap Generation Logic

**Feature Branch**: `054-fix-mindmap-generation`
**Created**: 2026-04-03
**Status**: Draft
**Input**: User description: "توليد الخرائط مش مظبوط عايزك تراجعوا و تراجع اللوجك بتاعها"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Accurate State Representation (Priority: P1)

As an admin, I want the mindmap generation status to accurately reflect the actual outcome of the AI generation process without contradictory UI elements, so that I can reliably know if a mindmap was successfully created or if it failed.

**Why this priority**: Correct status indication is critical for the admin to trust the automated AI generation system. Displaying a success message alongside an error icon causes confusion and disrupts the workflow.

**Independent Test**: Can be fully tested by triggering a mindmap generation task and observing the UI upon completion. The UI must only show a success indicator if it succeeded, or an error indicator if it failed.

**Acceptance Scenarios**:

1. **Given** a video where mindmap generation completes successfully, **When** the admin views the content list, **Then** only the success message "اكتمل توليد الخرائط الذهنية بنجاح" is shown, with no error icons.
2. **Given** a video where mindmap generation fails, **When** the admin views the content list, **Then** only an error indicator and retry button are shown, with no success messages.

---

### User Story 2 - Robust AI Generation Logic (Priority: P2)

As an admin, I want the backend logic for mindmap generation to be thoroughly resilient, handling prompt failures or database update concurrency smoothly, so that partial or failed generations are properly tracked.

**Why this priority**: Ensuring the underlying stability of the AI worker prevents incorrect state assignments in the first place.

**Independent Test**: Can be tested by forcing an AI failure (e.g., API timeout) and ensuring the database correctly registers it as a clear failure without entering a limbo state.

**Acceptance Scenarios**:

1. **Given** the mindmap worker encounters an active API error, **When** the job fails, **Then** the database status is strictly marked as `MindmapFailed` (or equivalent).
2. **Given** multiple concurrent webhook updates, **When** updating the video entity, **Then** no concurrency conflicts corrupt the success/failure state flags.

### Edge Cases

- What happens when a user clicks the "Retry" button while the previous mindmap job is actually still running but incorrectly reported as failed?
- How does the system handle concurrent state updates from the chapter generation phase vs. the mindmap generation phase?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST uniquely and exclusively represent either a `success` or `failure` state for mindmap generation per lesson.
- **FR-002**: The UI MUST NOT render both the success text block and the failure/retry icons simultaneously for the same record.
- **FR-003**: The backend worker MUST correctly transition the `mindmapStatus` (or equivalent flag) to definitively reflect the final outcome of the job.
- **FR-004**: The system MUST gracefully recover or reset the mindmap state when an administrator requests a retry.

### Key Entities

- **LessonVideo**: Contains the properties identifying the background generation status (e.g., `AiProcessingState`, `MindmapImageUrl`, `IsMindmapFailed`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of completed mindmap jobs display exactly one non-contradictory status indicator in the admin UI.
- **SC-002**: "Retry" operations reliably clear previous error flags and re-trigger generation without UI glitching.
- **SC-003**: Concurrency exceptions during background webhook reporting are completely resolved or handled without data corruption.

## Assumptions

- The underlying Gemini AI generation prompt is generally functional, and the issue lies primarily in state tracking, job status resolution, and UI conditional rendering logic.
- The system already has a defined state enum or set of boolean flags (e.g., `AiStatus`, `HasMindmap`, `MindmapError`) that simply need logical correction.

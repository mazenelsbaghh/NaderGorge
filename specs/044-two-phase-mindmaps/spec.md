# Feature Specification: Two-Phase Mindmap Generation

**Feature Branch**: `044-two-phase-mindmaps`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: "احط الصور منين و اعمل توليد للصور منين انا عايزها مرحلتين مرحله للاقسام بعهدا مرحله الصور"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Multi-Phase AI Generation Workflow (Priority: P1)

As an admin, I outline the AI generation into two clear steps: first to extract chapters (sections) from the video audio, and second, to generate the expressive caricature mind map images. This prevents wasted generation time if chapters need manual adjustments first.

**Why this priority**: It is the core requirement. Splitting the process allows manual review of chapters before initiating the expensive and time-consuming image generation process.

**Independent Test**: Can be fully tested by verifying that video chapter generation completes without mind maps, and a separate manual trigger initiates the mind map generation.

**Acceptance Scenarios**:

1. **Given** a lesson video without chapters, **When** the admin triggers AI analysis, **Then** only the chapters and subtitles are generated.
2. **Given** a lesson with existing AI chapters but no mind maps, **When** the admin clicks the "Generate Mindmaps" button, **Then** the generative AI produces the mind maps for each chapter.

---

### User Story 2 - Admin UI for Image Upload and Generation Trigger (Priority: P2)

As an admin, I need a clear UI location within the lesson details page where I can understand how to provide the reference images (instructor caricature photo) and a dedicated button to start the mind map generation phase.

**Why this priority**: Resolves the user's specific confusion ("Where do I put the images and where do I generate them from").

**Independent Test**: Can be fully tested by checking the Admin Lesson interface for clear calls-to-action regarding mind map generation and reference photo status.

**Acceptance Scenarios**:

1. **Given** the admin is viewing a specific lesson, **When** they look at the AI processing section, **Then** they see a clear indicator of whether the teacher reference photo is set and a button to "Generate Mindmap Images".
2. **Given** the admin triggers the mind map generation, **When** the process runs, **Then** the UI shows a loading state and updates the chapter list with the new image previews upon completion.

### Edge Cases

- What happens if the admin triggers mind map generation before chapters are generated? The system should disable the button or show a warning.
- How does the system handle a failure in one of the mind map generations? It should mark that specific chapter as failed without aborting the others, allowing retries.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST detach the mind map generation logic from the immediate video chaptering job pipeline.
- **FR-002**: The admin interface MUST provide a distinct action button per Lesson/Video to trigger "Generate Mindmaps" exclusively.
- **FR-003**: The system MUST verify that Video Chapters exist before allowing mind map generation.
- **FR-004**: The system MUST queue a dedicated asynchronous background job exclusively for generating mind map images.
- **FR-005**: The admin interface MUST display the status of the teacher's reference photo to clarify "where images are put from".
- **FR-006**: The system MUST update the admin UI to reflect the progress or completion of the mind map generation.

### Key Entities

- **LessonVideo**: Needs to track the separate processing states (e.g., `IsProcessingMindmaps`).
- **VideoChapter**: Stores the generated `MindmapImageUrl`.
- **Background Job Queue**: A new queue specifically for the mind map generation tasks to keep it decoupled from the heavier audio analysis queue.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can generate chapters and edit them manually BEFORE spending time/resources on mind map generation.
- **SC-002**: Admins successfully locate the "Generate Mindmaps" button within 5 seconds of opening the lesson details page.
- **SC-003**: The system completely decouples the two processes, ensuring audio analysis never fails due to image generation timeouts.

## Assumptions

- We assume the existing global `TeacherPhoto` configuration is sufficient for the reference photo, but the UI must make this clear to avoid user confusion.
- We assume that mind maps are generated for ALL chapters of the video at once when triggered, rather than individually per chapter.
- The same background job infrastructure (BullMQ + Redis) will be utilized for the separated tasks.

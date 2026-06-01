# Architecture Research: Two-Phase Mindmap Generation

## Context
The system currently performs video chapter extraction (audio analysis) and mind map generation in a single monolithic background job. The user requested to split this into a two-phase process: 
1. **Phase 1**: Extract chapters and subtitles from the video.
2. **Phase 2**: Generate mind maps for the chapters.

This split allows admins to verify and potentially edit the chapters before paying the generation cost (time and tokens) for mind maps. It also provides a clear UI to trigger mind map generation.

## Technical Decisions

### 1. Decoupling Background Jobs
**Decision**: Separate the existing BullMQ job into two distinct jobs: `analyze-video-audio` (existing) and `generate-chapter-mindmaps` (new).
**Rationale**: By separating the jobs, we gain granular control over when the image generation process occurs. The audio analysis can remain automatic (triggered on video upload), while the mind map generation becomes a deliberate, manual trigger from the Admin UI.
**Alternatives considered**: Passing a flag to the existing job (e.g., `generateImages: boolean`). Overcomplicates the job state machine and doesn't cleanly separate the concerns or scaling profiles of audio analysis vs. image generation.

### 2. State Management in Database
**Decision**: Add an `IsProcessingMindmaps` flag to the `LessonVideo` entity.
**Rationale**: We need to lock the generic "Generate Mindmaps" button in the frontend while generation is already in progress. We already have `IsProcessingAI` for the audio analysis phase, so adding `IsProcessingMindmaps` maintains consistency.
**Alternatives considered**: Checking BullMQ state dynamically. Rejected because querying Redis for UI state is less robust than tracking the authoritative lock in the primary Postgres database.

### 3. API Design
**Decision**: Create a new endpoint `POST /api/Admin/Content/Lessons/{lessonId}/Videos/{videoId}/GenerateMindmaps`.
**Rationale**: This explicit endpoint clearly correlates with the User Story 2 requirement for a dedicated button to trigger the process. It will dispatch the new `generate-chapter-mindmaps` job to BullMQ.

### 4. Frontend Admin UI Placement
**Decision**: Add a new button in `AdminLessonVideoList` and `AdminTeacherPhotoUpload` status indicator.
**Rationale**: The admin must upload a teacher photo before generating mind maps, so showing the photo status next to the mind map generation button makes the dependency clear.

## Conclusion
All `NEEDS CLARIFICATION` points regarding how to decouple the background process and how to initiate the second phase from the frontend have been resolved. We will implement a new job queue type, a new C# MediatR command, and a new frontend button to bridge the gap.

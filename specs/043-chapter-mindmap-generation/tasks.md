# Tasks: Chapter Mindmap Generation

**Input**: Design documents from `/specs/043-chapter-mindmap-generation/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

## Phase 1: Foundational (Database & Shared Architecture)

**Purpose**: Core infrastructure extending the database schema required before stories are built.

- [x] T001 Create `TeacherPhoto` entity and add `MindmapImageUrl` to `LessonChapter` in `backend/src/NaderGorge.Domain/Entities/`
- [x] T002 Apply EF Core configurations and generate migration `AddChapterMindmapGeneration` in `backend/src/NaderGorge.Infrastructure/`

**Checkpoint**: Foundation ready - Database schema supports the new requirements.

---

## Phase 2: User Story 1 - Upload Teacher Reference Photos (Priority: P1) 🎯 MVP

**Goal**: Allow admins to upload teacher photos to be used as caricature references (stored locally).
**Independent Test**: Navigate to the admin UI, upload a photo, and verify it writes to `/public/uploads/teacher/` or `.tmp/` and saves to DB.

### Implementation for User Story 1

- [x] T003 [US1] Implement `UploadTeacherPhotoCommand.cs` and handler in `backend/src/NaderGorge.Application/Features/Admin/Commands/TeacherPhotoOps/`
- [x] T004 [P] [US1] Add `TeacherController` (or via `AdminController`) endpoint mapping the upload command in `backend/src/NaderGorge.Api/`
- [x] T005 [US1] Create `AdminTeacherPhotoUpload.tsx` React component for the frontend admin panel allowing file selection.

**Checkpoint**: At this point, User Story 1 should be fully functional; photos can be uploaded and saved successfully.

---

## Phase 3: User Story 2 - Automated Mind Map Gen per Chapter (Priority: P1)

**Goal**: Automatically trigger the `gemini-3.1-flash-image-preview` model post-chaptering to map the chapter text + reference photos into an image.
**Independent Test**: Trigger a video analysis job. Worker should successfully call Gemini, write base64 output to `.tmp/mindmaps/`, and return URL.

### Implementation for User Story 2

- [x] T006 [P] [US2] Update `LessonVideoMessage` and payload construction in `.NET Backend` to transmit active local `TeacherPhoto` paths to the BullMQ job.
- [x] T007 [US2] Update `worker/src/services/geminiService.ts` to implement `ai.models.generateContent` with `gemini-3.1-flash-image-preview`. Apply the Arabic Educational constraint + Caricature prompt.
- [x] T008 [US2] Update `geminiService.ts` to decode `part.inlineData.data` via `Buffer.from` and run `fs.writeFileSync(...)` to store the image locally.
- [x] T009 [P] [US2] Update `AiAnalysisCompletedCommand.cs` to map incoming `MindmapImageUrl` payloads back into the `LessonChapter` DB entity.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently. AI accurately creates map images and `.NET` backend saves the links.

---

## Phase 4: User Story 3 - Displaying Mind Maps to Students (Priority: P2)

**Goal**: Present the mind map natively in the student interface (both overlay + persistent view).
**Independent Test**: Open a processed lesson. Skip to the end of a chapter; observe the overlay. Scroll down, observe the persistent map updates synchronously.

### Implementation for User Story 3

- [x] T010 [P] [US3] Update `GetLessonCockpitQuery.cs` to project `MindmapImageUrl` when retrieving `LessonChapterDTOs`.
- [x] T011 [US3] Update `frontend/src/components/video/SecureVideoPlayer.tsx` to display a transient `framer-motion` overlay mapped with `activeChapter?.MindmapImageUrl` dynamically popping up briefly when a chapter ends.
- [x] T012 [US3] Create `LessonMindmapDisplay.tsx` beneath the video player that persistently renders the actively hovered or playing chapter's `MindmapImageUrl` avoiding `next/image` usage inside motion constraints.

**Checkpoint**: All user stories should now be independently functional.

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T013 Polish transitions and spring animations on the Mind map display UI to comply with the project constitution "Premium Editorial Design System" guidelines.
- [ ] T014 Handle Error states/Fallbacks (e.g. if rendering fails or image is not generated due to safety triggers in Gemini). 

# Tasks: AI Video Transcription and Chapters

**Input**: Design documents from `/specs/037-ai-video-chapters/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Install `@google/genai` and `fluent-ffmpeg` (plus `@types/fluent-ffmpeg`) dependencies in the `worker` project
- [x] T002 [P] Configure FFmpeg environment variables or path in the Node worker environment if required

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 Create `VideoChapter.cs` domain entity in `backend/src/NaderGorge.Domain/Entities/Content/`
- [x] T004 Update `backend/src/NaderGorge.Domain/Entities/Content/Video.cs` to add `SubtitleUrl` and `IsProcessingAI` properties
- [x] T005 Update `AppDbContext.cs` to include DbSet and configure the relationship between Video and VideoChapter
- [x] T006 Add new EF Core Migration `AddVideoChapters` and update the database

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Automated Lesson Analysis (Priority: P1) 🎯 MVP

**Goal**: Extract audio from lesson videos and process it via AI (Gemini File API + FFmpeg) to automatically generate SRT subtitles, chapter timelines, and chapter summaries.

**Independent Test**: Can be tested by invoking the generation process for a specific video in the admin dashboard and verifying that the database is populated with valid SRT URL, chapter timestamps, and summaries.

### Implementation for User Story 1

- [x] T007 [US1] Create `geminiService.ts` in `worker/src/services/` to handle `uploadFile` and `generateContent` interactions with Gemini Flash 2.5
- [x] T008 [P] [US1] Create `audioExtractor.ts` in `worker/src/utils/` wrapping `fluent-ffmpeg` to download remote video stream and extract a low-bitrate MP3
- [x] T009 [US1] Create the BullMQ job processor `analyzeVideoChapters.ts` in `worker/src/jobs/` that orchestrates audio extraction, Gemini API, and calls the webhook
- [x] T010 [P] [US1] Create `AnalyzeVideoAICommand.cs` in `backend/src/NaderGorge.Application/Features/Admin/Commands/` to publish the job to Redis and set `IsProcessingAI = true`
- [x] T011 [US1] Implement `POST /api/V1/admin/lessons/{lessonId}/videos/{videoId}/generate-ai` in `AdminController.cs`
- [x] T012 [P] [US1] Create `AiAnalysisCompletedCommand.cs` in `backend/src/NaderGorge.Application/Features/Internal/Commands/` to update DB with chapters and subtitle URL
- [x] T013 [US1] Implement securely internally-routed `POST /api/V1/internal/callbacks/ai-analysis-completed` in `InternalController.cs`

**Checkpoint**: At this point, User Story 1 should be fully functional. Admin triggers process -> DB gets updated by worker callback.

---

## Phase 4: User Story 2 - Student Video Player Chapters (Priority: P2)

**Goal**: Show interactive chapter markers on the video player and a list of chapter summaries.

**Independent Test**: Can be tested by rendering a video player with DB chapter data and ensuring timeline markers and summary side-panels update and navigate correctly.

### Implementation for User Story 2

- [x] T014 Update `GetLessonDetailsQuery.cs` to include `VideoChapters` mapping sorted by `Order`
- [x] T015 Run full backend test suite to ensure DTO shape hasn't broken existing usage
- [x] T016 Create `InteractiveTimeline.tsx` component in `frontend/src/components/video/` calculating `%` width and rendering segmented markers
- [x] T017 Create `ChapterList.tsx` component in `frontend/src/components/video/` to show the chronological list and summaries
- [x] T018 Integrate `InteractiveTimeline` and `ChapterList` into `VideoPlayer.tsx` and synchronize with the player's `currentTime`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T019 Polish the Admin Video UI (`frontend/src/components/admin/VideoManager.tsx`) to add a "Generate AI Chapters" button that triggers the API and shows a loading state if `isProcessingAI` is true
- [x] T020 Run quickstart.md validation to ensure end-to-end integration works seamlessly

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2)
- **User Story 2 (P2)**: Can start after Foundational (Phase 2). Needs mock data or US1 completely finished to test interactively.

### Parallel Opportunities

- Worker development (T007-T009) and Backend Command setup (T010-T013) can run completely parallel during US1.
- DTO definitions (T015) and component scaffolding (T016, T017) can be done while the backend queries (T014) are being updated.

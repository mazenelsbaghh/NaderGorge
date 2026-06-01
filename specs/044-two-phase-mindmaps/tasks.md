---
description: "Task list for Two-Phase AI Mindmap Generation feature"
---

# Tasks: Two-Phase AI Mindmap Generation

**Input**: Design documents from `/specs/044-two-phase-mindmaps/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure
*(No foundational structure to apply since the monorepo is already established)*

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [ ] T001 Update data model by adding `IsProcessingMindmaps` primitive boolean property to `backend/src/NaderGorge.Domain/Entities/LessonVideo.cs`
- [ ] T002 Generate Entity Framework migration for `IsProcessingMindmaps` field and update database
- [ ] T003 Register the new BullMQ `generate-chapter-mindmaps` queue alongside existing queues in the Node.js worker at `worker/src/index.ts`

**Checkpoint**: Foundation ready - Database schema and worker queue are prepared.

---

## Phase 3: User Story 1 - Multi-Phase AI Generation Workflow (Priority: P1) 🎯 MVP

**Goal**: Fully decouple image generation from the immediate audio processing step in the background worker, and create the command to trigger the new isolated image task.

**Independent Test**: Initiating audio transcription does not generate mind maps. Triggering the new explicit API endpoint will successfully run the new image job in isolation.

### Implementation for User Story 1

- [ ] T004 [P] [US1] In `worker/src/index.ts`, remove the automatic mindmap generation logic at the end of the `analyze-video-audio` pipeline.
- [ ] T005 [P] [US1] Implement the standalone background worker job handler in `worker/src/index.ts` to listen for the `generate-chapter-mindmaps` queue.
- [ ] T006 [US1] Expose the isolated mindmap image loop logic safely within `worker/src/services/geminiService.ts` to generate and save images per existing DB video chapter.
- [ ] T007 [P] [US1] Update `GetLessonCockpitQuery` and `GetLessonDetailQuery` response DTOs in backend API to return the `IsProcessingMindmaps` status field.
- [ ] T008 [US1] Construct the backend MediatR workflow `GenerateChapterMindmapsCommand` inside `backend/src/NaderGorge.Application/Features/Admin/Commands/MindmapOps/GenerateChapterMindmapsCommand.cs` to submit the job payload.
- [ ] T009 [US1] Implement `CalculateMindmaps` Endpoint in `backend/src/NaderGorge.API/Controllers/AdminController.cs` receiving the REST payload to trigger `GenerateChapterMindmapsCommand`.

**Checkpoint**: At this point, User Story 1 backend dependencies are fully functional and independently testable through API clients (e.g. Swagger) or external tools.

---

## Phase 4: User Story 2 - Admin UI for Image Upload and Generation Trigger (Priority: P2)

**Goal**: Build a clear UI context so the Admin sees "Where the images come from" (Teacher Photo), and how to invoke the mind map process after validating AI chapters.

**Independent Test**: Loading the lesson cockpit displays the teacher photo visual verification rule and an actionable button bound to the new mindmap endpoint.

### Implementation for User Story 2

- [ ] T010 [P] [US2] Update frontend queries/types in `frontend/src/api/adminApi.ts` to include the `IsProcessingMindmaps` status boolean mapping.
- [ ] T011 [US2] Extend `frontend/src/components/admin/AdminLessonVideoList.tsx` or its matching Cockpit view to inject the explicit "Generate Mind Maps" trigger button for a video that already has generated chapters.
- [ ] T012 [US2] Augment the `AdminTeacherPhotoUpload.tsx` view (or place a contextual hint next to the generate button limit) declaring explicitly where the AI character avatar gets drawn from as reference status.
- [ ] T013 [US2] Wire the frontend interaction from `T011` to fire against the new `AdminController` endpoint created in `T009`, implementing local reactive fallback (e.g., locking the button to prevent rapid subsequent submission loops).

**Checkpoint**: At this point, User Stories 1 AND 2 should both work cohesively forming the fully intended decoupled execution path.

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T014 Run quickstart.md validation locally matching the execution cycle.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: Depends on existing system. BLOCKS US1.
- **User Stories (Phase 3+)**: All depend on Foundational phase completion. User Story 1 MUST complete first so the backend API is ready for the User Story 2 UI.

### Within Each User Story

- Data models & database prior to MediatR business logic.
- Node.js job architecture before backend REST dispatcher logic.
- REST endpoints exist before React.js integration points.

### Parallel Opportunities

- Worker Queue separation (T004/T005) can execute simultaneously with DTO (T007) and Backend MediatR command (T008) scaffolding.
- The UI representation definitions (T010) safely can be typed alongside the implementation of the backend controller handler.

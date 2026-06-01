# Tasks: Dynamic Video Watermark

**Input**: Design documents from `/specs/036-dynamic-video-watermark/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are OPTIONAL - only include them if explicitly requested in the feature specification.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

*(No new dependencies or database migrations required for this feature)*

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T001 Update `IVideoEncryptionService.cs` interface to accept `studentName` and `studentPhone` in its payload generation in `backend/src/NaderGorge.Application/Common/IVideoEncryptionService.cs`
- [x] T002 Update `VideoEncryptionService` implementation to inject student identifying data into the JSON payload before encrypting in `backend/src/NaderGorge.Infrastructure/Services/VideoEncryptionService.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Deterring Screen Recording (Priority: P1) 🎯 MVP

**Goal**: Display an unclickable, moving watermark over the player iframe containing student data.

**Independent Test**: Load a video URL and observe the student's name moving randomly every 12 seconds.

### Implementation for User Story 1

- [x] T003 [US1] Update `CreateVideoSessionCommand` to fetch user identity (Name, Phone) and pass it to the encryption service in `backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs`
- [x] T004 [US1] Update Next.js `embed` handler to parse `StudentName` and `StudentPhone` from decrypted token in `frontend/src/app/api/video/embed/route.ts`
- [x] T005 [US1] Update `generateEmbedHtml` to inject the CSS classes, moving watermark `div`, and `setInterval` logic for positioning in `frontend/src/app/api/video/embed/route.ts`
- [x] T006 [US1] Update `generateTelegramPlayerWrapper` similarly to inject the watermark in `frontend/src/app/api/video/embed/route.ts`
- [x] T007 [US1] Update the `domKiller.allowedIds` in `embed/route.ts` to include the watermark element ID to prevent self-deletion

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T008 Test local compilation (`dotnet build` & Next.js reload) to ensure syntax is valid and no formatting corruption broke the DOCTYPE.
- [x] T009 Verify pointer-events are disabled completely via DevTools so watermark doesn't capture clicks.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories

### Within Each User Story

- Models before services
- Core implementation before integration
- Story complete before moving to next priority

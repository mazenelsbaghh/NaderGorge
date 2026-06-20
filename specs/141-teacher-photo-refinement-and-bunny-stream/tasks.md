# Tasks: Teacher Photo Refinement and Bunny Stream

**Input**: Design documents from `/specs/141-teacher-photo-refinement-and-bunny-stream/`
**Prerequisites**: plan.md (required), spec.md (required)

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Setup & Environment Configuration

**Purpose**: Map necessary environment variables for the worker service to authenticate with Bunny Stream API.

- [x] T001 Configure environment variables `BUNNY_STREAM_LIBRARY_ID` and `BUNNY_STREAM_API_KEY` for the `worker` service in `docker-compose.yml`

---

## Phase 2: Foundational & Backend API Implementation

**Purpose**: Build the database queries and API controllers to retrieve active teacher photos.

- [x] T002 Implement `GetActiveTeacherPhotoQuery` and handler returning the active photo's relative URL in `backend/src/NaderGorge.Application/Features/Admin/Queries/AdminTeacherQueries.cs`
- [x] T003 [P] Implement `GET /api/admin/teachers/{teacherId}/active-photo` endpoint in `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [x] T004 [P] Implement `GET /api/teacher/profile/active-photo` endpoint in `backend/src/NaderGorge.API/Controllers/TeacherController.cs`
- [x] T005 [P] Create unit tests for `GetActiveTeacherPhotoQuery` in `backend/tests/NaderGorge.Application.Tests/MultiTeacher/TeacherProfileTests.cs`

---

## Phase 3: User Story 1 - Active AI Photo Preview in Admin Portal (Priority: P1)

**Goal**: Fetch and display the active reference photo for a teacher in both teachers list (edit modal) and AI monitor page.

**Independent Test**: Open edit modal for a teacher or select a teacher in AI monitor.
**Expected Result**: The frontend calls the active-photo endpoint and the photo is shown in the preview box.

- [x] T006 [P] [US1] Add `getActiveTeacherPhoto` service call in `frontend/src/services/admin-service.ts`
- [x] T007 [P] [US1] Add `getActiveTeacherPhoto` service call in `frontend/src/services/teacher-service.ts`
- [x] T008 [US1] Modify `AdminTeacherPhotoUpload.tsx` in `frontend/src/components/admin/AdminTeacherPhotoUpload.tsx` to accept `teacherId` prop, and fetch the active photo preview on mount/change.
- [x] T009 [US1] Update `handleOpenModal` in `frontend/src/app/admin/teachers/AdminTeachersPageClient.tsx` to fetch the active AI photo URL when editing a teacher and set the preview state.
- [x] T010 [US1] Update `AIMonitorPageClient.tsx` in `frontend/src/app/admin/ai-monitor/AIMonitorPageClient.tsx` to render a teacher selection dropdown if user is an admin.
- [x] T011 [US1] Handle `?teacherId=` query parameter in `frontend/src/app/admin/ai-monitor/AIMonitorPageClient.tsx` to initialize selected teacher.
- [x] T012 [US1] Pass `selectedTeacherId` from `AIMonitorPageClient.tsx` to `<AdminTeacherPhotoUpload />`.

---

## Phase 4: User Story 2 - Contextual Teacher Photo Upload & Job Scoping (Priority: P1)

**Goal**: Ensure uploads from AI Monitor target the selected teacher, and AI video analysis commands query the correct teacher's photo instead of a global latest.

**Independent Test**: Upload a photo in AI Monitor for a selected teacher.
**Expected Result**: The photo is linked to that teacher's `TeacherId`. Verify that starting video analysis uses the specific teacher's photo.

- [x] T013 [US2] Modify `handleUpload` in `frontend/src/components/admin/AdminTeacherPhotoUpload.tsx` to upload to the correct `teacherId` passed via props instead of defaulting to the logged-in admin user.
- [x] T014 [US2] Update `AnalyzeVideoAICommand.cs` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AnalyzeVideoAICommand.cs` to query the specific teacher's photo for the lesson's package, instead of the global latest.
- [x] T015 [US2] Update `GenerateChapterMindmapsCommand.cs` in `backend/src/NaderGorge.Application/Features/Admin/Commands/MindmapOps/GenerateChapterMindmapsCommand.cs` to query the specific teacher's photo.
- [x] T016 [US2] Update `RegenerateChapterMindmapCommand.cs` in `backend/src/NaderGorge.Application/Features/Admin/Commands/MindmapOps/RegenerateChapterMindmapCommand.cs` to query the specific teacher's photo.

---

## Phase 5: User Story 3 - Bunny Stream Video Audio Extraction (Priority: P1)

**Goal**: Enable worker to download audio track from Bunny Stream videos using `yt-dlp` and the official Referer header.

**Independent Test**: Run a job with a Bunny video ID.
**Expected Result**: The worker downloads the Bunny Stream video using `yt-dlp` and outputs a valid local MP3 file.

- [x] T017 [US3] Update `extractAudioFromVideo` in `worker/src/utils/audioExtractor.ts` to detect Bunny Stream GUIDs.
- [x] T018 [US3] Construct the correct embed iframe URL using the GUID and `BUNNY_STREAM_LIBRARY_ID` in `worker/src/utils/audioExtractor.ts`.
- [x] T019 [US3] Add `--referer "https://admin.massar-academy.net/"` to `yt-dlp` execution arguments for Bunny videos.
- [x] T020 [US3] Add a unit test verifying `extractAudioFromVideo` detects Bunny GUIDs and constructs arguments in `worker/src/worker-flows.test.ts`.

---

## Phase 6: Polish, Critique & Verification

**Goal**: Final critique checks, quality gates, and verification.

- [x] T021 Run deep critique fixes on all changed files
- [x] T022 Run `clean-code-guard` on changed production files
- [x] T023 Run `test-guard` on changed test files
- [x] T024 Run feature tests for user stories and verify build
- [x] T025 Run final build verification and verify Docker containers

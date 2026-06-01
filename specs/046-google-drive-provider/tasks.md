# Tasks: Google Drive Video Provider

**Input**: Design documents from `/specs/046-google-drive-provider/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

N/A - This feature builds on the existing structure.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T001 Update `AddLessonVideoCommand` and `UpdateLessonVideoCommand` validators in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs` to accept `"google_drive"` as a valid Provider.
- [x] T002 Update video provider types/enums (if any exist) in the frontend to include `'google_drive'`.

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Admin Registers a Google Drive Video for a Lesson (Priority: P1) 🎯 MVP

**Goal**: Admins can select Google Drive and input a Google Drive File ID.

**Independent Test**: Admin can save a lesson with a Google Drive video safely.

### Implementation for User Story 1

- [x] T003 [US1] Update `frontend/src/components/admin/AddVideoForm.tsx` to add `"Google Drive"` to the Provider select dropdown.
- [x] T004 [US1] Add logic in `frontend/src/components/admin/AddVideoForm.tsx` to automatically parse and extract the Google Drive `fileId` when a full `drive.google.com` URL is pasted into the ProviderVideoId input.
- [x] T005 [US1] Update `frontend/src/components/admin/LessonVideoList.tsx` to correctly display the Google Drive provider label/icon.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Stories 2 & 3 - Student Watches a Google Drive Video Securely (Priority: P1 & P2)

**Goal**: Students can watch the Google Drive video via Shadow DOM without leaking the ID.

**Independent Test**: Student accesses the lesson page, and the Google Drive video plays inside the iframe without leaking the ID in the network tab or accessible DOM.

### Implementation for User Stories 2 & 3

- [x] T006 [P] [US2] Update `frontend/src/app/api/video/embed/route.ts` to build the Shadow DOM HTML containing the `<iframe src="https://drive.google.com/file/d/.../preview?rm=minimal">` when the `provider` equals `'google_drive'`.
- [x] T007 [P] [US2] Verify `frontend/src/components/video/SecureVideoPlayer.tsx` gracefully handles the Google Drive embed (it relies on standard iframe/blob rendering, which should be natively compatible).

**Checkpoint**: At this point, both admin and student flows are fully functional.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T008 Update AI job dispatching (e.g. `worker/src/index.ts` or `frontend/src/app/api/video/ai-monitor/route.ts`) or `geminiService.ts` to ensure it gracefully ignores or explicitly skips processing for `"google_drive"` videos, as they are playback-only for v1.
- [x] T009 Run quickstart.md tests manually to validate functionality.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: Starts immediately.
- **User Stories (Phase 3 & 4)**: Depend on Foundational phase completion.
- **Polish (Final Phase)**: Depends on all user stories being complete.

### Priority / Parallel Opportunities

- US1 and US2 can technically be developed in parallel once the base DTO allows `google_drive` (T001).
- T003, T004, and T005 can be resolved together within the admin UI context.
- T006 and T007 handle the student perspective and can be resolved independently from admin forms by mocking the DB entry.

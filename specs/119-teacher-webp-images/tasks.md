# Tasks: Teacher Image WebP Conversion

**Input**: Design documents from `/specs/119-teacher-webp-images/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure (None needed as project is already initialized)

- [x] T001 Verify project structure is ready for implementation

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core utilities that MUST be complete before ANY user story can be implemented

- [x] T002 Implement filename extension resolver helper `getExtensionFromBase64` and filename rename helper `renameFileToMatchBase64` in `frontend/src/utils/image-compressor.ts`

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Admin uploads or updates Teacher Profile Image (Priority: P1) 🎯 MVP

**Goal**: Convert and rename teacher profile images to WebP client-side during Admin upload, and handle it gracefully on the backend.

**Independent Test**: Upload a PNG image in Admin Teachers page, verify it is uploaded as WebP, and saved with `.webp` extension.

### Implementation for User Story 1

- [x] T003 [P] [US1] Rename profile image file extension to `.webp` before upload in `frontend/src/app/admin/teachers/AdminTeachersPageClient.tsx`
- [x] T004 [P] [US1] Detect and enforce `.webp` extension based on base64 prefix header in `backend/src/NaderGorge.Application/Features/Admin/Commands/TeacherPhotoOps/UploadTeacherProfileImageCommand.cs`

**Checkpoint**: User Story 1 is fully functional and testable independently.

---

## Phase 4: User Story 2 - Admin uploads Teacher AI Analysis Photo (Priority: P2)

**Goal**: Convert and rename teacher AI photos to WebP client-side during Admin upload, and save as `.webp` on the backend.

**Independent Test**: Upload a JPEG photo in Admin Teachers AI photo input, verify it saves as `.webp`.

### Implementation for User Story 2

- [x] T005 [P] [US2] Rename AI photo file extension to `.webp` before upload in `frontend/src/app/admin/teachers/AdminTeachersPageClient.tsx`
- [x] T006 [P] [US2] Detect and enforce `.webp` extension based on base64 prefix header in `backend/src/NaderGorge.Application/Features/Admin/Commands/TeacherPhotoOps/UploadTeacherPhotoCommand.cs`

**Checkpoint**: User Stories 1 and 2 are functional.

---

## Phase 5: User Story 3 - Teacher updates own Profile Image & AI Photo (Priority: P2)

**Goal**: Convert and rename teacher profile images and AI photos to WebP client-side during Teacher Portal uploads.

**Independent Test**: Upload photos in Teacher Portal profile page, verify they save as `.webp`.

### Implementation for User Story 3

- [x] T007 [P] [US3] Rename profile image and AI photo file extensions to `.webp` before upload in `frontend/src/app/teacher/profile/TeacherProfilePageClient.tsx`

**Checkpoint**: All user stories are functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation updates and final linting

- [ ] T008 [P] Update documentation in `docs/frontend_plan.md` and `docs/backend_plan.md` to reflect completed status
- [ ] T009 Run lint checks on modified files in `frontend` via `npm run lint`

---

## Phase 7: Quality Gates & End-of-Phase Verification

**Purpose**: Execute mandatory quality gates (`clean-code-guard`, `test-guard`) and verify compilation, Docker health, and manual QA.

- [ ] T010 Run `clean-code-guard` against changed files to verify production code quality and resolve all findings
- [ ] T011 Run `test-guard` to audit any changed test files (if no test files changed, record that in achievements.md)
- [ ] T012 Verify backend project builds successfully via `dotnet build`
- [ ] T013 Verify frontend project builds successfully via `npm run build`
- [ ] T014 Run `docker compose config -q` to verify Docker configuration
- [ ] T015 Verify docker containers start cleanly with `make up` and are healthy via `make ps`
- [ ] T016 Perform manual QA checklist for all user stories and document results
- [ ] T017 Compile and output the final walkthrough report summarizing the completed work

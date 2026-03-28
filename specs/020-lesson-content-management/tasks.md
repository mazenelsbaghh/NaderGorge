---
description: "Task list for implementing lesson content management"
---

# Tasks: Lesson Content Management

**Input**: Design documents from `/specs/020-lesson-content-management/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create `app/admin/content/lessons/[id]/page.tsx` basic layout page for the Lesson Cockpit empty shell
- [x] T002 Update routing so clicking a Lesson from `SectionListManager` navigates to `/admin/content/lessons/[lessonId]`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 Create `GetLessonCockpitQuery` and `LessonCockpitDto` in `backend/src/NaderGorge.Application/Features/Content/Queries`
- [x] T004 Implement `GetLessonCockpit` in `backend/src/NaderGorge.API/Controllers/ContentController.cs` returning basic lesson info
- [x] T005 Update `frontend/src/services/admin-service.ts` to include `getLessonCockpit` endpoint

**Checkpoint**: Foundation ready - basic Lesson Cockpit page loads.

---

## Phase 3: User Story 1 - Add Videos to Lesson (Priority: P1) 🎯 MVP

**Goal**: Admins can add and list videos within a specific lesson.

**Independent Test**: Can be fully tested by successfully attaching a video URL (YouTube) to a lesson and seeing it listed.

### Implementation for User Story 1

- [x] T006 [US1] Create `CreateLessonVideoCommand` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`
- [x] T007 [US1] Implement POST endpoint `videos` in `backend/src/NaderGorge.API/Controllers/AdminController.cs` handling `CreateLessonVideoCommand`
- [x] T008 [US1] Add `LessonVideoList.tsx` and `AddVideoForm.tsx` components in `frontend/src/components/admin/`
- [x] T009 [US1] Integrate `LessonVideoList` and the video form into `frontend/src/app/admin/content/lessons/[id]/page.tsx`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Add Files/Documents to Lesson (Priority: P1)

**Goal**: Admins can attach document URLs to a specific lesson.

**Independent Test**: Can be fully tested by attaching a file URL to a lesson and viewing it in the resource list.

### Implementation for User Story 2

- [x] T010 [P] [US2] Create `CreateLessonResourceCommand` in `AdminContentCommands.cs`
- [x] T011 [US2] Implement POST endpoint `resources` in `AdminController.cs` handling `CreateLessonResourceCommand`
- [x] T012 [P] [US2] Add `addLessonResource` function to `frontend/src/services/admin-service.ts`
- [x] T013 [US2] Add `LessonResourceList.tsx` and `AddResourceForm.tsx` components in `frontend/src/components/admin/`
- [x] T014 [US2] Integrate the resource components into `frontend/src/app/admin/content/lessons/[id]/page.tsx`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Add Homework to Lesson (Priority: P2)

**Goal**: Admins can define and link homework instructions for a specific lesson.

**Independent Test**: Can be fully tested by creating a homework assignment within a lesson.

### Implementation for User Story 3

- [x] T015 [P] [US3] Ensure `CreateHomeworkCommand` is implemented in `backend/src/NaderGorge.Application/Features/Homework/Commands/HomeworkCommands.cs`
- [x] T016 [US3] Ensure `POST /api/homework` endpoint exists in `backend/src/NaderGorge.API/Controllers/HomeworkController.cs` and supports `LessonId`
- [x] T017 [P] [US3] Validate `frontend/src/services/homework-service.ts` includes `createHomework` supporting `LessonId`
- [x] T018 [US3] Add `LessonHomeworkList.tsx` and `AddHomeworkForm.tsx` components in `frontend/src/components/admin/`
- [x] T019 [US3] Integrate homework components into `frontend/src/app/admin/content/lessons/[id]/page.tsx`

**Checkpoint**: Homework flow works independently

---

## Phase 6: User Story 4 - Add Exams to Lesson (Priority: P2)

**Goal**: Admins can select an existing Exam and link it to the current lesson.

**Independent Test**: Fully tested by selecting an exam from a dropdown and seeing it linked.

### Implementation for User Story 4

- [x] T020 [P] [US4] Create `LinkLessonExamCommand` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`
- [x] T021 [US4] Implement `PUT /api/admin/lessons/{id}/exam` endpoint in `AdminController.cs`
- [x] T022 [P] [US4] Add `linkLessonExam` function to `admin-service.ts`
- [x] T023 [US4] Create Exam selection dropdown component in `frontend/src/components/admin/` picking from available exams.
- [x] T024 [US4] Integrate exam linking section into the Cockpit page `frontend/src/app/admin/content/lessons/[id]/page.tsx`

**Checkpoint**: All user stories should now be independently functional

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T025 Confirm all tabs and lists in Cockpit handle empty states elegantly.
- [x] T026 Add toast notifications for success/error handling across all forms.
- [x] T027 Ensure layout looks cohesive following "Editorial Scholar" Manrope and glassmorphism standards.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1
- **User Stories (Phase 3+)**: All depend on Phase 2
- **Polish (Final Phase)**: Depends on all user stories

### Parallel Opportunities

- US1, US2, US3, US4 can be assigned to different developers assuming Phase 2 is merged. Backend Commands/Endpoints can be developed independently of the frontend UI components via API Contracts.

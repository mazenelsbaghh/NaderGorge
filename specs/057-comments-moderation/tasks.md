# Tasks: Lesson Comments Moderation

**Input**: Design documents from `/specs/057-comments-moderation/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: No standalone test tasks are generated here because the feature spec did not explicitly request TDD. Validation is captured in each story's independent test and the final quickstart verification task.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. This file is intentionally written in a highly explicit, low-ambiguity style so a cheaper LLM can execute it with minimal inference.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Every task includes exact file paths

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the existing lesson and admin surfaces that this feature will extend.

- [X] T001 Review existing lesson entry points in `frontend/src/components/content/LessonViewer.tsx`, `frontend/src/services/content-service.ts`, `frontend/src/app/admin/content/lessons/[id]/page.tsx`, and `frontend/src/services/admin-service.ts` before changing code.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core schema and shared backend primitives that MUST exist before any user story work starts.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Create the `LessonCommentStatus` enum in `backend/src/NaderGorge.Domain/Enums/LessonCommentStatus.cs` with `Pending`, `Approved`, and `Rejected`.
- [X] T003 [P] Add the `LessonComment` entity and lesson navigation collection to `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs`.
- [X] T004 [P] Register `DbSet<LessonComment>` in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`.
- [X] T005 Update `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` to expose `LessonComments`, map the `lesson_comments` table, enforce required fields, configure lesson/author/reviewer relationships, and add useful indexes for `LessonId`, `Status`, and `CreatedAt`.
- [X] T006 Create the EF Core migration `AddLessonCommentsModeration` in `backend/src/NaderGorge.Infrastructure/Data/Migrations/` and update `backend/src/NaderGorge.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`.

**Checkpoint**: Database schema and shared domain primitives are ready; user story implementation can now begin.

---

## Phase 3: User Story 1 - Student posts a lesson comment (Priority: P1) 🎯 MVP

**Goal**: Show a comments section below the lesson video area, allow a student to submit a comment, and keep new comments pending until reviewed.

**Independent Test**: Open a lesson page, verify the comments block renders below the lesson video area, submit a valid comment, refresh the page, and confirm the public list still shows only approved comments while the student's own comment shows `Pending`.

### Implementation for User Story 1

- [X] T007 [P] [US1] Create `GetLessonCommentsQuery` and its DTOs in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonCommentsQuery.cs` to return only approved comments for a lesson after access validation.
- [X] T008 [P] [US1] Create `GetMyLessonCommentsQuery` and its DTOs in `backend/src/NaderGorge.Application/Features/Content/Queries/GetMyLessonCommentsQuery.cs` to return the current student's own comments and moderation statuses for a lesson.
- [X] T009 [P] [US1] Create `CreateLessonCommentCommand` and its handler in `backend/src/NaderGorge.Application/Features/Content/Commands/CreateLessonCommentCommand.cs` to validate lesson access, reject empty comments, trim the body, and save new comments as `Pending`.
- [X] T010 [US1] Update `backend/src/NaderGorge.API/Controllers/ContentController.cs` to add `GET /api/content/lessons/{lessonId}/comments`, `GET /api/content/lessons/{lessonId}/comments/mine`, and `POST /api/content/lessons/{lessonId}/comments`.
- [X] T011 [US1] Extend `frontend/src/services/content-service.ts` with lesson comment DTO types plus `getLessonComments`, `getMyLessonComments`, and `createLessonComment` API helpers.
- [X] T012 [P] [US1] Create the student comments UI in `frontend/src/components/content/LessonCommentsSection.tsx`, including approved comments list, empty state, submission textarea, submit button, and pending-status list for the current student's own comments.
- [X] T013 [US1] Integrate `frontend/src/components/content/LessonCommentsSection.tsx` into `frontend/src/components/content/LessonViewer.tsx` directly below the lesson video experience and before the later lesson sections.

**Checkpoint**: User Story 1 is fully functional and independently testable.

---

## Phase 4: User Story 2 - Teacher reviews lesson comments from the course page (Priority: P2)

**Goal**: Let teachers and admins review pending lesson comments from the existing lesson management page and approve or reject each comment.

**Independent Test**: Open the lesson management page as a teacher or admin, find a pending comment, approve it, verify it becomes public on the lesson page, then reject another pending comment and verify it remains hidden.

### Implementation for User Story 2

- [X] T014 [P] [US2] Create `GetLessonCommentsForModerationQuery` and moderation DTOs in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetLessonCommentsForModerationQuery.cs` to return lesson comments with student name, body, status, and review metadata.
- [X] T015 [P] [US2] Create `ApproveLessonCommentCommand` and its handler in `backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveLessonCommentCommand.cs` to resolve only pending comments and stamp reviewer metadata.
- [X] T016 [P] [US2] Create `RejectLessonCommentCommand` and its handler in `backend/src/NaderGorge.Application/Features/Admin/Commands/RejectLessonCommentCommand.cs` to resolve only pending comments and stamp reviewer metadata.
- [X] T017 [US2] Add staff moderation endpoints in `backend/src/NaderGorge.API/Controllers/AdminLessonCommentsController.cs` for `GET /api/admin/lessons/{lessonId}/comments`, `POST /api/admin/comments/{commentId}/approve`, and `POST /api/admin/comments/{commentId}/reject` with `Admin,Teacher` authorization.
- [X] T018 [US2] Extend `frontend/src/services/admin-service.ts` with moderation DTO types plus `getLessonCommentsForModeration`, `approveLessonComment`, and `rejectLessonComment` helpers.
- [X] T019 [P] [US2] Create the moderation UI component in `frontend/src/components/admin/LessonCommentsModerationTab.tsx` using the existing admin design language to show pending comments and approve/reject actions.
- [X] T020 [US2] Update `frontend/src/app/admin/content/lessons/[id]/page.tsx` to add a `comments` tab and render `LessonCommentsModerationTab` from the lesson management page.

**Checkpoint**: User Story 2 is fully functional and independently testable.

---

## Phase 5: User Story 3 - Teacher monitors comment state clearly (Priority: P3)

**Goal**: Make moderation status obvious so staff can distinguish pending, approved, and rejected comments quickly and understand what still requires action.

**Independent Test**: Open a lesson with comments in mixed states, filter or scan the moderation view, confirm pending/approved/rejected comments are visually distinct, and refresh the page to verify the latest status persists with reviewer and timestamp context.

### Implementation for User Story 3

- [X] T021 [P] [US3] Extend `backend/src/NaderGorge.Application/Features/Admin/Queries/GetLessonCommentsForModerationQuery.cs` to support optional status filtering and include reviewer display data plus review timestamps in every row.
- [X] T022 [P] [US3] Extend `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonCockpitQuery.cs` to return lesson comment summary counts needed for the lesson management header or tab badge.
- [X] T023 [US3] Update `frontend/src/services/admin-service.ts` to support passing a moderation status filter and consuming comment summary counts from the lesson cockpit payload.
- [X] T024 [P] [US3] Upgrade `frontend/src/components/admin/LessonCommentsModerationTab.tsx` to show explicit status chips, optional status filters, reviewed-by metadata, reviewed-at timestamps, and no-pending-comments empty states.
- [X] T025 [US3] Update `frontend/src/app/admin/content/lessons/[id]/page.tsx` to surface comment counts in the lesson page shell using the existing tab/stat-card patterns.

**Checkpoint**: User Story 3 is fully functional and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final quality work that touches multiple user stories.

- [X] T026 [P] Add audit log creation for student comment submission in `backend/src/NaderGorge.Application/Features/Content/Commands/CreateLessonCommentCommand.cs`.
- [X] T027 [P] Add audit log creation for teacher approval in `backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveLessonCommentCommand.cs`.
- [X] T028 [P] Add audit log creation for teacher rejection in `backend/src/NaderGorge.Application/Features/Admin/Commands/RejectLessonCommentCommand.cs`.
- [X] T029 Verify all student and moderation empty states, success toasts, and validation messages in `frontend/src/components/content/LessonCommentsSection.tsx` and `frontend/src/components/admin/LessonCommentsModerationTab.tsx`.
- [ ] T030 Run the manual quickstart verification in `specs/057-comments-moderation/quickstart.md` against `backend/src/NaderGorge.API/Controllers/ContentController.cs`, `backend/src/NaderGorge.API/Controllers/AdminController.cs`, `frontend/src/components/content/LessonViewer.tsx`, and `frontend/src/app/admin/content/lessons/[id]/page.tsx`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Blocks all user stories until the schema and entity layer are ready.
- **User Story 1 (Phase 3)**: Starts after Phase 2 and delivers the MVP.
- **User Story 2 (Phase 4)**: Starts after Phase 2 and depends functionally on the entity/migration work plus the student submission flow from US1 to provide pending comments to moderate.
- **User Story 3 (Phase 5)**: Starts after US2 because it refines the moderation surface and relies on moderation data already existing.
- **Polish (Phase 6)**: Starts after the desired user stories are complete.

### User Story Dependencies

- **US1**: Depends only on Foundational tasks.
- **US2**: Depends on Foundational tasks and benefits from US1 because moderation needs real pending comments to act on.
- **US3**: Depends on US2 because it enhances the moderation query and UI rather than introducing a separate workflow.

### Within Each User Story

- Query/command files come before controller wiring.
- Backend endpoint wiring comes before frontend service integration.
- Frontend service integration comes before page/component integration.
- UI integration is the final step inside each story.

### Parallel Opportunities

- In **Phase 2**, T002, T003, and T004 can run in parallel before T005 and T006.
- In **US1**, T007, T008, and T009 can run in parallel before T010, then T011 and T012 can proceed in parallel before T013.
- In **US2**, T014, T015, and T016 can run in parallel before T017, then T018 and T019 can run in parallel before T020.
- In **US3**, T021 and T022 can run in parallel before T023, then T024 can proceed before T025.
- In **Polish**, T026, T027, and T028 can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Backend read/write slices can be prepared in parallel:
Task: "Create GetLessonCommentsQuery in backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonCommentsQuery.cs"
Task: "Create GetMyLessonCommentsQuery in backend/src/NaderGorge.Application/Features/Content/Queries/GetMyLessonCommentsQuery.cs"
Task: "Create CreateLessonCommentCommand in backend/src/NaderGorge.Application/Features/Content/Commands/CreateLessonCommentCommand.cs"

# Frontend slices can then split cleanly:
Task: "Extend frontend/src/services/content-service.ts with lesson comment helpers"
Task: "Create frontend/src/components/content/LessonCommentsSection.tsx"
```

---

## Parallel Example: User Story 2

```bash
# Moderation backend primitives can be built together:
Task: "Create GetLessonCommentsForModerationQuery in backend/src/NaderGorge.Application/Features/Admin/Queries/GetLessonCommentsForModerationQuery.cs"
Task: "Create ApproveLessonCommentCommand in backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveLessonCommentCommand.cs"
Task: "Create RejectLessonCommentCommand in backend/src/NaderGorge.Application/Features/Admin/Commands/RejectLessonCommentCommand.cs"

# Frontend moderation slices can then split cleanly:
Task: "Extend frontend/src/services/admin-service.ts with moderation helpers"
Task: "Create frontend/src/components/admin/LessonCommentsModerationTab.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational schema and entity work.
3. Complete Phase 3: User Story 1.
4. Stop and validate the student lesson page flow before moving on.

### Incremental Delivery

1. Deliver **US1** first so the comments section exists under the lesson video and students can submit comments.
2. Deliver **US2** second so teachers can moderate the pending comments already being created.
3. Deliver **US3** third to improve staff speed and clarity without reopening core data flow decisions.
4. Finish with Phase 6 polish and quickstart validation.

### Cheap-LLM Execution Strategy

1. Execute tasks strictly in task-ID order unless a task is marked `[P]`.
2. Do not merge controller wiring before the corresponding query/command file exists.
3. Do not integrate UI into `LessonViewer.tsx` or the admin lesson page until the matching service helpers are present.
4. Prefer one task per file edit whenever possible; if a task names one file, avoid opportunistic extra refactors elsewhere.

---

## Notes

- All tasks follow the required checklist format.
- File paths are intentionally concrete to reduce LLM ambiguity.
- No task assumes replies, editing, deletion, notifications, or a separate global comments dashboard.
- Suggested MVP scope: **Phase 1 + Phase 2 + Phase 3 (US1 only)**.

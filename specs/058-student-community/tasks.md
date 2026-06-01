# Tasks: Student Community

**Input**: Design documents from `/specs/058-student-community/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/, quickstart.md

**Tests**: No standalone TDD test tasks are generated because the feature spec did not explicitly request a tests-first workflow. Independent verification is captured per user story and in the final quickstart validation task.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. This file is intentionally written in a highly explicit, low-ambiguity style so a cheaper LLM can execute it with minimal inference.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Every task includes exact file paths

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the existing code patterns this feature should mirror before changing code.

- [X] T001 Review moderated-content reference files `backend/src/NaderGorge.Application/Features/Content/Commands/CreateLessonCommentCommand.cs`, `backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveLessonCommentCommand.cs`, `backend/src/NaderGorge.API/Controllers/AdminLessonCommentsController.cs`, `frontend/src/services/content-service.ts`, and `frontend/src/components/admin/LessonCommentsModerationTab.tsx` before implementing community equivalents.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core schema and shared primitives that MUST exist before any user story work starts.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Create the `CommunityPostStatus` enum in `backend/src/NaderGorge.Domain/Enums/CommunityPostStatus.cs` with `Pending`, `Approved`, and `Rejected`.
- [X] T003 [P] Create the `CommunityPost` entity in `backend/src/NaderGorge.Domain/Entities/CommunityPost.cs` with author, body, moderation, and navigation properties.
- [X] T004 [P] Create the `CommunityPostComment` entity in `backend/src/NaderGorge.Domain/Entities/CommunityPostComment.cs` with post, author, body, and created-time fields.
- [X] T005 [P] Create the `CommunityPostLike` entity in `backend/src/NaderGorge.Domain/Entities/CommunityPostLike.cs` with post, user, and created-time fields plus uniqueness intent for one like per user per post.
- [X] T006 [P] Register `DbSet<CommunityPost>`, `DbSet<CommunityPostComment>`, and `DbSet<CommunityPostLike>` in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`.
- [X] T007 Update `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` to map `community_posts`, `community_post_comments`, and `community_post_likes`, configure relationships, required fields, indexes, and the unique like constraint.
- [X] T008 Create the EF Core migration for community tables in `backend/src/NaderGorge.Infrastructure/Data/Migrations/` and update `backend/src/NaderGorge.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`.

**Checkpoint**: Database schema and shared domain primitives are ready; user story implementation can now begin.

---

## Phase 3: User Story 1 - Student submits a community post (Priority: P1) 🎯 MVP

**Goal**: Let students open a dedicated community page, submit a post, and see their own post waiting for admin approval while the public feed still shows only approved posts.

**Independent Test**: Open the student community page, verify the approved feed and empty state render, submit a valid post, refresh the page, and confirm the student's own post shows `Pending` while it does not appear in the public feed.

### Implementation for User Story 1

- [X] T009 [P] [US1] Create `CommunityPostFeedDto` and `GetCommunityPostsQuery` in `backend/src/NaderGorge.Application/Features/Community/Queries/GetCommunityPostsQuery.cs` to return approved posts only with author name, like count, comment count, and current-user like state.
- [X] T010 [P] [US1] Create `MyCommunityPostDto` and `GetMyCommunityPostsQuery` in `backend/src/NaderGorge.Application/Features/Community/Queries/GetMyCommunityPostsQuery.cs` to return the current student's own submitted posts with moderation status.
- [X] T011 [P] [US1] Create `CreateCommunityPostCommand` in `backend/src/NaderGorge.Application/Features/Community/Commands/CreateCommunityPostCommand.cs` to validate access, trim the body, reject empty input, persist posts as `Pending`, and return the submission message.
- [X] T012 [US1] Create the student community endpoints `GET /api/community/posts`, `GET /api/community/posts/mine`, and `POST /api/community/posts` in `backend/src/NaderGorge.API/Controllers/CommunityController.cs`.
- [X] T013 [P] [US1] Add community post DTO types and API helpers to `frontend/src/services/community-service.ts` for `getCommunityPosts`, `getMyCommunityPosts`, and `createCommunityPost`.
- [X] T014 [P] [US1] Create the post composer UI in `frontend/src/components/student/CommunityPostComposer.tsx` with textarea, validation state, submit button, and success/pending feedback.
- [X] T015 [P] [US1] Create the current-student pending-posts panel in `frontend/src/components/student/MyCommunityPostsPanel.tsx` to show submitted posts and their statuses.
- [X] T016 [P] [US1] Create the approved-feed list UI in `frontend/src/components/student/CommunityFeed.tsx` to render approved posts, author names, timestamps, empty state, and engagement counts.
- [X] T017 [US1] Create the student page route in `frontend/src/app/student/community/page.tsx` to compose `CommunityPostComposer`, `MyCommunityPostsPanel`, and `CommunityFeed` using `frontend/src/services/community-service.ts`.

**Checkpoint**: User Story 1 is fully functional and independently testable.

---

## Phase 4: User Story 2 - Admin moderates submitted posts (Priority: P2)

**Goal**: Let admins review pending community posts from a dedicated admin surface and approve or reject them so only suitable posts reach the public feed.

**Independent Test**: Submit one or more student posts, open the admin community moderation page, approve one post and reject another, then confirm only the approved post becomes visible in the student feed.

### Implementation for User Story 2

- [X] T018 [P] [US2] Create `ModerationCommunityPostDto` and `GetCommunityPostsForModerationQuery` in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetCommunityPostsForModerationQuery.cs` to return moderation rows with student, post body, status, timestamps, like count, and comment count.
- [X] T019 [P] [US2] Create `ApproveCommunityPostCommand` in `backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveCommunityPostCommand.cs` to allow approval only from `Pending` state and stamp reviewer metadata.
- [X] T020 [P] [US2] Create `RejectCommunityPostCommand` in `backend/src/NaderGorge.Application/Features/Admin/Commands/RejectCommunityPostCommand.cs` to allow rejection only from `Pending` state and stamp reviewer metadata.
- [X] T021 [US2] Create the admin moderation endpoints `GET /api/admin/community/posts`, `POST /api/admin/community/posts/{postId}/approve`, and `POST /api/admin/community/posts/{postId}/reject` in `backend/src/NaderGorge.API/Controllers/AdminCommunityController.cs`.
- [X] T022 [P] [US2] Add moderation DTO types and API helpers to `frontend/src/services/admin-service.ts` for `getCommunityPostsForModeration`, `approveCommunityPost`, and `rejectCommunityPost`.
- [X] T023 [P] [US2] Create the moderation table UI in `frontend/src/components/admin/CommunityPostsModerationTable.tsx` using shared admin shell/table patterns to list posts and expose approve/reject actions.
- [X] T024 [US2] Create the admin route in `frontend/src/app/admin/community/page.tsx` to render `CommunityPostsModerationTable` inside the existing admin shell.

**Checkpoint**: User Story 2 is fully functional and independently testable.

---

## Phase 5: User Story 3 - Students engage with approved posts (Priority: P3)

**Goal**: Let students like approved posts and add flat comments to approved posts inside the community feed.

**Independent Test**: Open an approved post in the student community page, toggle a like, verify the count updates without duplicates, add a comment, and confirm the comment appears under the same approved post.

### Implementation for User Story 3

- [X] T025 [P] [US3] Create `CommunityPostCommentDto` and `GetCommunityPostCommentsQuery` in `backend/src/NaderGorge.Application/Features/Community/Queries/GetCommunityPostCommentsQuery.cs` to return comments only for approved posts in chronological order.
- [X] T026 [P] [US3] Create `CreateCommunityPostCommentCommand` in `backend/src/NaderGorge.Application/Features/Community/Commands/CreateCommunityPostCommentCommand.cs` to validate approved-post access, trim comment bodies, and save flat comments.
- [X] T027 [P] [US3] Create `ToggleCommunityPostLikeCommand` in `backend/src/NaderGorge.Application/Features/Community/Commands/ToggleCommunityPostLikeCommand.cs` to add or remove the current student's like and return the updated count/state.
- [X] T028 [US3] Extend `backend/src/NaderGorge.API/Controllers/CommunityController.cs` with `GET /api/community/posts/{postId}/comments`, `POST /api/community/posts/{postId}/comments`, and `POST /api/community/posts/{postId}/likes/toggle`.
- [X] T029 [P] [US3] Extend `frontend/src/services/community-service.ts` with `getCommunityPostComments`, `createCommunityPostComment`, and `toggleCommunityPostLike`.
- [X] T030 [P] [US3] Create the comments thread UI in `frontend/src/components/student/CommunityPostComments.tsx` to load post comments, show empty state, and submit a new flat comment.
- [X] T031 [P] [US3] Create the like button UI in `frontend/src/components/student/CommunityPostLikeButton.tsx` to toggle likes and reflect current-user state and count.
- [X] T032 [US3] Update `frontend/src/components/student/CommunityFeed.tsx` to integrate `CommunityPostLikeButton` and `CommunityPostComments` for each approved post.

**Checkpoint**: User Story 3 is fully functional and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final quality work that touches multiple user stories.

- [X] T033 [P] Add audit log creation for student post submission in `backend/src/NaderGorge.Application/Features/Community/Commands/CreateCommunityPostCommand.cs`.
- [X] T034 [P] Add audit log creation for admin post approval in `backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveCommunityPostCommand.cs`.
- [X] T035 [P] Add audit log creation for admin post rejection in `backend/src/NaderGorge.Application/Features/Admin/Commands/RejectCommunityPostCommand.cs`.
- [X] T036 [P] Add user-friendly empty states and validation/success messages in `frontend/src/components/student/CommunityPostComposer.tsx`, `frontend/src/components/student/MyCommunityPostsPanel.tsx`, `frontend/src/components/student/CommunityFeed.tsx`, `frontend/src/components/student/CommunityPostComments.tsx`, and `frontend/src/components/admin/CommunityPostsModerationTable.tsx`.
- [X] T037 Update exports or navigation wiring in `frontend/src/components/admin/index.ts` and any relevant admin/student navigation component so the new community pages are reachable from the existing UI shell.
- [ ] T038 Run the manual verification flow from `specs/058-student-community/quickstart.md` against `backend/src/NaderGorge.API/Controllers/CommunityController.cs`, `backend/src/NaderGorge.API/Controllers/AdminCommunityController.cs`, `frontend/src/app/student/community/page.tsx`, and `frontend/src/app/admin/community/page.tsx`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Blocks all user stories until schema and shared entities are ready.
- **User Story 1 (Phase 3)**: Starts after Phase 2 and delivers the MVP.
- **User Story 2 (Phase 4)**: Starts after Phase 2 and benefits from US1 because moderation needs pending posts to act on.
- **User Story 3 (Phase 5)**: Starts after Phase 2 but is best implemented after US1 because it extends approved posts already shown in the student feed.
- **Polish (Phase 6)**: Starts after the desired user stories are complete.

### User Story Dependencies

- **US1**: Depends only on the Foundational phase.
- **US2**: Depends on the Foundational phase and practically benefits from US1 creating pending posts.
- **US3**: Depends on the Foundational phase and on US1 because likes/comments attach to the approved posts displayed in the community feed.

### Within Each User Story

- Backend query/command files come before controller wiring.
- Backend controller wiring comes before frontend service helpers.
- Frontend service helpers come before component/page integration.
- Page integration is the final step inside each story.

### Parallel Opportunities

- In **Phase 2**, T002, T003, T004, T005, and T006 can run in parallel before T007 and T008.
- In **US1**, T009, T010, and T011 can run in parallel before T012, then T013, T014, T015, and T016 can run in parallel before T017.
- In **US2**, T018, T019, and T020 can run in parallel before T021, then T022 and T023 can run in parallel before T024.
- In **US3**, T025, T026, and T027 can run in parallel before T028, then T029, T030, and T031 can run in parallel before T032.
- In **Polish**, T033, T034, and T035 can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Backend student community primitives can be created together:
Task: "Create GetCommunityPostsQuery in backend/src/NaderGorge.Application/Features/Community/Queries/GetCommunityPostsQuery.cs"
Task: "Create GetMyCommunityPostsQuery in backend/src/NaderGorge.Application/Features/Community/Queries/GetMyCommunityPostsQuery.cs"
Task: "Create CreateCommunityPostCommand in backend/src/NaderGorge.Application/Features/Community/Commands/CreateCommunityPostCommand.cs"

# Frontend student feed slices can then split cleanly:
Task: "Add community helpers in frontend/src/services/community-service.ts"
Task: "Create frontend/src/components/student/CommunityPostComposer.tsx"
Task: "Create frontend/src/components/student/MyCommunityPostsPanel.tsx"
Task: "Create frontend/src/components/student/CommunityFeed.tsx"
```

---

## Parallel Example: User Story 2

```bash
# Moderation backend primitives can be built together:
Task: "Create GetCommunityPostsForModerationQuery in backend/src/NaderGorge.Application/Features/Admin/Queries/GetCommunityPostsForModerationQuery.cs"
Task: "Create ApproveCommunityPostCommand in backend/src/NaderGorge.Application/Features/Admin/Commands/ApproveCommunityPostCommand.cs"
Task: "Create RejectCommunityPostCommand in backend/src/NaderGorge.Application/Features/Admin/Commands/RejectCommunityPostCommand.cs"

# Frontend moderation slices can then split cleanly:
Task: "Extend frontend/src/services/admin-service.ts with community moderation helpers"
Task: "Create frontend/src/components/admin/CommunityPostsModerationTable.tsx"
```

---

## Parallel Example: User Story 3

```bash
# Community engagement backend primitives can be built together:
Task: "Create GetCommunityPostCommentsQuery in backend/src/NaderGorge.Application/Features/Community/Queries/GetCommunityPostCommentsQuery.cs"
Task: "Create CreateCommunityPostCommentCommand in backend/src/NaderGorge.Application/Features/Community/Commands/CreateCommunityPostCommentCommand.cs"
Task: "Create ToggleCommunityPostLikeCommand in backend/src/NaderGorge.Application/Features/Community/Commands/ToggleCommunityPostLikeCommand.cs"

# Frontend engagement slices can then split cleanly:
Task: "Extend frontend/src/services/community-service.ts with comment and like helpers"
Task: "Create frontend/src/components/student/CommunityPostComments.tsx"
Task: "Create frontend/src/components/student/CommunityPostLikeButton.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational schema and entity work.
3. Complete Phase 3: User Story 1.
4. Stop and validate the student post-submission flow before moving on.

### Incremental Delivery

1. Deliver **US1** first so students can open the community page and submit moderated posts.
2. Deliver **US2** second so admins can review the pending posts already being created.
3. Deliver **US3** third so approved posts become interactive with likes and comments.
4. Finish with Phase 6 polish and quickstart validation.

### Cheap-LLM Execution Strategy

1. Execute tasks strictly in task-ID order unless a task is marked `[P]`.
2. Prefer one task per file edit whenever possible; if a task names one file, avoid opportunistic refactors in unrelated files.
3. Do not create frontend page integration before the matching backend endpoint and frontend service helper exist.
4. Do not implement likes/comments before approved posts can already be read from the student feed.
5. If a task depends on a file that does not exist yet, create only that file and the minimum supporting imports needed for the task.

---

## Notes

- All tasks follow the required checklist format.
- File paths are intentionally concrete to reduce LLM ambiguity.
- No task assumes notifications, editing, deletion, attachments, segmentation by course/grade, or private groups.
- Suggested MVP scope: **Phase 1 + Phase 2 + Phase 3 (US1 only)**.

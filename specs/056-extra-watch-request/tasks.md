---
description: "Task list template for feature implementation"
---

# Tasks: Extra Watch Request

**Input**: Design documents from `/specs/056-extra-watch-request/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are OPTIONAL - only include them if explicitly requested in the feature specification.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup & Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T001 Create ExtraWatchRequest entity in backend/src/NaderGorge.Domain/Entities/ExtraWatchRequest.cs
- [x] T002 Map DbSet for ExtraWatchRequest in backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs
- [x] T003 Create DbMigration for ExtraWatchRequest

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 2: User Story 1 - Requesting Extra Views (Priority: P1) 🎯 MVP

**Goal**: As a student, I want to click a button to request an additional view, so that I can continue studying the material with support's approval.

**Independent Test**: Can be fully tested by locking a video for a user, verifying the button appears, clicking the button, and ensuring a request is created in the backend.

### Implementation for User Story 1

- [ ] T004 [P] [US1] Create CreateExtraWatchRequestCommand.cs in backend/src/NaderGorge.Application/Features/Student/Commands/
- [ ] T005 [P] [US1] Create CheckExtraWatchStatusQuery.cs in backend/src/NaderGorge.Application/Features/Student/Queries/
- [ ] T006 [US1] Expose endpoints (`POST /video-session/{lessonVideoId}/request-extra`, `GET /video-session/{lessonVideoId}/request-status`) in backend/src/NaderGorge.API/Controllers/StudentVideoController.cs
- [ ] T007 [P] [US1] Add `requestExtraWatch` and `getExtraWatchStatus` methods in frontend/src/services/video-session-service.ts
- [ ] T008 [US1] Update `SecureVideoPlayer.tsx` frontend/src/components/video/SecureVideoPlayer.tsx locked state UI to fetch `getExtraWatchStatus` and show "Request Extra Watch" button, "Pending", or "Rejected" states.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. Student can create the request.

---

## Phase 3: User Story 2 - Admin Management of Requests (Priority: P1)

**Goal**: As an admin, I want to view a list of all extra watch requests submitted by students and approve or reject them.

**Independent Test**: Can be fully tested by creating dummy requests in the database and checking if the admin dashboard lists them correctly, and if approving them alters the student's watch limits.

### Implementation for User Story 2

- [ ] T009 [P] [US2] Create GetWatchRequestsQuery.cs in backend/src/NaderGorge.Application/Features/Admin/Queries/
- [ ] T010 [P] [US2] Create ApproveWatchRequestCommand.cs in backend/src/NaderGorge.Application/Features/Admin/Commands/ (Must set IsLocked=false and WatchCount=MaxWatchCount-1 in VideoWatchEvent, and ResolvedAt=UtcNow, Status=Approved)
- [ ] T011 [P] [US2] Create RejectWatchRequestCommand.cs in backend/src/NaderGorge.Application/Features/Admin/Commands/
- [ ] T012 [US2] Expose endpoints (`GET /watch-requests`, `POST /watch-requests/{id}/approve`, `POST /watch-requests/{id}/reject`) in backend/src/NaderGorge.API/Controllers/AdminController.cs
- [ ] T013 [P] [US2] Update frontend/src/services/admin-service.ts to include methods for watch requests APIs
- [ ] T014 [US2] Create new admin page frontend/src/app/admin/watch-requests/page.tsx to list and manage these requests
- [ ] T015 [US2] Add navigation link to frontend/src/components/admin/AdminShellChrome.tsx sidebar for the Watch Requests dashboard

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently. Admin can manage requests.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T016 [P] Code cleanup and refactoring
- [ ] T017 [P] Verify quickstart.md flows accurately map to real implementation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: Can start immediately - BLOCKS all user stories
- **User Stories (Phase 2 & 3)**: All depend on Foundational phase completion
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- Models within a story marked [P] can run in parallel

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Each story adds value without breaking previous stories

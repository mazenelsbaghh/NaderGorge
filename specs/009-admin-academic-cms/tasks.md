---
description: "Task list for Phase 2.5 Admin CMS for Homework and Assistants"
---

# Tasks: Phase 2.5 Admin CMS for Homework and Assistants

**Input**: Design documents from `/specs/009-admin-academic-cms/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

There are no extensive shared architecture changes required since this phase extends existing NaderGorge.API and Next.js frontend logic directly.

- [ ] T001 Verify standard Admin authentication flow works in `frontend/src/middleware.ts` for any new `admin/content` sub-paths if necessary.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T002 Ensure the `IdentityUserRole` relationship is accessible within the backend user query mapping logic if it's not already eagerly loaded for the users list.

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Admin can manage Assistant roles (Priority: P1) 🎯 MVP

**Goal**: Admins need to be able to create or promote existing users to have the "Assistant" role directly from the User Management dashboard.

**Independent Test**: Can be fully tested by taking a raw phone number, assigning the assistant role, and logging in with that number to see the Assistant Dashboard.

### Implementation for User Story 1

- [ ] T003 [P] [US1] Create `UpdateUserRoleCommand.cs` in `backend/src/NaderGorge.Application/UseCases/Admin/Users/` using `UserManager` to assign Roles.
- [ ] T004 [P] [US1] Implement mapping endpoint `PUT /api/v1/admin/users/{userId}/roles` in `backend/src/NaderGorge.API/Controllers/AdminController.cs`.
- [ ] T005 [P] [US1] Create React component `UserRoleDropdown.tsx` in `frontend/src/components/admin/` to provide a Shadcn/UI selection menu.
- [ ] T006 [US1] Integrate `UserRoleDropdown` into the Users data table in `frontend/src/app/admin/users/page.tsx` (depends on T005, T004).

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently.

---

## Phase 4: User Story 2 - Admin can add Homework to Lessons (Priority: P1)

**Goal**: Admins and Content Managers need to be able to attach Textual and MCQ Homework questions to specific lessons directly from the Lesson Detail page.

**Independent Test**: Can be fully tested by navigating to a lesson in the Admin panel, adding a mandatory essay homework, saving it, and verifying it appears in the student's lesson view.

### Implementation for User Story 2

- [ ] T007 [P] [US2] Create CQRS Command `AttachHomeworkCommand.cs` in `backend/src/NaderGorge.Application/UseCases/Admin/Content/` mapped to the API Contract payload.
- [ ] T008 [P] [US2] Implement endpoint `POST /api/v1/admin/content/lessons/{lessonId}/homework` inside `backend/src/NaderGorge.API/Controllers/ContentController.cs`.
- [ ] T009 [P] [US2] Develop frontend form `HomeworkTabEditor.tsx` in `frontend/src/components/admin/content/` to manipulate questions arrays.
- [ ] T010 [US2] Inject the `HomeworkTabEditor.tsx` inside the existing tabs layout at `frontend/src/app/admin/content/packages/[packageId]/sections/[sectionId]/lessons/[lessonId]/page.tsx` (depends on T009).

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently.

---

## Phase 5: User Story 3 - Admin can easily copy the Parent Report Link (Priority: P2)

**Goal**: Admins need a simple, one-click button in the User Management list to copy the Parent Report link for any specific student, to share it easily via WhatsApp.

**Independent Test**: Can be fully tested by generating the link, clicking copy, and pasting it into the browser to successfully load the parent report.

### Implementation for User Story 3

- [ ] T011 [P] [US3] Create pure client component `CopyParentLinkButton.tsx` in `frontend/src/components/admin/` utilizing `navigator.clipboard`.
- [ ] T012 [US3] Add the `CopyParentLinkButton` component to the existing action cells inside `frontend/src/app/admin/users/page.tsx`.

**Checkpoint**: All user stories should now be independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T013 [P] Add error handling Toast notifications for both the `UserRoleDropdown` and `HomeworkTabEditor` network requests using `react-hot-toast` or similar.
- [ ] T014 [P] Verify that tests defined in `admin-content.spec.ts` are robust or add a manual test loop for adding Homework.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - No dependencies on other stories

### Parallel Opportunities

- The Backend Commands and API Endpoints (T003, T004, T007, T008) can be developed independently of the frontend components.
- The `CopyParentLinkButton` (T011) can be implemented completely independently of everything else.

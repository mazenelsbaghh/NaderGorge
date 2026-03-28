---
description: "Task list for Package Profile and Term Management feature"
---

# Tasks: Package Profile and Term Management

**Input**: Design documents from `/specs/017-package-profile-management/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/api.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create directory structure `frontend/src/app/admin/content/packages/[id]`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core api clients and backend handlers that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Update `frontend/src/services/curriculum-service.ts` to include `getPackageById` client method.
- [x] T003 Update `frontend/src/services/curriculum-service.ts` to include `updatePackage` client method.
- [x] T004 Update `frontend/src/services/curriculum-service.ts` to include `createTerm` client method.
- [x] T005 [P] Implement `NaderGorge.Application/Features/Curriculum/Queries/GetPackageByIdQuery.cs` and Handler.
- [x] T006 [P] Implement `NaderGorge.Application/Features/Curriculum/Commands/UpdatePackageCommand.cs` and Handler.
- [x] T007 [P] Implement `NaderGorge.Application/Features/Curriculum/Commands/AddTermCommand.cs` and Handler.
- [x] T008 Update `NaderGorge.API/Controllers/PackagesController.cs` to expose GET `/{id}` and PUT `/{id}`.
- [x] T009 Update `NaderGorge.API/Controllers/PackagesController.cs` to expose POST `/{id}/terms` (or via `TermsController.cs`).

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Package Profile Navigation (Priority: P1) 🎯 MVP

**Goal**: As an administrator, I want to click on a package in the content dashboard and be navigated to its dedicated profile page, so I can view all its details in one centralized location.

**Independent Test**: Navigate directly to `/admin/content/packages/[id]` and verify package metadata loads correctly via the new components.

### Implementation for User Story 1

- [x] T010 [US1] Create basic layout and data fetching in `frontend/src/app/admin/content/packages/[id]/page.tsx` using `AdminShellChrome`.
- [x] T011 [US1] Update `frontend/src/app/admin/content/page.tsx` to link existing package cards/rows to their dedicated profile page `.../packages/[id]`.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Term Management Within Package (Priority: P2)

**Goal**: As an administrator, I want to be able to add, view, and manage Terms directly from the Package Profile page.

**Independent Test**: Open a package profile, click "Add Term", fill out the form, submit, and see the term appear immediately in the package's term list.

### Implementation for User Story 2

- [x] T012 [P] [US2] Create component `TermListManager.tsx` in `frontend/src/components/admin/TermListManager.tsx` to list existing terms.
- [x] T013 [P] [US2] Create component `AddTermForm.tsx` in `frontend/src/components/admin/AddTermForm.tsx` utilizing shared input components.
- [x] T014 [US2] Integrate `TermListManager` and `AddTermForm` into the content tab of `frontend/src/app/admin/content/packages/[id]/page.tsx`.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Unified Settings via Shared Components (Priority: P3)

**Goal**: As an administrator, I want to manage all package settings from its profile using standard, shared interface components.

**Independent Test**: Edit package details like status, price, and title from the profile's settings tab, and verify changes stick upon saving.

### Implementation for User Story 3

- [x] T015 [P] [US3] Create `PackageDetailsForm.tsx` in `frontend/src/components/admin/PackageDetailsForm.tsx` using `AdminStatCard` glass styling.
- [x] T016 [US3] Integrate `PackageDetailsForm` into the settings tab of `frontend/src/app/admin/content/packages/[id]/page.tsx`.

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T017 Review Arabic numerals and generic strings to ensure they map to English digits and use the proper design system colors.
- [x] T018 Verify JWT role-based access control works properly locking out students from the new endpoints in the backend and frontend middleware.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - Sequential priority execution (P1 → P2 → P3) or Parallel execution where components don't overlap.
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### Parallel Opportunities

- All Foundational Backend commands (T005, T006, T007) and Frontend service updates (T002, T003, T004) can be built concurrently.
- Within US2, `TermListManager` (T012) and `AddTermForm` (T013) can be built concurrently.
- Within US3, `PackageDetailsForm` (T015) can be built independently before being wired into the page layout.

### Implementation Strategy

#### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Verify package profile navigation works correctly.
5. Deploy/demo if ready

#### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test package routing → Deploy/Demo (MVP!)
3. Add User Story 2 → Test term creation within profile → Deploy/Demo
4. Add User Story 3 → Test editing package configuration → Deploy/Demo

---
description: "Task list for Admin Student Details Profile implementation"
---

# Tasks: Admin Student Details Profile

**Input**: Design documents from `/specs/011-admin-student-profile/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api-contracts.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Scaffold `frontend/src/app/admin/users/[id]/page.tsx` basic layout with `AdminShellChrome` components

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Create Domain Entity `AdminAuditLog` (Skipped: Reusing existing `AuditLog`)
- [x] T003 Create Entity Framework Migration for Audit Logs (Skipped: Existing `AuditLog` covers needs)
- [x] T004 Implement `StudentProfileExtendedDto` and nested DTOs in Application layer

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Comprehensive Student Overview (Priority: P1) 🎯 MVP

**Goal**: Display comprehensive student profile containing demographic, packaged, and devices data without N+1 queries.

**Independent Test**: Admin can click on a student and successfully load their read-only profile tabs (Overview, Packages, Devices).

### Implementation for User Story 1

- [x] T019 End-to-end testing of `GetStudentProfileDetailQuery` and all quick actions from UIin `backend/src/NaderGorge.Application/Admin/Queries/GetStudentProfileDetailQuery.cs`
- [x] T006 [US1] Expose `GET /api/admin/users/students/{userId}/profile` in `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [x] T007 [US1] Add `getStudentProfile` to `frontend/src/services/admin-service.ts`
- [x] T008 [US1] Implement Overview, Packages, and Devices Tabs UI in `frontend/src/app/admin/users/[id]/page.tsx` integrating fetched data

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Video Views Overrides and Adjustments (Priority: P1)

**Goal**: Allow admins to safely adjust individual student watch limits on specific videos.

**Independent Test**: Admin can grant +2 views to a specific video on a student's profile and the student's access is immediately updated.

### Implementation for User Story 2

- [x] T009 [P] [US2] Create `OverrideVideoLimitCommand` MediatR Command and Handler in `backend/src/NaderGorge.Application/Admin/Commands/OverrideVideoLimitCommand.cs`
- [x] T010 [US2] Expose `POST /api/admin/users/students/{userId}/overrides` in `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [x] T011 [US2] Add `overrideVideoLimit` to `frontend/src/services/admin-service.ts`
- [x] T012 [US2] Implement Overrides Tab and Add Override `AdminModal` in `frontend/src/app/admin/users/[id]/page.tsx`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Administrative Actions & Account Controls (Priority: P2)

**Goal**: Provide centralized quick-actions to disconnect sessions, edit gamification points, or deactivate a student account.

**Independent Test**: Admin can open the profile, click "Disconnect All Devices", and the request succeeds smoothly.

### Implementation for User Story 3

- [x] T013 [P] [US3] Create `ToggleStudentSystemAccessCommand` in `backend/src/NaderGorge.Application/Admin/Commands/ToggleStudentSystemAccessCommand.cs`
- [x] T014 [P] [US3] Create `AdjustGamificationPointsCommand` in `backend/src/NaderGorge.Application/Admin/Commands/AdjustGamificationPointsCommand.cs`
- [x] T015 [P] [US3] Create `DisconnectStudentDeviceCommand` in `backend/src/NaderGorge.Application/Admin/Commands/DisconnectStudentDeviceCommand.cs`
- [x] T016 [US3] Expose PATCH status, POST gamification, and DELETE device endpoints in `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [x] T017 [US3] Add quick-action methods to `frontend/src/services/admin-service.ts`
- [x] T018 [US3] Implement Account Controls (Deactivate, Disconnect Device, Adjust Points) in UI modals inside `frontend/src/app/admin/users/[id]/page.tsx`

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T019 Implement MediatR Pipeline Behavior to intercept Action commands and write to `AdminAuditLog` seamlessly
- [x] T020 Implement Audit Trail Tab in UI to display read-only timeline of recorded actions for the student
- [x] T021 Validate Profile Load Time (< 1.5s) and responsive alignment across AdminShell components

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - No dependencies
- **User Story 2 (P1)**: Can start after Foundational - Depends on US1's shared UI shell (`[id]/page.tsx`)
- **User Story 3 (P2)**: Can start after Foundational - Depends on US1's shared UI shell

### Parallel Opportunities

- Commands in Phase 5 (T013, T014, T015) can be built in parallel.
- Frontend and backend integration for each independent story can be built in parallel if stubbed correctly.

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and 2 Setup
2. Complete Phase 3 (US1) 
3. Validate loading of the aggregate profile
4. Proceed to action additions incrementally (US2 -> US3 -> Polish)

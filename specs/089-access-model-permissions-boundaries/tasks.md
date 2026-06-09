# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Phase 1 - Access Model, Staff Surfaces, and Permission Boundaries

**Input**: Design documents from `/specs/089-access-model-permissions-boundaries/`
**Prerequisites**: plan.md (required), spec.md (required)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files/commands, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Path Conventions

- **Web app**: `backend/src/`, `frontend/src/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and specs verification

- [x] T001 Verify specs directory and configuration file `.specify/feature.json` points to the correct directory path.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core model extensions that MUST be complete before user stories can start

- [x] T002 Backend: Update `RoleType` enum in `backend/src/NaderGorge.Domain/Enums/RoleType.cs` to add `Supervisor = 7` and `Staff = 8`.
- [x] T003 Backend: Update `Seeder.cs` in `backend/src/NaderGorge.Infrastructure/Data/Seeder.cs` to check and seed the `Supervisor` and `Staff` roles dynamically on database initialization.
- [x] T004 Backend: Create an EF Core migration to seed the `Supervisor` and `Staff` roles into the `roles` table. Run `make migrate-add NAME=AddSupervisorAndStaffRoles`.

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Add Supervisor and Staff System Roles (Priority: P1) 🎯 MVP

**Goal**: Expose user permissions in the auth store and claims token response.

**Independent Test**: Verify login response payload contains permissions array.

### Implementation for User Story 1

- [x] T005 [P] [US1] Backend: Update `UserDto` in `backend/src/NaderGorge.Application/Features/Auth/Commands/LoginCommand.cs` to include `string[] Permissions`.
- [x] T006 [US1] Backend: Update `LoginCommandHandler` in `backend/src/NaderGorge.Application/Features/Auth/Commands/LoginCommand.cs` to retrieve user permissions from role `PermissionsJson` and map it to `UserDto`.
- [x] T007 [P] [US1] Backend: Update `UserDto` in `backend/src/NaderGorge.Application/Features/Auth/Commands/RefreshTokenCommand.cs` to include `string[] Permissions`.
- [x] T008 [US1] Backend: Update `RefreshTokenCommandHandler` in `backend/src/NaderGorge.Application/Features/Auth/Commands/RefreshTokenCommand.cs` to retrieve user permissions from role `PermissionsJson` and map it to `UserDto`.
- [x] T009 [US1] Frontend: Update `User` interface in `frontend/src/stores/auth-store.ts` to include `permissions: string[];`.

**Checkpoint**: User Story 1 claims flow is functional.

---

## Phase 4: User Story 2 - Register Permissions for HR, CRM, Finance, Tasks, Chat, Media & Audits (Priority: P1)

**Goal**: Expose new permissions in the settings page role editor.

**Independent Test**: Roles management page displays checkable list of new permissions.

### Implementation for User Story 2

- [x] T010 [US2] Frontend: Update `PERMISSION_DEFINITIONS` in `frontend/src/app/admin/settings/page.tsx` to include the definitions for `hr.manage`, `tasks.manage`, `chat.manage`, `crm.manage`, `payments.manage`, `media.manage`, `finance.manage`, and `reports.manage`.

**Checkpoint**: Role creator exposes new permission tags.

---

## Phase 5: User Story 3 - Role-Based Interface and Menu Restrictions (Priority: P2)

**Goal**: Hide sidebar links and block direct route access for unauthorized staff.

**Independent Test**: Login as restricted user shows filtered sidebar and blocks unauthorized route navigation.

### Implementation for User Story 3

- [x] T011 [US3] Frontend: Add optional `permission` field to menu item interface in `frontend/src/packages/admin/navigation.tsx`.
- [x] T012 [US3] Frontend: Implement a `useHasPermission` hook in `frontend/src/hooks/useHasPermission.ts` to check if a user has a specific permission or belongs to `Admin`/`Teacher`.
- [x] T013 [US3] Frontend: Update `AdminLayout` in `frontend/src/app/admin/layout.tsx` to filter `adminMenuItems` using `useHasPermission` before rendering the `Sidebar`.
- [x] T014 [US3] Frontend: Create an unauthorized access screen at `frontend/src/app/admin/unauthorized/page.tsx`.
- [x] T015 [US3] Frontend: Update route guard or layout to verify permission of active route and redirect to `/admin/unauthorized` if lacking.

**Checkpoint**: Access control is fully functional at navigation and routing layers.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Performance, verification, and code cleanup

- [x] T016 Verify build/lint status across backend and frontend workspaces.
- [x] T017 Update permissions documentation in design records.

---

## Phase 7: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment before starting the next phase.

- [x] T018 Run backend build/test and frontend lint/build.
- [x] T019 Run DB migration updates.
- [x] T020 Compile and output the final report.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories.
- **User Stories (Phase 3+)**: All depend on Foundational phase completion.
- **Polish (Final Phase)**: Depends on all desired user stories being complete.
- **End-of-Phase Verification**: Depends on all implementation and polish tasks.

# Tasks: fix-forms-id-mismatch

**Input**: Design documents from `/specs/087-fix-forms-id-mismatch/`
**Prerequisites**: plan.md (required), spec.md (required)

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`specs/087-fix-forms-id-mismatch/spec.md` generated)
- [x] Phase 2: Technical Planning (`specs/087-fix-forms-id-mismatch/plan.md` generated)
- [x] Phase 3: Detailed Task Breakdown (This `tasks.md` file generated)

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure (None needed as project is already set up)

- [x] T001 Verify project structure and load config files

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [x] T002 Verify local repository build and lint state

---

## Phase 3: User Story 1 - Admin can update custom form details (Priority: P1) 🎯 MVP

**Goal**: Enable updating form details and toggling active status successfully by passing the form ID inside the request body payload.

**Independent Test**:
- Toggle the active state of a custom form from the admin forms list.
- Edit form title or description on `/admin/forms/{id}/edit` and click save.
- Verify both request payloads contain the `id` field and complete with 200 OK.

### Implementation for User Story 1

- [x] T003 [US1] Modify the `updateAdminForm` function in `frontend/src/services/forms-service.ts` to include the `id` property inside the PUT request body payload.

---

## Phase 4: User Story 2 - Admin can update submission status (Priority: P2)

**Goal**: Enable updating form submission status successfully by passing the `submissionId` inside the request body payload.

**Independent Test**:
- Open the submissions list for any custom form.
- Click to change a submission's status or add admin notes and click save.
- Verify the request payload contains the `submissionId` field and completes with 200 OK.

### Implementation for User Story 2

- [x] T004 [US2] Modify the `updateSubmissionStatus` function in `frontend/src/services/forms-service.ts` to include the `submissionId` property inside the PUT request body payload.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Verification, final documentation updates, and cleanup

- [x] T005 Run frontend build and linter checks to ensure no TypeScript or syntax errors
- [x] T006 Update the backend master plan documentation file `docs/backend_plan.md` and frontend master plan documentation file `docs/frontend_plan.md` with the completed changes.
- [x] T007 Run the quickstart verification steps to validate fixes in the dev environment.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup & Foundational**: Completed.
- **User Story 1 (P1)**: No dependencies, can be worked on immediately.
- **User Story 2 (P2)**: Can be worked on in parallel with or after US1.
- **Polish (Final Phase)**: Depends on US1 and US2 being completed.

### Parallel Opportunities
- T003 and T004 are in the same file but can technically be updated together in one edit.

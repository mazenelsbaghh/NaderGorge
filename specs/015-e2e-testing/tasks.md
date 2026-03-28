---
description: "Task list for E2E Testing and Verification"
---

# Tasks: E2E Testing and Verification

**Input**: Design documents from `/specs/015-e2e-testing/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure for E2E Tests.

- [x] T001 Verify Playwright dependencies in `frontend/package.json` and initialize playwright config if necessary
- [x] T002 Create E2E test directories `frontend/tests/e2e/` and `frontend/tests/fixtures/`
- [x] T003 [P] Add Playwright run commands to `frontend/package.json` scripts

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story test can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Create Global Setup fixture in `frontend/tests/fixtures/global-setup.ts` to coordinate admin authentication before tests run.
- [x] T005 [P] Implement specialized E2E helper methods for randomizing student phone numbers and bypass OTPs in `frontend/tests/fixtures/auth-helpers.ts`
- [x] T006 [P] Expose necessary `.env.test` file loading mechanism inside `frontend/playwright.config.ts`

**Checkpoint**: Foundation ready - user story test implementations can now begin in parallel

---

## Phase 3: User Story 1 - E2E Code Generation and Activation (Priority: P1) 🎯 MVP

**Goal**: An Administrator needs to easily generate large batches of codes for different levels of the platform and a new student receives one of the codes and uses it to automatically join logic flows.

**Independent Test**: Running the specific spec file isolated from the suite `npx playwright test tests/e2e/codes-wallet.spec.ts`

### Implementation for User Story 1

- [x] T007 [P] [US1] Create test file `frontend/tests/e2e/codes-wallet.spec.ts`
- [x] T008 [US1] Implement `Admin Bulk Generation` test block to generate a 500 EGP balance code using the API context inside `codes-wallet.spec.ts`
- [x] T009 [US1] Implement `Student Registration` browser journey in `codes-wallet.spec.ts` filling the form using `StudentTestContext` randomization.
- [x] T010 [US1] Implement `Code Redemption` browser journey inserting the generated balance code and verifying the UI updates to show 500 EGP.
- [x] T011 [US1] Implement `Wallet Direct Purchase` browser journey to buy a package and assert success without double-spending.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Admin Profiles & Deep Search (Priority: P2)

**Goal**: Administrators need to smoothly filter through hundreds of users and inspect granular details about a specific student's demographics without continuously traversing through slow page navigation jumps.

**Independent Test**: Running `npx playwright test tests/e2e/admin-users.spec.ts`

### Implementation for User Story 2

- [x] T012 [P] [US2] Create test file `frontend/tests/e2e/admin-users.spec.ts`
- [x] T013 [US2] Implement test to navigate to `/admin/users` as an authorized Admin.
- [x] T014 [US2] Implement filter testing logic (Select "Secondary Stage") and assert the table refreshes correctly in `admin-users.spec.ts`
- [x] T015 [US2] Implement row expansion testing logic clicking on the student generated in US1 and asserting metadata visibility (e.g. Birthdate, StudentCode).

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T016 [P] Update `frontend/README.md` to point to E2E execution instructions.
- [x] T017 Review and stabilize Playwright timeouts to prevent flaky drops in `frontend/playwright.config.ts`.
- [x] T018 Integrate test report capturing (Screenshots/Traces on failure) in `frontend/playwright.config.ts`.
- [x] T019 Run quickstart.md validation to ensure end-to-end execution passes headless cleanly.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Technically independent but can assert on the User generated in US1 if ordered sequentially.

### Within Each User Story

- Test scaffold setup first.
- API generation / Data seeding before Browser tests.
- Core implementation before integration.
- Story complete before moving to next priority.

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1 & 2

```bash
# Developer A focuses on Code Hierarchy Tests:
Task: "Implement `Admin Bulk Generation` test block to generate a 500 EGP balance code using the API context inside `codes-wallet.spec.ts`"

# Developer B focuses on Admin Component Tests:
Task: "Implement filter testing logic (Select "Secondary Stage") and assert the table refreshes correctly in `admin-users.spec.ts`"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently via Playwright UI.

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → E2E Verified Payment flows
3. Add User Story 2 → Test independently → E2E Verified Admin visibility
4. Each story adds value without breaking previous stories.

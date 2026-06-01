# Tasks: Package-Specific Code Page Profiles

**Input**: Design documents from `/Users/mazenelsbagh/mazen mac/apps/nader gorge/specs/060-package-code-profiles/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/package-code-page-profile.md`, `quickstart.md`

**Tests**: Include focused backend coverage for profile validation/fallback logic and Playwright coverage for admin/student flows because the plan and quickstart explicitly require both.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`)
- Each task includes exact file path(s)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare feature-specific test and implementation scaffolding

- [X] T001 Create backend test project scaffold in `backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj`
- [X] T002 [P] Create feature E2E spec scaffold in `frontend/tests/e2e/package-code-profiles.spec.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core persistence and shared contracts that block all user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Create package code page profile domain types in `backend/src/NaderGorge.Domain/Entities/PackageCodePageProfile.cs`
- [X] T004 Update app DB contract and EF registration for package code profiles in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` and `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [X] T005 Add EF Core migration for `package_code_page_profiles` in `backend/src/NaderGorge.Infrastructure/Data/Migrations/`
- [X] T006 [P] Add shared frontend DTOs and service method signatures for code profile APIs in `frontend/src/services/admin-service.ts` and `frontend/src/services/content-service.ts`

**Checkpoint**: Persistence model, migration, and service contracts are ready; user story work can start.

---

## Phase 3: User Story 1 - Customize the code page per package (Priority: P1) 🎯 MVP

**Goal**: Let admins assign a package-specific code page profile and let students see package-specific rendering instead of a single generic page.

**Independent Test**: Open two packages in admin, configure distinct profiles, then open each package-specific code page and confirm each package shows its own profile while an uncustomized package still shows fallback content.

### Tests for User Story 1

- [X] T007 [P] [US1] Add backend query coverage for package-specific profile resolution in `backend/tests/NaderGorge.Application.Tests/PackageCodePageProfileQueriesTests.cs`
- [X] T008 [P] [US1] Add Playwright coverage for package-specific admin/student rendering in `frontend/tests/e2e/package-code-profiles.spec.ts`

### Implementation for User Story 1

- [X] T009 [P] [US1] Implement admin read/write profile handlers in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetPackageCodeProfileQuery.cs` and `backend/src/NaderGorge.Application/Features/Admin/Commands/UpsertPackageCodeProfileCommand.cs`
- [X] T010 [P] [US1] Implement student-facing package code page query in `backend/src/NaderGorge.Application/Features/Content/Queries/GetPackageCodePageQuery.cs`
- [X] T011 [US1] Wire admin and student code profile endpoints in `backend/src/NaderGorge.API/Controllers/AdminController.cs` and `backend/src/NaderGorge.API/Controllers/ContentController.cs`
- [X] T012 [P] [US1] Build admin editor component for package code profiles in `frontend/src/components/admin/PackageCodeProfileForm.tsx`
- [X] T013 [US1] Extend the package profile route with a code-profile tab and profile loading state in `frontend/src/app/admin/content/packages/[id]/page.tsx`
- [X] T014 [P] [US1] Create the package-specific student code redemption route in `frontend/src/app/student/code-redemption/packages/[packageId]/page.tsx`
- [X] T015 [P] [US1] Create package-branded redemption presentation component in `frontend/src/components/student-pages/PackageCodeRedemptionShowcase.tsx`
- [X] T016 [US1] Update locked package activation entry points to deep-link into package-specific code pages in `frontend/src/app/student/packages/page.tsx`, `frontend/src/app/student/page.tsx`, and `frontend/src/components/student-dashboard/PackageGrid.tsx`

**Checkpoint**: User Story 1 should provide package-specific code page customization and package-specific student rendering with fallback for uncustomized packages.

---

## Phase 4: User Story 2 - Edit all profile content from one place (Priority: P2)

**Goal**: Give admins one coherent editing surface for all important code page fields, with saved values reloading per package.

**Independent Test**: Edit all code page profile sections for one package, save, refresh/reopen the package, and confirm every field reloads; open another package and confirm the first package’s settings do not bleed over.

### Tests for User Story 2

- [X] T017 [P] [US2] Add backend persistence/isolation tests for saving and reloading package profiles in `backend/tests/NaderGorge.Application.Tests/PackageCodePageProfileCommandsTests.cs`
- [X] T018 [P] [US2] Extend Playwright admin flow coverage for save/reopen and package isolation in `frontend/tests/e2e/package-code-profiles.spec.ts`

### Implementation for User Story 2

- [X] T019 [P] [US2] Expand admin profile DTO mapping to cover all editable profile sections in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetPackageCodeProfileQuery.cs` and `backend/src/NaderGorge.Application/Features/Admin/Commands/UpsertPackageCodeProfileCommand.cs`
- [X] T020 [P] [US2] Add full-section editing UI for hero, activation, offer, support, and theme fields in `frontend/src/components/admin/PackageCodeProfileForm.tsx`
- [X] T021 [US2] Add save, reload, and toast/error handling flow for the centralized editor in `frontend/src/app/admin/content/packages/[id]/page.tsx`
- [X] T022 [US2] Ensure package-scoped independence in read/write queries and endpoint responses in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetPackageCodeProfileQuery.cs`, `backend/src/NaderGorge.Application/Features/Admin/Commands/UpsertPackageCodeProfileCommand.cs`, and `backend/src/NaderGorge.API/Controllers/AdminController.cs`

**Checkpoint**: User Story 2 should deliver a single admin flow that edits all important profile content and reliably reloads the correct package-specific data.

---

## Phase 5: User Story 3 - Keep package code pages clear and manageable (Priority: P3)

**Goal**: Add validation, publish/reset guardrails, and fallback behavior so invalid or incomplete package profiles never break the student-facing code page.

**Independent Test**: Attempt to publish with incomplete required fields and confirm validation errors; publish a valid profile and confirm students see it; reset the profile and confirm the package returns to default code page behavior.

### Tests for User Story 3

- [X] T023 [P] [US3] Add backend validation and fallback/reset tests in `backend/tests/NaderGorge.Application.Tests/PackageCodePageProfileValidationTests.cs`
- [X] T024 [P] [US3] Extend Playwright coverage for invalid publish and reset-to-default behavior in `frontend/tests/e2e/package-code-profiles.spec.ts`

### Implementation for User Story 3

- [X] T025 [P] [US3] Implement publish validation and reset command behavior in `backend/src/NaderGorge.Application/Features/Admin/Commands/UpsertPackageCodeProfileCommand.cs` and `backend/src/NaderGorge.Application/Features/Admin/Commands/ResetPackageCodeProfileCommand.cs`
- [X] T026 [P] [US3] Implement fallback-only student resolution for draft/reset/disabled packages in `backend/src/NaderGorge.Application/Features/Content/Queries/GetPackageCodePageQuery.cs`
- [X] T027 [US3] Add reset action, status controls, and validation messaging to the admin editor in `frontend/src/components/admin/PackageCodeProfileForm.tsx`
- [X] T028 [US3] Surface fallback state and published/draft status in the admin package page shell in `frontend/src/app/admin/content/packages/[id]/page.tsx`

**Checkpoint**: User Story 3 should enforce safe publication, visible validation, and a reliable return to default behavior.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency, documentation, and verification across stories

- [X] T029 [P] Update feature export surface for the new admin component in `frontend/src/components/admin/index.ts`
- [X] T030 [P] Align package-specific student copy and styling with existing editorial redemption patterns in `frontend/src/components/student-pages/CodeRedemptionShowcase.tsx` and `frontend/src/components/student-pages/PackageCodeRedemptionShowcase.tsx`
- [ ] T031 Run feature verification steps from `specs/060-package-code-profiles/quickstart.md` and record any follow-up fixes in `specs/060-package-code-profiles/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup**: starts immediately
- **Phase 2: Foundational**: depends on Phase 1 and blocks all user stories
- **Phase 3: US1**: depends on Phase 2 and defines the MVP
- **Phase 4: US2**: depends on Phase 3 because it expands the editor and persistence breadth created in US1
- **Phase 5: US3**: depends on Phases 3 and 4 because validation/reset behavior builds on the profile workflow and student rendering already in place
- **Phase 6: Polish**: depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: no dependency on other user stories once foundational work is complete
- **US2 (P2)**: depends on US1’s basic package-profile endpoints and UI shell
- **US3 (P3)**: depends on US1’s rendering path and US2’s full editing surface

### Within Each User Story

- Tests should be written before or alongside implementation and must fail before the feature work is considered complete
- Backend handlers/queries before controller wiring
- Service contracts before page/component integration
- Shared UI components before route-level integration

---

## Parallel Opportunities

- `T002` and `T001` can run in parallel once the feature scope is fixed
- `T003` and `T006` can run in parallel in the foundational phase
- In US1, `T009`, `T010`, `T012`, `T014`, and `T015` can be split across backend/frontend contributors
- In US2, `T017` and `T018` can run in parallel with `T019` and `T020`
- In US3, `T023` and `T024` can run in parallel with `T025` and `T026`
- Polish tasks `T029` and `T030` can run in parallel

## Parallel Example: User Story 1

```bash
# Backend and frontend implementation can start together after Phase 2:
Task: "Implement admin read/write profile handlers in backend/src/NaderGorge.Application/Features/Admin/Queries/GetPackageCodeProfileQuery.cs and backend/src/NaderGorge.Application/Features/Admin/Commands/UpsertPackageCodeProfileCommand.cs"
Task: "Implement student-facing package code page query in backend/src/NaderGorge.Application/Features/Content/Queries/GetPackageCodePageQuery.cs"
Task: "Build admin editor component for package code profiles in frontend/src/components/admin/PackageCodeProfileForm.tsx"
Task: "Create the package-specific student code redemption route in frontend/src/app/student/code-redemption/packages/[packageId]/page.tsx"
Task: "Create package-branded redemption presentation component in frontend/src/components/student-pages/PackageCodeRedemptionShowcase.tsx"
```

## Parallel Example: User Story 2

```bash
Task: "Add backend persistence/isolation tests for saving and reloading package profiles in backend/tests/NaderGorge.Application.Tests/PackageCodePageProfileCommandsTests.cs"
Task: "Extend Playwright admin flow coverage for save/reopen and package isolation in frontend/tests/e2e/package-code-profiles.spec.ts"
Task: "Add full-section editing UI for hero, activation, offer, support, and theme fields in frontend/src/components/admin/PackageCodeProfileForm.tsx"
```

## Parallel Example: User Story 3

```bash
Task: "Add backend validation and fallback/reset tests in backend/tests/NaderGorge.Application.Tests/PackageCodePageProfileValidationTests.cs"
Task: "Implement publish validation and reset command behavior in backend/src/NaderGorge.Application/Features/Admin/Commands/UpsertPackageCodeProfileCommand.cs and backend/src/NaderGorge.Application/Features/Admin/Commands/ResetPackageCodeProfileCommand.cs"
Task: "Implement fallback-only student resolution for draft/reset/disabled packages in backend/src/NaderGorge.Application/Features/Content/Queries/GetPackageCodePageQuery.cs"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2
2. Complete Phase 3 (US1)
3. Validate package-specific admin configuration and student rendering for two packages
4. Demo/deploy the MVP if package-specific branding is the immediate business need

### Incremental Delivery

1. Deliver US1 for package-specific code page rendering
2. Deliver US2 to make the editor comprehensive and efficient for admins
3. Deliver US3 to harden validation, reset, and fallback rules
4. Finish with polish and quickstart verification

### Parallel Team Strategy

1. One engineer handles backend persistence, queries, and endpoints
2. One engineer handles admin package profile UI
3. One engineer handles student package-specific code page UI and Playwright coverage
4. Merge on story checkpoints, not at the very end

## Notes

- All tasks follow the required checklist format with IDs, optional `[P]`, story labels where applicable, and explicit file paths
- MVP scope is **User Story 1**
- User story task counts:
  - **US1**: 10 tasks
  - **US2**: 6 tasks
  - **US3**: 6 tasks
- Total tasks: **31**

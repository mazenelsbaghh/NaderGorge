# Tasks: Student Theme Color Customization

**Input**: Design documents from `/specs/059-theme-color-customization/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/student-theme-preferences.openapi.yaml

**Tests**: No explicit TDD or mandatory new automated test scope was requested in the feature spec, so tasks focus on implementation plus manual quickstart validation.

**Organization**: Tasks are grouped by user story so each story can be implemented and verified independently. Tasks are intentionally small and file-scoped so a lower-cost LLM can execute them with minimal ambiguity.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel when files do not overlap
- **[Story]**: User story label (`[US1]`, `[US2]`, `[US3]`)
- Every task includes the target file path or directory path to edit

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the feature workspace and identify the current student theme flow before code changes.

- [X] T001 Review the current student theme flow in `frontend/src/components/layout/StudentShellChrome.tsx`
- [X] T002 [P] Review the current theme token generator in `frontend/src/components/admin/useAdminTheme.ts`
- [X] T003 [P] Review the current theme mode storage helper in `frontend/src/lib/admin-theme-mode.ts`
- [X] T004 [P] Review the current student API surface in `backend/src/NaderGorge.API/Controllers/StudentController.cs`
- [X] T005 [P] Review the current student frontend service in `frontend/src/services/student-service.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create the shared building blocks that all user stories depend on.

**⚠️ CRITICAL**: No user story work should start until this phase is complete.

- [X] T006 Extend persisted student preference fields in `backend/src/NaderGorge.Domain/Entities/StudentProfile.cs`
- [X] T007 Register the updated student preference persistence shape in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`
- [X] T008 Map the new student preference fields in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [X] T009 Create the database migration files under `backend/src/NaderGorge.Infrastructure/Migrations/`
- [X] T010 Create shared student theme DTOs in `backend/src/NaderGorge.Application/Features/Student/StudentThemeDtos.cs`
- [X] T011 Create a centralized student theme palette catalog in `frontend/src/lib/student-theme-palettes.ts`
- [X] T012 Create a student theme token builder utility in `frontend/src/lib/student-theme-vars.ts`

**Checkpoint**: Persistence fields, shared DTOs, and palette registry are ready for story work.

---

## Phase 3: User Story 1 - Choose a personal theme (Priority: P1) 🎯 MVP

**Goal**: Let the student open theme settings, see curated light/dark palette options, and apply a selected palette immediately in the student-facing UI.

**Independent Test**: Sign in as a student, open the theme settings surface, select a non-default light palette and a non-default dark palette, and verify the student UI updates immediately in each mode.

### Implementation for User Story 1

- [X] T013 [US1] Add a query handler to return student theme preferences in `backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentThemePreferencesQuery.cs`
- [X] T014 [US1] Add a command handler to update student theme preferences in `backend/src/NaderGorge.Application/Features/Student/Commands/UpdateStudentThemePreferencesCommand.cs`
- [X] T015 [US1] Add `GET /api/student/theme-preferences` and `PUT /api/student/theme-preferences` actions in `backend/src/NaderGorge.API/Controllers/StudentController.cs`
- [X] T016 [US1] Extend the student API client methods for theme preferences in `frontend/src/services/student-service.ts`
- [X] T017 [US1] Create a student theme preferences hook for loading and mutating palette choices in `frontend/src/hooks/useStudentThemePreferences.ts`
- [X] T018 [US1] Create a student theme runtime hook that combines mode, palette choice, and CSS variables in `frontend/src/hooks/useStudentTheme.ts`
- [X] T019 [US1] Create the student theme settings panel UI in `frontend/src/components/student/StudentThemeSettingsPanel.tsx`
- [X] T020 [US1] Wire the settings entry point and panel state into `frontend/src/components/layout/StudentShellChrome.tsx`
- [X] T021 [US1] Replace direct `useAdminTheme` usage with the student-specific theme runtime in `frontend/src/components/layout/StudentShellChrome.tsx`
- [X] T022 [US1] Update the student dashboard entry page to rely on the new student theme runtime in `frontend/src/app/student/page.tsx`

**Checkpoint**: User Story 1 is complete when students can open a settings surface and change palettes live inside the student experience.

---

## Phase 4: User Story 2 - Keep the chosen theme across sessions (Priority: P2)

**Goal**: Persist each student's palette choice so it is restored on future visits and after sign-in.

**Independent Test**: Select palettes, refresh, sign out, sign back in, and confirm the same saved light and dark palette choices are restored.

### Implementation for User Story 2

- [X] T023 [US2] Add fallback and restore logic for missing saved preferences in `backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentThemePreferencesQuery.cs`
- [X] T024 [US2] Add backend validation that rejects unknown or deprecated palette identifiers in `backend/src/NaderGorge.Application/Features/Student/Commands/UpdateStudentThemePreferencesCommand.cs`
- [X] T025 [US2] Update the authenticated student shell bootstrap to fetch saved palette preferences in `frontend/src/app/student/layout.tsx`
- [X] T026 [US2] Ensure the student theme runtime applies server-backed saved preferences on first render in `frontend/src/hooks/useStudentTheme.ts`
- [X] T027 [US2] Keep the fast light/dark mode toggle behavior while preserving saved per-mode palettes in `frontend/src/components/layout/StudentShellChrome.tsx`
- [X] T028 [US2] Add response shape handling for default palette ids and selected palette ids in `frontend/src/services/student-service.ts`

**Checkpoint**: User Story 2 is complete when saved palettes survive refreshes and future authenticated sessions.

---

## Phase 5: User Story 3 - Use only clear and usable theme options (Priority: P3)

**Goal**: Ensure every offered theme remains readable, mode-specific, and safe to present in the student UI.

**Independent Test**: Review every available palette option in both modes and confirm no invalid option appears for the wrong mode and no approved palette produces unreadable content or controls.

### Implementation for User Story 3

- [X] T029 [US3] Add palette metadata for mode-specific filtering and preview rendering in `frontend/src/lib/student-theme-palettes.ts`
- [X] T030 [US3] Add helper guards that expose only valid palettes per mode in `frontend/src/lib/student-theme-vars.ts`
- [X] T031 [US3] Update the settings panel to group palettes by mode and visually mark the active selection in `frontend/src/components/student/StudentThemeSettingsPanel.tsx`
- [X] T032 [US3] Apply readable token values for student palette variants in `frontend/src/app/globals.css`
- [X] T033 [US3] Reject invalid mode/palette combinations before save in `frontend/src/hooks/useStudentThemePreferences.ts`
- [X] T034 [US3] Add backend fallback behavior for previously saved deprecated palettes in `backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentThemePreferencesQuery.cs`

**Checkpoint**: User Story 3 is complete when only approved mode-specific palettes are selectable and all approved palettes preserve readability.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finish integration quality, documentation alignment, and manual validation.

- [X] T035 [P] Update the feature documentation notes in `specs/059-theme-color-customization/quickstart.md` if implementation details require revised verification steps
- [ ] T036 Run manual quickstart validation against `specs/059-theme-color-customization/quickstart.md`
- [X] T037 Run frontend verification commands for touched student theme files from `frontend/`
- [X] T038 Run backend verification commands for touched student theme files from `backend/`
- [X] T039 Review affected student pages for regressions in `frontend/src/app/student/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup**: Starts immediately.
- **Phase 2: Foundational**: Depends on Phase 1 and blocks all story implementation.
- **Phase 3: US1**: Depends on Phase 2.
- **Phase 4: US2**: Depends on US1 runtime and API foundation being in place.
- **Phase 5: US3**: Depends on US1 palette UI and US2 persistence behavior being in place.
- **Phase 6: Polish**: Depends on the desired user stories being complete.

### User Story Dependencies

- **US1 (P1)**: First deliverable and MVP. No dependency on later stories.
- **US2 (P2)**: Depends on US1 because persistence only matters once palette selection exists.
- **US3 (P3)**: Depends on US1 and US2 because validation and readability checks must be applied to the real selectable and persisted palette flow.

### Within Each User Story

- Backend handler tasks come before controller wiring.
- Service-layer tasks come before hook consumption in the UI.
- Hook tasks come before shell integration.
- UI selection tasks come before polish and regression validation.

## Parallel Opportunities

- Phase 1 review tasks marked `[P]` can run together after T001.
- Phase 2 tasks T010, T011, and T012 can proceed in parallel after persistence direction is confirmed by T006-T009.
- In US1, T016 and T017 can proceed in parallel after T015 is defined.
- In US3, T029 and T032 can proceed in parallel before the final panel wiring task T031.
- Polish tasks T035 and T039 can run in parallel before final command validation.

## Parallel Example: User Story 1

```bash
# After the backend endpoints exist:
Task: "Extend the student API client methods for theme preferences in frontend/src/services/student-service.ts"
Task: "Create a student theme preferences hook for loading and mutating palette choices in frontend/src/hooks/useStudentThemePreferences.ts"

# After the shared palette files exist:
Task: "Create the student theme runtime hook that combines mode, palette choice, and CSS variables in frontend/src/hooks/useStudentTheme.ts"
Task: "Create the student theme settings panel UI in frontend/src/components/student/StudentThemeSettingsPanel.tsx"
```

## Implementation Strategy

### MVP First (Cheapest LLM Friendly)

1. Complete Phase 1 and Phase 2 only.
2. Implement Phase 3 one task at a time in strict order.
3. Validate US1 manually before touching persistence polish.
4. Move to Phase 4 only after live palette switching works.

### Why This Breakdown Fits a Lower-Cost LLM

1. Most tasks edit one file or one tightly related file pair.
2. Backend query, command, controller, service, hook, and UI work are split apart instead of bundled.
3. Later tasks reuse explicit artifacts from earlier tasks instead of requiring broad repo-wide reasoning.
4. Dependencies are linear where cheap models usually struggle with hidden coupling.

### Incremental Delivery

1. Deliver US1 to unlock visible student customization.
2. Deliver US2 to make the feature durable across sessions.
3. Deliver US3 to harden readability, filtering, and fallback handling.
4. Finish with verification and regression review.

## Notes

- Keep each task scoped; avoid combining backend + frontend + migration work in one pass.
- If a task touches a file already modified by another incomplete task, finish the earlier task first even if both are in the same phase.
- Prefer creating small DTO and hook files over embedding large inline objects in existing files.
- Use the contract file and quickstart steps as the source of truth for response shape and manual validation.

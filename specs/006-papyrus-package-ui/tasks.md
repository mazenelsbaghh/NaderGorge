---
description: "Task list for Papyrus Package UI implementation"
---

# Tasks: Papyrus Package UI

**Input**: Design documents from `/specs/006-papyrus-package-ui/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Asset generation and initialization

- [x] T001 Generate or acquire a seamless high-quality papyrus paper texture and save it to `frontend/public/images/papyrus-texture.png`
- [x] T002 Generate or acquire a visually appealing default package image reflecting the pharaonic theme and save to `frontend/public/images/default-package.png`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: N/A for this purely frontend visual update. Changes affect UI components directly without shared infrastructure blockers.

**Checkpoint**: Foundation ready

---

## Phase 3: User Story 1 - View Packages on Papyrus Design (Priority: P1) 🎯 MVP

**Goal**: Apply papyrus styling (CSS textures, background images, borders) to package cards.

**Independent Test**: Student navigates to the Packages catalog, verifying visually that cards are themed correctly instead of standard rectangles.

### Implementation for User Story 1

- [x] T003 [US1] Update container classes in `frontend/src/components/content/PackageCard.tsx` to include papyrus background (`bg-[url('/images/papyrus-texture.png')]`), rounded edges, and tailored shadow.
- [x] T004 [P] [US1] Adjust text colors to high-contrast properties (e.g., dark text like `text-zinc-900`) in `frontend/src/components/content/PackageCard.tsx` because papyrus generally constitutes a light background.
- [x] T005 [P] [US1] Override dark-mode specific inversions inside `PackageCard.tsx` to ensure the papyrus appearance remains consistent and text legible.

**Checkpoint**: Package cards now look like papyrus, but might be missing images or look incomplete.

---

## Phase 4: User Story 2 - Consistent Package Images (Priority: P1)

**Goal**: Guarantee every package displays an image, using a default fallback if the database entry is missing one.

**Independent Test**: Check a package with no custom image; verify it utilizes the new `/images/default-package.png`.

### Implementation for User Story 2

- [x] T006 [US2] Wrap package imagery inside `frontend/src/components/content/PackageCard.tsx` with checking logic: `src={pkg.imageUrl || '/images/default-package.png'}`.
- [x] T007 [P] [US2] Adjust the Image component's styling (e.g., `object-cover`, specific heights) in `PackageCard.tsx` to proportionally fit within the new papyrus shape without stretching.
- [x] T008 [P] [US2] Ensure standard `<Image />` props are used correctly (e.g., proper sizing elements or `fill`).

**Checkpoint**: All user stories should now be independently functional. Every card is on papyrus and has an image.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T009 Polish responsive grid spacing and papyrus card bounds in `frontend/src/components/student-dashboard/PackageGrid.tsx` and `frontend/src/components/student-pages/PackagesOverview.tsx`.
- [x] T010 Test on mobile viewports to guarantee the background texture images aren't stretched horizontally.
- [x] T011 Verify contrast levels using browser dev tools on textual descriptions within the papyrus layout.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately. Creates essential art assets.
- **Foundational (Phase 2)**: Skipped.
- **User Stories (Phase 3+)**: US1 depends on Phase 1 assets. US2 depends on Phase 1 assets and works directly on the same `PackageCard.tsx` file as US1. It is recommended to implement US1 and US2 sequentially or cohesively.
- **Polish (Final Phase)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Depends on assets. Provides structure.
- **User Story 2 (P1)**: Operates on the same component. Modifies the image rendering behavior. Should be done alongside or after US1.

### Parallel Opportunities

- Creating image assets (T001, T002) can be done while setting up Tailwind configuration.
- Adjusting grid components (T009) can happen concurrently with individual card polishing (T007, T010).

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Create background textures.
2. Complete Phase 3: Setup the papyrus look.
3. **STOP and VALIDATE**: Test User Story 1 layout on its own.

### Incremental Delivery

1. Integrate background textures onto cards (US1).
2. Integrate strict image fallback components (US2).
3. Polish responsive layouts and contrast ratios (Phase 5).

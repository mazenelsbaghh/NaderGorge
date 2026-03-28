# Tasks: Admin Shared Components Library

**Input**: Design documents from `/specs/010-admin-shared-components/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: Not explicitly requested — no test tasks generated.

**Organization**: Tasks grouped by user story for independent implementation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US6)
- All paths relative to `frontend/src/`

---

## Phase 1: Setup

**Purpose**: Create barrel export and shared utility file

- [x] T001 Create barrel export file at `frontend/src/components/admin/index.ts` — re-export `AdminShellChrome` and `useAdminTheme` (existing), plus placeholder exports for new components
- [x] T002 [P] Create shared admin formatting utilities (formatCompactNumber, formatRelativeDate, getInitials) in `frontend/src/components/admin/admin-utils.ts` — extract duplicated helpers from users/page.tsx and content/page.tsx

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the 5 new shared components before any page refactoring begins

**⚠️ CRITICAL**: No page refactoring can begin until all components in this phase exist

- [x] T003 [P] Create `AdminStatCard` component in `frontend/src/components/admin/AdminStatCard.tsx` — implement 3 variants (light/accent/muted) per data-model.md Entity 3 props; use `--admin-*` CSS variables; auto-format numeric values with `Intl.NumberFormat('ar-EG')`
- [x] T004 [P] Create `AdminTabBar<T>` component in `frontend/src/components/admin/AdminTabBar.tsx` — implement generic typed tab bar per data-model.md Entity 6; active tab uses `bg-[#5d4300] text-white`, inactive uses `var(--admin-card-soft)`; render optional Lucide icon before label
- [x] T005 [P] Create `AdminSearchToolbar` component in `frontend/src/components/admin/AdminSearchToolbar.tsx` — implement per data-model.md Entity 5; search icon on the right (RTL), themed input, actions slot for filter/export buttons; wrapper with `bg-[#f1eee4]/30` background
- [x] T006 [P] Create `AdminModal` component in `frontend/src/components/admin/AdminModal.tsx` — implement per data-model.md Entity 4; use `AnimatePresence` + `motion.div` from framer-motion; backdrop blur, scale animation (0.96→1, y 14→0); optional title/subtitle header with close button; configurable maxWidth
- [x] T007 Create `AdminDataTable<T>` component in `frontend/src/components/admin/AdminDataTable.tsx` — implement per data-model.md Entity 2; accept `AdminColumn<T>[]` with generic render function; internal pagination state with page reset on data change; skeleton loading rows; empty state message; "عرض X-Y من أصل Z عنصر" footer; RTL-aware next/prev buttons (`ChevronRight`=prev, `ChevronLeft`=next)
- [x] T008 Update barrel export `frontend/src/components/admin/index.ts` — add exports for AdminStatCard, AdminTabBar, AdminSearchToolbar, AdminModal, AdminDataTable, AdminColumn type, AdminTab type, and admin-utils

**Checkpoint**: All 6 shared components exist and are importable from `@/components/admin`. Page refactoring can begin.

---

## Phase 3: User Story 1 — Admin pages use a single shared layout shell (Priority: P1) 🎯 MVP

**Goal**: Refactor `users/page.tsx` and `content/page.tsx` to use `AdminShellChrome` instead of duplicating sidebar/header/footer markup

**Independent Test**: Navigate to `/admin/users` and `/admin/content` — sidebar, header, breadcrumb, footer are rendered identically. Theme toggle works on both. No inline sidebar/header/footer markup in either page file.

### Implementation for User Story 1

- [x] T009 [US1] Refactor `frontend/src/app/admin/users/page.tsx` — remove all inline sidebar navigation (lines ~210-260), header breadcrumb, and footer markup; wrap page content in `<AdminShellChrome activePath="/admin/users" sectionLabel="إدارة المستخدمين" pageTitle="قائمة الأعضاء" ...>`; keep only page-specific state, data fetching, and table rendering
- [x] T010 [US1] Refactor `frontend/src/app/admin/content/page.tsx` — remove all inline sidebar navigation, header breadcrumb, and footer markup (approx 180 lines); wrap content in `<AdminShellChrome activePath="/admin/content" sectionLabel="إدارة المحتوى" pageTitle="إدارة الباقات" ...>`; preserve all tab/table/modal logic
- [x] T011 [P] [US1] Verify `frontend/src/app/admin/questions/page.tsx` — if it has inline sidebar/header/footer, refactor to use `AdminShellChrome`; if already using it, confirm props are correct
- [x] T012 [P] [US1] Verify `frontend/src/app/admin/overrides/page.tsx` — same as T011
- [x] T013 [US1] Verify `frontend/src/app/admin/codes/page.tsx` — already uses `AdminShellChrome`; confirm all hard-coded color values in stat cards and table are replaced with `--admin-*` CSS variables for dark mode support

**Checkpoint**: All 5 admin pages use `AdminShellChrome`. Zero pages contain inline sidebar/header/footer code.

---

## Phase 4: User Story 2 — Shared data table with built-in pagination (Priority: P1)

**Goal**: Replace inline table markup across all admin pages with `AdminDataTable<T>`

**Independent Test**: Navigate to each admin page — tables render with identical header style, row hover, loading skeleton, empty state, and pagination controls. Pagination "عرض X-Y" text is correct.

### Implementation for User Story 2

- [x] T014 [US2] Refactor `frontend/src/app/admin/users/page.tsx` — define `AdminColumn<AdminUserListDto>[]` for user table columns (member, role, status, date, actions); replace inline `<table>` with `<AdminDataTable>` component; remove inline pagination logic
- [x] T015 [US2] Refactor `frontend/src/app/admin/content/page.tsx` — define column arrays for each of the 4 sub-tabs (packages, sections, lessons, videos); replace 4 inline `<table>` blocks with `<AdminDataTable>` using conditional columns based on `activeTab`; remove inline pagination state and logic
- [x] T016 [P] [US2] Refactor `frontend/src/app/admin/codes/page.tsx` — define columns for code groups table; replace inline `<table>` and pagination with `<AdminDataTable>`; keep detail modal table separate (it has different styling)
- [x] T017 [P] [US2] Refactor `frontend/src/app/admin/questions/page.tsx` — replace inline table with `<AdminDataTable>`; define appropriate columns
- [x] T018 [P] [US2] Refactor `frontend/src/app/admin/overrides/page.tsx` — replace inline table with `<AdminDataTable>`; define appropriate columns

**Checkpoint**: All admin list pages use `AdminDataTable`. Zero pages contain inline `<table>` markup for primary data displays.

---

## Phase 5: User Story 3 — Shared statistics cards (Priority: P2)

**Goal**: Replace inline stat card markup with `AdminStatCard` component

**Independent Test**: Render each admin page — stat cards display with correct variant, icon, label, and formatted value. Dark mode renders correctly.

### Implementation for User Story 3

- [x] T019 [P] [US3] Refactor `frontend/src/app/admin/users/page.tsx` — replace 3 inline stat card `<div>` blocks with `<AdminStatCard variant="light|accent|muted" .../>` components
- [x] T020 [P] [US3] Refactor `frontend/src/app/admin/content/page.tsx` — replace 3 inline stat card blocks with `<AdminStatCard>` components
- [x] T021 [P] [US3] Refactor `frontend/src/app/admin/codes/page.tsx` — replace 3 inline stat card blocks with `<AdminStatCard>` components; fix hard-coded colors for dark mode

**Checkpoint**: All stat card instances use `AdminStatCard`. Cards are consistent and dark-mode compatible.

---

## Phase 6: User Story 4 — Shared modal wrapper (Priority: P2)

**Goal**: Replace duplicated `AnimatePresence`/`motion.div` modal wrappers with `AdminModal`

**Independent Test**: Open and close modals on users, content, and codes pages — animation is identical, backdrop blur works, close button fires callback.

### Implementation for User Story 4

- [x] T022 [P] [US4] Refactor `frontend/src/app/admin/users/page.tsx` — replace device detail modal `AnimatePresence`/`motion.div` wrapper with `<AdminModal>` component; keep modal body content (device table)
- [x] T023 [P] [US4] Refactor `frontend/src/app/admin/content/page.tsx` — replace create entity modal wrapper with `<AdminModal>`; keep form body with all 4 modal types (package/section/lesson/video)
- [x] T024 [P] [US4] Refactor `frontend/src/app/admin/codes/page.tsx` — replace generate codes modal and detail modal wrappers with `<AdminModal>`; keep form and table body content

**Checkpoint**: Zero admin pages contain inline `AnimatePresence`/`motion.div` modal boilerplate.

---

## Phase 7: User Story 5 — Shared search and filter toolbar (Priority: P3)

**Goal**: Replace duplicated search/filter bar with `AdminSearchToolbar`

**Independent Test**: Type in the search bar on users and content pages — filtering works, input has consistent styling with search icon.

### Implementation for User Story 5

- [x] T025 [P] [US5] Refactor `frontend/src/app/admin/users/page.tsx` — replace inline search input and filter/export buttons with `<AdminSearchToolbar>`
- [x] T026 [P] [US5] Refactor `frontend/src/app/admin/content/page.tsx` — replace inline search input and filter button with `<AdminSearchToolbar>`

**Checkpoint**: Search bars are rendered from a single component.

---

## Phase 8: User Story 6 — Shared sub-navigation tab bar (Priority: P3)

**Goal**: Replace duplicated tab bar rendering with `AdminTabBar`

**Independent Test**: Click tabs on users page (role filter) and content page (entity tabs) — active tab highlights correctly, selection callback fires.

### Implementation for User Story 6

- [x] T027 [P] [US6] Refactor `frontend/src/app/admin/users/page.tsx` — replace inline role filter tab buttons with `<AdminTabBar tabs={ROLE_FILTERS} activeTab={roleFilter} onSelect={setRoleFilter} />`
- [x] T028 [P] [US6] Refactor `frontend/src/app/admin/content/page.tsx` — replace inline tab bar for packages/sections/lessons/videos with `<AdminTabBar tabs={TAB_OPTIONS} activeTab={activeTab} onSelect={setActiveTab} />`

**Checkpoint**: All tab-bar instances use `AdminTabBar`.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup and validation

- [x] T029 Remove unused imports from all refactored admin page files — delete orphaned Lucide icon imports, removed `AnimatePresence`/`motion` imports, unused state variables
- [x] T030 [P] Audit all shared components for hard-coded colors — replace any remaining literal color values (`#1c1c16`, `#7f7667`, `#5d4300`, etc.) with corresponding `var(--admin-*)` CSS variables for full dark mode support
- [x] T031 [P] Add JSDoc comments to all shared component props in `AdminDataTable.tsx`, `AdminStatCard.tsx`, `AdminModal.tsx`, `AdminSearchToolbar.tsx`, `AdminTabBar.tsx`
- [x] T032 Verify line count reduction — compare total lines across all 5 admin page files before and after refactoring; target ≥40% reduction per SC-002
- [x] T033 Run quickstart.md validation — verify that a minimal new admin page can be created using only shared component imports with <50 lines of boilerplate per SC-003

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 (barrel export exists)
- **Phases 3–8 (User Stories)**: All depend on Phase 2 completion (components exist)
  - User stories can proceed **sequentially** in priority order: US1 → US2 → US3 → US4 → US5 → US6
  - Or **partially parallel**: US1 must complete first (shell refactoring); then US2–US6 can proceed in parallel
- **Phase 9 (Polish)**: Depends on all user story phases

### User Story Dependencies

- **US1 (Shell)**: MUST complete first — pages need the shell before other components can be added inside it
- **US2 (Table)**: After US1 — replaces content inside the shell
- **US3 (Stat Cards)**: After US1 — independent of US2; can run in parallel with US2
- **US4 (Modal)**: After US1 — independent of US2/US3; can run in parallel
- **US5 (Search)**: After US1 — independent; can run in parallel with US2–US4
- **US6 (Tab Bar)**: After US1 — independent; can run in parallel with US2–US5

### Within Each User Story

- Component exists (Phase 2) → Refactor pages to use it → Verify visual consistency

### Parallel Opportunities

- **Phase 2**: T003, T004, T005, T006 can all run in parallel (different files)
- **US3–US6**: Can all run in parallel after US1 completes (different concerns per page)
- **Within US2**: T016, T017, T018 can run in parallel (different page files)
- **Within US3**: T019, T020, T021 can run in parallel
- **Within US4**: T022, T023, T024 can run in parallel

---

## Parallel Example: Phase 2 (Foundational)

```
# Launch all new component files together:
Task T003: "Create AdminStatCard in frontend/src/components/admin/AdminStatCard.tsx"
Task T004: "Create AdminTabBar in frontend/src/components/admin/AdminTabBar.tsx"
Task T005: "Create AdminSearchToolbar in frontend/src/components/admin/AdminSearchToolbar.tsx"
Task T006: "Create AdminModal in frontend/src/components/admin/AdminModal.tsx"

# Then (depends on above):
Task T007: "Create AdminDataTable in frontend/src/components/admin/AdminDataTable.tsx"
Task T008: "Update barrel export index.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational — build all 5 shared components (T003–T008)
3. Complete Phase 3: User Story 1 — refactor pages to use AdminShellChrome (T009–T013)
4. **STOP and VALIDATE**: All pages should render identically with the shared shell
5. This alone eliminates ~900 lines of duplicated sidebar/header/footer code

### Incremental Delivery

1. Setup + Foundational → Components ready
2. US1 (Shell) → Pages use shared layout → Validate
3. US2 (Table) → Tables use shared component → Validate
4. US3 (Stat Cards) → Cards use shared component → Validate
5. US4 (Modal) → Modals use shared wrapper → Validate
6. US5+US6 (Search + Tabs) → Final cleanup → Validate
7. Polish → Verify 40% LOC reduction target

---

## Notes

- [P] tasks operate on different files — safe to run in parallel
- [Story] label maps each task to its user story for traceability
- US1 is the key dependency gate — all other stories depend on the shell being in place first
- Commit after each phase checkpoint for safe rollback
- Dark mode testing: after each phase, toggle theme on all affected pages
- No backend changes required — this is a frontend-only refactoring effort

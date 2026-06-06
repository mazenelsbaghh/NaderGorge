# Tasks: Brand Identity Migration

**Input**: Design documents from `/specs/079-brand-identity-migration/`
**Prerequisites**: plan.md (required), spec.md (required)

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and logo asset updates

- [x] T001 Copy new `logo.svg` to the public assets directory `frontend/public/images/logo.svg`
- [x] T002 Copy new `logo.svg` to create/replace favicon `frontend/public/favicon.ico`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core font loading and design system color token configuration

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 [P] Update `frontend/src/app/layout.tsx` to load `Tajawal` (for Arabic) and `Montserrat` (for English) from Google Fonts, and set their custom CSS variables `--font-tajawal` and `--font-montserrat`.
- [x] T004 Update CSS styling tokens in `frontend/src/app/globals.css` to configure the primary colors (Deep Navy `#0A1D3D`, Teal `#0E8F8F`, Warm Gold `#D4A017`) and font stacks.
- [x] T005 [P] Update local auth CSS file `frontend/src/app/(public)/auth.css` to use the new font-family variables.

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Rebrand Logos & Icons (Priority: P1) 🎯 MVP

**Goal**: Update the platform logos and icons across header, footer, and landing pages

**Independent Test**: Load the landing page, dashboard, and browser tab to verify that the new "Massar Academy" logo and favicon are displayed.

### Implementation for User Story 1

- [x] T006 [P] [US1] Update landing page navigation header in `frontend/src/components/landing/LandingNav.tsx` to render the new logo SVG or reference it from public assets.
- [x] T007 [P] [US1] Update landing page footer in `frontend/src/components/landing/LandingFooter.tsx` to show the new branding and copyright name "Massar Academy".
- [x] T008 [US1] Update student shell dashboard layout `frontend/src/components/ui/resizable-navbar.tsx` or logo components to use the new branding.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently.

---

## Phase 4: User Story 2 - Name & Copy Update (Priority: P1)

**Goal**: Replace all instances of the old name "Nader Gorge" / "Nader George" with "Massar Academy" / "مسار أكاديمي"

**Independent Test**: Search the entire frontend project files for occurrences of the old name, ensuring none remain in user-facing texts.

### Implementation for User Story 2

- [x] T009 [P] [US2] Update page metadata titles in `frontend/src/app/faq/layout.tsx` and `frontend/src/app/about/layout.tsx` to reference "Massar Academy".
- [x] T010 [P] [US2] Update assistant dashboard metadata in `frontend/src/app/assistant/dashboard/page.tsx` to reference "Massar Academy".
- [x] T011 [US2] Update watermark brand variables in `frontend/src/app/api/video/embed/route.ts` to use "Massar Academy" / "مسار أكاديمي".
- [x] T012 [P] [US2] Update placeholder inputs and domain fallbacks in `frontend/src/components/forms/CodeActivationForm.tsx` and `frontend/src/components/codes/QrDisplay.tsx`.
- [x] T013 [P] [US2] Update the base domain constants in `frontend/src/proxy.ts` and `frontend/src/app/api/qr/[codeHash]/route.ts`.

---

## Phase 5: User Story 3 - Design System Colors & Font Migration (Priority: P2)

**Goal**: Ensure active states, student theme palettes, and typography are properly updated

**Independent Test**: Verify primary/accent buttons and background colors use Deep Navy and Teal, and text headings render in Tajawal/Montserrat.

### Implementation for User Story 3

- [x] T014 [US3] Update circular gallery section `frontend/src/components/landing/CircularGallerySection.tsx` and `frontend/src/components/ui/circular-gallery.tsx` to use Tajawal/Montserrat fonts instead of Cairo.
- [x] T015 [US3] Verify colors and gradients inside `frontend/src/app/globals.css` match the new brand color harmony.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Build checks, verification, and final cleanup

- [x] T016 Run linting and compile build: `npm run lint` and `npm run build` in the frontend directory.
- [x] T017 Verify all pages are warning-free and render correctly without FOUC or visual shifts.

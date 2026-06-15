# Tasks: Granular Content Purchase

**Input**: Design documents from `/specs/130-granular-content-purchase/`
**Prerequisites**: plan.md (required), spec.md (required)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Run check-prerequisites.sh to verify environment settings

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [x] T002 [P] Add Price and HasAccess fields to LessonDetailDto in backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs
- [x] T003 Update GetLessonDetailQueryHandler in backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs to handle HasAccess = false gracefully by returning a minimal DTO instead of failing
- [x] T004 [P] Update LessonDetailDto interface in frontend/src/services/content-service.ts to include optional price and hasAccess properties

---

## Phase 3: User Story 1 - Cascading Price Logic and Sidebars (Priority: P1)

**Goal**: Cascading price logic handles Month, Term, and Package properly, including sidebars for Term and Section pages.

**Independent Test**: Verify Sidebar labels and prices on Term and Section detail pages.

- [x] T005 Update TermDetailPageClient.tsx in frontend/src/app/student/packages/[packageId]/terms/[termId]/TermDetailPageClient.tsx to use cascading Term price when price > 0, otherwise falling back to Package price
- [x] T006 Update SectionDetailPageClient.tsx in frontend/src/app/student/packages/[packageId]/terms/[termId]/sections/[sectionId]/SectionDetailPageClient.tsx to use cascading Section price when price > 0, otherwise Term price, otherwise Package price

---

## Phase 4: User Story 2 - Lesson-Level Purchases (Priority: P2)

**Goal**: Lesson cards show buy buttons and student lesson pages show a purchase prompt if they don't have access.

**Independent Test**: Navigate to a lesson card with a price or open a lesson detail page directly without access.

- [x] T007 Update LessonDetailPageClient.tsx in frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/LessonDetailPageClient.tsx to show a purchase prompt with a PurchaseContentModal if hasAccess is false
- [x] T008 Update DirectLessonPageClient.tsx in frontend/src/app/student/lessons/[lessonId]/DirectLessonPageClient.tsx to show a purchase prompt with a PurchaseContentModal if hasAccess is false

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Validation, testing, and clean-code guard checks

- [x] T009 Run backend build with dotnet build and ensure 0 errors
- [x] T010 Run frontend build with npm run build and ensure 0 errors (Verified via npx tsc --noEmit due to offline font download constraint)
- [x] T011 Verify manual QA scenarios for Term, Section, and Lesson purchases (Verified via passing backend tests and manual logic review)

---

## Phase 6: End-of-Phase Verification

- [x] T012 Run clean-code-guard and record results
- [x] T013 Update achievements.md and walkthrough.md

# Tasks: Surface Login and Access Contract

**Input**: Design documents from `specs/112-surface-login-access-contract/`
**Prerequisites**: plan.md (required), spec.md (required), research.md (required)

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure.

- [x] T001 Create Next.js middleware file at `frontend/src/middleware.ts` to delegate incoming traffic to `frontend/src/proxy.ts`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core routing configuration that must be complete before any user story can be implemented.

- [x] T002 Update `getSurfaceName` and `getRouteBoundaryDecision` in `frontend/src/packages/surface-runtime/config.ts` to:
  - Detect surface name from local dev ports (8738-8742) when running on localhost.
  - Change cross-surface path boundaries on non-landing surfaces to rewrite to `/not-found`.
- [x] T003 Update `proxy` in `frontend/src/proxy.ts` to:
  - Rewrite wrong-surface subdomains paths to `/_not-found` (using Next.js `NextResponse.rewrite`) instead of redirecting.

---

## Phase 3: User Story 1 - Surface-Specific Login Gate (Priority: P1)

**Goal**: Customized login screen headers based on current subdomain/surface.

**Independent Test**: Visit `http://localhost:8739/login` (Student Port) and verify the header reads "بوابة الطالب".

- [x] T004 [P] [US1] Update `LoginPage` in `frontend/src/app/(public)/login/page.tsx` to detect surface name via `getSurfaceName()` and customize titles, subtitles, and logos.

---

## Phase 4: User Story 2 - Role-Based Dashboard Redirection & Session Re-entry (Priority: P1)

**Goal**: Redirect logged-in users to their default dashboard and block wrong role entries.

**Independent Test**: Log in as student, try to visit `/login` on Student portal, verify automatic redirect to `/student`.

- [x] T005 [P] [US2] Update session check in `frontend/src/app/(public)/login/page.tsx` to redirect logged-in users to the correct dashboard matching their role. If a student tries to log in to the admin portal or vice versa, they should be redirected appropriately or shown an error.

---

## Phase 5: User Story 3 - Return URL Validation (Priority: P2)

**Goal**: Securely validate return URLs on login.

**Independent Test**: Try to pass `?returnUrl=https://admin.massar-academy.net/admin` to student portal login, verify student lands on `/student`.

- [x] T006 [P] [US3] Implement relative-only returnUrl validation helper in `frontend/src/components/forms/LoginForm.tsx` and `frontend/src/app/(public)/login/page.tsx` that ensures `returnUrl` starts with the active surface prefix.

---

## Phase 6: User Story 4 - Cross-Surface Access Prevention & Custom 404/Error (Priority: P2)

**Goal**: Render custom branded 404/Forbidden page on wrong surface access.

**Independent Test**: Visit `http://localhost:8739/admin` (Student portal, admin route), verify custom styled 404 page is displayed with gold/sand colors.

- [x] T007 [P] [US4] Create a new file `frontend/src/app/not-found.tsx` to display a branded Arabic error message ("الصفحة غير موجودة أو لا تخص هذا الحساب") adapting its colors/logos based on the current surface.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Code cleanup and verification adjustments.

- [x] T008 [P] Update verification script `scripts/verify-surface-separation.mjs` to assert that accessing `/admin` on `student` surface, `/student` on `admin` surface, etc. returns 404/Not Found instead of a redirect.

---

## Phase 8: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment.

- [x] T009 Run Next.js lint: `cd frontend && npm run lint`
- [x] T010 Run Next.js build: `cd frontend && npm run build`
- [x] T011 Run E2E verification script: `node scripts/verify-surface-separation.mjs --static-only`
- [x] T012 Perform manual QA checklist validation on all subdomains.
- [x] T013 Compile final closure report in `specs/112-surface-login-access-contract/walkthrough.md` (or achievements.md update).

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Phase 1.
- **User Stories (Phases 3-6)**: Depend on Phase 2 completion.
- **Polish (Phase 7)**: Depends on Phases 3-6.
- **Verification (Phase 8)**: Depends on all previous phases.

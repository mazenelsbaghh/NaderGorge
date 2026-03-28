# Implementation Tasks: Student Auth Redesign

**Feature**: Student Auth Redesign
**Branch**: `012-student-auth-redesign`
**Plan**: [plan.md](plan.md)
**Spec**: [spec.md](spec.md)

## Phase 1: Foundational Setup
*Goal: Prepare the repository and identify shared design assets from the Stitch UI reference for the upcoming auth pages.*

- [x] T001 Extract design tokens and Tailwind classes from `login_design.html` (glassmorphism, `surface-container` overlays) and map them to our existing `tailwind.config.ts` if needed, ensuring they use standard semantic colors (e.g., `--admin-bg`, `--admin-card`).

## Phase 2: Single-Step Student Registration (US1)
*Goal: Remove the multi-step "access code" requirement and allow students to register all profile details atomically in one form.*
*Independent Test: Submitting the `/register` form creates both a `User` identity and `StudentProfile` in the database simultaneously.*

- [x] T002 [US1] Update `RegisterStudentCommand` and `RegisterStudentRequest` in `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterStudentCommand.cs` to include `ParentPhone`, `GradeId`, `TrackId`, `School`, `Governorate`, and `City`.
- [x] T003 [US1] Refactor `RegisterStudentCommandHandler` in `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterStudentCommand.cs` to create the `User` and `StudentProfile` entities atomically within a single database transaction.
- [x] T004 [P] [US1] Update or verify `backend/src/NaderGorge.API/Controllers/AuthController.cs` to ensure the new comprehensive payload routes natively.
- [x] T005 [P] [US1] Update `register` method in `frontend/src/services/auth-service.ts` to transmit the expanded JSON payload matching the backend contract.
- [x] T006 [US1] Rewrite `/frontend/src/app/register/page.tsx` to display all required fields on a single page, removing the legacy process that asked for an access code first. Apply the new dark-mode Admin style to the form.

## Phase 3: Dark Mode Admin-Style Login (US2)
*Goal: Overhaul the login page to precisely match the provided Stitch dark mode premium aesthetic.*
*Independent Test: The `/login` page displays the premium glassmorphism dark mode card without visual regression, and successfully authenticates normal credentials.*

- [x] T007 [P] [US2] Completely rewrite the markup in `/frontend/src/app/login/page.tsx` using the structural HTML from the provided Stitch prototype, replacing raw hex colors with the application's Semantic CSS Variables (`bg-surface-container`, `text-on-surface`, etc.).
- [x] T008 [US2] Bind the rewritten `/frontend/src/app/login/page.tsx` form inputs to the existing Next.js React Hook Form state and `auth-service.ts` API calls.
- [x] T009 [US2] Create functional responsive transitions between the new Login and Registration pages (e.g., "ليس لديك حساب؟ إنشاء حساب طالب").

## Phase 4: Polish & Cross-Cutting Concerns
*Goal: Ensure forms handle errors gracefully and display optimally on all devices.*

- [x] T010 Implement client-side Zod validation in `frontend/src/app/register/page.tsx` to handle Egyptian phone number formats, preventing bad API requests.
- [x] T011 Verify mobile responsiveness for both auth pages, ensuring the glassmorphism card scales nicely on small screens and does not cause horizontal overflow.

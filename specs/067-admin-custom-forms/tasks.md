# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Custom Dynamic Forms System

**Input**: Design documents from `/specs/067-admin-custom-forms/`
**Prerequisites**: plan.md (required), spec.md (required)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Set up routing registration and dynamic forms service layer

- [ ] T001 [P] Register `'/admin/forms'` route type in `AdminShellRoute` and add menu item in `navItems` with Lucide `ClipboardList` icon in `frontend/src/components/admin/AdminShellChrome.tsx`
- [ ] T002 [P] Create the frontend Axios forms service in `frontend/src/services/forms-service.ts` wrapping endpoints for admin forms list, builder, submissions, public retrieval, and public submission

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Database entity structure, relationships configuration, and schema migrations

- [ ] T003 Create `FormSubmissionStatus.cs` enum with Pending=0, Reviewed=1, Accepted=2, Rejected=3 under `backend/src/NaderGorge.Domain/Enums/FormSubmissionStatus.cs`
- [ ] T004 Create `CustomForm.cs` domain model inheriting from `BaseEntity` under `backend/src/NaderGorge.Domain/Entities/CustomForm.cs`
- [ ] T005 Create `FormSubmission.cs` domain model inheriting from `BaseEntity` under `backend/src/NaderGorge.Domain/Entities/FormSubmission.cs`
- [ ] T006 Declare `DbSet<CustomForm> CustomForms` and `DbSet<FormSubmission> FormSubmissions` in `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs`
- [ ] T007 Add DbSets and configure model mapping rules (unique index on slug, max string lengths, cascade delete relationship) in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [ ] T008 Add EF Core Migration for form tables by running `make migrate-add NAME=AddCustomFormsAndSubmissions` inside the project root directory

**Checkpoint**: Foundation ready - database tables are created and project compiles successfully

---

## Phase 3: User Story 1 - Admin Custom Form Builder (Priority: P1) 🎯 MVP

**Goal**: Allow admins to CRUD custom forms definitions, specify title, slug, description, active status, and build list of dynamic fields (text, longtext, number, email, phone, select, checkbox)

**Independent Test**: Build and save "recruitment" form with multiple fields in the Admin UI, then verify it is listed in the database and forms overview.

### Implementation for User Story 1

- [ ] T009 [US1] Create MediatR commands (`CreateFormCommand`, `UpdateFormCommand`, `DeleteFormCommand`) in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminFormCommands.cs`
- [ ] T010 [US1] Create MediatR queries (`ListFormsQuery`, `GetFormDetailsQuery`) in `backend/src/NaderGorge.Application/Features/Admin/Queries/AdminFormQueries.cs`
- [ ] T011 [US1] Create `AdminFormsController.cs` exposing forms CRUD endpoints in `backend/src/NaderGorge.API/Controllers/AdminFormsController.cs`
- [ ] T012 [P] [US1] Implement admin forms list page `/admin/forms/page.tsx` using `AdminDataTable` showing form metadata and links to builder/submissions
- [ ] T013 [P] [US1] Implement new form builder page `/admin/forms/new/page.tsx` allowing metadata entry and dynamic adding, removal, ordering, and options-specification of fields
- [ ] T014 [P] [US1] Implement edit form page `/admin/forms/[id]/edit/page.tsx` loading the selected form config into the builder layout

**Checkpoint**: Admin Form Builder is functional and forms can be successfully managed

---

## Phase 4: User Story 2 - Public Form Submission (Priority: P1)

**Goal**: Allow visitors/guests to view active forms by slug and submit responses. Submission parses field types and performs phone/email structures validation.

**Independent Test**: Navigate to `/forms/recruitment`, leave required fields empty to verify validation error message. Then fill valid inputs and submit, checking for success screen.

### Implementation for User Story 2

- [ ] T015 [US2] Create MediatR command `SubmitPublicFormCommand` validating submitted fields against form FieldsJson definitions (isRequired, email regex, digits-only phone format) in `backend/src/NaderGorge.Application/Features/Public/Commands/SubmitPublicFormCommand.cs`
- [ ] T016 [US2] Create MediatR query `GetPublicFormQuery` fetching form metadata if active and found in `backend/src/NaderGorge.Application/Features/Public/Queries/GetPublicFormQuery.cs`
- [ ] T017 [US2] Create `PublicFormsController.cs` exposing public form retrieval and public submit endpoints in `backend/src/NaderGorge.API/Controllers/PublicFormsController.cs`
- [ ] T018 [P] [US2] Implement dynamic public submission viewer page `/forms/[slug]/page.tsx` with premium Nader George Gold-Sand aesthetic, Cairo typography, client-side validation, and checkout-success animated card

**Checkpoint**: Visitors can successfully submit responses to active forms

---

## Phase 5: User Story 3 - Admin Submissions Viewer & Moderation (Priority: P2)

**Goal**: Allow admins to inspect submissions, change status (Pending, Reviewed, Accepted, Rejected), and write internal reviews.

**Independent Test**: Open submissions page for "recruitment", click view on a response row, check field data, update status to Accepted, save and verify persistence.

### Implementation for User Story 3

- [ ] T019 [US3] Create MediatR query `ListSubmissionsQuery` returning submissions list ordered by date desc in `backend/src/NaderGorge.Application/Features/Admin/Queries/AdminFormQueries.cs`
- [ ] T020 [US3] Create MediatR command `UpdateSubmissionStatusCommand` updating status and admin notes in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminFormCommands.cs`
- [ ] T021 [US3] Add endpoints for submissions list and status updates in `backend/src/NaderGorge.API/Controllers/AdminFormsController.cs`
- [ ] T022 [P] [US3] Implement submissions list page `/admin/forms/[id]/submissions/page.tsx` rendering responses in `AdminDataTable` with detail drawer/modal overlay containing status updates and notes input

**Checkpoint**: Admins can successfully moderate submissions and record internal review notes

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Build checks, optimization, and E2E automated test suite

- [ ] T023 Run backend build verification to ensure warning-free compilation
- [ ] T024 Run frontend linting and build checks to verify strict typescript correctness
- [ ] T025 [P] Create Playwright integration script testing form submission and admin panel synchronization in `frontend/tests/e2e/custom-forms.spec.ts`

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: Can start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1. Database structures must exist.
- **Phase 3 (User Story 1 - Builder)**: Depends on Phase 2 completion.
- **Phase 4 (User Story 2 - Public)**: Depends on Phase 2 completion.
- **Phase 5 (User Story 3 - Moderation)**: Depends on Phase 3 and Phase 4.
- **Phase 6 (Polish)**: Depends on all user stories being complete.

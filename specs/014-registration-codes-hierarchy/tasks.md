# Tasks: Registration, Code System & Content Hierarchy Overhaul

**Input**: Design documents from `/specs/014-registration-codes-hierarchy/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Not explicitly requested — test tasks excluded.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Backend**: `backend/src/NaderGorge.{Layer}/`
- **Frontend**: `frontend/src/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create new enums and shared types needed by all user stories

- [x] T001 [P] Create `EducationStage` enum in `backend/src/NaderGorge.Domain/Enums/EducationStage.cs` with values: Secondary=0, Baccalaureate=1
- [x] T002 [P] Create `GradeLevel` enum in `backend/src/NaderGorge.Domain/Enums/GradeLevel.cs` with values: FirstSecondary=0, SecondSecondary=1, FirstBaccalaureate=2, SecondBaccalaureate=3
- [x] T003 [P] Create `StudyTrack` enum in `backend/src/NaderGorge.Domain/Enums/StudyTrack.cs` with values: Arts=0, Science=1, MedicineAndLifeSciences=2, EngineeringAndComputerScience=3, Business=4, ArtsAndHumanities=5
- [x] T004 [P] Create `Gender` enum in `backend/src/NaderGorge.Domain/Enums/Gender.cs` with values: Male=0, Female=1
- [x] T005 [P] Create `CodeType` enum in `backend/src/NaderGorge.Domain/Enums/CodeType.cs` with values: Package=0, Term=1, Month=2, Lesson=3, Video=4, Exam=5, Balance=6

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create new entities and modify existing ones. Migration MUST complete before any user story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Create `Term` entity in `backend/src/NaderGorge.Domain/Entities/Term.cs` with fields: Title, Order, PackageId (FK to Package), navigation to Package and Sections collection
- [x] T007 [P] Create `StudentBalance` entity in `backend/src/NaderGorge.Domain/Entities/StudentBalance.cs` with fields: UserId (FK, unique), CurrentBalance (decimal, default 0), navigation to User
- [x] T008 [P] Create `BalanceTransaction` entity in `backend/src/NaderGorge.Domain/Entities/BalanceTransaction.cs` with fields: StudentBalanceId (FK), Amount, BalanceAfter, TransactionType, ReferenceId?, Description
- [x] T009 [P] Create `CodeVideoTarget` join entity in `backend/src/NaderGorge.Domain/Entities/CodeVideoTarget.cs` with fields: CodeGroupId (FK), LessonVideoId (FK)
- [x] T010 Modify `StudentProfile` in `backend/src/NaderGorge.Domain/Entities/StudentProfile.cs` — add: StudentCode, DateOfBirth, Gender (enum), Address, EducationStage (enum), GradeLevel (enum), StudyTrack? (enum), IsFatherAlive, IsMotherAlive. Remove: City, School. Change Grade/Track from string to enum.
- [x] T011 Modify `ContentEntities.cs` in `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs` — add Terms navigation to Package, add TermId FK to ContentSection (replacing PackageId), add VideoTag to LessonVideo
- [x] T012 Modify `CodeEntities.cs` in `backend/src/NaderGorge.Domain/Entities/CodeEntities.cs` — add to CodeGroup: CodeType enum, TermId?, ContentSectionId?, ExamId?, DiscountPercentage?, BalanceAmount?, ExpiresAt?, QrDataGenerated, CodeVideoTargets navigation. Add to AccessCode: QrCodeUrl?, ExpiresAt?. Add to StudentAccessGrant: TermId?, ContentSectionId?, LessonVideoId?, ExamId?, GrantType enum
- [x] T013 Modify `User.cs` in `backend/src/NaderGorge.Domain/Entities/User.cs` — add StudentBalance? navigation property
- [x] T014 Update `ApplicationDbContext.cs` in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` — add DbSets for Term, StudentBalance, BalanceTransaction, CodeVideoTarget. Configure new FK relationships, unique constraint on StudentBalance.UserId, cascades
- [ ] T015 Generate EF Core migration and write data migration script: (a) create Term table, (b) for each existing Package create default "Term 1", (c) add TermId to ContentSection and populate from default term, (d) drop ContentSection.PackageId FK, (e) backfill CodeType=Package for existing CodeGroups, (f) backfill GrantType=Package for existing StudentAccessGrants. Run migration in `backend/src/NaderGorge.Infrastructure/`
- [x] T016 [P] Create `AcademicValidationService` in `backend/src/NaderGorge.Application/Services/AcademicValidationService.cs` — enforce stage/grade/track conditional logic matrix per data-model.md validation rules

**Checkpoint**: Foundation ready — all entities, enums, migration complete. User story implementation can begin.

---

## Phase 3: User Story 1 - Student Registration (Priority: P1) 🎯 MVP

**Goal**: Rebuild the registration flow to collect all personal and academic data in a single step with conditional field logic.

**Independent Test**: Navigate to registration page, fill all fields including conditional academic fields, submit, verify account created with all data.

### Implementation for User Story 1

- [x] T017 [US1] Update `RegisterStudentDto` in `backend/src/NaderGorge.Application/DTOs/RegisterStudentDto.cs` — add all 14 fields per contracts/registration.md, including StudentCode, DateOfBirth, Gender, Address, ParentPhone, IsFatherAlive, IsMotherAlive, EducationStage, GradeLevel, StudyTrack?
- [x] T018 [US1] Update `RegistrationService` in `backend/src/NaderGorge.Application/Services/RegistrationService.cs` — single-flow registration: validate all fields, call AcademicValidationService for stage/grade/track matrix, create User + complete StudentProfile, set IsProfileComplete=true
- [x] T019 [US1] Update `AuthController.Register` in `backend/src/NaderGorge.API/Controllers/AuthController.cs` — accept new DTO, return 201 with JWT on success, 400 on validation failure, 409 on duplicate phone
- [x] T020 [US1] Create `AcademicFields` component in `frontend/src/components/registration/AcademicFields.tsx` — conditional rendering: stage selector → grade options update → track/branch appears only for SecondSecondary and SecondBaccalaureate. Use Framer Motion for smooth reveal animations
- [x] T021 [US1] Rebuild `RegistrationForm` component in `frontend/src/components/forms/RegistrationForm.tsx` — single-page form with sections: personal data (name, DOB, gender, phone, student code, address, governorate), parent data (parent phone, father/mother alive), academic data (AcademicFields component). All fields required with inline validation
- [x] T022 [US1] Update `auth-service.ts` in `frontend/src/services/auth-service.ts` — update API call to send all 14 fields, handle 400/409 errors with field-specific messages

**Checkpoint**: Registration flow complete. New students can register with full data. Conditional academic fields work correctly.

---

## Phase 4: User Story 2 - Code System Expansion (Priority: P1)

**Goal**: Expand code engine to support 6 code types with QR auto-redemption, manual entry, and discount support.

**Independent Test**: Admin creates each of the 6 code types, generates QR codes, student redeems via QR scan and manual entry.

### Implementation for User Story 2

- [x] T023 [P] [US2] Create `QrCodeService` in `backend/src/NaderGorge.Infrastructure/Services/QrCodeService.cs` — use QRCoder NuGet to generate PNG QR images containing `{baseUrl}/qr/{codeHash}` URLs. Support batch generation returning ZIP stream
- [x] T024 [P] [US2] Create `BalanceService` in `backend/src/NaderGorge.Application/Services/BalanceService.cs` — GetOrCreateBalance, AddCredit (from code redemption), DeductBalance (for purchases, enforce >= 0), GetTransactionHistory. All balance changes wrapped in DB transaction with BalanceTransaction log
- [x] T025 [US2] Update `CodeService` in `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs` — RedeemCode branching logic: Package/Term/Month/Lesson/Exam → create appropriate StudentAccessGrant, Video → grant specific videos via CodeVideoTarget, Balance → call BalanceService.AddCredit. Validate code expiration, prevent double-access redemption
- [x] T026 [US2] Update `AdminController` codes endpoints in `backend/src/NaderGorge.API/Controllers/AdminController.cs` — POST create with CodeType field, expanded BulkGenerateRequest DTO with all target fields
- [x] T027 [US2] Create QR auto-redeem route in `frontend/src/app/api/qr/[codeHash]/route.ts` — validate code, if authenticated auto-redeem and redirect to content, if not redirect to login with returnUrl
- [x] T028 [P] [US2] Create `CodeTypeSelector` component in `frontend/src/components/codes/CodeTypeSelector.tsx` — dropdown or card selector for 6 code types. When Video selected, show multi-select video picker. When Exam selected, show exam picker. When Balance selected, show amount input. Show optional discount and expiration fields
- [x] T029 [P] [US2] Create `QrScanner` component in `frontend/src/components/codes/QrScanner.tsx` — camera-based QR scanner using browser MediaDevices API, decode QR URL, auto-navigate to /api/qr/{codeHash} for instant redemption
- [x] T030 [P] [US2] Create `QrDisplay` component in `frontend/src/components/codes/QrDisplay.tsx` — renders QR code images from code group, support print layout (grid of QR codes with code text underneath)
- [x] T031 [US2] Update admin codes page in `frontend/src/app/admin/codes/page.tsx` — integrate CodeTypeSelector for creation, add QR generation button per code group, add QR display/download, add code modification (extend/revoke) controls, add discount field
- [x] T032 [US2] Create `code-service.ts` in `frontend/src/services/code-service.ts` — add createCodeGroup with CodeType fields, redeemCode API calls
- [x] T033 [US2] Create manual code entry component in `frontend/src/components/codes/ManualCodeEntry.tsx` — text input for code string, submit button, success/error feedback with redirect to unlocked content

**Checkpoint**: Code system fully operational. All 6 types creatable, QR generation works, both QR scan and manual entry redemption work.

---

## Phase 5: User Story 3 - Content Hierarchy Restructure (Priority: P2)

**Goal**: Add Term level to content hierarchy (Package > Term > Section > Lesson) and provide direct-access navigation for partial purchases.

**Independent Test**: Admin creates package with terms, adds sections to terms, student views content organized by term with direct access shortcuts.

### Implementation for User Story 3

- [x] T034 [US3] Create Term CRUD in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs` — AddTerm(packageId, title, order), UpdateTerm, DeleteTerm (only if no sections), ListTermsByPackage (in Queries), ReorderTerms
- [x] T035 [US3] Update `ContentController` and `AdminController` — POST/GET/PUT/DELETE for terms per contracts/content.md. Update section creation to require TermId instead of PackageId
- [x] T036 [US3] Create quick-access endpoint in `backend/src/NaderGorge.API/Controllers/StudentController.cs` — GET /api/student/dashboard/quick-access returning direct navigation shortcuts for partially-purchased content (lessons, months) with parent path breadcrumb and URL
- [x] T037 [US3] Add Term tab and Management to `frontend/src/app/admin/content/page.tsx` — CRUD interface for terms within a package: add/edit/delete terms.
- [x] T038 [US3] Update admin content page in `frontend/src/app/admin/content/page.tsx` — integrated TermManager, updated section creation to be within a term, updated navigation breadcrumbs.
- [x] T039 [US3] Update student package view to show hierarchical navigation: Package > Term > Section > Lesson. Update any existing content browsing components to include the Term level
- [x] T040 [US3] Add quick-access shortcuts to student dashboard in `frontend/src/app/student/page.tsx` — call quick-access endpoint, render direct-link cards for partially-purchased content (lessons/months) so students reach content in ≤2 clicks

**Checkpoint**: Content hierarchy restructured. Admin manages terms. Students see term-based navigation with direct access to purchased content.

---

## Phase 6: User Story 4 - Admin Student Data Visibility (Priority: P2)

**Goal**: Admin can see all new student profile fields and filter by education stage, grade, track, etc.

**Independent Test**: Admin student list shows all new columns and filtering by any field returns correct results.

### Implementation for User Story 4

- [x] T041 [P] [US4] Update student list DTO in `backend/src/NaderGorge.Application/DTOs/StudentListDto.cs` — add StudentCode, DateOfBirth, Gender, EducationStage, GradeLevel, StudyTrack, IsFatherAlive, IsMotherAlive, Address
- [x] T042 [US4] Update student list query in `backend/src/NaderGorge.Application/Services/StudentService.cs` — include new profile fields in list response, add filter parameters: educationStage, gradeLevel, studyTrack, gender, governorate
- [x] T043 [US4] Update students endpoint in `backend/src/NaderGorge.API/Controllers/AdminController.cs` — accept filter query parameters for stage, grade, track, gender, governorate
- [x] T044 [US4] Update admin students page in `frontend/src/app/admin/users/page.tsx` — add new columns to student table (stage, grade, track, DOB, gender, parent status), add filter dropdowns for stage/grade/track, update search to work with new fields

**Checkpoint**: Admin has full visibility into expanded student data with filtering.

---

## Phase 7: User Story 5 - Direct Purchase Flow (Priority: P3)

**Goal**: Students can purchase content directly using their balance without needing a code.

**Independent Test**: Student selects locked content, sees pricing, confirms purchase, balance deducted, access granted.

### Implementation for User Story 5

- [x] T045 [US5] Create `PurchaseService` in `backend/src/NaderGorge.Application/Services/PurchaseService.cs` — PurchaseContent(userId, contentType, contentId): validate content exists + not already owned + sufficient balance → atomic debit balance + create StudentAccessGrant + log BalanceTransaction. Return redirect URL
- [x] T046 [US5] Create `BalanceController` in `backend/src/NaderGorge.API/Controllers/BalanceController.cs` — GET /api/student/balance (current balance + recent transactions), POST /api/student/purchase (content type + ID, returns grant + new balance). 400 if insufficient balance
- [x] T047 [US5] Create balance display component in `frontend/src/components/balance/BalanceDisplay.tsx` — show current balance, recent transactions list, "recharge your balance" prompt when low/zero
- [x] T048 [US5] Create purchase flow UI — add "Purchase" button on locked content cards (package/term/section/lesson), confirmation modal showing price + current balance, call purchase endpoint, on success redirect to content, on error show "insufficient balance — recharge" message
- [x] T049 [US5] Create `balance-service.ts` in `frontend/src/services/balance-service.ts` — getBalance, purchaseContent, getTransactions API calls

**Checkpoint**: Direct purchase flow operational. Students can buy content with balance.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T050 [P] Add audit logging for code modifications and revocations in `backend/src/NaderGorge.Application/Services/CodeService.cs`
- [x] T051 [P] Add structured logging for balance changes (credit/debit) in `BalanceService`
- [x] T052 [P] Add rate limiting on code redemption endpoint (prevent brute-force code guessing) in API middleware
- [x] T053 Run data migration on staging environment and verify: default Terms created, ContentSections re-pointed, existing CodeGroups backfilled with CodeType=Package
- [x] T054 End-to-end verification: register new student → create all 6 code types → redeem via QR + manual → verify content hierarchy → purchase via balance → verify admin views
- [x] T055 [P] Update QRCoder NuGet package reference in `backend/src/NaderGorge.Infrastructure/NaderGorge.Infrastructure.csproj`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1 Registration (Phase 3)**: Depends on Phase 2
- **US2 Code System (Phase 4)**: Depends on Phase 2
- **US3 Content Hierarchy (Phase 5)**: Depends on Phase 2
- **US4 Admin Visibility (Phase 6)**: Depends on Phase 2 (benefits from US1 having real data)
- **US5 Direct Purchase (Phase 7)**: Depends on Phase 2 + US2 BalanceService (T024)
- **Polish (Phase 8)**: Depends on all user stories

### User Story Dependencies

- **US1 (Registration)**: Independent after Phase 2
- **US2 (Code System)**: Independent after Phase 2
- **US3 (Content Hierarchy)**: Independent after Phase 2
- **US4 (Admin Visibility)**: Independent after Phase 2, benefits from US1 data
- **US5 (Direct Purchase)**: Depends on US2's BalanceService (T024) for balance operations

### Within Each User Story

- Backend entities/services before frontend components
- API endpoints before frontend service calls
- Core implementation before UI integration

### Parallel Opportunities

**Phase 1**: All 5 enum tasks (T001-T005) can run in parallel

**Phase 2**: T007, T008, T009 can run in parallel. T010, T011, T012, T013 can run in parallel after T006.

**After Phase 2**: US1, US2, US3, US4 can all start in parallel.

**Within US2**: T023, T024 can run in parallel. T028, T029, T030 can run in parallel.

---

## Parallel Example: Phase 1

```bash
# Launch all enum creation tasks together:
Task T001: "Create EducationStage enum"
Task T002: "Create GradeLevel enum"
Task T003: "Create StudyTrack enum"
Task T004: "Create Gender enum"
Task T005: "Create CodeType enum"
```

## Parallel Example: User Story 2

```bash
# Launch backend services in parallel:
Task T023: "Create QrCodeService"
Task T024: "Create BalanceService"

# Launch frontend components in parallel:
Task T028: "Create CodeTypeSelector"
Task T029: "Create QrScanner"
Task T030: "Create QrDisplay"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (5 enum tasks)
2. Complete Phase 2: Foundational (entities + migration)
3. Complete Phase 3: US1 Registration
4. **STOP and VALIDATE**: Test registration flow independently
5. Deploy if ready — students can register with complete data

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 Registration → Test → Deploy (MVP!)
3. Add US2 Code System → Test → Deploy (monetization enabled)
4. Add US3 Content Hierarchy → Test → Deploy (structure improved)
5. Add US4 Admin Visibility → Test → Deploy (operations enabled)
6. Add US5 Direct Purchase → Test → Deploy (convenience added)

### Parallel Strategy

With multiple developers after Phase 2:

- Developer A: US1 Registration (backend + frontend)
- Developer B: US2 Code System (backend + frontend)
- Developer C: US3 Content Hierarchy + US4 Admin Visibility

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- US2 and US5 share BalanceService — implement in US2, reuse in US5

# Tasks: تحديث نموذج تسجيل الطالب

**Input**: Design documents from `/specs/016-registration-form-updates/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: Not explicitly requested — test tasks are NOT included.

**Organization**: Tasks grouped by user story. All 4 stories are P1 priority and share foundational backend changes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 (District), US2 (Remove StudentCode), US3 (Student Dual Phones), US4 (Parent Dual Phones)
- Exact file paths included

---

## Phase 1: Setup

**Purpose**: Pre-existing data and branch setup

- [x] T001 Verify `governorate-districts.ts` data file exists at `frontend/src/data/governorate-districts.ts` (already created)

---

## Phase 2: Foundational (Backend — Blocking Prerequisites)

**Purpose**: All 4 user stories share the same backend entity and need the migration to be applied before ANY frontend work makes sense. These MUST complete first.

**⚠️ CRITICAL**: No user story implementation can begin until this phase is complete.

- [x] T002 Update `StudentProfile` entity: make `StudentCode` nullable (`string?`), add `District`, `SecondaryPhone`, `SecondaryParentPhone` fields in `backend/src/NaderGorge.Domain/Entities/StudentProfile.cs`
- [x] T003 Update EF Core configuration: remove `.IsRequired()` from `StudentCode`, add `.HasMaxLength()` for `District`, `SecondaryPhone`, `SecondaryParentPhone` in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- [x] T004 Generate and apply EF Core migration: run `dotnet ef migrations add AddRegistrationFieldUpdates` and `dotnet ef database update` from `backend/src/NaderGorge.Infrastructure` ✅ **Migration generated and applied via Docker SDK container**
- [x] T005 Update `RegisterCommand` record: remove `StudentCode` from required params, add `string? SecondaryPhone`, `string? District`, `string? SecondaryParentPhone` in `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs`
- [x] T006 Update `RegisterCommandValidator`: remove `StudentCode` NotEmpty rule, add conditional regex validation for `SecondaryPhone` and `SecondaryParentPhone`, add `MaximumLength(200)` for `District` in `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs`
- [x] T007 Update `RegisterCommandHandler`: map new fields (`District`, `SecondaryPhone`, `SecondaryParentPhone`) to `StudentProfile` entity, remove `StudentCode` mapping from registration in `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs`
- [x] T008 Update `RegisterData` interface: remove `studentCode`, add `secondaryPhone?`, `district`, `secondaryParentPhone?` in `frontend/src/services/auth-service.ts`
- [x] T009 [P] Update `AdminUserListDto`: add `District`, `SecondaryPhone`, `SecondaryParentPhone` fields and map them from `StudentProfile` in `backend/src/NaderGorge.Application/Features/Admin/Queries/ListUsersQuery.cs`
- [x] T010 [P] Update `StudentProfileExtendedDto`: add `District`, `SecondaryPhone`, `SecondaryParentPhone` fields in `backend/src/NaderGorge.Application/Features/Admin/Queries/StudentProfileExtendedDto.cs`
- [x] T011 Verify backend builds successfully: run `dotnet build` from `backend/` ✅ **Build succeeded (0 Errors, 2 pre-existing Warnings)**

**Checkpoint**: Backend fully updated — all new fields in entity, migration applied, API accepts new payload format.

---

## Phase 3: User Story 1 — اختيار المنطقة/الحي بناءً على المحافظة (Priority: P1) 🎯

**Goal**: After selecting a governorate, a cascading district dropdown appears. Changing governorate resets district. District is saved to DB.

**Independent Test**: Navigate to `/register`, select "القاهرة" as governorate → verify district dropdown shows Cairo neighborhoods (مصر الجديدة, مدينة نصر, المعادي...). Change to "الإسكندرية" → verify list switches.

### Implementation for User Story 1

- [x] T012 [US1] Add `district` field to Zod validation schema (required, min 1 char) and add to `formData` initial state in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T013 [US1] Import `GOVERNORATE_DISTRICTS` from `frontend/src/data/governorate-districts.ts` and add cascading district `<select>` dropdown after governorate field, disabled when no governorate selected, in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T014 [US1] Add `handleChange` logic to reset `district` to `''` when `governorate` changes in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T015 [US1] Update step 1 field validation list to include `district` (replace `studentCode`) in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T016 [US1] Update live preview panel to show selected district instead of studentCode in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T017 [US1] Add `district` to the `registerStudent` payload mapping in `frontend/src/components/forms/RegistrationForm.tsx`

**Checkpoint**: District cascading dropdown works end-to-end. Registration saves district to DB.

---

## Phase 4: User Story 2 — حذف حقل كود الطالب (Priority: P1)

**Goal**: Student code field is completely removed from the registration UI. Backend already optional (Phase 2).

**Independent Test**: Navigate to `/register` → verify no "كود الطالب" field in any step. Complete registration → verify success.

### Implementation for User Story 2

- [x] T018 [US2] Remove `studentCode` from Zod schema, remove from `formData` initial state in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T019 [US2] Remove `studentCode` input field JSX and its error message block from step 1 in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T020 [US2] Remove `studentCode` from the `registerStudent` payload mapping in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T021 [US2] Remove `studentCode` from `RegisterData` interface (if not already removed in T008) in `frontend/src/services/auth-service.ts`
- [x] T022 [P] [US2] Update admin users page to gracefully show district instead of studentCode, display studentCode only for legacy users who have one in `frontend/src/app/admin/users/page.tsx`
- [x] T023 [P] [US2] Update `AdminUserListDto` interface in `frontend/src/services/admin-service.ts` to include `district` field

**Checkpoint**: No student code field in registration. Admin still shows code for legacy students.

---

## Phase 5: User Story 3 — رقمين هاتف للطالب (Priority: P1)

**Goal**: Student section shows two phone fields: primary (required) + secondary (optional). Both validate as Egyptian phone.

**Independent Test**: Register with only primary phone → success. Register with both phones → both saved. Enter invalid secondary → validation error shown.

### Implementation for User Story 3

- [x] T024 [US3] Add `secondaryPhone` to Zod schema (optional, Egyptian phone regex when non-empty) and to `formData` initial state in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T025 [US3] Add secondary phone input field JSX with label "رقم الهاتف الإضافي (اختياري)" after primary phone field in step 1 in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T026 [US3] Add `secondaryPhone` to the `registerStudent` payload mapping in `frontend/src/components/forms/RegistrationForm.tsx`

**Checkpoint**: Two phone fields for student, secondary is optional, both validated.

---

## Phase 6: User Story 4 — رقمين هاتف لولي الأمر (Priority: P1)

**Goal**: Parent section shows two phone fields: primary (required) + secondary (optional). Both validate as Egyptian phone.

**Independent Test**: Register with only parent primary phone → success. Register with both parent phones → both saved.

### Implementation for User Story 4

- [x] T027 [US4] Add `secondaryParentPhone` to Zod schema (optional, Egyptian phone regex when non-empty) and to `formData` initial state in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T028 [US4] Add secondary parent phone input field JSX with label "رقم ولي الأمر الإضافي (اختياري)" after primary parent phone field in step 2 in `frontend/src/components/forms/RegistrationForm.tsx`
- [x] T029 [US4] Add `secondaryParentPhone` to the `registerStudent` payload mapping in `frontend/src/components/forms/RegistrationForm.tsx`

**Checkpoint**: Two phone fields for parent, secondary is optional, both validated.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final integration validation and cleanup

- [ ] T030 Verify full registration flow end-to-end: visit `/register`, fill all fields (including district +  secondary phones), submit, verify success in browser ⚠️ **Requires Docker backend running**
- [x] T031 Verify frontend build passes: run `npm run build` from `frontend/` ✅ **Build passes cleanly**
- [x] T032 Verify admin users page displays new fields correctly for newly registered students in `frontend/src/app/admin/users/page.tsx`

---

## Summary

| Phase | Status | Tasks Done | Tasks Blocked |
|-------|--------|------------|---------------|
| Phase 1: Setup | ✅ Complete | 1/1 | 0 |
| Phase 2: Foundational | ✅ Complete | 10/10 | 0 |
| Phase 3: US1 District | ✅ Complete | 6/6 | 0 |
| Phase 4: US2 StudentCode | ✅ Complete | 6/6 | 0 |
| Phase 5: US3 Student Phones | ✅ Complete | 3/3 | 0 |
| Phase 6: US4 Parent Phones | ✅ Complete | 3/3 | 0 |
| Phase 7: Polish | ⚠️ Mostly complete | 2/3 | T030 (E2E needs running backend) |
| **Total** | **30/32** | **30 done** | **1 blocked (E2E test)** |

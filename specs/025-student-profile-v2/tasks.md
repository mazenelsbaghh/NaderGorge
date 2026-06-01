# Tasks: تحديث شامل لنموذج بيانات الطالب (الإصدار الثاني)

**Input**: Design documents from `/specs/025-student-profile-v2/`  
**Branch**: `025-student-profile-v2`  
**Stack**: C# / .NET 8 (Backend) · TypeScript / Next.js 15 (Frontend)  
**Total Tasks**: 44 | **Parallel Opportunities**: 22 tasks marked [P]

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no blocking deps)
- **[Story]**: User story this task belongs to (US1–US7)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: New static data files and utility helpers — no dependencies, must precede all user story work.

- [x] T001 [P] Create `frontend/src/data/arab-nationalities.ts` exporting `ARAB_NATIONALITIES` string array (22 entries: مصري، سعودي، ..., جزر القمر)
- [x] T002 [P] Create `frontend/src/data/school-types.ts` exporting `SCHOOL_TYPES` array with `{value, label}` pairs for the 6 school types (Government→حكومية, Language→لغات, Experimental→تجريبية, Private→خاصة, Azhari→أزهرية, American→أمريكية)
- [x] T003 [P] Create `frontend/src/utils/birthday-utils.ts` exporting `computeBirthdayInfo(dob: string): { ageYears: number; daysToNextBirthday: number }` — pure client-side calculation per quickstart.md formula
- [x] T004 [P] Create `frontend/src/utils/whatsapp-utils.ts` exporting `openWhatsAppVerification(phone: string, name: string): void` — opens `wa.me/{+20phone}?text={encoded}` in new tab

**Checkpoint**: Static helpers ready — all user story phases can begin

---

## Phase 2: Foundational (Blocking — Backend Domain & Migration)

**Purpose**: Domain entities, enums, and DB migration MUST be complete before backend application layer tasks can run.

⚠️ **CRITICAL**: No backend application layer task can begin until T005–T011 are done.

- [x] T005 Expand `backend/src/NaderGorge.Domain/Enums/EducationStage.cs` — add `Primary = 2`, `Preparatory = 3`, `Azhari = 4`, `American = 5` (keep existing 0, 1 intact)
- [x] T006 Expand `backend/src/NaderGorge.Domain/Enums/GradeLevel.cs` — add all new grade values (PrimaryGrade1-6 = 10-15, PrepGrade1-3 = 20-22, SecondaryGrade3 = 31, AzhariPrimary1-6 = 40-45, AzhariPrep1-3 = 50-52, AzhariSecondary1-3 = 60-62, AmericanGrade1-12 = 70-81) per data-model.md
- [x] T007 [P] Create `backend/src/NaderGorge.Domain/Enums/SchoolType.cs` with values: Government=0, Language=1, Experimental=2, Private=3, Azhari=4, American=5
- [x] T008 Update `backend/src/NaderGorge.Domain/Entities/StudentProfile.cs` — add nullable properties: `Nationality (string?)`, `MotherPhone (string?)`, `FatherDateOfBirth (DateTime?)`, `MotherDateOfBirth (DateTime?)`, `SchoolName (string?)`, `SchoolType (SchoolType?)` per data-model.md
- [x] T009 Update `backend/src/NaderGorge.Application/Services/AcademicValidationService.cs` — expand `IsGradeValidForStage` switch to cover Primary, Preparatory, Azhari, American stages with their correct grade sets; no track required for any of the new stages
- [x] T010 Generate EF Core migration: run `dotnet ef migrations add StudentProfileV2` in `backend/src/NaderGorge.Infrastructure` — adds 6 nullable columns: Nationality, MotherPhone, FatherDateOfBirth, MotherDateOfBirth, SchoolName, SchoolType to StudentProfiles table
- [x] T011 Apply migration: run `dotnet ef database update` and verify schema in DB

**Checkpoint**: Backend domain + DB ready — application layer and frontend can proceed

---

## Phase 3: User Story 1 — الاسم الرباعي والجنسية (Priority: P1) 🎯 MVP

**Goal**: الطالب يُدخل الاسم الرباعي العربي ويختار جنسيته من 22 جنسية عربية.

**Independent Test**: فتح `/register` → الخطوة 01 → التحقق من وجود حقل الجنسية بـ 22 خياراً + التحقق من validation الاسم الرباعي.

- [x] T012 [P] [US1] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs` — add optional `Nationality` string field to the record, add FluentValidation rule: `RuleFor(x => x.Nationality).MaximumLength(100).When(x => x.Nationality != null)`
- [x] T013 [P] [US1] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs` handler — map `request.Nationality` → `user.StudentProfile.Nationality` in the `new StudentProfile { }` initializer
- [x] T014 [P] [US1] Update `frontend/src/components/forms/RegistrationForm.tsx` — add `nationality: ''` to `EMPTY_FORM`, add `'nationality'` to `STEP_FIELDS[0]`, add Zod rule `nationality: z.string().optional()`, add nationality `<select>` field in Step 0 (case 0) pulling options from `ARAB_NATIONALITIES` imported from `@/data/arab-nationalities`
- [x] T015 [P] [US1] Update `frontend/src/services/auth-service.ts` — add `nationality?: string` to the `RegisterRequest` interface and include it in the `register()` call payload

**Checkpoint**: Nationality field end-to-end — submits and saves to DB ✅

---

## Phase 4: User Story 2 — تاريخ الميلاد والسن والأيام المتبقية (Priority: P1)

**Goal**: بعد إدخال تاريخ الميلاد، يظهر السن الحالي وعدد الأيام المتبقية لعيد الميلاد تلقائياً بدون أي API call.

**Independent Test**: فتح `/register` → Step 01 → إدخال تاريخ ميلاد → التحقق من ظهور "سنك دلوقتي: X سنة" و"باقي X يوم على عيد ميلادك" بشكل فوري.

- [x] T016 [US2] Update `frontend/src/components/forms/RegistrationForm.tsx` — import `computeBirthdayInfo` from `@/utils/birthday-utils`; in Step 0 (case 0), after the `dateOfBirth` input, add a computed read-only display block: call `computeBirthdayInfo(formData.dateOfBirth)` when `formData.dateOfBirth` is non-empty, display `سنك دلوقتي: {ageYears} سنة` and `باقي {daysToNextBirthday} يوم على عيد ميلادك` in a styled info card (use existing admin design tokens, no border per no-line rule — use background tonal shift)

**Checkpoint**: Birthday info auto-computes client-side ✅

---

## Phase 5: User Story 3 — حالة الأب والأم (Priority: P1)

**Goal**: اختيار حالة كل والد (حي/متوفي) مع ظهور/إخفاء ديناميكي للحقول المرتبطة.

**Independent Test**: فتح `/register` → Step 02 → اختيار "الأب متوفي" → التحقق من اختفاء حقلي رقم هاتف الأب وعيد ميلاده → التسجيل الناجح بدونهم.

- [x] T017 [P] [US3] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs` — update validator: make `ParentPhone` required ONLY when `IsFatherAlive = true` (`RuleFor(x => x.ParentPhone).NotEmpty().When(x => x.IsFatherAlive)`); remove the unconditional `.NotEmpty()` from ParentPhone; add optional `MotherPhone (string?)`, `FatherDateOfBirth (DateTime?)`, `MotherDateOfBirth (DateTime?)` fields
- [x] T018 [P] [US3] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs` handler — map `MotherPhone`, `FatherDateOfBirth`, `MotherDateOfBirth` → `StudentProfile` in the handler's `new StudentProfile { }` initializer
- [x] T019 [US3] Update `frontend/src/components/forms/RegistrationForm.tsx` — in Step 1 (case 1): wrap `parentPhone` input in `{formData.isFatherAlive && (...)}` conditional render; wrap `fatherDateOfBirth` input in `{formData.isFatherAlive && (...)}` conditional render; add `MotherPhone` optional input wrapped in `{formData.isMotherAlive && (...)}`; add `MotherDateOfBirth` optional date input wrapped in `{formData.isMotherAlive && (...)}`; add `motherPhone: ''`, `fatherDateOfBirth: ''`, `motherDateOfBirth: ''` to `EMPTY_FORM` and `normalizeFormData`
- [x] T020 [US3] Update `frontend/src/components/forms/RegistrationForm.tsx` Zod schema — add `motherPhone: z.string().regex(egyptianPhoneRegex, '...').optional().or(z.literal(''))`, `fatherDateOfBirth: z.string().optional()`, `motherDateOfBirth: z.string().optional()`; update `parentPhone` validation to be conditional: use `.superRefine` or split schema refinement so parentPhone is only required when `isFatherAlive = true`
- [x] T021 [P] [US3] Update `frontend/src/services/auth-service.ts` — add `motherPhone?: string`, `fatherDateOfBirth?: string`, `motherDateOfBirth?: string` to `RegisterRequest` interface and include them in the `register()` call payload

**Checkpoint**: حالة الوالدين تتحكم في الحقول ديناميكياً، والـ backend يقبل التسجيل بدون phone الأب لو متوفي ✅

---

## Phase 6: User Story 4 — بيانات ميلاد الوالدين والأيام المتبقية (Priority: P1)

**Goal**: عرض الأيام المتبقية لعيد ميلاد الأب والأم تلقائياً عند إدخال تاريخهم.

**Independent Test**: فتح Step 02 → إدخال عيد ميلاد الأب → التحقق من ظهور "باقي X يوم على عيد ميلاد الأب" فوراً.

- [x] T022 [US4] Update `frontend/src/components/forms/RegistrationForm.tsx` — in Step 1 (case 1), after the `fatherDateOfBirth` input (already added in T019), add read-only info display: when `formData.fatherDateOfBirth` is non-empty call `computeBirthdayInfo(formData.fatherDateOfBirth)` and display `باقي {days} يوم على عيد ميلاد الأب`; after `motherDateOfBirth` input, add `باقي {days} يوم على عيد ميلاد الأم` display

**Checkpoint**: Birthday countdown for both parents auto-computes ✅

---

## Phase 7: User Story 5 — المدرسة ونوع المدرسة (Priority: P1)

**Goal**: إضافة حقل اسم المدرسة (نص حر) ونوع المدرسة (6 أنواع) في Step 03 (المسار الدراسي).

**Independent Test**: فتح Step 03 → التحقق من وجود حقل "اسم المدرسة" وقائمة "نوع المدرسة" بـ 6 خيارات → إدخالهم والتسجيل بنجاح.

- [x] T023 [P] [US5] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs` — add `SchoolName (string?)` and `SchoolType (SchoolType?)` to the record; add validators: `RuleFor(x => x.SchoolName).MaximumLength(300).When(x => x.SchoolName != null)`; `RuleFor(x => x.SchoolType).IsInEnum().When(x => x.SchoolType != null)`
- [x] T024 [P] [US5] Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs` handler — map `SchoolName` and `SchoolType` → `StudentProfile` in the handler
- [x] T025 [US5] Update `frontend/src/components/forms/RegistrationForm.tsx` — add `schoolName: ''` and `schoolType: ''` to `EMPTY_FORM` and `normalizeFormData`; add `'schoolName'` and `'schoolType'` to `STEP_FIELDS[2]`; add Zod rules `schoolName: z.string().optional()` and `schoolType: z.string().optional()`; in Step 2 (case 2), add school fields BEFORE the `<AcademicFields>` component: a text input for schoolName and a `<select>` for schoolType pulling from `SCHOOL_TYPES` imported from `@/data/school-types`
- [x] T026 [P] [US5] Update `frontend/src/services/auth-service.ts` — add `schoolName?: string` and `schoolType?: string` to `RegisterRequest` and include in the `register()` call payload

**Checkpoint**: School name + type saved to DB end-to-end ✅

---

## Phase 8: User Story 6 — المراحل الدراسية الموسَّعة والصفوف (Priority: P1)

**Goal**: قائمة المرحلة تشمل الابتدائي والإعدادي والأزهري والأمريكي مع الصفوف الصحيحة لكل مرحلة.

**Independent Test**: فتح Step 03 → اختيار "أزهري" → التحقق من ظهور 15 صفاً في ثلاث مجموعات → اختيار "أمريكي" → ظهور Grade 1-12.

- [x] T027 [US6] Update `frontend/src/components/registration/AcademicFields.tsx` — expand `EducationStage` type to `'Secondary' | 'Baccalaureate' | 'Primary' | 'Preparatory' | 'Azhari' | 'American'`; expand `GradeLevel` type with all new values (PrimaryGrade1-6, PrepGrade1-3, SecondaryGrade3, AzhariPrimary1-6, AzhariPrep1-3, AzhariSecondary1-3, AmericanGrade1-12); update `GRADES_BY_STAGE` map to include all 6 stages with their correct grade `{value, label}` arrays; add Arabic labels for all grades (e.g., الأول الابتدائي, الثاني الإعدادي, Grade 1...); for Azhari, group optgroups into ابتدائي/إعدادي/ثانوي using `<optgroup>` elements; keep `requiresTrack` returning false for all new stages
- [x] T028 [US6] Update `frontend/src/components/forms/RegistrationForm.tsx` — update the Zod `educationStage` enum validation to include the new stages: `z.enum(['Secondary', 'Baccalaureate', 'Primary', 'Preparatory', 'Azhari', 'American'], ...)`; ensure `AcademicData` type in the handler is updated to match

**Checkpoint**: كل المراحل والصفوف الجديدة تظهر بشكل صحيح ✅

---

## Phase 9: User Story 7 — رقم الطالب والتحقق عبر واتساب (Priority: P2)

**Goal**: رقم الطالب المُولَّد تلقائياً يظهر في خطوة إتمام التسجيل مع زر "تحقق عبر واتساب".

**Independent Test**: إتمام التسجيل → في Step 04 → رؤية رقم الهاتف المُدخَل + اسم الطالب كمعلومات مراجعة + زر "تحقق عبر واتساب" يفتح wa.me.

> **ملاحظة**: رقم الطالب هنا = رقم هاتفه (وسيلة التعريف). لا يوجد student number منفصل في الـ schema الحالي. الزر يُرسل واتساب لتأكيد الرقم.

- [x] T029 [US7] Update `frontend/src/components/forms/RegistrationForm.tsx` — import `openWhatsAppVerification` from `@/utils/whatsapp-utils`; in Step 3 (case default — security), add a review summary card above the password fields showing: `fullName`, `phoneNumber`, and a "تحقق عبر واتساب" button that calls `openWhatsAppVerification(formData.phoneNumber, formData.fullName)`; the button should be styled using existing design tokens (gradient CTA style per constitution)

**Checkpoint**: المستخدم يستطيع التحقق من رقمه عبر واتساب قبل إنهاء التسجيل ✅

---

## Phase 10: Admin Panel — تحديث واجهة الإدارة

**Purpose**: الحقول الجديدة تظهر في لوحة إدارة بيانات الطالب.

- [x] T030 [P] Update `backend/src/NaderGorge.Application/Features/Admin/Queries/StudentProfileExtendedDto.cs` — add: `Nationality (string?)`, `MotherPhone (string?)`, `FatherDateOfBirth (DateTime?)`, `MotherDateOfBirth (DateTime?)`, `SchoolType (string?)` (as string label, not enum), `IsFatherAlive (bool)`, `IsMotherAlive (bool)` properties
- [x] T031 [P] Update `backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs` — map the new `StudentProfile` fields to the updated DTO in the query projection
- [x] T032 Update the admin student profile frontend view to display the new fields (Nationality, SchoolName, SchoolType, FatherDateOfBirth, MotherDateOfBirth, MotherPhone, IsFatherAlive, IsMotherAlive) — locate the admin student profile component and add appropriate display rows using `AdminDataTable` pattern

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Validation edge cases, preview panel updates, field label consistency.

- [x] T033 Update `frontend/src/components/forms/RegistrationForm.tsx` `renderPreviewPanel()` — Step 0 preview: add nationality badge to the student card; Step 1 preview: add father/mother status indicators conditionally; Step 2 preview: add school name and type tags
- [x] T034 Update `frontend/src/components/forms/RegistrationForm.tsx` — ensure `handleChange` resets `motherPhone`, `fatherDateOfBirth` to `''` when `isFatherAlive` is toggled to false; reset `motherDateOfBirth` when `isMotherAlive` is toggled to false
- [x] T035 [P] Add `'motherPhone'`, `'fatherDateOfBirth'`, `'motherDateOfBirth'` to `STEP_FIELDS[1]` in `RegistrationForm.tsx` for proper step validation routing
- [x] T036 [P] Review all Arabic field labels in `RegistrationForm.tsx` for consistency: ensure labels match spec exactly (رقم تليفون الأب، رقم تليفون الأم، عيد ميلاد الأب، عيد ميلاد الأم، الاسم الرباعي باللغة العربية، الجنسية، اسم المدرسة، نوع المدرسة)
- [x] T037 Update `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs` FluentValidation — add conditional validation: `RuleFor(x => x.FatherDateOfBirth).LessThan(DateTime.UtcNow).When(x => x.FatherDateOfBirth.HasValue).WithMessage("تاريخ ميلاد الأب غير صالح")`; same for `MotherDateOfBirth`
- [x] T038 [P] Run quickstart.md testing checklist manually — verify all 8 test scenarios pass (new registration with all fields, old registration unaffected, Azhari stage, American stage, birthday calc, father deceased, WhatsApp button, nationality list)
- [x] T039 [P] Verify TypeScript strict mode passes: run `npx tsc --noEmit` in `frontend/` — fix any type errors from new form fields
- [x] T040 [P] Verify .NET build: run `dotnet build` in `backend/src/NaderGorge.API` — ensure no compiler warnings from new enum values or properties
- [x] T041 Update `specs/025-student-profile-v2/checklists/requirements.md` — mark all items as complete post-implementation
- [x] T042 [P] Commit all changes: `git add -A && git commit -m "feat(025): student profile v2 - nationality, school, extended stages, parent birthdays, whatsapp verify"`

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup: T001-T004)         — No deps, start immediately, all [P]
Phase 2 (Foundation: T005-T011)    — No deps, start immediately; BLOCKS backend app layer
Phase 3-9 (User Stories)           — Backend tasks need Phase 2; Frontend tasks need Phase 1
Phase 10 (Admin)                   — Needs Phase 2 (backend domain) complete
Phase 11 (Polish: T033-T042)       — Needs all user story phases complete
```

### User Story Dependencies

| Story | Depends On | Can Parallel With |
|---|---|---|
| US1 (Nationality) | Phase 1 + 2 | US2, US5, US6 (different files) |
| US2 (Birthday Calc) | Phase 1 (T003) | US1, US3, US5 |
| US3 (Parent Status) | Phase 1 + 2 | US1, US2, US5 |
| US4 (Parent Birthdays) | US3 (T019 adds fields) | — |
| US5 (School) | Phase 1 + 2 | US1, US2, US3 |
| US6 (Stages) | Phase 2 (T005, T006) | US1, US2, US5 |
| US7 (WhatsApp) | Phase 1 (T004), Phase 3+ | — |

### Within Each User Story

- Backend record/validation → Backend handler → Frontend form → Auth service type
- Frontend static data (Phase 1) → Form field addition → Zod validation update

---

## Parallel Execution Examples

### Phase 1 — All 4 tasks in parallel:
```bash
T001: Create arab-nationalities.ts
T002: Create school-types.ts
T003: Create birthday-utils.ts
T004: Create whatsapp-utils.ts
```

### Phase 2 — Enums in parallel, then entity, then migration:
```bash
# Wave 1 (parallel):
T005: Expand EducationStage.cs
T006: Expand GradeLevel.cs
T007: Create SchoolType.cs

# Wave 2 (depends on Wave 1):
T008: Update StudentProfile.cs
T009: Update AcademicValidationService.cs

# Wave 3 (depends on T008):
T010: Generate migration
T011: Apply migration
```

### US1 + US5 backend tasks in parallel (different files):
```bash
T012: RegisterCommand + Nationality (backend)
T023: RegisterCommand + SchoolName/SchoolType (backend)
# Wait — same file! Do sequentially: T012 then T023
```

> ⚠️ `RegisterCommand.cs` is modified by multiple stories — do those tasks sequentially per story phase order to avoid conflicts.

---

## Implementation Strategy

### MVP First (US1 — Nationality only)

1. Complete Phase 1 (T001-T004) — 15 min
2. Complete Phase 2 (T005-T011) — 30 min
3. Complete Phase 3/US1 (T012-T015) — 20 min
4. **Validate**: Register a student with nationality → confirm it saves → admin sees it
5. Deploy/demo nationality field ✅

### Full Incremental Delivery

```
Phase 1+2 → Foundation (45 min)
   ↓
US1 (Nationality) → 20 min
US2 (Birthday Calc) → 15 min   ← just display logic
US3 (Parent Status) → 40 min   ← conditional rendering + backend validator update
US4 (Parent Birthdays) → 15 min ← display logic like US2
US5 (School) → 25 min
US6 (Stages) → 35 min          ← biggest frontend change (AcademicFields)
US7 (WhatsApp) → 15 min        ← pure frontend
   ↓
Admin Panel (T030-T032) → 20 min
Polish (T033-T042) → 30 min
```

**Estimated total**: ~4.5 hours end-to-end

---

## Notes

- `RegisterCommand.cs` is touched by US1, US3, US5 — work **sequentially** on that file to avoid merge conflicts
- `RegistrationForm.tsx` is touched by almost every story — work through phases **in order**
- `AcademicFields.tsx` is touched only by US6 — fully independent [P] with other story frontend work
- EF Core migration (T010/T011) only adds 6 nullable columns — safe zero-downtime migration
- All new backend fields are `nullable` — existing student records unaffected (backward compatible)
- `EducationStage` and `GradeLevel` enum values stored as `int` — no DB-level migration needed for new enum names, only code changes

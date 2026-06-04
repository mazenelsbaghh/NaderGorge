# Spec Kit Preparation Workflow / سير عمل إعداد مواصفات التجهيز

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Admin Student Profile — Complete Data Display

**Input**: Design documents from `/specs/073-admin-student-profile-complete/`
**Prerequisites**: plan.md (required), spec.md (required for user stories)
**Tests**: Tests are OPTIONAL — rely on runtime compilation and manual verification.

---

## Phase 1: Backend DTO Expansion

**Purpose**: Add missing fields to the backend DTO and populate them in the query handler.

- [ ] T001 In [StudentProfileExtendedDto.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Queries/StudentProfileExtendedDto.cs), add the following properties to the `StudentProfileExtendedDto` class:
  - `public DateTime? DateOfBirth { get; set; }`
  - `public string? Gender { get; set; }`
  - `public string? Governorate { get; set; }`
  - `public string? Address { get; set; }`
  - `public string? StudentCode { get; set; }`
  - `public bool IsProfileComplete { get; set; }`
  - `public string? EducationStage { get; set; }`
  - `public string? StudyTrack { get; set; }`
  - Remove `public string Email { get; set; } = string.Empty;`

- [ ] T002 In [GetStudentProfileDetailQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs), inside the `return new StudentProfileExtendedDto { ... }` block (around line 122-164), add the following field assignments:
  - `DateOfBirth = user.StudentProfile?.DateOfBirth,`
  - `Gender = user.StudentProfile?.Gender.ToString(),`
  - `Governorate = user.StudentProfile?.Governorate,`
  - `Address = user.StudentProfile?.Address,`
  - `StudentCode = user.StudentProfile?.StudentCode,`
  - `IsProfileComplete = user.IsProfileComplete,`
  - `EducationStage = user.StudentProfile?.EducationStage.ToString(),`
  - `StudyTrack = user.StudentProfile?.StudyTrack?.ToString(),`
  - Remove the line `Email = string.Empty,`

**Checkpoint**: Run `dotnet build` in `backend/` — must compile with zero errors.

---

## Phase 2: Frontend DTO Update

- [ ] T003 In [admin-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/admin-service.ts), update the `StudentProfileExtendedDto` interface (around lines 89-129):
  - Add: `dateOfBirth?: string;`
  - Add: `gender?: string;`
  - Add: `governorate?: string;`
  - Add: `address?: string;`
  - Add: `studentCode?: string;`
  - Add: `isProfileComplete?: boolean;`
  - Add: `educationStage?: string;`
  - Add: `studyTrack?: string;`
  - Remove the `email: string;` line.

**Checkpoint**: Run `npx tsc --noEmit` in `frontend/` — must compile with zero type errors.

---

## Phase 3: Frontend UI — Redesign Profile Overview Tab

- [ ] T004 In [[id]/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/users/%5Bid%5D/page.tsx), add these Arabic enum label mapping helper functions BEFORE the `return` statement (around line 104). These must be defined inside the component or above it:

```typescript
const mapGender = (g?: string) => {
  if (!g) return 'غير متوفر';
  const m: Record<string, string> = { Male: 'ذكر', Female: 'أنثى' };
  return m[g] || g;
};

const mapSchoolType = (t?: string) => {
  if (!t) return 'غير متوفر';
  const m: Record<string, string> = {
    Government: 'حكومية', Language: 'لغات', Experimental: 'تجريبية',
    Private: 'خاصة', Azhari: 'أزهرية', American: 'أمريكية',
  };
  return m[t] || t;
};

const mapEducationStage = (s?: string) => {
  if (!s) return 'غير متوفر';
  const m: Record<string, string> = { Secondary: 'ثانوية', Baccalaureate: 'بكالوريا' };
  return m[s] || s;
};

const mapGradeLevel = (g?: string) => {
  if (!g) return 'غير متوفر';
  const m: Record<string, string> = {
    FirstSecondary: 'أولى ثانوي', SecondSecondary: 'ثانية ثانوي',
    FirstBaccalaureate: 'أولى بكالوريا', SecondBaccalaureate: 'ثانية بكالوريا',
  };
  return m[g] || g;
};

const mapStudyTrack = (t?: string) => {
  if (!t) return 'لا ينطبق';
  const m: Record<string, string> = {
    Science: 'علمي', Arts: 'أدبي',
    MedicineAndLifeSciences: 'الطب وعلوم الحياة',
    EngineeringAndComputerScience: 'الهندسة وعلوم الحاسب',
    Business: 'قطاع الأعمال', ArtsAndHumanities: 'الآداب والفنون',
  };
  return m[t] || t;
};

const formatDate = (d?: string | null) => {
  if (!d) return 'غير متوفر';
  return new Date(d).toLocaleDateString('en-GB');
};
```

- [ ] T005 In [[id]/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/users/%5Bid%5D/page.tsx), replace the entire "البيانات الشخصية" section (lines 148-182 approximately — the `<div className="rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm">` block inside the `activeTab === 'overview'` branch) with a new comprehensive layout containing 4 sections:

**Section 1: البيانات الشخصية** — grid of: الاسم بالكامل (`fullName`), رقم الهاتف (`phone`), هاتف إضافي (`secondaryPhone`), تاريخ الميلاد (`dateOfBirth` formatted via `formatDate`), النوع (`gender` via `mapGender`), الجنسية (`nationality`), كود الطالب (`studentCode`), حالة الملف (`isProfileComplete` → مكتمل/غير مكتمل)

**Section 2: بيانات الوالدين** — grid of: هاتف ولي الأمر (أب) (`parentPhone`), هاتف الأم (`motherPhone`), هاتف ولي أمر إضافي (`secondaryParentPhone`), حالة الأب (`isFatherAlive` → show colored badge), حالة الأم (`isMotherAlive` → show colored badge), تاريخ ميلاد الأب (`fatherDateOfBirth` via `formatDate`), تاريخ ميلاد الأم (`motherDateOfBirth` via `formatDate`)

**Section 3: البيانات الأكاديمية** — grid of: المرحلة الدراسية (`educationStage` via `mapEducationStage`), الصف الدراسي (`grade` via `mapGradeLevel`), الشعبة/التخصص (`studyTrack` via `mapStudyTrack`), اسم المدرسة (`schoolName`), نوع المدرسة (`schoolType` via `mapSchoolType`)

**Section 4: العنوان** — grid of: المحافظة (`governorate`), المنطقة/الحي (`district`), العنوان التفصيلي (`address`)

Each section MUST:
- Use a `<h3>` heading with `text-[length:var(--admin-font-title-md)] font-bold` class
- Use a `rounded-3xl bg-[var(--admin-bg)] p-8 shadow-sm` container
- Use a `grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6` for fields
- Each field uses: `<p className="text-[var(--admin-muted)] text-sm mb-1">Label</p>` + `<p className="text-[var(--admin-text)] font-semibold">Value || 'غير متوفر'</p>`
- Parent alive/deceased status uses colored text: emerald for alive, red for deceased (matching existing expandedRowRender pattern)
- Import `Calendar, MapPin, GraduationCap, UsersRound` from lucide-react for section icons

- [ ] T006 In [[id]/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/users/%5Bid%5D/page.tsx), remove any reference to `studentData?.email` from the JSX (the "البريد الإلكتروني" field in the current overview).

**Checkpoint**: Run `npx tsc --noEmit` + `npm run lint` in `frontend/` — zero errors.

---

## Phase 4: Build Verification & Docs Update

- [ ] T007 Run `dotnet build` and `dotnet test` in `backend/` to confirm clean build and all 12 tests pass.
- [ ] T008 Run `npx tsc --noEmit` and `npm run lint` in `frontend/` to confirm zero type errors and zero new lint errors.
- [ ] T009 Update master plan files in `docs/` with completed items.

---

## Dependencies & Execution Order

- **Phase 1** (Backend DTO): No dependencies.
- **Phase 2** (Frontend DTO): Depends on Phase 1 design but can be done independently.
- **Phase 3** (Frontend UI): Depends on Phase 2 for the interface types.
- **Phase 4** (Verification): Depends on all phases.

# Implementation Plan: Admin Student Profile — Complete Data Display

**Branch**: `073-admin-student-profile-complete` | **Date**: 2026-06-04 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/073-admin-student-profile-complete/spec.md)

---

## Summary

The admin student profile detail page (`/admin/users/[id]`) currently displays only 6 fields out of 25+ stored in the database. This feature adds all missing fields to the backend DTO, populates them in the query handler, updates the frontend TypeScript interface, and redesigns the "نظرة عامة" tab to show all data organized in clearly labeled sections.

---

## Technical Context

- **Language/Version**: C# 13 (.NET 9.0) Backend, TypeScript 5.x / Next.js 16.2.1 Frontend
- **Primary Dependencies**: MediatR, Entity Framework Core 9.0, Lucide React
- **Storage**: PostgreSQL (no schema changes — reading existing columns)
- **Target Platform**: Docker, Modern Browsers
- **Constraints**: No database migrations. No new API endpoints. Only DTO expansion and UI enrichment.

---

## Proposed Changes

### Backend: DTO Expansion

#### [MODIFY] [StudentProfileExtendedDto.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Queries/StudentProfileExtendedDto.cs)

Add the following missing properties to `StudentProfileExtendedDto`:

```csharp
// Personal
public DateTime? DateOfBirth { get; set; }
public string? Gender { get; set; }
public string? Governorate { get; set; }
public string? Address { get; set; }
public string? StudentCode { get; set; }
public bool IsProfileComplete { get; set; }

// Academic
public string? EducationStage { get; set; }
public string? StudyTrack { get; set; }
```

Remove `Email` field (never populated, misleading).

---

#### [MODIFY] [GetStudentProfileDetailQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs)

Update the handler to populate the new fields from the loaded entity:

```csharp
DateOfBirth = user.StudentProfile?.DateOfBirth,
Gender = user.StudentProfile?.Gender.ToString(),
Governorate = user.StudentProfile?.Governorate,
Address = user.StudentProfile?.Address,
StudentCode = user.StudentProfile?.StudentCode,
IsProfileComplete = user.IsProfileComplete,
EducationStage = user.StudentProfile?.EducationStage.ToString(),
StudyTrack = user.StudentProfile?.StudyTrack?.ToString(),
```

Remove `Email = string.Empty` assignment.

---

### Frontend: TypeScript DTO Update

#### [MODIFY] [admin-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/admin-service.ts)

Update `StudentProfileExtendedDto` interface to add:

```typescript
dateOfBirth?: string;
gender?: string;
governorate?: string;
address?: string;
studentCode?: string;
isProfileComplete?: boolean;
educationStage?: string;
studyTrack?: string;
```

Remove `email` field.

---

### Frontend: Admin Student Profile Page

#### [MODIFY] [[id]/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/users/%5Bid%5D/page.tsx)

Redesign the "نظرة عامة" tab's "البيانات الشخصية" section. Replace the current 6-field grid with organized sections:

**Section 1: البيانات الشخصية (Personal Data)**
- الاسم بالكامل, رقم الهاتف, هاتف إضافي, تاريخ الميلاد, النوع, الجنسية, كود الطالب

**Section 2: بيانات الوالدين (Parent/Family Data)**
- هاتف ولي الأمر (أب), هاتف الأم, هاتف ولي أمر إضافي
- حالة الأب (حي/متوفى), حالة الأم (حية/متوفاة)
- تاريخ ميلاد الأب, تاريخ ميلاد الأم

**Section 3: البيانات الأكاديمية (Academic Data)**
- المرحلة الدراسية, الصف الدراسي, الشعبة/التخصص
- اسم المدرسة, نوع المدرسة

**Section 4: العنوان ومعلومات الاتصال (Address & Contact)**
- المحافظة, المنطقة/الحي, العنوان التفصيلي

Add helper functions for Arabic enum label mapping:
- `mapGender()`: Male → ذكر, Female → أنثى
- `mapSchoolType()`: Government → حكومية, Language → لغات, etc.
- `mapEducationStage()`: Secondary → ثانوية, Baccalaureate → بكالوريا
- `mapGradeLevel()`: FirstSecondary → أولى ثانوي, etc.
- `mapStudyTrack()`: Science → علمي, Arts → أدبي, etc.

---

## Verification Plan

### Automated Tests
- `dotnet build` — backend compiles cleanly
- `dotnet test` — all 12 existing tests pass
- `npx tsc --noEmit` — frontend type-checks with zero errors
- `npm run lint` — zero new lint errors

### Manual Verification
- Navigate to `/admin/users/{studentId}` and verify all 25+ fields are visible
- Test with a student who has null optional fields to verify "غير متوفر" fallback
- Test with students who have deceased parents to verify red status badges

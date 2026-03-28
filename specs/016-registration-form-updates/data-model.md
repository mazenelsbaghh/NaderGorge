# Data Model: تحديث نموذج تسجيل الطالب

**Feature**: 016-registration-form-updates
**Date**: 2026-03-28

## Entity Changes

### StudentProfile (MODIFY)

**File**: `backend/src/NaderGorge.Domain/Entities/StudentProfile.cs`

| Field | Type | Required | Change | Notes |
|-------|------|----------|--------|-------|
| StudentCode | string | ~~Required~~ → **Optional** | MODIFY | Was `string.Empty` required. Now `string?` nullable. Existing values preserved |
| District | string? | Optional | ADD | Neighborhood/area within the governorate. Max 200 chars |
| SecondaryPhone | string? | Optional | ADD | Student's second phone number. Egyptian format. Max 20 chars |
| SecondaryParentPhone | string? | Optional | ADD | Parent/guardian's second phone number. Max 20 chars |

**Unchanged fields**: UserId, DateOfBirth, Gender, Governorate, Address, ParentPhone, IsFatherAlive, IsMotherAlive, EducationStage, GradeLevel, StudyTrack

### Updated Entity Definition

```csharp
public class StudentProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // --- Personal data ---
    public string? StudentCode { get; set; }              // Was required, now optional
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string Governorate { get; set; } = string.Empty;
    public string? District { get; set; }                  // NEW: Neighborhood/area
    public string Address { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }            // NEW: Student's 2nd phone

    // --- Parent data ---
    public string? ParentPhone { get; set; }
    public string? SecondaryParentPhone { get; set; }      // NEW: Parent's 2nd phone
    public bool IsFatherAlive { get; set; } = true;
    public bool IsMotherAlive { get; set; } = true;

    // --- Academic data (conditional) ---
    public EducationStage EducationStage { get; set; }
    public GradeLevel GradeLevel { get; set; }
    public StudyTrack? StudyTrack { get; set; }
}
```

## EF Core Configuration Changes

**File**: `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`

```csharp
// StudentProfile config changes:
e.Property(s => s.StudentCode).HasMaxLength(100);           // Remove .IsRequired()
e.Property(s => s.District).HasMaxLength(200);              // NEW
e.Property(s => s.SecondaryPhone).HasMaxLength(20);         // NEW
e.Property(s => s.SecondaryParentPhone).HasMaxLength(20);   // NEW
```

## Migration SQL (Expected)

```sql
-- Add new columns
ALTER TABLE student_profiles ADD COLUMN "District" varchar(200) NULL;
ALTER TABLE student_profiles ADD COLUMN "SecondaryPhone" varchar(20) NULL;
ALTER TABLE student_profiles ADD COLUMN "SecondaryParentPhone" varchar(20) NULL;

-- Make StudentCode optional
ALTER TABLE student_profiles ALTER COLUMN "StudentCode" DROP NOT NULL;
```

## Frontend Data Model

### RegisterData Interface (MODIFY)

**File**: `frontend/src/services/auth-service.ts`

```typescript
export interface RegisterData {
  fullName: string;
  phoneNumber: string;
  secondaryPhone?: string;           // NEW
  password: string;
  // studentCode: REMOVED
  dateOfBirth: string;
  gender: 'Male' | 'Female';
  governorate: string;
  district: string;                   // NEW
  address: string;
  parentPhone: string;
  secondaryParentPhone?: string;      // NEW
  isFatherAlive: boolean;
  isMotherAlive: boolean;
  educationStage: 'Secondary' | 'Baccalaureate';
  gradeLevel: string;
  studyTrack?: string;
}
```

### GovernorateDistricts Data (NEW)

**File**: `frontend/src/data/governorate-districts.ts`

```typescript
export const GOVERNORATE_DISTRICTS: Record<string, string[]> = {
  'القاهرة': ['مصر الجديدة', 'مدينة نصر', 'المعادي', ...],
  'الجيزة': ['الدقي', 'المهندسين', 'الهرم', '6 أكتوبر', ...],
  // ... all 27 governorates
};
```

## Validation Rules

### Backend (FluentValidation)

| Field | Rule | Error Message |
|-------|------|---------------|
| StudentCode | Removed `NotEmpty()` | N/A |
| District | `MaximumLength(200)` | "District name too long" |
| SecondaryPhone | `Matches(@"^01[0125]\d{8}$")` when not empty | "Invalid Egyptian phone number" |
| SecondaryParentPhone | `Matches(@"^01[0125]\d{8}$")` when not empty | "Invalid Egyptian parent phone number" |

### Frontend (Zod)

| Field | Rule | Error Message |
|-------|------|---------------|
| studentCode | REMOVED from schema | N/A |
| district | `z.string().min(1)` | "اختر المنطقة / الحي" |
| secondaryPhone | `z.string().regex(...).optional().or(z.literal(''))` | "رقم هاتف مصري غير صالح" |
| secondaryParentPhone | `z.string().regex(...).optional().or(z.literal(''))` | "رقم هاتف ولي الأمر الإضافي غير صالح" |

## Relationships

No new relationships. All new fields are scalar properties on the existing `StudentProfile` entity.

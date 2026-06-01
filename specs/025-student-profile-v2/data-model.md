# Data Model: Student Profile V2

**Branch**: `025-student-profile-v2` | **Date**: 2026-03-30

---

## Entity Changes

### StudentProfile (Updated)

```
StudentProfile
├── UserId                    Guid          FK → User
│
│── ── Personal Data ──────────────────────────────────────────
├── StudentCode               string?       اختياري — لا يظهر في الـ UI
├── DateOfBirth               DateTime      تاريخ الميلاد (UTC)
├── Gender                    Gender        Male | Female
├── Nationality               string?       NEW ← قيمة من قائمة الجنسيات العربية
├── Governorate               string
├── District                  string?       الحي/المنطقة
├── Address                   string
├── SecondaryPhone            string?       رقم هاتف الطالب الإضافي
│
│── ── Parent Data ────────────────────────────────────────────
├── ParentPhone               string?       رقم هاتف الأب (الرقم الرئيسي)
├── SecondaryParentPhone      string?       رقم هاتف ولي الأمر الإضافي
├── MotherPhone               string?       NEW ← رقم هاتف الأم (شرطي/اختياري)
├── IsFatherAlive             bool          default = true
├── IsMotherAlive             bool          default = true
├── FatherDateOfBirth         DateTime?     NEW ← عيد ميلاد الأب (اختياري)
├── MotherDateOfBirth         DateTime?     NEW ← عيد ميلاد الأم (اختياري)
│
│── ── School Data ────────────────────────────────────────────
├── SchoolName                string?       NEW ← اسم المدرسة (اختياري)
├── SchoolType                SchoolType?   NEW ← نوع المدرسة (enum nullable)
│
│── ── Academic Data ──────────────────────────────────────────
├── EducationStage            EducationStage   موسَّع (Primary, Prep, Azhari, American)
├── GradeLevel                GradeLevel       موسَّع (صفوف جديدة لكل مرحلة)
└── StudyTrack                StudyTrack?      بدون تغيير
```

---

## New Enums

### SchoolType (جديد)

```csharp
public enum SchoolType
{
    Government   = 0,   // حكومية
    Language     = 1,   // لغات
    Experimental = 2,   // تجريبية
    Private      = 3,   // خاصة
    Azhari       = 4,   // أزهرية
    American     = 5,   // أمريكية
}
```

### EducationStage (موسَّع — إضافة فقط)

```csharp
public enum EducationStage
{
    Secondary      = 0,   // ثانوية (موجود)
    Baccalaureate  = 1,   // بكالوريا (موجود)
    Primary        = 2,   // ابتدائي (جديد)
    Preparatory    = 3,   // إعدادي (جديد)
    Azhari         = 4,   // أزهري (جديد)
    American       = 5,   // أمريكي (جديد)
}
```

### GradeLevel (موسَّع — إضافة فقط)

```csharp
public enum GradeLevel
{
    // ── موجودة (محفوظة) ──────────────────
    FirstSecondary       = 0,
    SecondSecondary      = 1,
    FirstBaccalaureate   = 2,
    SecondBaccalaureate  = 3,

    // ── ابتدائي (Primary: 10-15) ──────────
    PrimaryGrade1        = 10,
    PrimaryGrade2        = 11,
    PrimaryGrade3        = 12,
    PrimaryGrade4        = 13,
    PrimaryGrade5        = 14,
    PrimaryGrade6        = 15,

    // ── إعدادي (Preparatory: 20-22) ───────
    PrepGrade1           = 20,
    PrepGrade2           = 21,
    PrepGrade3           = 22,

    // ── ثانوي الثالث (موجود: 0,1 + جديد 31) ─
    SecondaryGrade3      = 31,

    // ── أزهري ابتدائي (40-45) ─────────────
    AzhariPrimary1       = 40,
    AzhariPrimary2       = 41,
    AzhariPrimary3       = 42,
    AzhariPrimary4       = 43,
    AzhariPrimary5       = 44,
    AzhariPrimary6       = 45,

    // ── أزهري إعدادي (50-52) ──────────────
    AzhariPrep1          = 50,
    AzhariPrep2          = 51,
    AzhariPrep3          = 52,

    // ── أزهري ثانوي (60-62) ───────────────
    AzhariSecondary1     = 60,
    AzhariSecondary2     = 61,
    AzhariSecondary3     = 62,

    // ── أمريكي (70-81) ────────────────────
    AmericanGrade1       = 70,
    AmericanGrade2       = 71,
    AmericanGrade3       = 72,
    AmericanGrade4       = 73,
    AmericanGrade5       = 74,
    AmericanGrade6       = 75,
    AmericanGrade7       = 76,
    AmericanGrade8       = 77,
    AmericanGrade9       = 78,
    AmericanGrade10      = 79,
    AmericanGrade11      = 80,
    AmericanGrade12      = 81,
}
```

---

## Validation Rules

### Backend (FluentValidation)

| Field | Rule |
|---|---|
| `Nationality` | Optional. MaxLength(100). If provided, must be in the list of 22 Arab nationalities (validated by set membership). |
| `MotherPhone` | Optional unless `IsMotherAlive = true` AND admin requires it. Regex: `^01[0125]\d{8}$` |
| `FatherDateOfBirth` | Optional. Must be in the past if provided. |
| `MotherDateOfBirth` | Optional. Must be in the past if provided. |
| `SchoolName` | Optional. MaxLength(300). |
| `SchoolType` | Optional. Must be valid enum value if provided. |
| `EducationStage` | Required. Must be valid expanded enum. |
| `GradeLevel` | Required. Must be valid for the chosen `EducationStage`. |

### Frontend (Zod)

| Field | Rule |
|---|---|
| `nationality` | `z.string().optional()` — default empty |
| `motherPhone` | `z.string().regex(egyptianPhoneRegex).optional().or(z.literal(''))` |
| `fatherDateOfBirth` | `z.string().optional()` — shown only if `isFatherAlive` |
| `motherDateOfBirth` | `z.string().optional()` — shown only if `isMotherAlive` |
| `schoolName` | `z.string().optional()` |
| `schoolType` | `z.string().optional()` |

---

## State Transitions (Conditional Fields)

```
isFatherAlive = true  →  show: [ParentPhone (required), FatherDateOfBirth (optional)]
isFatherAlive = false →  hide: [ParentPhone, FatherDateOfBirth] — ParentPhone not required

isMotherAlive = true  →  show: [MotherPhone (optional), MotherDateOfBirth (optional)]
isMotherAlive = false →  hide: [MotherPhone, MotherDateOfBirth]

dateOfBirth changed   →  auto-compute: [ageYears, daysToNextBirthday] — display only
fatherDateOfBirth changed → auto-compute: [daysToDadBirthday] — display only
motherDateOfBirth changed → auto-compute: [daysTomomBirthday] — display only
```

---

## Migration Delta

```sql
-- New columns in StudentProfiles table:
ALTER TABLE "StudentProfiles" ADD "Nationality"         varchar(100)  NULL;
ALTER TABLE "StudentProfiles" ADD "MotherPhone"         varchar(20)   NULL;
ALTER TABLE "StudentProfiles" ADD "FatherDateOfBirth"   timestamp     NULL;
ALTER TABLE "StudentProfiles" ADD "MotherDateOfBirth"   timestamp     NULL;
ALTER TABLE "StudentProfiles" ADD "SchoolName"          varchar(300)  NULL;
ALTER TABLE "StudentProfiles" ADD "SchoolType"          integer       NULL;

-- Note: EducationStage & GradeLevel are stored as int — enum expansion is code-only,
-- no DB migration needed for new enum values.
```

---

## StudentProfileExtendedDto (Admin — Updated)

```csharp
public class StudentProfileExtendedDto
{
    // ... existing fields ...
    public string? Nationality { get; set; }           // NEW
    public string? MotherPhone { get; set; }           // NEW
    public DateTime? FatherDateOfBirth { get; set; }   // NEW
    public DateTime? MotherDateOfBirth { get; set; }   // NEW
    public string? SchoolName { get; set; }            // (existed as stub) — now populated
    public string? SchoolType { get; set; }            // NEW (string label)
    public bool IsFatherAlive { get; set; }            // NEW — surfaced in admin
    public bool IsMotherAlive { get; set; }            // NEW — surfaced in admin
}
```

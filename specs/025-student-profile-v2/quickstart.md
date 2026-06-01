# Quickstart: Student Profile V2

**Branch**: `025-student-profile-v2` | **Date**: 2026-03-30

---

## تسلسل التنفيذ

### 1. Backend — Domain & Enums

```bash
# الملفات المطلوب تحديثها:
backend/src/NaderGorge.Domain/Enums/EducationStage.cs   # إضافة: Primary, Preparatory, Azhari, American
backend/src/NaderGorge.Domain/Enums/GradeLevel.cs       # إضافة: كل الصفوف الجديدة (10-81)
backend/src/NaderGorge.Domain/Enums/SchoolType.cs       # جديد: Government, Language, Experimental, Private, Azhari, American
backend/src/NaderGorge.Domain/Entities/StudentProfile.cs # إضافة: Nationality, MotherPhone, FatherDateOfBirth, MotherDateOfBirth, SchoolName, SchoolType
```

### 2. Backend — Application Layer

```bash
# RegisterCommand: إضافة properties جديدة + validators
backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs

# AcademicValidationService: توسيع للمراحل الجديدة
backend/src/NaderGorge.Application/Services/AcademicValidationService.cs

# Admin DTO: إضافة الحقول الجديدة
backend/src/NaderGorge.Application/Features/Admin/Queries/StudentProfileExtendedDto.cs
backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs
```

### 3. Backend — Migration

```bash
# من داخل backend/src/NaderGorge.Infrastructure
dotnet ef migrations add StudentProfileV2 \
  --project ../NaderGorge.Infrastructure \
  --startup-project ../../NaderGorge.API

dotnet ef database update
```

**Migration يضيف الأعمدة التالية فقط (nullable):**
- `"Nationality"` varchar(100) NULL
- `"MotherPhone"` varchar(20) NULL
- `"FatherDateOfBirth"` timestamp NULL
- `"MotherDateOfBirth"` timestamp NULL
- `"SchoolName"` varchar(300) NULL
- `"SchoolType"` integer NULL

> **تحذير**: `EducationStage` و`GradeLevel` مخزَّنان كـ int — لا تغيير في الـ schema، فقط توسيع الـ enum في C#.

### 4. Frontend — Static Data

```bash
# ملفات جديدة:
frontend/src/data/arab-nationalities.ts
frontend/src/data/school-types.ts
```

### 5. Frontend — AcademicFields Component

```bash
# تحديث:
frontend/src/components/registration/AcademicFields.tsx
# - إضافة types جديدة للـ stages والـ grades
# - تحديث GRADES_BY_STAGE لكل المراحل الجديدة
# - لا track للمراحل الجديدة (Primary, Prep, Azhari, American)
```

### 6. Frontend — RegistrationForm

```bash
# تحديث:
frontend/src/components/forms/RegistrationForm.tsx
# Step 1 (Identity): + nationality, + age/birthday calculations
# Step 2 (Guardian): + motherPhone, + fatherDateOfBirth, + motherDateOfBirth + conditional logic
# Step 3 (Academic): + schoolName, + schoolType
# Step 4 (Security): + WhatsApp verify button
```

### 7. Frontend — Auth Service

```bash
# تحديث RegisterRequest type:
frontend/src/services/auth-service.ts
```

---

## Birthday Calculation Helper

```typescript
// frontend/src/utils/birthday-utils.ts

export function computeBirthdayInfo(dateOfBirth: string): {
  ageYears: number;
  daysToNextBirthday: number;
} {
  const today = new Date();
  const dob = new Date(dateOfBirth);

  let ageYears = today.getFullYear() - dob.getFullYear();
  const monthDiff = today.getMonth() - dob.getMonth();
  const dayDiff = today.getDate() - dob.getDate();
  if (monthDiff < 0 || (monthDiff === 0 && dayDiff < 0)) {
    ageYears--;
  }

  // Next birthday this year or next year
  const nextBirthday = new Date(
    today.getFullYear(),
    dob.getMonth(),
    dob.getDate()
  );
  if (nextBirthday < today) {
    nextBirthday.setFullYear(today.getFullYear() + 1);
  }
  const msPerDay = 1000 * 60 * 60 * 24;
  const daysToNextBirthday = Math.round(
    (nextBirthday.getTime() - today.getTime()) / msPerDay
  );

  return { ageYears, daysToNextBirthday };
}
```

---

## WhatsApp Verify Button

```typescript
function openWhatsAppVerification(phoneNumber: string, studentName: string) {
  const message = encodeURIComponent(
    `مرحباً! هذا تأكيد لتسجيلك في منصة نادر جورج.\nاسمك: ${studentName}\nرقم هاتفك: ${phoneNumber}\n\nإذا لم تقم بالتسجيل، يرجى التواصل معنا.`
  );
  const phone = phoneNumber.replace(/^0/, '20'); // Egypt: 0 → +20
  window.open(`https://wa.me/${phone}?text=${message}`, '_blank');
}
```

---

## Testing Checklist

- [ ] تسجيل طالب جديد بجميع الحقول الجديدة → يصل للـ DB
- [ ] تسجيل طالب قديم (بدون الحقول الجديدة) → لا يتأثر
- [ ] اختيار مرحلة "أزهري" → تظهر صفوفه الثلاث مجموعات
- [ ] اختيار مرحلة "أمريكي" → تظهر Grade 1 حتى Grade 12
- [ ] إدخال تاريخ ميلاد → يظهر العمر والأيام المتبقية فوراً
- [ ] الأب متوفي → يختفي حقل رقم هاتف الأب وعيد ميلاده
- [ ] زر واتساب → يفتح wa.me برسالة صحيحة
- [ ] حقل الجنسية → يحتوي 22 جنسية عربية

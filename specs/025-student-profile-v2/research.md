# Research: Student Profile V2

**Branch**: `025-student-profile-v2` | **Date**: 2026-03-30

---

## 1. الجنسيات العربية — Arab Nationalities

**Decision**: قائمة ثابتة (static) من 22 جنسية عربية مخزَّنة في ملف `arab-nationalities.ts` في الـ frontend، ومُخزَّنة كـ `string` في الـ DB (وليس enum).

**Rationale**: 
- الجنسيات نادراً ما تتغيير — لا داعي لـ enum في الـ DB
- تخزين الجنسية كـ `string` أبسط وأقل migration cost في المستقبل إن تغيير الخيارات
- قائمة الـ frontend تُعطي UX أفضل (searchable dropdown)

**Alternatives Considered**:
- Enum في C#: يتطلب migration عند إضافة جنسية → مرفوض
- جدول `nationalities` في DB: overkill للقائمة الثابتة → مرفوض

**List**:
مصري، سعودي، إماراتي، كويتي، قطري، بحريني، عُماني، يمني، أردني، لبناني، سوري، عراقي، فلسطيني، ليبي، تونسي، جزائري، مغربي، موريتاني، سوداني، صومالي، جيبوتي، جزر القمر

---

## 2. توسيع المراحل الدراسية — Education Stages Expansion

**Decision**: توسيع `EducationStage` enum بإضافة قيم جديدة تحافظ على القيم القديمة:

```csharp
public enum EducationStage
{
    // ── موجودة (لا تُلمس) ──
    Secondary = 0,       // ثانوية
    Baccalaureate = 1,   // بكالوريا

    // ── جديدة ──
    Primary = 2,         // ابتدائي
    Preparatory = 3,     // إعدادي
    Azhari = 4,          // أزهري
    American = 5,        // أمريكي
}
```

**GradeLevel** يُوسَّع كذلك:

```csharp
public enum GradeLevel
{
    // ── موجودة ──
    FirstSecondary = 0,
    SecondSecondary = 1,
    FirstBaccalaureate = 2,
    SecondBaccalaureate = 3,

    // ── ابتدائي (Primary) ──
    PrimaryGrade1 = 10, PrimaryGrade2 = 11, PrimaryGrade3 = 12,
    PrimaryGrade4 = 13, PrimaryGrade5 = 14, PrimaryGrade6 = 15,

    // ── إعدادي (Preparatory) ──
    PrepGrade1 = 20, PrepGrade2 = 21, PrepGrade3 = 22,

    // ── ثانوي كامل (Secondary ثلاث سنوات) ──
    SecondaryGrade3 = 31, // "الثالث الثانوي" — الجديد

    // ── أزهري ──
    AzhariPrimary1 = 40, AzhariPrimary2 = 41, AzhariPrimary3 = 42,
    AzhariPrimary4 = 43, AzhariPrimary5 = 44, AzhariPrimary6 = 45,
    AzhariPrep1 = 50, AzhariPrep2 = 51, AzhariPrep3 = 52,
    AzhariSecondary1 = 60, AzhariSecondary2 = 61, AzhariSecondary3 = 62,

    // ── أمريكي ──
    AmericanGrade1 = 70, AmericanGrade2 = 71, AmericanGrade3 = 72,
    AmericanGrade4 = 73, AmericanGrade5 = 74, AmericanGrade6 = 75,
    AmericanGrade7 = 76, AmericanGrade8 = 77, AmericanGrade9 = 78,
    AmericanGrade10 = 79, AmericanGrade11 = 80, AmericanGrade12 = 81,
}
```

**Rationale**:
- Integers غير متتالية (gaps: 10s, 20s, 30s...) تُسهِّل إضافة قيم مستقبلية دون تعارض
- القيم القديمة (0-3) محفوظة — الطلاب القدامى غير متأثرين
- EF Core يحفظ `int` في DB — لا حاجة لـ migration ثقيل (فقط لتوسيع enum في C# للـ nullable validation)

**Alternatives Considered**:
- جدول `grades` في DB: flexible ولكن يُعقِّد الـ Registration flow → مرفوض لهذه الـ feature
- Tags/strings بدل enum: تفقد type safety في C# → مرفوض

---

## 3. نوع المدرسة — School Type

**Decision**: `SchoolType` enum جديد مخزَّن كـ `int?` في `StudentProfile`:

```csharp
public enum SchoolType
{
    Government = 0,    // حكومية
    Language = 1,      // لغات
    Experimental = 2,  // تجريبية
    Private = 3,       // خاصة
    Azhari = 4,        // أزهرية
    American = 5,      // أمريكية
}
```

**Rationale**: محدودة وثابتة — enum أنسب من string. Nullable (`int?`) للتوافق مع الحسابات القديمة.

---

## 4. تواريخ ميلاد الوالدين وحسابات الميلاد

**Decision**: حسابات السن وأيام الميلاد تتم **client-side** فقط في الـ frontend (لا API calls).

```typescript
function computeAgeAndDaysToNextBirthday(dateOfBirth: string): {
  ageYears: number;
  daysToNextBirthday: number;
} {
  const today = new Date();
  const dob = new Date(dateOfBirth);
  // ... حساب السن
  // ... حساب الأيام المتبقية
}
```

**Rationale**: المنطق حسابي بحت — لا حاجة لـ server roundtrip. يُظهر للمستخدم النتيجة فوراً عند الإدخال.

---

## 5. التحقق عبر واتساب

**Decision**: زر "تحقق عبر واتساب" يفتح رابط `https://wa.me/${phoneNumber}?text=${encodedMessage}` في نافذة جديدة. لا Backend API مطلوب.

**Rationale**: 
- `wa.me` API من واتساب مجاني ولا يتطلب مصادقة
- التحقق هنا هو "تأكيد بشري" وليس OTP أوتوماتيكي
- إذا احتيج OTP مستقبلاً يمكن إضافته في Phase 2

---

## 6. رقم هاتف الأم — MotherPhone

**Decision**: `ParentPhone` يبقى كما هو ويُفسَّر رسمياً كـ "رقم الأب". يُضاف `MotherPhone` كحقل منفصل nullable في `StudentProfile`.

**لماذا**: إعادة تسمية `ParentPhone` إلى `FatherPhone` breaking change للـ API — نتجنبه. التوثيق والـ UI labels تُوضِّح الغرض.

---

## 7. AcademicValidationService

**Decision**: تُوسَّع `AcademicValidationService` لتشمل الـ stages والـ grades الجديدة. الـ validation matrix:

| EducationStage | Valid GradeLevels | Requires Track? |
|---|---|---|
| Secondary | FirstSecondary, SecondSecondary, SecondaryGrade3 | SecondSecondary فقط |
| Baccalaureate | FirstBaccalaureate, SecondBaccalaureate | SecondBaccalaureate فقط |
| Primary | PrimaryGrade1-6 | لا |
| Preparatory | PrepGrade1-3 | لا |
| Azhari | AzhariPrimary1-6, AzhariPrep1-3, AzhariSecondary1-3 | لا |
| American | AmericanGrade1-12 | لا |

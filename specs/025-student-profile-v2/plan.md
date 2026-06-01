# Implementation Plan: تحديث شامل لنموذج بيانات الطالب (الإصدار الثاني)

**Branch**: `025-student-profile-v2` | **Date**: 2026-03-30 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/025-student-profile-v2/spec.md`

---

## Summary

إضافة حقول جديدة لبيانات الطالب المسجَّل تشمل: **الجنسية** (22 دولة عربية)، **حالة المدرسة ونوعها**، **حسابات تلقائية** (السن وأيام عيد الميلاد)، **بيانات ميلاد الوالدين** وحسابات تلقائية خاصة بهم، **توسيع المراحل الدراسية** (أزهري، أمريكي، ابتدائي، إعدادي)، و**التحقق عبر واتساب** لرقم الطالب. التغييرات تمس Domain entities في الـ backend وتتطلب migration جديد، وتتطلب تحديث `RegistrationForm` في الـ frontend بالإضافة لتحديث `AcademicFields`.

---

## Technical Context

**Language/Version**: TypeScript 5 (Frontend) / C# 12 / .NET 8 (Backend)  
**Primary Dependencies**: Next.js 15, React 19, Framer Motion, Zod, FluentValidation, MediatR, Entity Framework Core 9  
**Storage**: PostgreSQL (code-first migrations via EF Core)  
**Testing**: xUnit (backend) / Playwright (E2E)  
**Target Platform**: Web — Desktop + Mobile (responsive)  
**Project Type**: Web application (Next.js frontend + .NET API backend)  
**Performance Goals**: < 500ms API p95, حসابات العمر/أيام الميلاد تتم client-side instantaneously  
**Constraints**: Backward compatible — الحقول الجديدة nullable للحسابات القديمة؛ لا breaking changes للـ RegisterCommand API  
**Scale/Scope**: ~1000+ طالب مسجَّل حالياً، يُتوقع تسجيل مئات جُدد

---

## Constitution Check

### Gate 1: Modular Clean Architecture ✅
- Domain layer: `StudentProfile` entity + enums موجودة في `NaderGorge.Domain`
- Application layer: `RegisterCommand` في `NaderGorge.Application.Features.Auth.Commands`
- Infrastructure layer: Migration جديد فقط — لا raw SQL
- Frontend: `RegistrationForm` → `AcademicFields` component hierarchy محفوظة

### Gate 2: Provider Abstraction ✅
- زر واتساب يفتح `wa.me` رابط — لا API خارجي مدمج مباشرةً

### Gate 3: Security & Access Control ✅
- الحقول الجديدة (الجنسية، المدرسة، تواريخ ميلاد الوالدين) تخضع لنفس FluentValidation في `RegisterCommandValidator`
- لا تغيير في نموذج المصادقة أو الصلاحيات

### Gate 4: Database Migrations ✅
- كل تغيير في schema يمر عبر EF Core migration — ممنوع تعديل schema مباشرة

### Gate 5: Admin UI Consistency ✅
- بيانات الطالب الجديدة تُعرض في `StudentProfileExtendedDto` المُحدَّث
- الشاشة الإدارية تستخدم `AdminDataTable` و`AdminStatCard` موجودة مسبقاً

### Gate 6: Registration Single-Flow ✅
- الإضافات تدخل ضمن الـ 4 مراحل الحالية — لا مرحلة خامسة
- المرحلة 01 (الهوية): +الجنسية، +حسابات الميلاد
- المرحلة 02 (ولي الأمر): +تواريخ ميلاد الوالدين، +حقول شرطية لأرقام هواتفهم
- المرحلة 03 (المسار الدراسي): +المدرسة ونوعها، +توسيع المراحل

---

## Project Structure

### Documentation (this feature)

```text
specs/025-student-profile-v2/
├── plan.md              ← هذا الملف
├── research.md          ← Phase 0
├── data-model.md        ← Phase 1
├── quickstart.md        ← Phase 1
├── contracts/
│   ├── register-api.md  ← Phase 1
│   └── admin-profile-api.md ← Phase 1
└── tasks.md             ← Phase 2 (/speckit.tasks)
```

### Source Code

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   └── StudentProfile.cs               ← تحديث: +Nationality, +SchoolName, +SchoolType,
│   │   │                                           +FatherDateOfBirth, +MotherDateOfBirth,
│   │   │                                           +MotherPhone (منفصل عن ParentPhone)
│   │   └── Enums/
│   │       ├── EducationStage.cs               ← توسيع: +Primary, +Preparatory, +Azhari, +American
│   │       ├── GradeLevel.cs                   ← توسيع: كل الصفوف الجديدة
│   │       ├── SchoolType.cs                   ← جديد
│   │       └── ArabNationality.cs              ← جديد
│   ├── NaderGorge.Application/
│   │   ├── Features/
│   │   │   └── Auth/
│   │   │       └── Commands/
│   │   │           └── RegisterCommand.cs      ← تحديث: +حقول جديدة
│   │   └── Services/
│   │       └── AcademicValidationService.cs    ← توسيع: stages و grades جديدة
│   └── NaderGorge.Infrastructure/
│       └── Migrations/
│           └── XXXXXXXX_StudentProfileV2.cs    ← migration جديد

frontend/
├── src/
│   ├── components/
│   │   ├── forms/
│   │   │   └── RegistrationForm.tsx             ← تحديث: حقول جديدة في step 0, 1, 2
│   │   └── registration/
│   │       └── AcademicFields.tsx              ← توسيع: مراحل وصفوف جديدة
│   ├── data/
│   │   ├── arab-nationalities.ts               ← جديد: قائمة 22 جنسية
│   │   └── school-types.ts                     ← جديد: أنواع المدارس
│   └── services/
│       └── auth-service.ts                     ← تحديث: RegisterRequest type
```

**Structure Decision**: Web Application (Option 2) — Backend API + Next.js Frontend موجودَان.

---

## Complexity Tracking

لا violations — جميع الإضافات ضمن الـ constitution.

| الحقل/القرار | التبرير |
|---|---|
| `MotherPhone` كحقل منفصل عن `ParentPhone` | `ParentPhone` الحالي يُقرأ كـ "رقم ولي الأمر" وتاريخياً خُزِّن فيه رقم الأب. الفصل الصريح (FatherPhone/MotherPhone) يُحسِّن الوضوح ولكنه breaking change. الحل: `ParentPhone` يبقى كـ "رقم الأب" وتُضاف `MotherPhone` كحقل جديد اختياري |
| صفوف ابتدائي (6) + إعدادي (3) + أزهري | التوسع يتطلب إعادة تصميم GradeLevel enum — نضيف قيم جديدة دون حذف القديمة (backward compatible) |

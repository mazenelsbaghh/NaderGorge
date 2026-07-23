# Implementation Plan: مركز التقارير المتقدمة

**Branch**: `160-employee-realtime-refresh` | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/163-advanced-reporting-center/spec.md`

## Summary

إنشاء محرك تقارير موحد للأدمن والمدرس يدعم كتالوج مجالات وحقول، ومجموعات فلاتر متداخلة `all/any`، ومؤشرات ورسم وجدول مصفح من لقطة استعلام واحدة، وحفظ تعريفات شخصية، وتصدير Excel/PDF من الخادم. يفرض الخادم صلاحية `reports.manage` للأدمن ونطاق المدرس المستمد من هوية المستخدم قبل بناء أي استعلام؛ ولا يقبل نطاقاً موثوقاً من العميل. تظهر جميع المجالات المطلوبة في الكتالوج، ويعاد `unavailable` مع سبب واضح عندما لا يوجد مصدر بيانات موثوق.

## Technical Context

**Language/Version**: C# 13 على .NET 9؛ TypeScript 5.9 strict على Next.js 16.2.7 وReact 19.2.4  
**Primary Dependencies**: ASP.NET Core Web API، MediatR، FluentValidation، EF Core 9/Npgsql، Next.js App Router، Axios، Zustand، Tailwind CSS؛ يضاف `ClosedXML` لإنشاء XLSX و`QuestPDF` لإنشاء PDF بخط عربي مضمن  
**Storage**: PostgreSQL 16 لتعريفات التقارير وطلبات التصدير ولقطات التصدير الوصفية؛ البيانات التشغيلية تبقى في جداولها الحالية؛ تخزين الملفات المؤقت الحالي أو مجلد مخصص قابل للاستبدال بمخزن أصول  
**Testing**: xUnit Application/Integration، PostgreSQL integration fixtures للاستعلامات والترجمة، Playwright E2E، ESLint/TypeScript، اختبارات عقود للتصدير وفك XLSX/PDF  
**Target Platform**: Linux/Docker Compose؛ واجهات ويب responsive للأدمن والمدرس  
**Project Type**: تطبيق ويب متعدد الطبقات (API + Next.js frontend + PostgreSQL)  
**Performance Goals**: p95 أقل من 5 ثوانٍ للتقرير التفاعلي في الحجم الاعتيادي؛ فتح الكتالوج أقل من 500ms؛ الصفحة الافتراضية 50 صفاً؛ التصدير حتى 50,000 صف كعملية غير متزامنة  
**Constraints**: توقيت `Africa/Cairo` للعرض والتجميع؛ حد أقصى لعمق مجموعات الفلاتر 3، 20 شرطاً، 100 قيمة مجمعة، 10 أعمدة فرز، pageSize أقصى 200؛ منع raw SQL/field names من العميل؛ عدم كشف أسرار المصادقة أو الدعم أو الأمان للمدرس  
**Scale/Scope**: 13 مجالاً ظاهراً للأدمن، 9 مجالات مسموحة للمدرس، قوالب جاهزة، صفحتان `/admin/reports` و`/teacher/reports`، CRUD لتعريفات شخصية، Excel/PDF، سجل تدقيق

## Constitution Check

### قبل التصميم

| البوابة | القرار | الحالة |
|---|---|---|
| Clean Architecture | العقود والـ validators والـ handlers في Application، التكوين والترجمة والاستعلام والتخزين في Infrastructure، والـ controllers رقيقة | PASS |
| Security by default | النطاق يستخرج من claims وTeacherProfile/TeacherStaffMember، وقائمة حقول allowlist، ومخرجات export يعاد تفويضها | PASS |
| Audit | حفظ/تعديل/نسخ/حذف تعريف وتصدير يسجل AuditLog بدون قيم حساسة | PASS |
| Data integrity | لا تُشتق أرقام غير مسجلة؛ `unavailable` بدلاً من بيانات مصطنعة؛ لقطة export immutable | PASS |
| Phased delivery | قصص P1 قابلة للتسليم المستقل، ثم إضافة كل المجالات P2 دون كسر العقد | PASS |
| Worker impact | لا تغيير في worker؛ التصدير ينفذ بخدمة خلفية .NET محدودة التوازي | PASS |

### أثر الطبقات

- **Backend API**: `ReportsController` موحد لمسارات الأدمن والمدرس، rate limit وسياسات الصلاحية.
- **Application**: DTOs/validators، كتالوج allowlist، scope resolver interface، CQRS للحفظ والتنفيذ والتصدير.
- **Domain**: `ReportDefinition` و`ReportExport` وحالاتها؛ لا تحفظ AST قابلاً للتنفيذ وإنما JSON مُصدّر بعد validation.
- **Infrastructure/Database**: EF mappings/migration/indexes، providers لكل مجال، export generator/storage/cleanup.
- **Frontend**: مركز مشترك يبنى منه سطحي الأدمن والمدرس، filter builder، summary/chart/table، saved reports، export status.
- **Worker**: لا أثر.
- **Docker**: إعادة بناء backend/admin/teacher؛ volume اختياري لملفات التصدير المؤقتة؛ health checks كما هي.

### بعد التصميم

التصميم لا يضيف مستودعاً عاماً أو لغة استعلام حرة، ويستخدم providers مسماة لكل مجال. صلاحيات الحقول والمجالات جزء من كتالوج الخادم وعقد الاختبار. لا توجد مخالفة دستورية تحتاج تبريراً.

## Architecture

```text
HTTP request
  -> authorization + actor scope resolver
  -> catalog/domain validator (allowlisted fields/operators/columns)
  -> normalized immutable ReportSnapshot
  -> domain query provider (IReportDomainProvider)
  -> summary + chart + paged rows
  -> optional persisted ReportDefinition / ReportExport
  -> server-side XLSX/PDF generator over the same snapshot
```

### قرارات أساسية

1. **Provider لكل مجال**: يمنع handler واحداً ضخماً ويتيح اختبار كل مجال واستبدال مصادره.
2. **Scope قبل الفلاتر**: يضيف `TeacherId`/content ownership كشرط غير قابل للحذف قبل تفسير شروط العميل.
3. **Typed field catalog**: لكل field نوع، operators، cardinality، الحساسية، الأدوار والأعمدة القابلة للفرز.
4. **لقطة normalized**: تحفظ domain/schemaVersion/filters/columns/sort/timezone/scopeFingerprint؛ لا يعاد استخدام JSON قديم مباشرة.
5. **Export ذو لقطة مجمدة**: طلب التصدير يعيد التفويض ثم يبث الصفوف المطابقة إلى spool خاص immutable عند قبول الطلب؛ يعالج worker هذا الـspool إلى XLSX/PDF بشكل غير متزامن. لا يعيد worker الاستعلام عن بيانات قد تكون تغيرت.

## Permission Matrix

| المجال | Admin + `reports.manage` | Teacher owner | Teacher staff + `reports` | بيانات المدرس |
|---|---:|---:|---:|---|
| students | كامل | نعم | نعم | طلاب لهم وصول/نشاط/شراء مرتبط بمحتوى المدرس فقط؛ بيانات الملف والاتصال مسموحة، أسرار الحساب والأجهزة ممنوعة |
| purchases_access | كامل | نعم | نعم | العمليات والمنح التي تخص محتوى المدرس فقط |
| codes | كامل | نعم | نعم | مجموعات وأكواد المدرس فقط؛ plaintext/hash ممنوع |
| balance_recharge | كامل | نعم | نعم | شحن/رصيد مخصص للمدرس أو شراء محتواه فقط؛ لا يعرض محفظة عامة غير مرتبطة |
| content | كامل | نعم | نعم | محتوى المدرس والمحتوى المشترك المخصص له |
| engagement | كامل | نعم | نعم | مشاهدة/تقدم/نشاط طلاب المدرس على محتواه فقط |
| assessments | كامل | نعم | نعم | امتحانات/واجبات المدرس وطلاب نطاقه |
| teachers_finance | كامل | نعم | بإذن مالي منفصل أو `reports.finance` | حساب وتخصيصات وأرباح المدرس الحالي فقط |
| comments_community | كامل | نعم | نعم | تعليقات/منشورات مرتبطة بمحتوى/مجتمع المدرس |
| parent_tracking | كامل | لا | لا | مجال تشغيلي للأدمن؛ بيانات اتصال ولي الأمر اللازمة للمدرس موجودة في students، ولا يعرض `ParentTrackingCode` الخام أو tokens |
| staff_operations | كامل | لا | لا | غير متاح للمدرس |
| support | كامل | لا | لا | غير متاح للمدرس مهما كانت صلة الطالب |
| security_audit | كامل | لا | لا | غير متاح للمدرس؛ audit الخاص بحفظ/تصدير تقرير لا يظهر له كتقرير أمان |

الأدمن الكامل يتجاوز فحص claim الحالي وفق `HasPermissionAttribute`، بينما Supervisor/الأدوار المفوضة تحتاج `reports.manage`. المدرس يثبت امتلاكه `TeacherProfile.UserId == actorId`، والاستاف يثبت عضوية فعالة وpermission key صريح. كل تعريف محفوظ يعاد تفويضه عند الفتح ولا يرث صلاحيات وقت الحفظ.

## Report Domains and Sources

| المجال | المصادر الرئيسية | ملاحظات التوافر |
|---|---|---|
| students | Users, StudentProfiles, StudentStatusTrackers, WarningEvents | متاح |
| purchases_access | StudentAccessGrants, BalanceTransactions, AccessCodeActivationLogs, gifts/sales effects | متاح؛ يميز المصدر والحالة |
| codes | CodeGroups, AccessCodes, activation/redemption tables | متاح؛ القيم السرية لا تخرج |
| balance_recharge | StudentBalances, BalanceTransactions, RechargeRequests, IncomingSmsLogs | متاح للأدمن؛ مقيد بشدة للمدرس |
| content | Subjects, TeacherProfiles, Packages, Terms, ContentSections, Lessons, LessonVideos | متاح |
| engagement | VideoWatchEvents, LessonProgresses, VideoPlaybackSessions, StudentStatusTrackers | متاح؛ جلسات التشغيل الخام لا تعرض كأسرار |
| assessments | Exams, StudentExamAttempts, StudentAnswers, Homeworks, HomeworkSubmissions | متاح؛ تحليل السؤال يتطلب صفوف الإجابة الموجودة |
| teachers_finance | TeacherAccounts, TeacherFinancialEvents/Allocations/Payouts | متاح |
| staff_operations | EmployeeProfiles, AttendanceLogs, TaskItems, CRM, Media, Payroll | متاح للأدمن |
| support | LiveSupportConversation/Assignment/Message/Event/Rating | متاح للأدمن فقط |
| comments_community | LessonComments, CommunityPosts/Comments/Likes | متاح |
| parent_tracking | StudentProfiles, ParentDeviceTokens, parent academic queries | جزئي؛ لا توجد حالياً telemetry موثوقة لكل فتح، فتظهر المقاييس غير المسجلة unavailable |
| security_audit | AuditLogs, Devices, RefreshTokens, WebVitalsMetric | متاح للأدمن؛ token values ممنوعة |

## Project Structure

```text
backend/src/NaderGorge.Domain/Entities/
├── ReportDefinition.cs
└── ReportExport.cs
backend/src/NaderGorge.Application/Features/Reporting/
├── Contracts/
├── Catalog/
├── Queries/
├── Commands/
├── Validation/
└── Services/
backend/src/NaderGorge.Infrastructure/Reporting/
├── Catalog/
├── Providers/
├── Exports/
└── ReportScopeResolver.cs
backend/src/NaderGorge.API/Controllers/ReportsController.cs
backend/src/NaderGorge.API/BackgroundServices/ReportExportWorker.cs
backend/tests/NaderGorge.Application.Tests/Reporting/
backend/tests/NaderGorge.Integration.Tests/Reporting/
frontend/src/components/reports/
frontend/src/services/advanced-report-service.ts
frontend/src/app/admin/reports/
frontend/src/app/teacher/reports/
frontend/tests/e2e/advanced-reports.spec.ts
specs/163-advanced-reporting-center/
```

**Structure Decision**: استخدام الوحدات القائمة مع module جديد `Reporting` بدلاً من توسيع استعلامات `Admin.Reports` القديمة. تبقى endpoints القديمة مؤقتاً للتوافق، ثم تعرض واجهة المركز الجديد النتائج الجديدة فقط.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter Reporting`
- `ConnectionStrings__DefaultConnection=... dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter Reporting`
- `cd frontend && npm run lint && npm run typecheck && npm run build`
- `make verify-e2e` بعد تشغيل بيئة E2E وفق `docs/verification-contract.md`.
- matrix الصلاحيات، nested filters، تعريف «لم يشترِ»، اتساق summary/chart/table، حفظ schema version، XLSX/PDF، expiry، Cairo DST/time boundaries موثقة في [quickstart.md](./quickstart.md).

**Docker Gate Required**:

1. `docker compose config -q`
2. `make up`
3. `make migrate` لأن الميزة تضيف/تكمل جداول report definitions/exports.
4. `docker compose ps` وكل الخدمات المطلوبة healthy.
5. فحص `/health` للـ backend وفتح `/admin/reports` و`/teacher/reports`.
6. إنشاء وتحمـيل XLSX/PDF داخل Docker والتحقق من انتهاء الملف بعد TTL.

**Manual QA Required**:

- Admin: بناء تقرير `(اشترى A أو B) AND لم يشاهد AND فترة`، مقارنة المؤشرات والرسم والجدول، الحفظ وإعادة الفتح والتصدير.
- Teacher A: تقرير طلابه ومبيعاته؛ محاولة إرسال teacherId الخاص بـTeacher B في body/query والتأكد من 403/نتيجة مقيدة بلا تسريب totals.
- Teacher: التأكد من غياب support/security/staff ومنع استدعاء endpoint مباشرة.
- Teacher staff: منح/سحب `reports` ثم التحقق فورياً؛ المال يحتاج `reports.finance`.
- تعطيل مصدر/بيانات مجال جزئي والتأكد من `unavailable` لا الصفر المصطنع.
- فحص RTL، الهاتف، keyboard navigation، table alternative للرسم، empty/error/loading states.

**End-of-Phase Report Format**: النطاق المنفذ، migrations، أوامر ونتائج الاختبار بالأعداد، نتائج Docker/health، QA لكل دور، قياسات p95 وحجم export، المخاطر المفتوحة، وقرار go/no-go. لا تبدأ المرحلة التالية قبل إصلاح البوابات الفاشلة أو توثيق موافقة المالك على المخاطرة.

## Risks

- الاستعلامات العابرة لعدة جداول قد تنتج Cartesian explosion؛ يلزم projection/aggregate providers واختبارات PostgreSQL query plan.
- تعريف «لم يشترِ» يحتاج cohort مؤهل محدد ومحتوى منشور ونقطة زمنية ثابتة؛ الخطأ فيه يعطي قوائم تسويق غير صحيحة.
- التقاط صفوف export عند الطلب يزيد زمن POST ومساحة التخزين المؤقت، لكنه ضروري لتحقيق التطابق لحظة الطلب؛ يلزم streaming وحدود 50k XLSX و5k PDF وتنظيف spool والملف النهائي.
- التصدير العربي PDF يحتاج خطاً مرخصاً مضمنًا واختبار shaping؛ fallback المتصفح غير مقبول لأنه قد يختلف عن صلاحية اللقطة.
- حفظ JSON دون schema migration يعطل التقارير القديمة؛ يلزم versioning/migrator ورفض آمن.
- ملفات export قد تحتوي PII؛ TTL قصير، أسماء عشوائية، authorization عند التنزيل، وعدم تقديم static public URL.

## Complexity Tracking

لا توجد مخالفة دستورية. خدمة التصدير الخلفية داخل API مبررة لأن الملفات الكبيرة لا ينبغي إنشاؤها في request thread، ولا تستدعي إضافة worker تقني رابع أو queue جديدة في هذه المرحلة.

# Implementation Plan: منظومة الموارد البشرية المتكاملة

**Branch**: `164-comprehensive-hr-platform` | **Date**: 2026-07-22 | **Spec**: `specs/164-comprehensive-hr-platform/spec.md`
**Input**: Feature specification from `specs/164-comprehensive-hr-platform/spec.md`

## Summary

توسيع نواة HR الحالية إلى منظومة أحادية الشركة تدير دورة حياة الموظف كاملة، مع إنشاء ذري للحساب والملف، هيكل وعقود مؤرخة، شفتات وحضور، إجازات وموافقات متعددة، محرك رواتب قابل للتكوين، خدمة ذاتية، مستندات وعهد وأداء وقضايا وتوظيف وتقارير. ينفذ العمل داخل Feature واحدة عبر موجات تشغيل مستقلة، ويظل لكل وحدة مصدر حقيقة واحد مع Dry-run وتسوية ورجوع موثق. يبدأ التنفيذ بإصلاح عيوب سلامة البيانات والصلاحيات الحالية، ثم يضيف الوحدات تدريجيًا دون خلط الموظفين بالمدرسين أو الطلاب.

## Technical Context

**Language/Version**: C# 13 على .NET 9؛ TypeScript 5.9 strict على Next.js 16.2.7 وReact 19.2.4  
**Primary Dependencies**: ASP.NET Core Web API، MediatR، FluentValidation، EF Core 9.0.6/Npgsql 9.0.4، SignalR 9 مع Redis backplane، Next.js App Router، Axios، Zustand، Tailwind CSS، Lucide React  
**Storage**: PostgreSQL 16 للبيانات الموثوقة؛ مخزن الملفات الحالي للمرفقات؛ Redis للتنسيق المؤقت وSignalR فقط وليس كمصدر HR  
**Testing**: xUnit واختبارات Application/Integration الحالية، Vitest/Node test وESLint/Next build، Playwright/E2E عبر `make verify-e2e`  
**Target Platform**: حاويات Linux Docker؛ واجهة ويب عربية RTL، إدارة مكتبية كثيفة البيانات وخدمة ذاتية متجاوبة  
**Project Type**: تطبيق ويب كامل Backend + Frontend؛ الـworker لا يحتاج تغييرًا وظيفيًا في هذه الميزة  
**Performance Goals**: القوائم المعتادة خلال 3 ثوانٍ، التقارير الشهرية خلال 5 ثوانٍ، منع الاستدعاءات N+1، pagination server-side بحد أقصى 100 صف للصفحة  
**Constraints**: شركة قانونية واحدة؛ القاهرة timezone؛ حساب واحد إلزامي لكل موظف؛ لا حذف مباشر للرواتب أو audit؛ منع self-approval؛ idempotency للعمليات الحساسة؛ كل موجة قابلة للتشغيل والرجوع منفردة  
**Scale/Scope**: 12 رحلة أعمال، 6 موجات بعد طبقة الأمان، نحو 30 مجموعة كيان وواجهات منفصلة للموظف والمدير وHR والمالية والمدير العام

## Constitution Check

### Gate قبل Phase 0

| Gate | Decision | Evidence |
|---|---|---|
| Clean Architecture | PASS | الكيانات والقواعد في Domain، الأوامر/الاستعلامات في Application، EF في Infrastructure، HTTP في API، والخدمات في frontend service layer. |
| Security by default | PASS | صلاحيات مستقلة ونطاق تنظيمي من الخادم، منع الاعتماد الذاتي، audit مترابط، وحماية ملفات ورواتب دون الاعتماد على إخفاء الواجهة. |
| Data safety | PASS | EF migrations فقط، معاملات ذرية، مفاتيح idempotency، قيود uniqueness، retention، وعدم cascade delete للسجلات التاريخية الحساسة. |
| Phased delivery | PASS | Wave 0 ثم 1-6؛ لا تبدأ موجة حتى تنجح بوابات السابقة أو يسجل المالك قبول خطر صريح. |
| Observability | PASS | correlation id، audit قبل/بعد، outbox للإشعارات، قياسات نجاح/رفض/تصعيد/ترحيل دون تسجيل بيانات رواتب أو وثائق حساسة. |
| Frontend conventions | PASS | App Router وAxios وZustand والنمط الحالي لـquery invalidation؛ PRODUCT.md وDESIGN.md وadmin tokens هي المرجع البصري. |
| Docker/QA closure | PASS | لكل موجة اختبارات آلية وDocker health وmanual QA حسب الدور وتقرير go/no-go. |

### Layer impact

- **Domain**: توسيع `EmployeeProfile` وإضافة كيانات تنظيم وعقد وشفت وموافقة ورواتب ودورة حياة مع transitions صريحة.
- **Application**: Feature slices داخل `Features/HR` و`Features/Admin/Finance` مع validators وauthorization scope وidempotency.
- **Infrastructure**: mappings وEF migrations وفهارس وقيود، خدمات clock/file/retention/migration، وoutbox.
- **API**: controllers versioned تحت `/api/admin/hr`, `/api/employee/hr`, `/api/manager/hr`, `/api/admin/finance` بصلاحية دقيقة لكل endpoint.
- **Frontend**: HR workbench للإدارة، manager inbox، employee self-service، payroll workspace، مع service/query contracts وrealtime invalidation.
- **Worker**: لا تغيير؛ التصعيد والتنبيهات تنفذ بخدمة background مستضافة في الـbackend/outbox ما لم يثبت قياس الحمل عكس ذلك.
- **Docker**: لا خدمة جديدة؛ schema migration ثم health checks للخدمات الحالية.

## Project Structure

### Documentation (this feature)

```text
specs/164-comprehensive-hr-platform/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── access-and-workflow.md
│   ├── employee-organization.md
│   ├── shifts-attendance-leave.md
│   ├── payroll-and-lifecycle.md
│   └── migration-rollout.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/src/
├── NaderGorge.Domain/
│   ├── Entities/                  # HR aggregates and immutable history
│   └── Enums/                     # explicit lifecycle states
├── NaderGorge.Application/
│   ├── Common/                    # current actor, authorization, idempotency
│   └── Features/
│       ├── HR/                    # employee/org/shift/attendance/leave/lifecycle
│       └── Admin/Finance/         # payroll review and payout authorization
├── NaderGorge.Infrastructure/
│   ├── Data/AppDbContext.cs
│   └── Migrations/
└── NaderGorge.API/Controllers/    # admin, manager and employee HR APIs

backend/tests/NaderGorge.Application.Tests/
├── HR/
├── Finance/
└── Authorization/

frontend/src/
├── app/admin/hr/                  # HR workbench and configuration
├── app/admin/finance/             # payroll workspace, separate from teacher finance
├── app/assistant/                 # employee self-service compatibility routes
├── app/employee/                  # canonical self-service routes
├── components/hr/
├── features/employee/
├── services/hr-service.ts
└── lib/                           # permissions, query contracts, realtime scopes
```

**Structure Decision**: نمدد المشاريع الحالية ولا ننشئ خدمة مستقلة أو قاعدة منفصلة. تبقى محاسبة المدرسين مستقلة عن Payroll الموظفين داخل نفس Finance surface مع controllers وصلاحيات منفصلة.

## Phase 0: Research Decisions

كل القرارات والبدائل موثقة في `research.md`. أهم النتائج: model صريح لصفة الموظف بدل `non-student`، معاملات ذرية لإنشاء الموظف، جداول effective-dated للتاريخ، approval instance عام مع steps/delegation/escalation، attendance sessions مستقلة عن day classification، payroll snapshot immutable، وmodule rollout registry يمنع dual write.

## Phase 1: Design & Contracts

- `data-model.md` يحدد aggregates والعلاقات والقيود والـstate machines وسياسة الحذف.
- `contracts/` يحدد endpoints والصلاحيات والأخطاء وidempotency وevent/invalidation contracts.
- `quickstart.md` يحدد bootstrap والاختبارات وDocker gate وmanual QA لكل موجة.
- التصميم البصري يحافظ على هوية Massar: navy/teal/gold وTajawal/Montserrat وRTL. اقتراح البحث العام لواجهة داكنة/App-Store غير ملائم وتم رفضه لصالح `PRODUCT.md` و`DESIGN.md` وtokens الحالية.

### Post-design Constitution Re-check

PASS. العقود لا تكشف domain entities، جميع الكتابات الحساسة تمر عبر commands، payroll/audit لا يحذفان، كل transition موثق، وكل wave لها اختبار ورجوع. لا توجد مخالفة دستورية تحتاج تبريرًا.

## Delivery Waves

### Wave 0 — Safety foundation

إصلاح leave-as-open-attendance، وقف تصنيف كل non-student كموظف، تمرير current actor للـaudit، فصل `hr.*` و`payroll.*` permissions، إضافة rollout registry/idempotency/audit correlation وcharacterization tests للنواة الحالية.

### Wave 1 — Organization, profile and contract

إنشاء ذري للحساب والملف، رقم وظيفي، الهيكل والتعيين والعقد والتاريخ المؤرخ، lifecycle status، استيراد تجريبي للملفات والهيكل، ثم cutover أو rollback مستقل.

### Wave 2 — Shifts and attendance

قوالب الشفت والفترات والتعيينات والتقويم، sessions/breaks/attempt evidence، السياسات الثلاث، العمل عن بعد، overnight attribution، التصحيحات، ثم ترحيل الحضور وcutover.

### Wave 3 — Leave and approvals

الأنواع والسياسات والأرصدة والحركات والحجز، approval engine، manager→HR، delegation dated، timed escalation، no-self-approval، migration/cutover للإجازات.

### Wave 4 — Payroll

components/rules effective-dated، دورة وsnapshots وline explanations، attendance inputs، advances/loans/expenses/commissions، finance review، GM final approval، payslips، reconciliation/cutover.

### Wave 5 — Self-service, documents and assets

ملف وجدول وطلبات ومفردات للموظف، مستندات versioned، عهد وتسليم، expiry alerts، responsive/accessibility hardening.

### Wave 6 — Performance, cases, recruitment and reports

الأهداف والتقييم والاعتراض، الإنذارات والتحقيقات والجزاءات، recruitment→hire، onboarding/probation/offboarding، تقارير scoped exports، retention/archive/anonymization، reconciliation النهائي.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj` لمسارات atomicity، scope، state transitions، payroll invariants، migration reconciliation.
- `cd frontend && npm run lint && npm run build` للعقود وstrict types والأسطح.
- `make verify` للانحدار الكامل، و`make verify-e2e` في بيئة E2E لرحلات الأدوار.
- اختبارات concurrency متوازية للحضور والموافقة وتوليد الراتب وcutover، واختبارات replay بنفس idempotency key.

**Docker Gate Required**:

1. `docker compose config -q`.
2. `make up` ثم `make ps` ومراجعة health للـAPI/frontend/PostgreSQL/Redis.
3. `make migrate` لكل wave فيها schema change على نسخة بيانات مماثلة للإنتاج.
4. dry-run migration، reconciliation بلا فروق غير معتمدة، cutover، smoke، rollback rehearsal، ثم cutover مرة ثانية.

**Manual QA Required**:

- HR: إنشاء موظف كامل، نقل وعقد، نشر شفت، مراجعة حضور وإجازة ووثيقة وعهد وقضية.
- الموظف: دخول، حضور/انصراف/استراحة، إجازة وتصحيح، جدول ورصيد ومستند ومفردات.
- المدير: يرى فريقه فقط ويعتمد أول مستوى ولا يعتمد طلبه.
- البديل: يعمل داخل نافذة التفويض فقط؛ والتصعيد يحدث بعد SLA.
- المالية: تراجع الراتب دون امتلاك اعتماد الصرف؛ المدير العام يعتمد النهائي فقط.
- اختبارات سلبية مباشرة للـAPI: HR بلا payroll.view، مدير خارج فريقه، موظف لملف غيره، مدرس/طالب غير معين كموظف.

**End-of-Phase Report Format**: نطاق منفذ، migrations، أوامر ونتائج الاختبارات، Docker/health، reconciliation قبل/بعد، manual QA حسب الدور، أخطار متبقية، قرار go/no-go. فشل أي gate يوقف الموجة التالية إلا بقبول خطر مكتوب من المالك.

## Complexity Tracking

لا توجد مخالفات دستورية. عدد الكيانات والموجات ناتج مباشرة عن النطاق المؤكد، بينما أعيد استخدام الهوية والصلاحيات والإشعارات والـoutbox والبنية الحالية بدل إضافة خدمات جديدة.

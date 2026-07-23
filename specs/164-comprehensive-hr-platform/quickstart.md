# Quickstart & Verification: منظومة HR

## Prerequisites

- Docker وDocker Compose، .NET 9 SDK، Node.js 20+.
- نسخة بيانات اختبار مجهولة الهوية تمثل جداول users/employee_profiles/attendance_logs/employee_vacations/payroll.
- لا تنشئ migration HR جديدة قبل دمج/تثبيت أي تعديل قائم على `AppDbContextModelSnapshot.cs`، خصوصًا migration العضوية الحالية في worktree.

## Baseline

```bash
git status --short
docker compose config -q
make verify
cd frontend && npm run lint && npm run build
```

سجل النتائج قبل أي تغيير. لا تنظف الـworktree ولا تعدل تغييرات المستخدم غير المتعلقة بالميزة.

## Local stack

```bash
make up
make ps
make migrate
```

تحقق من health للـAPI والـfrontend وPostgreSQL وRedis، ثم نفذ smoke login لحسابات HR وموظف ومدير ومالية ومدير عام. استخدام `make migrate` يكون فقط بعد إنشاء ومراجعة EF migration للموجة.

## Verification by wave

### Wave 0

- اختبار regression يثبت أن إجازة مستقبلية لا تنشئ open attendance ولا تمنع clock-in.
- Query الموظفين يعيد أصحاب EmployeeProfile فقط.
- direct API checks تفصل HR عن payroll وتثبت actor في audit.
- legacy baseline counts/hashes محفوظة.

### Wave 1

- فشل contract/shift step يترك صفر User وصفر EmployeeProfile.
- Student/Teacher لا يصبح موظفًا إلا explicit hire.
- النقل والعقد يحفظان التاريخ ويمنعان overlap/cycle/self-manager.
- dry-run وfinal وrollback للملفات/الهيكل متطابقة 100%.

### Wave 2

- unrestricted/geofence/trusted-device مع remote exception.
- overnight/split shift، breaks، duplicate/replay، concurrent clock-in، correction before/after.
- live-support eligibility عند الدخول والخروج لا تتراجع.
- PostgreSQL partial uniqueness واختبارات concurrency إلزامية.

### Wave 3

- reserve/release/debit للرصيد مرة واحدة.
- manager ثم HR، delegation داخل المدة، escalation بعد SLA، self-approval ممنوع.
- leave classification لا ينشئ AttendanceSession.

### Wave 4

- كل net قابل للتتبع إلى lines/sources/rules.
- rerun لا يكرر commission/installment/attendance deduction.
- finance review ثم GM approval؛ closed immutable؛ settlement بعد الإغلاق.
- teacher finance routes/results لا تتغير.

### Waves 5-6

- employee sees self only؛ document download authorization/access audit.
- asset blocks offboarding unless approved exception.
- case existence hidden without permission.
- candidate-to-employee atomic، reports scoped/export audited، retention dry-run ثم apply.

## Required commands before closing each wave

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj
cd frontend && npm run lint && npm run build
make verify
docker compose config -q
make ps
```

بعد تجهيز E2E backend طبقًا لـ`docs/verification-contract.md`:

```bash
make verify-e2e
```

أي فشل يوقف الانتقال للموجة التالية، إلا إذا سجل مالك المنتج قبول خطر واضح مع سبب ونطاق وزمن معالجة.

## Role-based manual QA

1. **HR**: ينشئ موظفًا كاملًا في أقل من 5 دقائق، ينقله، يعيّن عقدًا وشفتًا، ويراجع إجازة وتصحيحًا.
2. **Employee / موظف / دعم فني / technical support assistant**: يسجل الدخول، يرى ملفه فقط، يسجل حضورًا واستراحة وانصرافًا، يقدم إجازة وتصحيحًا، وينزل مفردته.
3. **Direct manager**: يرى direct team فقط ويعتمد المستوى الأول ولا يستطيع اعتماد طلبه.
4. **Delegate**: يستطيع القرار داخل start/end فقط، ويظهر كacting actor بجانب المسؤول الأصلي.
5. **HR reviewer**: يكمل المستوى الثاني دون صلاحية راتب إن لم تمنح له.
6. **Finance**: يراجع دورة الراتب ويردها ولا يعتمد الصرف النهائي.
7. **General manager**: يعتمد الصرف النهائي ولا يغير القواعد أو البنود.
8. **Teacher/Student negative**: لا يظهران في قوة العمل ولا يقرآن HR إلا إذا عُيّنا صراحة كموظفين.

## Migration rehearsal

لكل وحدة: Dry-run → resolve conflicts → verify count/total/hash → final migration → shadow read → activate sole writer → smoke/E2E → rollback rehearsal → verify other modules unchanged → activate again. احتفظ بتقرير قبل/بعد وbatch id وقرار go/no-go.

## UI acceptance

- العربية RTL وتناسق navy/teal/gold وTajawal/Montserrat وفق `PRODUCT.md` و`DESIGN.md`.
- Admin desktop-first وجداول paged؛ self-service responsive؛ touch targets 44px.
- loading/empty/error/permission states واضحة؛ focus visible وWCAG AA وreduced motion.
- salary/document/case data لا تظهر لحظة واحدة أثناء hydration أو loading للمستخدم غير المخول.

## End-of-wave evidence

أنشئ تقريرًا يحتوي: scope، files/migration، automated commands/results، Docker health، migration reconciliation، manual role matrix، known risks، rollback result، وقرار go/no-go.

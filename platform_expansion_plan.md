# خطة توسعة منصة مسار - Phased Execution Plan

هذه الوثيقة تحول مخطط التوسعة الكبير إلى مراحل تنفيذ واختبار واضحة مبنية على
هيكل المشروع الحالي في المستودع. الهدف أن كل Phase تخرج بوظيفة قابلة للتجربة،
اختبارات آلية محددة، وتشغيل Docker في النهاية قبل الانتقال للمرحلة التالية.

## طريقة التنفيذ الإلزامية

- لا تبدأ Phase جديدة قبل إغلاق Phase السابقة بتقرير واضح.
- كل Phase MUST تضيف أو تحدث الاختبارات المرتبطة بها في نفس التغيير.
- أي تغيير في قاعدة البيانات MUST يتم عبر EF Core migrations داخل
  `backend/src/NaderGorge.Infrastructure/Migrations/`، وليس SQL يدوي مباشر.
- أي API جديد MUST يمر عبر MediatR في `backend/src/NaderGorge.Application`.
- أي استدعاء API في الواجهة MUST يمر عبر `frontend/src/services/`.
- أي Job أو تكامل طويل MUST يمر عبر `worker/src/jobs/` أو `worker/src/services/`.
- كل Phase MUST تنتهي بتشغيل Docker أو على الأقل Docker config/health gate إذا
  كانت مفاتيح البيئة الخارجية غير متاحة محليا.
- في نهاية كل Phase لازم يتكتب:
  - ما الذي تم تنفيذه.
  - ما الذي تم اختباره آليا.
  - ما الذي يجب اختباره يدويا.
  - أوامر Docker التي اشتغلت ونتيجتها.
  - أي مخاطر أو TODOs قبل المرحلة التالية.

## الهيكل الحالي الذي يجب البناء عليه

```text
backend/
├── NaderGorge.sln
├── src/
│   ├── NaderGorge.API/              # Controllers, Middleware, health, config
│   ├── NaderGorge.Application/      # MediatR Features, DTOs, validators
│   ├── NaderGorge.Domain/           # Entities, Enums, Interfaces
│   └── NaderGorge.Infrastructure/   # EF DbContext, migrations, services
└── tests/
    └── NaderGorge.Application.Tests/

frontend/
├── src/app/                         # Next.js App Router routes
│   ├── admin/
│   ├── assistant/
│   ├── student/
│   ├── teacher/
│   └── api/
├── src/components/
├── src/services/
├── src/stores/
└── tests/e2e/

worker/
├── src/index.ts
├── src/jobs/
├── src/services/
├── src/scripts/
└── src/utils/

tests/                               # Python smoke/inventory/API tests
scripts/                             # endpoint inventory and Docker surface checks
docker-compose.yml                   # full local stack
docker/docker-compose.yml            # infra-only/telegram-related stack
Makefile                             # build, test, migrate, Docker workflow
```

## أوامر التحقق المشتركة

استخدم هذه الأوامر كقائمة أساس. كل Phase تختار منها ما يناسبها، لكن Docker gate
في نهاية كل Phase إلزامي.

```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-build

cd frontend && npm run lint && npm run build
cd frontend && npm run test:e2e -- --project=chromium

cd worker && npm run build

python3 -m pip install -r tests/requirements.txt
python3 -m pytest -q

node scripts/generate-endpoint-inventory.mjs --check
node scripts/verify-surface-separation.mjs --static-only
docker compose config -q
```

### Docker gate في آخر كل Phase

```bash
# أول مرة فقط إذا كانت volumes غير موجودة
make docker-volumes

docker compose config -q
make up
make migrate
make ps

curl -f http://localhost:5245/api/health
curl -f http://localhost:3001/health
curl -f http://localhost:8738
curl -f http://localhost:8739
curl -f http://localhost:8740
node scripts/verify-surface-separation.mjs
```

إذا كانت `.env` ناقصة، لا تعتبر المرحلة مقفولة. المطلوب على الأقل:

- `JWT_SECRET`
- `API_CALLBACK_SECRET`
- `AI_CALLBACK_SECRET`
- `PARENT_REPORT_SIGNING_SECRET`
- `WORKER_ADMIN_TOKEN`
- `GEMINI_API_KEY` عند اختبار وظائف AI

## قالب تقرير نهاية كل Phase

```text
Phase:
Branch/spec:

Implemented:
- ...

Automated tests run:
- command: result

Docker gate:
- docker compose config -q: pass/fail
- make up: pass/fail
- make migrate: pass/fail
- health URLs: pass/fail

Manual QA required:
- ...

Risks / follow-ups:
- ...
```

---

## Phase 0 - Baseline, Specs, and Safety Inventory

### الهدف

تثبيت خط البداية قبل أي توسعة: معرفة الموجود، منع تكرار كيانات موجودة، وتجهيز
Specs/Tasks لكل Phase لاحقة.

### النطاق

- مراجعة الكيانات الموجودة في:
  - `backend/src/NaderGorge.Domain/Entities/`
  - `backend/src/NaderGorge.Domain/Enums/`
  - `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- مراجعة Features الحالية في:
  - `backend/src/NaderGorge.Application/Features/Admin`
  - `backend/src/NaderGorge.Application/Features/Assistant`
  - `backend/src/NaderGorge.Application/Features/Student`
  - `backend/src/NaderGorge.Application/Features/Content`
  - `backend/src/NaderGorge.Application/Features/Internal`
- مراجعة واجهات التشغيل الحالية:
  - `frontend/src/app/admin`
  - `frontend/src/app/assistant`
  - `frontend/src/app/student`
  - `frontend/src/app/teacher`
- إنشاء أو تحديث Spec منفصل لكل Phase كبيرة بدلا من تنفيذ الخطة كلها مرة واحدة.
- تحديد Feature flags أو route guards لأي شاشة جديدة قبل فتحها للمستخدمين.

### الاختبارات الآلية

- `dotnet build backend/NaderGorge.sln`
- `dotnet test backend/NaderGorge.sln --no-build`
- `cd frontend && npm run lint`
- `cd worker && npm run build`
- `python3 -m pytest tests/test_endpoint_inventory.py -q`
- `node scripts/generate-endpoint-inventory.mjs --check`
- `node scripts/verify-surface-separation.mjs --static-only`
- `docker compose config -q`

### Docker gate

شغل Docker stack للتأكد أن خط البداية سليم قبل أي توسعة:

- `make docker-volumes` إذا كانت volumes غير موجودة.
- `make up`
- `make migrate`
- `make ps`
- Health checks للـ backend, worker, landing, student, admin.

### Manual QA المطلوب منك

- فتح `http://localhost:8738`, `http://localhost:8739`, `http://localhost:8740`.
- تسجيل دخول Admin وتجربة لوحة المستخدمين والمحتوى.
- تسجيل دخول Student وتجربة dashboard ودرس وفيديو إن وجد test data.
- فتح Bull Board من `http://localhost:3001/ui`.
- التأكد أن `.env` لا يستخدم secrets افتراضية في بيئة إنتاج.

### مخرجات المرحلة

- قائمة بما هو موجود فعلا وما هو ناقص لكل module.
- Specs منفصلة أو Tasks واضحة قبل التنفيذ.
- تقرير baseline tests محفوظ في نهاية المرحلة.

---

## Phase 1 - Access Model, Staff Surfaces, and Permission Boundaries

### الهدف

تجهيز صلاحيات التوسعة قبل بناء HR/CRM/Finance، لأن كل الأنظمة اللاحقة تعتمد على
فصل واضح بين Super Admin, Supervisor, Staff, Teacher, Assistant, Student.

### النطاق

- تحديث نموذج الأدوار والصلاحيات بدون كسر `RoleType` الحالي.
- دعم صلاحيات granular داخل admin settings إذا احتاجت الأدوار الجديدة ذلك.
- توحيد route guards لكل سطح:
  - `frontend/src/app/admin`
  - `frontend/src/app/assistant`
  - `frontend/src/app/teacher`
  - staff views داخل admin أو مسار staff حسب قرار Surface separation.
- Backend authorization policies في:
  - `backend/src/NaderGorge.API/Configuration/`
  - `backend/src/NaderGorge.API/Controllers/`
  - `backend/src/NaderGorge.Application/Features/Admin`
- منع أي موظف من رؤية بيانات خارج نطاق صلاحياته.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Enums/RoleType.cs`
- `backend/src/NaderGorge.Domain/Entities/Role.cs`
- `backend/src/NaderGorge.Domain/Entities/UserRole.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Commands/*Role*`
- `backend/src/NaderGorge.Application/Features/Admin/Queries/ListRolesQuery.cs`
- `frontend/src/app/admin/settings/`
- `frontend/src/services/admin-service.ts`
- `frontend/tests/e2e/admin-users.spec.ts`

### الاختبارات الآلية

- Backend unit tests:
  - admin can create/update roles.
  - lower role cannot grant higher permission.
  - disabled user cannot access protected endpoints.
- E2E:
  - Admin sees role settings.
  - Teacher cannot open admin-only pages.
  - Assistant sees assistant dashboard only.
- Commands:
  - `dotnet test backend/NaderGorge.sln --no-build`
  - `cd frontend && npm run lint && npm run build`
  - `cd frontend && npm run test:e2e -- admin-users.spec.ts`
  - `python3 -m pytest tests/test_endpoint_inventory.py -q`

### Docker gate

- `docker compose config -q`
- `make up`
- `make migrate`
- `curl -f http://localhost:5245/api/health`
- `node scripts/verify-surface-separation.mjs`

### Manual QA المطلوب منك

- تجربة Login لكل دور: Admin, Teacher, Assistant, Student.
- فتح صفحات Admin بدور غير Admin والتأكد من منع الوصول.
- إنشاء دور جديد محدود وتجربة أنه يرى المسموح فقط.
- تعطيل مستخدم والتأكد أن الجلسة لا تستمر بصلاحيات قديمة.

### لا تبدأ Phase 2 إلا إذا

- صلاحيات الأدوار الجديدة واضحة.
- route guards تمنع الوصول الخاطئ.
- Docker stack يعمل بعد migrations.

---

## Phase 2 - HR Core: Employees, Attendance, Vacations

### الهدف

بناء ملف الموظف وحضور/انصراف وإجازات كطبقة تشغيلية أولى قبل المهام والرواتب.

### النطاق

- Employee profile مرتبط بـ `User`.
- بيانات الوظيفة والمرتب الأساسي وساعات العمل.
- Clock-in / clock-out مع device/IP/location metadata حسب الحاجة.
- حالات حضور: Present, Late, Absent, Sick, Leave.
- طلبات إجازة واعتماد/رفض من Supervisor أو Admin.
- Audit log لكل تعديل حساس.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Entities/EmployeeProfile.cs`
- `backend/src/NaderGorge.Domain/Entities/AttendanceLog.cs`
- `backend/src/NaderGorge.Domain/Entities/EmployeeVacation.cs`
- `backend/src/NaderGorge.Application/Features/Admin/HR/`
- `backend/src/NaderGorge.API/Controllers/AdminHrController.cs`
- `backend/src/NaderGorge.API/Controllers/HrController.cs`
- `frontend/src/app/admin/users/`
- `frontend/src/app/admin/hr/`
- `frontend/src/components/admin/`
- `frontend/src/services/admin-service.ts`
- `backend/tests/NaderGorge.Application.Tests/*Hr*Tests.cs`
- `frontend/tests/e2e/admin-hr.spec.ts`

### الاختبارات الآلية

- Unit:
  - إنشاء EmployeeProfile لا يكرر نفس User.
  - clock-out بدون clock-in يرجع خطأ واضح.
  - late minutes تتحسب بشكل صحيح.
  - الإجازة لا تتعتمد بدون صلاحية.
- Integration/API:
  - `POST /api/hr/attendance/clock-in`
  - `POST /api/hr/attendance/clock-out`
  - `POST /api/admin/hr/vacations/{id}/approve`
- E2E:
  - Admin يضيف موظف.
  - موظف يسجل حضور وانصراف.
  - Admin يراجع سجل حضور.
- Commands:
  - `dotnet build backend/NaderGorge.sln`
  - `dotnet test backend/NaderGorge.sln --no-build`
  - `cd frontend && npm run lint && npm run build`
  - `cd frontend && npm run test:e2e -- admin-hr.spec.ts`

### Docker gate

- `make up`
- `make migrate`
- `make ps`
- Health checks.
- افتح Admin surface وتأكد أن صفحة HR تعمل من Docker وليس dev server فقط.

### Manual QA المطلوب منك

- إنشاء موظف جديد وربطه بمستخدم.
- تجربة clock-in ثم clock-out.
- تجربة clock-out مرة ثانية والتأكد من ظهور خطأ.
- إنشاء طلب إجازة واعتماده ورفض طلب آخر.
- التأكد أن Student لا يستطيع فتح أي HR endpoint.

### لا تبدأ Phase 3 إلا إذا

- الجداول الجديدة migrated بنجاح.
- HR flow يعمل من Docker.
- الاختبارات تغطي happy path ورفض الصلاحيات.

---

## Phase 3 - Operations Task Manager and Approval Pipeline

### الهدف

بناء إدارة المهام اليومية وسلسلة الاعتمادات التشغيلية التي سيستخدمها فريق
المنصة والكول سنتر والإنتاج.

### النطاق

- Task items بحالات: New, InProgress, Review, Completed, Paused, Overdue.
- Priority: Low, Medium, High, Critical.
- Assign/reassign لموظف.
- Comments ومرفقات وروابط.
- Supervisor/Admin approval عند الإغلاق.
- Permission/approval requests للعمليات الحساسة.
- إشعار عند الإسناد أو التأخير أو طلب المراجعة.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Entities/Assistant/AssistantTaskQueue.cs`
- `backend/src/NaderGorge.Domain/Entities/TaskItem.cs`
- `backend/src/NaderGorge.Domain/Entities/TaskComment.cs`
- `backend/src/NaderGorge.Application/Features/Assistant/`
- `backend/src/NaderGorge.Application/Features/Admin/Operations/`
- `backend/src/NaderGorge.API/Controllers/AssistantController.cs`
- `backend/src/NaderGorge.API/Controllers/AdminOperationsController.cs`
- `frontend/src/app/assistant/dashboard/`
- `frontend/src/app/admin/operations/`
- `frontend/src/components/assistant/`
- `frontend/src/services/assistant-service.ts`
- `frontend/tests/e2e/assistant-dashboard.spec.ts`

### الاختبارات الآلية

- Unit:
  - لا يمكن إغلاق task بدون assignee صحيح.
  - لا يمكن تقليل status من Completed إلى InProgress بدون صلاحية.
  - overdue يتحسب من due date.
  - comment يحفظ user وtimestamp.
- E2E:
  - Admin ينشئ task.
  - Assistant يغير status ويضيف comment.
  - Supervisor يعتمد الإغلاق.
- Python/API:
  - اختبار endpoint inventory لعدم فقد endpoints قديمة.
- Commands:
  - `dotnet test backend/NaderGorge.sln --no-build`
  - `cd frontend && npm run test:e2e -- assistant-dashboard.spec.ts`
  - `python3 -m pytest tests/test_endpoint_inventory.py -q`

### Docker gate

- `make up`
- `make migrate`
- `curl -f http://localhost:5245/api/health`
- `curl -f http://localhost:8738`
- `curl -f http://localhost:8740`

### Manual QA المطلوب منك

- إنشاء مهمة high priority لموظف.
- تجربة إعادة إسنادها.
- إضافة تعليق ومرفق.
- محاولة إغلاقها من مستخدم غير مصرح.
- اعتمادها من Supervisor/Admin.
- مراجعة ظهور الإشعار أو pending notification.

### لا تبدأ Phase 4 إلا إذا

- المهام قابلة للاستخدام اليومي.
- approval rules واضحة ومغطاة باختبارات.

---

## Phase 4 - Multi-Teacher Multi-Subject Architecture and Teacher Isolation

### الهدف

تحويل المنصة من مدرس واحد إلى منصة متعددة المدرسين، حيث كل مدرس منعزل تماماً
عن باقي المدرسين: أكواده، مواده، طلابه، محتواه، وامتحاناته منفصلة بالكامل.

### النطاق

#### كيانات جديدة
- إنشاء كيان `Subject` (المادة الدراسية): تاريخ، رياضيات، فيزياء، كيمياء، إلخ.
- إنشاء كيان `TeacherProfile` مرتبط بـ `User` يحتوي: bio, specialization,
  commission rate, profile image URL, contact info.
- إنشاء جدول ربط `TeacherSubject` (Many-to-Many: مدرس ممكن يدرّس أكتر من مادة).

#### تعديلات على كيانات المحتوى الحالية
- إضافة `SubjectId` على `Program` لربط البرنامج بمادة معينة.
- إضافة `TeacherId` (FK → User) على `Package` لربط كل باقة بمدرسها.
  - يعني نفس المادة ونفس الصف ممكن يكون فيه باقات من مدرسين مختلفين.
  - باقي السلسلة (Term → Section → Lesson → Video) يرثوا المدرس من الـ Package.

#### تعديلات على كيانات الأكواد — عزل كامل
- إضافة `TeacherId` على `CodeGroup` بحيث كل مجموعة أكواد خاصة بمدرس محدد.
- عند تفعيل كود، الطالب يحصل على صلاحية الباقة المرتبطة بذلك المدرس فقط.
- كل مدرس يرى ويدير أكواده فقط — لا يرى أكواد مدرس آخر.

#### تعديلات على كيانات الامتحانات والأسئلة
- إضافة `CreatedByTeacherId` (FK → User) على `Exam`.
- إضافة `SubjectId` + `CreatedByTeacherId` على `QuestionBankItem` لتصنيف الأسئلة
  حسب المادة والمدرس.
- إضافة `GradedByTeacherId` على `EssaySubmission` لتسجيل مين المدرس اللي قيّم.

#### عزل بيانات المدرس — القاعدة الأساسية
- **المدرس لا يرى** طلاب مدرس آخر.
- **المدرس لا يرى** باقات أو محتوى مدرس آخر.
- **المدرس لا يرى** أكواد أو إحصائيات مدرس آخر.
- **المدرس لا يرى** امتحانات أو بنك أسئلة مدرس آخر.
- **Admin فقط** يرى كل البيانات عبر جميع المدرسين مع فلاتر.

#### تعديلات على تجربة الطالب
- الطالب يرى الباقات مصنفة حسب المدرس والمادة.
- عند تفعيل كود، الباقة تظهر مع اسم وصورة المدرس.
- Dashboard الطالب يعرض المدرسين المشترك معهم.

#### Teacher Surface/Dashboard (حالياً فاضي تماماً)
- بناء `frontend/src/app/teacher/` من الصفر.
- عرض: طلاب المدرس، باقاته، أكواده، إحصائيات المشاهدة، امتحاناته.
- المدرس يقدر يراجع essay submissions الخاصة بطلابه فقط.

#### تعديلات Admin Dashboard
- إضافة فلاتر بالمدرس والمادة على كل صفحات المحتوى.
- صفحة جديدة لإدارة المدرسين: إنشاء/تعديل/تعطيل.
- صفحة جديدة لإدارة المواد: إنشاء/تعديل/ربط بالمدرسين.

#### تعديلات Landing Page
- عرض المدرسين ديناميكياً من API بدلاً من البيانات الثابتة في `data.ts`.
- تحديث FAQ لحذف إشارة "المنصة بتغطي محتوى التاريخ فقط".

#### Data Migration للبيانات الحالية
- إنشاء مدرس default وربط كل الباقات والأكواد الحالية بيه.
- إنشاء مادة default وربط كل البرامج الحالية بيها.
- الـ FKs الجديدة تبدأ nullable مع data migration ثم تتحول لـ required.

### ملفات متوقعة

#### كيانات جديدة
- `backend/src/NaderGorge.Domain/Entities/TeacherProfile.cs`
- `backend/src/NaderGorge.Domain/Entities/Subject.cs`
- `backend/src/NaderGorge.Domain/Entities/TeacherSubject.cs`

#### كيانات معدلة
- `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs` — SubjectId على
  Program, TeacherId على Package.
- `backend/src/NaderGorge.Domain/Entities/CodeEntities.cs` — TeacherId على CodeGroup.
- `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs` — CreatedByTeacherId على
  Exam و QuestionBankItem، SubjectId على QuestionBankItem.
- `backend/src/NaderGorge.Domain/Entities/EssaySubmission.cs` — GradedByTeacherId.
- `backend/src/NaderGorge.Domain/Entities/User.cs` — navigation لـ TeacherProfile.
- `backend/src/NaderGorge.Domain/Interfaces/IAppDbContext.cs` — DbSets جديدة.
- `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` — DbSets + relationships.

#### Application layer
- `backend/src/NaderGorge.Application/Features/Admin/Commands/ManageSubjectCommand.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Commands/ManageTeacherProfileCommand.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Queries/GetTeachersQuery.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Queries/GetSubjectsQuery.cs`
- `backend/src/NaderGorge.Application/Features/Teacher/Queries/` — Teacher dashboard.
- تعديل جميع Content queries لإضافة فلترة بالمدرس.
- تعديل Code commands لربط الأكواد بالمدرس.
- تعديل Exam queries لفلترة بالمدرس.

#### API
- `backend/src/NaderGorge.API/Controllers/TeacherController.cs`
- تعديل `AdminController.cs` لإضافة endpoints المدرسين والمواد.
- تعديل Content/Codes controllers لإضافة فلترة.

#### Frontend
- `frontend/src/app/teacher/` — Teacher dashboard كامل من الصفر.
- `frontend/src/app/admin/teachers/` — إدارة المدرسين.
- `frontend/src/app/admin/subjects/` — إدارة المواد.
- `frontend/src/services/teacher-service.ts`
- تعديل `frontend/src/components/landing/data.ts` لجلب المدرسين من API.
- تعديل Student packages/lessons/dashboard لعرض اسم المدرس.
- تعديل Admin content pages لفلترة حسب المدرس/المادة.
- تعديل `frontend/src/app/faq/page.tsx` لحذف إشارة "التاريخ فقط".

#### اختبارات
- `backend/tests/NaderGorge.Application.Tests/MultiTeacher/TeacherProfileTests.cs`
- `backend/tests/NaderGorge.Application.Tests/MultiTeacher/SubjectTests.cs`
- `backend/tests/NaderGorge.Application.Tests/MultiTeacher/TeacherIsolationTests.cs`
- `frontend/tests/e2e/teacher-dashboard.spec.ts`

### الاختبارات الآلية

- Unit:
  - إنشاء TeacherProfile مرتبط بـ User ذو دور Teacher فقط.
  - عدم السماح بإنشاء TeacherProfile لـ Student.
  - إنشاء Subject وربطه بمدرس عبر TeacherSubject.
  - إنشاء Package مرتبط بمدرس ومادة عبر Program.
  - Teacher A لا يرى packages/codes/students/exams خاصة بـ Teacher B (عزل كامل).
  - CodeGroup مرتبط بمدرس يظهر فقط في بيانات ذلك المدرس.
  - الطالب يرى packages مصنفة حسب المدرس والمادة.
  - Data migration: الباقات والأكواد الحالية تربط بمدرس default بنجاح.
- E2E:
  - Admin ينشئ مادة ومدرس ويربطهم.
  - Admin ينشئ package لمدرس معين.
  - Teacher يفتح dashboard ويرى طلابه فقط.
  - Teacher لا يرى أكواد أو باقات مدرس آخر.
  - Student يرى المدرسين والمواد في الواجهة.
- Commands:
  - `dotnet build backend/NaderGorge.sln`
  - `dotnet test backend/NaderGorge.sln --no-build`
  - `cd frontend && npm run lint && npm run build`
  - `cd frontend && npm run test:e2e -- teacher-dashboard.spec.ts`

### Docker gate

- `make up`
- `make migrate`
- `curl -f http://localhost:5245/api/health`
- `curl -f http://localhost:8738`
- `curl -f http://localhost:8739`
- `curl -f http://localhost:8740`

### Manual QA المطلوب منك

- إنشاء مدرسين اتنين بمواد مختلفة من Admin.
- إنشاء باقة لكل مدرس مع ربطها بمادة وصف.
- إنشاء أكواد لكل مدرس والتأكد أن كل مدرس يرى أكواده فقط.
- تسجيل دخول كل مدرس والتأكد من عزل البيانات الكامل.
- تفعيل كود من حساب طالب والتأكد أن الباقة ظاهرة مع اسم المدرس.
- فتح Landing page والتأكد أن المدرسين يظهروا ديناميكياً.
- فتح Admin والتأكد أن فلاتر المدرس والمادة تعمل على المحتوى.
- إنشاء امتحان من مدرس والتأكد أن المدرس التاني لا يراه.
- التأكد أن Student لا يرى Teacher dashboard.

### لا تبدأ Phase 5 إلا إذا

- عزل المدرسين مثبت باختبارات unit وE2E.
- كل الباقات والأكواد والامتحانات مرتبطة بمدرس.
- Teacher dashboard يعمل ويعرض البيانات المعزولة.
- Data migration للبيانات الحالية تم بنجاح.
- Landing page تعرض المدرسين من API.

---

## Phase 5 - Internal Chat, Workrooms, and Real-Time Notifications

### الهدف

توفير تواصل داخلي منظم بين الإدارة والموظفين وفريق الإنتاج مع إشعارات لحظية.

### النطاق

- Chat rooms: فردي، group، workroom مرتبط بمهمة.
- Participants بصلاحيات واضحة.
- Messages: text, image/file/audio metadata.
- Read receipts.
- Mentions `@user`.
- Pin important messages.
- Archive rooms.
- SignalR/WebSocket channel منفصل أو backend hub داخل API حسب أبسط تكامل آمن.
- Notifications تربط المهام والشات والاعتمادات.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Entities/ChatRoom.cs`
- `backend/src/NaderGorge.Domain/Entities/ChatMessage.cs`
- `backend/src/NaderGorge.Domain/Entities/ChatMessageReadState.cs`
- `backend/src/NaderGorge.Domain/Entities/Notifications/NotificationEvent.cs`
- `backend/src/NaderGorge.Application/Features/Internal/`
- `backend/src/NaderGorge.API/Controllers/InternalChatController.cs`
- `backend/src/NaderGorge.API/Hubs/ChatHub.cs`
- `frontend/src/app/admin/chat/`
- `frontend/src/app/assistant/chat/`
- `frontend/src/components/admin/`
- `frontend/src/services/notification-service.ts`
- `worker/src/jobs/notification-sender.ts`

### الاختبارات الآلية

- Unit/API:
  - user خارج room لا يقرأ الرسائل.
  - read receipt لا يتكرر لنفس user/message.
  - archived room لا يقبل رسائل جديدة إلا بصلاحية.
  - mention ينشئ notification للعضو المقصود.
- Worker:
  - `cd worker && npm run build`
  - اختبار notification job بدون gateway خارجي باستخدام stub/mocked payload.
- E2E:
  - فتح chat كـ Admin وAssistant والتحقق من room/message/read state.

### Docker gate

- `make up`
- `make migrate`
- `curl -f http://localhost:5245/api/health`
- `curl -f http://localhost:3001/health`
- `make logs-backend` عند فشل realtime فقط.

### Manual QA المطلوب منك

- فتح مستخدمين في متصفحين مختلفين.
- إرسال رسالة والتأكد من وصولها بدون refresh.
- تجربة mention.
- تثبيت رسالة.
- أرشفة room والتأكد من توقف الكتابة.
- التأكد أن Student لا يرى chat داخلي.

### لا تبدأ Phase 6 إلا إذا

- الرسائل والصلاحيات مستقرة.
- notifications لا تسبب spam أو duplicate.

---

## Phase 6 - Call Center CRM and Student Follow-Up

### الهدف

بناء CRM للمتابعة اليومية للطلاب وأولياء الأمور مع قوائم توزع على Agents
وتقارير لمدير الكول سنتر.

### النطاق

- CRM student status لكل طالب.
- Assigned agent.
- Call logs بنتائج: Completed, Pending, NoAnswer, Postponed, Closed.
- Next follow-up date.
- Priority.
- WhatsApp action/link أو تكامل Evolution API عند توفر مفاتيح.
- تقارير agent performance.
- منع Agent من رؤية قوائم غير مسندة إليه إلا بصلاحية.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Entities/CrmStudentStatus.cs`
- `backend/src/NaderGorge.Domain/Entities/CrmCallLog.cs`
- `backend/src/NaderGorge.Application/Features/Admin/CRM/`
- `backend/src/NaderGorge.Application/Features/Assistant/CRM/`
- `backend/src/NaderGorge.API/Controllers/CrmController.cs`
- `frontend/src/app/admin/crm/`
- `frontend/src/app/assistant/crm/`
- `frontend/src/services/crm-service.ts`
- `frontend/tests/e2e/crm.spec.ts`
- `tests/test_crm.py`

### الاختبارات الآلية

- Unit:
  - agent يرى assigned students فقط.
  - next follow-up لا يقبل تاريخ ماضي إلا لو status يسمح.
  - call outcome يحدث LastContactedAt.
- Python/API:
  - إنشاء follow-up.
  - تحديث outcome.
  - منع cross-agent access.
- E2E:
  - Agent يفتح قائمة اليوم.
  - يسجل مكالمة.
  - يؤجل متابعة.

### Docker gate

- `make up`
- `make migrate`
- `python3 -m pytest tests/test_crm.py -q`
- Health checks.

### Manual QA المطلوب منك

- تعيين طلاب لموظف كول سنتر.
- تسجيل نتيجة مكالمة.
- إرسال/فتح WhatsApp action.
- التأكد أن Agent B لا يرى بيانات Agent A.
- مراجعة تقرير مدير الكول سنتر.

### لا تبدأ Phase 7 إلا إذا

- CRM data isolation مثبت باختبارات.
- follow-up workflow مفهوم للموظف.

---

## Phase 7 - SMS Auto-Payment and Wallet Matching (مؤجلة)

### الهدف

أتمتة شحن رصيد الطالب من رسائل SMS القادمة من تطبيق Android خارجي مع حماية
التكرار والاحتيال.

### النطاق

- Student payment request بحالة Pending/Completed/Expired.
- SMS parsing templates قابلة للتعديل من Super Admin.
- Webhook محمي بـ `X-SMS-APP-KEY` أو secret equivalent.
- Matching خلال نافذة 30 دقيقة.
- منع duplicate transaction reference.
- Unmatched queue للمراجعة اليدوية.
- Balance transaction عند النجاح.
- WhatsApp/student notification عند الشحن.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Entities/StudentPaymentRequest.cs`
- `backend/src/NaderGorge.Domain/Entities/InterceptedSmsLog.cs`
- `backend/src/NaderGorge.Domain/Entities/SmsParsingTemplate.cs`
- `backend/src/NaderGorge.Application/Features/Payments/`
- `backend/src/NaderGorge.API/Controllers/PaymentsController.cs`
- `frontend/src/app/student/balance/`
- `frontend/src/app/admin/payments/`
- `frontend/src/services/balance-service.ts`
- `frontend/src/services/payment-service.ts`
- `tests/test_sms_payment_automation.py`

### الاختبارات الآلية

- Unit:
  - regex يستخرج amount/sender/reference.
  - webhook يرفض secret خطأ.
  - payment request منتهي لا يشحن.
  - reference مكرر لا يشحن مرتين.
  - unmatched SMS يذهب للمراجعة.
- Python/API:
  - الطالب ينشئ payment request.
  - webhook يستقبل SMS مطابق.
  - balance يزيد بالقيمة.
  - webhook مكرر يرجع status آمن بدون شحن إضافي.
- E2E:
  - Admin يضيف parsing template.
  - Student يرى balance updated.

### Docker gate

- `make up`
- `make migrate`
- `python3 -m pytest tests/test_sms_payment_automation.py -q`
- `curl -f http://localhost:5245/api/health`
- راجع backend logs لأي 500 أو duplicate processing.

### Manual QA المطلوب منك

- إنشاء طلب شحن من حساب طالب.
- إرسال SMS webhook يدوي بـ `curl` لقيمة مطابقة.
- إرسال SMS بقيمة غير مطابقة والتأكد أنه في manual review.
- تكرار نفس reference والتأكد أن الرصيد لا يزيد مرتين.
- تجربة regex template جديد من Admin.

### لا تبدأ Phase 8 إلا إذا

- الشحن التلقائي آمن ضد التكرار.
- العمليات غير المطابقة لا تضيع وتظهر للمراجعة.

---

## Phase 8 - Media Production Pipeline and Social Planner

### الهدف

تنظيم خط إنتاج الحصص والسوشيال من التحضير إلى النشر مع KPIs لفريق الإنتاج.

### النطاق

- Media production pipeline بمراحل:
  Preparation, Filming, Editing, Uploading, Review, Approved, Published.
- Assigned editor/producer.
- Asset folder URL أو upload references.
- Editing error count.
- PublishedAt.
- Social media plan للأفكار والسيناريوهات وجدولة النشر.
- ربط content approval بنظام الموافقات عند الحاجة.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Entities/MediaProductionPipeline.cs`
- `backend/src/NaderGorge.Domain/Entities/SocialMediaPlan.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Media/`
- `backend/src/NaderGorge.API/Controllers/AdminMediaController.cs`
- `frontend/src/app/admin/content/`
- `frontend/src/app/admin/media/`
- `frontend/src/components/admin/content/`
- `frontend/src/services/content-service.ts`
- `frontend/src/services/media-service.ts`
- `frontend/tests/e2e/admin-content.spec.ts`

### الاختبارات الآلية

- Unit:
  - stage transitions لا تتخطى Review إلى Published بدون approval.
  - PublishedAt يضاف عند النشر فقط.
  - KPI delay يحسب من scheduled date.
- E2E:
  - Admin ينشئ pipeline item.
  - Editor يغير stage.
  - Admin يوافق وينشر.
  - Social plan ينتقل من scripting إلى scheduled.
- Commands:
  - `dotnet test backend/NaderGorge.sln --no-build`
  - `cd frontend && npm run test:e2e -- admin-content.spec.ts`

### Docker gate

- `make up`
- `make migrate`
- `curl -f http://localhost:8740`
- `node scripts/verify-surface-separation.mjs`

### Manual QA المطلوب منك

- إنشاء حصة تصوير بتاريخ قادم.
- تحريكها بين المراحل بالترتيب.
- محاولة نشرها قبل approval والتأكد من المنع.
- إضافة social media plan ومراجعة الجدولة.
- مراجعة KPI التأخير أو الالتزام.

### لا تبدأ Phase 9 إلا إذا

- خط إنتاج المحتوى واضح ومغلق باعتماد.
- لا توجد طريقة لنشر محتوى بدون Review/Approval.

---

## Phase 9 - Payroll, Teacher Finance, and Activated Code Accounting

### الهدف

إغلاق الدائرة المالية الداخلية: رواتب الموظفين، أرباح المدرسين، الأكواد المفعلة،
والمدفوعات المعتمدة.

### النطاق

- يبني على كيانات `TeacherProfile` و `Subject` و `Package.TeacherId` و
  `CodeGroup.TeacherId` المنشأة في Phase 4.
- Payroll records شهرية للموظفين.
- Additions/deductions مع أسباب واضحة.
- اعتماد نهائي قبل الصرف.
- Teacher accounts: total earnings/current balance/commission rate.
- Teacher payouts بحالات Pending/Paid/Rejected.
- Activated code accounting مرتبط بـ AccessCode/CodeGroup الحالي.
- منع المدرس من رؤية بيانات مالية لمدرس آخر (عزل البيانات الأساسي تم في Phase 4).
- فصل EGP عن gamification points نهائيا.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Entities/PayrollRecord.cs`
- `backend/src/NaderGorge.Domain/Entities/TeacherAccount.cs`
- `backend/src/NaderGorge.Domain/Entities/TeacherPayout.cs`
- `backend/src/NaderGorge.Domain/Entities/ActivatedCodeLog.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Finance/`
- `backend/src/NaderGorge.Application/Features/Teacher/Finance/`
- `backend/src/NaderGorge.API/Controllers/AdminFinanceController.cs`
- `backend/src/NaderGorge.API/Controllers/TeacherFinanceController.cs`
- `frontend/src/app/admin/finance/`
- `frontend/src/app/teacher/finance/`
- `frontend/src/services/finance-service.ts`
- `tests/test_teacher_finance.py`

### الاختبارات الآلية

- Unit:
  - payroll total = base + additions - deductions.
  - payroll لا يصرف بدون approval.
  - teacher commission يحسب من code activation.
  - teacher cannot query another teacher summary.
- Python/API:
  - Teacher A isolation returns 403 for Teacher B.
  - Admin creates payout.
  - Redeeming code creates activated code log.
- E2E:
  - Admin يراجع payroll ويعتمده.
  - Teacher يفتح finance summary.

### Docker gate

- `make up`
- `make migrate`
- `python3 -m pytest tests/test_teacher_finance.py -q`
- Health checks.

### Manual QA المطلوب منك

- إنشاء payroll لشهر محدد.
- إضافة خصم ومكافأة.
- محاولة صرف payroll بدون اعتماد.
- اعتماد payroll ومراجعة total.
- تفعيل كود خاص بمدرس ومراجعة ظهوره في teacher finance.
- تسجيل payout والتأكد من تغير الرصيد.

### لا تبدأ Phase 10 إلا إذا

- الماليات لا تسمح برصيد سالب أو صرف غير معتمد.
- خصوصية المدرسين مثبتة باختبارات.

---

## Phase 10 - Audit Trail, KPI Dashboards, and Operational Reports

### الهدف

تجميع الرقابة والتقارير بعد اكتمال العمليات الأساسية، مع ضمان أن كل عملية حساسة
تترك أثر Audit قابل للمراجعة.

### النطاق

- توسيع AuditLog الحالي ليغطي HR, Tasks, CRM, Payments, Media, Payroll,
  Teacher Finance.
- Dashboard KPIs:
  - attendance/late/absence.
  - task completion and overdue.
  - call center outcomes.
  - media production delays.
  - payment matching rate.
  - payroll approval status.
- Filters بالتاريخ والدور والموظف والمدرس.
- Export عند الحاجة لاحقا، لكن لا يسبق dashboard الأساسي.

### ملفات متوقعة

- `backend/src/NaderGorge.Domain/Entities/AuditLog.cs`
- `backend/src/NaderGorge.Infrastructure/Repositories/AuditRepository.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Reports/`
- `backend/src/NaderGorge.Application/Features/Reports/`
- `backend/src/NaderGorge.API/Controllers/AdminReportsController.cs`
- `frontend/src/app/admin/reports/`
- `frontend/src/components/admin/`
- `frontend/src/services/report-service.ts`
- `tests/test_audit_and_reports.py`

### الاختبارات الآلية

- Unit:
  - state-changing command writes AuditLog.
  - old/new values لا تحتوي secrets.
  - report counts match seeded data.
- Python/API:
  - إنشاء عملية حساسة ثم قراءة audit entry.
  - فلترة KPIs بتاريخ.
- E2E:
  - Admin يفتح reports dashboard.
  - يفلتر حسب الموظف والتاريخ.

### Docker gate

- `make up`
- `make migrate`
- `python3 -m pytest tests/test_audit_and_reports.py -q`
- `node scripts/generate-endpoint-inventory.mjs --check`

### Manual QA المطلوب منك

- تعديل موظف ومراجعة AuditLog.
- اعتماد payroll ومراجعة AuditLog.
- تسجيل call log ومراجعة KPI.
- تجربة filter لا يرجع بيانات خارج النطاق.
- التأكد من عدم ظهور secrets في audit values.

### لا تبدأ Phase 11 إلا إذا

- كل العمليات الحساسة تترك audit.
- التقارير لا تعتمد على أرقام ثابتة أو حسابات واجهة فقط.

---

## Phase 11 - Surface Separation, Subdomains, Assets, and Docker Hardening

### الهدف

تحويل التقسيم التشغيلي إلى surfaces/subdomains واضحة بدون كسر التشغيل الحالي.
هذه المرحلة تأتي بعد اكتمال الوظائف الأساسية حتى لا يتم تعقيد التطوير مبكرا.

### النطاق

- الحفاظ على Docker services الحالية:
  - `landing` على 8738.
  - `student` على 8739.
  - `admin` على 8740.
  - `backend` على 5245.
  - `worker` على 3001.
- تحديد هل سيتم فصل `super`, `staff`, `teacher` كـ Docker services مستقلة أو
  كـ route/surface داخل frontend image الحالي.
- Nginx reverse proxy للـ subdomains:
  - `massaracademy.com`
  - `www.massaracademy.com`
  - `staff.massaracademy.com`
  - `super.massaracademy.com`
  - `teacher.massaracademy.com`
  - `api.massaracademy.com`
  - `ws.massaracademy.com`
  - `assets.massaracademy.com`
- Cookie/session domain: `.massaracademy.com`.
- CORS allowed origins لكل subdomain.
- Assets route أو static hosting لملفات PDF/images/uploads.
- WebSocket route إذا تم فصل SignalR على `ws`.
- Health checks لكل service.

### ملفات متوقعة

- `docker-compose.yml`
- `docker-compose.override.yml`
- `docker/docker-compose.yml`
- `frontend/Dockerfile`
- `backend/Dockerfile`
- `worker/Dockerfile`
- `scripts/verify-surface-separation.mjs`
- `frontend/src/packages/surface-runtime/`
- `frontend/src/middleware.ts` فقط إذا تقرر احتياجه وبمراجعة خاصة.
- Nginx config جديد داخل `docker/` أو deployment repo حسب بيئة الإنتاج.

### الاختبارات الآلية

- Static:
  - `docker compose config -q`
  - `node scripts/verify-surface-separation.mjs --static-only`
- Runtime:
  - `node scripts/verify-surface-separation.mjs`
  - health لكل ports.
  - Host-header smoke tests للـ subdomain routing لو Nginx محلي.
- Frontend:
  - Playwright smoke لكل surface: landing, student, admin/staff/teacher.
- Backend:
  - CORS rejects unknown origin.
  - auth cookie/token behavior لا يتسرب بين domains غير مسموحة.

### Docker gate

هذه المرحلة لا تقفل بدون Docker كامل:

- `make down`
- `docker compose build --no-cache`
- `make up`
- `make migrate`
- `make ps`
- `node scripts/verify-surface-separation.mjs`
- Health checks لكل surface.
- فحص logs:
  - `make logs-backend`
  - `make logs-worker`
  - `make logs-frontend`

### Manual QA المطلوب منك

- تجربة كل domain أو localhost surface حسب المتاح.
- تسجيل دخول Admin ثم فتح staff/teacher والتأكد من السلوك الصحيح.
- تجربة Student flow على student surface.
- تجربة رفع/فتح asset.
- تجربة notification/chat لو ws منفصل.
- التأكد أن direct API من origin غير مسموح مرفوض.

### لا تبدأ Phase 12 إلا إذا

- Docker يعمل من cold start.
- CORS/domain/session rules واضحة.
- لا يوجد service يعتمد على dev-only URL.

---

## Phase 12 - Full Regression, Launch Drill, and Rollback Plan

### الهدف

تجميع كل المراحل في اختبار إطلاق كامل قبل اعتبار التوسعة جاهزة.

### النطاق

- تشغيل كل الاختبارات.
- clean database migration drill.
- seed data drill.
- backup/restore drill.
- rollback plan لكل migration أو feature flag.
- مراجعة endpoint inventory.
- مراجعة manual QA لكل دور.
- مراجعة security checklist.

### الاختبارات الآلية

```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-build

cd frontend && npm run lint && npm run build
cd frontend && npm run test:e2e -- --project=chromium

cd worker && npm run build

python3 -m pip install -r tests/requirements.txt
python3 -m pytest -q

node scripts/generate-endpoint-inventory.mjs --check
node scripts/verify-surface-separation.mjs --static-only
docker compose config -q
```

### Docker launch drill

```bash
make down
docker compose build --no-cache
make up
make migrate
make ps

curl -f http://localhost:5245/api/health
curl -f http://localhost:3001/health
curl -f http://localhost:8738
curl -f http://localhost:8739
curl -f http://localhost:8740
node scripts/verify-surface-separation.mjs
```

### Manual QA النهائي المطلوب منك

- Admin:
  - إدارة المستخدمين والأدوار.
  - HR attendance/vacations.
  - operations tasks approvals.
  - CRM assignment.
  - payment review.
  - payroll approval.
  - reports/audit.
- Staff/Assistant:
  - استلام task.
  - تحديث task.
  - CRM call log.
  - chat/notification.
- Teacher:
  - فتح finance summary.
  - مراجعة activated codes/payouts.
  - التأكد من isolation.
- Student:
  - تسجيل الدخول.
  - فتح dashboard/lesson/video.
  - إنشاء payment request.
  - رؤية balance بعد SMS test.
  - community/comment flows إذا موجودة.
- Production:
  - مراجعة logs.
  - مراجعة backup.
  - تجربة restart containers.
  - التأكد من عدم وجود secrets افتراضية.

### تقرير الإغلاق النهائي

لا تعتبر التوسعة جاهزة إلا بتقرير يحتوي:

- كل commands التي تم تشغيلها ونتيجتها.
- كل manual flows التي تمت وتجربتها.
- أي flows لم تختبر بسبب missing external dependency.
- قائمة dependencies الخارجية:
  - Evolution API.
  - Gemini API.
  - SMS Android app.
  - Nginx/SSL.
  - Telegram local API إذا مستخدم.
- rollback steps.
- قرار واضح: Ready / Not Ready.

---

## ترتيب التنفيذ المختصر

1. Phase 0 - Baseline and specs.
2. Phase 1 - Roles and permission boundaries.
3. Phase 2 - HR core.
4. Phase 3 - Operations tasks and approvals.
5. Phase 4 - Multi-teacher multi-subject architecture and teacher isolation.
6. Phase 5 - Internal chat and notifications.
7. Phase 6 - CRM.
8. Phase 7 - SMS auto-payment. (مؤجلة)
9. Phase 8 - Media production and social planner.
10. Phase 9 - Payroll and teacher finance.
11. Phase 10 - Audit and reports.
12. Phase 11 - Subdomains/Docker hardening.
13. Phase 12 - Full regression and launch drill.

## قاعدة أخيرة

لو Phase فشلت في اختبار Docker أو manual QA، لا يتم تعديل المرحلة التالية لإخفاء
الفشل. يتم فتح bug/fix داخل نفس Phase، إعادة تشغيل الاختبارات، ثم إعادة Docker
gate من البداية.

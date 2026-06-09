# تقرير الناقص الحالي من توسعة منصة مسار

تاريخ المراجعة: 2026-06-09  
الملفات التي تمت مراجعتها:

- `platform_expansion_plan.md`
- `platform_expansion_action_plan_2026-06-09.md`
- صفحات `frontend/src/app`
- Guards وsurface runtime
- `docker-compose.yml`
- `docker/nginx/massar.conf`
- `scripts/verify-surface-separation.mjs`

## القرار المختصر

الحالة: **Not Ready**.

فيه شغل مهم اتعمل بعد التقرير السابق: صفحات المساعد اتوسعت، `assistant/layout.tsx` موجود، صفحات teacher الجديدة موجودة، `AdminGuard` اتضيق، Teacher bypass اتشال من `HasPermission`, وخدمات `teacher` و`assistant` اتضافت في Docker/Nginx.

المتبقي الأساسي الآن:

- عزل Docker images فعليا لكل surface، لأن كل frontend services ما زالت تستخدم نفس image: `massar_frontend:local`.
- تنظيف الدومينات القديمة من Docker/Nginx/CORS بعد اعتماد `massar-academy.net`.
- إثبات أن كل domain لا يفتح surface الثاني، ليس فقط redirect من landing.
- إكمال بعض صفحات Teacher/Student، خصوصا activity وstudent profile/notifications.
- تثبيت اختبارات E2E وDocker cold-start launch drill.

## المستبعد

Phase 7 - SMS Auto-Payment and Wallet Matching مؤجلة، ولا تدخل في هذا التقرير.

## ما اتعمل فعلا ولا يجب تكراره

### الأدوار والـ Guards

- `AdminGuard` أصبح يسمح لـ `Admin` و`Supervisor` فقط.
- `useHasPermission` أصبح يجعل `Admin` فقط bypass عام.
- `HasPermissionAttribute` في backend أصبح يجعل `Admin` فقط bypass عام.
- `StudentGuard` أصبح يتحقق من `Student` أو `Admin` preview.
- `AssistantGuard` موجود ويقبل `Assistant`, `Staff`, `Admin`, `Supervisor`.
- `TeacherGuard` موجود ويقبل `Teacher` أو `Admin` preview.

### صفحات المساعد

موجود حاليا:

- `/assistant`
- `/assistant/dashboard`
- `/assistant/tasks`
- `/assistant/tasks/[id]`
- `/assistant/crm`
- `/assistant/chat`
- `/assistant/attendance`
- `/assistant/vacations`
- `/assistant/notifications`
- `AssistantShellChrome` وNavbar خاصة بالمساعد.

### صفحات المدرس

موجود حاليا:

- `/teacher`
- `/teacher/packages`
- `/teacher/packages/...`
- `/teacher/codes`
- `/teacher/codes/[groupId]`
- `/teacher/exams`
- `/teacher/essays`
- `/teacher/finance`
- `/teacher/chat`
- `/teacher/students`
- `/teacher/profile`

### الدومينات وDocker services

موجود حاليا:

- `landing`, `student`, `admin`, `teacher`, `assistant` services.
- ports محلية:
  - landing: `8738`
  - student: `8739`
  - admin: `8740`
  - teacher: `8741`
  - assistant: `8742`
- Nginx يوجه:
  - `massar-academy.net`
  - `app.massar-academy.net`
  - `admin.massar-academy.net`
  - `super.massar-academy.net`
  - `teacher.massar-academy.net`
  - `staff.massar-academy.net`
  - `api.massar-academy.net`
  - `ws.massar-academy.net`
  - `assets.massar-academy.net`
- `scripts/verify-surface-separation.mjs` أصبح يعرف services `teacher` و`assistant`.

## النواقص الحالية

### 1. Docker images ليست منفصلة فعليا

كل frontend services تستخدم نفس الصورة:

- `landing`: `massar_frontend:local`
- `student`: `massar_frontend:local`
- `admin`: `massar_frontend:local`
- `teacher`: `massar_frontend:local`
- `assistant`: `massar_frontend:local`

هذا يحقق service/port/env separation، لكنه لا يحقق شرط "كل واحدة لها صورة Docker بتاعتها مالهاش علاقة بالتانية" حرفيا.

المطلوب واحد من خيارين:

- الأفضل: صور منفصلة:
  - `massar_landing_frontend:local`
  - `massar_student_frontend:local`
  - `massar_admin_frontend:local`
  - `massar_teacher_frontend:local`
  - `massar_assistant_frontend:local`
- أو توثيق قرار runtime isolation صراحة، مع اختبار يثبت أن `APP_SURFACE` يمنع تشغيل routes الخطأ داخل نفس image.

### 2. الدومينات القديمة ما زالت موجودة في config

رغم أن الدومين الجديد هو `massar-academy.net`، ما زالت هذه الدومينات موجودة في Nginx/CORS:

- `massarplatform.com`
- `bsma-academy.com`
- subdomains الخاصة بهم.

المطلوب:

- حذف الدومينات القديمة من `docker/nginx/massar.conf` إذا لم تعد مستخدمة.
- حذفها من `Cors__AllowedOrigins` في `docker-compose.yml`.
- تحديث `.env.example` وأي deployment templates للدومين الجديد فقط.
- توثيق أي دومين قديم سيبقى كـ legacy redirect فقط، وليس كـ surface تشغيل كامل.

### 3. اختبار عزل الدومينات ما زال غير كاف

`verify-surface-separation.mjs` يفحص subdomain routing وredirects من landing، لكنه لا يثبت بشكل كامل أن:

- student domain لا يعرض `/admin`.
- student domain لا يعرض `/teacher`.
- teacher domain لا يعرض `/admin`.
- staff domain لا يعرض Admin navbar أو routes.
- admin domain لا يعرض teacher/staff shell بالخطأ.

المطلوب:

- إضافة forbidden-route checks لكل domain.
- التأكد من status المتوقع: redirect إلى domain الصحيح أو 404/403 حسب القرار.
- فحص HTML marker أو header مثل `X-App-Surface` للتأكد أن الصفحة المعروضة هي surface الصحيح.

### 4. صفحات Teacher ما زال ناقصها activity/watch stats

موجود `teacher/students`, `teacher/profile`, و`teacher/essays`، لكن لا توجد صفحة:

- `/teacher/activity`
- watch stats أو learner activity dashboard واضح.

المطلوب:

- صفحة activity تعرض:
  - الطلاب النشطين.
  - آخر مشاهدة.
  - تقدم الطالب داخل باقات المدرس.
  - فيديوهات/دروس أكثر مشاهدة.
  - إنذارات الطالب المتأخر أو غير النشط.

### 5. صفحات Student ناقصة profile/notifications

موجود صفحات التعلم الأساسية، لكن لا توجد حسب الجرد الحالي:

- `/student/profile`
- `/student/notifications`

المطلوب:

- صفحة profile للطالب.
- صفحة notifications أو مكان واضح لها داخل dashboard.
- التأكد أن student domain لا يفتح أي surface داخلي.

### 6. Assistant permissions تحتاج إثبات أدق

`AssistantShellChrome` عنده Navbar خاص، لكن لازم الاختبارات تثبت:

- Assistant بدون `crm.manage` لا يرى CRM tab.
- Academic assistant يرى المهام الأكاديمية فقط.
- CRM agent يرى CRM والمهام الخاصة به فقط.
- Operations staff يرى tasks/chat/attendance فقط.
- Supervisor يرى approval/review بدون إدارة كاملة.

المطلوب:

- Permission matrix واضحة في tests.
- E2E لكل نوع مساعد أو على الأقل smoke لكل role variant.

### 7. Teacher binding يحتاج إثبات نهائي بالاختبارات

الكود الحالي يحتوي مؤشرات ربط المدرس في services والـ DTOs، لكن الإغلاق لا يتم إلا باختبارات تغطي:

- Admin ينشئ package بمدرس ومادة.
- Teacher ينشئ package بدون تمرير `teacherId` ويستخدم current teacher.
- Admin يولد code مرتبط بهدف له مدرس.
- Teacher يولد code لموارده فقط.
- Admin/Teacher ينشئ question/exam مع subject/teacher صحيح.
- Activated code accounting يربط الإيراد بالمدرس.
- Teacher A لا يرى موارد Teacher B.

### 8. Backend assistant endpoints تحتاج role attributes صريحة

`AssistantController` يحسن ownership في details/status/comments، لكن `my/*` endpoints لا تزال بلا `[Authorize(...)]` صريح على كل action.

المطلوب:

- إضافة `[Authorize(Roles = "Admin,Supervisor,Assistant,Staff,AssistantAcademic,AssistantReviewer")]` أو policy واضحة على:
  - `GET /api/v1/assistant/tasks/my`
  - `GET /api/v1/assistant/tasks/my/{id}`
  - `POST /api/v1/assistant/tasks/my/{id}/status`
  - `POST /api/v1/assistant/tasks/my/{id}/comments`

### 9. Launch drill غير مكتمل

لا يوجد في الملفات دليل إغلاق نهائي يحتوي نتائج:

- build/lint/unit tests كاملة.
- Playwright لكل دور.
- Docker cold-start.
- health checks لكل services.
- backup/restore.
- rollback plan.
- manual QA لكل role/domain.

## أقل عدد Phases للتنفيذ

### Phase 1 - Domain and Docker Isolation Finalization

الهدف: كل surface له domain/service/port/image أو عزل موثق، ولا يفتح سطح غيره.

المهام:

- تحويل frontend images إلى صور منفصلة، أو توثيق runtime isolation كقرار رسمي.
- تنظيف `docker/nginx/massar.conf` من الدومينات القديمة أو تحويلها إلى legacy redirects فقط.
- تنظيف `Cors__AllowedOrigins` من الدومينات القديمة غير المستخدمة.
- تحديث `.env.example` وdeployment envs للدومين `massar-academy.net`.
- إضافة forbidden-route checks في `scripts/verify-surface-separation.mjs`.
- إضافة HTML/header marker لكل surface إن لم يكن موجودا.

مخرجات القبول:

- `docker compose config -q` ينجح.
- كل service له port مختلف.
- كل domain يوجه surface الصحيح فقط.
- direct wrong-domain routes تفشل أو تتحول للدومين الصحيح.

### Phase 2 - Role Pages and Permissions Completion

الهدف: كل دور يرى صفحاته فقط، والصفحات الناقصة تكتمل.

المهام:

- إضافة `/teacher/activity`.
- إضافة `/student/profile`.
- إضافة `/student/notifications` أو دمج موثق داخل dashboard.
- إضافة role attributes الصريحة على `AssistantController` `my/*`.
- تثبيت Assistant permission matrix في الواجهة والاختبارات.
- تثبيت Teacher binding tests لكل content/code/question/exam/finance flow.
- مراجعة أي service داخل teacher surface يستدعي `/admin/*` والتأكد أنه محمي بصلاحية مناسبة أو استبداله endpoint مخصص للمدرس.

مخرجات القبول:

- Assistant لا يرى Admin navbar أو صفحات Admin.
- Staff/Assistant permissions تعمل على tabs والأزرار والـ APIs.
- Teacher يرى بياناته فقط.
- Student يرى teacher identity في الباقات والأكواد والدروس حيث ينطبق.

### Phase 3 - Full Regression and Launch Evidence

الهدف: إثبات أن التوسعة مقفولة فعلا.

الأوامر المطلوبة:

```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-build

cd frontend && npm run lint && npm run build
cd frontend && npm run test:e2e -- --project=chromium

cd worker && npm run build

python3 -m pytest tests -q
node scripts/generate-endpoint-inventory.mjs --check
node scripts/verify-surface-separation.mjs --static-only
docker compose config -q
```

Docker cold-start:

```bash
make down
docker compose build --no-cache
make up
make migrate
make ps
node scripts/verify-surface-separation.mjs
```

Manual QA:

- Admin على `admin.massar-academy.net`.
- Supervisor على `super.massar-academy.net` أو داخل admin بصلاحيات محددة.
- Teacher على `teacher.massar-academy.net`.
- Assistant/Staff على `staff.massar-academy.net`.
- Student على `app.massar-academy.net` أو `student.massar-academy.net`.
- API على `api.massar-academy.net`.
- WS على `ws.massar-academy.net`.

مخرجات القبول:

- تقرير نهائي: Ready / Not Ready.
- نتائج كل command.
- نتائج manual QA.
- backup/restore.
- rollback plan.

## ملفات غالبا ستتعدل

- `docker-compose.yml`
- `docker/nginx/massar.conf`
- `frontend/Dockerfile`
- `.env.example`
- `scripts/verify-surface-separation.mjs`
- `frontend/src/proxy.ts`
- `frontend/src/packages/surface-runtime/config.ts`
- `backend/src/NaderGorge.API/Controllers/AssistantController.cs`
- `frontend/src/app/teacher/activity/page.tsx`
- `frontend/src/app/student/profile/page.tsx`
- `frontend/src/app/student/notifications/page.tsx`
- `frontend/tests/e2e/*`
- `backend/tests/NaderGorge.Application.Tests/*`
- `tests/test_*`

## شروط الإغلاق النهائي

- لا يوجد surface يستخدم domain surface آخر.
- لا يوجد surface يعرض navbar surface آخر.
- كل surface له Docker service/port/env مستقل.
- كل surface له Docker image مستقل أو قرار runtime isolation موثق ومختبر.
- الدومين الأساسي والـ subdomains كلها على `massar-academy.net`.
- أي دومين قديم إما محذوف أو redirect-only موثق.
- كل role له E2E smoke.
- كل teacher-owned entity مربوطة بمدرس.
- Phase 7 المؤجلة لا تمنع الإطلاق الحالي.


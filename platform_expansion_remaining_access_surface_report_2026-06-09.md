# تقرير الناقص المتبقي - Access, Login, Pages, Domains

تاريخ المراجعة: 2026-06-09  
الدومين المعتمد: `massar-academy.net`  
المصادر:

- `platform_expansion_plan.md`
- `platform_expansion_action_plan_2026-06-09.md`
- `platform_expansion_gap_report_2026-06-09.md`
- مراجعة فعلية للصفحات، guards، proxy، Docker/Nginx، والاختبارات الحالية.

## القرار المختصر

الحالة: **Not Ready**.

فيه حاجات مهمة اتعملت بالفعل، لذلك التقرير ده يركز على المتبقي فقط:

- الصفحات الأساسية للمساعد والمدرس والطالب موجودة بدرجة كبيرة.
- `AdminGuard` اتضيق.
- Teacher bypass اتشال من frontend/backend permission checks.
- `teacher` و`assistant` services اتضافوا في Docker/Nginx.
- `teacher/activity`, `student/profile`, `student/notifications` موجودين حاليا.

المتبقي الأساسي:

- Login لازم يبقى واضح ومختلف لكل surface.
- أي subdomain لو المستخدم مش مسجل يفتح Login الخاص بنفس surface.
- لو المستخدم مسجل، يتوجه تلقائيا للـ dashboard الصحيح حسب نوع الحساب.
- أي حساب يحاول يفتح صفحة مش بتاعته يأخذ Error/Not Found، وليس redirect صامت لسطح آخر.
- Docker images ما زالت مشتركة بين كل frontend surfaces.
- الدومينات القديمة ما زالت موجودة في config.
- محتاجين E2E يثبت كل الكلام ده.

## المستبعد

Phase 7 - SMS Auto-Payment and Wallet Matching مؤجلة، ولا تدخل في هذا التقرير.

## الموجود حاليا

### Surfaces وصفحات الأدوار

Admin:

- `/admin`
- users/students/teachers/assistants/admins
- content/subjects/codes/questions
- hr/crm/operations/chat/media/finance/reports/settings

Assistant/Staff:

- `/assistant`
- `/assistant/dashboard`
- `/assistant/tasks`
- `/assistant/tasks/[id]`
- `/assistant/crm`
- `/assistant/chat`
- `/assistant/attendance`
- `/assistant/vacations`
- `/assistant/notifications`
- `AssistantShellChrome` وnavbar خاص بالمساعد.

Teacher:

- `/teacher`
- `/teacher/packages`
- `/teacher/codes`
- `/teacher/exams`
- `/teacher/essays`
- `/teacher/finance`
- `/teacher/chat`
- `/teacher/students`
- `/teacher/profile`
- `/teacher/activity`

Student:

- `/student`
- `/student/packages`
- `/student/lessons/[lessonId]`
- `/student/exams/[examId]`
- `/student/code-redemption`
- `/student/balance`
- `/student/community`
- `/student/mistakes`
- `/student/profile`
- `/student/notifications`

### Domains وDocker services

موجود:

- `landing`: `8738`
- `student`: `8739`
- `admin`: `8740`
- `teacher`: `8741`
- `assistant`: `8742`
- Nginx routing لـ:
  - `massar-academy.net`
  - `app.massar-academy.net`
  - `student.massar-academy.net`
  - `admin.massar-academy.net`
  - `super.massar-academy.net`
  - `teacher.massar-academy.net`
  - `staff.massar-academy.net`
  - `api.massar-academy.net`
  - `ws.massar-academy.net`
  - `assets.massar-academy.net`

## النواقص المتبقية

### 1. Login ليس surface-specific بما يكفي

المطلوب:

- كل surface له Login واضح بصريا ونصيا:
  - Student login على student/app domain.
  - Teacher login على teacher domain.
  - Assistant/Staff login على staff domain.
  - Admin/Supervisor login على admin/super domain.
- المستخدم وهو داخل login يعرف هو بيسجل فين.
- لو دخل root domain `/login` يتم توجيهه لبوابة الطالب أو صفحة اختيار بوابة واضحة حسب قرار المنتج.

الحالي:

- يوجد `/login` واحد عام.
- النصوص عامة للمنصة، وليست مخصصة لكل surface.
- `LoginForm` يوجه بعد الدخول حسب الدور، لكن `LoginPage` عند وجود جلسة يستخدم منطق `hasAdmin = any non-student` ويرسل أي staff إلى admin بدلا من teacher/staff حسب الدور.
- `returnUrl` يستخدم مباشرة بعد login، ويحتاج validation حتى لا يدخل المستخدم على route أو domain لا يخص دوره.

### 2. سلوك غير المسجل على subdomain يحتاج توحيد

المطلوب:

- غير المسجل على `teacher.massar-academy.net/*` يذهب إلى `teacher.massar-academy.net/login`.
- غير المسجل على `staff.massar-academy.net/*` يذهب إلى `staff.massar-academy.net/login`.
- غير المسجل على `app.massar-academy.net/*` يذهب إلى `app.massar-academy.net/login`.
- غير المسجل على `admin.massar-academy.net/*` يذهب إلى `admin.massar-academy.net/login`.
- بعد login يتم إرجاعه للمسار المسموح داخل نفس surface فقط، أو dashboard الخاص بدوره.

الحالي:

- guards تعمل `router.replace("/login")` نسبيا، وهذا جيد مبدئيا داخل نفس domain.
- لكن لا يوجد اختبار يثبت هذا لكل subdomain.
- لا يوجد validation موثق للـ `returnUrl`.

### 3. سلوك المسجل لازم يروح Dashboard حسب نوع الحساب

المطلوب:

- Student -> `app.massar-academy.net/student`
- Teacher -> `teacher.massar-academy.net/teacher`
- Assistant/Staff -> `staff.massar-academy.net/assistant`
- Admin -> `admin.massar-academy.net/admin`
- Supervisor -> `super.massar-academy.net/admin` أو surface مشرف موثق.

الحالي:

- `LoginForm` قريب من المطلوب.
- `LoginPage` عند وجود session بالفعل لا يستخدم نفس mapping؛ أي non-student يذهب إلى admin.
- لا يوجد اختبار يغطي user already logged-in visiting `/login` on each domain.

### 4. Cross-surface access يعطي redirect لا Error/Not Found

المطلوب من المستخدم:

- كل حساب محدد له صفحاته فقط.
- لو حاول يدخل صفحة مش بتاعته، يطلع له error أو صفحة "غير موجودة" كأن الصفحة مش موجودة.
- لا يتم redirect صامت للسطح الآخر لأن هذا يكشف وجود routes ويخلط تجربة المستخدم.

الحالي:

- `surface-runtime/config.ts` و`frontend/src/proxy.ts` يوجهان المسارات الخطأ إلى الدومين الصحيح.
- مثال: teacher surface عند فتح `/admin` يعمل redirect إلى admin origin.
- المطلوب الجديد عكس ذلك: داخل surface الغلط يجب 404/Not Found أو Forbidden branded للسطح الحالي.

المطلوب تعديله:

- استبدال cross-surface redirects داخل non-landing surfaces بـ `notFound`/rewrite إلى صفحة error.
- ترك landing فقط يوجه المستخدم للبوابات الصحيحة عند اختيارها.
- إضافة صفحة error موحدة لكل surface:
  - "الصفحة غير موجودة أو لا تخص هذا الحساب".
- عدم إظهار Admin route أو اسم route الداخلي للمستخدم غير المصرح.

### 5. لا يوجد Auth-aware domain gateway على مستوى proxy

المطلوب:

- أي subdomain يعرف إذا المستخدم مسجل أم لا.
- إن لم يكن مسجلا -> login الخاص بالـ surface.
- إن كان مسجلا ودوره لا يخص surface -> Not Found/Error أو redirect للdashboard الصحيح حسب القرار.

الحالي:

- `proxy.ts` يعتمد على `APP_SURFACE` والـ path فقط.
- الـ auth state غالبا client-side/local storage، لذلك proxy لا يعرف الدور.
- guards تعمل بعد تحميل الصفحة، وهذا قد يسمح بوميض أو redirect متأخر.

المطلوب:

- استخدام cookie/session يمكن قراءته server-side أو endpoint سريع للتحقق.
- أو قبول client-side guard مؤقتا، لكن يجب اختباره وتوثيق أن أول render لا يعرض بيانات محظورة.

### 6. Docker images ليست منفصلة فعليا

المطلوب حسب الشرط:

- كل surface له صورة Docker مستقلة لا علاقة لها بالتانية.

الحالي:

- `landing`, `student`, `admin`, `teacher`, `assistant` كلهم يستخدمون `massar_frontend:local`.
- هذا service/env separation وليس image isolation.

المطلوب:

- صور منفصلة:
  - `massar_landing_frontend:local`
  - `massar_student_frontend:local`
  - `massar_admin_frontend:local`
  - `massar_teacher_frontend:local`
  - `massar_assistant_frontend:local`
- أو قرار رسمي موثق: image واحدة مع build/runtime guard صلب، لكن هذا يخالف الشرط الحرفي.

### 7. الدومينات القديمة ما زالت موجودة

الحالي في Nginx/CORS ما زال يحتوي:

- `massarplatform.com`
- `bsma-academy.com`
- subdomains الخاصة بهم.

المطلوب:

- حذفها إذا لم تعد مستخدمة.
- أو تحويلها legacy redirects فقط إلى `massar-academy.net`.
- عدم اعتبارها surfaces تشغيل مستقلة.

### 8. Assistant permission variants تحتاج إثبات

المطلوب:

- Academic assistant يرى الأكاديمي فقط.
- CRM agent يرى CRM فقط وما يخصه.
- Operations staff يرى tasks/chat/attendance/vacations.
- Supervisor يرى review/approval بدون Admin كامل.

الحالي:

- Navbar خاص موجود.
- بعض العناصر تعتمد على permissions.
- لا يكفي بدون E2E/permission matrix.

### 9. Teacher binding يحتاج إغلاق اختباري

المطلوب إثباته:

- package/code/question/exam/finance كلها مربوطة بمدرس.
- Teacher A لا يرى Teacher B.
- Student يرى اسم وصورة المدرس بعد التفعيل.
- الأكواد العامة أو الرصيد لو تدخل في محاسبة المدرس تحتاج `TeacherId` صريح.

الحالي:

- الكود فيه مؤشرات جيدة، لكن التقرير النهائي يحتاج اختبارات ونتائج.

### 10. Launch drill ناقص

لا يوجد تقرير إغلاق يحتوي:

- نتائج build/lint/unit/e2e.
- Docker cold-start.
- health checks لكل domain/service.
- manual QA لكل role.
- backup/restore.
- rollback plan.

## أقل عدد Phases مقترح

### Phase 1 - Surface Login and Access Contract

الهدف: تثبيت سلوك الدخول والخروج ومنع الوصول الخاطئ.

المهام:

- تحديث Login UI ليعرض surface الحالي:
  - بوابة الطالب.
  - بوابة المدرس.
  - بوابة المساعدين/الموظفين.
  - بوابة الإدارة/المشرفين.
- توحيد role-to-dashboard mapping في `LoginForm` و`LoginPage`.
- validation للـ `returnUrl`:
  - يقبل فقط routes داخل surface ودور المستخدم.
  - لو غير صالح يذهب dashboard المناسب.
- تعديل `surface-runtime/config.ts` و`proxy.ts`:
  - wrong surface route داخل subdomain يعطي Not Found/Error.
  - landing فقط يوجه إلى domains المناسبة.
- إضافة صفحات error/not-found لكل surface برسالة واضحة.
- توثيق سياسة Supervisor: `super` domain أم admin domain بصلاحية محددة.

قبول phase:

- غير المسجل يذهب login الخاص بنفس subdomain.
- المسجل يذهب dashboard الصحيح حسب الدور.
- المسجل لا يستطيع فتح صفحة دور آخر.
- route غير مسموح يظهر Error/Not Found، وليس redirect لسطح آخر.

### Phase 2 - Docker/Domain Isolation and Role Permissions

الهدف: كل domain وDocker surface مستقل، وكل حساب يرى صفحاته فقط.

المهام:

- فصل Docker images لكل surface أو توثيق قرار مختلف بموافقة صريحة.
- تنظيف الدومينات القديمة من Nginx/CORS أو تحويلها legacy redirects فقط.
- تحديث `.env.example` وdeployment templates لـ `massar-academy.net`.
- إضافة forbidden-domain tests في `scripts/verify-surface-separation.mjs`.
- إضافة E2E:
  - Student لا يفتح admin/teacher/assistant.
  - Teacher لا يفتح admin/student/assistant.
  - Assistant لا يفتح admin/student/teacher.
  - Admin/Supervisor حسب الصلاحيات فقط.
- إضافة Assistant permission matrix tests.
- إضافة Teacher binding tests.

قبول phase:

- كل surface له domain وport وenv مستقل.
- كل image مستقلة، أو runtime isolation مثبت ومقبول.
- كل role له navbar وصفحات فقط حسب صلاحياته.
- لا يوجد route leak بين surfaces.

### Phase 3 - Full Regression and Launch Evidence

الهدف: إثبات الإغلاق النهائي.

الأوامر:

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

Docker:

```bash
make down
docker compose build --no-cache
make up
make migrate
make ps
node scripts/verify-surface-separation.mjs
```

Manual QA:

- `massar-academy.net`
- `app.massar-academy.net`
- `admin.massar-academy.net`
- `super.massar-academy.net`
- `teacher.massar-academy.net`
- `staff.massar-academy.net`
- `api.massar-academy.net`
- `ws.massar-academy.net`

قبول phase:

- تقرير Ready / Not Ready.
- نتائج كل command.
- Manual QA لكل role.
- backup/restore.
- rollback plan.

## ملفات غالبا ستتعدل

- `frontend/src/packages/surface-runtime/config.ts`
- `frontend/src/proxy.ts`
- `frontend/src/app/(public)/login/page.tsx`
- `frontend/src/components/forms/LoginForm.tsx`
- `frontend/src/app/not-found.tsx`
- `frontend/src/app/admin/not-found.tsx`
- `frontend/src/app/teacher/not-found.tsx`
- `frontend/src/app/assistant/not-found.tsx`
- `frontend/src/app/student/not-found.tsx`
- `docker-compose.yml`
- `docker/nginx/massar.conf`
- `frontend/Dockerfile`
- `.env.example`
- `scripts/verify-surface-separation.mjs`
- `frontend/tests/e2e/*`
- `backend/tests/NaderGorge.Application.Tests/*`
- `tests/test_*`

## شروط الإغلاق النهائي

- كل login يوضح المستخدم داخل أي بوابة.
- غير المسجل على أي subdomain يذهب login الخاص بنفس subdomain.
- المسجل يذهب dashboard الصحيح حسب نوع الحساب.
- أي حساب يفتح صفحة ليست له يرى Error/Not Found.
- لا يوجد redirect صامت يكشف surface آخر من داخل surface حالي.
- كل account له صفحات وnavbar وأزرار حسب صلاحياته فقط.
- كل domain مستقل ولا يعتمد على domain آخر.
- كل Docker image مستقلة أو runtime isolation موثق ومختبر.
- كل شيء يخص المدرس مربوط بمدرس.
- Phase 7 المؤجلة لا تمنع الإطلاق.


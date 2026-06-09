# خطة إغلاق توسعة منصة مسار - الصفحات، الصلاحيات، الدومينات، Docker

تاريخ المراجعة: 2026-06-09  
النطاق: مراجعة `platform_expansion_plan.md` والصفحات الحالية، مع استبعاد Phase 7 المؤجلة.

## القرار المختصر

الحالة: **Not Ready**.

معظم الكيانات والـ APIs موجودة، لكن الإغلاق الحقيقي يحتاج:

- فصل صفحات كل دور عن الإدارة.
- Navbar وPermissions مستقلة لكل دور.
- ربط كل المحتوى والأكواد والأسئلة والامتحانات والماليات بالمدرس.
- فصل الدومينات والـ Docker services/images/ports لكل surface بحيث لا يعتمد أي سطح على الآخر.
- E2E وDocker launch drill كامل.

## المستبعد

- Phase 7 - SMS Auto-Payment and Wallet Matching: مؤجلة، ولا تدخل في العمل الحالي.

## القواعد النهائية المطلوبة

### 1. فصل الأدوار والصفحات

كل دور له صفحات وNavbar وPermissions تخص شغله فقط:

- Admin: إدارة كاملة.
- Supervisor: مراجعة واعتماد وتقارير حسب الصلاحيات.
- Staff/Assistant: مهام، CRM، شات، حضور/إجازات، بدون روابط Admin.
- Teacher: محتوى، أكواد، طلاب، أسئلة/امتحانات، ماليات تخصه فقط.
- Student: تعلم، رصيد، تفعيل أكواد، مجتمع، بدون أي سطح داخلي.

ظهور الرابط في الـ navbar، فتح الصفحة، تنفيذ الزر، والـ API endpoint لازم يكونوا مربوطين بنفس صلاحية الدور.

### 2. فصل الدومينات والـ Docker

كل surface لازم يكون له domain وport وDocker image/service مستقل، أو قرار موثق بنفس القوة لو تم استخدام image واحدة بruntime isolation. المطلوب النهائي:

| Surface | Domain | Local port | Docker service/image | المسموح يشوفه |
| --- | --- | --- | --- | --- |
| Landing | `massar-academy.net`, `www.massar-academy.net` | `8738` | `landing` image/service | صفحات عامة فقط |
| Student | `student.massar-academy.net` أو `app.massar-academy.net` | `8739` | `student` image/service | Student فقط |
| Admin | `admin.massar-academy.net` أو `super.massar-academy.net` | `8740` | `admin` image/service | Admin/Supervisor حسب الصلاحيات |
| Teacher | `teacher.massar-academy.net` | منفصل، مثال `8741` | `teacher` image/service | Teacher فقط |
| Staff/Assistant | `staff.massar-academy.net` | منفصل، مثال `8742` | `staff` أو `assistant` image/service | Staff/Assistant |
| API | `api.massar-academy.net` | `5245` | `backend` | كل surfaces عبر CORS مضبوط |
| Worker/Bull Board | internal أو admin-only | `3001` | `worker` | Admin/Ops فقط |
| WebSocket | `ws.massar-academy.net` | حسب الإعداد | backend hub أو ws service | Chat/notifications حسب الدور |
| Assets | `assets.massar-academy.net` | حسب الإعداد | static/assets service | ملفات فقط بدون صلاحيات صفحات |

ممنوع:

- `teacher.massar-academy.net` يفتح admin surface.
- `staff.massar-academy.net` يفتح admin navbar.
- student domain يفتح `/admin` أو `/teacher`.
- أي image أو env أو `NEXT_PUBLIC_APP_SURFACE` خاصة بسطح تدخل سطح آخر.
- أي surface يعتمد على local/dev URL خاص بسطح ثاني.

المطلوب في Docker:

- Service منفصل أو runtime config منفصل لكل surface.
- `NEXT_PUBLIC_APP_SURFACE` مختلف لكل service.
- `NEXT_PUBLIC_PUBLIC_ORIGIN`, `NEXT_PUBLIC_STUDENT_ORIGIN`, `NEXT_PUBLIC_ADMIN_ORIGIN`, `NEXT_PUBLIC_TEACHER_ORIGIN`, `NEXT_PUBLIC_STAFF_ORIGIN` مضبوطة.
- CORS في backend يسمح فقط بالدومينات الرسمية.
- Nginx يوجه كل domain إلى service الصحيح.
- `scripts/verify-surface-separation.mjs` يتوسع ليفحص landing/student/admin/teacher/staff.

### 3. ربط كل شيء بالمدرس

كل محتوى تعليمي أو كود أو سؤال أو امتحان أو ربح مالي لازم يكون مربوطا بمدرس محدد:

- Admin عند إنشاء باقة يختار `teacherId` و`programId`/`subjectId`.
- Teacher عند إنشاء باقة لا يختار `teacherId`; النظام يستخدم current teacher فقط.
- Admin عند توليد كود يختار هدفا مربوطا بمدرس، أو يختار المدرس صراحة لو الكود غير مربوط بهدف.
- Teacher عند توليد كود يرى باقاته/دروسه/امتحاناته فقط.
- Admin عند إنشاء سؤال يختار subject وteacher.
- Teacher عند إنشاء سؤال يختار subject من مواده فقط، والمدرس يأتي من current user.
- الامتحانات ترث المدرس من منشئها أو من الدرس/الباقة المرتبطة.
- Student يرى اسم وصورة المدرس على الباقة، الكود المفعّل، والدرس حيث ينطبق.
- Finance يعتمد على teacher-linked activated codes فقط.
- ممنوع fallback إلى first/default teacher أو first/default subject في production flows.

## النواقص الحرجة الحالية

1. `AdminGuard` يسمح لأي دور غير Student بفتح `/admin`.
2. `useHasPermission` يعطي Teacher bypass كامل.
3. `HasPermissionAttribute` في backend يعطي Teacher bypass كامل.
4. لا يوجد `frontend/src/app/assistant/layout.tsx`.
5. صفحات `/assistant/crm` و`/assistant/chat` تستخدم `AdminGuard` و`AdminShellChrome`.
6. `/assistant/dashboard` لا يستخدم guard مستقل.
7. `StudentGuard` يتحقق من تسجيل الدخول فقط ولا يتحقق من دور Student.
8. لا توجد `/staff` أو `/supervisor` surfaces واضحة.
9. `teacher.massar-academy.net` حاليا موجه إلى admin service في Nginx.
10. Docker يفصل landing/student/admin فقط، ولا يفصل teacher/staff كصور وخدمات مستقلة.
11. Teacher dashboard يعرض cards ثابتة أكثر من بيانات فعلية.
12. لا توجد صفحة واضحة لطلاب المدرس أو activity/watch stats.
13. Assistant task details/status يحتاج ownership check أقوى.
14. Launch drill وE2E لكل الأدوار غير مكتملين.

## الصفحات المطلوبة حسب الدور

### Assistant / Staff

موجود حاليا:

- `/assistant/dashboard`
- `/assistant/crm`
- `/assistant/chat`

المطلوب:

- `/assistant` أو `/staff`
- `/assistant/tasks`
- `/assistant/tasks/[id]`
- `/assistant/crm`
- `/assistant/chat`
- `/assistant/attendance`
- `/assistant/vacations`
- `/assistant/notifications`
- Navbar خاص لا يحتوي users/settings/content العام.
- Permissions حسب نوع المساعد:
  - Academic assistant: مهام أكاديمية وتصحيح/مراجعة فقط.
  - CRM agent: الطلاب المسندين والمكالمات فقط.
  - Operations staff: المهام التشغيلية والشات والحضور فقط.
  - Supervisor: مراجعة واعتماد، وليس إدارة كاملة.

Task workflow:

- استلام المهمة.
- فتح التفاصيل.
- إضافة تعليق/مرفق.
- تغيير الحالة إلى InProgress/Review.
- انتظار اعتماد Supervisor/Admin عند الإغلاق.
- منع رؤية أو تعديل مهام مساعد آخر إلا بصلاحية Supervisor.

### Teacher

موجود حاليا:

- `/teacher`
- `/teacher/packages`
- `/teacher/packages/...`
- `/teacher/codes`
- `/teacher/codes/[groupId]`
- `/teacher/exams`
- `/teacher/finance`
- `/teacher/chat`

المطلوب:

- `/teacher/students`
- `/teacher/activity`
- `/teacher/essays` أو تبويب واضح داخل `/teacher/exams`
- `/teacher/profile`
- Dashboard ببيانات فعلية: عدد الطلاب، الباقات، الأكواد المستخدمة، الأرباح، مهام التصحيح.
- كل package يظهر TeacherName/SubjectName.
- كل code group يظهر مصدر الربط وTeacherName.
- توليد الأكواد يقتصر على موارد المدرس الحالي.
- E2E يثبت أن Teacher A لا يرى بيانات Teacher B.

### Student

موجود حاليا:

- `/student`
- `/student/packages`
- `/student/packages/[packageId]`
- `/student/lessons/[lessonId]`
- `/student/exams/[examId]`
- `/student/code-redemption`
- `/student/balance`
- `/student/community`
- `/student/mistakes`

المطلوب:

- `/student/profile`
- `/student/notifications`
- `/student/support` أو دمج واضح مع community.
- `StudentGuard` يقبل Student فقط، مع preview mode واضح لو Admin محتاج معاينة.
- student domain لا يفتح أي routes داخلية.

### Supervisor

المطلوب إذا تم فصله عن Admin:

- `/supervisor/tasks`
- `/supervisor/approvals`
- `/supervisor/crm`
- `/supervisor/hr`
- `/supervisor/media`
- `/supervisor/reports`

لو Supervisor سيبقى داخل Admin surface، يجب:

- Navbar باسم وصلاحيات Supervisor، وليس Admin كامل.
- منع users/settings الحساسة إلا بصلاحية واضحة.
- E2E يثبت أنه لا يرى صفحات Admin غير المصرح بها.

## أقل عدد Phases للتنفيذ

### Phase 1 - Guards, Permissions, Routing, Domains

الهدف: منع الوصول الخاطئ وفصل الدومينات قبل تطوير الصفحات.

- استبدال `AdminGuard` بمنطق Admin/Supervisor فقط.
- إنشاء `AssistantGuard`, `StaffGuard`, `TeacherGuard`, `StudentGuard`.
- تعديل `useHasPermission`: Admin فقط bypass.
- تعديل `HasPermissionAttribute`: Admin فقط bypass.
- تحديث policies في backend لكل دور.
- تحديث login redirect:
  - Student -> student domain.
  - Teacher -> teacher domain.
  - Assistant/Staff -> staff/assistant domain.
  - Admin/Supervisor -> admin/super domain.
- توسيع Docker/Nginx:
  - `teacher` service/image/port.
  - `staff` أو `assistant` service/image/port.
  - origins/env منفصلة لكل service.
- تحديث `scripts/verify-surface-separation.mjs` لفحص الدومينات والـ redirects والـ forbidden routes.
- E2E smoke: كل دور يدخل دومينه فقط، ويفشل عند دخول دومين غيره.

### Phase 2 - Assistant/Staff Surface and Task Workflow

الهدف: بناء مساحة المساعدين والموظفين بشكل مستقل عن Admin.

- إضافة `frontend/src/app/assistant/layout.tsx`.
- إنشاء navbar assistant/staff.
- نقل dashboard/crm/chat إلى shell مستقل.
- إضافة task list/detail/comments/status/review.
- إضافة attendance/vacations/notifications.
- إخفاء أي tab/button لا يملك المستخدم صلاحيتها.
- Backend ownership checks للمهام:
  - assignee يرى مهمته.
  - supervisor/admin يرى حسب الصلاحية.
  - غير ذلك 403.
- E2E: مساعد يرى مهامه وCRM فقط، ولا يرى Admin pages.

### Phase 3 - Teacher Binding and Teacher/Student Completion

الهدف: تثبيت عزل المدرسين وربط كل العمليات بالمدرس.

- إكمال teacher dashboard ببيانات فعلية.
- إضافة teacher students/activity/profile/essays.
- تثبيت teacher binding في:
  - package creation.
  - code generation.
  - question creation.
  - exam creation.
  - activated code accounting.
  - teacher finance.
- منع أي fallback إلى default teacher/subject.
- Student pages تعرض teacher identity بوضوح.
- E2E:
  - Admin ينشئ مدرس ومادة وباقة وكود.
  - Teacher A يرى موارده فقط.
  - Teacher B لا يرى موارد Teacher A.
  - Student يرى المدرس الصحيح بعد تفعيل الكود.

### Phase 4 - Regression, Docker Launch Drill, Final Report

الهدف: إثبات أن كل شيء يعمل من Docker والدومينات.

- تشغيل:
  - `dotnet build backend/NaderGorge.sln`
  - `dotnet test backend/NaderGorge.sln --no-build`
  - `cd frontend && npm run lint && npm run build`
  - `cd frontend && npm run test:e2e -- --project=chromium`
  - `cd worker && npm run build`
  - `python3 -m pytest tests -q`
  - `node scripts/generate-endpoint-inventory.mjs --check`
  - `node scripts/verify-surface-separation.mjs --static-only`
- Docker cold-start:
  - `make down`
  - `docker compose build --no-cache`
  - `make up`
  - `make migrate`
  - `make ps`
  - health checks لكل services.
  - `node scripts/verify-surface-separation.mjs`
- Manual QA لكل دور.
- backup/restore drill.
- rollback plan.
- تقرير إغلاق: Ready / Not Ready.

## ملفات غالبا ستتعدل

Frontend:

- `frontend/src/components/layout/AdminGuard.tsx`
- `frontend/src/components/layout/TeacherGuard.tsx`
- `frontend/src/components/layout/StudentGuard.tsx`
- `frontend/src/hooks/useHasPermission.ts`
- `frontend/src/app/assistant/layout.tsx`
- `frontend/src/app/assistant/dashboard/page.tsx`
- `frontend/src/app/assistant/crm/page.tsx`
- `frontend/src/app/assistant/chat/page.tsx`
- `frontend/src/app/teacher/page.tsx`
- `frontend/src/app/teacher/packages/page.tsx`
- `frontend/src/app/teacher/codes/page.tsx`
- `frontend/src/app/teacher/exams/page.tsx`
- `frontend/src/app/student/layout.tsx`
- `frontend/src/packages/admin/navigation.tsx`
- `frontend/src/services/*`

Backend:

- `backend/src/NaderGorge.API/Extensions/HasPermissionAttribute.cs`
- `backend/src/NaderGorge.API/Program.cs`
- `backend/src/NaderGorge.API/Controllers/AssistantController.cs`
- `backend/src/NaderGorge.Application/Features/Operations/Queries/GetTaskDetailsQuery.cs`
- `backend/src/NaderGorge.Application/Features/Operations/Commands/UpdateTaskStatusCommand.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Commands/BulkGenerateCodesCommand.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminQuestionCommands.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs`

Docker/Deploy:

- `docker-compose.yml`
- `frontend/Dockerfile`
- `docker/nginx/massar.conf`
- `scripts/verify-surface-separation.mjs`
- `.env.example` أو deployment env templates

Tests:

- `frontend/tests/e2e/*`
- `backend/tests/NaderGorge.Application.Tests/*`
- `tests/test_*`

## شروط القبول النهائي

- كل role له domain وlanding route واضح بعد login.
- كل surface له Docker service/image/port/env مستقل أو عزل runtime موثق ومختبر.
- كل domain يفتح surface الصحيح فقط.
- لا يوجد domain يفتح navbar أو صفحات surface آخر.
- كل role له navbar خاص.
- كل page/action/API عليها permission check مطابق.
- لا يوجد Teacher bypass عام لصلاحيات Admin.
- لا يوجد Student route مفتوح لأي authenticated user.
- كل package/code/question/exam/finance transaction مرتبط بمدرس أو مستبعد صراحة من محاسبة المدرسين.
- كل role له E2E smoke: login, dashboard, صفحة أساسية، direct forbidden page.
- Docker launch drill ينجح من cold start.


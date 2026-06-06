# تقرير الفحص التقني العميق - Nader Gorge

تاريخ الفحص: 2026-06-06  
النطاق: Frontend Next.js، Backend .NET، Worker Node/BullMQ، واجهات Admin، واجهات Student، اختبارات E2E/Unit، الأمن، الأداء، قابلية الصيانة.

## ملخص تنفيذي

المشروع ليس في حالة انهيار: `frontend build` ناجح، `frontend lint` بدون errors، `worker build` ناجح، و`dotnet test` ناجح. لكن توجد مشاكل تقنية مهمة قبل الاعتماد الإنتاجي الكامل، أغلبها في الأمن، جودة اختبارات E2E، أداء استعلامات صفحات الطالب/الأدمن، وتباين تصميم/تجربة الواجهات.

درجة الصحة العامة: **13/20 - مقبول مع عمل مهم مطلوب**

| البعد | الدرجة | أهم ملاحظة |
|---|---:|---|
| الأمن والصلاحيات | 2/4 | تخزين access/refresh tokens في `localStorage/sessionStorage`، وحماية route shell client-side فقط. |
| الأداء وقابلية التوسع | 2/4 | عدة استعلامات N+1 وتحميل graph كبير في dashboard/profile/progress. |
| موثوقية الاختبارات | 2/4 | E2E config قديم على port 3000 رغم أن التطبيق يعمل على 8738، وبعض selectors/endpoints قديمة. |
| جودة Frontend/Admin/Student | 3/4 | البناء واللينت جيدان، لكن يوجد خلط tokens، `any` كثير، confirm/prompt native، وتجربة admin ثقيلة. |
| Worker/Background Jobs | 4/4 | TypeScript build ناجح وحماية worker token موجودة، لكن توجد مخاطر في إلغاء jobs وتنظيف الملفات والـ cron. |

نتائج الفحوصات:

- `npm run lint` في `frontend`: نجح، مع warning واحد فقط في `frontend/src/app/admin/users/[id]/page.tsx:8`.
- `npm run build` في `frontend`: نجح.
- `npm run build` في `worker`: نجح.
- `dotnet test backend/NaderGorge.sln --no-restore`: نجح، 12 اختبارًا.
- لم أشغل Playwright E2E فعليًا لأن تشغيله يحتاج خدمات backend/frontend/E2E DB متاحة ومتزامنة، لكن فحص الملفات كشف مشاكل إعداد واضحة.

## P0 - مشاكل مانعة

لا توجد مشكلة P0 مؤكدة من build/test الحالي. المشروع يبني ويمرر unit tests. أقرب شيء لـ P0 عملي هو أن اختبارات E2E غالبًا غير قابلة للاعتماد بوضعها الحالي، لكنه مصنف P1 لأنه لا يمنع build الإنتاجي.

## P1 - مشاكل كبيرة يجب علاجها قبل الإنتاج

### P1-1: تخزين التوكنات في browser storage يرفع أثر أي XSS

الموقع:

- `frontend/src/lib/auth-storage.ts:69-71`
- `frontend/src/services/api-client.ts:24-30`
- `frontend/src/services/api-client.ts:53-68`

الوصف:

الـ access token والـ refresh token يتم تخزينهما في `localStorage` أو `sessionStorage`، ثم يقرأهما axios interceptor ويضع `Authorization: Bearer`. هذا يجعل أي ثغرة XSS في أي شاشة قادرة على سرقة refresh token طويل العمر، وليس فقط access token.

الأثر:

- سيطرة طويلة على حساب الطالب/الأدمن لو حدث XSS.
- صفحات فيها rich HTML أو scripts مدمجة تزيد حساسية هذا القرار.

التوصية:

- نقل refresh token إلى HttpOnly Secure SameSite cookie.
- إبقاء access token قصير جدًا أو استخدام BFF/proxy pattern.
- إضافة rotation مع reuse detection في backend.

### P1-2: حماية صفحات Admin/Student تعتمد على client guard فقط

الموقع:

- `frontend/src/components/layout/AdminGuard.tsx:18-35`
- `frontend/src/components/layout/StudentGuard.tsx:12-24`
- `frontend/src/app/admin/layout.tsx:24-37`
- `frontend/src/app/student/layout.tsx:17-20`

الوصف:

الـ route shell يتحقق بعد render client-side من storage. الـ backend endpoints محمية وهذا جيد، لكن route access نفسه لا توجد له middleware/SSR redirect. صفحات كثيرة مبنية كـ static routes في `next build`، ثم تعتمد على client redirect.

الأثر:

- Flash/loading shell قبل الحسم.
- UX ضعيف في الجلسات المنتهية.
- أي data fetching client-side سليم من ناحية API، لكن routing authorization غير موحد وممكن يتكرر أو ينسى.

التوصية:

- إضافة Next middleware يفصل `/admin`, `/student`, `/assistant` بناءً على cookie/session server-readable.
- إن استمر token في storage فقط، أضف route-level auth state bootstrap موحد وامنع static shells من عرض محتوى قبل التحقق.

### P1-3: E2E tests غير موثوقة بسبب port/config/selectors/endpoints قديمة

الموقع:

- `frontend/playwright.config.ts:16-18`
- `frontend/package.json:6-10`
- `frontend/tests/e2e/codes.spec.ts:31-36`
- `frontend/tests/e2e/assistant-dashboard.spec.ts` يستخدم `/api/v1/auth/login` في أكثر من موضع بينما controllers الأساسية على `api/[controller]`.

الوصف:

Playwright مضبوط على `http://localhost:3000`، بينما scripts تشغل Next على `8738`. كذلك بعض الاختبارات تتوقع نصوص قديمة مثل `Access Codes` و`BullMQ Bulk Generate` بينما الواجهة الحالية عربية ومختلفة. بعض login payloads تستخدم `deviceId` بدل `deviceFingerprint` المطلوب في `AuthController`.

الأثر:

- CI ممكن يكون أخضر بشكل مضلل لو E2E لا يعمل، أو أحمر بسبب test drift وليس bug حقيقي.
- صعب الاعتماد على E2E لحماية تدفقات الدفع/الأكواد/الامتحانات.

التوصية:

- توحيد `baseURL` مع `8738` أو جعلها env-driven.
- إضافة `webServer` في Playwright config.
- تحديث selectors إلى role/name مستقرة.
- توحيد API base paths والـ DTOs المستخدمة في الاختبارات.

### P1-4: استعلامات Student dashboard/progress فيها تحميل زائد وN+1

الموقع:

- `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs:47-50`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs:108-115`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetProgressQuery.cs:36-39`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetQuickAccessQuery.cs:31-83`

الوصف:

Dashboard/Progress يحمل Packages -> Terms -> Sections -> Lessons كاملة ثم يحسب في الذاكرة. في `GetDashboardQuery` يتم جلب كل امتحان داخل loop (`FirstOrDefaultAsync`)، و`GetQuickAccessQuery` يجلب entity داخل loop لكل grant.

الأثر:

- بطء واضح مع الطلاب أصحاب باقات كثيرة.
- استهلاك ذاكرة أعلى من اللازم.
- latency متزايد مع حجم المحتوى.

التوصية:

- Projection مباشر إلى DTO بدل `Include` كامل.
- جلب exams/terms/sections/lessons المطلوبة دفعة واحدة باستخدام ids.
- إضافة indexes على `StudentAccessGrants(UserId, IsActive, ExpiresAt)`, `LessonProgresses(UserId, LessonId)`, `StudentExamAttempts(UserId, ExamId, IsPassed)`.

### P1-5: Admin student profile فيه N+1 وبيانات placeholders

الموقع:

- `backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs:51-72`
- `backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs:88-92`
- `backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs:93-115`

الوصف:

كل package grant يعمل `FindAsync` منفصل للباقة. `Overrides` يرجع list فارغة مع comment أنها placeholder. صفحة الطالب الشاملة في الأدمن تعتمد على هذا DTO، وبالتالي تعرض بيانات ناقصة أو مضللة.

الأثر:

- شاشة الأدمن الحرجة قد لا تعرض overrides الحقيقية.
- بطء مع الطلاب أصحاب منح كثيرة.
- قرارات دعم خاطئة لأن جزء من history غير ظاهر.

التوصية:

- join/projection لجلب package grants مع package data في query واحدة.
- تنفيذ مصدر overrides الحقيقي أو إزالة القسم من UI حتى يكتمل.
- إضافة tests لهذه الشاشة لأنها تحتوي قرارات إدارية عالية التأثير.

### P1-6: إلغاء Worker job بـ `job.remove()` لا يضمن إيقاف job نشط فعليًا

الموقع:

- `worker/src/index.ts:212-227`
- `frontend/src/app/admin/ai-monitor/page.tsx:429-436`

الوصف:

Endpoint الإلغاء يحاول `job.remove()` للـ job. في BullMQ، إزالة job نشط ليست دائمًا إيقافًا تعاونيًا للمعالجة الجارية. Job استخراج فيديو/نداء Gemini قد يستمر حتى لو الواجهة اعتبرته اتلغى.

الأثر:

- تكلفة AI/تحميل فيديو قد تستمر بعد الإلغاء.
- حالة frontend/backend قد تتعارض: UI يقول canceled بينما worker يكمل callback.
- احتمالية overwrite لنتيجة قديمة بعد إلغاء أو retry.

التوصية:

- إضافة cancellation flag في DB/Redis يفحصه processor بين المراحل.
- تمرير AbortController للـ fetch/عمليات قابلة للإلغاء حيث يمكن.
- جعل callback backend يرفض النتائج إذا حالة الفيديو أصبحت Cancelled.

### P1-7: native confirm/prompt في إجراءات إدارية خطرة

الموقع:

- `frontend/src/app/admin/ai-monitor/page.tsx:429-430`
- `frontend/src/app/admin/ai-monitor/page.tsx:268`
- `frontend/src/components/admin/CommunityCommentsModerationTable.tsx` يستخدم `window.prompt` حسب نتائج البحث.

الوصف:

إجراءات مثل إلغاء AI job أو إعادة توليد mindmap تستخدم browser confirm/prompt. هذا لا يعطي سياقًا كافيًا ولا audit note ولا قابلية وصول جيدة.

الأثر:

- أخطاء تشغيلية من الأدمن.
- صعوبة توثيق السبب.
- تجربة غير متسقة مع باقي UI.

التوصية:

- استخدام `ConfirmDialog` موحد مع عنوان، أثر العملية، item summary، وسبب إلزامي للإجراءات المدمرة.

## P2 - مشاكل متوسطة

### P2-1: استخدام `any` واسع في service layer ومكونات admin/video

الموقع:

- `frontend/src/services/admin-service.ts:327-329`
- `frontend/src/services/admin-service.ts:548-596`
- `frontend/src/app/admin/users/[id]/page.tsx` عدة render callbacks بـ `any`
- `worker/src/jobs/analyzeVideoChapters.ts:19`
- `worker/src/services/geminiService.ts:191`

الوصف:

Type safety ضعيف في DTOs حرجة مثل videos/resources/homework/content creators. هذا يخفي contract drift بين backend/frontend.

الأثر:

- أخطاء runtime عند تغير shape من backend.
- صعوبة refactor.
- اختبارات TypeScript لا تمسك مشاكل البيانات.

التوصية:

- تعريف DTOs دقيقة لكل endpoint.
- استخدام schema validation خفيف للـ AI/worker payloads مثل zod أو parser يدوي واضح.

### P2-2: Student community يستخدم admin tokens وألوان hard-coded

الموقع:

- `frontend/src/components/student/CommunityFeed.tsx:29-40`
- `frontend/src/components/student/CommunityFeed.tsx:81-82`
- `frontend/src/components/student/CommunityPostComposer.tsx:89-111`
- `frontend/src/components/student/CommunityPostComposer.tsx:156-160`

الوصف:

واجهة الطالب تستخدم `--admin-*` tokens وFacebook blue `#0866ff`. هذا يخالف فصل surfaces ويفقد هوية الطالب التعليمية.

الأثر:

- تباين بصري بين الطالب وباقي المنتج.
- صعوبة theme customization لاحقًا.

التوصية:

- إنشاء semantic tokens مشتركة: `--surface-card`, `--surface-muted`, `--status-info`.
- إعادة community كـ "مناقشة صف" لا social feed.

### P2-3: inline style blocks وصفحة AI monitor ضخمة وغير معزولة

الموقع:

- `frontend/src/app/admin/ai-monitor/page.tsx:861+`

الوصف:

صفحة AI monitor تحتوي CSS كبير داخل component. هذا يجعل الصيانة والـ theming والمراجعة أصعب، ويزيد احتمال كسر responsive states.

الأثر:

- صعوبة reuse.
- styles غير قابلة للفحص المركزي.
- أي تغيير صغير في monitoring يصبح risky.

التوصية:

- تقسيم الصفحة إلى components: `WorkerStatusBanner`, `JobCard`, `MindmapTracker`, `FailedJobsTable`.
- نقل styles إلى CSS module أو tokens/classes مشتركة.

### P2-4: Exam drafts تحفظ إجابات الطالب في localStorage

الموقع:

- `frontend/src/components/exams/ExamViewer.tsx:676-686`
- `frontend/src/components/exams/ExamViewer.tsx:699-718`
- `frontend/src/components/exams/ExamViewer.tsx:775-780`

الوصف:

الإجابات تحفظ محليًا باسم attempt id. هذا جيد للـ recovery، لكنه يعرض إجابات الامتحان لأي script على الصفحة، ويبقى أثر بيانات تعليمية بعد session issues لو فشل التنظيف.

الأثر:

- خصوصية أقل للطالب.
- احتمال leakage في جهاز مشترك.

التوصية:

- حفظ drafts server-side أو sessionStorage مع TTL وتنظيف عند logout.
- تشفير local draft بمفتاح session غير persistent إن استمر التخزين المحلي.

### P2-5: Exception middleware يكتب إلى `/tmp` مباشرة

الموقع:

- `backend/src/NaderGorge.API/Middleware/ExceptionHandlingMiddleware.cs:62-72`

الوصف:

الـ middleware يستخدم `File.AppendAllText("/tmp/NaderGorge_errors.txt", ex.ToString())` بجانب logger. هذا يخرج عن logging pipeline وقد يفشل حسب صلاحيات container أو يسرّب stack traces محليًا.

الأثر:

- مشاكل تشغيل في containers/read-only filesystems.
- logs غير مركزية.

التوصية:

- حذف الكتابة المباشرة، والاعتماد على structured logging provider.
- تضمين correlation id فقط في response.

### P2-6: Cron worker مكتوب كـ interval hourly رغم وصفه nightly

الموقع:

- `worker/src/index.ts:156-164`

الوصف:

الدالة تقول nightly sweep لكنها تعمل كل ساعة. التعليق يذكر simulated hourly، لكنه موجود في مسار worker العام.

الأثر:

- إنذار/حساب commitment أكثر من المتوقع.
- ضغط زائد على DB.

التوصية:

- استخدام BullMQ repeatable jobs أو cron حقيقي مضبوط env-driven.
- فصل dev cadence عن production cadence.

### P2-7: video embed anti-download يعتمد على obfuscation وليس security boundary

الموقع:

- `frontend/src/app/api/video/embed/route.ts:20-31`
- `frontend/src/app/api/video/embed/route.ts:117-260`

الوصف:

الصفحة تولد HTML يتلاعب بـ DOM APIs ويخفي iframe ويفحص devtools. هذا يرفع الاحتكاك لكنه ليس حماية حقيقية؛ الـ stream/provider id يمكن استخلاصه من runtime دائمًا.

الأثر:

- إحساس أمان مبالغ فيه.
- احتمال كسر accessibility/debugging/browser compatibility.

التوصية:

- التعامل معه كـ deterrence فقط.
- الاعتماد الحقيقي يكون signed sessions قصيرة، watermark، watch limits server-side، وlogging.

## P3 - تحسينات وصيانة

### P3-1: warning لينت واحد

الموقع:

- `frontend/src/app/admin/users/[id]/page.tsx:8`

الوصف:

`Play` imported وغير مستخدم.

التوصية:

- حذف import.

### P3-2: ملفات build artifacts داخل backend tree

الموقع:

- `backend/src/**/bin`
- `backend/src/**/obj`

الوصف:

وجود bin/obj داخل tree يزيد ضوضاء البحث والتقارير. قد تكون غير tracked، لكن تظهر في الفحص.

التوصية:

- التأكد من `.gitignore`.
- تنظيف artifacts قبل audits/CI إن كانت غير لازمة.

### P3-3: Playwright tests تستخدم waits ثابتة

الموقع:

- `frontend/tests/e2e/auth.spec.ts:6`
- `frontend/tests/e2e/auth.spec.ts:31`
- عدة ملفات E2E أخرى.

الوصف:

`waitForTimeout` يجعل الاختبارات أبطأ وأقل موثوقية.

التوصية:

- استبدالها بانتظار عناصر أو responses محددة.

## ملاحظات حسب السطح

### Frontend عام

الإيجابيات:

- `next build` ناجح.
- lint errors = 0.
- يوجد `StudentGuard` و`AdminGuard`.
- يوجد sanitization في `ExamViewer` قبل `dangerouslySetInnerHTML`، وهذا جيد.

المشاكل:

- Auth tokens في browser storage.
- `any` في service layer.
- hard-coded colors وadmin tokens داخل student UI.
- اختبار E2E غير متزامن مع ports/UI الحالي.

### Admin

الإيجابيات:

- endpoints محمية بـ `[Authorize(Roles = "Admin")]`.
- worker proxy يتحقق من staff role قبل تمرير `WORKER_ADMIN_TOKEN`.

المشاكل:

- AI monitor ضخم ومليء CSS داخلي وnative confirms.
- Student profile query فيه placeholders وN+1.
- إجراءات خطرة تحتاج confirm dialog مع audit reason.

### Student

الإيجابيات:

- `StudentGuard` موجود.
- backend endpoints عامة للطالب محمية بـ `[Authorize]`.
- progress/exam/lesson locking موجود على backend وليس UI فقط.

المشاكل:

- student shell يعتمد على client auth.
- dashboard/progress queries تحمل بيانات كثيرة.
- community UI تستخدم admin visual system.
- exam drafts في localStorage.

### Backend

الإيجابيات:

- JWT validation وsecurity config validator موجودان.
- rate limiting موجود على auth/codes/parent.
- unit tests الحالية ناجحة.
- callback/internal token validation موجود في controllers الحساسة.

المشاكل:

- بعض queries تحتاج projection وتحسين indexing.
- refresh tokens مخزنة كقيمة مباشرة في DB حسب `LoginCommand` و`RefreshTokenCommand`; الأفضل hash refresh tokens في DB.
- Exception middleware يكتب stack traces إلى `/tmp`.
- tests تغطي 12 حالة فقط، أغلبها application logic حديث وليس auth/device/codes/watch limits.

### Worker

الإيجابيات:

- TypeScript build ناجح.
- `WORKER_ADMIN_TOKEN` و`API_CALLBACK_SECRET` مطلوبان عند startup.
- Bull Board محمي بتوكن.

المشاكل:

- cancel لا يضمن إيقاف job نشط.
- cron hourly رغم اسم nightly.
- `.tmp` audio/subtitle lifecycle يحتاج retention/cleanup job.
- استخدام `any` في AI payloads.

## أولويات الإصلاح المقترحة

1. **P1 - Auth hardening**: انقل refresh token إلى HttpOnly cookie، وابدأ في server-readable session strategy.
2. **P1 - E2E repair**: أصلح Playwright baseURL/webServer/selectors/API DTOs، ثم اجعلها جزءًا من CI.
3. **P1 - Query optimization**: حسّن Student dashboard/progress/quick-access وAdmin student profile باستخدام projections/batched queries.
4. **P1 - Worker cancellation**: أضف cancellation state وفحصه في processor/backend callbacks.
5. **P1 - Admin destructive actions UX**: استبدل confirm/prompt native بمكون confirm موحد مع reason/audit.
6. **P2 - Type cleanup**: استبدل `any` في admin-service/video/worker payloads بـ DTOs.
7. **P2 - Surface token normalization**: افصل student/community tokens عن admin tokens وأزل hard-coded blues.
8. **P2 - Logging cleanup**: أزل الكتابة المباشرة لـ `/tmp` من middleware.
9. **P3 - Cleanup**: حذف import غير مستخدم، تنظيف build artifacts، وتقليل `waitForTimeout`.

## أوامر التحقق التي تم تشغيلها

```bash
cd frontend && npm run lint
cd frontend && npm run build
cd worker && npm run build
dotnet test backend/NaderGorge.sln --no-restore
```

كلها نجحت.

## تحديث تنفيذ Spec Kit الكامل - 2026-06-06

تم إغلاق مهام المعالجة المتبقية ضمن `specs/083-deep-audit-remediation/tasks.md` من T052 إلى T057:

- نقل refresh token من browser storage إلى cookie `HttpOnly` في تدفق login/refresh، مع إبقاء access token فقط في تخزين العميل وإزالة أي refresh token قديم من التخزين.
- إزالة الكتابة المباشرة لملف `/tmp/NaderGorge_errors.txt` من exception middleware والاعتماد على structured logging الموجود.
- تحسين استعلام admin student profile بإسقاط package grants مباشرة مع بيانات package، وإرجاع بيانات video overrides الحقيقية بدل placeholder فارغ.
- استبدال إلغاء BullMQ النشط بعلامة إلغاء تعاونية في Redis، مع إزالة آمنة فقط للمهام waiting/delayed/prioritized ونقاط فحص داخل processors الطويلة.
- استبدال native `confirm`/`prompt` في AI monitor وتعليقات المجتمع بمكوّنات React قابلة للوصول ومناسبة لسياق الأدمن.

أوامر التحقق بعد الإغلاق:

```bash
dotnet build backend/NaderGorge.sln --no-restore
dotnet test backend/NaderGorge.sln --no-build
cd frontend && npm run lint
cd frontend && npm run build
cd worker && npm run build
```

كل الأوامر نجحت بتاريخ 2026-06-06 بدون أخطاء أو تحذيرات lint.

## ملفات يجب مراجعتها أولًا عند بدء الإصلاح

- `frontend/src/lib/auth-storage.ts`
- `frontend/src/services/api-client.ts`
- `frontend/playwright.config.ts`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetProgressQuery.cs`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetQuickAccessQuery.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Queries/GetStudentProfileDetailQuery.cs`
- `worker/src/index.ts`
- `frontend/src/app/admin/ai-monitor/page.tsx`
- `frontend/src/components/student/CommunityFeed.tsx`
- `frontend/src/components/student/CommunityPostComposer.tsx`

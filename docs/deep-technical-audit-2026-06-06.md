# تقرير بحث تقني شامل لمشاكل منصة Masar/Nader Gorge

تاريخ التقرير: 2026-06-06  
النطاق: Frontend, Backend API, Admin, Student, Worker, Docker/Ops, Tests  
الهدف: توثيق المشاكل التقنية الحالية القابلة للتحقق، مع الأولوية والتأثير وخطوات العلاج المقترحة.

## ملخص تنفيذي

حالة المشروع العامة: **قابل للبناء، لكنه يحتاج إصلاحات تشغيلية وأمنية قبل اعتباره production-ready بالكامل**.

الفحوصات التي تم تشغيلها:

| الفحص | النتيجة |
|---|---|
| `frontend npm run lint` | نجح |
| `frontend npm run build` | نجح |
| `backend dotnet build NaderGorge.sln` | نجح بدون warnings |
| `backend dotnet test NaderGorge.sln --no-build` | نجح: 12 test |
| `worker npm run build` | نجح |
| `python3 -m pytest -q` | فشل: `pytest` غير مثبت في البيئة |
| `node scripts/generate-endpoint-inventory.mjs` | نجح ووجد 144 endpoint |

توزيع المشاكل:

| الأولوية | العدد | المعنى |
|---|---:|---|
| P0 | 3 | مشاكل تكسر workflow أو تفتح سطح حساس |
| P1 | 9 | مشاكل كبيرة تؤثر على الطلاب/الأدمن/الأمان/البيانات |
| P2 | 12 | مشاكل جودة وتشغيل وقابلية صيانة |
| P3 | 6 | تحسينات مهمة لكنها ليست عاجلة |

أهم 5 مشاكل:

1. **Worker proxy في Next.js لا يمرر auth الحقيقي، وفي نفس الوقت لا يتحقق من دور Admin**.
2. **QR auto-redeem لا يعمل غالبا بسبب تعارض تخزين auth في localStorage مع route يبحث عن cookie**.
3. **Homework service يستدعي paths غير موجودة (`/api/api/v1/...`)**.
4. **عمليات الأكواد والرصيد والمشاهدة معرضة race conditions بدون transaction/locking كافيين**.
5. **فصل landing/student/admin موجود في Docker والـ proxy، لكن نفس bundle والـ routes ما زالت تحتوي كل الأسطح، مما يقلل العزل الفعلي**.

## تقدير جودة الواجهة

| البعد | الدرجة /4 | ملاحظة |
|---|---:|---|
| Accessibility | 2 | يوجد ARIA في أماكن جيدة، لكن الاعتماد الكثيف على custom UI وoverlays يحتاج e2e/a11y coverage |
| Performance | 2 | build ناجح، لكن صفحات admin/student كثيفة client-side وتستخدم polling وanimations كثيرة |
| Theming | 3 | token system واضح، لكن الطالب يعتمد على `--admin-*` واسماء admin داخل student |
| Responsive | 2 | يوجد mobile nav، لكن كثافة الجداول والـ cards في admin تحتاج اختبار حقيقي على viewports |
| Anti-patterns | 2 | تكرار cards/glass/rounded-heavy/ambient gradients بكثرة يجعل الواجهة أقل نضجا بصريا |
| الإجمالي | 11/20 | مقبول، يحتاج تمريرة harden/adapt/normalize |

## P0 - مشاكل عاجلة

### P0-1: Worker proxy يكسر شاشة مراقبة AI وقد يسمح بتحكم غير مضبوط

المكان:
- `frontend/src/app/api/worker/[...path]/route.ts:30-47`
- `frontend/src/app/admin/ai-monitor/page.tsx:162`
- `frontend/src/app/admin/ai-monitor/page.tsx:789-801`
- `frontend/src/components/admin/LessonVideoList.tsx:27-78`
- `worker/src/index.ts:183-240`

المشكلة:
- Next proxy يشترط وجود `authorization` header فقط، لكنه لا يتحقق من JWT ولا من دور المستخدم.
- صفحات admin تستدعي `fetch('/api/worker/status/...')` مباشرة، وليس `apiClient`، لذلك لا تضيف Authorization من localStorage.
- النتيجة العملية: طلبات مراقبة/حذف/إعادة jobs غالبا ترجع 401 من proxy.
- لو تم تمرير أي Authorization header يدويا، الـ proxy يستخدم `WORKER_ADMIN_TOKEN` server-side للوصول إلى worker، بدون إثبات أن الطالب/المستخدم Admin.

التأثير:
- شاشة AI monitor وtracking داخل LessonVideoList قد تظهر worker unavailable/unauthorized.
- احتمال escalation: أي مستخدم قادر على صنع request فيه Authorization header قد يصل إلى عمليات DELETE/POST retry عبر proxy لو endpoint متاح له.

الإصلاح المقترح:
- لا تعتمد على header وجودي فقط.
- أضف JWT validation داخل route handler أو مرر الطلب إلى backend endpoint محمي بـ `[Authorize(Roles="Admin,Teacher")]`.
- في frontend استخدم helper خاص يقرأ access token أو استخدم `apiClient` بدلا من raw `fetch`.
- اجعل DELETE/Retry محصورة في Admin/Teacher فقط، وليس أي authenticated user.

### P0-2: QR auto-redeem لا يتوافق مع نظام auth الحالي

المكان:
- `frontend/src/app/api/qr/[codeHash]/route.ts:24-46`
- `frontend/src/lib/auth-storage.ts:60-72`
- `frontend/src/services/api-client.ts:24-30`

المشكلة:
- QR route يقرأ token من cookie باسم `token`.
- نظام login يخزن `accessToken`, `refreshToken`, و`user` داخل localStorage/sessionStorage.
- لا يوجد في auth flow الحالي ما يضع cookie باسم `token`.

التأثير:
- الطالب المسجل دخوله عندما يفتح QR URL من المتصفح سيظهر للـ server route كأنه غير مسجل.
- سيعاد توجيهه إلى login بدلا من تفعيل الكود.
- بعد login لا يوجد ضمان أن `returnUrl=/api/qr/...` سيعمل لأن token لا يزال ليس cookie.

الإصلاح المقترح:
- إما تحويل auth إلى HttpOnly cookies للـ server routes.
- أو جعل QR flow client-side: صفحة `/qr/[codeHash]` تقرأ token من auth store وتستدعي `/codes/activate`.
- أو route يمرر المستخدم إلى صفحة student redeem ومعها codeHash، وليس يحاول تفعيل server-side.

### P0-3: Homework frontend يستدعي API paths غير موجودة

المكان:
- `frontend/src/services/homework-service.ts:24-31`
- `backend/src/NaderGorge.API/Controllers/HomeworkController.cs:10-29`
- `frontend/src/services/api-client.ts:11`

المشكلة:
- `apiClient` base URL ينتهي بـ `/api`.
- `homework-service` يستدعي `/api/v1/students/homework/pending` و`/api/v1/students/homework/{id}/submit`.
- المسارات الفعلية في backend هي `/api/homework/pending` و`/api/homework/{homeworkId}/submit`.
- الناتج الفعلي يصبح غالبا `/api/api/v1/students/homework/...`.

التأثير:
- واجبات الطالب pending/submit ستفشل 404.

الإصلاح المقترح:
- عدل service إلى:
  - `apiClient.get('/homework/pending')`
  - `apiClient.post(`/homework/${homeworkId}/submit`, answers)`
- أضف integration/e2e test لصفحة الواجب.

## P1 - مشاكل كبيرة

### P1-1: Refresh/access tokens مخزنة في localStorage

المكان:
- `frontend/src/lib/auth-storage.ts:69-71`
- `frontend/src/lib/auth-storage.ts:121-128`

المشكلة:
- accessToken وrefreshToken محفوظان في localStorage/sessionStorage.
- أي XSS داخل التطبيق أو dependency يمكنه سرقة refresh token طويل العمر.

التأثير:
- account takeover لطلاب أو admin.
- الخطر أكبر لأن admin يستخدم نفس auth mechanism.

الإصلاح المقترح:
- نقل refresh token إلى HttpOnly Secure SameSite cookie.
- إبقاء access token قصير العمر في memory فقط أو cookie HttpOnly مع CSRF protection.
- إضافة CSP قوية وتقليل `dangerouslySetInnerHTML`.

### P1-2: Code redemption معرض لسباق تفعيل نفس الكود

المكان:
- `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs:47-64`
- `backend/src/NaderGorge.Application/Features/Codes/Commands/ActivateCodeCommand.cs:115-153`
- `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs:202-209`

المشكلة:
- يتم جلب الكود بشرط `!IsConsumed` ثم يتم تعليمه consumed في الذاكرة.
- لا يوجد transaction isolation/row lock/concurrency token يضمن أن طلبين متزامنين لن ينجحا معا.

التأثير:
- نفس الكود يمكن أن يمنح access لأكثر من مستخدم تحت ضغط أو إعادة محاولة متزامنة.

الإصلاح المقترح:
- استخدم transaction مع `SELECT ... FOR UPDATE` أو update مشروط:
  - `UPDATE access_codes SET IsConsumed=true ... WHERE Id=@id AND IsConsumed=false`
  - تحقق من affected rows = 1.
- أضف unique partial guard للـ grants حسب نوع المحتوى والمستخدم.

### P1-3: شراء المحتوى والرصيد معرضان race conditions

المكان:
- `backend/src/NaderGorge.Application/Features/Student/Commands/PurchaseContentCommand.cs:62-115`
- `backend/src/NaderGorge.Application/Services/BalanceService.cs:57-72`
- `backend/src/NaderGorge.Application/Services/BalanceService.cs:92-111`

المشكلة:
- يتم قراءة الرصيد ثم تعديله وحفظه بدون row lock أو optimistic concurrency.
- طلبان شراء متزامنان قد يقرآن نفس الرصيد ويصرفاه مرتين.

التأثير:
- رصيد سلبي أو grants بدون رصيد كاف.
- تضارب في `BalanceAfter`.

الإصلاح المقترح:
- إضافة row version/concurrency token لـ `StudentBalance`.
- أو تنفيذ debit بعملية SQL شرطية: `WHERE CurrentBalance >= amount`.
- لف purchase + grant + transaction داخل transaction صريحة.

### P1-4: تتبع المشاهدة يعتمد على client seconds ويمكن تزويره

المكان:
- `frontend/src/components/video/SecureVideoPlayer.tsx:332-346`
- `frontend/src/components/video/SecureVideoPlayer.tsx:290-295`
- `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs:65-89`

المشكلة:
- backend يضيف `SecondsWatched` كما يرسلها العميل.
- لا يوجد session binding أو server-side elapsed validation أو حد أقصى للزيادة لكل request.

التأثير:
- الطالب يستطيع عبر console/API رفع secondsWatched بسرعة وتجاوز watch threshold/locks.

الإصلاح المقترح:
- اربط progress بـ `VideoPlaybackSession`.
- اقبل delta بحد أقصى بناء على الزمن الحقيقي منذ آخر update.
- خزّن `LastProgressAt` وتحقق من session/user/video.

### P1-5: منطق قفل المشاهدة يسمح بمشاهدة إضافية قبل القفل

المكان:
- `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs:81-91`

المشكلة:
- القفل يحصل عندما `WatchCount > MaxWatchCount`.
- لو الحد 3، سيصبح القفل بعد الوصول إلى 4 وليس عند 3.

التأثير:
- الطالب يحصل على مشاهدة إضافية كاملة فوق الحد المتوقع.

الإصلاح المقترح:
- استخدم `>=` عند الوصول للحد، أو عرّف بوضوح هل `MaxWatchCount` عدد مرات مسموحة قبل القفل أم بعده.
- أضف unit tests للحالات: 0, 1, max, max+1.

### P1-6: AdminGuard يسمح لـ Assistant بدخول كامل admin UI بينما backend لا يسمح له بكل endpoints

المكان:
- `frontend/src/components/layout/AdminGuard.tsx:8-11`
- أمثلة backend:
  - `backend/src/NaderGorge.API/Controllers/AdminController.cs:13`
  - endpoints كثيرة تحت `[Authorize(Roles = "Admin")]`

المشكلة:
- الواجهة تعتبر `Assistant` له admin access.
- كثير من controllers/backend operations تتطلب Admin فقط أو Admin/Teacher فقط.

التأثير:
- تجربة assistant ستظهر صفحات/أزرار تفشل 403.
- خطر confusion وتشغيل UI لا يطابق الصلاحيات.

الإصلاح المقترح:
- بناء permission matrix واضح.
- إخفاء pages/actions حسب role granular وليس فقط دخول عام للـ `/admin`.
- توحيد roles بين frontend/backend: Assistant, AssistantAcademic, AssistantReviewer.

### P1-7: Surface separation غير كامل رغم وجود services منفصلة

المكان:
- `docker-compose.yml:138-194`
- `frontend/src/proxy.ts:15-69`
- `frontend/src/packages/surface-runtime/config.ts:57-123`

المشكلة:
- landing/student/admin تستخدم نفس صورة frontend ونفس Next app bundle.
- الفصل يعتمد على runtime proxy/redirect، وليس build-time route pruning.
- matcher يستثني `/api` بالكامل، لذلك API routes موجودة على كل surface إن لم يحجبها reverse proxy.

التأثير:
- admin bundle/routes قد تكون قابلة للاكتشاف على student/landing حسب misconfig.
- سطح الهجوم أكبر مما يوحي به فصل الحاويات.

الإصلاح المقترح:
- تطبيق build-time route gating أو separate Next apps/surfaces.
- حجب `/api/worker` وأي admin-only Next routes على student/landing.
- إضافة tests حقيقية لـ landing/student/admin origins وليس static-only فقط.

### P1-8: E2E endpoints تظهر anonymous في inventory وتعتمد على runtime env/token فقط

المكان:
- `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs:12-30`
- `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs:305-319`

المشكلة:
- لا توجد `[Authorize]` أو compile-time exclusion.
- الحماية داخل method عبر `EnvironmentName == "E2e"` و`X-E2E-Token`.

التأثير:
- لو production env misconfigured إلى E2e أو token ضعيف، توجد endpoints destructive مثل seed/clear.

الإصلاح المقترح:
- افصل controller في build profile/testing assembly أو اشترط Development/E2e مع startup registration.
- أضف startup guard يمنع `E2e` في Docker/Production deployments.

### P1-9: AI/video session token/key موجودان في iframe query string

المكان:
- `frontend/src/components/video/SecureVideoPlayer.tsx:445`
- `frontend/src/app/api/video/embed/route.ts:24-49`

المشكلة:
- encrypted token والمفتاح `k` في URL query.
- URL قد يظهر في browser history, logs, reverse proxy logs, Referer في بعض الحالات.

التأثير:
- حماية الفيديو تصبح obfuscation أكثر من كونها secure access control.

الإصلاح المقترح:
- استخدم opaque session id فقط في query.
- اجعل server route يجلب key/token من DB بعد تحقق session/user.
- لا ترسل key للعميل.

## P2 - مشاكل جودة وتشغيل

### P2-1: `beforeunload` يحاول إرسال progress async بطريقة غير موثوقة

المكان:
- `frontend/src/components/video/SecureVideoPlayer.tsx:368-379`

المشكلة:
- `void flushTrackedProgress()` داخل beforeunload لا يضمن اكتمال HTTP request.

التأثير:
- آخر ثواني مشاهدة قد تضيع.

الإصلاح:
- استخدم `navigator.sendBeacon` أو flush دوري أقصر مع server reconciliation.

### P2-2: Session consumed قبل التأكد من تحميل iframe/player

المكان:
- `frontend/src/components/video/SecureVideoPlayer.tsx:438-445`
- `backend/src/NaderGorge.Application/Features/Student/Commands/ConsumeVideoSessionCommand.cs:27-38`

المشكلة:
- يتم استهلاك session قبل أن ينجح iframe/player فعليا.

التأثير:
- network/browser failure بعد consume قد يجبر الطالب على طلب session جديد أو يسبب أخطاء.

الإصلاح:
- consume عند أول `ready` مؤكد من embed، أو اجعل session reusable حتى أول progress event.

### P2-3: `innerHTML = ''` في player مع mutation guard قد يسبب states هشة

المكان:
- `frontend/src/components/video/SecureVideoPlayer.tsx:448`
- `frontend/src/utils/dom-shield.ts:35-65`

المشكلة:
- الكود يمسح DOM يدويا ثم يركب iframe، ومع MutationObserver قد يفسر تغييرات شرعية كتلاعب.

التأثير:
- أخطاء player متقطعة يصعب تشخيصها.

الإصلاح:
- إدارة iframe عبر React state/rendering بدلا من DOM imperative.
- اجعل guard يبدأ بعد اكتمال التركيب.

### P2-4: `postMessage('*')` بدون origin validation

المكان:
- `frontend/src/components/video/SecureVideoPlayer.tsx:169-172`
- `frontend/src/components/video/SecureVideoPlayer.tsx:253-256`

المشكلة:
- listener يقبل أي `event.origin` طالما `msg.source === 'video-embed'`.
- sender يستخدم `'*'`.

التأثير:
- spoofed message من iframe/نافذة غير متوقعة قد يغير state محلي.

الإصلاح:
- تحقق من `event.origin === window.location.origin`.
- استخدم target origin صريح.

### P2-5: Endpoint inventory يظهر internal callbacks كـ anonymous لبعض actions

المكان:
- `tests/endpoint_inventory.md`
- `backend/src/NaderGorge.API/Controllers/InternalController.cs:22-31`

المشكلة:
- الحماية custom header داخل method لا تظهر كـ auth attribute.
- inventory يصنف بعض callbacks anonymous.

التأثير:
- tooling/security reviews قد تفوّت endpoints محمية custom أو تعتبرها مفتوحة.

الإصلاح:
- تحويل internal token validation إلى Authorization policy/filter attribute.

### P2-6: تغطية الاختبارات ضيقة مقارنة بـ 144 endpoint

المكان:
- `backend/tests/NaderGorge.Application.Tests`
- `tests/endpoint_inventory.md`

المشكلة:
- .NET tests الحالية 12 فقط.
- Python API tests موجودة لكن لا تعمل لأن `pytest` غير مثبت.
- لا توجد نتيجة e2e مؤكدة للـ frontend.

التأثير:
- regressions في admin/student workflows لن تظهر في CI المحلي الحالي.

الإصلاح:
- إضافة test setup موثق لـ Python أو نقل API smoke tests إلى .NET integration tests.
- إضافة Playwright smoke لـ login, code redemption, homework, video, admin AI monitor.

### P2-7: Worker logs قد تكشف بيانات أو responses حساسة

المكان:
- `worker/src/jobs/evaluateEssay.ts:61`
- `worker/src/utils/audioExtractor.ts:45`
- `worker/src/services/geminiService.ts:94-176`

المشكلة:
- logging مباشر لـ AI responses/source URLs/job details.

التأثير:
- تسريب بيانات طالب أو محتوى تعليمي أو URLs في logs.

الإصلاح:
- استخدم logger مع redaction.
- لا تطبع AI raw response إلا في debug mode وبقص محدود.

### P2-8: Worker queues تحتفظ بعدد قليل جدا من jobs

المكان:
- `worker/src/index.ts:270-274`
- `worker/src/index.ts:307-310`
- `worker/src/index.ts:338-341`

المشكلة:
- `removeOnComplete` يحتفظ بآخر 10 لمدة ساعة، و`removeOnFail` آخر 5.

التأثير:
- admin monitor قد يرى `not_found` بسرعة بعد completion/failure.
- التحقيق في failures صعب.

الإصلاح:
- احتفظ بسجل job state في PostgreSQL أو زد retention حسب احتياج support.

### P2-9: Redis fallback ports متضاربة بين Docker/native

المكان:
- `backend/src/NaderGorge.API/Program.cs:29-43`
- `backend/src/NaderGorge.Infrastructure/Cache/RedisConnectionFactory.cs:18`
- `worker/src/index.ts:19`

المشكلة:
- fallback محلي يستخدم `localhost:6382` في backend/worker، بينما docker redis داخليا `6379`.
- docker compose يمرر env صحيح، لكن native/dev قد يتعطل لو redis على 6379.

التأثير:
- onboarding محلي هش.

الإصلاح:
- توحيد fallback أو توثيقه في `.env.example` وMakefile.

### P2-10: `JWT_EXPIRY_MINUTES` الافتراضي في docker طويل جدا

المكان:
- `docker-compose.yml:79`

المشكلة:
- الافتراضي `18000000` دقيقة.

التأثير:
- access token شبه دائم، يزيد أثر التسريب.

الإصلاح:
- استخدم 15-60 دقيقة، واعتمد refresh rotation.

### P2-11: Password reset/admin reset validation غير متسقة

المكان:
- `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminResetPasswordCommand.cs:22`
- `backend/src/NaderGorge.Application/Features/Auth/Commands/RegisterCommand.cs:68`

المشكلة:
- register يطلب 8 أحرف، admin reset يقبل 4 أحرف.

التأثير:
- admin قد يضع كلمات مرور أضعف من سياسة التسجيل.

الإصلاح:
- استخراج PasswordPolicy مشتركة.

### P2-12: Frontend services فيها `any` كثير في admin DTOs

المكان:
- `frontend/src/services/admin-service.ts:3`
- `frontend/src/services/admin-service.ts:130-150`
- `frontend/src/services/admin-service.ts:495-504`

المشكلة:
- استخدام `any` يقلل حماية TypeScript على أهم سطح إداري.

التأثير:
- تغييرات backend DTO قد تكسر UI في runtime فقط.

الإصلاح:
- توليد types من OpenAPI أو تشديد DTOs تدريجيا.

## P3 - تحسينات

### P3-1: أسماء student theme تعتمد على admin tokens

المكان:
- `frontend/src/components/layout/StudentShellChrome.tsx:4-15`
- `frontend/src/hooks/useStudentTheme.tsx`

المشكلة:
- الطالب يستخدم `--admin-*` كمصدر tokens.

التأثير:
- صعوبة صيانة الهوية البصرية وفصل الأسطح.

الإصلاح:
- alias tokens: `--surface-*` أو `--student-*` مع mapping داخلي.

### P3-2: UI يستخدم rounded/glass/cards بشكل زائد

المكان:
- صفحات admin/student متعددة، أمثلة: `frontend/src/app/student/code-redemption/packages/[packageId]/page.tsx`

المشكلة:
- مظهر متكرر وcard-heavy، أقل ملاءمة لواجهات تشغيلية كثيفة.

الإصلاح:
- تمريرة normalize/arrange لتقليل nested cards وتحسين density.

### P3-3: بعض comments قديمة أو misleading

المكان:
- `frontend/src/utils/dom-shield.ts:4`
- `frontend/src/components/video/SecureVideoPlayer.tsx:37-46`

المشكلة:
- عبارات مثل "stop 99% of users" و"no YouTube URL" مبالغ فيها.

التأثير:
- توقعات أمان غير دقيقة.

الإصلاح:
- استبدالها بتوصيف threat model حقيقي.

### P3-4: `pytest` requirements غير مفعلة تلقائيا

المكان:
- `tests/requirements.txt`

المشكلة:
- tests موجودة لكن بيئة التشغيل لا تحتوي pytest.

الإصلاح:
- أضف Make target/CI step: `python3 -m pip install -r tests/requirements.txt && python3 -m pytest`.

### P3-5: endpoint inventory generated file مفيد لكنه ليس enforce gate

المكان:
- `scripts/generate-endpoint-inventory.mjs`
- `tests/test_endpoint_inventory.py`

المشكلة:
- inventory موجود، لكن لا يوجد تأكيد أن كل endpoint له frontend service أو test.

الإصلاح:
- أضف coverage matrix: endpoint -> service -> test.

### P3-6: Docker volumes declared external في root compose

المكان:
- `docker-compose.yml:233-239`

المشكلة:
- `external: true` يتطلب إنشاء volumes مسبقا.

التأثير:
- `docker compose up` لأول مرة قد يفشل عند مستخدم جديد.

الإصلاح:
- إما Make target ينشئ volumes، أو إزالة external في local profile.

## مشاكل حسب السطح

### Frontend عام

- Auth state client-only؛ لا يدعم server routes التي تحتاج معرفة المستخدم.
- raw `fetch` مستخدم في AI monitor بدلا من `apiClient`.
- الاعتماد على localStorage واسع: auth, theme, onboarding, exam drafts.
- `postMessage` وiframe player يحتاجان hardening.
- بعض services تستعمل paths قديمة (`homework-service`).

### Admin

- AdminGuard غير كاف كـ permission model.
- AI monitor غالبا مكسور بسبب worker proxy auth.
- أزرار cancel/retry jobs تمر عبر proxy لا يتحقق من Admin role.
- كثافة DTOs بـ `any` عالية.
- بعض العمليات الحساسة مثل reset password لها validation أضعف من register.

### Student

- QR activation لا يعمل مع auth الحالي.
- Homework pending/submit paths خاطئة.
- video watch tracking يمكن تزويره client-side.
- watch limit قد يسمح بمشاهدة إضافية.
- student UI يعتمد على admin design tokens.

### Backend

- Builds/tests سليمة، وهذا إيجابي.
- أكبر المخاطر في concurrency وليس compile errors.
- code redemption, purchase, balance, watch tracking تحتاج transactions/concurrency controls.
- بعض custom auth لا يظهر كسياسات رسمية في inventory.

### Worker

- build ناجح.
- auth الداخلي موجود في worker نفسه، لكن Next proxy قبله هو الحلقة الضعيفة.
- logs تحتاج redaction.
- retention قصير جدا للـ job diagnostics.
- queue loops تعمل infinite loops بدون graceful shutdown واضح.

### Docker/Ops

- فصل surfaces موجود لكنه runtime-level وليس build-level.
- JWT expiry الافتراضي طويل جدا.
- volumes external قد تصعب أول تشغيل.
- أكثر من docker-compose file مع ports مختلفة قد يسبب confusion.

## خطة علاج مقترحة

### المرحلة 1 - إصلاحات حرجة

1. إصلاح worker proxy:
   - تحقق JWT ودور Admin/Teacher في Next route، أو انقل proxy للbackend.
   - عدل raw fetch في admin إلى helper يضيف Authorization.

2. إصلاح QR flow:
   - اختيار strategy واحدة: HttpOnly cookies أو client-side redemption page.

3. إصلاح homework-service:
   - استبدال paths القديمة بالمسارات الفعلية.

4. تقليل JWT expiry الافتراضي:
   - من `18000000` دقيقة إلى قيمة production معقولة.

### المرحلة 2 - سلامة البيانات

1. إضافة transactions/locking لـ:
   - `ActivateCodeCommand`
   - `PurchaseContentCommand`
   - `BalanceService`
   - `TrackWatchProgressCommand`

2. إضافة concurrency tests:
   - redeem نفس الكود بطلبين متزامنين.
   - شراءين بنفس الرصيد.
   - watch progress deltas متضاربة.

3. تعديل watch limit من `>` إلى behavior محدد ومختبر.

### المرحلة 3 - Security hardening

1. نقل refresh tokens إلى HttpOnly cookies.
2. تشديد CSP.
3. إزالة key من video embed query.
4. تحويل internal-token checks إلى authorization filter/policy.
5. مراجعة logs وإخفاء PII/AI content.

### المرحلة 4 - Frontend/Admin/Student quality

1. endpoint-service-test matrix.
2. Playwright smoke flows:
   - login student
   - QR/code redemption
   - homework submit
   - video watch
   - admin AI monitor
   - admin user detail actions
3. a11y/responsive pass على admin tables وstudent player.
4. تقليل `any` في admin-service تدريجيا.

## ملاحظات إيجابية

- المشروع يبني بنجاح في frontend/backend/worker.
- .NET tests الحالية ناجحة.
- يوجد SecurityConfigurationValidator ويتحقق من secrets في non-development.
- يوجد rate limiting لبعض endpoints الحساسة مثل auth/codes/video-session/public forms.
- يوجد endpoint inventory script مفيد كبداية لحوكمة API.
- يوجد محاولة واضحة لفصل surfaces في Docker/proxy.

## الخلاصة

المشكلة ليست أن المشروع "لا يعمل"؛ بالعكس build صحي. المشكلة أن هناك workflows أساسية معرضة للكسر أو race/security bugs في وقت التشغيل: QR, homework, worker monitor, code redemption, balance purchase, and video tracking.

الأولوية العملية: ابدأ بـ P0 الثلاثة، ثم concurrency fixes، ثم hardening auth/video/session، ثم ارفع تغطية الاختبارات حول flows الحقيقية للطالب والأدمن.

## حالة المعالجة - 2026-06-06

مرجع التنفيذ التفصيلي: [083-deep-audit-remediation spec](../specs/083-deep-audit-remediation/spec.md) و[قائمة المهام](../specs/083-deep-audit-remediation/tasks.md).

### تم إصلاحه

- Worker proxy لم يعد يثق في وجود header فقط؛ أصبح يتحقق من `/api/auth/me` ويقبل أدوار `Admin` و`Teacher` فقط، مع helper موحد في الواجهة يرسل bearer token.
- QR flow لم يعد يفعل الأكواد من Next API route server-side؛ route يوجه إلى صفحة redemption client-side تحترم حالة تسجيل الدخول و`returnUrl`.
- Homework service يستخدم المسارات الفعلية `/homework/pending` و`/homework/{homeworkId}/submit`.
- تفعيل الأكواد، شحن/خصم الرصيد، وشراء الباقات أصبحت داخل معاملات serializable مع تحديثات شرطية تمنع double redemption وoverspend.
- مسارات video watch تقبل deltas معقولة فقط، وتغلق الفيديو عند الوصول إلى `MaxWatchCount` بالضبط.
- public video session DTO لم يعد يعيد token/key؛ embed material ينتقل فقط عبر server-side endpoint محمي بـ `InternalTokenAuthorize`.
- رسائل `postMessage` داخل secure player وembed HTML أصبحت تتحقق من origin ولا تستخدم wildcard target للرسائل بين الصفحة والإطار.
- Internal callbacks وE2E endpoints أصبحت تستخدم filters مركزية، والـ endpoint inventory يصنفها `internal-token` و`e2e-token` بدل anonymous.
- JWT expiry الافتراضي في Docker أصبح 60 دقيقة، وvalidator يرفض أكثر من 120 دقيقة خارج development.
- Worker logs أصبحت تخفي URLs والحقول الحساسة والنصوص الطويلة، وتمت إزالة طباعة ردود Gemini الخام وروابط الفيديو الخام.
- BullMQ retention زاد لدعم التشخيص الإداري، وأضيفت Makefile targets للجرد، اختبارات Python، Docker volumes، والتحقق الكامل.
- Admin reset password يستخدم سياسة موحدة بحد أدنى 8 أحرف، وadmin/student UI لمسارات الباقات وطلبات المشاهدة حصلت على DTOs/tokens أوضح.

### مؤجل عمداً

- نقل refresh tokens بالكامل إلى HttpOnly cookies يحتاج migration أوسع لتخزين auth في الواجهة وتدوير الجلسات.
- CSP شامل للواجهات يحتاج حصر مصادر الفيديو وTelegram/VK/YouTube بدقة حتى لا يكسر providers الحالية.
- Playwright smoke suite كامل للطالب والأدمن لم يضف هنا؛ الاعتماد الحالي على Python E2E + build/lint/inventory.
- فصل Docker على مستوى build images لكل surface بقي خارج هذا الإصلاح لأن الخطة الحالية ركزت على runtime safety والبوابات الحرجة.

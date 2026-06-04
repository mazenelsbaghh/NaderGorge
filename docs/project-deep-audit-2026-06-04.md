# تقرير تدقيق عميق لمشروع Nader Gorge

تاريخ التدقيق: 2026-06-04  
النطاق: المشروع كله، Backend + Frontend + Worker + Docker + CI + UI/UX  
ملحوظة: لم أضع أي قيمة سرية فعلية في التقرير. أي مفاتيح أو أسرار مذكورة هنا موصوفة فقط كوجود أو مكان.

## الخلاصة التنفيذية

المشروع يمر من ناحية البناء الأساسي، لكن ليس جاهزا كمنظومة production آمنة. الخطر الأكبر ليس في الشكل، بل في أسطح تحكم داخلية مكشوفة، بيانات حساسة في config، صفحات عامة تكشف بيانات طلاب، XSS محتمل من HTML غير منظم، وCI/DevOps غير متماسك مع stack الحالي.

تقييمي الحالي: **52/100**

- الوظائف الأساسية تبني وتعمل جزئيا.
- جودة الأمان الحالية أقل من المطلوب لمنصة تعليمية فيها بيانات طلاب وأولياء أمور ودفع/رصيد.
- الـ frontend بصريا متحسن في shell العام، لكن فيه خلط بين تجربة الطالب وتجربة الأدمن، mock analytics، بقايا branding قديمة، واعتماد زائد على الحدود/cards خلاف التصميم الموثق.
- الـ worker هو أضعف نقطة تشغيلية: API وBull Board مفتوحين، callbacks تعتمد على secret افتراضي، وlogs قد تكشف payloads وملفات.

## نطاق الفحص

- ملفات المشروع المفهرسة: 1184 ملف.
- صفحات frontend: 42 صفحة، منها 21 صفحة admin و12 صفحة student.
- Backend controllers: 23 controller.
- اختبارات backend الموجودة: 6 ملفات tests.
- تم تحميل سياق التصميم من `PRODUCT.md` و`DESIGN.md` واستخدام عدسة Impeccable للـ UI: distill/adapt/polish.

## نتائج أوامر التحقق

| الأمر | النتيجة |
|---|---|
| `npm run build` داخل `frontend` | نجح، مع تحذير Next.js أن `middleware` أصبح deprecated لصالح `proxy` |
| `npm run lint` داخل `frontend` | نجح بدون errors، لكن 106 warnings |
| `npm run build` داخل `worker` | نجح |
| `dotnet test backend/NaderGorge.sln --no-restore` | نجح: 12 tests، مع warning تعارض EF package versions |
| `npm audit --omit=dev` داخل `frontend` | فشل audit: 16 vulnerabilities، منها 6 high |
| `npm audit --omit=dev` داخل `worker` | فشل audit: 7 vulnerabilities، منها 1 critical |
| `dotnet list package --vulnerable --include-transitive` | لا توجد NuGet vulnerabilities من المصدر الحالي |

## P0: مشاكل حرجة

### P0-1: التطبيق يعمل seeding لحسابات افتراضية بكلمات مرور معروفة على كل البيئات

الأدلة:

- `backend/src/NaderGorge.API/Program.cs:139-143` ينفذ `Seeder.SeedAsync(db)` بدون تقييد بيئة.
- `backend/src/NaderGorge.Infrastructure/Data/Seeder.cs:11` يخرج فقط إذا وجدت roles.
- `backend/src/NaderGorge.Infrastructure/Data/Seeder.cs:22-33` ينشئ admin افتراضي.
- `backend/src/NaderGorge.Infrastructure/Data/Seeder.cs:35-46` ينشئ student افتراضي.

الأثر:

- أي production database جديدة أو ممسوحة ستنشئ حساب admin/student معروفين.
- هذا يفتح باب takeover مباشر لو لم يتم تغييرهم فوريا.

الإصلاح المطلوب:

- منع seeding الافتراضي تماما في `Production` و`Docker`.
- إنشاء أول admin فقط من secret/one-time setup token أو migration آمن.
- حذف أو تدوير الحسابات الافتراضية من أي بيئة حقيقية.
- إضافة test يفشل إذا كان `SeedAsync` يعمل خارج `Development/E2e`.

### P0-2: Worker وinternal callbacks قابلين للتحكم أو التلاعب لو الشبكة/السر الافتراضي اتكشف

الأدلة:

- `backend/src/NaderGorge.API/Controllers/InternalController.cs:21-80` لا يحتوي `[Authorize]` ويعتمد فقط على `X-Internal-Token`.
- `InternalController.cs:26`, `40`, `55`, `70` تستخدم fallback `secretxyz`.
- `docker-compose.yml:64` و`docker-compose.yml:92` يمرران callback secret ب fallback افتراضي.
- `worker/src/index.ts:171-173` يشغل Express مع `cors()` مفتوح.
- `worker/src/index.ts:176-196` يعرض status jobs.
- `worker/src/index.ts:198-213` يسمح بإلغاء jobs.
- `worker/src/index.ts:216-230` يسمح بإعادة jobs.
- `worker/src/index.ts:232-248` يعرض Bull Board على `/ui`.
- `docker-compose.yml:94-95` يفتح worker على host port `3001`.
- `frontend/src/app/api/worker/[...path]/route.ts:14-71` يعمل proxy عام لأي GET/POST/DELETE إلى worker بدون تحقق auth ظاهر.

الأثر:

- مهاجم يستطيع إلغاء AI jobs أو retry jobs لو وصل route.
- callbacks ممكن تزيف نجاح AI analysis أو mindmaps أو essay grading إذا عرف secret أو بقي default.
- Bull Board قد يكشف queue state وjob payloads.

الإصلاح المطلوب:

- منع أي fallback للـ secrets. التطبيق يفشل startup إذا لم توجد secrets قوية.
- جعل worker غير منشور على host في production، ويكون على private Docker network فقط.
- حماية `/ui` و`/api/status` بJWT admin أو mTLS أو service token قوي.
- استخدام HMAC request signing مع timestamp/nonce بدل shared static token.
- غلق `cors()` المفتوح وتحديد origins داخلية فقط.
- منع frontend proxy من DELETE/POST إلا بعد تحقق admin session server-side.

### P0-3: Parent report عام ويكشف بيانات طالب بمجرد معرفة `studentId`

الأدلة:

- `backend/src/NaderGorge.API/Controllers/ParentController.cs:20-27` endpoint anonymous.
- `ParentController.cs:21` يصرح أن الرابط MVP ولا يستخدم token/hash بعد.
- `backend/src/NaderGorge.Application/Features/Reports/Queries/GetParentReportQuery.cs` يجمع تقرير الطالب من `StudentId` فقط.

الأثر:

- GUID ليس authorization.
- أي رابط مسرب يكشف تقرير الطالب لغير ولي الأمر.
- هذا خطر خصوصية مباشر لمنصة طلاب.

الإصلاح المطلوب:

- استخدام signed, expiring parent report token مرتبط بالطالب وولي الأمر والغرض.
- إضافة audit log لكل فتح تقرير.
- عدم قبول `studentId` وحده.
- إتاحة revoke/rotate للروابط.

### P0-4: HTML الخاص بالأسئلة والإجابات يعرض بدون sanitization، وهذا يفتح XSS

الأدلة:

- `frontend/src/components/exams/ExamViewer.tsx:281`, `285`, `289`, `340`, `346`, `354`, `477`, `620`.
- `frontend/src/app/student/mistakes/page.tsx:140`.
- `frontend/src/app/admin/content/exams/[id]/dashboard/page.tsx:146`.
- `frontend/src/app/api/video/embed/route.ts:152` و`408` يحقن `studentName/studentPhone` في `watermark.innerHTML`.

الأثر:

- أي HTML مخزن في سؤال أو اختيار قد ينفذ script/event handlers داخل جلسة الطالب أو الأدمن.
- لأن tokens محفوظة في browser storage، XSS قد يتحول إلى account/session theft.

الإصلاح المطلوب:

- Sanitization server-side عند الحفظ وclient-side قبل العرض باستخدام allowlist واضحة.
- السماح فقط بعناصر rich text محددة مثل `b`, `i`, `u`, `p`, `ul`, `li`, `br`.
- منع event handlers و`javascript:` وiframe/script/style.
- في embed route استخدم `textContent` أو `JSON.stringify` للحقن داخل script، وليس `innerHTML` بنص interpolated.
- إضافة tests لمدخلات مثل `<img onerror=...>`.

### P0-5: `RequireStudent` policy مستخدمة وغير متسجلة

الأدلة:

- `backend/src/NaderGorge.API/Controllers/GamificationController.cs:12` يستخدم `[Authorize(Policy = "RequireStudent")]`.
- `backend/src/NaderGorge.API/Program.cs:84-91` يسجل فقط `RequireAssistantReviewer` و`RequireAcademicAssistant`.

الأثر:

- `/api/Gamification/status` قد يفشل runtime لأن policy غير موجودة.
- هذا يكسر gamification للطلاب، وهي جزء مهم من value loop.

الإصلاح المطلوب:

- إضافة policy:
  `options.AddPolicy("RequireStudent", p => p.RequireRole("Student"));`
- أو استبدالها بـ `[Authorize(Roles = "Student")]`.
- إضافة integration test لهذا endpoint.

## P1: مشاكل عالية الخطورة

### P1-1: تبعيات frontend وworker بها vulnerabilities حديثة

الأدلة من `npm audit --omit=dev`:

- Frontend: 16 vulnerabilities، منها 6 high.
- الحزم المؤثرة تشمل `next`, `axios`, `postcss`, `hono`, `fast-uri`, `path-to-regexp`, `picomatch`.
- `frontend/package.json:19` يستخدم `axios 1.13.6`.
- `frontend/package.json:27` يستخدم `next 16.2.1`.
- Worker: 7 vulnerabilities، منها 1 critical.
- الحزم المؤثرة تشمل `protobufjs`, `lodash`, `bullmq`, `uuid`, `ws`, `qs`.
- `worker/package.json:20` يستخدم `@google/genai`.
- `worker/package.json:23` يستخدم `bullmq`.

الأثر:

- SSRF/DoS/prototype pollution في frontend dependency tree.
- critical protobufjs في worker dependency tree قد يؤثر على AI/GenAI stack.

الإصلاح المطلوب:

- تحديث Next إلى الإصدار الآمن المقترح من audit.
- تحديث Axios إلى الإصدار الآمن المقترح من audit.
- تحديث worker dependencies وتشغيل `npm audit fix` بشكل مدروس.
- بعد التحديث: `npm run build`, `npm run lint`, e2e smoke.

### P1-2: أسرار وبيانات اتصال حساسة داخل ملفات tracked أو defaults ضعيفة

الأدلة:

- `backend/src/NaderGorge.API/appsettings.Development.json:8-9` يحتوي JWT secret.
- `backend/src/NaderGorge.API/appsettings.Development.json:28-30` يحتوي Evolution API config/key.
- `backend/src/NaderGorge.API/appsettings.E2e.json:8-9` يحتوي JWT secret.
- `.env.example:16-17` يستخدم postgres defaults.
- `worker/.env.example:3` يحتوي DB connection string بكلمة مرور default.
- `docker-compose.yml:8-10`, `52-64`, `89-93` فيها defaults أو env passthrough بدون enforcement كاف.

الأثر:

- Development secrets لو اتسربت أو أعيد استخدامها في production تصبح خطر مباشر.
- defaults الضعيفة تتحول بسرعة إلى production misconfiguration.

الإصلاح المطلوب:

- إزالة أي real API keys من الملفات tracked.
- تدوير المفاتيح الموجودة حاليا.
- استخدام `appsettings.Development.example.json` بدون أسرار.
- إضافة secret scanning في CI.
- تشغيل startup validation يفشل إذا كانت secrets مفقودة أو default.

### P1-3: تخزين access/refresh tokens في `localStorage/sessionStorage`

الأدلة:

- `frontend/src/lib/auth-storage.ts:69-71` يحفظ accessToken وrefreshToken وuser في storage.
- `frontend/src/lib/auth-storage.ts:109-123` يقرأ ويبدل tokens من storage.
- `frontend/src/services/api-client.ts:22-28` يضيف bearer token من storage.

الأثر:

- أي XSS في الأسئلة أو الواجهة يستطيع قراءة refresh token.
- refresh token طويل العمر، وهذا يرفع أثر XSS من مشكلة frontend إلى اختراق حساب.

الإصلاح المطلوب:

- نقل refresh token إلى HttpOnly Secure SameSite cookie.
- إبقاء access token قصير العمر في memory فقط إن أمكن.
- فرض CSP صارمة لتقليل XSS blast radius.
- revoke refresh tokens بعد reset password أو device disconnect.

### P1-4: Password reset يعتمد على بيانات شخصية ثابتة وJWT stateless

الأدلة:

- `backend/src/NaderGorge.Application/Features/Auth/Commands/VerifyResetFieldsCommand.cs:50-82` يتحقق من phone + DOB + governorate + district.
- `VerifyResetFieldsCommand.cs:84-89` يصدر JWT reset token لمدة 10 دقائق.
- `backend/src/NaderGorge.Application/Features/Auth/Commands/ResetPasswordCommand.cs:41-63` يغير كلمة المرور ولا يبطل refresh tokens القديمة.

الأثر:

- البيانات المطلوبة قد تكون معروفة أو قابلة للتخمين.
- reset token غير one-time ولا يوجد سجل revocation.
- بعد تغيير كلمة المرور، الجلسات القديمة قد تظل فعالة عبر refresh tokens.

الإصلاح المطلوب:

- reset token مخزن server-side، one-time، hashed، ومع attempts per account.
- إرسال reset عبر قناة يملكها الطالب/ولي الأمر، وليس مجرد مطابقة بيانات.
- عند reset password: revoke كل refresh tokens للطالب.

### P1-5: E2E controller يمكنه حذف وإعادة إنشاء database لو البيئة أخطأت

الأدلة:

- `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs:11-13` route عام `api/e2e` بدون auth.
- `E2eTestingController.cs:27-33` يحذف ويعيد إنشاء DB إذا `ASPNETCORE_ENVIRONMENT == "E2e"`.
- `E2eTestingController.cs:45-55`, `75-95` ينشئ users بكلمة مرور `password`.

الأثر:

- مجرد تشغيل production أو staging ببيئة `E2e` بالخطأ يجعل endpoint مدمر ومفتوح.

الإصلاح المطلوب:

- عدم تسجيل controller من الأساس إلا في E2E build/profile.
- إضافة secret خاص للاختبارات حتى داخل E2e.
- منع `EnsureDeleted` في أي environment غير test container.

### P1-6: CI الحالي غير متوافق مع stack الفعلي وقد يعطي ثقة كاذبة

الأدلة:

- `.github/workflows/e2e-tests.yml:41-44` يثبت .NET `8.0.x` بينما backend targets `net9.0`.
- `frontend/package.json:6` يشغل Next على port `8738`.
- `.github/workflows/e2e-tests.yml:60-67` يشغل frontend ثم ينتظر `http://localhost:3000`.
- workflow لا يشغل worker رغم أن admin AI monitor وvideo processing يعتمدان عليه.

الأثر:

- E2E قد يفشل قبل الاختبارات أو ينتظر port خطأ.
- لو تم تعطيله أو تجاهله، production deploy قد يحدث بدون تحقق حقيقي.

الإصلاح المطلوب:

- استخدام `dotnet-version: 9.0.x`.
- انتظار `http://localhost:8738`.
- تشغيل worker أو mock واضح للـ worker endpoints.
- جعل build/lint/test gates إجبارية قبل deploy.

### P1-7: Docker وdeployment يفتحون خدمات داخلية أو يستخدمون defaults خطيرة

الأدلة:

- `docker-compose.yml:8-10` Postgres defaults.
- `docker-compose.yml:11-12` يفتح Postgres على host.
- `docker-compose.yml:29-30` يفتح Redis على host.
- `docker-compose.yml:94-95` يفتح worker/Bull Board على host.
- `.github/workflows/deploy.yml:25-33` يعمل build/restart/prune بدون health rollback أو migration safety gates.
- `Makefile:192-208` deploy يعمل `git add .`, commit, checkout main, merge, push.

الأثر:

- تعرض Redis/Postgres/worker يزيد سطح الهجوم.
- deploy target قد يدفع ملفات غير مقصودة إلى main.
- لا يوجد rollback تلقائي لو migration أو service health فشل بعد restart.

الإصلاح المطلوب:

- عدم نشر DB/Redis/worker ports في production.
- فصل compose dev عن production.
- إلغاء Makefile deploy أو جعله dry-run/explicit branch فقط.
- deploy pipeline: backup، migrate، health check، rollback.

### P1-8: AI processing locking غير ذري

الأدلة:

- `backend/src/NaderGorge.Application/Features/Admin/Commands/AnalyzeVideoAICommand.cs:24-40` يقرأ الفيديو، يفحص `IsProcessingAI`، ثم يغيره ويحفظ.
- `backend/src/NaderGorge.Application/Features/Admin/Commands/MindmapOps/GenerateChapterMindmapsCommand.cs` يستخدم نمط مشابه لـ `IsProcessingMindmaps`.

الأثر:

- طلبان متزامنان قد يريان `false` ثم يضعان jobs مكررة.
- job state في UI قد يتعارض مع Redis/BullMQ.

الإصلاح المطلوب:

- update ذري في DB: `WHERE Id = @id AND IsProcessingAI = false`.
- unique job IDs في BullMQ موجودة جزئيا، لكن DB lock يجب أن يكون المصدر الحاسم.
- اختبار concurrency.

### P1-9: تغيير أدوار المستخدمين قد يزيل كل الأدوار أو آخر admin

الأدلة:

- `backend/src/NaderGorge.Application/Features/Admin/Commands/UpdateUserRoleCommand.cs:30-36` إذا `request.Roles` فارغة، يسمح بـ `rolesToAssign` فارغة.
- `UpdateUserRoleCommand.cs:40-51` يحذف كل الأدوار ثم يضيف المطلوب.
- لا يوجد guard يمنع self-demotion أو إزالة آخر admin.

الأثر:

- يمكن قفل الإدارة من داخل النظام.
- يمكن إنشاء مستخدم بلا role، وهذا قد يسبب سلوك غير واضح في auth/UI.

الإصلاح المطلوب:

- منع roles الفارغة إلا لو use case موثق.
- منع إزالة آخر admin.
- منع self-demotion بدون admin آخر مؤكد.
- إضافة transaction.

### P1-10: تعديل رصيد الطالب غير محمي من race conditions ولا transaction كاملة

الأدلة:

- `backend/src/NaderGorge.Application/Features/Admin/Commands/AdjustBalanceCommand.cs:19-32` ينشئ balance ويحفظ قبل العملية الأساسية.
- `AdjustBalanceCommand.cs:34-43` يضيف amount مباشرة.
- `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs:652-668` يضبط decimal فقط، بدون concurrency token.

الأثر:

- تعديلات متزامنة قد تضيع updates.
- قد ينتج balance سلبي أو adjustment غير مبرر لو لم توجد validation على amount/reason.

الإصلاح المطلوب:

- transaction واحدة.
- optimistic concurrency token أو SQL atomic update.
- validation: reason required، amount ضمن حدود، منع negative balance إلا بسياسة واضحة.

## P2: مشاكل متوسطة ومهمة

### P2-1: تعارض package versions في .NET

الأدلة:

- `dotnet test` نجح لكن أظهر MSB3277 conflict بين `Microsoft.EntityFrameworkCore.Relational` 9.0.1 و9.0.6.
- `backend/src/NaderGorge.API/NaderGorge.API.csproj:15` يستخدم `Microsoft.Extensions.Caching.StackExchangeRedis 10.0.5` داخل net9 project.
- `backend/src/NaderGorge.Infrastructure/NaderGorge.Infrastructure.csproj:10-19` يعتمد EF/Npgsql packages بإصدارات مختلفة في السلسلة.

الأثر:

- runtime behavior قد يختلف بين dev/CI/prod.
- package 10.x في net9 مشروع قد يدخل transitive versions غير متوقعة.

الإصلاح المطلوب:

- توحيد كل Microsoft.Extensions/EF packages على عائلة واحدة متوافقة مع .NET 9.
- استخدام central package management.
- تشغيل `dotnet restore` و`dotnet test` بدون warnings.

### P2-2: Redis configuration غير موحدة

الأدلة:

- `backend/src/NaderGorge.API/Program.cs:27-35` يستخدم `GetConnectionString("Redis")`.
- appsettings التي تمت مراجعتها تستخدم مفاتيح أخرى مثل Redis/ConnectionString أو Docker env `ConnectionStrings__Redis`.

الأثر:

- Redis cache قد لا يعمل في native dev إذا لم تكن `ConnectionStrings:Redis` موجودة.
- `ConnectionMultiplexer` fallback على `localhost:6382` يخفي misconfiguration.

الإصلاح المطلوب:

- توحيد المفتاح: `ConnectionStrings:Redis`.
- فشل startup إذا Redis مطلوب وغير مضبوط.
- logging واضح إذا cache disabled.

### P2-3: rate limiting يعتمد على IP خام ولا يستخدم forwarded headers

الأدلة:

- `backend/src/NaderGorge.API/Configuration/RateLimitingConfig.cs:16-24`, `27-35`, `38-46`, `49-57`.
- `backend/src/NaderGorge.API/Program.cs:122-137` لا يستخدم `UseForwardedHeaders`.

الأثر:

- خلف reverse proxy قد يرى التطبيق IP واحد لكل المستخدمين أو IP غير صحيح.
- rate limiting إما يضرب مستخدمين أبرياء أو لا يحمي endpoints كما يجب.

الإصلاح المطلوب:

- إعداد `ForwardedHeadersOptions` بثقة proxy محددة.
- استخدام user id حيث ممكن.
- إضافة rate limits لـ WhatsApp/public forms/parent report.

### P2-4: لا توجد security headers عامة كافية

الأدلة:

- البحث وجد CSP/X-Frame-Options فقط في `frontend/src/app/api/video/embed/route.ts:79-80`.
- لا يوجد `UseHttpsRedirection`, `Hsts`, أو middleware عام للـ security headers في `Program.cs`.

الأثر:

- المنصة لا تضع دفاعات قياسية ضد clickjacking/XSS downgrade/mixed content على كل routes.

الإصلاح المطلوب:

- HSTS وHTTPS redirection في production.
- CSP مناسبة للـ frontend.
- X-Frame-Options/Frame-ancestors لكل pages الحساسة.
- Secure cookies عند نقل refresh token.

### P2-5: Public WhatsApp check قابل للاستغلال والتعداد

الأدلة:

- `backend/src/NaderGorge.API/Controllers/WhatsAppController.cs:21-44` endpoint عام يفحص رقم WhatsApp.
- لا يوجد `[EnableRateLimiting]` مخصص هنا.

الأثر:

- enumeration لأرقام الطلاب.
- استهلاك quota أو إساءة استخدام Evolution API.

الإصلاح المطلوب:

- rate limit قوي per IP/per phone hash.
- captcha أو proof-of-work خفيف في public registration.
- response لا يكشف معلومات أكثر من اللازم.

### P2-6: Public forms معرضة للسبام وpayloads كبيرة

الأدلة:

- `backend/src/NaderGorge.API/Controllers/PublicFormsController.cs:11-37` anonymous GET/POST.
- POST يقبل `Dictionary<string,string>` بدون حد ظاهر للحجم أو rate-limit/captcha.

الأثر:

- spam submissions.
- تحميل DB ببيانات كبيرة أو حقول كثيرة.

الإصلاح المطلوب:

- body size limit.
- validation حسب schema المخزن للform.
- rate limit/captcha للـ submit.

### P2-7: Access code generation في worker legacy يخزن الكود plaintext والـ hash بنفس القيمة

الأدلة:

- `worker/src/index.ts:43-52` يضيف `CodeHash` و`CodePlaintext` بنفس `code`.
- `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs:201-209` فقط يضع unique index على `CodeHash`.

الأثر:

- إذا تسربت قاعدة البيانات، الأكواد قابلة للاستخدام مباشرة.

الإصلاح المطلوب:

- تخزين hash قوي فقط للاستهلاك.
- plaintext يظهر مرة واحدة عند التصدير فقط أو يحفظ encrypted-at-rest بمفتاح خارجي إن كان ضروريا.

### P2-8: worker يكتب ملفات في مسار source للـ backend بدلا من storage مشترك مضمون

الأدلة:

- `worker/src/jobs/analyzeVideoChapters.ts:52-63` يكتب SRT في `../backend/src/NaderGorge.API/wwwroot/subtitles`.

الأثر:

- داخل Docker، worker container قد لا يشارك هذا المسار مع backend.
- ملفات subtitle قد تضيع عند rebuild أو لا تظهر للـ API.

الإصلاح المطلوب:

- volume مشترك أو S3/local bucket واضح.
- config `SUBTITLE_STORAGE_PATH` و`PUBLIC_SUBTITLE_BASE_URL`.
- health check للتأكد من أن backend يرى الملف.

### P2-9: logs في worker تكشف payloads ومعلومات حساسة

الأدلة:

- `worker/src/index.ts:281-285` يطبع raw Redis payloads.
- `worker/src/services/geminiService.ts` يطبع File API URI ومراحل Gemini.
- `worker/src/jobs/notification-sender.ts:10-11` يطبع StudentId ومحتوى الرسالة.
- `worker/src/scripts/birthday-congratulator.ts:156-168` يطبع أسماء/أرقام سياقية للطلاب.

الأثر:

- logs قد تحتوي بيانات طلاب أو روابط ملفات أو payloads jobs.

الإصلاح المطلوب:

- structured logging مع redaction.
- إزالة raw payload logs.
- log correlation ids وليس بيانات شخصية.

### P2-10: admin analytics تعرض mock/random data في صفحات حقيقية

الأدلة:

- `frontend/src/components/admin/EntityOverviewDashboard.tsx:29-65` يولد أرقام ثابتة/توضيحية.
- `EntityOverviewDashboard.tsx:114-118` يظهر تنبيه mock، لكنه ما زال داخل صفحات الإدارة.
- `frontend/src/app/admin/content/packages/[id]/page.tsx:161-165`, `sections/[id]/page.tsx:105-110`, `lessons/[id]/page.tsx:97-102`, `terms/[id]/page.tsx:104-109` تمرر `mockStats={true}`.
- `frontend/src/components/admin/AttachedExamViewer.tsx:98-102` يستخدم `Math.random()` لإحصائيات الأسئلة.

الأثر:

- الأدمن قد يثق في أرقام غير حقيقية.
- الأرقام تتغير عند refresh، فتقل الثقة في النظام.

الإصلاح المطلوب:

- إخفاء mock analytics خلف feature flag dev فقط.
- أو استبدالها بـ empty state صريح: "لا توجد بيانات تحليلية بعد".

### P2-11: fallback API URLs والـ domains فيها drift

الأدلة:

- `frontend/src/services/api-client.ts:11-16` fallback إلى `http://localhost:5000/api` بينما backend المستخدم في Makefile/Docker هو `5245`.
- `frontend/src/middleware.ts:11-25` يحتوي `bsma-academy.com`.
- `frontend/src/app/api/video/embed/route.ts:56`, `152`, `408` يحتوي `basma-acadmy`.

الأثر:

- dev بدون env يفشل أو يضرب port غلط.
- بقايا brand قد تظهر للطالب وتضعف ثقة المنتج.

الإصلاح المطلوب:

- توحيد env defaults على `5245`.
- نقل domains إلى env.
- إزالة `basma/bsma` من الكود واستبدالها بـ Nader George Academy أو token مركزي.

### P2-12: Next middleware deprecated ومبني على إخفاء route لا حماية حقيقية

الأدلة:

- `frontend/src/middleware.ts` موجود وbuild يحذر أن convention deprecated لصالح `proxy`.
- `middleware.ts:25-32` يخفي `/admin` على main domain بالrewrite إلى `/_not-found`.

الأثر:

- قد ينكسر مع تحديثات Next.
- إخفاء admin route ليس security boundary.

الإصلاح المطلوب:

- الانتقال إلى `proxy` حسب Next 16.
- إبقاء حماية admin في backend JWT roles، وليس domain hiding.
- إضافة tests للـ host routing.

### P2-13: Frontend lint warnings كثيرة وتشير إلى ديون فعلية

الأدلة:

- `npm run lint`: 106 warnings، 0 errors.
- أمثلة: unused imports، missing hook dependencies، `<img>` بدل `next/image`.
- `frontend/src/app/admin/users/[id]/page.tsx:61` missing dependency.
- `frontend/src/components/ui/animated-theme-toggler.tsx:122` missing dependency.
- `frontend/src/components/video/PlayerControls.tsx:85-92` dependency instability.

الأثر:

- missing dependencies قد تسبب stale UI أو polling ناقص.
- unused code يزيد noise ويخفي مشاكل حقيقية.

الإصلاح المطلوب:

- خفض warnings إلى 0 تدريجيا.
- جعل CI يفشل على warnings بعد تنظيف أولي.

### P2-14: UI/UX لا يلتزم بالكامل بسياق التصميم الموثق

الأدلة:

- `PRODUCT.md` يحدد أن الطلاب mobile-first ويدخلون "يخلصوا اللي عليهم ويمشوا".
- `frontend/src/app/student/layout.tsx:3-9` يصرح أن Student Layout يطابق AdminShellChrome.
- `frontend/src/components/layout/StudentShellChrome.tsx:3-16` يؤكد أن student shell "mirrors AdminShellChrome exactly".
- `StudentShellChrome.tsx:142-151` يضيف خلفيات/دوائر/زخرفة عامة.
- `frontend/src/components/layout/StudentShellChrome.tsx:146-147` يستخدم shapes زخرفية عامة.
- `frontend/src/components/layout/StudentShellChrome.tsx:246-251` يعتمد مساحة shell ثابتة أكثر قربا من dashboard.
- `frontend/src/components/layout/AdminShellChrome.tsx:250-258` يستخدم bottom nav border/cards كثيرة.
- `frontend/src/app/globals.css:362`, `375`, `448`, `515`, `549`, `579` فيها `border: 1px solid...` رغم أن `DESIGN.md` يمنع الاعتماد على 1px borders كفصل رئيسي.

الأثر:

- تجربة الطالب تبدو كنسخة admin مصغرة، وليست مسار دراسة سريع وموجه.
- كثرة borders/cards تقلل premium feel الموثق.
- الزخرفة موجودة لكنها ليست مرتبطة بما يكفي بالهوية المصرية الحديثة، وتبدو أحيانا كnoise عام.

الإصلاح المطلوب:

- فصل student shell عن admin shell وظيفيا: student = progress path + next task + minimal nav.
- الحفاظ على الخلفية كما طلبت، لكن تحويل الزخرفة إلى motif موحد وخفيف بدلا من دوائر/مربعات عامة.
- تقليل borders لصالح tonal surfaces والspacing.
- عمل pass distill ثم adapt ثم polish على صفحات: `/student`, `/student/packages`, `/student/lessons/[id]`, `/admin/users/[id]`, `/admin/ai-monitor`.

### P2-15: صفحة/مكون secure video فيه مؤشرات unfinished state

الأدلة:

- `frontend/src/components/video/SecureVideoPlayer.tsx` به warnings عن `qualityLevels`, `currentQuality`, `handleQualityChange`, `onEnded` غير مستخدمة.
- `frontend/src/app/api/video/embed/route.ts:79-80` يضع headers route-specific فقط.
- `frontend/src/app/api/video/embed/route.ts:152`, `408` يستخدم watermark `innerHTML`.

الأثر:

- controls قد تبدو موجودة في التصميم لكنها لا تعمل.
- anti-download protection لا ينبغي أن يعطي إحساس أمان كاذب إذا لم يقفل XSS/session theft.

الإصلاح المطلوب:

- إما توصيل quality controls بالكامل أو حذفها.
- تنظيف secure player warnings.
- harden embed script ضد injection.

### P2-16: `Guid.Empty` fallback في controllers يضعف audit integrity

الأدلة:

- `backend/src/NaderGorge.API/Controllers/AdminController.cs:21`.
- `backend/src/NaderGorge.API/Controllers/AdminCommunityController.cs:22`.
- `backend/src/NaderGorge.API/Controllers/BalanceController.cs:20`.
- `backend/src/NaderGorge.API/Controllers/HomeworkController.cs:19`.
- `backend/src/NaderGorge.API/Controllers/StudentController.cs:27`.

الأثر:

- إذا claim ناقص لأي سبب، بعض الأوامر قد تسجل `Guid.Empty` بدلا من fail fast.

الإصلاح المطلوب:

- helper مشترك يرجع Unauthorized/throws إذا claim ناقص.
- عدم استخدام `Guid.Empty` كactor.

### P2-17: technical debt في PostgreSQL timestamp behavior

الأدلة:

- `backend/src/NaderGorge.API/Program.cs:21` يستخدم `Npgsql.EnableLegacyTimestampBehavior`.

الأثر:

- يخفي مشاكل timezone ويؤجل ترحيل مهم.

الإصلاح المطلوب:

- توحيد DateTime handling على UTC.
- إزالة legacy switch بعد migration/test.

## P3: مشاكل أقل أولوية لكنها مؤثرة

### P3-1: build artifacts غير tracked، لكن موجودة محليا

التحقق:

- `git ls-files | rg '(^|/)(node_modules|\\.next|dist|bin|obj)(/|$)'` رجع 0 tracked files.

الملاحظة:

- هذا جيد. لكن وجود `bin/obj/.next/dist` محليا قد يربك البحث اليدوي. تأكد أن `.gitignore` يستمر في منعهم.

### P3-2: كثرة مكونات UI عامة قد تزيد bundle والضوضاء

أمثلة:

- `frontend/src/components/ui/circular-gallery.tsx`
- `frontend/src/components/ui/feature-carousel.tsx`
- `frontend/src/components/ui/ripple-grid.tsx`
- `frontend/src/components/ui/resizable-navbar.tsx`

الأثر:

- لو دخلت في صفحات الطلاب بدون lazy loading، ستؤثر على الأداء.

الإصلاح المطلوب:

- قياس bundle analyzer.
- lazy load للزخرفة الثقيلة.
- حذف غير المستخدم.

### P3-3: UX copy مختلط بين عربي رسمي وكلام عام/تقني

الأمثلة:

- `تحليل AI` في admin navigation.
- بعض رسائل worker/progress طويلة وتقنية.
- وجود أسماء brand قديمة `basma-acadmy`.

الإصلاح المطلوب:

- توحيد glossary عربي: ذكاء اصطناعي، تحليل الفيديو، خرائط ذهنية.
- إزالة old brand strings.

## أولويات الإصلاح المقترحة

### خلال 24 ساعة

1. إيقاف default seeding في production/Docker وتدوير أي credentials.
2. إزالة `secretxyz` fallbacks وفشل startup عند missing callback secrets.
3. إغلاق worker port/Bull Board في production أو حمايتهما.
4. إضافة `RequireStudent` policy.
5. Sanitization عاجل لكل HTML للأسئلة والإجابات.
6. إزالة/تدوير Evolution/JWT secrets الموجودة في config.

### خلال أسبوع

1. تحديث Next/Axios/worker vulnerable dependencies.
2. إصلاح CI: .NET 9، port 8738، worker/mocks، build gates.
3. نقل refresh token إلى HttpOnly cookie أو على الأقل تقليل أثر XSS.
4. harden parent reports بروابط موقعة منتهية الصلاحية.
5. إضافة auth/rate limits لـ WhatsApp/public forms/worker proxy.
6. إصلاح password reset ليكون one-time server-side ويرفض refresh tokens القديمة.

### خلال شهر

1. فصل student shell عن admin shell.
2. إزالة mock analytics أو ربطها ببيانات حقيقية.
3. تنظيف lint warnings إلى 0.
4. إضافة integration/e2e tests للـ critical workflows: login, reset, admin roles, parent report, AI job cancel/retry.
5. توحيد design tokens وتقليل borders/cards.
6. storage واضح لملفات subtitles/mindmaps.

## ملاحظات خاصة بالطالب والأدمن

### الطالب

- المسار الحالي أقرب إلى app shell عام، وليس "ادخل أخلص اللي عليا وأمشي".
- أهم صفحات يجب إعادة تقييمها UX: `/student`, `/student/packages`, `/student/lessons/[lessonId]`, `/student/exams/[examId]`, `/student/mistakes`.
- يجب أن تكون الأولوية: next task، progress، locked/unlocked state، واجبات/امتحانات مطلوبة، وليس dashboard زخرفي.
- الخلفية يمكن تركها، لكن الزخرفة تحتاج لغة واحدة ثابتة: motif مصري خفيف، لا دوائر ومربعات عامة.

### الأدمن

- admin shell أقوى من الطالب، لكنه يعرض أرقام mock وهذا خطر ثقة.
- صفحات content details تستخدم overview جذاب لكنه غير حقيقي.
- AI monitor يتعامل مباشرة مع worker proxy، وهذا يجب أن يكون server-authenticated.
- user detail page بها missing hook deps و`Math.random` row key، وهذا قد يسبب re-render غير مستقر.

## الخاتمة

المشروع ليس سيئا من حيث الحجم أو ambition، لكنه محتاج hardening حقيقي قبل production. ترتيب المخاطر واضح: أمان وsecrets وworker/callbacks أولا، ثم XSS/token storage، ثم CI/dependencies، ثم UI consistency والتجربة الموجهة للطالب.

أهم قرار معماري الآن: لا تعتبر الـ frontend أو middleware أو hidden routes كحدود حماية. حدود الحماية يجب أن تكون في backend/worker/service-to-service auth، مع secrets قوية، no defaults، وسجلات لا تكشف بيانات.

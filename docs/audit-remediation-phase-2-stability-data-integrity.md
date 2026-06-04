# Phase 2: Stability, Data Integrity, CI, and Operations

تاريخ الإنشاء: 2026-06-04  
المصدر: `docs/project-deep-audit-2026-06-04.md`  
الأولوية: عالية إلى متوسطة  
الهدف: جعل النظام أكثر استقرارا وتشغيلا، إصلاح race conditions، توحيد dependency/config، وتقليل مخاطر deploy وCI.

## تعليمات تنفيذ عامة للموديل المنفذ

- نفذ هذه المرحلة بعد Phase 1 أو بالتوازي فقط لو لا يوجد تعارض.
- لا تغير behavior business حساس بدون tests.
- أي إصلاح race condition يجب أن يكون ذري في DB أو داخل transaction واضحة.
- أي تغيير dependencies يجب أن يتبعه build/test.
- لا تجعل CI "ينجح" بتعطيل خطوات مهمة.

## نطاق المرحلة

هذه المرحلة تغطي البنود التالية من التدقيق:

- P1-1: تحديث dependencies الضعيفة في frontend/worker.
- P1-6: إصلاح CI ليتوافق مع stack الحالي.
- P1-7: Docker/deployment hardening.
- P1-8: AI processing locking غير ذري.
- P1-9: حماية تغيير أدوار المستخدمين.
- P1-10: حماية تعديلات رصيد الطلاب.
- P2-1: توحيد .NET package versions.
- P2-2: توحيد Redis configuration.
- P2-3: rate limiting وforwarded headers.
- P2-4: security headers العامة.
- P2-5/P2-6: حماية WhatsApp/public forms.
- P2-7: access code plaintext/hash issue.
- P2-8: storage واضح لملفات subtitles.
- P2-9: redacted worker logs.
- P2-11: API URLs/domain drift.
- P2-12: Next middleware deprecated.
- P2-16: إزالة `Guid.Empty` actor fallback.
- P2-17: PostgreSQL timestamp behavior.

## Task 1: تحديث dependencies ذات vulnerabilities

### المشكلة

`npm audit --omit=dev` أظهر vulnerabilities في frontend والworker، منها high وcritical. هذا يرفع خطر SSRF/DoS/prototype pollution ومشاكل داخل dependency tree.

### ملفات يجب مراجعتها

- `frontend/package.json`
- `frontend/package-lock.json` أو lockfile المستخدم.
- `worker/package.json`
- `worker/package-lock.json` أو lockfile المستخدم.

### خطوات التنفيذ

1. داخل `frontend` شغل:
   - `npm audit --omit=dev`
2. اقرأ الحزم المقترحة للإصلاح.
3. حدث الحزم المباشرة أولا، خصوصا:
   - `next`
   - `axios`
   - أي package تظهر كdirect dependency.
4. لا تستخدم `npm audit fix --force` بدون فهم لأنه قد يكسر Next/React.
5. داخل `worker` كرر نفس العملية.
6. بعد التحديث شغل:
   - frontend build/lint
   - worker build
7. لو بقيت vulnerabilities transitive لا يمكن إصلاحها بدون major breaking change، وثقها في ملف follow-up مع سبب واضح.

### اختبارات القبول

- `frontend npm run build` ينجح.
- `frontend npm run lint` لا يزيد warnings.
- `worker npm run build` ينجح.
- `npm audit --omit=dev` يتحسن بوضوح أو يصل إلى 0 known actionable vulnerabilities.

### أخطاء ممنوعة

- ممنوع تحديث Next/React major بدون قراءة breaking changes.
- ممنوع حذف lockfile وإعادة توليده بلا داع.
- ممنوع تجاهل critical vulnerability بدون توثيق سبب.

## Task 2: إصلاح CI ليتوافق مع المشروع

### المشكلة

CI يستخدم .NET 8 رغم أن backend يستهدف .NET 9، وينتظر frontend على port 3000 بينما المشروع يشغل Next على 8738. كما أن worker غير داخل E2E flow رغم اعتمادات AI/video.

### ملفات يجب مراجعتها

- `.github/workflows/e2e-tests.yml`
- `.github/workflows/deploy.yml`
- `frontend/package.json`
- `worker/package.json`
- `backend/NaderGorge.sln`

### خطوات التنفيذ

1. غيّر setup .NET إلى `9.0.x`.
2. اجعل CI ينتظر frontend على `http://localhost:8738`.
3. تأكد أن backend port في CI مطابق للـ config.
4. قرر بوضوح ماذا يحدث مع worker:
   - تشغيل worker في CI إذا كانت الاختبارات تحتاجه.
   - أو mock endpoints صريحة إن كان E2E لا يختبر AI.
5. اجعل build/lint/test gates واضحة:
   - backend tests
   - frontend build
   - frontend lint
   - worker build
6. لا تجعل الخطوات critical تعمل بـ `continue-on-error`.

### اختبارات القبول

- workflow يستخدم .NET 9.
- workflow ينتظر port الصحيح.
- workflow يفشل عند build/test failure.
- لا توجد خطوة وهمية تعطي ثقة كاذبة.

## Task 3: Docker وdeployment hardening

### المشكلة

compose يفتح Postgres/Redis/worker ports على host، وdeploy workflow/Makefile لا يحتوي safety gates كافية.

### ملفات يجب مراجعتها

- `docker-compose.yml`
- أي `docker-compose.override.yml` أو production compose إن وجد.
- `.github/workflows/deploy.yml`
- `Makefile`

### خطوات التنفيذ

1. افصل dev عن production:
   - dev compose يمكنه نشر DB/Redis ports محليا.
   - production compose لا ينشر DB/Redis/worker ports على host.
2. للـ production:
   - اجعل DB/Redis داخل private network.
   - worker لا يفتح `/ui` للعامة.
   - أضف healthchecks للخدمات المهمة.
3. راجع deploy:
   - backup قبل migrations.
   - migrate step واضح.
   - health check بعد restart.
   - rollback أو على الأقل stop عند health failure.
4. راجع Makefile deploy:
   - احذف أو عطل أي أمر يعمل `git add .` وcommit/merge/push تلقائيا.
   - اجعل deploy explicit ولا يغير git state بدون تأكيد.

### اختبارات القبول

- production compose لا يفتح Postgres/Redis/worker ports.
- deploy لا يدفع ملفات غير مقصودة إلى main.
- يوجد health check بعد deploy.

### أخطاء ممنوعة

- ممنوع كسر dev compose بدون توفير بديل.
- ممنوع جعل Redis/Postgres public في production.

## Task 4: جعل AI processing locking ذري

### المشكلة

أوامر AI analysis/mindmaps تقرأ `IsProcessingAI` أو `IsProcessingMindmaps` ثم تحفظ. طلبان متزامنان قد ينشئان jobs مكررة.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.Application/Features/Admin/Commands/AnalyzeVideoAICommand.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Commands/MindmapOps/GenerateChapterMindmapsCommand.cs`
- DbContext/repository methods.
- worker job id logic.

### خطوات التنفيذ

1. لا تستخدم read-then-write للحجز.
2. أضف method ذرية مثل:
   - `UPDATE LessonVideos SET IsProcessingAI = true WHERE Id = @id AND IsProcessingAI = false`
3. تحقق من عدد الصفوف المتأثرة:
   - 1 = lock acquired.
   - 0 = يوجد processing قائم أو الفيديو غير موجود.
4. بعد enqueue job بنجاح، احتفظ بالحالة.
5. إذا فشل enqueue، أعد flag إلى false داخل transaction أو compensation واضح.
6. استخدم unique BullMQ job IDs كطبقة إضافية، لكن DB lock هو المصدر الأساسي.
7. كرر نفس النمط للـ mindmaps.

### اختبارات القبول

- اختبار concurrency يرسل طلبين متزامنين، job واحد فقط يتم إنشاؤه.
- UI يعرض "processing" مرة واحدة.
- فشل enqueue لا يترك الفيديو stuck في processing.

### أخطاء ممنوعة

- ممنوع lock في memory فقط لأن API قد يعمل بأكثر من instance.
- ممنوع الاعتماد على BullMQ unique id فقط مع DB state غير متسقة.

## Task 5: حماية تغيير أدوار المستخدمين

### المشكلة

`UpdateUserRoleCommand` يسمح بأدوار فارغة ويحذف كل الأدوار ثم يضيف المطلوب، ولا يمنع إزالة آخر admin أو self-demotion.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.Application/Features/Admin/Commands/UpdateUserRoleCommand.cs`
- user/role entities.
- admin users controller.
- tests للـ admin/auth.

### خطوات التنفيذ

1. ارفض `Roles` الفارغة إلا لو يوجد use case موثق.
2. تحقق أن كل role مطلوب معروف ومسموح.
3. قبل إزالة admin role:
   - احسب عدد admins الحاليين.
   - امنع إزالة آخر admin.
4. امنع self-demotion إذا كان سيترك النظام بلا admin قادر.
5. نفذ إزالة/إضافة roles داخل transaction.
6. أضف audit log لتغيير الأدوار.

### اختبارات القبول

- لا يمكن جعل المستخدم بلا roles.
- لا يمكن إزالة آخر admin.
- لا يمكن لمستخدم admin أن يقفل نفسه لو لا يوجد admin آخر.
- تغيير role صالح يعمل.

## Task 6: حماية تعديل رصيد الطالب

### المشكلة

`AdjustBalanceCommand` يعدل balance مباشرة وقد يخسر updates مع التزامن. لا توجد transaction كاملة أو concurrency token واضح.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.Application/Features/Admin/Commands/AdjustBalanceCommand.cs`
- `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- balance entity وtransactions إن وجدت.

### خطوات التنفيذ

1. اجعل إنشاء balance وتعديل الرصيد وتسجيل adjustment داخل transaction واحدة.
2. استخدم optimistic concurrency token مثل row version إن كان EF setup يسمح.
3. أو استخدم SQL atomic update:
   - `Balance = Balance + amount`
   - مع شروط تمنع السالب إن لم يكن مسموحا.
4. أضف validation:
   - `reason` مطلوب.
   - `amount` ليس صفرا.
   - حدود maximum/minimum معقولة.
   - policy واضحة للرصيد السلبي.
5. أضف audit log باسم admin actor.

### اختبارات القبول

- طلبان متزامنان لتعديل الرصيد لا يضيع أحدهما.
- amount = 0 يرفض.
- reason فارغ يرفض.
- negative balance يرفض أو يسمح فقط حسب policy موثقة.

## Task 7: توحيد .NET package versions

### المشكلة

`dotnet test` أظهر conflict بين EF package versions. هذا قد يسبب runtime behavior غير متوقع.

### ملفات يجب مراجعتها

- كل ملفات `.csproj` داخل `backend/src`
- `Directory.Packages.props` إن وجد.
- `backend/NaderGorge.sln`

### خطوات التنفيذ

1. شغل `dotnet list backend/NaderGorge.sln package --include-transitive`.
2. حدد عائلات packages:
   - EF Core
   - Npgsql EF provider
   - Microsoft.Extensions
3. وحد الإصدارات على عائلة متوافقة مع .NET 9.
4. يفضل استخدام Central Package Management لو المشروع قريب منه.
5. شغل restore/test.

### اختبارات القبول

- `dotnet test` ينجح بدون MSB3277 conflict.
- لا توجد downgrade warnings.

## Task 8: توحيد Redis configuration

### المشكلة

الكود يستخدم `ConnectionStrings:Redis` بينما بعض الإعدادات تستخدم مفاتيح أخرى أو fallback يخفي misconfiguration.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Program.cs`
- `appsettings*.json`
- `docker-compose.yml`
- worker Redis config.

### خطوات التنفيذ

1. اختر مفتاح واحد: `ConnectionStrings:Redis`.
2. حدث كل configs وenv vars لتستخدمه.
3. لا تستخدم fallback مثل `localhost:6382` في production.
4. إذا Redis اختياري لبعض الميزات، سجل `cache disabled` بوضوح.
5. إذا Redis مطلوب، افشل startup عند missing config.

### اختبارات القبول

- local dev يعمل بالمفتاح الجديد.
- Docker يعمل بالمفتاح الجديد.
- production لا يخفي missing Redis.

## Task 9: Forwarded headers وrate limiting

### المشكلة

rate limiting يعتمد على IP الخام، ولا يوجد `UseForwardedHeaders`. خلف reverse proxy قد تكون كل الطلبات من IP واحد أو IP غير صحيح.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Configuration/RateLimitingConfig.cs`
- `backend/src/NaderGorge.API/Program.cs`
- reverse proxy/deploy config إن وجد.

### خطوات التنفيذ

1. أضف `ForwardedHeadersOptions`.
2. حدد trusted proxies/networks. لا تقبل forwarded headers من أي مصدر بدون ضبط.
3. استخدم user id في rate limits للـ authenticated endpoints.
4. استخدم IP + phone hash للـ public phone endpoints.
5. أضف rate limits للـ:
   - WhatsApp check
   - public forms submit
   - parent report
   - password reset attempts

### اختبارات القبول

- rate limit يرى IP الحقيقي خلف proxy موثوق.
- public endpoints لا يمكن spam بسهولة.
- لا يتم قفل كل المستخدمين بسبب IP reverse proxy واحد.

## Task 10: Security headers عامة

### المشكلة

security headers موجودة route-specific فقط في embed route. لا يوجد middleware عام لـ HSTS/HTTPS/security headers.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Program.cs`
- frontend Next config أو middleware/proxy.
- `frontend/src/app/api/video/embed/route.ts`

### خطوات التنفيذ

1. في production backend:
   - `UseHttpsRedirection`
   - HSTS
2. أضف headers عامة للصفحات الحساسة:
   - `X-Content-Type-Options: nosniff`
   - `X-Frame-Options` أو `frame-ancestors` في CSP
   - `Referrer-Policy`
3. أضف CSP مناسبة:
   - لا تكسر video providers مثل Telegram/VK/Google Drive/Rutube.
   - ابدأ report-only إذا كان الخطر كبيرا ثم شددها.
4. عند نقل refresh token للcookie، استخدم:
   - HttpOnly
   - Secure
   - SameSite مناسب

### اختبارات القبول

- production responses تحتوي security headers.
- video embed providers لا تنكسر.
- لا توجد inline script unsafe إلا لو مبررة ومحدودة.

## Task 11: حماية WhatsApp/public forms

### المشكلة

WhatsApp check وpublic forms anonymous وقابلان للتعداد والسبام وpayloads كبيرة.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Controllers/WhatsAppController.cs`
- `backend/src/NaderGorge.API/Controllers/PublicFormsController.cs`
- rate limiting config.
- DTO validation.

### خطوات التنفيذ

1. WhatsApp check:
   - rate limit per IP.
   - rate limit per phone hash.
   - response لا يكشف أكثر من "يمكن المتابعة/لا يمكن المتابعة" حسب الحاجة.
   - لا تطبع الرقم كاملا في logs.
2. Public forms:
   - body size limit.
   - maximum fields count.
   - maximum value length.
   - validation حسب schema.
   - captcha أو proof-of-work عند الحاجة.
3. أضف rejection messages واضحة.

### اختبارات القبول

- payload كبير يرفض.
- submit spam يتقيد بالrate limit.
- phone enumeration يصبح صعبا.

## Task 12: إصلاح access code storage

### المشكلة

worker legacy يخزن `CodeHash` و`CodePlaintext` بنفس قيمة الكود. إذا تسرب DB، الأكواد قابلة للاستخدام مباشرة.

### ملفات يجب مراجعتها

- `worker/src/index.ts`
- `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`
- access code entity/usage logic.

### خطوات التنفيذ

1. لا تخزن plaintext code في DB كقيمة دائمة.
2. خزّن hash قوي فقط، مثل HMAC أو password hashing مناسب حسب طبيعة الكود.
3. لو يجب إظهار الكود عند التصدير:
   - اعرضه مرة واحدة قبل التخزين.
   - أو خزنه encrypted-at-rest بمفتاح خارجي غير موجود في DB.
4. راجع unique index على `CodeHash`.
5. أضف migration لو schema يحتاج تغيير.

### اختبارات القبول

- DB لا تحتوي code plaintext.
- code validation ما زال يعمل.
- duplicate code يرفض عبر hash.

## Task 13: storage واضح لملفات subtitles

### المشكلة

worker يكتب SRT داخل مسار source للbackend. في Docker قد لا يكون المسار مشتركا، وقد تضيع الملفات عند rebuild.

### ملفات يجب مراجعتها

- `worker/src/jobs/analyzeVideoChapters.ts`
- backend static file config.
- `docker-compose.yml`
- storage config/env.

### خطوات التنفيذ

1. أضف config:
   - `SUBTITLE_STORAGE_PATH`
   - `PUBLIC_SUBTITLE_BASE_URL`
2. في Docker، استخدم volume مشترك بين worker وbackend أو S3/local bucket.
3. لا تكتب داخل `backend/src/...` في runtime.
4. أضف health check أو startup check أن worker يستطيع الكتابة وأن backend يستطيع القراءة.
5. نظف path traversal. لا تسمح filenames من user input مباشرة.

### اختبارات القبول

- worker يكتب SRT في storage configured.
- backend يخدم الملف عبر URL صحيح.
- rebuild لا يحذف الملفات.

## Task 14: Redacted worker logs

### المشكلة

worker يطبع raw payloads وبيانات طلاب وروابط ملفات. logs قد تحتوي PII أو secrets.

### ملفات يجب مراجعتها

- `worker/src/index.ts`
- `worker/src/services/geminiService.ts`
- `worker/src/jobs/notification-sender.ts`
- `worker/src/scripts/birthday-congratulator.ts`

### خطوات التنفيذ

1. استبدل raw logs بـ structured logs.
2. لا تطبع:
   - phone numbers كاملة
   - student names عند عدم الحاجة
   - message contents
   - raw Redis payloads
   - file URIs الحساسة
3. استخدم correlation IDs/job IDs.
4. أضف helper redaction إن تكرر الأمر.

### اختبارات القبول

- تشغيل worker لا يطبع raw payload.
- logs كافية للتشخيص بدون PII.

## Task 15: توحيد URLs وdomains وإزالة brand drift

### المشكلة

frontend fallback يستخدم backend port مختلف، وبعض الكود يحتوي domains/brand قديم مثل `bsma` و`basma`.

### ملفات يجب مراجعتها

- `frontend/src/services/api-client.ts`
- `frontend/src/middleware.ts`
- `frontend/src/app/api/video/embed/route.ts`
- `.env.example`
- frontend config.

### خطوات التنفيذ

1. وحد API fallback على port المستخدم فعليا، غالبا `5245`.
2. انقل domains إلى env مثل:
   - `NEXT_PUBLIC_APP_DOMAIN`
   - `NEXT_PUBLIC_ADMIN_DOMAIN`
3. أزل strings القديمة `basma`, `bsma`, `basma-acadmy`.
4. استخدم brand مركزي مثل `Nader George Academy` أو قيمة env.

### اختبارات القبول

- local dev بدون env لا يضرب port خاطئ.
- لا توجد brand strings قديمة في code search.

## Task 16: الانتقال من Next middleware إلى proxy

### المشكلة

Next build يحذر أن `middleware` deprecated لصالح `proxy`. كما أن middleware يستخدم route hiding لا يمثل security boundary.

### ملفات يجب مراجعتها

- `frontend/src/middleware.ts`
- Next.js docs الرسمية إذا احتجت تفاصيل أحدث.
- auth/backend role checks.

### خطوات التنفيذ

1. اقرأ warning من build وحدد convention الجديد في Next 16.
2. انقل logic إلى `proxy` حسب المطلوب.
3. أبق route hiding فقط كـ UX/routing behavior وليس حماية.
4. تأكد أن admin APIs محمية backend-side بالroles.

### اختبارات القبول

- build لا يظهر deprecation warning.
- host routing يعمل كما كان.
- admin protection لا تعتمد على proxy وحده.

## Task 17: إزالة `Guid.Empty` actor fallback

### المشكلة

عدة controllers تستخدم `Guid.Empty` لو claim ناقص، مما يضعف audit integrity.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- `backend/src/NaderGorge.API/Controllers/AdminCommunityController.cs`
- `backend/src/NaderGorge.API/Controllers/BalanceController.cs`
- `backend/src/NaderGorge.API/Controllers/HomeworkController.cs`
- `backend/src/NaderGorge.API/Controllers/StudentController.cs`

### خطوات التنفيذ

1. أنشئ helper مشترك لاستخراج user id من claims.
2. إذا claim ناقص أو invalid:
   - ارجع `Unauthorized`.
   - أو throw exception تتحول إلى `401`.
3. استبدل كل `Guid.Empty` actor fallback.
4. أضف tests أو على الأقل controller-level checks.

### اختبارات القبول

- request بدون user id claim لا يسجل `Guid.Empty`.
- audit logs تحتوي actor صحيح أو الطلب يفشل.

## Task 18: خطة إزالة legacy timestamp behavior

### المشكلة

`Npgsql.EnableLegacyTimestampBehavior` يخفي مشاكل timezone ويؤجل ترحيل مهم.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Program.cs`
- entities التي تستخدم DateTime.
- migrations.

### خطوات التنفيذ

1. لا تحذف switch فجأة بدون اختبار.
2. احصر DateTime fields.
3. حدد policy:
   - كل timestamps تخزن UTC.
   - display يتحول حسب timezone في frontend.
4. أضف conversion أو migration حسب الحاجة.
5. بعد tests، احذف legacy switch.

### اختبارات القبول

- timestamps الجديدة UTC.
- التقارير والامتحانات لا تتغير مواعيدها بشكل خاطئ.
- legacy switch محذوف أو توجد خطة migration موثقة.

## أوامر تحقق مطلوبة قبل إنهاء المرحلة

```bash
dotnet restore backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-restore
cd frontend && npm audit --omit=dev && npm run build && npm run lint
cd worker && npm audit --omit=dev && npm run build
```

لو audit بقي يفشل بسبب transitive غير قابل للإصلاح حاليا، لا تعتبر المرحلة مكتملة إلا إذا وثقت:

- اسم الحزمة.
- مستوى الخطورة.
- لماذا لم يتم إصلاحها الآن.
- الخطة أو الإصدار المنتظر.

## تعريف اكتمال Phase 2

تعتبر المرحلة مكتملة عندما:

- CI يختبر stack الصحيح.
- dependencies الحرجة محدثة أو موثقة بخطة مقبولة.
- AI jobs لا تتكرر بسبب race condition.
- تغيير roles/balance محمي من الحالات الخطرة.
- Docker production لا يفتح خدمات داخلية.
- Redis/config/URLs موحدة.
- public endpoints عليها rate limits وحدود payload.
- logs لا تكشف بيانات حساسة.

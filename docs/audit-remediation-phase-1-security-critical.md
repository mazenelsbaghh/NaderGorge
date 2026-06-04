# Phase 1: Critical Security Hardening

تاريخ الإنشاء: 2026-06-04  
المصدر: `docs/project-deep-audit-2026-06-04.md`  
الأولوية: حرجة  
الهدف: إغلاق أخطر ثغرات الأمان التي قد تؤدي إلى takeover، تسريب بيانات طلاب، تزوير callbacks، أو XSS.

## تعليمات تنفيذ عامة للموديل المنفذ

- لا تبدأ بتجميل الواجهة أو refactor واسع. هذه المرحلة أمان فقط.
- اقرأ الملفات المذكورة في كل مهمة قبل التعديل.
- لا تضع أي secret حقيقي في الكود أو في ملفات tracked.
- أي default secret مثل `secretxyz` يجب أن يتحول إلى فشل startup واضح، وليس قيمة بديلة.
- بعد كل مجموعة تعديلات، شغل الاختبارات أو build المناسب وسجل النتيجة.
- لا تعتمد على إخفاء routes في frontend كحماية. الحماية يجب أن تكون server-side.

## نطاق المرحلة

هذه المرحلة تغطي البنود التالية من التدقيق:

- P0-1: منع seeding لحسابات افتراضية في production/Docker.
- P0-2: حماية worker وinternal callbacks وإزالة default secrets.
- P0-3: حماية parent report بروابط موقعة ومنتهية الصلاحية.
- P0-4: منع XSS من HTML الأسئلة والإجابات وwatermark embed.
- P0-5: تسجيل `RequireStudent` policy.
- P1-2: إزالة أسرار tracked وتدويرها وإضافة startup validation.
- P1-3: تقليل خطر تخزين tokens في browser storage.
- P1-4: إصلاح password reset.
- P1-5: تأمين E2E controller.

## Task 1: إيقاف default seeding في البيئات غير الآمنة

### المشكلة

التطبيق يشغل `Seeder.SeedAsync(db)` بدون تقييد كاف، والـ seeder ينشئ admin/student بكلمات مرور معروفة. هذا خطر مباشر إذا اشتغلت قاعدة بيانات production جديدة أو تم مسحها.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Program.cs`
- `backend/src/NaderGorge.Infrastructure/Data/Seeder.cs`
- ملفات tests داخل `backend/tests` إن وجدت patterns مناسبة.

### خطوات التنفيذ

1. في `Program.cs`، امنع تشغيل `Seeder.SeedAsync(db)` إلا في بيئات آمنة صريحة مثل `Development` أو `E2e`.
2. لا تعتمد على `!app.Environment.IsProduction()` فقط، لأن Docker/Staging قد لا تسمى Production.
3. أضف config flag صريح مثل `SeedDefaults:Enabled`.
4. اجعل القيمة الافتراضية `false`.
5. اسمح بالـ seeding فقط عندما:
   - البيئة `Development` أو `E2e`.
   - و`SeedDefaults:Enabled == true`.
6. داخل `Seeder.cs`، لا تنشئ admin/student افتراضيين في أي مسار production.
7. إن وجدت accounts افتراضية في DB حقيقية، اكتب ملاحظة في changelog/ops docs أن credentials يجب تدويرها.

### اختبارات القبول

- عند عدم وجود `SeedDefaults:Enabled=true` لا يتم إنشاء الحسابات الافتراضية.
- عند `Development` ومع تفعيل flag، يستمر seeding في العمل محليا.
- يوجد test أو فحص واضح يثبت أن production لا تشغل seeding.
- لا توجد كلمة مرور افتراضية جديدة مضافة في الكود.

### أخطاء ممنوعة

- ممنوع ترك seeding يعمل في Docker بسبب أنه ليس Production.
- ممنوع استبدال المشكلة بكلمة مرور افتراضية مختلفة.
- ممنوع حذف seeding بالكامل إذا كان مطلوبا للـ E2E بدون توفير مسار test آمن.

## Task 2: إزالة default callback secrets وحماية internal callbacks

### المشكلة

`InternalController` يعتمد على `X-Internal-Token` مع fallback `secretxyz`. هذا يسمح بتزوير callbacks لو default secret بقي مستخدما أو اتكشف.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Controllers/InternalController.cs`
- `backend/src/NaderGorge.API/Program.cs`
- `docker-compose.yml`
- `worker/src/index.ts`
- أي client داخل worker يرسل callbacks إلى backend.

### خطوات التنفيذ

1. ابحث عن كل ظهور لـ `secretxyz`.
2. احذف أي fallback ثابت.
3. أضف startup validation في backend:
   - إذا كان secret مفقودا، فارغا، قصيرا، أو يساوي default معروف، يفشل startup.
   - استخدم رسالة خطأ واضحة بدون طباعة قيمة السر.
4. أضف نفس الفكرة في worker إذا كان يحتاج secret لإرسال callbacks.
5. بدلا من مقارنة token ثابت فقط، نفذ HMAC signing إن أمكن ضمن نطاق هذه المرحلة:
   - headers مقترحة: `X-Internal-Timestamp`, `X-Internal-Nonce`, `X-Internal-Signature`.
   - signature = HMAC-SHA256 over method + path + timestamp + nonce + body hash.
   - ارفض الطلب لو timestamp أقدم من 5 دقائق.
   - ارفض nonce مكرر إن أمكن باستخدام Redis/cache لمدة قصيرة.
6. إذا لم تنفذ HMAC الآن، على الأقل:
   - اجعل secret قويا ومطلوبا.
   - استخدم constant-time comparison.
   - ضع rate limit للـ internal endpoints.
7. لا تضف `[Authorize]` عشوائيا إن كان worker لا يملك JWT. استخدم service-to-service auth واضح.

### اختبارات القبول

- backend لا يبدأ إذا لم يتم ضبط internal callback secret.
- callback بدون header صحيح يرجع `401`.
- callback بـ default secret يرجع `401`.
- callback صحيح من worker ما زال يعمل.
- لا تظهر قيمة secret في logs.

### أخطاء ممنوعة

- ممنوع ترك `secretxyz` في أي ملف production أو compose fallback.
- ممنوع طباعة secret عند فشل startup.
- ممنوع الاعتماد على CORS كحماية للـ callbacks.

## Task 3: إغلاق worker وBull Board وworker proxy

### المشكلة

الـ worker exposes endpoints لإلغاء/retry jobs، ويعرض Bull Board على `/ui`، ويفتح port على host. يوجد frontend proxy عام يمكن أن يمرر GET/POST/DELETE إلى worker.

### ملفات يجب مراجعتها

- `worker/src/index.ts`
- `docker-compose.yml`
- `frontend/src/app/api/worker/[...path]/route.ts`
- أي صفحات admin تستخدم worker proxy مثل AI monitor.

### خطوات التنفيذ

1. في `docker-compose.yml`:
   - لا تنشر worker port في production compose.
   - اجعل worker متاحا فقط داخل Docker network.
   - إن كان compose الحالي dev، افصل production compose أو أضف تعليق/ملف override واضح.
2. في `worker/src/index.ts`:
   - أوقف `cors()` المفتوح.
   - حدد allowed origins في development فقط.
   - اجعل `/ui` يتطلب admin auth أو service token قوي.
   - اجعل endpoints مثل cancel/retry/status تتطلب auth.
3. في `frontend/src/app/api/worker/[...path]/route.ts`:
   - امنع proxy العام لأي path.
   - اعمل allowlist للـ paths المطلوبة فقط.
   - قبل POST/DELETE، تحقق server-side أن المستخدم admin.
   - لا تثق في client role من localStorage.
4. لو frontend لا يستطيع التحقق من session حاليا بسبب auth architecture، اجعل route يرجع `403` مؤقتا للعمليات الخطرة حتى تتوفر حماية صحيحة.

### اختبارات القبول

- `/api/worker/...` لا يسمح بـ POST/DELETE لمستخدم غير admin.
- Bull Board غير متاح بدون auth.
- worker port غير منشور على host في إعداد production.
- AI monitor يستمر في قراءة status بشكل آمن أو يعرض error واضح.

### أخطاء ممنوعة

- ممنوع ترك Bull Board مفتوح لأنه "داخلي".
- ممنوع حماية worker بـ frontend UI فقط.
- ممنوع proxy wildcard للـ worker بدون allowlist.

## Task 4: حماية parent reports

### المشكلة

تقرير ولي الأمر anonymous ويقبل `studentId` فقط. GUID ليس authorization. أي شخص يعرف أو يحصل على الرابط يمكنه رؤية بيانات الطالب.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Controllers/ParentController.cs`
- `backend/src/NaderGorge.Application/Features/Reports/Queries/GetParentReportQuery.cs`
- Entities الخاصة بالطلاب وأولياء الأمور إن وجدت.
- EF migrations.

### خطوات التنفيذ

1. لا تقبل `studentId` وحده في endpoint العام.
2. أنشئ token موقّع ومنتهي الصلاحية.
3. token يجب أن يرتبط بـ:
   - `studentId`
   - نوع الاستخدام `parent-report`
   - expiry
   - version أو revocation key
4. يفضل تخزين hash للـ token في DB مع expiry وrevoked flag، خصوصا لو مطلوب revoke.
5. endpoint العام يأخذ token فقط أو token + studentId، لكنه يتحقق أن token يخص نفس الطالب.
6. أضف audit log عند فتح التقرير:
   - student id
   - timestamp
   - requester ip إن متاح
   - user agent إن متاح
7. أضف endpoint admin أو service method لإنشاء/تدوير رابط ولي الأمر.

### اختبارات القبول

- طلب report بـ `studentId` فقط يفشل.
- token صالح وغير منتهي يعرض التقرير الصحيح.
- token منتهي يرجع `401` أو `403`.
- token لطالب A لا يفتح تقرير طالب B.
- يمكن revoke token.

### أخطاء ممنوعة

- ممنوع اعتبار GUID كسر.
- ممنوع وضع بيانات الطالب داخل token بدون توقيع وتشفير مناسب.
- ممنوع token لا ينتهي.

## Task 5: منع XSS في الأسئلة والإجابات وembed watermark

### المشكلة

يوجد عرض HTML من الأسئلة/الإجابات بدون sanitization كاف، ويوجد حقن `studentName/studentPhone` في `innerHTML` داخل embed route. هذا يمكن أن يسمح بتنفيذ JavaScript داخل جلسة الطالب أو الأدمن.

### ملفات يجب مراجعتها

- `frontend/src/components/exams/ExamViewer.tsx`
- `frontend/src/app/student/mistakes/page.tsx`
- `frontend/src/app/admin/content/exams/[id]/dashboard/page.tsx`
- `frontend/src/app/api/video/embed/route.ts`
- backend commands التي تحفظ أسئلة أو اختيارات الامتحانات.

### خطوات التنفيذ

1. حدد allowlist واضحة للـ rich text:
   - tags مسموحة: `p`, `br`, `strong`, `b`, `em`, `i`, `u`, `ul`, `ol`, `li`, `span` عند الحاجة.
   - attributes محدودة جدا مثل `class` فقط إذا كان ضروريا ومتحكما فيه.
2. امنع تماما:
   - `script`
   - `iframe`
   - `style`
   - event handlers مثل `onerror`, `onclick`
   - URLs تبدأ بـ `javascript:`
3. نفذ sanitization server-side عند الحفظ إن كان ممكنا.
4. نفذ sanitization client-side قبل `dangerouslySetInnerHTML`.
5. إن لم توجد مكتبة مناسبة، استخدم مكتبة موثوقة مثل DOMPurify في frontend، وHtmlSanitizer أو allowlist parser في backend.
6. في `embed/route.ts`:
   - لا تستخدم `innerHTML` مع interpolated user data.
   - استخدم `textContent`.
   - عند إدخال قيمة داخل script string، استخدم `JSON.stringify(value)`.
7. راجع كل `dangerouslySetInnerHTML` في المشروع وليس الملفات المذكورة فقط.

### اختبارات القبول

- `<img src=x onerror=alert(1)>` لا ينفذ.
- `<script>alert(1)</script>` لا يظهر ولا ينفذ.
- `javascript:` داخل link يتم حذفه أو تعطيله.
- السؤال الغني الطبيعي مثل bold/list يعمل.
- watermark يعرض الاسم/الهاتف كنص فقط حتى لو بهما علامات HTML.

### أخطاء ممنوعة

- ممنوع عمل regex بسيط لحذف `<script>` فقط.
- ممنوع تعطيل كل HTML إذا كان rich text مطلوبا بدون التأكد من UX.
- ممنوع الاعتماد على client-side فقط لو المحتوى يعرض أيضا في admin/backend contexts.

## Task 6: تسجيل `RequireStudent` policy

### المشكلة

`GamificationController` يستخدم policy غير مسجلة، مما قد يكسر endpoint للطلاب runtime.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Controllers/GamificationController.cs`
- `backend/src/NaderGorge.API/Program.cs`

### خطوات التنفيذ

1. في authorization setup، أضف:
   - `RequireStudent` يتطلب role `Student`.
2. راجع أسماء roles المستخدمة في seeder وauth claims.
3. إن كان المشروع يفضل attributes مباشرة، استبدلها بـ `[Authorize(Roles = "Student")]`، لكن الأفضل توحيد policies.

### اختبارات القبول

- student يستطيع طلب `/api/Gamification/status`.
- admin بدون Student role لا يدخل إذا كان endpoint للطلاب فقط.
- anonymous يرجع `401`.

## Task 7: إزالة الأسرار tracked وإضافة secret validation

### المشكلة

بعض ملفات development/e2e تحتوي JWT/Evolution config أو defaults ضعيفة. حتى لو كانت dev، إعادة استخدامها في production خطر.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/appsettings.Development.json`
- `backend/src/NaderGorge.API/appsettings.E2e.json`
- `.env.example`
- `worker/.env.example`
- `docker-compose.yml`
- CI workflows.

### خطوات التنفيذ

1. أزل أي real API key من الملفات tracked.
2. استبدلها placeholders واضحة مثل `CHANGE_ME_IN_LOCAL_ENV`.
3. أنشئ example files فقط، ولا تضع أسرار فعلية.
4. أضف startup validation للـ JWT secret:
   - موجود.
   - طوله مناسب.
   - ليس placeholder.
   - ليس default معروف.
5. أضف نفس validation لـ Evolution API key وأي callback secret مطلوب.
6. أضف secret scanning في CI إن أمكن.
7. اكتب ملاحظة ops: كل secrets القديمة يجب تدويرها.

### اختبارات القبول

- التطبيق لا يبدأ بـ placeholder secrets في production.
- ملفات example لا تحتوي أسرار حقيقية.
- لا توجد strings قديمة مثل `secretxyz` أو API keys فعلية في git.

### أخطاء ممنوعة

- ممنوع حذف config المطلوب بدون توفير example واضح.
- ممنوع جعل production يستخدم dev fallback.

## Task 8: تقليل خطر token storage وpassword reset

### المشكلة

الـ frontend يخزن access/refresh tokens في `localStorage/sessionStorage`. مع وجود XSS، يمكن سرقة refresh token. password reset يصدر JWT stateless ولا يبطل refresh tokens القديمة.

### ملفات يجب مراجعتها

- `frontend/src/lib/auth-storage.ts`
- `frontend/src/services/api-client.ts`
- backend auth commands:
  - `VerifyResetFieldsCommand.cs`
  - `ResetPasswordCommand.cs`
- auth controller/endpoints.
- refresh token persistence إن وجدت.

### خطوات التنفيذ

1. الحل الأفضل:
   - انقل refresh token إلى HttpOnly Secure SameSite cookie.
   - اجعل access token قصير العمر.
   - خزّن access token في memory إن أمكن.
2. إذا كان النقل الكامل كبيرا لهذه المرحلة:
   - قلل عمر access token.
   - أضف CSP قوية.
   - تأكد أن password reset يبطل كل refresh tokens القديمة.
   - جهز plan واضح لنقل refresh token للـ cookie في Phase 2.
3. password reset:
   - لا تستخدم JWT reset token stateless فقط.
   - أنشئ reset token server-side.
   - خزّن hash فقط.
   - اجعله one-time مع expiry قصيرة.
   - أضف attempts per account/IP.
4. عند نجاح reset password:
   - revoke كل refresh tokens للمستخدم.
   - سجل security event.

### اختبارات القبول

- reset token لا يعمل مرتين.
- reset token المنتهي لا يعمل.
- بعد reset password، refresh token القديم لا يستطيع إصدار access token جديد.
- XSS mitigation الأساسية موجودة: CSP + sanitization من Task 5.

### أخطاء ممنوعة

- ممنوع تخزين reset token plaintext في DB.
- ممنوع ترك refresh sessions القديمة بعد تغيير كلمة المرور.
- ممنوع اعتبار CSP بديل عن sanitization.

## Task 9: تأمين E2E testing controller

### المشكلة

E2E controller route عام ويمكنه حذف وإعادة إنشاء DB إذا البيئة `E2e`. misconfiguration واحد قد يكون مدمر.

### ملفات يجب مراجعتها

- `backend/src/NaderGorge.API/Controllers/E2eTestingController.cs`
- `backend/src/NaderGorge.API/Program.cs`
- CI e2e setup.

### خطوات التنفيذ

1. لا تسجل controller إلا في E2E/test profile.
2. أضف secret خاص للاختبارات حتى داخل E2E.
3. `EnsureDeleted` يجب أن يعمل فقط لو:
   - environment = `E2e`
   - connection string يشير test DB واضح
   - e2e secret صحيح
4. امنع استخدام كلمات مرور عامة مثل `password` إلا في test فقط.
5. أضف log واضح عند تفعيل E2E endpoints، بدون طباعة secrets.

### اختبارات القبول

- في Development العادي، `/api/e2e` غير متاح أو يرجع `404`.
- في Production، `/api/e2e` غير متاح.
- في E2E بدون secret، endpoint يرفض.
- في E2E مع secret وtest DB، endpoint يعمل.

## ترتيب التنفيذ المقترح داخل Phase 1

1. `RequireStudent` policy لأنها صغيرة وتكسر endpoint.
2. منع seeding الافتراضي.
3. إزالة `secretxyz` وstartup validation.
4. إغلاق worker/Bull Board/proxy.
5. XSS sanitization وembed route.
6. Parent report signed tokens.
7. E2E controller hardening.
8. token storage/password reset hardening.

## أوامر تحقق مطلوبة قبل إنهاء المرحلة

نفذ ما ينطبق حسب الملفات التي تغيرت:

```bash
dotnet test backend/NaderGorge.sln --no-restore
cd frontend && npm run build && npm run lint
cd worker && npm run build
```

لو فشل أمر، لا تخفي الفشل. اكتب:

- الأمر الذي فشل.
- سبب الفشل من logs.
- هل الفشل بسبب تعديلك أم مشكلة موجودة مسبقا.
- ما المطلوب لإصلاحه.

## تعريف اكتمال Phase 1

تعتبر المرحلة مكتملة عندما:

- لا توجد default credentials أو callback secrets فعالة.
- worker endpoints الحساسة ليست مفتوحة للعامة.
- parent report لا يفتح بـ `studentId` فقط.
- HTML الأسئلة والإجابات لا ينفذ JavaScript.
- `RequireStudent` مسجلة أو مستبدلة بشكل صحيح.
- reset password لا يترك refresh sessions قديمة.
- E2E destructive endpoints لا تظهر في non-test environments.

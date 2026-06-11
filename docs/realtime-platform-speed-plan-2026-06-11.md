# خطة تسريع المنصة والتحديث اللحظي - 2026-06-11

النطاق: منصة Massar/Nader Gorge الحالية: Next.js frontend، .NET API، PostgreSQL، Redis، Worker، Docker، الفيديوهات، الأكواد، الباقات، الملفات، والإشعارات.

الهدف: أي حاجة تتضاف أو تتغير في المنصة "تسمع" فورًا عند المستخدمين من غير ما الطالب أو المدرس أو الأدمن يدوس تحديث الصفحة أو زر تحديث المهام. وفي نفس الوقت المنصة تبقى أسرع ومحميّة من الضغط والتكرار.

## الخلاصة

المشكلة ليست في زر تحديث واحد. المطلوب طبقة تحديث لحظي عامة:

- Backend يغير البيانات في PostgreSQL.
- Backend يطلع حدث واضح مثل `LessonPublished` أو `VideoReady` أو `CodeActivated`.
- SignalR يوصل الحدث للمستخدمين المعنيين.
- Frontend يمسح الكاش المتأثر فقط ويحدث الجزء المطلوب من الصفحة.
- لا يوجد `window.location.reload()` ولا `router.refresh()` إلا كحل طوارئ.
- العمليات الثقيلة مثل رفع فيديو أو تجهيز ملف تشتغل كـ jobs، ولما تخلص ترسل event فورًا.

المشروع عنده أساس جيد: SignalR موجود للدردشة، Redis موجود، rate limiting موجود، وresponse compression/output cache موجودين. الناقص هو استخدام SignalR كطبقة platform events لكل المنصة بدل ما يكون للدردشة فقط.

## الموجود حاليًا

نقاط جيدة:

- يوجد SignalR في `backend/src/NaderGorge.API/Program.cs`.
- يوجد hub حالي للدردشة: `backend/src/NaderGorge.API/Hubs/ChatHub.cs`.
- يوجد hook في الواجهة: `frontend/src/hooks/useSignalR.ts`.
- يوجد Redis في Docker.
- يوجد rate limiting على auth/codes/video-session/public endpoints.
- يوجد response compression وoutput cache.
- صفحات `page.tsx` أصبحت Server Components، وهذا ممتاز للأداء.

فجوات لازم تتقفل:

- SignalR غير مستخدم لتحديث الباقات، الدروس، الفيديوهات، الملفات، الأكواد، الرصيد، والإشعارات.
- يوجد أماكن ما زالت تعمل refresh يدوي:
  - `frontend/src/components/balance/PurchaseContentModal.tsx`
  - `frontend/src/app/qr/[codeHash]/QrRedeemClient.tsx`
  - `frontend/src/app/admin/students/AdminStudentsPageClient.tsx`
- يوجد polling سريع في:
  - AI monitor
  - lesson video processing status
- لا توجد طبقة cache invalidation موحدة في frontend.
- لا يوجد outbox event pattern يضمن أن كل تغيير في DB يطلع له event مضمون.

## القرار الفني الصحيح

نفذ `PlatformHub` مستقل عن `ChatHub`.

لا توسع `ChatHub` لأن الدردشة لها منطق مختلف. المطلوب hub عام للمنصة:

- `/hubs/platform`
- يربط المستخدم بجروبات حسب دوره وبياناته.
- يرسل events صغيرة، لا يرسل payload كبير.
- الواجهة عندها handlers تمسح cache keys وتجلب الجزء المطلوب فقط.

## تصميم PlatformHub

### Groups

عند اتصال المستخدم:

- كل مستخدم يدخل `User_{userId}`.
- الطالب يدخل `Role_Student`.
- المدرس يدخل `Role_Teacher`.
- الأدمن يدخل `Role_Admin`.
- الطالب يدخل جروبات الباقات المفعلة: `Package_{packageId}`.
- المدرس يدخل `Teacher_{teacherId}`.
- عند فتح درس، الواجهة ممكن تطلب الانضمام إلى `Lesson_{lessonId}`.

### Events المطلوبة

| Event | يروح لمين | الواجهة تعمل إيه |
|---|---|---|
| `NotificationCreated` | `User_{userId}` | تحديث عداد الإشعارات وإظهار toast |
| `BalanceChanged` | `User_{userId}` | تحديث الرصيد في sidebar |
| `CodeActivated` | `User_{userId}` | تحديث الباقات والصلاحيات والرصيد |
| `PackagePublished` | الطلاب المستهدفون | تحديث قائمة الباقات |
| `PackageUpdated` | `Package_{packageId}` | تحديث بيانات الباقة |
| `LessonPublished` | `Package_{packageId}` | إظهار الدرس بدون refresh |
| `LessonUpdated` | `Lesson_{lessonId}` و `Package_{packageId}` | تحديث تفاصيل الدرس |
| `VideoProcessingStarted` | `Lesson_{lessonId}` | إظهار حالة "جاري التجهيز" |
| `VideoReady` | `Lesson_{lessonId}` | إظهار الفيديو فورًا |
| `ResourceReady` | `Lesson_{lessonId}` | إظهار الملف فورًا |
| `ExtraWatchRequestUpdated` | `User_{userId}` | تحديث حالة طلب مشاهدة إضافية |
| `AiJobProgress` | admin/teacher | تحديث شاشة المهام بدون polling كل 3 ثواني |

## Backend Flow

أي command يغير حاجة مهمة يمشي بهذا الشكل:

1. يتحقق من الصلاحية.
2. يغير البيانات في PostgreSQL.
3. يسجل event في جدول `OutboxEvents`.
4. يعمل commit.
5. Background service يقرأ `OutboxEvents` ويرسلها عبر SignalR.
6. يعلم event أنه اتبعت.

ليه Outbox مهم؟

- يمنع أن البيانات تتغير لكن الإشعار لا يصل.
- يمنع تكرار الإرسال عند retry.
- يخلي كل الأحداث قابلة للتتبع.
- لو SignalR وقع لحظة، الأحداث لا تضيع.

### جدول OutboxEvents

الحقول المقترحة:

- `Id`
- `Type`
- `PayloadJson`
- `TargetGroup`
- `TargetUserId`
- `CreatedAt`
- `ProcessedAt`
- `RetryCount`
- `LastError`

ابدأ بسيط. لا تحتاج نظام events معقد في البداية.

## Frontend Flow

اعمل hook جديد:

- `frontend/src/hooks/usePlatformEvents.ts`

وظيفته:

- يفتح اتصال SignalR بعد login.
- يستقبل events.
- ينادي cache invalidation حسب نوع event.
- يظهر toast عند الحاجة.
- لو الاتصال وقع، يعمل reconnect تلقائي.

مثال المنطق المطلوب:

```ts
PackageUpdated -> invalidate(["student:packages", `package:${packageId}`])
LessonPublished -> invalidate(["student:packages", `package:${packageId}`, `term:${termId}`])
VideoReady -> invalidate([`lesson:${lessonId}`])
BalanceChanged -> invalidate(["student:shell", "student:balance"])
NotificationCreated -> invalidate(["student:shell", "notifications"])
```

المهم: لا تعمل refresh للصفحة كلها. حدث الجزء الذي تغير فقط.

## استبدال زر التحديث والـ Refresh

### ممنوع في الشاشات الأساسية

- `window.location.reload()`
- `router.refresh()` بعد شراء أو تفعيل أو تعديل بسيط
- polling سريع كل 2 أو 3 ثواني كحل دائم

### البديل

- optimistic UI لو العملية بسيطة.
- invalidate cache keys بعد نجاح API.
- SignalR event يعمل sync نهائي.
- polling fallback فقط لو SignalR غير متصل، ويكون كل 30-60 ثانية مع backoff.

## سيناريوهات عملية

### 1. المدرس أضاف درس جديد

المفروض يحصل:

1. المدرس يحفظ الدرس.
2. backend يسجل `LessonPublished`.
3. SignalR يرسل event إلى `Package_{packageId}`.
4. الطالب الذي فاتح الباقة يرى الدرس يظهر مباشرة.
5. الطالب الذي في صفحة أخرى يرى عداد/تنبيه حسب الحاجة.

بدون:

- زر تحديث.
- reload.
- polling دائم.

### 2. الأدمن أو المدرس رفع فيديو

المفروض:

1. الفيديو يظهر بحالة `Processing`.
2. worker يجهز الفيديو أو يتحقق من الرابط.
3. عند الجاهزية backend يرسل `VideoReady`.
4. صفحة الدرس تحدث الفيديو فقط.

لو فشل:

1. backend يرسل `VideoProcessingFailed`.
2. الواجهة تعرض الخطأ للمدرس/الأدمن فقط.

### 3. ملف جديد نزل في درس

المفروض:

1. الملف يسجل بحالة `Uploading` أو `Processing`.
2. لما يبقى جاهز، يرسل `ResourceReady`.
3. الطالب يرى الملف في قائمة ملفات الدرس فورًا.

### 4. طالب فعل كود

المفروض:

1. backend يفعل الكود.
2. يرسل `CodeActivated` و `BalanceChanged` إن لزم.
3. الواجهة تحدث:
   - الباقات.
   - صلاحيات الوصول.
   - الرصيد.
   - shell data.

لا تستخدم `router.refresh()`.

### 5. طلب مشاهدة إضافية اتقبل أو اترفض

المفروض:

1. الأدمن يوافق أو يرفض.
2. backend يرسل `ExtraWatchRequestUpdated` إلى الطالب.
3. مشغل الفيديو يفتح أو يظل مقفول ويعرض السبب.

## التحميل والتنزيل الآمن والسريع

### المشكلة

لو الملفات الكبيرة تمر من .NET API مباشرة، السيرفر يتضغط. ولو الملفات public، الصلاحيات تضيع.

### الحل

- backend يتحقق من الصلاحية.
- backend يصدر signed URL قصير العمر.
- Nginx أو storage يخدم الملف.
- للفيديو لازم يدعم `Range Requests`.

مدة الرابط:

- ملفات صغيرة: 5 دقائق.
- فيديوهات: 1-5 دقائق مع session.
- روابط حساسة: 60-120 ثانية.

### لو التخزين Local داخل Docker

استخدم Nginx `X-Accel-Redirect`:

- الطالب يطلب download.
- .NET يتحقق من الصلاحية.
- .NET يرجع header داخلي.
- Nginx يرسل الملف بسرعة.

هذا أسرع وأخف من أن .NET يقرأ الملف ويكتبه في response.

## حماية من الضغط والتكرار

### Rate limiting

الموجود جيد كبداية، لكن production يحتاج:

- IP limit في Nginx/Cloudflare للزوار.
- user limit في Redis للمستخدمين المسجلين.
- limits خاصة للعمليات الثقيلة:
  - تفعيل كود.
  - إنشاء video session.
  - طلب signed download URL.
  - رفع ملف.
  - بدء AI job.

### Idempotency

أي POST مهم لازم يدعم `Idempotency-Key`.

أمثلة:

- purchase
- activate code
- submit exam
- submit homework
- request extra watch
- start processing job

لو نفس الطلب اتبعت مرتين، السيرفر يرجع نفس النتيجة بدل ما ينفذ مرتين.

### منع duplicate jobs

في BullMQ:

- استخدم job id ثابت مثل `video-process:{lessonVideoId}`.
- لا تسمح بأكثر من job نشط لنفس الفيديو.
- retry يكون exponential backoff.
- failed jobs تظهر في admin monitor.

## تسريع الواجهة

استمر على القواعد الحالية:

- `page.tsx` Server Component.
- التفاعل داخل client components صغيرة.
- لا تحمل مكتبات ثقيلة في أول الصفحة.
- استخدم dynamic imports لـ:
  - `framer-motion`
  - `gsap`
  - `three`
  - `react-quill-new`
  - QR scanner
  - SignalR على الصفحات التي تحتاجه فقط

## تسريع API وDatabase

قواعد ثابتة:

- كل read query يستخدم `AsNoTracking()`.
- استخدم projection إلى DTO.
- لا تستخدم `Include` عميق لو محتاج count أو summary.
- أي list لازم pagination.
- لا تعمل query داخل loop.
- أي response أكبر من 150KB يتراجع.

Endpoints الأكثر أهمية:

- student dashboard
- lesson detail
- packages
- progress
- mistakes
- community feed
- notifications
- admin reports
- code groups
- AI monitor

## قياسات النجاح

| القياس | الهدف |
|---|---|
| ظهور درس جديد عند الطالب | أقل من 1 ثانية بعد الحفظ |
| ظهور فيديو جاهز | أقل من 1 ثانية بعد انتهاء المعالجة |
| تحديث الرصيد بعد شراء/كود | فوري بدون refresh |
| إشعار جديد | فوري |
| fallback polling | 30-60 ثانية وليس 2-3 ثواني |
| API P95 للقراءة | أقل من 300ms |
| lesson/dashboard P95 | أقل من 800ms |
| download token | أقل من 150ms |

## خطة تنفيذ بالترتيب

### P0 - لازم الأول

1. إنشاء `PlatformHub` في backend.
2. إنشاء `usePlatformEvents` في frontend.
3. إنشاء cache invalidation registry في frontend.
4. إزالة `router.refresh()` و `window.location.reload()` من الشاشات الأساسية.
5. ربط events:
   - `NotificationCreated`
   - `BalanceChanged`
   - `CodeActivated`
   - `LessonPublished`
   - `VideoReady`
   - `ResourceReady`

### P1 - بعد ما realtime يشتغل

1. إضافة `OutboxEvents`.
2. إضافة background sender للأحداث.
3. تحويل AI monitor من polling سريع إلى SignalR events.
4. تحويل video processing status إلى events.
5. إضافة signed download URLs.
6. إضافة Redis idempotency للعمليات الحساسة.

### P2 - تحسينات أداء مستمرة

1. slow endpoint logging.
2. slow query logging.
3. مراجعة الفهارس بـ `EXPLAIN ANALYZE`.
4. bundle analyzer للواجهة.
5. performance budget في CI.

## أوامر فحص دورية

```bash
cd frontend
npm run build
npm run lint
rg -n "window\\.location\\.reload|router\\.refresh\\(" src
rg -n "^['\"]use client['\"]" src/app --glob 'page.tsx'
```

```bash
cd backend
dotnet test
rg -n "Include\\(|ToListAsync\\(|FirstOrDefaultAsync\\(" src/NaderGorge.Application src/NaderGorge.Infrastructure
```

## النتيجة المطلوبة

بعد تنفيذ الخطة، المستخدم لن يحتاج يضغط تحديث. أي درس، فيديو، ملف، كود، رصيد، إشعار، أو حالة مهمة ستظهر فورًا عن طريق SignalR events. والضغط على السيرفر يقل لأن الواجهة ستحدث الجزء المتأثر فقط بدل reload كامل أو polling سريع.

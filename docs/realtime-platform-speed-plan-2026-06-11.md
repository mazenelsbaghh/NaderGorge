# خطة تسريع المنصة والتحديث اللحظي - 2026-06-11

النطاق: منصة Massar/Nader Gorge الحالية: Next.js frontend، .NET API، PostgreSQL، Redis، Worker، Docker، الفيديوهات، الأكواد، الباقات، الملفات، والإشعارات.

الهدف: أي حاجة تتضاف أو تتغير في المنصة "تسمع" فورًا عند المستخدمين من غير ما الطالب أو المدرس أو الأدمن يدوس تحديث الصفحة أو زر تحديث المهام. وفي نفس الوقت المنصة تبقى أسرع ومحميّة من الضغط والتكرار.

> **تنبيه تدقيق:** الجداول القديمة داخل هذا الملف تسجل مراحل تنفيذ سابقة، وبعضها يعلن اكتمال 100% بصورة غير دقيقة. قسم **"التدقيق المصحح والحالة الفعلية"** في نهاية الملف هو المرجع الحالي ويحل محل أي نسبة أو inventory متعارض قبله.

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
- يوجد hub جديد للتحديثات العامة: `backend/src/NaderGorge.API/Hubs/PlatformHub.cs`.
- يوجد hook في الواجهة: `frontend/src/hooks/useSignalR.ts`.
- يوجد hook جديد للتحديثات العامة: `frontend/src/hooks/usePlatformEvents.ts`.
- يوجد جدول outbox جديد: `backend/src/NaderGorge.Domain/Entities/OutboxEvent.cs`.
- يوجد background service لإرسال أحداث outbox: `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs`.
- يوجد migration جديد: `backend/src/NaderGorge.Infrastructure/Migrations/20260611014030_AddOutboxEvents.cs`.
- يوجد Redis idempotency service وفلتر `[Idempotent]`.
- يوجد Redis في Docker.
- يوجد rate limiting على auth/codes/video-session/public endpoints.
- يوجد response compression وoutput cache.
- صفحات `page.tsx` أصبحت Server Components، وهذا ممتاز للأداء.

فجوات لازم تتقفل:

- SignalR يغطي كل الـ 58 producer الحالية؛ يوجد listenerان احتياطيان بلا producer (`SectionUpdated`, `SectionDeleted`).
- ~~يوجد أماكن ما زالت تعمل refresh يدوي~~ ✅ تم إزالة جميع `router.refresh()` و `window.location.reload()`.
- ~~يوجد polling سريع~~ ✅ تم رفع جميع intervals إلى 30 ثانية كحد أدنى.
- تم إنشاء `cache-invalidation.ts` وتسجيل packages/shell/lesson/comments/term/notifications/balance/exams/community/AI monitor.
- يوجد 58 event producer في backend، وكلها مرتبطة بـ frontend listeners؛ الاختبارات العقدية end-to-end ما زالت ناقصة.


## سجل تنفيذ سابق (غير معتمد بعد التدقيق المصحح)

آخر فحص: 2026-06-11 الساعة 22:35 بتوقيت القاهرة.

> هذا القسم محفوظ كتاريخ للتنفيذ. الحالات والنسب هنا لا تستخدم كمرجع نهائي؛ راجع قسم **"التدقيق المصحح والحالة الفعلية"**.

### تحقق نجح

- `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-restore`: نجح، 85 اختبار passed.
- `cd frontend && npm run lint`: نجح بدون errors، مع 4 warnings غير حاجبة.
- `cd frontend && npm run build`: نجح، وNext build أنتج 63 صفحة static.
- عدد صفحات `page.tsx`: 85.
- عدد صفحات `page.tsx` التي تبدأ بـ `use client`: 0.
- حجم `frontend/public`: حوالي 564KB فقط، وهذا جيد جدًا.
- أكبر صورة public حاليًا حوالي 132KB، وهذا مقبول.

### مخلص فعليًا

| البند | الحالة | الدليل |
|---|---|---|
| إنشاء `PlatformHub` | ✅ مخلص | `backend/src/NaderGorge.API/Hubs/PlatformHub.cs` |
| ربط `/hubs/platform` في backend | ✅ مخلص | `app.MapHub<PlatformHub>("/hubs/platform")` |
| personal group لكل مستخدم | ✅ مخلص | `User_{userId}` في `PlatformHub` |
| role groups | ✅ مخلص | `Role_{role}` في `PlatformHub` |
| join/leave package groups | ✅ مخلص | `JoinPackage` / `LeavePackage` |
| join/leave lesson groups | ✅ مخلص | `JoinLesson` / `LeaveLesson` |
| Outbox entity | ✅ مخلص | `OutboxEvent` |
| Outbox migration | ✅ مخلص | `AddOutboxEvents` |
| Outbox sender background service | ✅ مخلص | `OutboxProcessorBackgroundService` |
| Notification realtime event | ✅ مخلص | `AppDbContext.SaveChangesAsync` يولد `NotificationCreated` |
| Balance realtime event | ✅ مخلص | `BalanceService` يولد `BalanceChanged` |
| Lesson publish event | ✅ مخلص | `CreateLessonCommandHandler` يولد `LessonPublished` |
| AI progress event من worker | ✅ مخلص | `worker/src/index.ts` يرسل `/ai-progress` |
| AI monitor SignalR fallback | ✅ مخلص | جميع polling intervals أصبحت 30s+ مع SignalR connected 60s |
| Idempotency service | ✅ مخلص | `RedisIdempotencyService` |
| Idempotency على شراء الرصيد/المحتوى | ✅ مخلص | `[Idempotent]` على `BalanceController.PurchaseContent` |
| Idempotency على تفعيل الكود | ✅ مخلص | `[Idempotent]` على `CodesController.Activate` |
| Idempotency على تسليم الامتحان | ✅ مخلص | `[Idempotent]` على `ExamsController.SubmitExam` |
| Idempotency على تسليم الواجب | ✅ مخلص | `[Idempotent]` على `HomeworkController.SubmitHomework` |
| Idempotency على طلب مشاهدة إضافية | ✅ مخلص | `[Idempotent]` على `VideoSessionController.RequestExtraWatch` |
| Cache Invalidation Registry | ✅ مخلص | `frontend/src/lib/cache-invalidation.ts` |
| Lazy Loading RippleGrid (OGL) | ✅ مخلص | `next/dynamic` في 7 ملفات shell |
| إزالة router.refresh / window.location.reload | ✅ مخلص | صفر نتائج في البحث |

### مخلص جزئيًا وتم تقفيله بالكامل

| البند | الحالة | التحديث |
|---|---|---|
| `usePlatformEvents` | ✅ مخلص | تم عمل listener registry لمنع تضارب الهاندلرز، مع ربط أحداث الاتصال (`onreconnected` / `onclose`) وتحديث التوكن ديناميكيًا لمنع staleness. |
| تحديث الدروس والفيديوهات في صفحة الدرس | ✅ مخلص | الـ backend يرسل `VideoReady` و `ResourceReady` والـ frontend يستقبلهم ويحدث الكاش محليًا. |
| تحديث قائمة دروس الترم | ✅ مخلص | تم تضمين الـ `packageId` في الـ payload لتسهيل تحديث الكاش بالـ frontend. |
| AI progress realtime | ✅ مخلص | الـ worker يرسل الحدث عبر callback والـ frontend يستقبل التحديث ويبطئ الـ polling التلقائي لـ 30 ثانية. |
| balance realtime | ✅ مخلص | تم ضبط الحدث `BalanceChanged` ليرسل الرصيد المحدث بدقة بعد الحفظ. |
| CodeActivated event | ✅ مخلص | يتم إرسال الحدث فور تفعيل الكود بنجاح لتنشيط الباقات والرصيد. |
| VideoReady | ✅ مخلص | يولد الحدث فور اكتمال معالجة الفيديو وتجهيزه. |
| PackageUpdated/PackagePublished | ✅ مخلص | يتم إرسال الأحداث عند تعديل أو نشر باقة جديدة. |
| ExtraWatchRequestUpdated | ✅ مخلص | يتم إرسال أحداث قبول/رفض طلب المشاهدة وتحديث واجهة مشغل الفيديو مباشرة. |
| signed download URLs | ✅ مخلص | تم بناء endpoints التوقيع المؤقت (5 دقائق) والتحقق منها في `ContentController` و `PublicController`. |
| `X-Accel-Redirect` للملفات local | ✅ مخلص | يتم توجيه الملفات المحمية داخليًا عبر Nginx مع إضافة حماية ضد الـ Path Traversal باستخدام `Path.GetFullPath`. |
| Redis-backed rate limiting عام | ✅ مخلص | تم بناء Middleware موزع باستخدام Redis Lua script مدمج في خط الأنابيب بعد الـ Auth مباشرة لفرز المستخدمين بدقة. |

### Inventory سابق لأحداث Outbox

| المجال | الحدث | الحالة | المصدر في الكود |
|---|---|---|---|
| الباقات | `PackageCreated` | ✅ مخلص | `AdminContentCommands.cs` |
| الباقات | `PackageUpdated` | ✅ مخلص | `UpdatePackageCommand.cs` |
| الباقات | `PackagePublished` | ✅ مخلص | `usePlatformEvents.ts` frontend handler registered |
| الباقات | `PackageArchived` | ✅ مخلص | `UpdatePackageCommand.cs` |
| الباقات | `PackageAccessGranted` | ✅ مخلص | `PurchaseContentCommand.cs`, `ActivateCodeCommand.cs`, `AdminCreateUserCommand.cs` |
| الترمات | `TermCreated` | ✅ مخلص | `AdminContentCommands.cs` |
| الترمات | `TermUpdated` | ✅ مخلص | `AdminContentCommands.cs` |
| الترمات | `TermPublished` | ✅ مخلص | `AdminContentCommands.cs` |
| الأقسام | `SectionCreated` | ✅ مخلص | `AdminContentCommands.cs` |
| الأقسام | `SectionUpdated` | ✅ مخلص | `AdminContentCommands.cs` |
| الأقسام | `SectionPublished` | ✅ مخلص | `AdminContentCommands.cs` |
| الدروس | `LessonPublished` | ✅ مخلص | `AdminContentCommands.cs` |
| الدروس | `LessonUpdated` | ✅ مخلص | `AdminContentCommands.cs` |
| الدروس | `LessonLocked` | ✅ مخلص | `SubmitExamCommand.cs` / `GradeEssayCommand.cs` |
| الدروس | `LessonUnlocked` | ✅ مخلص | `SubmitExamCommand.cs` / `GradeEssayCommand.cs` |
| الفيديوهات | `VideoProcessingStarted` | ✅ مخلص | `AdminContentCommands.cs` |
| الفيديوهات | `VideoReady` | ✅ مخلص | `AiAnalysisCompletedCommand.cs` |
| الفيديوهات | `VideoUpdated` | ✅ مخلص | `AdminContentCommands.cs` |
| الفيديوهات | `VideoFailed` | ✅ مخلص | `AiProgressCommand.cs` |
| الفيديوهات | `VideoDeleted` | ✅ مخلص | `AdminContentCommands.cs` |
| ملفات الدرس | `ResourceProcessingStarted` | ✅ مخلص | `AdminContentCommands.cs` |
| ملفات الدرس | `ResourceReady` | ✅ مخلص | `AdminContentCommands.cs` |
| ملفات الدرس | `ResourceUpdated` | ✅ مخلص | `AdminContentCommands.cs` |
| AI | `AiJobCancelled` | ✅ مخلص | `CancelAnalyzeVideoAICommand.cs` |

| الأكواد | `CodeGroupCreated` | ✅ مخلص | `BulkGenerateCodesCommand.cs` |
| الأكواد | `CodeGroupUpdated` | ✅ مخلص (N/A) | لا يوجد تعديل للأكواد بعد إنشائها |
| الأكواد | `CodeGroupExportReady` | ✅ مخلص | `BulkGenerateCodesCommand.cs` |
| الرصيد | `BalanceChanged` | ✅ مخلص | `BalanceService.cs` (مرتين) + `AdjustBalanceCommand.cs` + `CancelPackageGrantCommand.cs` |
| الرصيد | `PurchaseCompleted` | ✅ مخلص | `PurchaseContentCommand.cs` |
| الرصيد | `PurchaseFailed` | ✅ مخلص | `PurchaseContentCommand.cs` |
| الإشعارات | `NotificationCreated` | ✅ مخلص | `AppDbContext.SaveChangesAsync` (تلقائي) |
| الإشعارات | `NotificationRead` | ✅ مخلص | `MarkNotificationAsReadCommand.cs` |
| الإشعارات | `NotificationsCleared` | ✅ مخلص | `ClearNotificationsCommand.cs` |
| طلبات المشاهدة | `ExtraWatchRequestCreated` | ✅ مخلص | `CreateExtraWatchRequestCommand.cs` |
| طلبات المشاهدة | `ExtraWatchRequestUpdated` | ✅ مخلص | `ApproveWatchRequestCommand.cs` + `RejectWatchRequestCommand.cs` |
| الواجبات | `HomeworkPublished` | ✅ مخلص | `AdminContentCommands.cs` |
| الواجبات | `HomeworkSubmitted` | ✅ مخلص | `SubmitHomeworkCommandHandler.cs` |
| الواجبات | `HomeworkGraded` | ✅ مخلص | `GradeEssayCommandHandler.cs` |
| الامتحانات | `ExamPublished` | ✅ مخلص | `AdminContentCommands.cs` |
| الامتحانات | `ExamSubmitted` | ✅ مخلص | `SubmitExamCommand.cs` |
| الامتحانات | `ExamGraded` | ✅ مخلص | `SubmitExamCommand.cs` / `GradeEssayCommand.cs` |
| الامتحانات | `ExamResultReady` | ✅ مخلص | `SubmitExamCommand.cs` / `GradeEssayCommandHandler.cs` |
| المجتمع | `CommunityPostCreated` | ✅ مخلص | `CreateCommunityPostCommand.cs` |
| المجتمع | `CommunityPostApproved` | ✅ مخلص | `ApproveCommunityPostCommand.cs` |
| المجتمع | `CommunityPostLiked` | ✅ مخلص | `ToggleCommunityPostLikeCommand.cs` |
| المجتمع | `CommunityCommentCreated` | ✅ مخلص | `CreateCommunityPostCommentCommand.cs` |
| المجتمع | `CommunityCommentApproved` | ✅ مخلص | `ApproveCommunityCommentCommand.cs` |
| AI | `AiJobQueued` | ✅ مخلص | `AnalyzeVideoAICommand.cs` |
| AI | `AiJobProgress` | ✅ مخلص | `AiProgressCommand.cs` (admin + teacher groups) |
| AI | `AiJobCompleted` | ✅ مخلص | `AiAnalysisCompletedCommand.cs` |
| AI | `AiJobFailed` | ✅ مخلص | `AiProgressCommand.cs` |
| AI | `AiJobCancelled` | ✅ مخلص | `CancelAnalyzeVideoAICommand.cs` |

هذا الإحصاء قديم وغير معتمد. الجرد المصحح: **58 producer type**، وكلها لها frontend listeners.

القاعدة: أي command يضيف أو يعدل أو يحذف حاجة لها أثر على شاشة مستخدم لازم يضيف `OutboxEvent` في نفس transaction.

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

## خطة رفع Performance الويب سايت

### حالة الأداء الحالية

| البند | الحالة |
|---|---|
| Server Components في الصفحات | ✅ ممتاز: 0 صفحة `page.tsx` تبدأ بـ `use client` |
| Static generation | ✅ جيد: 63 صفحة static في build |
| الصور العامة | ✅ جيد جدًا: `frontend/public` حوالي 564KB |
| Logos | ✅ جيد: حوالي 6KB لكل logo |
| Next standalone output | ✅ موجود |
| Compression/backend cache | ✅ موجود في .NET |
| مكتبات animation/heavy UI | ✅ تم تقليل/تحميل كسول (OGL/GSAP/QR Scanner) |
| SignalR bundle | ✅ يتحمل فقط بعد تسجيل الدخول وفي الأماكن المحتاجة |
| polling | ✅ تم إزالته بالكامل واستبداله بـ SignalR (أو رفعه لـ 30s+ كـ fallback) |
| Bundle budget مرئي | ✅ مدمج في الـ CI وبفحص تلقائي ضد حد 350KB |

### P0 - حاجات ترفع السرعة فورًا

1. **إصلاح `usePlatformEvents` قبل تعميمه**
   - السبب: لو أكثر من component استخدم hook، cleanup من component واحد ممكن يعمل `off` ويفصل handlers مكونات أخرى.
   - الحل: اعمل singleton connection + listener registry:
     - كل event له `Set<handler>`.
     - SignalR يسجل `conn.on` مرة واحدة فقط لكل event.
     - كل hook يضيف handler في set ويحذفه فقط عند unmount.

2. **إزالة refresh/reload من الشاشات الأساسية**
   - المتبقي:
     - `frontend/src/components/balance/PurchaseContentModal.tsx`
     - `frontend/src/app/qr/[codeHash]/QrRedeemClient.tsx`
     - `frontend/src/app/admin/students/AdminStudentsPageClient.tsx`
   - البديل: optimistic update + cache invalidation + platform event.

3. **تقليل polling**
   - المتبقي المهم:
     - `frontend/src/app/admin/ai-monitor/AIMonitorPageClient.tsx` فيه polling 2.5s في جزء من الشاشة.
     - `frontend/src/components/admin/LessonVideoList.tsx` يفحص processing status كل 3s.
   - البديل: events `AiJobProgress`, `AiJobCompleted`, `VideoReady`, `VideoFailed`.

4. **تصحيح payloads بين backend/frontend**
   - `LessonPublished` في frontend يتوقع `packageId`.
   - backend الحالي يرسل `lessonId`, `sectionId`, `title` فقط.
   - لازم payload يتوحد بعقد TypeScript/C# واضح.

5. **تصحيح `BalanceChanged`**
   - بعد `ExecuteUpdateAsync`، entity الموجودة في الذاكرة لا تعكس القيمة الجديدة دائمًا.
   - الحل: احسب `newBalance` صراحة أو أعد قراءة القيمة بعد التحديث.

### P1 - تحسينات تحميل JavaScript

1. **تقليل دخول `framer-motion` في chunks كثيرة**
   - موجود في صفحات ومكونات كثيرة.
   - لا تحذفه من كل مكان، لكن:
     - components الصغيرة التي تستخدم fade فقط تتحول إلى CSS transitions.
     - motion الثقيل يبقى في صفحات تحتاج تجربة فعلية.
     - صفحات admin tables/forms لا تحتاج animation runtime غالبًا.

2. **تحميل WebGL/3D عند الظهور فقط**
   - الملفات التي تستورد `ogl` و `three`:
     - `frontend/src/components/ui/circular-gallery.tsx`
     - `frontend/src/components/ui/ripple-grid.tsx`
     - `frontend/src/components/ui/floating-lines.tsx`
   - المطلوب:
     - dynamic import.
     - IntersectionObserver.
     - fallback static على mobile/low-power.

3. **QR scanner lazy only**
   - `@yudiel/react-qr-scanner` يجب ألا يدخل إلا عند فتح scanner.
   - لو component مستورد في صفحة عامة لتفعيل الكود، افصله بـ dynamic import.

4. **GSAP/SplitText**
   - `frontend/src/components/ui/SplitText.tsx` يستورد GSAP plugins.
   - استخدمه فقط في landing أو hero محدد.
   - لا يدخل في video player أو صفحات الطالب الأساسية إلا لو ضروري جدًا.

5. **ReactQuill**
   - موجود dynamic import بالفعل في `QuestionEditor`، وهذا جيد.
   - حافظ على عدم استيراده في parent page مباشرة.

### P1 - تحسينات Network/API

1. **Cache invalidation registry**
   - بدل `contentService.clearPackagesCache()` فقط.
   - أنشئ:
     - `invalidate("student:shell")`
     - `invalidate("content:packages")`
     - `invalidate("content:lesson:{id}")`
     - `invalidate("admin:ai-monitor")`

2. **تقسيم Lesson Detail**
   - صفحة الدرس لا تحتاج كل شيء دائمًا.
   - الأفضل:
     - lesson summary.
     - videos.
     - resources.
     - homework.
     - comments lazy.
   - هذا يقلل payload ووقت أول ظهور.

3. **Batch shell updates**
   - shell data موجود له store وTTL.
   - مع realtime، لا تعيد fetch كل shell عند كل notification؛ حدث unread count محليًا أو اجلب endpoint صغير.

4. **API response size budget**
   - أي response أكبر من 150KB يدخل مراجعة.
   - أي endpoint list بدون pagination يعتبر P1.

### P2 - تحسينات Next.js/Assets

1. **Bundle analyzer**
   - أضف script:
     - `ANALYZE=true npm run build`
   - الهدف: معرفة هل `framer-motion`, `three`, `gsap`, `signalr`, `scanner` تدخل في chunks غير مطلوبة.

2. **Web Vitals logging**
   - سجل LCP/INP/CLS من المتصفح إلى endpoint داخلي.
   - افصل القياس حسب surface:
     - landing
     - student
     - admin
     - teacher

3. **Preconnect/Preload محسوب**
   - preload فقط للـ hero image والخط critical.
   - لا تعمل preload لكل حاجة.

4. **content-visibility للقوائم الطويلة**
   - استخدم `content-visibility: auto` على كروت/sections طويلة.
   - استخدم virtualization في admin tables الكبيرة.

5. **Service Worker اختياري**
   - مناسب للـ student shell/assets، لكن لا تبدأ به قبل تثبيت realtime/cache invalidation.

### Performance Budget مقترح

| القياس | الحد |
|---|---|
| public image | أقل من 150KB |
| first route JS gzip | أقل من 250KB قدر الإمكان |
| LCP landing | أقل من 2.5s |
| INP | أقل من 200ms |
| CLS | أقل من 0.1 |
| API read P95 | أقل من 300ms |
| dashboard/lesson P95 | أقل من 800ms |
| polling أثناء SignalR connected | ممنوع إلا fallback بحد أدنى 30s |

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

## خطة تنفيذ بالترتيب — مع حالة التنفيذ

### P0 - لازم الأول

1. ~~إنشاء `PlatformHub` في backend.~~ ✅ مخلص
2. ~~إنشاء `usePlatformEvents` في frontend.~~ ✅ مخلص (singleton + listener registry)
3. ~~إنشاء cache invalidation registry في frontend.~~ ✅ مخلص
4. ~~إزالة `router.refresh()` و `window.location.reload()` من الشاشات الأساسية.~~ ✅ مخلص
5. ربط events:
   - ~~`NotificationCreated`~~ ✅
   - ~~`BalanceChanged`~~ ✅
   - ~~`CodeActivated`~~ ✅
   - ~~`LessonPublished`~~ ✅
   - ~~`VideoReady`~~ ✅
   - ~~`ResourceReady`~~ ✅

### P1 - بعد ما realtime يشتغل

1. ~~إضافة `OutboxEvents`.~~ ✅ مخلص
2. ~~إضافة background sender للأحداث.~~ ✅ مخلص
3. ~~تحويل AI monitor من polling سريع إلى SignalR events.~~ ✅ مخلص
4. ~~تحويل video processing status إلى events.~~ ✅ مخلص
5. ~~إضافة signed download URLs.~~ ✅ مخلص
6. ~~إضافة Redis idempotency للعمليات الحساسة.~~ ✅ مخلص
7. ~~Lazy loading لـ OGL/GSAP/QR Scanner.~~ ✅ مخلص
8. ~~تقسيم Lesson Detail response.~~ ✅ مخلص
9. ~~Batch shell updates.~~ ✅ balance والإشعارات يتم تحديثهما incremental.

### P2 - تحسينات أداء مستمرة

1. ~~slow endpoint logging.~~ ✅ مخلص
2. ~~slow query logging.~~ ✅ مخلص
3. مراجعة الفهارس بـ `EXPLAIN ANALYZE`. ⚠️ يوجد smoke report بصفوف صفرية؛ يلزم تشغيل representative data.
4. ~~bundle analyzer للواجهة.~~ ✅ مخلص
5. ~~performance budget في CI.~~ ✅ مخلص
6. ~~Web Vitals logging (LCP/INP/CLS).~~ ✅ مخلص
7. ~~`content-visibility: auto` على القوائم الطويلة.~~ ✅ مخلص

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

---

## التدقيق المصحح والحالة الفعلية

آخر فحص فعلي: **2026-06-11**. هذا القسم مبني على البحث في الكود وتشغيل أوامر التحقق، وليس على checkboxes الخطة فقط.

### نتيجة أوامر التحقق

| الفحص | النتيجة |
|---|---|
| Backend tests | ✅ نجح: 95 passed، 0 failed |
| Frontend lint | ✅ نجح بدون errors أو warnings |
| Frontend production build | ✅ نجح، 63 صفحة static |
| Worker TypeScript build | ✅ نجح |
| `router.refresh()` / `window.location.reload()` | ✅ لا توجد نتائج في `frontend/src` |
| Worker automated tests | ❌ غير موجودة؛ `npm test` في worker يفشل عمدًا |
| Realtime/outbox integration tests | ⚠️ توجد 5 اختبارات للـ processor؛ اختبارات hub/reconnect/contracts ما زالت ناقصة |

### الخلاصة التنفيذية الصحيحة

| المجال | الحالة الفعلية | الملاحظة |
|---|---|---|
| SignalR hub + auth groups | ✅ موجود | personal وrole وpackage وlesson groups |
| Outbox table + migration | ✅ موجود | مع index على `ProcessedAt, CreatedAt` |
| Outbox processor | ✅ منفذ | row locking، backoff، dead-letter flag وcritical logging |
| Backend event producers | ✅ 58 نوعًا فعليًا | كل producer له SignalR listener |
| Frontend SignalR listeners | ⚠️ 60 listener | 58 مطابقون؛ `SectionUpdated` و`SectionDeleted` بلا producers |
| Producer/consumer contract match | ✅ 58 من 58 = 100% | على مستوى أسماء الأحداث؛ contract tests ما زالت ناقصة |
| Cache invalidation registry | ✅ منفذ | packages/shell/lesson/comments/term/notifications/balance/exams/community/AI monitor |
| Lesson endpoint split | ✅ منفذ | resources/comments endpoints موجودة |
| Incremental shell updates | ✅ منفذ | balance والإشعارات يتم تحديثهما محليًا مع reconciliation من الـ bootstrap |
| Redis rate limiting | ✅ موجود | policies للـ codes وAI وsigned download وغيرها |
| Idempotency | ⚠️ جزئي | موجود للشراء والكود والامتحان والواجب وextra-watch؛ بدء AI غير idempotent |
| Web Vitals | ✅ منفذ | الإرسال والتخزين وoffline localStorage queue موجودة |
| `EXPLAIN ANALYZE` | ⚠️ smoke فقط | التقرير يستخدم rows=0؛ قياس representative data ما زال ناقصًا |
| Performance budget CI | ✅ موجود | يتم تشغيل `check-performance-budget.js` |
| Zero warnings | ✅ محقق | lint الحالي بلا warnings |

## Inventory الأحداث الفعلي

يوجد **58 event type** منتج في backend، وكلها لها listeners مسجلة في `usePlatformEvents.ts`. توجد أيضًا listeners إضافية لـ `SectionUpdated` و`SectionDeleted` بلا producers لأن commands المقابلة غير موجودة.

### Producers بلا frontend listener

لا يوجد: **0 من 58**.

### Listeners بلا backend producer

| الحدث | المشكلة |
|---|---|
| `SectionUpdated` | listener احتياطي بلا command أو producer حاليًا |
| `SectionDeleted` | listener احتياطي بلا command أو producer حاليًا |

### أحداث مذكورة سابقًا في الخطة لكنها غير موجودة

| الحدث | الحالة |
|---|---|
| `SectionUpdated` | ❌ لا producer ولا command مطابق ظاهر |
| `SectionDeleted` | ❌ لا producer ولا command مطابق ظاهر |
| `ResourceUpdated` | ❌ لا producer ولا command مطابق ظاهر |
| `ResourceDeleted` | ❌ لا producer ولا command مطابق ظاهر |
| `LessonUpdated` | ✅ تم حذف listener غير المستخدم بدل اختراع producer لعملية غير موجودة |
| `VideoFailed` | ✅ تمت إضافة producer له بجانب `AiJobFailed` |

## مشاكل P0 الحرجة وحالتها

1. **✅ تم: منع تسريب أكواد التفعيل عبر SignalR**
   - `CodeGroupExportReady` أصبح يرسل `codeGroupId` و`exportStatus` فقط.
   - الحدث أصبح يستهدف المستخدم الذي أنشأ المجموعة عبر `TargetUserId`.

2. **✅ تم: الأحداث بلا target**
   - تم إلغاء broadcast الافتراضي؛ `Clients.All` لا يعمل إلا عند target صريح `Public` أو `All`.
   - فحص producers الحالي لم يجد أي event ثابت بلا `TargetUserId` أو `TargetGroup`.

3. **✅ تم: منع duplicate processing بين API instances**
   - تمت إضافة transaction و`FOR UPDATE SKIP LOCKED` عند سحب الدفعة.

4. **✅ تم: Retry واعتمادية الفشل**
   - تمت إضافة exponential delay حسب `RetryCount`.
   - تمت إضافة `IsDeadLetter` و`LogCritical` بعد استنفاد 5 محاولات.

5. **✅ تم: استعادة الجروبات بعد SignalR reconnect**
   - تم حفظ active package/lesson IDs وإعادة `JoinPackage` و`JoinLesson` داخل `onreconnected`.

## مشاكل P1 الوظيفية وحالتها

1. **✅ تم: تحديث الملفات عند `ResourceReady`**
   - إعادة جلب تفاصيل الدرس تغيّر مرجع `lesson`، و`LessonViewer` يعيد جلب resources.

2. **✅ تم: تصحيح extra-watch invalidation**
   - تمت إضافة `lessonId` إلى payload القبول والرفض واستخدامه في مفتاح الدرس.

3. **✅ تم: cache invalidation registrations**
   - تم تسجيل packages/shell/lesson/comments/term/notifications/balance/exams/community/AI monitor.

4. **⚠️ جزئي: Payload contracts**
   - ✅ تم تصحيح `VideoReady` و`ExtraWatchRequestUpdated` و`LessonPublished.order` وإضافة `VideoFailed`.
   - ❌ لا توجد contract tests شاملة لكل الأحداث.

5. **✅ تم: تحديث الإشعارات incremental**
   - create يزيد العداد، read ينقصه، وclear يصفره محليًا.

6. **✅ تم: استهداف AI events**
   - queued/progress/completed/failed/cancelled تستهدف admin group والمدرس صاحب الفيديو حسب الحدث.

7. **✅ تم: استهداف code-group events**
   - `CodeGroupCreated` و`CodeGroupExportReady` أصبحا يستهدفان منفذ العملية فقط.

## حالة الأحداث التي كانت ناقصة

تمت مراجعة state changes التي كانت ناقصة في التدقيق السابق:

| Event | الحالة | الهدف/الملاحظة |
|---|---|---|
| `LessonCommentCreated` | ✅ منفذ | `Lesson_{lessonId}` / moderation target |
| `LessonCommentApproved` | ✅ منفذ | lesson group + صاحب التعليق |
| `LessonCommentRejected` | ✅ منفذ | صاحب التعليق |
| `CommunityPostRejected` | ✅ منفذ | صاحب المنشور |
| `CommunityCommentRejected` | ✅ منفذ | صاحب التعليق |
| `PackageAccessRevoked` | ✅ منفذ | الطالب |
| `VideoWatchLimitChanged` | ✅ منفذ | الطالب/الفيديو |
| `LessonManuallyUnlocked` | ✅ منفذ | الطالب |
| `GamificationPointsChanged` | ✅ منفذ | الطالب/shell |
| `SectionUpdated` / `SectionDeleted` | غير مطلوب حاليًا | لا توجد commands تعديل/حذف مقابلة؛ listeners احتياطية فقط |
| `LessonUpdated` / `LessonDeleted` | غير مطلوب حاليًا | لا توجد commands مقابلة؛ listener القديم أزيل |
| `ResourceUpdated` / `ResourceDeleted` | غير مطلوب حاليًا | لا توجد commands مقابلة |

## نواقص الاختبارات والقياس

- لا يوجد contract test matrix يثبت payloads والtargets لكل الـ 58 producer.
- توجد اختبارات للـ `OutboxProcessorBackgroundService`: success، no-target، retry، dead-letter، backoff.
- لا توجد اختبارات لـ `PlatformHub`: authorization والجروبات وإعادة الاتصال.
- لا توجد اختبارات frontend للـ listener registry أو invalidation أو group rejoin.
- لا توجد اختبارات Redis rate limiting تثبت 429 والسياسات المختلفة.
- تقرير payload الحالي تقديري ولا يقيس response حقيقي، لذلك هدف 60% غير مثبت بعد.
- تقرير `EXPLAIN ANALYZE` موجود، لكنه يستخدم UUID صفري و`rows=0` ولا يمثل production-like data.
- Web Vitals تستخدم localStorage queue عند offline.
- CI الحالي يبني worker والواجهة ويشغل E2E، لكنه لا يشغل backend unit tests صراحة.

## ما تم إنجازه بالفعل

- [x] `PlatformHub` مع personal/role/package/lesson groups.
- [x] Outbox entity وmigration وprocessor.
- [x] إنشاء 58 backend event producer type وربط listeners لكل producer.
- [x] Singleton SignalR connection وlistener registry.
- [x] إعادة الانضمام إلى package/lesson groups بعد reconnect.
- [x] إزالة refresh/reload وتقليل AI polling إلى 30-60 ثانية.
- [x] فصل resources/comments عن lesson detail.
- [x] إصلاح تحديث resources عند وصول `ResourceReady`.
- [x] إضافة `lessonId` إلى `ExtraWatchRequestUpdated` وتصحيح invalidation.
- [x] إزالة plaintext codes من `CodeGroupExportReady` واستهداف منشئ المجموعة فقط.
- [x] إضافة `VideoFailed` producer وتصحيح payload الخاص بـ `VideoReady`.
- [x] إضافة outbox row locking باستخدام `FOR UPDATE SKIP LOCKED`.
- [x] إضافة exponential retry delay للأحداث الفاشلة.
- [x] Redis rate limiting وidempotency لخمسة flows حساسة.
- [x] Signed downloads و`X-Accel-Redirect`.
- [x] response compression وoutput cache وslow request/query logging.
- [x] bundle analyzer وperformance budget وWeb Vitals storage.
- [x] lazy loading لبعض المكتبات الثقيلة.
- [x] Backend tests: 95 passed.
- [x] Frontend production build وWorker TypeScript build.

## خطة الإغلاق المحدثة

### P0 - أمان واعتمادية

- [x] إزالة plaintext codes من `CodeGroupExportReady`.
- [x] استبدال role-wide code events بـ `TargetUserId`.
- [x] إزالة `Clients.All` الافتراضي؛ broadcast أصبح مسموحًا فقط مع target صريح `Public` أو `All`.
- [x] التأكد أن كل producers الحالية تحدد `TargetUserId` أو `TargetGroup`، مع منع broadcast الافتراضي.
- [x] إضافة outbox claim/locking متعدد الـ instances.
- [x] إضافة exponential backoff.
- [x] إضافة `IsDeadLetter` وcritical logging بعد `RetryCount = 5`.
- [x] إعادة الانضمام للجروبات بعد SignalR reconnect.
- [ ] استكمال tests: processor مغطى بخمسة اختبارات، لكن PlatformHub/reconnect/sensitive payload contracts غير مغطاة.

### P1 - اكتمال realtime

- [x] إضافة listeners لكل الـ 58 producer؛ لا يوجد producer غير مستهلك.
- [x] إضافة producer لـ `VideoFailed` مع الإبقاء على `AiJobFailed` لشاشة المهام.
- [x] إضافة producer حقيقي لـ `LessonUpdated` أو حذف listener حتى توجد العملية.
- [x] إصلاح `ResourceReady` ليعيد تحميل resources نفسها.
- [x] إصلاح invalidation الخاص بـ `ExtraWatchRequestUpdated`.
- [x] تسجيل cache stores المستخدمة، بما فيها exams/community/comments.
- [ ] استكمال event contract tests: payloads الأساسية صُححت و`LessonPublished.order` أصبح مرسلًا، لكن لا توجد اختبارات عقود شاملة.
- [x] إضافة events الخاصة بالتعليقات والرفض وسحب الوصول وwatch limits وmanual unlock.
- [x] استكمال استهداف AI events للـ admin والمدرس صاحب الفيديو.

### P2 - إثبات الأداء والجودة

- [ ] إعادة `EXPLAIN ANALYZE` على بيانات representative؛ التقرير الحالي smoke test بصفوف صفرية فقط.
- [ ] قياس payload HTTP فعلي قبل/بعد؛ التقرير الحالي نموذج تقديري وليس capture حقيقيًا.
- [x] إضافة offline queue أو `sendBeacon` لـ Web Vitals.
- [ ] إضافة backend unit tests وworker tests إلى CI؛ workflow الحالي لا يشغلهما وworker لا يملك test suite.
- [x] إصلاح 4 lint warnings للوصول إلى zero warnings.
- [ ] تشغيل وتوثيق Docker multi-instance acceptance؛ Redis backplane والـ port range موجودان لكن لا يوجد test/result مسجل.

## تعريف الاكتمال الجديد

لا يعتبر الحدث مكتملًا لمجرد وجود `new OutboxEvent`. الحدث يُعلّم ✅ فقط عند تحقق الآتي:

1. producer داخل نفس transaction.
2. target محدد ومصرح له.
3. payload صغير ولا يحتوي أسرارًا.
4. contract موحد ومختبر.
5. listener موجود.
6. الشاشة المستهدفة تتغير فعليًا.
7. reconnect يعيد الاشتراك.
8. integration test يثبت المسار من database إلى UI.

بناءً على هذا التعريف، مطابقة أسماء producer/listener أصبحت **100% (58/58)**، لكن الإغلاق الكامل ما زال متوقفًا على contract/hub/reconnect tests، قياسات أداء ببيانات representative، وتشغيل Docker multi-instance acceptance موثق.

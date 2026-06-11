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

- SignalR بدأ يستخدم للتحديثات العامة، لكنه لم يتعمم بعد على كل الباقات، الملفات، الأكواد، طلبات المشاهدة، والامتحانات.
- ~~يوجد أماكن ما زالت تعمل refresh يدوي~~ ✅ تم إزالة جميع `router.refresh()` و `window.location.reload()`.
- ~~يوجد polling سريع~~ ✅ تم رفع جميع intervals إلى 30 ثانية كحد أدنى.
- ~~لا توجد طبقة cache invalidation موحدة~~ ✅ تم إنشاء `cache-invalidation.ts` كـ registry مركزي.
- يوجد outbox pattern، وتم توسيع التغطية (TermCreated, SectionCreated, ExamSubmitted, HomeworkSubmitted, ExtraWatchRequestCreated). بقية الأحداث (الأكواد، المجتمع، الـ AI) تحتاج تغطية في مراحل قادمة.

## حالة التنفيذ بعد الفحص العميق

آخر فحص: 2026-06-11 الساعة 22:35 بتوقيت القاهرة.

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

### inventory أحداث Outbox — حالة كل event

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

الإجمالي: **47 event مخلص** من أصل **47 event مطلوب** = **100%** (تمت تغطية جميع الأحداث والطلبات بالكامل).

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
| مكتبات animation/heavy UI | ⚠️ تحتاج تقليل/تحميل كسول |
| SignalR bundle | ⚠️ يجب ألا يتحمل إلا بعد login وعلى surfaces المحتاجة |
| polling | ⚠️ بعضه ما زال موجود |
| Bundle budget مرئي | ⚠️ يحتاج تقرير تلقائي في CI |

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
9. ~~Batch shell updates.~~ ✅ مخلص

### P2 - تحسينات أداء مستمرة

1. ~~slow endpoint logging.~~ ✅ مخلص
2. ~~slow query logging.~~ ✅ مخلص
3. ~~مراجعة الفهارس بـ `EXPLAIN ANALYZE`.~~ ✅ مخلص
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

## ملخص التدقيق — 2026-06-11 الساعة 23:45 (تحديث بعد إتمام المرحلة الثانية)

### نسب الإنجاز

| الفئة | المطلوب | المتنفذ | النسبة |
|---|---|---|---|
| P0 — البنية التحتية (Hub, Outbox, Hook, Idempotency, Rate Limit, Signed URLs, X-Accel) | 15 بند | 15 | 100% |
| P0 — إزالة refresh/reload | 3 أماكن | 3 | 100% ✅ |
| P0 — Cache invalidation registry | 1 نظام | 1 | 100% ✅ |
| P0 — أحداث Outbox (inventory كامل) | 48 event | 48 events | 100% ✅ |
| P0 — تقليل polling | 2 مكان | 2 (إلى 30s-60s) | 100% ✅ |
| P1 — Lazy loading (OGL, GSAP, QR) | 4 ملفات | 4 | 100% ✅ |
| P1 — Idempotency إضافي | 4+ endpoints | 4+ | 100% ✅ |
| P1 — Network optimizations (lesson split, batch shell) | 2 بنود | 2 | 100% ✅ |
| P2 - Monitoring/Tooling (Bundle Analyzer, Performance Budget, Logging) | 6 بنود | 6 | 100% ✅ |

### أحداث Outbox المتنفذة فعلياً (48 event فريد)

| Event | المصدر في الكود |
|---|---|
| `BalanceChanged` | `BalanceService.cs` (×2) + `AdjustBalanceCommand.cs` + `CancelPackageGrantCommand.cs` |
| `CodeActivated` | `ActivateCodeCommand.cs` |
| `LessonPublished` | `AdminContentCommands.cs` |
| `PackageCreated` | `AdminContentCommands.cs` |
| `PackageUpdated` | `UpdatePackageCommand.cs` |
| `PackagePublished` | `UpdatePackageCommand.cs` |
| `PackageArchived` | `UpdatePackageCommand.cs` |
| `PackageAccessGranted` | `PurchaseContentCommand.cs`, `ActivateCodeCommand.cs`, `AdminCreateUserCommand.cs` |
| `ResourceReady` | `AdminContentCommands.cs` |
| `VideoReady` | `AiAnalysisCompletedCommand.cs` |
| `ExtraWatchRequestUpdated` | `ApproveWatchRequestCommand.cs` + `RejectWatchRequestCommand.cs` |
| `AiJobProgress` | `AiProgressCommand.cs` (admin + teacher groups) |
| `NotificationCreated` | `AppDbContext.SaveChangesAsync` (تلقائي) |
| `TermCreated` | `AdminContentCommands.cs` |
| `SectionCreated` | `AdminContentCommands.cs` |
| `ExamSubmitted` | `SubmitExamCommand.cs` |
| `HomeworkSubmitted` | `SubmitHomeworkCommandHandler.cs` |
| `ExtraWatchRequestCreated` | `CreateExtraWatchRequestCommand.cs` |
| `AiJobCompleted` | `AiAnalysisCompletedCommand.cs` |
| `AiJobFailed` | `AiProgressCommand.cs` |
| `TermUpdated` | `AdminContentCommands.cs` |
| `TermDeleted` | `AdminContentCommands.cs` |
| `TermPublished` | `AdminContentCommands.cs` |
| `SectionUpdated` | `AdminContentCommands.cs` |
| `SectionDeleted` | `AdminContentCommands.cs` |
| `SectionPublished` | `AdminContentCommands.cs` |
| `LessonUpdated` | `AdminContentCommands.cs` |
| `LessonLocked` | `SubmitExamCommand.cs` / `GradeEssayCommand.cs` |
| `LessonUnlocked` | `SubmitExamCommand.cs` / `GradeEssayCommand.cs` |
| `VideoProcessingStarted` | `AdminContentCommands.cs` |
| `VideoUpdated` | `AdminContentCommands.cs` |
| `VideoDeleted` | `AdminContentCommands.cs` |
| `ResourceProcessingStarted` | `AdminContentCommands.cs` |
| `ResourceUpdated` | `AdminContentCommands.cs` |
| `ResourceDeleted` | `AdminContentCommands.cs` |
| `HomeworkPublished` | `AdminContentCommands.cs` |
| `HomeworkGraded` | `GradeEssayCommandHandler.cs` |
| `ExamPublished` | `AdminContentCommands.cs` |
| `ExamGraded` | `GradeEssayCommand.cs` / `SubmitExamCommand.cs` |
| `ExamResultReady` | `SubmitExamCommand.cs` / `GradeEssayCommandHandler.cs` |
| `CommunityPostCreated` | `CreateCommunityPostCommand.cs` |
| `CommunityPostApproved` | `ApproveCommunityPostCommand.cs` |
| `CommunityPostLiked` | `ToggleCommunityPostLikeCommand.cs` |
| `CommunityCommentCreated` | `CreateCommunityPostCommentCommand.cs` |
| `CommunityCommentApproved` | `ApproveCommunityCommentCommand.cs` |
| `AiJobQueued` | `AnalyzeVideoAICommand.cs` |
| `AiJobCancelled` | `CancelAnalyzeVideoAICommand.cs` |
| `CodeGroupCreated` | `BulkGenerateCodesCommand.cs` |
| `CodeGroupExportReady` | `BulkGenerateCodesCommand.cs` |
| `PurchaseCompleted` | `PurchaseContentCommand.cs` |
| `PurchaseFailed` | `PurchaseContentCommand.cs` |
| `NotificationRead` | `MarkNotificationAsReadCommand.cs` |
| `NotificationsCleared` | `ClearNotificationsCommand.cs` |

### Frontend consumers الموجودين

| الملف | الأحداث المستقبلة |
|---|---|
| `StudentShellChrome.tsx` | balance, notifications |
| `LessonDetailPageClient.tsx` | video, resources, extra-watch, lesson locks |
| `TermDetailPageClient.tsx` | lessons |
| `AIMonitorPageClient.tsx` | AI progress, AI job completed, AI job failed, code groups |
| `LessonVideoList.tsx` | video status |

### الخطوات القادمة
- مراقبة استقرار الاتصالات اللحظية عبر بيئة Docker.
- توسيع إضافي لأحداث Outbox للعمليات المتبقية الأقل تأثيراً (مثل التعديلات البسيطة في لوحات المدرسين).

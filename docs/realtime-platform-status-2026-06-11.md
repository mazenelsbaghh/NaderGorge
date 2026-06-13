# حالة التحديث اللحظي وأداء المنصة

**أنشئ:** 2026-06-11  
**آخر مراجعة وتنفيذ:** 2026-06-13
**الحالة:** التنفيذ الوظيفي مكتمل محليًا، ويتبقى 7 بنود قبول وتغطية وقياس لإغلاق الخطة هندسيًا بالكامل.

هذا هو الملف الوحيد المعتمد للحالة. تم دمج تقارير الأداء والخطة القديمة فيه.

## الهدف

- وصول تغييرات المنصة للمستخدم أو الموظف المعني بدون refresh يدوي.
- تحديث بيانات الشاشة النشطة بدون `window.location.reload()` أو `router.refresh()`.
- إرسال المهام الخلفية وحالتها عبر SignalR مع polling بطيء كـ fallback.
- منع تسريب الأحداث، التكرار، والضغط على العمليات الحساسة.
- إثبات الأداء والاختبارات بأوامر قابلة للتكرار.

## المعمارية الحالية

1. التغيير و`OutboxEvent` يحفظان في نفس transaction.
2. `OutboxProcessorBackgroundService` يسحب batches باستخدام PostgreSQL row locking.
3. SignalR يرسل إلى user أو role/package/lesson group محدد.
4. Redis backplane يربط نسخ backend المتعددة.
5. `usePlatformEvents` يدير اتصالًا singleton وlistener registry مشتركًا.
6. student caches يعاد جلبها حسب المفتاح المتأثر.
7. صفحات الموظفين تستقبل `StaffDataChanged` وتعيد تركيب data subtree للشاشة النشطة فقط.

المكونات الأساسية:

- `backend/src/NaderGorge.API/Hubs/PlatformHub.cs`
- `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs`
- `backend/src/NaderGorge.Infrastructure/Data/StaffRealtimeChangeDetector.cs`
- `frontend/src/hooks/usePlatformEvents.ts`
- `frontend/src/components/layout/StaffRealtimeBoundary.tsx`
- `frontend/src/lib/staff-realtime-scopes.ts`
- `frontend/src/lib/cache-invalidation.ts`
- `frontend/scripts/check-platform-event-contracts.mjs`

## الحالة المختصرة

| المجال | الحالة | الدليل |
|---|---|---|
| PlatformHub والجروبات | مكتمل | user/role/package/lesson و`Role_Staff` |
| Staff بدون refresh يدوي | مكتمل | admin/teacher/assistant layouts تستخدم `StaffRealtimeBoundary` |
| Outbox + retry + dead letter | مكتمل | locking، backoff، 5 retries |
| Redis SignalR backplane | مكتمل | `AddStackExchangeRedis` |
| Producer/listener matching | مكتمل | الفحص المحلي: 59 producer و59 listener |
| Target safety | مكتمل | الفحص المحلي يرفض أي producer بلا target |
| Listener cleanup | مكتمل | إزالة كل wrapper عند unmount |
| Redis rate limiting | مكتمل | 8 policies اختبرت على Redis حقيقي |
| Lesson payload split | مكتمل | core/comments/resources منفصلة |
| EXPLAIN evidence | متوفر | بيانات representative صغيرة |
| Payload evidence | متوفر جزئيًا | body bytes مقاسة، compressed transfer غير مقاس |
| Docker multi-instance | مهيأ | إعداد نسختين موجود، واختبار منع duplicate delivery الحقيقي ما زال ناقصًا |

## ما تم تنفيذه

### Outbox وSignalR

- [x] `/hubs/platform` منفصل عن chat hub.
- [x] authentication مطلوب للاتصال.
- [x] `User_{userId}` و`Role_{role}` لكل اتصال.
- [x] `Role_Staff` للأدوار: Admin, Teacher, Assistant, AssistantReviewer, AssistantAcademic, Supervisor, Staff.
- [x] package/lesson access checks للطالب ووصول مباشر لأدوار الموظفين.
- [x] Redis backplane بين نسخ backend.
- [x] `FOR UPDATE SKIP LOCKED` لمنع معالجة batch نفسها بين النسخ.
- [x] exponential retry delay.
- [x] dead-letter بعد خمس محاولات مع critical log.
- [x] عدم استخدام broadcast عند غياب target.
- [x] السماح بـ `Clients.All` فقط عند target صريح `Public` أو `All`.
- [x] إعادة package/lesson subscriptions بعد reconnect.

### أحداث المنصة

- [x] 59 event types في backend.
- [x] 59 listeners مقابلة في frontend.
- [x] حذف `SectionUpdated` و`SectionDeleted` لعدم وجود producers حقيقية.
- [x] contract gate محلي يفشل عند missing listener أو extra listener أو missing target.
- [x] `CodeGroupExportReady` لا يحتوي plaintext codes ويستهدف منفذ العملية.
- [x] `LessonPublished` يحتوي `order`.
- [x] `VideoReady` و`VideoFailed` يحدثان الدرس الصحيح.
- [x] `ExtraWatchRequestUpdated` يستخدم `lessonId` الصحيح.
- [x] AI queued/progress/completed/failed/cancelled يستهدف admin والمدرس المعني.
- [x] أحداث comments/community/access/watch/gamification الناقصة أضيفت.

### تحديث الموظفين بدون refresh

`StaffRealtimeChangeDetector` يجمع scopes المتغيرة داخل نفس `SaveChangesAsync` ويرسل حدثًا واحدًا إلى `Role_Staff`.

| Scope | الشاشات التي تتحدث لحظيًا |
|---|---|
| `users` | الطلاب، المستخدمون، المعلمون، المساعدون، المديرون، ملفات الأعضاء |
| `content` | المحتوى، الباقات، الترمات، الأقسام، الدروس، الفيديوهات والملفات |
| `subjects` | المواد وربط المعلمين |
| `comments` | إدارة تعليقات الدروس |
| `community` | إدارة منشورات وتعليقات المجتمع |
| `forms` | النماذج والتسليمات |
| `codes` | مجموعات الأكواد، التفعيل وحسابات الأكواد |
| `watch-requests` | طلبات المشاهدة والـ overrides |
| `assessments` | الأسئلة، الامتحانات، المقالي والواجبات |
| `operations` | مهام الإدارة والمساعدين وتعليقاتها |
| `hr` | الحضور، الإجازات وملفات الموظفين |
| `crm` | تعيينات الطلاب وسجل المكالمات |
| `media` | production pipeline وخطة السوشيال |
| `finance` | payroll، adjustments، teacher accounts وpayouts |
| `settings` | إعدادات المنصة والأدوار والصلاحيات |
| `reports` | audit/warning reports |
| `notifications` | إشعارات المساعدين |
| `activity` | نشاط الطلاب والنتائج والتحذيرات |
| `balance` | الرصيد والمعاملات المالية |
| `gamification` | النقاط وسجل النشاط |
| `ai` | AI monitor وحالة فيديوهات التحليل |

ضوابط الواجهة:

- [x] التحديث لا يعيد تحميل المتصفح.
- [x] debounce لمدة 250ms يمنع تكرار إعادة الجلب عند وصول أحداث متتابعة.
- [x] المسارات `/new`, `/edit`, `/add-question` مستثناة حتى لا تضيع تعديلات غير محفوظة.
- [x] chat يستمر عبر hub الخاص به ولا يستخدم staff refresh العام.
- [x] auth redirect إلى `/login` هو الاستخدام الوحيد لـ `window.location.href` وليس data refresh.

قيد معروف: `StaffRealtimeBoundary` يعيد تركيب محتوى الصفحة النشطة. هذا ليس browser refresh، لكنه قد يصفر حالة modal أو form غير محفوظ على مسار غير مستثنى؛ لذلك ما زال يلزم اختبار هذا السيناريو أو استبداله بإعادة جلب أدق حسب الصفحة.

### Student cache sync

- [x] packages وterm وlesson detail.
- [x] lesson comments/resources.
- [x] balance وstudent shell.
- [x] notifications unread state.
- [x] exams/homework/community.
- [x] AI monitor وvideo processing fallback.
- [x] لا يوجد `router.refresh()` أو `window.location.reload()` داخل `frontend/src`.

### API والأداء والحماية

- [x] فصل lesson comments/resources عن lesson core response.
- [x] pagination لتعليقات الدرس.
- [x] response compression وoutput cache.
- [x] slow request logging وslow query interceptor.
- [x] Redis rate limiting للسياسات الحساسة.
- [x] idempotency للشراء، تفعيل الكود، تسليم الامتحان، تسليم الواجب وطلب مشاهدة إضافية.
- [x] AI start محمي بقفل DB ذري على `IsProcessingAI` لمنع job مكرر.
- [x] signed downloads قصيرة العمر.
- [x] local files عبر `X-Accel-Redirect`.
- [x] Web Vitals backend storage مع offline browser queue.
- [x] bundle analyzer وperformance budget قابلان للتشغيل محليًا وضمن الأتمتة.
- [x] منع E2E seed من اعتبار قاعدة `masar_platform` العادية قاعدة اختبار.

## نتائج التحقق

| الفحص | النتيجة |
|---|---|
| Backend + Redis integration tests في 2026-06-13 | 129 passed، 0 failed |
| Redis policies | 8 من 8 تعيد HTTP 429 بعد تجاوز الحد |
| Frontend lint في 2026-06-12 | 0 errors، 0 warnings |
| Frontend production build في 2026-06-12 | نجح، 63 static pages |
| Worker build/tests في 2026-06-13 | نجح، 6 tests passed |
| Platform event contract gate في 2026-06-13 | 59 producers، 59 listeners، 0 untargeted |
| SignalR reconnect E2E | الاختبار موجود ويثبت إعادة `JoinLesson` بعد reconnect؛ لم يعد تشغيله في مراجعة اليوم لعدم تشغيل backend/frontend E2E |
| Listener registry E2E | الاختبار موجود ويثبت بقاء handlers الخاصة بالـ hooks النشطة بعد unmount؛ لم يعد تشغيله في مراجعة اليوم |
| Browser smoke check في 2026-06-12 | صفحة login تعمل، لا console errors |
| Docker في 2026-06-13 | PostgreSQL وRedis healthy؛ نسخ backend ليست مشغلة وقت المراجعة |

اختبارات outbox الحالية تغطي success، no-target، retry، dead letter، وbackoff. اختبار staff outbox يستخدم SQLite relational provider لإثبات إنشاء الحدث داخل `SaveChangesAsync`. اختبار PostgreSQL locking الحقيقي متعدد النسخ ما زال ضمن بنود القبول أدناه.

## أدلة الأداء المدمجة

### EXPLAIN ANALYZE

تم القياس على dataset يحتوي درسًا، 100 تعليق، 50 community post و100 audit log:

| الاستعلام | الخطة | الصفوف المقروءة | Execution Time |
|---|---|---:|---:|
| lesson by primary key | index scan | 1 | 0.025 ms |
| comments by lesson + top 10 | index scan + top-N heapsort | 100 | 0.093 ms |
| approved community posts + top 10 | index scan + top-N heapsort | 50 | 0.047 ms |
| audit logs by admin + top 10 | index scan + top-N heapsort | 100 | 0.062 ms |

هذه النتائج تثبت استخدام indexes على dataset الاختبار، لكنها ليست benchmark لحجم production كبير.

### Lesson payload

القياس الفعلي لـ JSON response bodies:

| Response | الحجم |
|---|---:|
| lesson core | 922 bytes |
| resources | 55 bytes |
| 100 comments | 16,195 bytes |
| before split المحسوب | 16.77 KB |
| after split | 0.90 KB |
| reduction | 94.63% |

الهدف 60% تحقق للـ uncompressed body. لم يتم تسجيل wire bytes منفصلة مع `Accept-Encoding: gzip/br`، لذلك compressed transfer reduction غير مثبت حتى الآن.

## المتبقي الحقيقي

هذه البنود لا تمنع التشغيل الوظيفي، لكنها لازمة لإغلاق الخطة هندسيًا بالكامل:

### P1 - اختبارات قبول محلية

- [x] تشغيل `signalr-events.spec.ts` داخل E2E database معزولة وإثبات reconnect ثم `JoinLesson` مرة ثانية.
- [ ] إضافة PostgreSQL multi-instance acceptance test يثبت عدم duplicate outbox delivery فعليًا، وليس فقط row-locking unit behavior.
- [ ] إضافة اختبار end-to-end يثبت وصول `StaffDataChanged` من database إلى صفحة موظف مفتوحة.

### P2 - Coverage

- [ ] توسيع payload contract tests لتغطي payload schema للأحداث المالية والحساسة، وليس type/target فقط.
- [x] إضافة worker tests لـ AI callback، callback failure، Telegram download وjob cancellation، بالإضافة إلى security baseline.
- [ ] إضافة BullMQ integration test يثبت retry الفعلي وعدد المحاولات، بدل الاكتفاء بإثبات أن handler يرمي الخطأ.
- [x] إضافة اختبار listener registry مباشر يثبت أن unmount لأحد hooks لا يزيل handlers الخاصة بالـ hooks الأخرى.
- [ ] اختبار أن `StaffRealtimeBoundary` لا يفقد حالة modal أو form غير محفوظ على المسارات غير المستثناة، أو استبدال remount بإعادة جلب موجهة.

### P3 - Performance proof

- [ ] قياس compressed وuncompressed HTTP wire bytes بشكل منفصل.
- [ ] إعادة EXPLAIN على dataset أكبر يمثل production وتوثيق p95/p99 بدل single execution فقط.
- [x] إضافة composite indexes لـ `(LessonId, CreatedAt)`, `(Status, CreatedAt)`, `(PerformedByUserId, CreatedAt)` مع migration مخصصة.

## أوامر التحقق

```bash
cd backend
RUN_REDIS_INTEGRATION_TESTS=1 \
TEST_REDIS_CONNECTION='localhost:6382,abortConnect=false' \
ASPNETCORE_ENVIRONMENT=Development \
dotnet test tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-restore
```

```bash
cd frontend
npm run lint
npm run build
npm run check:platform-events
node scripts/check-performance-budget.js
```

```bash
cd worker
npm test
```

```bash
docker compose config --quiet
docker compose ps
```

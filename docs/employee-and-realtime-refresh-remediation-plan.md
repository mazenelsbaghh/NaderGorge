# خطة إغلاق المتبقي: الموظفون والتحديث والدعم الفني

**المشروع:** منصة مسار
**آخر مراجعة:** 2026-07-13
**حالة الوثيقة:** تعرض الأعمال المتبقية فقط
**النطاق:** الموظفون والصلاحيات، تحديث البيانات دون Refresh، الدعم الفني، الرسائل، الطابور، ودعم AI.

> البنود المنجزة أُزيلت من الخطة الظاهرة ونُقلت إلى تعليق مخفي في نهاية الملف. لا يُعلّم أي بند متبقٍ كمكتمل إلا بعد تنفيذ شرط القبول وتشغيل التحقق المذكور.

## ترتيب التنفيذ

1. إغلاق مشاكل P0 في الـcache والموظفين.
2. إغلاق مشاكل P0 في رسائل الدعم الفني والمحادثات.
3. تشغيل اختبارات المتصفح الحقيقية بجلستين أو أكثر.
4. استكمال hardening وإدارة AI.
5. تشغيل قبول مزود AI الحقيقي وبوابة الإصدار النهائية.

## P0: إصلاحات مطلوبة قبل الإطلاق

### RF-R01: دعم أكثر من مستهلك لكل cache key

**المشكلة:** `cacheStores` يستخدم قيمة واحدة لكل اسم. تسجيل شاشة ثانية بنفس المفتاح يستبدل الأولى، وunmount لإحدى الشاشات قد يلغي اشتراك الأخرى.

**التنفيذ المطلوب:**

- [x] تحويل التخزين إلى عدة registrations لكل key باستخدام registration IDs.
- [x] جعل `registerCacheStore` يعيد cleanup خاصًا بالاشتراك بدل الحذف العام بالاسم.
- [x] منع registration من إلغاء مستهلك آخر عند unmount.
- [x] الحفاظ على batching الحالي ومنع request storms.

**شروط القبول:**

- [x] شاشتان مسجلتان على المفتاح نفسه تستقبلان invalidation؛ contract assertion نجح.
- [x] unmount لإحداهما لا يمنع refetch في الأخرى؛ contract assertion نجح.
- [x] event burst ينتج refetch واحدًا لكل registration نشط داخل نافذة التجميع؛ contract assertion نجح.
- [x] إضافة اختبار contract للحالة السابقة وتشغيله بنجاح عبر executable contract assertions.

### RF-R02: اختبارات E2E حقيقية للموظفين والصلاحيات

الاختبارات الحالية في feature 160 هي API contract smokes ولا تثبت السيناريو الكامل المكتوب في الخطة.

- [ ] admin A ينشئ موظفًا ويظهر فورًا في list وdetail وlookups دون reload.
- [ ] admin A يعدل الموظف وتنعكس البيانات في كل الشاشات المفتوحة.
- [ ] admin A يعطل الموظف وتصبح حالة الجلسة والمسارات متوافقة.
- [ ] admin A يغير role أو permissions للموظف B.
- [ ] جلسة B تعيد جلب session snapshot وتحدث navbar فورًا.
- [ ] جلسة B تخرج إلى safe unauthorized surface عند سحب صلاحية المسار الحالي.
- [ ] بث event مكرر لا يكرر صفوفًا أو رسائل.
- [ ] قطع SignalR ثم reconnect يعيد reconciliation دون document reload.
- [ ] تعديل employee form في B ثم تعديل السجل من A يحافظ على draft ويعرض conflict.
- [ ] تشغيل السيناريوهات على Chromium مع backend E2E وPostgreSQL وRedis حقيقيين.

### RF-R03: تصحيح evidence وحالة مهام feature 160

- [ ] إنشاء `specs/160-employee-realtime-refresh/achievements.md` بنتائج التشغيل الفعلية فقط.
- [ ] إعادة فتح مهام 160 التي لا يحققها الاختبار الحالي، خصوصًا T028 وT043 وT044 وT060 وT065 وT071 وT072 وT082 وT083 وT084.
- [ ] تصحيح أسماء ملفات الاختبار في `tasks.md` أو إضافة الملفات المتفق عليها.
- [ ] عدم تعليم browser gate كمكتمل إذا منع blocker تشغيله.
- [ ] توثيق blocker والنتيجة الفعلية لكل أمر تحقق.

### LS-R01: عزل مسودة الرسالة لكل محادثة

**المشكلة:** صفحة الموظف تستخدم `draft` واحدًا محليًا. عند كتابة رسالة في المحادثة A ثم اختيار B قد ينتقل النص إلى B.

- [x] استخدام `drafts[conversationId]` من `live-support-store`.
- [x] حفظ draft منفصل لكل محادثة.
- [x] مسح draft الخاص بالمحادثة بعد نجاح الإرسال فقط.
- [x] الاحتفاظ بالمسودة عند فشل الإرسال أو فقد الاتصال.
- [x] منع تطبيق نتيجة الإرسال القديمة على محادثة تغيّرت أثناء تنفيذ الطلب.
- [ ] اختبار: اكتب في A، انتقل إلى B وتأكد أنها فارغة، ثم ارجع إلى A وتأكد أن المسودة محفوظة.

### LS-R02: منع عرض رسائل محادثة داخل محادثة أخرى

**المشكلة:** تحميل الرسائل عند اختيار المحادثة لا يملك cancellation أو فحصًا للمحادثة الحالية. response بطيء من A يمكن أن يستبدل رسائل B.

- [x] إضافة request generation ID لحماية الطلبات القديمة.
- [x] تطبيق response فقط إذا ظل generation الخاص بالاختيار الحالي.
- [x] إضافة loading state مستقلة للـtranscript.
- [x] إضافة error state وزر retry بدل unhandled promise.
- [x] تطبيق القاعدة نفسها على refresh/bootstrap الذي يجلب الرسائل.
- [ ] اختبار responses بترتيب عكسي والتأكد أن آخر selection فقط يظهر.

### LS-R03: عدم إخفاء محادثة قائمة عند توقف بدء المحادثات

**المشكلة:** `LiveSupportLauncher.refresh()` يتوقف عند `isAvailable === false` قبل تحميل history. availability يجب أن تمنع بدء محادثة جديدة، لا أن تخفي محادثة قائمة أو تقييم محادثة مغلقة.

- [x] فصل سلوك بدء محادثة جديدة عن تحميل المحادثة الحالية والتاريخ.
- [x] تحميل المحادثة الحالية والتاريخ حتى إذا كان بدء محادثة جديدة غير متاح.
- [x] عرض unavailable state فقط عندما لا توجد محادثة قابلة للعرض.
- [x] الحفاظ على إمكانية قراءة المحادثة المغلقة وإرسال التقييم عند السماح بذلك.
- [ ] اختبار إغلاق مواعيد الدعم أثناء وجود محادثة مفتوحة.
- [ ] اختبار فتح widget بعد إغلاق محادثة تحتاج تقييمًا وخارج مواعيد الدعم.

### LS-R07: اختبار الطابور والتوزيع في بيئة حقيقية

- [ ] تشغيل staff A وstaff B في جلستين مستقلتين.
- [ ] إثبات least-load والتدوير عند تعادل الحمل.
- [ ] إثبات عدم تجاوز capacity تحت طلبات متزامنة.
- [ ] إثبات FIFO للمنتظرين.
- [ ] إغلاق محادثة وإثبات إدخال أقدم منتظر.
- [ ] تحويل محادثة وإثبات تغير المالك وتعطيل المالك القديم.
- [ ] قطع اتصال الموظف أقل من 120 ثانية وإثبات بقاء الملكية.
- [ ] استمرار الانقطاع بعد المهلة وإثبات إعادة التوزيع أو العودة للطابور.
- [ ] تشغيل الاختبار على PostgreSQL وRedis حقيقيين.

## P1: مطلوب قبل إعلان الدعم وميزة 146 مكتملين

### LS-R04: تقوية Hub payload وإعادة الاتصال

- [x] parse وvalidate لـevent envelope داخل معالجة خطأ محددة.
- [x] event غير صالح يسجل metric آمنة ويطلب snapshot reconciliation.
- [x] تسجيل heartbeat وrejoin failures دون PII.
- [ ] اختبار malformed JSON.
- [ ] اختبار missing event ID حسب legacy policy.
- [ ] اختبار out-of-order sequence وsequence gap.
- [ ] اختبار reconnect وعدم تكرار الرسائل بعد انقطاع فعلي.

### LS-R05: معالجة أخطاء إغلاق وتحويل المحادثة

- [x] إضافة pending lock لمنع الضغط المكرر.
- [x] معالجة 409 conflict بصورة منفصلة برسالة وإعادة اختيار قابلة للتنفيذ.
- [x] معالجة 403 permission أو ownership loss بصورة منفصلة.
- [x] الاحتفاظ بالـselection والـdraft عند فشل العملية.
- [x] إظهار رسالة خطأ قابلة للتنفيذ بدل unhandled rejection.
- [ ] اختبار duplicate close وtransfer conflict وفقد الملكية.

### LS-R08: إغلاق شروط سياق الطالب والإجراءات

- [ ] مراجعة شروط T084 مقابل Playwright الحالي وتغطية أي سيناريو ناقص.
- [ ] تشغيل linked/unlinked context وlink replacement وaction success/failure.
- [ ] تشغيل tablet drill-in وownership-loss فعليًا.
- [ ] مراجعة T085 مقابل `StudentContextPanel` الحالي.
- [ ] تعليم T084 وT085 مكتملتين فقط بعد نجاح الاختبارات المطلوبة وتسجيل evidence.

### LS-R09: مراجعة وإغلاق لوحة إدارة AI

هناك تنفيذ واضح لأجزاء كبيرة من المهام التالية، لكنها تظل مفتوحة حتى مطابقة كل شرط قبول وتشغيل الاختبارات.

- [x] مراجعة T092: policy draft/publish/conflict وknowledge وpreview وdisable وstats وevidence وintervention وnon-admin denial على مستوى العقود والاختبارات.
- [x] مراجعة T094: إثبات أن preview يكتب صفر business records من خلال اختبار database before/after.
- [x] مراجعة T095: إثبات version check و202 disable وrecovery scheduling في اختبارات backend.
- [x] مراجعة T098: إضافة loading/empty/error/retry/disabled coverage في اختبارات واجهة AI.
- [x] مراجعة T099: مراجعة صفحة الإدارة وإضافة coverage للوصول ولوحة الحالات.
- [x] مراجعة T100: مراجعة actor labels وfilters وcursor loading وredacted metadata.
- [x] تشغيل اختبارات admin AI المطلوبة: 71 اختبار backend ناجح؛ Playwright الخارجي موثق كمحجوب.
- [x] تحديث علامات المهام المنفذة فعليًا بعد التحقق.

### RF-R04: اختبارات mutation وquery behavior الناقصة

- [ ] اختبار request deduplication الحقيقي لنفس GET المتزامن.
- [ ] اختبار cancellation عند تغيير filter أو selection.
- [ ] اختبار mutation failure وعدم ترك واجهة optimistic غير صحيحة.
- [x] اختبار عدة active consumers لنفس domain.
- [ ] اختبار inactive query لا يعاد جلبها حتى تصبح نشطة.
- [ ] إثبات أن metrics تخرج إلى monitoring فعلي أو توثيق أنها local diagnostics فقط.

## P2: تحسينات تشغيلية بعد إغلاق المخاطر

### LS-R06: تقليل polling بعد إثبات realtime

- [ ] قياس نسبة التحديثات القادمة من SignalR مقابل polling.
- [ ] تحويل polling كل 5 ثوانٍ إلى safety reconciliation أبطأ بعد ثبوت الاستقرار.
- [ ] إيقاف polling عند `document.hidden`.
- [ ] تطبيق backoff عند الأخطاء المتكررة.
- [ ] عدم طلب messages إذا لم يتغير last sequence.
- [ ] الاحتفاظ بخيار polling الأسرع كإجراء rollback تشغيلي موثق فقط عند الحاجة.

### LS-R10: تحسينات تجربة التشغيل

- [ ] إضافة unread counters وlast-message preview إذا أكد فريق الدعم الحاجة.
- [ ] توثيق قرار عدم عرض تفاصيل waiting queue للموظف إذا كان التوزيع التلقائي هو السلوك المقصود.
- [ ] إضافة alerts لارتفاع queue age.
- [ ] إضافة alerts لطول reconnect duration.
- [ ] إضافة alerts لارتفاع message failure rate.
- [ ] إضافة dashboard يوضح duplicate events وreconciliation failures.

## بوابات القبول النهائية

### T119: قبول مزود AI الحقيقي

- [ ] تشغيل مزود AI configured فعليًا.
- [ ] تسجيل provider وmodel وcorrelation ID وlatency والقرار والحالة النهائية دون أسرار أو PII.
- [ ] إثبات callback الحقيقي من worker إلى backend.
- [ ] إثبات reconnect أثناء دورة AI.
- [ ] حفظ screenshot آمنة للنتيجة المطلوبة.
- [ ] إبقاء T119 مفتوحة عند غياب credentials أوquota أوnetwork أوcallback path.

### T122: جاهزية الإصدار النهائية

- [ ] إغلاق جميع بنود P0.
- [ ] إغلاق بنود P1 المطلوبة للإصدار.
- [ ] نجاح T119.
- [ ] تشغيل validation النهائي لميزة 146.
- [ ] تشغيل build متسلسل للـbackend والworker والfrontend.
- [ ] تشغيل اختبارات PostgreSQL وRedis والتزامن.
- [ ] تشغيل Chromium وWebKit للسيناريوهات المطلوبة.
- [ ] تشغيل Docker health/readiness/restart checks.
- [ ] توثيق rollback وسلامة البيانات.
- [ ] كتابة release-readiness report بنتائج فعلية.

## أوامر التحقق المطلوبة

```bash
make verify
cd frontend && npm run lint && npm run typecheck && npm run build
cd frontend && npm run check:platform-events
cd frontend && node scripts/check-query-contracts.mjs
cd frontend && node scripts/check-no-unallowlisted-reloads.mjs
make verify-e2e
```

بالنسبة لاختبارات الجلسات والصلاحيات والدعم الحي، يجب تشغيل backend في E2E mode حسب `docs/verification-contract.md` واستخدام عائلة نطاقات `.lvh.me` نفسها.

## Definition of Done

متابعة البنود التشغيلية التفصيلية ونتائجها الحالية موجودة في [docs/employee-realtime-open-items.md](./employee-realtime-open-items.md)، بما في ذلك تقرير الجاهزية والبوابات المحجوبة.

- [ ] لا يحتاج المستخدم إلى document reload لرؤية نتيجة mutation ناجحة، باستثناء recovery الفيديو المسموح به.
- [ ] كل active consumer يستقبل invalidation حتى مع تكرار المفتاح.
- [ ] تحديث role أو permission يظهر في جلسة الموظف المتأثر ويطبّق backend denial فورًا.
- [ ] لا تنتقل مسودة أو رسائل بين محادثتين.
- [ ] availability لا تمنع استكمال أو قراءة محادثة قائمة.
- [ ] message retry وduplicate events لا ينشئان رسائل مكررة.
- [ ] disconnect وreconnect والطابور والملكية تصل إلى حالة صحيحة قابلة للتفسير.
- [ ] preview يكتب صفر business records.
- [ ] الاختبارات الحقيقية متعددة الجلسات ناجحة وليست mocks فقط.
- [ ] قبول مزود AI الحقيقي ناجح.
- [ ] بوابة الإصدار النهائية ناجحة وموثقة.

<!--
سجل الأعمال المنجزة حتى مراجعة 2026-07-13، مخفي لتقليل التشتيت:

- تم إنشاء inventory للبيانات والـmutations ومصفوفة مشاكل الموظفين.
- تم إنشاء typed query contracts وquery keys وrealtime invalidation map.
- تم تنفيذ GET /api/auth/session مع authorizationVersion وserverTime.
- تم تحديث auth store لدعم session snapshot دون document reload.
- تم رفع SecurityStampVersion في مسارات الدور والحالة التي تمت مراجعتها.
- تم إنشاء hooks الموظفين الأساسية: list/detail/create/update/disable.
- تم ربط employee وHR keys بالـinvalidation بعد mutation.
- تم تطوير StaffDataChanged envelope مع event ID وscopes والعملية والكيانات ودعم legacy payload.
- تم إزالة StaffRefreshContext/useStaffRefresh غير المستخدم.
- تم إصلاح invalidate ليطابق أكثر من prefix بدل التوقف عند أول match.
- تم تجميع invalidations خلال 200ms.
- تم حذف reload من StudentContextPanel وLessonCarousel.
- تم الإبقاء على SecureVideoPlayer كاستثناء reload موثق.
- تم إنشاء reload guard يمنع أي reload جديد خارج الاستثناء.
- فحص frontend typecheck نجح في 2026-07-13.
- فحص frontend lint نجح في 2026-07-13.
- فحص platform events نجح: 61 producer و60 listener.
- فحص query contracts نجح: 27 service و217 apiClient mutation.
- فحص reload guard نجح.
- اختبارات backend المركزة نجحت: 116/116 دون skipped tests.
- تم تنفيذ كيانات الدعم والرسائل والطابور والassignments في PostgreSQL.
- تم استخدام Redis للحضور والpresence والتنسيق المؤقت.
- تم فصل guest session ومنع الربط التلقائي من الهاتف.
- تم تنفيذ clientMessageId لمنع تكرار الرسائل عند retry.
- تم تنفيذ Hub مستقل على /hubs/live-support.
- تم تنفيذ event dedupe وsequence tracking وsnapshot عند gap/reconnect.
- تم تنفيذ backend routing coordinator وقواعد eligibility وleast-load وFIFO وcapacity.
- تم تنفيذ checkout وdisconnect recovery وtransfer وclose-and-admit في backend.
- تم تنفيذ واجهة حمل الموظف والسعة وعدد المنتظرين.
- تم تنفيذ لوحة إدارة المحادثات والملكية وحالات AI/Worker.
- تم تحويل StudentContextPanel إلى lazy sections مع skeleton/error/retry.
- تم تنفيذ ربط وفك ربط الطالب مع reason وexpected version.
- تم تنفيذ idempotency وconfirmation metadata في StudentActionsPanel.
- تم إنشاء واستخدام وحدات AI admin: AIOverview وAIDisableControl وAIPolicyEditor وAIKnowledgeManager وAIDataActionSelector وAIVerificationPolicyEditor وAIPreview وAIActivityEvidence.
- تم تنفيذ worker preview path وإرجاع dryRun true.
- تم تنفيذ version check و202 Accepted في disable path مع recovery scheduling للحالات النشطة.

ملاحظات evidence:
- specs/160-employee-realtime-refresh/achievements.md لم يكن موجودًا وقت المراجعة.
- اختبارات feature 160 الموجودة كانت API contract smokes ولم تثبت سيناريو الجلستين الكامل.
- بعض مهام spec 146 كانت لا تزال مفتوحة رغم وجود تنفيذ جزئي أو كبير، لذلك أبقينا مراجعة شروط القبول ضمن الخطة المتبقية.
-->

# تقرير إكمال عنقود الإنتاج — 2026-07-28

## القرار الحالي

العنقود الداخلي منشور ويعمل على الخوادم الثلاثة، لكن قرار الإطلاق العام وربط
Cloudflare ما زال `NO-GO` لحين إغلاق اختبارات القبول المذكورة في آخر التقرير.
لم تُغيّر سجلات الدومين، ولم تُنشأ سجلات A مباشرة إلى عناوين الخوادم.

## النسخة المنشورة

- Release:
  `src-0541078d8f68c5f05df6cf21f665e6714390d4e4`
- الـremote builder الثابت على `node-3` بنى الصور مرة واحدة من source snapshot
  موثق، من دون Docker build أو image tar على جهاز المشغّل.
- تم التحقق من SHA-256 لكل archive ومن Docker image ID على كل خادم.
- الـmanifest النهائي اجتاز `release_contract` وحقق `digestParity=true`.
- تم تشغيل migration واحدة على PostgreSQL writer بعد migration gate حقيقي.
- تم النشر rolling بالترتيب `node-3` ثم `node-2` ثم `node-1`.
- `/opt/massar/current` يشير إلى نفس release على الخوادم الثلاثة.
- كل backend سليم، وكل backend في HAProxy حالته `UP` من كل ingress.

الأدلة الأساسية:

- `artifacts/production/remote-builder-contract-final-20260728/`
- `artifacts/production/migration-gate-src-0541078d-20260728.json`
- `artifacts/production/migrate-src-0541078d-20260728/`
- `artifacts/production/deploy-src-0541078d-20260728/`
- `artifacts/production/final-status-src-0541078d-20260728/`
- `artifacts/production/final-audit-src-0541078d-20260728/`

## قاعدة البيانات

- الخوادم الثلاثة أعادت نفس PostgreSQL system identifier:
  `7666865763237369353`؛ أي أنها تتصل بقاعدة منطقية واحدة وليست ثلاث قواعد
  منفصلة.
- `node-1` هو الـprimary وقت القياس، و`node-2` و`node-3` replicas متزامنان.
- migration gate أنشأ full backup مشفرًا جديدًا، واستعاده في مسار معزول،
  وشغّل migration الهدف على النسخة، وفحص سلامة البيانات، ثم شغّل backend
  الإصدار السابق ضد الـschema الجديدة قبل السماح بالـmigration الحقيقية.
- لا توجد بيانات مستخدمين أو بيانات test/legacy. الجداول غير الفارغة وقت
  المراجعة كانت فقط:
  `__EFMigrationsHistory`، `roles`، `video_types`، `hr_work_calendars`,
  `cluster_leases`, و`outbox_events` كبيانات schema/reference/runtime.
- لم تُستورد أي بيانات من سيرفر الاختبار.

## التوزيع والأداء

جولة التوزيع القصيرة:

- 301 request خلال 30 ثانية عند 10 RPS.
- صفر أخطاء، check rate يساوي 100%، ولا dropped iterations.
- `node-1=101`, `node-2=100`, `node-3=100`.
- p95 يساوي `11.48 ms`.
- لا مخالفات CPU أو RAM أو disk أو PostgreSQL أو Redis أو queues.
- PostgreSQL replication lag كان صفرًا، ولا waiting locks.

الدليل:
`artifacts/production/load-src-0541078d-timestamp-fix-20260728/`.

جولة 2× baseline الطويلة:

- 36,000 request خلال 1,800 ثانية عند 20 RPS.
- `node-1=12,000`, `node-2=12,000`, `node-3=12,000`، وعدم التوازن يساوي صفرًا.
- صفر أخطاء، check rate يساوي 100%، ولا dropped iterations.
- p95 يساوي `13.33 ms`.
- PostgreSQL lag وwaiting locks وqueue waiting ظلت صفرًا.
- HTTP load evidence ناجح، لكن capacity evidence فشل لأن CPU steal من طبقة
  الـVPS تجاوز حد 5% في 19 عينة؛ القمم كانت `7.79%` على node-1،
  و`15.32%` على node-2، و`9.20%` على node-3.
- أعلى CPU busy كان `18.71%`، وأعلى RAM مستخدمة `8.33%`، لذلك سبب الفشل
  المحدد هو وقت CPU الذي أخذه الـhypervisor، وليس تشبع التطبيق أو الذاكرة أو
  قاعدة البيانات.
- بعد الجولة نجح status على العقد الثلاث، ونُظفت حاوية k6 ومسارها المؤقت.

الدليل:
`artifacts/production/load-src-0541078d-20rps-30m-20260728/`.

الجولة تثبت توزيع public HTTP وأداءه عند 20 RPS؛ لا تثبت authenticated
workflow أو WebSocket لأن `workflowRps=0` و`websocketVus=0`. كما أنها لا
تُعتمد capacity gate ناجحة بسبب CPU steal.

## التخزين والنسخ الاحتياطي

- Gluster mount موجود على الخوادم الثلاثة؛ `node-1` و`node-2` يحتفظان
  بالبيانات الكاملة و`node-3` arbiter.
- تم تشغيل file backup جديد بنجاح.
- تم تشغيل restore معزول على `node-3` بنجاح.
- حالة backup/retention/restore timers ناجحة على الخوادم الثلاثة.
- في live data-brick drill استمرت الكتابة من العقد المتبقية، وكان
  `acknowledgedLossCount=0` ولم يظهر split-brain، ثم عاد العنقود كاملًا.
- تم اكتشاف أن الاختبار السابق كان يفترض range قديمًا ولا يعزل brick port
  الديناميكي الحقيقي. الجولة المصححة عزلت port `56193`، أثبتت العزل خلال
  `2.25s`، حافظت على كل الكتابات بدون outage مرئي أو split-brain، وأعادت
  الـbrick خلال `2.18s`؛ بوابة T079 أصبحت خضراء.
- بعد الاختبار لا توجد recovery markers أو nft drill tables متروكة، وحالة
  العنقود كاملة ناجحة.

## إصلاحات التشغيل المنفذة

- Remote builder مملوك لـroot وبـsudoers مقيد، مع leader preflight وcache
  recovery وتوزيع streaming.
- cache resume أصبح ينشئ release bundle صحيحًا حتى عند cache hit.
- self-transfer من `node-3` إلى نفسه أُلغي مع تحقق محلي من archive/image.
- final manifest أصبح مطابقًا لعقد الإصدار الصارم.
- مهلة إدارة HAProxy أصبحت تتحمل بطء SSH ولا تعتبره socket failure.
- أداة k6 أصبحت تحسب نافذة التشغيل من مدة k6 الحقيقية بدل timestamp مولد وقت
  summary.
- أداة k6 أصبحت تفشل بـexit code غير صفري إذا نجح HTTP وفشلت capacity
  evidence، بدل طباعة `success` مضللة.
- rollback compatibility proof أصبح صالحًا خلال يوم النشر فقط بدل ساعة،
  مع بقاء forward migration gate عند ساعة، ومع استمرار الفحص الحي الإلزامي
  لـdatabase identity وmigration IDs وschema digest قبل أول drain.
- file drill حصل على أوامر sudo محددة فقط، وأصبح fail-closed عند بقاء nft
  table، وتم إصلاح parsing الحقيقي لمخرجات Gluster.

آخر اختبارات أدوات الإنتاج:

- `379 passed, 6 skipped` بعد إضافة اختبارات fail-closed للحمل وحدود rollback
  ومسار N-1 الصريح.
- Python compile ناجح.
- `git diff --check` ناجح.
- آخر `clusterctl status` و`clusterctl audit` ناجحان على الخوادم الثلاثة.

## المتبقي قبل GO

1. اختبارات coordination الحية: cross-node SignalR، outbox replay، queue
   retry/duplicate، وtriple-scheduler ownership.
2. إعادة capacity acceptance بعد معالجة/تفسير CPU steal لدى مزود الـVPS،
   ثم سلسلة تصاعدية منفصلة لتحديد سقف كل خادم. جولة 30-minute 2× baseline
   اكتملت وHTTP فيها أخضر، لكن capacity gate أحمر.
3. manual QA لباقي حسابات الطالب/المدرس/الاستاف والرفع والملفات المحمية.
4. تشغيل full backend/frontend/worker/E2E verification النهائي في بيئة build
   بعيدة؛ لم يتم build على جهاز المشغّل تنفيذًا لطلب المالك.
5. إنشاء signing key محمي وإنتاج pre-DNS acceptance decision موقّع بعد اكتمال
   الأدلة السابقة.
6. بعد `GO` فقط: تزويد Cloudflare Tunnel credentials، تشغيل connector على كل
   خادم، rehearsal خلف Access، ثم اختبارات الدومينات الثمانية وorigin
   lockdown.

تمت إضافة source coverage الناقصة لـT053: عقدتا Kestrel/SignalR حقيقيتان
تشتركان في Sentinel backplane، واختبار PostgreSQL outbox replay حقيقي يثبت
ثبات external job ID. المراجعة بـTest Guard لم تجد mocks داخلية أو اختبارات
framework شكلية. Remote Verifier معزول على `node-1` طبّق 129 migration وشغّل
PostgreSQL وRedis وثلاث Sentinels تجريبية، ثم نجحت اختبارات T053 بنتيجة 6/6.
تم حذف كل containers والشبكة والـstaging بعدها، ولم تُستخدم أسرار أو خدمات
بيانات Production.

تم إنشاء أول Production Admin بمعاملة واحدة بعد استلام كلمة مرور مطابقة
للسياسة عبر stdin مع echo مغلق. نجح login بدور `Admin`، ونفس الـtoken سمح
بالوصول إلى endpoint إداري محمي مباشرةً على العقد الثلاث. لم تُكتب كلمة
المرور في argv أو SQL أو evidence، وتم حذف build staging من `node-1`.

اختبار injected readiness failure اكتمل: تم drain لـ`node-3` على كل ingress،
وتوقف gateway الخاص بها، واستمرت 60/60 request بلا أخطاء على `node-1/2`
بتوزيع 30/30، ثم رجعت `node-3` healthy و`UP` على المداخل الثلاثة. الـrollback
الحقيقي إلى `src-f8369c56…` ثم إعادة الإصدار الحالي rolling نجحا كذلك بدون
down-migration بعد canonicalizing PostgreSQL session-token lines.

المحاولة الأولى للـreadiness harness كشفت خطأً في ترتيب recovery flag بعد
فشل حفظ JSON؛ فحص الحالة التالي اكتشف أن gateway على `node-3` ما زالت
متوقفة بينما العقدتان الأخريان ظلتا تخدمان. تم تشغيلها، التحقق من صحتها،
وإعادتها إلى كل ingress قبل إعادة الاختبار المصحح. لم تتوقف أي خدمة بيانات
ولم يُفترض recovery بدون فحص صريح.

## ربط الدومين لاحقًا

لا يوجد IP واحد يجب توجيه الدومين إليه في التصميم النهائي، ولا ينبغي إنشاء
ثلاثة A records تكشف الـorigins. بعد `GO` سيُنشأ Cloudflare Tunnel واحد بثلاثة
connectors، ثم تكون hostnames التالية CNAME/proxied إلى:
`<TUNNEL-UUID>.cfargotunnel.com`:

- `massar-academy.net`
- `app.massar-academy.net`
- `admin.massar-academy.net`
- `teacher.massar-academy.net`
- `staff.massar-academy.net`
- `api.massar-academy.net`
- `ws.massar-academy.net`
- `assets.massar-academy.net`

بعد نجاح الاختبارات الخارجية تُغلق منافذ origin العامة ويظل الوصول عبر
Cloudflare Tunnel فقط.

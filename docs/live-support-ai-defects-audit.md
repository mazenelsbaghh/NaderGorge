# تدقيق عيوب AI في الدعم الفني

تاريخ التدقيق: 2026-07-13

النطاق: دورة AI للدعم الفني من رسالة الطالب، وبناء السياق، والـworker/provider، والـcallback، وتنفيذ الإجراءات، وحتى واجهة الإدارة والاختبارات.

هذا الملف يفرّق بين عيب مثبت من الكود، وبين بوابة قبول لم يثبت تشغيلها. عدم وجود دليل تشغيل حقيقي لا يعني تلقائيًا وجود عيب في المنتج.

## الملخص

يوجد مسار جيد للحماية الأساسية: قرارات AI لها schema وhash، الـcallback محمي بـ`AI_CALLBACK_SECRET`، الإجراءات تحتاج تأكيدًا، وهناك state/version checks وhandoff عند فشل الدور. لكن توجد عيوب تشغيلية وعقدية تمنع اعتبار AI للدعم الفني جاهزًا للإطلاق:

1. الـworker الحالي لا يملك إثبات قبول مزود AI حقيقي.
2. معلومات المزود المسجلة قد تكون خاطئة عند استخدام fallback.
3. كتالوج بيانات الطالب أكبر من البيانات التي يبنيها `ContextBuilder` فعليًا.
4. كتالوج الإجراءات لا يرسل schemas للـAI، فيقترح إجراءات بلا معرفة بالـarguments المطلوبة.
5. retry الخاص بالـprovider لا يحترم الوقت المتبقي بالكامل وقد يكرر استدعاء المزود بلا backoff.
6. حجز الدور AI قابل لتنافس workers ويعتمد على exception من optimistic concurrency بدل نتيجة claim ذرية واضحة.
7. callback claim يفسّر JSON إلى TypeScript type بدون validation للعقد.
8. اختبارات AI الحقيقية، reconnect، callback، وواجهة participant لم تُثبت end-to-end بمزود حقيقي.

## العيوب المثبتة

### AI-001 — بيانات catalog معلنة لكنها لا تصل إلى النموذج

- الشدة: P1
- الحالة: مثبت من الكود
- الدليل: `LiveSupportAICatalog` يعلن `packages.active`, `balance.summary`, `requests.summary`, `gamification.summary`, `notes.safe`, `crm.safe`, و`audit.safe_recent` ضمن البيانات القابلة للقراءة، لكن `BuildStudentContextAsync` لا يبني أيًا منها؛ التنفيذ يتوقف عند `homework.summary`.
- الملفات: [LiveSupportAICatalog.cs](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/LiveSupportAI/Services/LiveSupportAICatalog.cs)، [LiveSupportAIContextBuilder.cs](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIContextBuilder.cs:80)
- الأثر: الإدارة تستطيع تفعيل key وتعتقد أن AI يرى البيانات، بينما يصل `{}` أو سياق ناقص. ينتج عن ذلك ردود غير دقيقة أو handoff غير ضروري.
- الإصلاح المطلوب: إما تنفيذ كل key في `BuildStudentContextAsync` مع حدود/إخفاء بيانات، أو إزالة keys غير المنفذة من catalog ومن واجهة الإدارة، مع اختبار parity يمنع الفرق بين catalog وcontext builder.

### AI-002 — `argumentsSchema` لكل الإجراءات فارغ

- الشدة: P1
- الحالة: مثبت من الكود
- الدليل: `LiveSupportAIContextBuilder` يرسل `JsonDocument.Parse("{}").RootElement.Clone()` لكل action بدل schema حقيقي.
- الملف: [LiveSupportAIContextBuilder.cs](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIContextBuilder.cs:53)
- الأثر: النموذج لا يعرف أن `student.device.disconnect` يحتاج جهازًا، أو أن تعديل الرصيد يحتاج قيمة وسببًا، ثم يقترح arguments ناقصة/خاطئة. الفشل يظهر متأخرًا بعد تأكيد الطالب.
- الإصلاح المطلوب: تعريف schema لكل action في catalog، تمريره إلى worker، والتحقق منه قبل إنشاء `LiveSupportAIPendingAction` وقبل التنفيذ.

### AI-003 — اسم المزود المسجل غير صحيح بعد fallback

- الشدة: P1
- الحالة: مثبت من الكود
- الدليل: `AIProviderGateway` ينفذ Vertex ثم Developer fallback عند quota، لكنه يرجع `T` فقط ولا يرجع provider المستخدم. بعد ذلك `generateLiveSupportReply` يسجل `runtime.config.primaryProvider` دائمًا.
- الملفات: [aiProvider.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/services/aiProvider.ts:20)، [geminiService.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/services/geminiService.ts:482)
- الأثر: evidence وmetrics قد تقول `vertex` بينما التنفيذ تم عبر Developer API، فتفسد المراقبة والتكلفة والتدقيق.
- الإصلاح المطلوب: جعل gateway يرجع `{ value, provider }` أو metadata موحدة، واستخدامها في `LiveSupportCompletionPayload` وtelemetry.

### AI-004 — retry مزود AI يكرر الاستدعاء بلا backoff ولا تحديث للـdeadline

- الشدة: P1
- الحالة: مثبت من الكود
- الدليل: `generateLiveSupportReply` ينفذ `withDeadline()` مرة ثانية عند `provider` أو `quota-exhausted`، مستخدمًا نفس `remainingMs` المحسوبة قبل المحاولة الأولى.
- الملف: [geminiService.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/services/geminiService.ts:462)
- الأثر: قد يتجاوز الدور deadline الإجمالي، ويضاعف التكلفة والضغط على المزود. كما أن fallback موجود في gateway عند quota، ثم توجد إعادة محاولة إضافية في service.
- الإصلاح المطلوب: سياسة واحدة فقط للـretry، حساب الوقت المتبقي قبل كل محاولة، backoff محدود، وعدم إعادة الطلب إذا انتهى deadline أو كان الخطأ authentication/validation.

### AI-005 — حجز الدور ليس claim ذريًا واضحًا

- الشدة: P1
- الحالة: عيب تنافسي محتمل مثبت من التدفق
- الدليل: `ClaimAsync` يقرأ turn، ثم يغير `Queued` إلى `Processing`، ثم يحفظ. لا توجد عملية SQL ذرية من نوع `UPDATE ... WHERE Status = Queued` أو lock صريح حول القراءة والتحديث.
- الملف: [LiveSupportAITurnOrchestrator.cs](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAITurnOrchestrator.cs:65)
- الأثر: workers متزامنان قد يقرآن نفس turn قبل حفظ أحدهما. `Version` كـconcurrency token قد يمنع أحد الحفظين، لكن النتيجة تصبح exception/failed job بدل استجابة idempotent واضحة، وقد يبدأ workerان inference قبل اكتشاف التعارض.
- الإصلاح المطلوب: claim ذري مع status/version predicate، وإرجاع `null` أو outcome معروف عند race، مع اختبار workerين حقيقيين على PostgreSQL/Redis.

### AI-006 — callback claim لا يتحقق من schema القادم من backend

- الشدة: P1
- الحالة: مثبت من الكود
- الدليل: `claim()` يعمل `JSON.parse(response.body) as LiveSupportClaimContext` فقط. لا يتم التحقق من `schemaVersion`, `turnId`, `deadlineAt`, الرسائل، limits، أو allowed actions قبل تمريرها إلى prompt builder.
- الملف: [liveSupportCallbackClient.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/services/liveSupportCallbackClient.ts:111)
- الأثر: رد callback ناقص أو غير متوافق يسبب crash/فشلًا عامًا داخل worker بدل failure code واضح، وقد يصل سياق أكبر من الحدود إلى مرحلة inference.
- الإصلاح المطلوب: validator مشترك للعقد قبل `runLiveSupportAgent`، مع `CALLBACK_INVALID_RESPONSE` ورسالة آمنة واختبار malformed claim.

### AI-007 — عزل prompt injection يعتمد على نص التعليمات فقط

- الشدة: P1
- الحالة: خطر تصميمي يحتاج اختبار adversarial
- الدليل: `assembleLiveSupportPrompt` يضع knowledge وstudent context وallowed actions داخل `systemInstruction` بعد عبارة “UNTRUSTED_CONTEXT”، بينما النموذج نفسه هو من يقرر إن كان النص تعليمات أم بيانات.
- الملف: [liveSupportAgent.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/services/liveSupportAgent.ts:49)
- الأثر: محتوى knowledge أو رسالة طالب مصاغة كتعليمات قد يؤثر على الرد أو اقتراح action. الـallowlist والـconfirmation يقللان أثر التنفيذ، لكنهما لا يمنعان تسريبًا أو ردًا مضللًا.
- الإصلاح المطلوب: فصل البيانات عن system instruction باستخدام parts/structured payload المدعوم، تقليل ما يصل للنموذج، اختبارات prompt-injection، ومنع أي decision لا يطابق policy/action schema.

### AI-008 — فشل provider يتحول إلى handoff لكن retry queue يعيد معالجة نفس job

- الشدة: P2
- الحالة: مثبت من التدفق
- الدليل: processor يستدعي `callbacks.fail()` عند inference failure ثم يرمي error، بينما BullMQ يضيف live-support jobs مع `attempts: 4`. الـturn يصبح `Failed`، والمحاولة التالية تنتهي بـ`TURN_NOT_FOUND` من claim.
- الملفات: [processLiveSupportTurn.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/jobs/processLiveSupportTurn.ts:99)، [jobIngestion.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/queues/jobIngestion.ts:105)
- الأثر: retries غير مفيدة، ضوضاء في queue وlogs، وقياس فشل غير دقيق. في بعض الأعطال قد يتأخر handoff الفعلي إذا فشل callback نفسه.
- الإصلاح المطلوب: فصل provider failure عن callback delivery failure؛ بعد نجاح `fail` callback يجب إنهاء job بنجاح business-wise أو استخدام failure outcome غير قابل لإعادة inference.

### AI-009 — لا يوجد provider response id أو token usage حقيقي

- الشدة: P2
- الحالة: مثبت من الكود
- الدليل: processor يرسل `providerResponseId: null`, `inputTokenCount: null`, و`outputTokenCount: null` دائمًا.
- الملف: [processLiveSupportTurn.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/jobs/processLiveSupportTurn.ts:82)
- الأثر: لا يمكن ربط decision بطلب المزود أو حساب التكلفة/الاستهلاك أو التحقيق في response محدد.
- الإصلاح المطلوب: استخراج metadata من `@google/genai` إن كانت متاحة، أو توثيق عدم توفرها صراحة وعدم عرضها كأنها telemetry مكتملة.

### AI-010 — Preview ومسار الإنتاج لا يملكان نفس acceptance gate

- الشدة: P1
- الحالة: فجوة قبول مثبتة
- الدليل: preview يمر عبر `WORKER_ADMIN_TOKEN`، بينما production turn يمر عبر callback secret وBullMQ. وجود preview ناجح لا يثبت queue claim/complete/fail أو provider callback الحقيقي.
- الملفات: [LiveSupportAIWorkerPreviewClient.cs](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Infrastructure/Services/LiveSupportAI/LiveSupportAIWorkerPreviewClient.cs:12)، [accept-real-ai-provider.ts](/Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/worker/src/scripts/accept-real-ai-provider.ts)
- الأثر: الإدارة قد ترى preview يعمل بينما مسار رسالة الطالب لا يعمل.
- الإصلاح المطلوب: acceptance test واحد يثبت message → outbox → Redis/BullMQ → real provider → callback → persisted AI message/decision، مع correlation id.

## فجوات الاختبارات والقبول

هذه ليست ادعاءات أن الكود خاطئ، لكنها شروط غير مثبتة حاليًا:

- لا يوجد تشغيل ناجح بمزود AI حقيقي؛ harness الحالي يتوقف عند غياب إعدادات provider/callback المطلوبة.
- لا يوجد اختبار حقيقي لاثنين من workers يتنافسان على نفس turn.
- لا يوجد اختبار malformed claim response من backend إلى worker.
- لا يوجد اختبار فعلي لتسجيل provider الصحيح عند Vertex quota ثم Developer fallback.
- لا يوجد اختبار adversarial شامل للـknowledge/messages مع محاولة تغيير decision أو تسريب student context.
- لا يوجد اختبار WebKit وreconnect أثناء دورة AI كاملة.
- اختبارات participant الحالية ما زالت تحتوي فشلًا في live-support proxy/session، لذلك لا تثبت UI AI end-to-end.

## ترتيب الإصلاح المقترح

1. إصلاح parity بين catalog و`BuildStudentContextAsync`، وإضافة action argument schemas والتحقق منها.
2. توحيد provider gateway ليعيد provider/response metadata، وإلغاء retry المكرر.
3. جعل claim ذريًا وidempotent، ثم اختبار concurrency على PostgreSQL وRedis.
4. إضافة validator لعقد claim واختبارات malformed/out-of-order callback.
5. تقوية فصل untrusted context واختبارات prompt injection.
6. إصلاح semantics الخاصة بـBullMQ بعد `fail` callback حتى لا تعاد inference بلا داعٍ.
7. تنفيذ acceptance حقيقي بمزود configured ثم تحديث release readiness فقط بنتيجة خضراء.

## أوامر التحقق المستخدمة

```bash
dotnet build backend/NaderGorge.sln --no-restore
cd frontend && npm run lint && npm run build
cd worker && npm run build
cd worker && npm run accept:real-ai
```

النتيجة الحالية: build وlint ناجحان، لكن acceptance الحقيقي لمزود AI غير مكتمل؛ لذلك لا يُعتبر T119 أو T122 مغلقًا.

## تحديث حالة الإصلاح — 2026-07-13

هذا القسم هو الحالة الأحدث ويستبدل حالات العيوب القديمة أعلاه عند التعارض:

| العيب | الحالة الحالية | الدليل الأخير |
|---|---|---|
| AI-001 catalog/context parity | مغلق | كل مفاتيح catalog تُmaterialize بسياق bounded، واختبار parity نجح |
| AI-002 action schemas/validation | مغلق | schemas للإجراءات الـ19 والتحقق قبل pending وقبل confirm، واختبارات AI نجحت |
| AI-003 provider identity | مغلق | gateway يرجع provider المستخدم، واختبار fallback metadata نجح |
| AI-004 duplicate retry/deadline | مغلق | retry المكرر أزيل والـdeadline يعاد حسابه قبل inference |
| AI-005 concurrent claim | مغلق | claim ذري PostgreSQL واختبارات concurrency |
| AI-006 malformed claim | مغلق | validator للـIDs/schema/deadline/limits واختبارات malformed claim |
| AI-007 prompt injection | مفتوح | يلزم اختبار adversarial وتقوية فصل structured context |
| AI-008 BullMQ failure retry | مغلق | نجاح fail callback ينهي business job ولا يعيد inference |
| AI-009 provider response/token metadata | مفتوح | ما زالت provider response id وtoken counts غير متاحة في payload |
| AI-010 real provider E2E | مفتوح | يحتاج credentials/provider callback وتشغيل message-to-persistence حقيقي |

## آخر نتائج التحقق

- Backend application tests: `384 passed / 1 skipped`.
- Backend PostgreSQL/Redis integration tests: `24/24 passed`.
- Worker targeted AI tests: `14/14 passed`.
- Worker full suite: `63/64`؛ الفشل الوحيد اختبار mindmap قديم خارج مسار live-support AI.
- Build backend وworker نجحا بدون أخطاء.

### محاولة الإغلاق النهائي — 2026-07-13

تم تشغيل البوابات الخارجية فعليًا، والنتيجة المعتمدة:

| البوابة | النتيجة | الدليل |
|---|---|---|
| T119 real provider | محجوبة/فاشلة خارجيًا | إعدادات Vertex الموثقة لمسار تقسيم الفيديو (`project-d32eb428-fe1c-4551-b6d` وbucket `massar`) تم استخدامها، لكن ADC المحلي فشل بصلاحية `403` على bucket قبل inference. محاولة Developer السابقة أعادت `quota-exhausted`. |
| Prompt injection adversarial boundary | ناجحة محليًا، غير مغلقة end-to-end | اختبارات Worker الخاصة بحدود الـprompt والقرار نجحت ضمن `14/14`، لكن لا يوجد provider quota متاح لتشغيل adversarial inference حقيقي. |
| Live-support Playwright E2E | جزئية | `6 passed` و`12 failed`؛ الاختبارات الفاشلة احتاجت Backend E2E على `127.0.0.1:5245` ولم يكن متاحًا. |
| Backend/worker readiness callbacks | محجوبة | جلسة القبول المستقلة لم تصل إلى Backend/Worker readiness، والخدمات لم تصبح reachable على المنافذ المطلوبة. |

لا يتم اعتبار T119 أو AI-007 أو AI-010 مغلقة حتى يملك ADC صلاحيات Vertex/GCS المطلوبة، ويتوفر provider بحصة صالحة، وتصبح بيئة Backend/Worker E2E reachable. لا توجد نتيجة mock أو intercepted response تم احتسابها كدليل حقيقي.

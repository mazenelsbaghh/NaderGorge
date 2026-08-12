# Project Achievements & SDD Phase Progress / الإنجازات وتقدم المراحل

## Current Speckit-All Run / التشغيل الحالي

**Feature**: `169-admin-ai-agent`
**Scope**: إعداد المواصفات والتوضيح والبحث والخطة التقنية والمهام فقط لوكيل AI إداري شامل، ثم التوقف قبل أي تنفيذ حتى يراجع المستخدم الملفات ويعتمد بدء Phase 5.

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)
- [ ] Phase 5: Implementation (`speckit-implement`) — **متوقف بطلب المستخدم حتى موافقة جديدة**
- [ ] Phase 6: Deep Architectural, Code & UI/UX Critique
- [ ] Phase 7: Clean Code Guard (`clean-code-guard`)
- [ ] Phase 8: Test Guard (`test-guard`)
- [ ] Phase 9: Feature Tests, Final Verification & Summary Report

### Approved Feature Brief / ملخص الميزة المعتمد

- **المشكلة:** لا يوجد شات AI عام وخاص بالإدارة يستطيع فهم بيانات المنصة كلها وتنفيذ وظائف الأدمن بعيدًا عن شات الطلاب والدعم المباشر.
- **الهدف:** شات مستقل داخل `Admin Shell` يجيب من أحدث بيانات العمل، ويجهز وينفذ كل إجراءات الأدمن الحالية عبر قواعد المنصة المعتمدة.
- **الأدوار:** كل حساب يحمل دور `Admin` يستطيع الدخول؛ كل الأدوار الأخرى ممنوعة في الواجهة والخادم.
- **القراءة:** جميع بيانات الطلاب والمدرسين والمحتوى والمبيعات والمالية والموارد البشرية والدعم والسجلات والتقارير، إجماليًا أو تفصيليًا، مع أقل نطاق لازم للسؤال.
- **الحظر الدائم:** كلمات المرور وتجزئاتها، التوكنات، مفاتيح التشفير، أسرار الخدمات، بيانات الجلسات وأكواد التحقق لا تدخل النموذج أو الرد أو المحادثة أو السجل.
- **التنفيذ:** كل إجراءات الأدمن الموجودة عند اعتماد المواصفة من أول إصدار، دون SQL حر أو تجاوز لقواعد العمل؛ الإجراءات التي تحتاج سرًا تستخدم إدخالًا آمنًا منفصلًا عن المحادثة.
- **التأكيد:** كل تغيير يحتاج معاينة وتأكيدًا صريحًا؛ الحذف والمال والصلاحيات والأمن وتعطيل الحسابات والتعديلات الجماعية تحتاج عبارة تأكيد مكتوبة.
- **السلامة:** إعادة فحص الدور والحالة والقواعد قبل التنفيذ، انتهاء وإلغاء الاقتراح، منع التنفيذ المكرر، وسجل تدقيق كامل لكل نتيجة أو رفض أو فشل.
- **النطاق الحالي:** مراحل Spec Kit من 1 إلى 4 فقط؛ لا Implementation ولا تعديل في كود المنتج قبل مراجعة المستخدم وموافقته اللاحقة.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: `/root/phase1_spec_support` و`/root/phase1_spec_fast` → حددا الممثلين والمتطلبات والكيانات وحالات الفشل ومعايير القياس ومخاطر الأسرار والعمليات الجماعية دون تعديل ملفات.
- [x] Phase 2 clarify support: `/root/phase2_clarify_support` و`/root/phase2_clarify_fast` → راجعا المواصفة بالعربية وأكدا عدم وجود قرار منتج حرج جديد؛ أُجّلت مدد الصلاحية والـrate limits والمخطط وآليات المزود إلى البحث التقني لأنها لا تغيّر نية الميزة.
- [x] Phase 3 plan support: `/root/phase3_plan_support` راجع معمارية Backend/Worker/PostgreSQL/Outbox/Redis والتأكيد والتعافي والاختبارات، و`/root/admin_capability_inventory` حصر مجالات الأدمن والكتابات المباشرة وفجوات التدقيق/idempotency وصمم coverage gate ثنائي الاتجاه، و`/root/admin_agent_frontend_plan` حدد route مستقلة ومكونات وحالات وRTL/accessibility/responsive واختبارات الواجهة؛ لم يعدّل أي وكيل ملفات.

### Phase 2 Clarification Evidence / إثبات التوضيح

- [x] تم تشغيل فحص المتطلبات مع `SPECIFY_FEATURE=169-admin-ai-agent` وتحديد `specs/169-admin-ai-agent/spec.md` كمواصفة فعالة.
- [x] لا توجد أسئلة توضيح حرجة بعد القرارات المؤكدة والافتراضات المعتمدة؛ لم تُكرر أسئلة الصلاحيات أو نطاق البيانات أو التأكيد التي أجاب عنها المستخدم.
- [x] Functional Scope وRoles وUX Flow وSecurity/Privacy وFailures وCompletion Signals واضحة؛ تفاصيل TTL والحدود والمخطط والتكاملات مؤجلة بصورة صحيحة إلى `speckit-plan`.

### Phase 3 Speckit-Plan Evidence / إثبات التخطيط

- [x] تم إنشاء `plan.md` بسياق تقني فعلي، Constitution gates قبل/بعد التصميم، حدود الثقة، دورة أدوات مقيدة، baseline تغطية ثنائية الاتجاه، سبع موجات تنفيذ لاحقة، وبوابات اختبار/Docker/manual QA مع تثبيت توقف التنفيذ.
- [x] تم إنشاء `research.md` بعشرين قرارًا معماريًا وأمنيًا مع المبررات والبدائل، وحسم Worker-only Gemini وBackend-only tools/actions وPlatformHub الحالي وعدم استخدام SQL/Web/Vector DB.
- [x] تم إنشاء `data-model.md` للمحادثات والرسائل والأدوار والخطوات والقراءات والمقترحات والتأكيد الآمن والتنفيذ والتدقيق، مع القيود والفهارس وحالات السباق والاحتفاظ وmigration additive.
- [x] تم إنشاء `quickstart.md` لبوابات baseline والمخطط والاختبارات والـDocker والمزود الحقيقي والـmanual QA والتعطيل والـrollback دون حذف بيانات.
- [x] تم إنشاء سبعة عقود داخل `contracts/`: Public API، Worker/tool protocol، capability baseline، action state machine، realtime events، sensitive-data policy، وUI contract.
- [x] تم توثيق أن `tests/endpoint_inventory.json` الحالي stale ولا يثبت 100%، واعتماد runtime `EndpointDataSource` + reachable frontend AST/import graph + مراجعة semantic كشرط إصدار.
- [x] تم تشغيل `update-agent-context.sh codex` وتحديث `AGENTS.md` إلى خطة 169 مع إبقاء خطة 168 وخطة العنقود 166.
- [x] نجح `validate_spec_plan_quality.py --spec-dir specs/169-admin-ai-agent`، ونجح parsing عقد OpenAPI 3.1 وعدد مساراته 14، ونجح `git diff --check` للوثائق.

### Phase 4 Speckit-Tasks Evidence / إثبات تفصيل المهام

- [x] تم إنشاء `tasks.md` بعدد 212 مهمة ذرية متسلسلة T001–T212 بلا تكرار أو أرقام ناقصة، من تثبيت baseline الحقيقي حتى تقرير go/no-go النهائي.
- [x] تغطي المهام القصص الخمس، كل مجالات القراءة وكل إجراءات الأدمن الحالية، التأكيد العادي والقوي، الإدخال الآمن، المالية والعمليات الخارجية والجماعية، استخراج الكتابات المباشرة، وسجل التدقيق والتعافي.
- [x] أُدرجت اختبارات test-first، بوابة تغطية ثنائية الاتجاه، اختبارات منع أي أثر أثناء المعاينة، idempotency والتزامن، حجب الأسرار، Docker والمزود الحقيقي وmanual QA والمراجعات النهائية الإلزامية.
- [x] لم يكن `setup-tasks.sh` موجودًا في نسخة Spec Kit الحالية؛ استُخدم القالب القانوني `.specify/templates/tasks-template.md` مباشرة دون تغيير النطاق أو تجاوز أداة الجودة.
- [x] نجح `validate_tasks_quality.py --tasks specs/169-admin-ai-agent/tasks.md`، وبقيت Phase 5 وكل التنفيذ غير معتمدين ومتوقفين حتى موافقة جديدة من المستخدم.

---

## Current Speckit-All Run / التشغيل الحالي

**Feature**: `167-platform-speed-completion`
**Scope**: تنفيذ جميع نتائج مراجعة سرعة المنصة والتنقل والشاشات والـAPI والبنية الإنتاجية، ودمج كل تغييرات الـworking tree الحالية، ثم نشر الإصدار تدريجيًا على العقد الثلاث دون توقف بعد نجاح جميع البوابات.

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)
- [x] Phase 5: Implementation (`speckit-implement`)
- [x] Phase 6: Deep Architectural, Code & UI/UX Critique
- [x] Phase 7: Clean Code Guard (`clean-code-guard`)
- [x] Phase 8: Test Guard (`test-guard`)
- [ ] Phase 9: Feature Tests, Final Verification & Summary Report

### Approved Feature Brief / ملخص الميزة المعتمد

- **المشكلة:** بطء التجربة الفعلية ناتج عن إعادة تركيب الـshells، تعطيل prefetch، أحجام JavaScript كبيرة، طلبات بيانات متكررة أو ضخمة، حركة مكلفة، واستعلامات Backend غير فعالة، مع قصور في القياس المفصول حسب المسار والجهاز.
- **الهدف:** تنفيذ جميع توصيات `docs/platform-speed-navigation-ui-audit-2026-07-29.md` مع الحفاظ على السلوك والصلاحيات، ورفع الإصدار الكامل إلى Production بأدلة قياس وبناء واختبار ونشر.
- **الأدوار:** الزائر، الطالب، ولي الأمر، المعلم، المساعد، الموظف، الإدارة، الدعم المباشر، وفريق التشغيل.
- **النطاق:** Frontend والتنقل والـshells والتحميل والحركة والوصول والصور والخطوط والـcaching؛ Backend والجلسات وLive Support والـOutbox والمراقبة؛ كل تغييرات الـworking tree الحالية حتى التي لم ينشئها هذا الـrun؛ migrations وFrontend وBackend وWorker وإعدادات الإنتاج.
- **السلوك المطلوب:** shell ثابت داخل الـsurface، طلبات أقل وأصغر، query cache موحدة، انتقالات session سليمة، قوائم كبيرة server-paginated، حركة آمنة، قياسات RUM قابلة للتجزئة، واختبارات workflows واقعية.
- **الصلاحيات:** الحفاظ على كل قواعد الوصول الحالية وتوحيد سياسة route/navigation المتعارضة دون توسيع غير مقصود للصلاحيات.
- **النشر:** rolling deployment على العقد الثلاث دون downtime؛ تتوقف العملية وتنفذ rollback عند فشل build أو migration gate أو health/smoke/acceptance checks.
- **سلامة البيانات:** لا migrations تدميرية غير قابلة للعكس ولا حذف بيانات؛ كل migration لها بوابة forward compatibility وإثبات تطبيق.
- **القبول:** LCP p75 أقل من 2s، INP p75 أقل من 200ms، CLS p75 أقل من 0.1، warm navigation p75 أقل من 300ms، API reads المعتادة p95 أقل من 250ms، وصفر duplicate GETs في التنقل الطبيعي بعد اكتمال عينة قياس كافية.
- **قرار المستخدم:** نشر جميع التعديلات الموجودة أو التي تظهر أثناء التنفيذ، حتى غير المنشأة بواسطة هذا الـrun، ضمن نفس الإصدار بعد نجاح التحقق؛ أي تعديل بعد sealing يبطل المرشح ويستلزم rebuild وإعادة البوابات.
- **قرار rollback:** rollback تلقائي للتطبيق فقط؛ تبقى قاعدة البيانات على الـschema التوافقية الجديدة، وأي علاج للمخطط يكون forward-only.
- **قرار RUM:** لا انتظار لنافذة زمنية أو عدد مشاهدات ثابت كشرط نشر؛ بوابات الأداء والصحة الفورية تحكم الإصدار، وRUM اللاحقة تُعرض مع حجم عينتها.
- **قرار الجهاز المحلي:** ممنوع تنزيل أو تثبيت packages أو SDKs أو browser binaries أو Docker images على جهاز المستخدم؛ نستخدم الموجود محليًا فقط، وننقل البوابات التي تحتاج مواد مفقودة إلى الباني البعيد المراجع.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: `/root/phase1_perf_spec_support` → راجع تقرير الأداء وخطة العنقود واتفاقيات المواصفات وحالة الـworking tree، وحدد المستخدمين والمتطلبات ومعايير القياس ومخاطر rollback/RUM/scope-freeze دون كتابة ملفات.
- [x] Phase 2 clarify support: `/root/phase2_perf_clarify_support` → حلّل أخطر نقاط الغموض في rollback وتجميد نطاق التغييرات؛ اعتمد المستخدم rollback للتطبيق فقط، واختار إدخال كل تغييرات الـworkspace حتى لحظة النشر.
- [x] Phase 3 plan support: `/root/phase3_perf_plan_support` → راجع البنية الفعلية كاملة وحدد فجوتي بصمة المصدر الجزئية وrollback العقد المتقدمة، وقدّم قرارات backend/worker/observability/release واختباراتها.
- [x] Phase 3 frontend architecture support: `/root/frontend_arch_plan` → راجع layouts والـshells والـquery groundwork وWebGL والصور والخطوط والحركة والوصول، وحدد ترتيبًا dependency-aware واختبارات regression لكل surface.

### Phase 3 Speckit-Plan Evidence / إثبات التخطيط

- [x] إنشاء `plan.md` بسياق تقني فعلي وConstitution gates وسبع موجات تنفيذ من baseline حتى rolling production.
- [x] إنشاء `research.md` بقرارات shell/query cache/prefetch/WebGL/live-support/auth/outbox/RUM/sealing/rollback والبدائل المرفوضة.
- [x] إنشاء `data-model.md` وعقود client query/navigation وperformance/observability وrelease/rollback، مع `quickstart.md`.
- [x] تحديث سياق `AGENTS.md` إلى خطة 167 عبر أداة Spec Kit.
- [x] نجاح `validate_spec_plan_quality.py --spec-dir specs/167-platform-speed-completion`.

### Phase 4 Speckit-Tasks Evidence / إثبات تفصيل المهام

- [x] إنشاء `tasks.md` بعدد 130 مهمة متسلسلة تغطي baseline، العقود المشتركة، القصص الست، المراجعات، كل تغييرات الـworkspace، والـrolling production.
- [x] تضمين اختبارات قبل التنفيذ وDocker/manual QA/report checkpoint لكل قصة وموجة.
- [x] نجاح `validate_tasks_quality.py --tasks specs/167-platform-speed-completion/tasks.md`.

### Feature Test Evidence / إثبات اختبارات الفيتشر

- [x] Frontend strict typecheck, ESLint, query contracts, accessible colors,
  route-budget contracts, and Web Vitals contracts passed with already-present
  local dependencies.
- [x] Backend Application, worker, and production/SSH test runs reported 556
  passed with one pre-existing skip, 75/75, and 426 passed with six
  environment-dependent skips respectively. These counts were observed during
  execution; a release-bound `verification.json` has not yet been assembled,
  so they are not treated as final acceptance evidence.
- [x] Real PostgreSQL encrypted backup, isolated restore, migration, real-data
  validation, and N-1 readiness passed.
- [x] Three-node rolling deployment health, automatic app-only rollback,
  Cloudflare, backup schedules, current-release parity, and read-only browser
  smoke passed.
- [ ] Full authenticated k6 and Chromium/WebKit/four-role evidence remain open;
  therefore Phase 9 and feature acceptance remain incomplete.

---

## Current Speckit-All Run / التشغيل الحالي

**Feature**: `166-three-node-production-cluster`
**Scope**: تجهيز Production Cluster من ثلاث عقد، كلها تشغّل التطبيق وتشارك في توزيع الحمل، مع قاعدة بيانات موحدة عالية الإتاحة، تخزين ملفات رئيسي/احتياطي، نشر آمن، واختبارات فشل قبل ربط Cloudflare.

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)
- [x] Phase 5: Implementation (`speckit-implement`)
- [x] Phase 6: Deep Architectural, Code & UI/UX Critique
- [x] Phase 7: Clean Code Guard (`clean-code-guard`)
- [x] Phase 8: Test Guard (`test-guard`)
- [x] Phase 9: Feature Tests, Final Verification & Summary Report

### Approved Feature Brief / ملخص الميزة المعتمد

- **المشكلة:** إعداد النشر الحالي يفترض سيرفر اختبار واحد ويحتفظ بقاعدة البيانات وRedis والملفات محليًا، فلا يحقق توزيع حمل أو استمرارًا آمنًا عند سقوط عقدة.
- **الهدف:** تشغيل المنصة على السيرفرات الثلاثة، بحيث تشغّل كل عقدة التطبيق وتشارك في الحمل، ويعمل الاستقبال والتوزيع من داخل هذه العقد مع انتقال تلقائي، وتبقى البيانات والملفات والخدمات المشتركة متسقة.
- **العقد المعتمدة:** `191.218.161.76` و`191.218.161.78` و`168.231.106.230`.
- **قاعدة البيانات:** Production جديدة فارغة؛ قاعدة واحدة منطقيًا، Primary واحدة ونسختان حيتان متزامنتان مع failover تلقائي، منع split-brain، تدقيق schema/migrations/indexes/constraints، وbackup مع restore فعلي.
- **الخدمات المشتركة:** Redis موحد عالي الإتاحة، SignalR backplane، BullMQ موزع، وdistributed locks لكل scheduled/background operation غير الآمن للتكرار.
- **الملفات:** مخزن رئيسي على عقدة واحدة، نسخة حية احتياطية على عقدة ثانية، endpoint منطقي واحد، وتحويل تلقائي عند فشل المخزن الرئيسي.
- **الأمن والنشر:** تدوير الأسرار المكشوفة، SSH keys ومستخدم deploy، شبكة داخلية مشفرة، منافذ داخلية غير عامة، صور نشر موحدة، rolling deployment وrollback.
- **الاختبارات:** اختبارات الكود والبنية، اتساق البيانات والملفات بين العقد، ضغط، فشل كل عقدة، failover لقاعدة البيانات وRedis والتخزين والاستقبال، ومنع تكرار jobs.
- **الدومين:** لا يوجّه قبل نجاح اختبارات الـcluster؛ بعد النجاح يُربط عبر Cloudflare العادي مع proxy والحماية، بدون Cloudflare Load Balancing المدفوع.
- **خارج النطاق:** نقل بيانات السيرفر التجريبي، قواعد بيانات مستقلة لكل عقدة، توجيه الدومين قبل بوابات القبول، أو حذف تغييرات محلية غير مرتبطة.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: `/root/phase1_specify_support` → راجع Compose والتخزين وSignalR والworkers وmigration surface والمهارة الحالية، وقدّم actors ومتطلبات وحالات فشل ومعايير قبول ومخاطر دون كتابة ملفات.
- [x] Phase 2 clarify support: `/root/phase2_clarify_support` → حصر الغموض الحقيقي في سياسة النسخ، bootstrap أول Admin، وخريطة الدومينات، وأجّل آليات failover وRedis والتخزين إلى Phase 3؛ تم اعتماد سياسة نسخ قليلة الحمل وتسجيلها في المواصفة.
- [x] Phase 3 plan support: `/root/phase3_plan_support` → راجع الشبكة العامة فقط، Cloudflare Tunnel/HAProxy، Patroni/etcd، Redis Sentinel، Gluster arbiter، migrations، التخزين المحلي، background jobs، SSH skill والاختبارات، وكشف seed Admin/Teacher ثابت داخل migrations دون تعديل ملفات.

### Phase 3 Speckit-Plan Evidence / إثبات التخطيط

- [x] تم تشغيل `SPECIFY_FEATURE=166-three-node-production-cluster .specify/scripts/bash/setup-plan.sh --json` وإنشاء `plan.md`.
- [x] تم إنشاء `research.md` و`data-model.md` و`quickstart.md` وثلاثة عقود داخل `contracts/` للصحة/التوجيه وبيئة Production وعمليات الـcluster.
- [x] تم اعتماد Cloudflare Tunnel واحد بثلاث replicas لاستمرار الدخول، مع HAProxy على كل عقدة لتوزيع الحمل فعليًا على نسخ التطبيق الثلاث.
- [x] تم اعتماد WireGuard، PostgreSQL/Patroni/etcd بكاتب واحد، Redis/Sentinel، وGlusterFS ببيانات كاملة على عقدتين وarbiter على الثالثة لمنع split-brain.
- [x] تم توثيق continuous WAL/PITR بحد 5 دقائق، Differential يومي وFull أسبوعي، ملفات incremental كل ساعة، احتفاظ 30 يومًا، وGarage bucket مشفر ذاتي الاستضافة ومكرر على السيرفرات الثلاثة مع restore شهري.
- [x] تم اكتشاف أن `AddIbrahimAdmin` و`AddMultiTeacherSubjectArchitecture` يضيفان هويات/بيانات ثابتة، وتوثيق إزالة مسار الـseed السري وforward cleanup واختبار قاعدة فارغة كبوابة إلزامية.
- [x] تم تشغيل `update-agent-context.sh codex` وتحديث إشارة خطة Spec Kit في `AGENTS.md`.
- [x] نجح `validate_spec_plan_quality.py` بعد التحقق من قابلية قياس المواصفة وعدم وجود clarifications معلقة.

### Phase 4 Speckit-Tasks Evidence / إثبات تفصيل المهام

- [x] تم إنشاء `specs/166-three-node-production-cluster/tasks.md` من المواصفة والخطة والبحث ونموذج البيانات والعقود.
- [x] يحتوي الملف على 113 مهمة ذرية متسلسلة T001–T113، منها 52 فرصة متوازية، ومقسمة إلى Setup وFoundation ثم قصص التشغيل والبيانات والتنسيق والملفات والنشر والـcutover والمراجعات النهائية.
- [x] لكل قصة اختبار مستقل ومسارات ملفات دقيقة؛ US1=12، US2=13، US3=14، US4=14، US5=10، US6=13 مهمة.
- [x] نجح `validate_tasks_quality.py`، وتتضمن النهاية deep review و`clean-code-guard` و`test-guard` وfeature tests وبوابات Docker وmanual QA.

### Feature Test Evidence / إثبات اختبارات الفيتشر

### Current Production Commissioning Evidence / إثبات تجهيز Production الحالي

- [x] التطبيق الكامل يعمل على العقد الثلاث بنفس release والصور؛ الجولة القصيرة وزعت الحمل بالتساوي، والجولة الطويلة وزعت 36,000 طلب بالتساوي 12,000/12,000/12,000.
- [x] قاعدة PostgreSQL واحدة منطقيًا تحت Patroni: كاتب واحد ونسختان، ونجح failover مع حفظ الكتابة المؤكدة خلال 9 ثوانٍ.
- [x] Redis موحد تحت Sentinel ونجح failover خلال 9 ثوانٍ، وGluster `replica 3 arbiter 1` بدون heal backlog أو split brain.
- [x] قاعدة PostgreSQL 16 نظيفة وصلت إلى 129 migration؛ 203 relation و2,245 column و713 index و634 constraint، مع صفر orphan/duplicate/invalid/unvalidated/forbidden/critical findings وأدوار النظام فقط دون مستخدم ثابت.
- [x] منافذ التطبيق والبيانات الداخلية مغلقة عن الإنترنت، والـSSH key-only؛ تم إصلاح drift في HAProxy على node-2/node-3 وتأكيد checksum موحد.
- [x] آخر اختبارات العمليات وSSH: 397 passed و6 gated skipped، مع نجاح `git diff --check` وPython compile وBash syntax وFrontend lint/typecheck، ونجاح بناء Backend/Frontend/Worker على الـRemote Builder المقيّد في node-3.
- [x] PostgreSQL leases/fencing، stable outbox job IDs، shared atomic storage، rolling deploy/rollback، acceptance وCloudflare orchestration اكتملت محليًا ونجحت بوابات Clean Code/Test/Docs.
- [x] تم إنشاء Garage S3 bucket داخلي بتكرار 3 على السيرفرات الثلاثة، ونجح Restic backup وisolated cross-node file restore وpgBackRest stanza/WAL archiving وأول full DB backup؛ لا توجد حاجة إلى S3/R2 خارجي.
- [x] نجح اختبار PITR لخمس دقائق في PostgreSQL مع probe حقيقي وschema/index/role checks داخل نسخة معزولة، وتم تفعيل جداول DB/files backup والاحتفاظ واختبارات restore على العقد الثلاث مع primary/cluster locks لمنع الحمل المكرر.
- [x] اختبار file brick عزل المنفذ الديناميكي الحقيقي، حافظ على كل الكتابات، لم ينتج split brain، وسجّل 2.18 ثانية recovery؛ بوابة الملفات أصبحت ناجحة.
- [x] جولة الحمل النهائية لمدة 30 دقيقة نجحت وظيفيًا: 3,600 طلب بلا أخطاء أو drops، p95=20.117ms، و10 اتصالات WebSocket ثابتة، وتوزيع 1,197/1,191/1,212 على العقد الثلاث. نتيجة CPU steal بقيت أعلى من حد 5% وسُجلت كما هي، واعتمد مالك المنصة استثناءها صراحةً من قرار الإقفال في 2026-07-29.
- [x] تم إصلاح canonical schema hash، وإنشاء N-1 proof حديث، وتنفيذ rollback حقيقي للإصدار السابق ثم إعادة الإصدار الحالي rolling بدون down-migration.
- [x] اختبار readiness failure عزل gateway على `node-3` بعد drain، وحافظ على 60/60 طلب على `node-1/2` بتوزيع 30/30، ثم أعاد `node-3` healthy و`UP` على كل ingress.
- [x] Cloudflare Tunnel يعمل بثلاث replicas، كل connector يملك 4 اتصالات HA، والـ8 hostnames تعمل عبر Cloudflare؛ نجح عزل connector على node-3 مع 30/30 طلب ناجح ثم استعادته، والمنافذ الأصلية غير العامة بقيت مغلقة.
- [x] تم إنشاء أول Production Admin ذريًا عبر protected stdin، ونجح login بدور Admin والتحقق من endpoint محمي على العقد الثلاث بدون تسجيل كلمة المرور.
- [x] اكتملت manual QA للأدوار Student/Teacher/Staff والصلاحيات، ثم حُذفت الحسابات المؤقتة؛ نجح upload ثم read byte-identical عبر عقد أخرى ثم delete، ونجح SignalR live/reconnect بعد سقوط عقدة مع Redis backplane.
- [x] نُشر الإصدار immutable `src-bdf19804cf29d19634b131a16d3e519d26f0d425` تدريجيًا على العقد الثلاث بعد encrypted backup وisolated restore migration gate؛ status/audit/backups وDB archive وfile backup كلها ناجحة بعد اختبارات الفشل.
- [x] قرار القبول الآلي الموقّع محفوظ بدون تعديل كـ`NO-GO` لشفافية دليل CPU steal، واعتمد مالك المنصة `GO WITH OWNER WAIVER` لهذا المؤشر فقط. لا توجد مشكلة تطبيق/عنقود أخرى، واكتملت Phase 9 مع توثيق الاستثناء بدل تزوير نتيجة القياس.

---

## Current Speckit-All Run / التشغيل الحالي

**Feature**: `165-teacher-finance-center`
**Scope**: مركز مالي إداري موحد لاتفاقات المدرسين والمبيعات والأكواد والباقات المشتركة والتسويات والفواتير وتكلفة Bunny بالدولار.

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)
- [ ] Phase 5: Implementation (`speckit-implement`)
- [ ] Phase 6: Deep Architectural, Code & UI/UX Critique
- [ ] Phase 7: Clean Code Guard (`clean-code-guard`)
- [ ] Phase 8: Test Guard (`test-guard`)
- [ ] Phase 9: Feature Tests, Final Verification & Summary Report

### Approved Feature Brief / ملخص الميزة المعتمد

- **المشكلة:** حسابات المدرسين والمبيعات والأكواد والباقات المشتركة وتكلفة تشغيل الفيديوهات موزعة ولا تعطي رصيداً موحداً قابلاً للتسوية.
- **الهدف:** مركز مالي سهل للأدمن يحسب اتفاقات المدرسين، البيع، الخصم، الأكواد، الباقات المشتركة، التسويات، الفواتير، ومديونيات المدرسين مع تكلفة Bunny الفعلية بالدولار.
- **الأدوار:** الأدمن فقط يدير ويعرض مركز المالية؛ الطالب مصدر لحركات البيع؛ المدرس لا يملك وصولاً للمركز في هذا الإصدار.
- **السلوك المؤكد:** الاتفاق الافتراضي عند إضافة المدرس مع استثناءات للحصة/السيكشن/الترم/الباقة/الفيديو/الامتحان/الأكواد؛ الأدمن يختار حامل الخصم لكل بيع؛ توقيت حساب كل دفعة أكواد عند التسليم أو التفعيل؛ عند الإلغاء بعد الصرف يختار الأدمن مديونية أو خصماً قادماً؛ تكلفة Bunny تبقى بالدولار.
- **خارج النطاق:** التحويل التلقائي للمدرسين، التكامل الضريبي، التحويل الإجباري بين الدولار والجنيه، ووصول الموظفين أو المدرسين لمركز المالية.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: `/root/specify_support` → راجع نواة الحسابات الحالية، وحدد نموذج الاتفاقات والتسويات والباقات المشتركة وBunny، والمخاطر والاختبارات دون كتابة ملفات.
- [x] Phase 2 clarify support: `/root/clarify_support` → اقترح توضيحات المرتجع الجزئي، تجاوز حصص الباقة المشتركة، وتأكيد تسليم الأكواد؛ تم اعتماد القرارات وتسجيلها في المواصفة.
- [x] Phase 3 plan support: `/root/plan_support` → راجع الـledger الحالي، مسارات البيع والأكواد والباقات وBunny، وحدد نموذج الاتفاقات والتسويات والعقود والمخاطر والاختبارات دون كتابة ملفات.

### Phase 3 Speckit-Plan Evidence / إثبات التخطيط

- [x] تم تشغيل `SPECIFY_FEATURE=165-teacher-finance-center .specify/scripts/bash/setup-plan.sh --json` وإنشاء `plan.md`.
- [x] تم إنشاء `research.md` و`data-model.md` و`quickstart.md` وعقد API الإداري داخل `contracts/`.
- [x] تم اعتماد توسيع الـledger المالي الحالي بدلاً من إنشاء دفتر موازٍ، مع لقطات اتفاق/خصم غير قابلة للتعديل وربط بنود التسوية بمصادرها لمنع الدفع المكرر.
- [x] تم توثيق تأكيد تسليم الأكواد المنفصل، اختيار بنود المرتجع الجزئي، تحذير خسارة الباقة المشتركة، وفصل تكلفة Bunny بالدولار الفعلية/التقديرية/الناقصة.
- [x] تم استخدام Impeccable وUI/UX Pro Max لتخطيط واجهة مالية كثيفة البيانات مع الحفاظ على هوية Massar وTajawal بدلاً من الألوان والخطوط العامة المقترحة.
- [x] تم تشغيل `update-agent-context.sh codex` وتحديث إشارة خطة Spec Kit في `AGENTS.md`.

### Phase 4 Speckit-Tasks Evidence / إثبات تفصيل المهام

- [x] تم إنشاء `specs/165-teacher-finance-center/tasks.md` بعد فحص وثائق الخطة والعقود ونموذج البيانات.
- [x] يحتوي الملف على 60 مهمة ذرية T001–T060 مقسمة حسب قصص المستخدم، مع مسارات ملفات واختبارات وتبعيات وفرص تنفيذ متوازٍ.
- [x] تم تشغيل `validate_tasks_quality.py` بنجاح؛ تشمل النهاية مراجعة معمارية وواجهة، `clean-code-guard`، `test-guard`، اختبارات الميزة وبوابة Docker.

## Current Speckit-All Run / التشغيل الحالي

**Feature**: `164-comprehensive-hr-platform`
**Scope**: منظومة موارد بشرية متكاملة على مراحل تشمل دورة حياة الموظف والشفتات والحضور والإجازات والرواتب والخدمة الذاتية والحوكمة والترحيل الآمن.

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)
- [ ] Phase 5: Implementation (`speckit-implement`)
- [ ] Phase 6: Deep Architectural, Code & UI/UX Critique
- [ ] Phase 7: Clean Code Guard (`clean-code-guard`)
- [ ] Phase 8: Test Guard (`test-guard`)
- [ ] Phase 9: Feature Tests, Final Verification & Summary Report

### Approved Feature Brief / ملخص الميزة المعتمد

- **المشكلة:** نواة HR الحالية تفصل إنشاء الحساب عن الملف الوظيفي، ولا تمثل الهيكل والشفتات والإجازات والرواتب ودورة حياة الموظف بصورة متكاملة وآمنة.
- **الهدف:** إنشاء منظومة HR كاملة داخل Feature واحد على مراحل، تبدأ بإصلاح سلامة الحضور والصلاحيات وإنشاء الموظف الذري، وتنتهي بالرواتب والخدمة الذاتية والمستندات والعهد والأداء والجزاءات والتوظيف والتقارير والترحيل.
- **الأدوار:** الموظف، المدير المباشر، HR، المالية، المدير العام، ومدير النظام.
- **الحضور:** ثلاث سياسات قابلة للتعيين لكل شفت أو موظف: حر، نطاق جغرافي، أو جهاز موثوق، مع استثناءات العمل عن بعد.
- **الموافقات:** المدير المباشر ثم HR للطلبات؛ المالية تراجع الرواتب؛ المدير العام يعتمد الصرف النهائي.
- **الرواتب:** محرك مؤرخ وقابل للتخصيص للراتب والبدلات والحوافز والعمولات والحضور والإضافي والسلف والقروض والضرائب والتأمينات.
- **الترحيل:** ترحيل كامل وآمن للبيانات الحالية مع تشغيل تجريبي وتسوية قبل/بعد ومنع الفقد والتكرار وسجل مراجعة.
- **خارج النطاق:** أجهزة البصمة الخارجية، التحويل البنكي التلقائي، الإقرارات الحكومية الإلكترونية المباشرة، وتطبيق الهاتف الأصلي.
- **القرارات المؤكدة:** الطلاب والمدرسون ليسوا موظفين تلقائيًا؛ إنشاء الموظف عملية واحدة؛ السجلات المالية والتاريخية لا تحذف بإنهاء الحساب.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: `/root/hr_specify_support` → أكد نطاق الموظف والهيكل والشفتات والحضور والإجازات والرواتب ودورة الحياة والترحيل، واقترح كيانات ومعايير قبول ومخاطر التوضيح دون كتابة ملفات.
- [x] Phase 2 clarify support: `/root/hr_clarify_support` → راجع المواصفة واقترح قرارات الشركة الواحدة وربط الموظف بالحساب والتفويض والتحويل التدريجي والاحتفاظ، مع تأجيل تفاصيل التنفيذ التقني إلى التخطيط.
- [x] Phase 3 plan support: `/root/hr_plan_support` → راجع النواة الحالية وحدد ملفات الإنشاء والملف والحضور والإجازات والرواتب والصلاحيات، وقدم قيود البيانات وموجات التشغيل واختبارات PostgreSQL وE2E دون تعديل ملفات.

### Phase 3 Speckit-Plan Evidence / إثبات التخطيط

- [x] تم تشغيل `SPECIFY_FEATURE=164-comprehensive-hr-platform .specify/scripts/bash/setup-plan.sh --json`.
- [x] تم إنشاء `plan.md` و`research.md` و`data-model.md` و`quickstart.md` وخمسة عقود داخل `contracts/`.
- [x] تم توثيق الفصل الصريح للموظف عن المدرس/الطالب، الإنشاء الذري، التاريخ المؤرخ، الموافقات والتفويض والتصعيد، حضور الشفت الليلي، payroll snapshots، والـmodule rollout بلا dual-write.
- [x] تم تقسيم التنفيذ إلى Wave 0 للأمان ثم ملفات/هيكل، شفتات/حضور، إجازات، رواتب، خدمة ذاتية، وباقي دورة الحياة.
- [x] تم تحديث `AGENTS.md` وتشغيل `.specify/scripts/bash/update-agent-context.sh codex`.
- [x] تم دمج نتائج Impeccable وUI/UX مع الحفاظ على هوية Massar ورفض اقتراح dark/App-Store العام غير الملائم.

## Current Speckit-All Run / التشغيل الحالي

**Feature**: `162-unify-frontend-design-system`
**Scope**: توحيد واجهة Frontend كاملة لكل المسارات والمكونات مع تباين WCAG AA في light/dark، دون تغيير أي سلوك أو منطق أعمال.

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)
- [ ] Phase 5: Implementation (`speckit-implement`)
- [ ] Phase 6: Deep Architectural, Code & UI/UX Critique
- [ ] Phase 7: Clean Code Guard (`clean-code-guard`)
- [ ] Phase 8: Test Guard (`test-guard`)
- [ ] Phase 9: Feature Tests, Final Verification & Summary Report

### Approved Feature Brief / ملخص الميزة المعتمد

- المشكلة: أنظمة ألوان ومكونات متكررة وغير متسقة تسبب تباينًا غير مكتمل في light/dark.
- الهدف: إصدار Frontend واحد يوحد كل الأسطح والألوان والمكونات مع الحفاظ على ترتيب الشاشات، النصوص، الوظائف، الصلاحيات، والمنطق الحالي.
- النطاق: العام والطالب والمعلم والمساعد والإدارة والدعم المباشر، بما في ذلك التحميل والفراغ والخطأ والتعطيل.
- خارج النطاق: API وقاعدة البيانات والصلاحيات ومنطق الأعمال.
- القبول: WCAG AA، توكنز ومكونات مشتركة، قائمة سماح موثقة للألوان الخام، وفحوصات تغطي كل مجموعات المسارات.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: /root/specify_context → حدد النطاق والأدوار والمتطلبات القابلة للاختبار والمخاطر دون كتابة ملفات.
- [x] Phase 2 clarify support: `/root/clarify_context` → راجع أسئلة التباين، نطاق المسارات، وحوكمة الألوان والاستثناءات.
- [x] Phase 3 plan support: `/root/plan_context` → حدد نقاط الترحيل، جرد المسارات، وفحوصات التحقق دون تغيير مسار websocket.

### Implementation Wave 1 / موجة التنفيذ الأولى

- [x] توحيد `useAdminTheme` مع توكنز Massar الحالية في light/dark بدل لوحة slate المنفصلة.
- [x] ترحيل `LiveSupportStateNotice` و`LiveSupportEmptyState` و`LiveSupportSkeleton` إلى semantic admin tokens مع الحفاظ على النصوص والسلوك.
- [x] ترحيل مكوّنات الإدارة المشتركة `AdminStatCard` و`AdminModal` و`AdminDataTable` إلى semantic tokens، بما في ذلك الشفافية والحالات المعطلة.
- [x] ترحيل مكوّنات مجتمع الطالب `CommunityPostComposer` و`CommunityPostLikeButton` و`CommunityPostPoll` إلى semantic tokens.
- [x] إضافة تقرير route inventory وأداة `check:design-tokens` لتتبع الألوان الخام قبل إغلاق الـ allowlist.
- [x] Frontend lint/typecheck/build وlive-support contract checks نجحت بعد الموجة الأولى.
- [ ] إكمال ترحيل بقية الأسطح والمكونات ثم تشغيل فحص `--check` بدون مخالفات.

## Current Speckit-All Run / التشغيل الحالي

**Feature**: `161-teacher-profile-visibility`
**Scope**: إدارة بيانات المدرسين بالكامل مع إخفاء مستقل للمدرس والمحتوى عن الطلاب والزوار.

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)
- [x] Phase 5: Implementation (`speckit-implement`)
- [x] Phase 6: Deep Architectural, Code & UI/UX Critique
- [x] Phase 7: Clean Code Guard (`clean-code-guard`)
- [x] Phase 8: Test Guard (`test-guard`)
- [x] Phase 9: Feature Tests, Final Verification & Summary Report

### Approved Feature Brief / ملخص الميزة المعتمد

- **المشكلة:** لا يملك الـ Admin تحكماً كاملاً في بيانات المدرس أو ظهوره ومحتواه أمام الطلاب والزوار.
- **الهدف:** تمكين الـ Admin من تعديل كل بيانات المدرس، وإخفاء المدرس والمحتوى بشكل مستقل، مع حفظ السجلات وإتاحة الإظهار لاحقاً.
- **الأدوار:** الـ Admin منفذ العمليات؛ الطلاب والزوار متلقو البيانات المنشورة؛ المدرس متأثر بالتعديل والظهور.
- **السلوك المؤكد:** تعديل جميع بيانات المدرس بما فيها بيانات الدخول والملف الشخصي؛ إخفاء المدرس لا يحذف الحساب؛ إخفاء المحتوى لا يحذف المحتوى أو المشتريات؛ الزرّان مستقلان؛ المحتوى المخفي لا يظهر حتى للمشترين السابقين ولا يمكن فتحه بالرابط المباشر؛ عند الإظهار يعود الظهور والوصول.
- **الصلاحيات:** الـ Admin فقط يستطيع التعديل أو الإخفاء أو الإظهار؛ يجب رفض العمليات من غير الـ Admin من الـ Backend.
- **النطاق:** كل واجهات وقوائم وبحث وتوصيات وصفحات الطالب والزائر وأي روابط مباشرة تخص المدرس أو المحتوى.
- **خارج النطاق:** حذف الحساب أو المحتوى أو سجل الشراء، أو منح الصلاحية لموظفين غير الـ Admin.
- **حالات الفشل:** رفض التعديل غير المصرح، رفض البيانات غير الصالحة، إخفاء المحتوى مع بقاء سجلات الشراء، وإظهار المحتوى بعد إعادته.
- **التحقق:** اختبارات API والصلاحيات والتخزين، واختبار واجهة الإدارة، واختبار عدم الظهور وعدم الوصول للمشتري السابق.

### Current Run Evidence / إثبات التشغيل الحالي

- [x] Schema/state: أضيفت حقلا `TeacherProfile.IsVisibleToStudents` و`IsContentVisibleToStudents` بقيمة افتراضية `true` مع migration `20260713124855_AddTeacherVisibilityControls`.
- [x] Admin update: عقد التعديل يدعم الاسم والهاتف وكلمة سر write-only والملف والمواد والعمولة والروابط وحالتي الظهور، مع audit غير سري وإلغاء refresh tokens عند تغيير كلمة السر.
- [x] Public/student enforcement: تم تطبيق إخفاء المدرس في قوائم/تفاصيل المدرسين، وإخفاء المحتوى في الباقات والدروس والامتحانات والباقات المشتركة والمجتمع، ومنع الشراء والوصول المباشر للمشتري السابق مع استعادة الوصول بعد الإظهار.
- [x] Realtime: `StaffRealtimeChangeDetector` كان يتضمن `TeacherProfile` ضمن scopes `users` و`subjects`؛ الحقول الجديدة تدخل تلقائياً في نفس حدث التغيير بدون أي قيمة كلمة سر.
- [x] Feature tests: `TeacherVisibilityTests` يغطي public exclusion، admin full update/password audit، وdeny/restore للـ previous purchaser: `3/3 passed`.
- [x] Backend verification: `dotnet build ... -c Release --no-restore` → 0 warnings/0 errors؛ full application tests → `387 passed, 1 skipped`.
- [x] Frontend verification: `npm run lint`, `npm run typecheck`, و`npm run build` نجحت.
- [x] Clean Code Guard: تم إصلاح إعادة ضبط العمولة إلى صفر، منع تعديل بيانات الدخول في نموذج القائمة، والتحقق من subject IDs قبل أي mutation لتفادي partial state.
- [x] Test Guard: الاختبارات تستخدم `AppDbContext` الحقيقي في الذاكرة وكيانات حقيقية، وتتحقق من السلوك والآثار المرئية دون mock داخلي.
- External readiness: `docker compose config -q` نجح، بينما لم تُطبّق migration ولم تُشغّل health/manual multi-session QA على بيئة تشغيل فعلية في هذه الجولة.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: unavailable → لا توجد أداة subagent قابلة للاستدعاء في الأدوات الحالية؛ تم تنفيذ دعم المواصفات inline.
- [x] Phase 2 clarify support: unavailable → تم حسم قرارات المنتج أثناء التوضيح العربي قبل بدء Phase 1.
- [x] Phase 3 plan support: unavailable → سيتم تنفيذ handoff إلى `speckit-plan` inline مع توثيق البحث.

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)
- [x] Phase 5: Implementation (`speckit-implement`)
- [x] Phase 6: Deep Architectural, Code & UI/UX Critique
- [x] Phase 7: Clean Code Guard (`clean-code-guard`)
- [x] Phase 8: Test Guard (`test-guard`)
- [x] Phase 9: Feature Tests, Final Verification & Summary Report

### Current Speckit-All Run / التشغيل الحالي

- **Feature**: `160-employee-realtime-refresh`
- **Scope**: تنفيذ خطة إصلاح مشاكل الموظفين والتحديث الفوري للبيانات على كامل النطاق.

### Approved Feature Brief / ملخص الميزة المعتمد

- **المشكلة:** تغييرات الموظفين والصلاحيات وعمليات البيانات لا تنعكس بصورة موحدة وفورية، لأن الصفحات تستخدم تحميلًا يدويًا وcache متفرقًا وSignalR غير موصول فعليًا بمستهلكي البيانات.
- **الهدف:** كل mutation ناجحة تحدث الجلسة الحالية والـactive server state فورًا، وتزامن الجلسات الأخرى عبر SignalR، وتعيد التوافق بعد reconnect، بدون فقد drafts أو تجاوز صلاحيات.
- **الأدوار:** Admin، Staff، HR، Assistant، Teacher، CRM/Operations users، والطلاب المتأثرون بالبيانات المنشورة.
- **السلوك المطلوب:** توحيد query contracts/cache/invalidation، إصلاح session authorization refresh وnavbar/route guards، ترحيل domains تدريجيًا، إزالة reload/force workarounds، وإضافة اختبارات ومراقبة.
- **النطاق المشمول:** كل مراحل الوثيقة، بما فيها inventory لكل mutation/cache/reload، employee lifecycle، permissions، HR، operations، CRM، content، finance، exams/homework، community، notifications، reports، reconnect، conflicts، observability، Docker وE2E verification.
- **غير المشمول:** إعادة تصميم قواعد العمل غير المرتبطة بالتحديث، وإعادة كتابة النظام دفعة واحدة، وإضافة full reload جديد؛ TanStack Query مسموحة إذا أكد التخطيط ملاءمتها.
- **معايير القبول:** same-session خلال ثانية، cross-session permission خلال ثانيتين عند الاتصال، backend 403 فوري عند السحب، drafts محفوظة مع conflict notice، duplicate events بلا duplicate rows/toasts، reconnect بلا document reload.
- **القرارات المؤكدة:** التنفيذ كامل النطاق؛ السماح بإضافة TanStack Query عند ثبوت ملاءمتها.
- **الافتراضات:** الترحيل domain-by-domain مع feature flags/canary، والـbackend يبقى مصدر الحقيقة للصلاحيات والبيانات الدائمة.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: unavailable → لا توجد أداة subagent ظاهرة في الأدوات الحالية؛ تم تنفيذ specification inline.
- [x] Phase 2 clarify support: unavailable → تم تأكيد كل القرارات عالية التأثير أثناء Feature Intent Refinement.
- [x] Phase 3 plan support: unavailable → سيتم تنفيذ handoff إلى `speckit-plan` inline مع توثيق بحث المستودع.

### Approved Feature Brief / ملخص الميزة المعتمد

- **المشكلة أو الفرصة:** المرحلة والصف والمادة موجودة في أجزاء من النظام، لكنها ليست قاعدة موحدة على كل بوابة الطالب. هذا يسمح بظهور أو شراء أو تفعيل محتوى غير مخصص للطالب إذا لم يطبق endpoint أو صفحة معينة الفلترة الصحيحة.
- **الهدف والنتيجة المتوقعة:** كل شيء في بوابة الطالب يتفلتر حسب `EducationStage` و`GradeLevel` من بروفايل الطالب، وحسب المواد المسموحة لهذا الصف/المرحلة، مع السماح فقط بالمحتوى الذي حدده الأدمن صراحة كعام للمنصة أو لكل الصفوف أو لكل المواد.
- **الأدوار المتأثرة:** الطالب، الأدمن/المشرف، المدرس، والنظام الداخلي للأكواد والهدايا والشراء.
- **السلوك الحالي والمطلوب:** حاليا الربط جزئي مثل `Package.TargetGrade` وبعض الكيانات تحتوي `educationStage/gradeLevel`. المطلوب قاعدة موحدة على بوابة الطالب بالكامل: الباقات، الترمات، الشهور/الأقسام، الحصص، الفيديوهات، الامتحانات العامة، المدرسين، المجتمع، العروض/الإشعارات، الأكواد، الكوبونات، الهدايا، الباكدجات المشتركة، وأي صفحة طالب.
- **السيناريو الأساسي:** طالب لديه مرحلة وصف في بروفايله. عند فتح أي صفحة طالب أو محاولة شراء أو تفعيل كود أو استلام Gift، لا يظهر ولا يتم قبول إلا المحتوى المطابق لمرحلته وصفه ومواد صفه، أو المحتوى العام الصريح.
- **النطاق المشمول:** فلترة كل بيانات الطالب من الباك إند وليس الواجهة فقط، منع الشراء والتفعيل والهدايا لغير المطابق، إضافة/توحيد حقول المرحلة والصف والمادة على الكيانات التي تحتاجها، دعم محتوى عام كاستثناء صريح من الأدمن، وربط المواد بالمرحلة والصف حتى المدرسون والمجتمع يتفلترون بشكل صحيح.
- **غير المشمول:** اختيار الطالب لمواد مخصصة يدويا في بروفايله، السماح بالكود أو Gift كاستثناء خارج الصف، أو إظهار محتوى غير مربوط للطالب بشكل مؤقت.
- **قواعد العمل المؤكدة:** أي شيء غير مربوط ولا محدد كعام لا يظهر للطالب. الأكواد والهدايا ترفض لو هدفها غير مطابق للطالب إلا لو الهدف عام. المحتوى العام يظهر لكل الطلاب. المواد المسموحة تأتي من إعدادات المرحلة/الصف وليس من اختيار الطالب.
- **حالات الفراغ والفشل والإلغاء:** لو لا توجد عناصر مطابقة، تعرض صفحات الطالب حالات فارغة واضحة. لو حاول الطالب شراء/تفعيل/فتح عنصر غير مطابق، يرجع رفض واضح بدون إنشاء صلاحية وصول أو معاملة مالية. لو تغير صف الطالب، الظهور والصلاحيات المستقبلية تعاد تقييمها حسب القاعدة، ولا يتم إنشاء وصول جديد لمحتوى غير مطابق.
- **معايير قبول:** طالب في `FirstSecondary` يرى فقط عناصر `FirstSecondary` أو العامة. كود لمحتوى `SecondSecondary` يرفض لطالب `FirstSecondary`. الأدمن لا ينشر/يحفظ محتوى جديد للطلاب بدون ربط مرحلة/صف/مادة أو اختيار عام. المدرس غير المرتبط بمادة مسموحة لصف الطالب لا يظهر. بوست مجتمع خارج صف/مادة الطالب لا يظهر.
- **القرارات المؤكدة:** النطاق هو كل شيء في بوابة الطالب بالكامل. المادة تحسب حسب المواد المسموحة للصف/المرحلة. يوجد محتوى عام للمنصة يظهر لكل الطلاب. الأكواد والهدايا ترفض خارج الصف/المادة إلا لو المحتوى عام.
- **الافتراضات المتبقية:** سيتم استخدام أنماط المشروع الحالية للـ API، EF migrations، صفحات Next.js، والصلاحيات. أي تفاصيل تقنية عن كيفية تمثيل "عام" أو جداول ربط المواد سيتم حسمها في `speckit-plan` بدون تغيير قرارات المنتج المؤكدة.

### Subagent Evidence / إثبات استخدام الوكلاء الفرعيين

- [x] Phase 1 specify support: unavailable → لا توجد أداة subagent ظاهرة في الأدوات الحالية؛ تم تنفيذ specification inline.
- [x] Phase 2 clarify support: 019f34d7-2bfa-7231-9d0d-d93add63557f → اقترح 5 أسئلة عالية التأثير؛ تم استخدام مخرجاته لتأكيد تعدد النطاقات، وراثة النطاق، إعادة تقييم الوصول، وتحقق الأكواد/الكوبونات/الهدايا.
- [x] Phase 3 plan support: 019f34dd-6137-70a2-94d5-82a9d6dcc616 → راجع spec والدستور وأنماط المشروع وأعاد حزمة سياق تخطيط بملفات دقيقة ومخاطر واختبارات؛ تم دمج `GetPackageByIdQuery` و`academic-labels.ts`.

### Phase 2 Clarification Evidence / إثبات التوضيح

- [x] تم تشغيل prerequisite الخاص بـ `speckit-clarify` باستخدام `SPECIFY_FEATURE=159-student-academic-scope-enforcement` لأن الفرع الحالي لا يطابق اسم فيتشر Spec Kit.
- [x] تم طرح 5 أسئلة عربية كحد أقصى وتسجيل كل إجابة في `specs/159-student-academic-scope-enforcement/spec.md`.
- [x] تم توضيح مستويات النطاق العام: عام للمنصة، عام لكل صفوف مرحلة محددة، عام لكل مواد صف محدد.
- [x] تم توضيح أن العنصر يمكن أن يملك عدة نطاقات أكاديمية ويكفي تطابق نطاق واحد.
- [x] تم توضيح وراثة النطاق في المحتوى الهرمي من أقرب أب صريح، مع إلزام النطاق الخاص للابن إن وجد.
- [x] تم توضيح أن الوصول القائم يمنع فورا عند عدم المطابقة الحالية مع بقاء سجل الشراء أو المنحة.
- [x] تم توضيح أن الأكواد والكوبونات والهدايا تتحقق عند الإنشاء لضمان نطاق الهدف، ثم يعاد التحقق عند الاستخدام أو التسليم حسب الطالب الفعلي.

### Phase 3 Speckit-Plan Evidence / إثبات التخطيط

- [x] تم تشغيل `SPECIFY_FEATURE=159-student-academic-scope-enforcement .specify/scripts/bash/setup-plan.sh --json`.
- [x] تم تنفيذ handoff إلى `speckit-plan` بعد قراءة `speckit-plan/SKILL.md` والدستور.
- [x] تم إنشاء `specs/159-student-academic-scope-enforcement/plan.md`.
- [x] تم إنشاء `specs/159-student-academic-scope-enforcement/research.md`.
- [x] تم إنشاء `specs/159-student-academic-scope-enforcement/data-model.md`.
- [x] تم إنشاء `specs/159-student-academic-scope-enforcement/contracts/academic-scope-api.md`.
- [x] تم إنشاء `specs/159-student-academic-scope-enforcement/quickstart.md`.
- [x] تم تحديث `AGENTS.md` بإشارة الخطة تحت علامات SPECKIT.
- [x] أدلة البحث شملت: نموذج scope موحد، mapping للمواد، وراثة النطاق، fail-closed، تحقق الأكواد/الكوبونات/الهدايا، إعادة تقييم grants، وعدم تغيير worker.

### Phase 4 Speckit-Tasks Evidence / إثبات تفصيل المهام

- [x] تم إنشاء `specs/159-student-academic-scope-enforcement/tasks.md` بمهام T001-T083 مرتبة حسب قصص المستخدم والاعتماديات.
- [x] تم تشغيل `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/159-student-academic-scope-enforcement/tasks.md` بنجاح.
- [x] تم تسجيل ترتيب الحراسة الإلزامي في نهاية المهام: deep critique ثم `clean-code-guard` ثم `test-guard` ثم اختبارات الميزة والتحقق النهائي.

### Phase 5 Implementation Evidence / إثبات التنفيذ

- [x] T001: الفرع الحالي `codex/158-teacher-accounting-phase3` مع تنفيذ Spec Kit للفيتشر `159-student-academic-scope-enforcement` عبر `SPECIFY_FEATURE`; لم يتم عكس أي ملفات غير مرتبطة.
- [x] T002: أزواج المرحلة/الصف الحالية: `Secondary` مع `FirstSecondary`, `SecondSecondary`, `SecondaryGrade3`; `Baccalaureate` مع `FirstBaccalaureate`, `SecondBaccalaureate`; `Primary` مع `PrimaryGrade1..6`; `Preparatory` مع `PrepGrade1..3`; `Azhari` مع الصفوف الأزهرية الابتدائية والإعدادية والثانوية؛ `American` مع `AmericanGrade1..12`.
- [x] T003: الحقول القديمة ذات الصلة: `Package.SubjectId`, `Package.TargetGrade`, `PublicExamProduct.IsPlatformWide/GradeLevel/SubjectId`, sales target fields, code group target fields, gift `TargetType/TargetId`, و`SharedTeacherPackage.EducationStage/GradeLevel/SubjectId`.
- [x] T004: ترتيب الآثار الجانبية الحالي يتطلب الفحص الأكاديمي قبل الخصم أو استخدام الكوبون/الكود أو إنشاء `StudentAccessGrant` أو `GiftIssuance` أو outbox/audit المرتبط بنجاح العملية.
- [x] T005: واجهة الإدارة والطالب تحتاج DTO موحد لـ `academicScopes` مع عرض ملخص نطاق عربي في خدمات المحتوى، الأكواد، الهدايا، الامتحانات العامة، والباكدجات المشتركة.
- [x] T006-T014: تمت إضافة enums/entities/DbSets/configuration/service/DI لنموذج النطاق الأكاديمي الموحد.
- [x] T010: تم إنشاء migration `20260706005123_AddStudentAcademicScope` بجدولي `academic_subject_eligibilities` و`student_facing_academic_scopes` مع فهارس وقيود، بدون تحويل السجلات غير المربوطة إلى platform-wide.
- [x] T015: اختبارات EF in-memory تستخدم `AppDbContext` الحقيقي عبر `TestAppDbContextFactory`، والـ DbSets الجديدة متاحة من خلاله بدون fake context منفصل.
- [x] T016-T017: تم إنشاء `AcademicScopeServiceTests` وتشغيل `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AcademicScopeServiceTests"` بنجاح: 12/12 passed. التحذيرات المتبقية موجودة مسبقا في `GetQuickAccessQuery.cs` و`GetLessonDetailQuery.cs`.
- [x] T018-T020: تم إنشاء `StudentAcademicScopeAccessTests` بطلاب متعددين، ثلاث مواد، mapping للمواد، وباقة scoped مع هرم Package/Term/Section/Lesson/Video/Exam. الاختبارات تغطي matching, platform-wide, stage-wide, grade-all-subjects, non-matching, unscoped hidden، وراثة نطاق الأب، ورفض النطاق الصريح غير المطابق.
- [x] T022-T023: تم ربط `AccessCheckService` و`GetPackagesQuery` بـ `IAcademicScopeService` بحيث تتم الفلترة بعد role bypass وقبل projection، وتتم إعادة تقييم grants وقت الاستخدام.
- [x] تم تشغيل `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AcademicScopeServiceTests|FullyQualifiedName~StudentAcademicScopeAccessTests"` بنجاح: 15/15 passed.
- [x] T024: تم تحديث `GetPackageByIdQuery` لرفض طلب الطالب المباشر غير المطابق بـ `ACADEMIC_SCOPE_DENIED` مع إبقاء bypass لأدوار الإدارة/المدرسين. آخر تشغيل للاختبارات المركزة: 16/16 passed.
- [x] T025: تم تحديث `GetTermsQuery`, `GetSectionsQuery`, `GetLessonsQuery`, و`GetLessonDetailQuery` لتطبيق النطاق الفعال قبل إرجاع العناصر الهرمية والفيديوهات. آخر تشغيل للاختبارات المركزة: 17/17 passed.
- [x] T026: موارد الدرس وتعليقات الدرس كانت تعتمد على `IAccessCheckService`، وبعد ربطه بالنطاق الأكاديمي أصبحت ترفض الدرس غير المطابق. تمت إضافة regression test يؤكد عدم إنشاء تعليق عند grant قديم خارج النطاق. آخر تشغيل للاختبارات المركزة: 18/18 passed.
- [x] T027: تم تحديث `GetDashboardQuery`, `GetQuickAccessQuery`, `GetProgressQuery`, و`GetMistakesQuery` لإعادة تقييم package/term/section/lesson/video/exam grants والأهداف الحالية قبل العرض أو العد، ثم أضيفت فلترة `GetStudentNotificationsQuery` للإشعارات ذات target أكاديمي. آخر تشغيل `StudentAcademicScopeAccessTests|StudentAcademicScopeAdminValidationTests`: 28/28 passed.
- [x] T028: تم تحديث community feed/comment/like/vote لتطبيق `CommunityPost` academic scope قبل العرض أو أي side effect. تمت إضافة regression test يثبت أن post غير مطابق لا يظهر ولا ينشئ comment/like/vote. آخر تشغيل للاختبارات المركزة: 19/19 passed.
- [x] T029: تم تحديث `PublicTeachersController` لتصفية المدرسين للطالب المسجل إذا كان للمدرس scope عام مطابق أو مادة واحدة على الأقل ضمن مواد صف الطالب، مع تطبيق نفس فلترة posts المدرس عند وجود طالب مسجل. آخر تشغيل للاختبارات المركزة: 19/19 passed.
- [x] T030: تم تحديث `GetPublicExamProductsQuery` ليقبل `StudentId` اختياريًا ويطبق `PublicExamProduct` academic scope قبل payment/access projection، وتم تمرير الطالب من `PublicExamsController`. تمت إضافة regression test لمنتجين منشورين أحدهما free لكنه خارج النطاق ولا يظهر. آخر تشغيل للاختبارات المركزة: 20/20 passed.
- [x] T031: تم تحديث `StudentSharedPackagesController` لتطبيق `SharedTeacherPackage` eligibility في list/detail/purchase، وتطبيق `SharedTeacherPackageItem` مع target content eligibility قبل عرض العناصر أو إنشاء grants. آخر تشغيل للاختبارات المركزة: 20/20 passed.
- [x] T032: تم تشغيل `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~StudentAcademicScopeAccessTests"` بنجاح: 8/8 passed.
- [x] T038: تم تحديث `PurchaseContentCommand` ليعيد التحقق من نطاق target الأكاديمي قبل إنشاء `purchaseOperationId` وقبل discount/promotional balance/balance deduction/grant/financial effects/outbox success. آخر تشغيل للاختبارات المركزة: 20/20 passed.
- [x] T039-T040: تم تحديث `GetPurchaseFundingPreviewQuery` و`DiscountEngine` لإرجاع denial قبل عرض التمويل أو تسجيل coupon/printable usage عندما لا يطابق target نطاق الطالب الحالي. آخر تشغيل للاختبارات المركزة: 20/20 passed.
- [x] T041-T042: أصبح `ValidateCodeQuery` student-aware عبر `CodesController`، وتمت إضافة فحص academic scope داخل `ActivateCodeCommand` قبل marking code consumed أو إنشاء grant. آخر تشغيل للاختبارات المركزة: 20/20 passed.
- [x] T043-T047: تم منع code group غير scoped عند الإنشاء، والتحقق من targets في sales rules/coupons/printable batches، والتحقق من gift targets/recipients والاستهلاك، وإضافة audit actions: `AcademicScopeDeniedPurchase`, `AcademicScopeDeniedCodeActivation`, `AcademicScopeDeniedGiftRecipient`. آخر تشغيل للاختبارات المركزة: 20/20 passed.
- [x] T037: تمت إضافة regression test يثبت أن active grant يبقى موجودًا في قاعدة البيانات لكنه يتوقف عن التفويض فور تغير صف الطالب إلى صف غير مطابق. تشغيل `StudentAcademicScopeAccessTests`: 9/9 passed.
- [x] آخر تشغيل مركز بعد T037: `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AcademicScopeServiceTests|FullyQualifiedName~StudentAcademicScopeAccessTests"` نجح: 21/21 passed.
- [x] T033-T036: تمت إضافة `StudentAcademicScopePurchaseTests`, `StudentAcademicScopeCodeTests`, و`StudentAcademicScopeGiftTests` لتغطية منع الشراء/الكوبون قبل الآثار الجانبية، رفض validate/activate للكود قبل الاستهلاك، ورفض gift recipient بدون إنشاء `StudentAccessGrant`.
- [x] T047 follow-up: تم تثبيت `AcademicScopeDeniedPurchase` و`AcademicScopeDeniedCodeActivation` داخل الـ transactions عبر commit لسجل الرفض فقط، بدون grant أو code consumption أو balance side effects.
- [x] T048: تم تشغيل `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~StudentAcademicScopePurchaseTests|FullyQualifiedName~StudentAcademicScopeCodeTests|FullyQualifiedName~StudentAcademicScopeGiftTests"` بنجاح: 5/5 passed. التحذيرات المتبقية nullable warnings موجودة في مسارات query قائمة.
- [x] T052-T053: تمت إضافة `AcademicScopeDto`, `AcademicScopeValidationResult`, و`AcademicScopeSummaryDto`، مع helpers في `AcademicScopeService` للتحقق من scope arrays ومزامنة `StudentFacingAcademicScope` لمالك محدد. القواعد الحالية ترفض القائمة الفارغة، stage/grade غير المتوافق، وExact subject غير المفعلة للصف.
- [x] T049: تمت إضافة `StudentAcademicScopeAdminValidationTests` وتشغيل `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~StudentAcademicScopeAdminValidationTests"` بنجاح: 8/8 passed.
- [x] T050/T055: تم توسيع عقود وأوامر sales/public exams لإرسال وحفظ `academicScopes` في `PublicExamProductRequest`, `CreatePublicExamRequest`, `SalesCouponRequest`, و`PrintableBatchRequest`. تمت إضافة pre-validation قبل أي حفظ لمنع السجلات الجزئية عند scope فارغ أو غير صالح. تشغيل `StudentAcademicScopeAdminValidationTests`: 12/12 passed.
- [x] T056: تم توسيع `BulkGenerateCodesCommand`, `BulkGenerateRequest`, و`frontend/src/services/code-service.ts` لدعم `academicScopes` على code groups. تمت إضافة pre-validation قبل توليد الأكواد وحفظ scopes على `StudentFacingScopeOwnerType.CodeGroup`. تشغيل `StudentAcademicScopeAdminValidationTests`: 14/14 passed.
- [x] T057: تم تحديث نماذج واستعلامات/ردود الهدايا لتضمين `AcademicScopeSummaryDto` في target lookup وissue/details responses، مع إبقاء outcomes الحالية ومنها `ACADEMIC_SCOPE_DENIED`. تشغيل `StudentAcademicScopeAdminValidationTests|StudentAcademicScopeGiftTests`: 15/15 passed.
- [x] T058: تم تحديث `AdminSharedPackagesController` و`frontend/src/services/shared-package-service.ts` لدعم `academicScopes`. عند عدم إرسالها يتم bridge من `EducationStage/GradeLevel` إلى `GradeAllSubjects`، وعند إرسال قائمة فارغة تفشل قبل الحفظ. النشر يتحقق من وجود scope صالح. تشغيل `StudentAcademicScopeAdminValidationTests|StudentAcademicScopeGiftTests`: 15/15 passed.
- [x] T059: تم تحديث `ApproveCommunityPostCommand` لرفض اعتماد منشور مجتمع بدون `CommunityPost` academic scope قبل جعله `Approved` أو إرسال outbox. تمت إضافة اختبارات رفض unscoped post وقبول scoped post. تشغيل `StudentAcademicScopeAdminValidationTests`: 16/16 passed.
- [x] آخر تشغيل مركز بعد توسيع US3: `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AcademicScopeServiceTests|FullyQualifiedName~StudentAcademicScopeAccessTests|FullyQualifiedName~StudentAcademicScopePurchaseTests|FullyQualifiedName~StudentAcademicScopeCodeTests|FullyQualifiedName~StudentAcademicScopeGiftTests|FullyQualifiedName~StudentAcademicScopeAdminValidationTests"` نجح: 42/42 passed.
- [x] T054: تم توسيع أوامر create/update للباقات والترمات والأقسام والدروس لقبول `AcademicScopes`. القائمة الفارغة أو غير الصالحة ترفض قبل الحفظ، والباقة الجديدة تعمل bridge من `TargetGrade` إلى scope legacy عند عدم إرسال scopes. تشغيل `StudentAcademicScopeAdminValidationTests`: 18/18 passed.
- [x] T060: تمت إضافة `AcademicScopeOwnerType/AcademicScopeOwnerId` الاختيارية إلى `NotificationEvent`، وتحديث قائمة الإشعارات وmark-as-read وclear لإعادة التحقق من النطاق وقت الاستخدام. السجلات تبقى موجودة، لكن غير المطابق لا يظهر ولا يسمح بتعليم القراءة. تشغيل `StudentAcademicScopeAccessTests|StudentAcademicScopeAdminValidationTests`: 28/28 passed.
- [x] T062: تمت إضافة `frontend/src/components/admin/AcademicScopeSelector.tsx` مع controls لمستويات `PlatformWide`, `StageWide`, `GradeAllSubjects`, و`Exact`، وإضافة labels/types مشتركة في `frontend/src/lib/academic-labels.ts`. تشغيل `cd frontend && npm run lint` نجح بدون errors؛ التحذيرات الستة في ملفات غير مرتبطة بالتعديل.
- [x] T063: تم تشغيل `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~AcademicScopeServiceTests|FullyQualifiedName~StudentAcademicScopeAccessTests|FullyQualifiedName~StudentAcademicScopePurchaseTests|FullyQualifiedName~StudentAcademicScopeCodeTests|FullyQualifiedName~StudentAcademicScopeGiftTests|FullyQualifiedName~StudentAcademicScopeAdminValidationTests"` بنجاح: 46/46 passed. وتم تشغيل `cd frontend && npm run lint` بنجاح بدون errors، مع 6 warnings موجودة في ملفات غير مرتبطة.
- [x] T051: تمت تغطية code group validation/persistence، community post approval scoped/unscoped، notification list/read filtering، gift issuance denial، وإضافة اختبار `CreateSharedPackage_RejectsExplicitEmptyAcademicScopesBeforeSaving` لمسار shared package. تشغيل `StudentAcademicScopeAdminValidationTests`: 19/19 passed.
- [x] T061: تم ربط `AcademicScopeSelector` وإظهار ملخصات النطاق في واجهات إنشاء دفعات الأكواد، إنشاء الامتحانات العامة، تعديل كوبون المبيعات، وإنشاء الباكدجات المشتركة، مع عرض نطاق target المختار داخل نموذج الهدايا. DTOs الأمامية تحمل `academicScopes` في خدمات الأكواد والمبيعات والهدايا والباقات المشتركة والـ labels المشتركة. تشغيل `cd frontend && npm run lint` نجح بدون errors؛ التحذيرات الستة في ملفات غير مرتبطة.
- [x] T066-T070: تم تحديث empty states العربية للباقات، المدرسين، المجتمع، الامتحانات العامة، الباكدجات المشتركة، والإشعارات لتوضح أن النتائج مفلترة حسب بيانات الطالب الدراسية الحالية. تمت إضافة ترجمة `ACADEMIC_SCOPE_DENIED`/`ACADEMIC_SCOPE_TARGET_UNSCOPED` في `api-client.ts` إلى رسالة عربية عامة لا تكشف تفاصيل target محجوب. تشغيل `cd frontend && npm run lint` نجح بدون errors؛ التحذيرات الستة في ملفات غير مرتبطة.
- [x] T071: تم تشغيل `cd frontend && npm run build` بنجاح؛ Next.js production build وTypeScript مرّا بدون أخطاء.
- [x] T072: تمت إضافة backfill SQL داخل migration `20260706005123_AddStudentAcademicScope` لاستنتاج `AcademicSubjectEligibility` و`StudentFacingAcademicScope` من `Package.TargetGrade`, public exam legacy fields, shared package stage/grade, وteacher subject mappings. السجلات ذات الصف/المرحلة غير القابلة للاستنتاج لا تتحول إلى platform-wide. تحقق compile عبر `dotnet test ... --filter "FullyQualifiedName~AcademicScopeServiceTests|FullyQualifiedName~StudentAcademicScopeAdminValidationTests"` نجح: 31/31 passed.
- [x] T073: تمت إضافة `AcademicScopeMigrationBackfillTests` لفحص SQL operations داخل migration والتأكد من استخدام aliases المعروفة وعدم وجود fallback يحول `TargetGrade` المجهول إلى platform-wide، مع تغطية public exams/shared packages/teacher subject mappings. تشغيل `dotnet test ... --filter "FullyQualifiedName~AcademicScopeMigrationBackfillTests"` نجح: 2/2 passed.
- [x] T074: تمت مراجعة student-facing controllers/queries المباشرة: content packages/terms/sections/lessons/resources/comments، dashboard/quick access/progress/mistakes/notifications، community feed/actions، public exams، shared packages، purchase preview/purchase/code/gift paths. الاستثناءات المقصودة خارج فلترة النطاق الأكاديمي: profile/theme/shell bootstrap غير المحتوي، balance/recharge، gamification، parent historical reports، admin/teacher/CRM/live-support surfaces، وhomework/exam attempt endpoints التي تعتمد على access/attempt validation لا على catalog listing.
- [x] T075: تمت إضافة فهارس مركبة لـ `student_profiles(EducationStage, GradeLevel, UserId)`, و`academic_subject_eligibilities` مع `IsActive`, و`student_facing_academic_scopes(OwnerType, OwnerId, ScopeLevel, EducationStage, GradeLevel, SubjectId)` في `AppDbContext`, migration, designer, وsnapshot. تشغيل `dotnet test ... --filter "FullyQualifiedName~AcademicScopeServiceTests|FullyQualifiedName~AcademicScopeMigrationBackfillTests"` نجح: 14/14 passed.
- [x] T076: أول تشغيل كشف regression في `GiftsAndPromotionalBalanceTests.VideoGift_ExposesOnlySelectedVideo_AndConsumesSuccessfulSession` حيث كان مسار video-only access يعرض الفيديوهات غير المسموحة كمقفولة. تم تعديل `GetLessonDetailQuery` ليعيد فقط الفيديوهات المسموحة في هذا المسار. إعادة تشغيل `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AcademicScope|FullyQualifiedName~AccessCheck|FullyQualifiedName~Purchase|FullyQualifiedName~Gift|FullyQualifiedName~Code|FullyQualifiedName~Sales"` نجحت: 78/78 passed. التحذير المتبقي nullable في `GetQuickAccessQuery.cs` موجود خارج هذا التعديل.
- [x] T077: تمت مراجعة diff مقابل spec/plan/tasks. أهم finding كان تسريب فيديو غير مسموح في مسار `IsVideoOnlyAccess` وقد تم إصلاحه قبل إغلاق T076. تم تأكيد أن الملفات unrelated الظاهرة في worktree (`.agents/skills/ssh-server/docs/database_schema.md` وملفات mobile parent) ليست جزءاً من هذا التعديل ولم يتم عكسها.
- [x] T078: تم تشغيل clean-code-guard كـ guard pass على ملفات الإنتاج المعدلة. لا توجد findings جديدة بعد إصلاح `IsVideoOnlyAccess`. تم تسجيل تكرار `grade_alias` في SQL migration كاستثناء مقصود لأن كل statement يحتاج CTE مستقل، وتجنب بناء SQL ديناميكي يحافظ على وضوح migration.
- [x] T079: تم تشغيل test-guard على الاختبارات الجديدة/المعدلة. لا توجد findings تمنع المتابعة. تم تسجيل أن `AcademicScopeMigrationBackfillTests` يفحص SQL operations كـ migration-adjacent static guard، بينما تطبيق migration الفعلي مؤجل إلى تحقق Docker/T082.
- [x] T080: تم تشغيل quickstart backend feature tests: `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AcademicScope"` نجح: 48/48 passed، وفلتر `AccessCheck|Purchase|Gift|Code|Sales` نجح سابقاً في T076: 78/78 passed. تحقق الواجهة الأساسي المنفذ هو `npm run lint` و`npm run build` من T071، وتمت إضافة Playwright smoke لاحقاً في T064.
- [x] T081: أعيد تشغيل `dotnet test backend/NaderGorge.sln` مع قاعدة Docker عبر `ConnectionStrings__DefaultConnection='Host=localhost;Port=5435;Database=massar_platform;Username=postgres;Password=postgres'` ونجح بالكامل: `NaderGorge.Application.Tests` 356 passed و1 skipped، و`NaderGorge.Integration.Tests` 15/15 passed. كما نجح `cd frontend && npm run lint && npm run build` مع نفس 6 warnings غير المرتبطة، و`make verify` كان قد نجح سابقاً.
- [x] T021: تمت تغطية community feed/actions، public exams، shared packages، notifications، وgrant re-evaluation داخل `StudentAcademicScopeAccessTests`. تشغيلات T018-T031/T060/T076 أثبتت أن القوائم والتفاصيل لا تعيد إلا العناصر المطابقة.
- [x] T064: تمت إضافة `frontend/tests/e2e/student-academic-scope.spec.ts` كـ Playwright smoke spec يغطي empty states لصفحات الباقات، المدرسين، المجتمع، الامتحانات العامة، الباكدجات المشتركة، والإشعارات باستخدام API mocks وجلسة طالب وهمية. تم تشغيله بنجاح عبر `CI=1 PLAYWRIGHT_WEB_PORT=3100 PLAYWRIGHT_BASE_URL=http://app.lvh.me:3100 npx playwright test tests/e2e/student-academic-scope.spec.ts --project=chromium`: 1/1 passed.
- [x] T065: لا يوجد component/unit runner عام لصفحات الطالب في المشروع؛ التغطية المتاحة عملياً هي Playwright e2e. تم تشغيل `cd frontend && npm run lint` بعد إضافة الـ spec ونجح بدون errors، مع نفس 6 warnings غير المرتبطة.
- [x] T082: `docker compose config -q` نجح. `make up` نجح وبنى `massar_backend`, `massar_worker`, `massar_frontend`, و`massar_nginx` ثم شغّل كل الخدمات. `make migrate` نجح وطبق migrations حتى `20260706005123_AddStudentAcademicScope`. `curl -f http://localhost:5245/api/health` نجح، و`curl -f http://localhost:8738` نجح. `make ps` أظهر كل الحاويات healthy. تم إصلاح `docker-compose.yml` لنشر منفذ worker على `127.0.0.1:${MASSAR_WORKER_PORT:-3001}:3001`; بعدها `curl -f http://localhost:3001/ready` نجح. فحص `curl -f http://localhost:3001/ui` بقي غير صالح كأمر غير مصادق لأن Bull Board محمي عمداً: `WORKER_ADMIN_ENABLED=false` يرجع 404، وعند تفعيله يتطلب Bearer token.
- [x] T083: سجل الجاهزية النهائي: اختبارات backend المركزة نجحت، `dotnet test backend/NaderGorge.sln` الكامل نجح عند استخدام PostgreSQL Docker، `make verify` نجح، `frontend lint/build` نجح، Docker stack وmigrations نجحوا، وguard passes مسجلة في T078/T079. Playwright smoke الجديد نجح على منفذ بديل `3100` بعد جعل `frontend/playwright.config.ts` يدعم `PLAYWRIGHT_WEB_PORT` و`PLAYWRIGHT_BASE_URL` لتجنب تعارض container خارجي على `3000`.

### Phase 2 Clarification Evidence / إثبات التوضيح

- [x] تم تشغيل prerequisite الخاص بـ `speckit-clarify` على `160-employee-realtime-refresh`.
- [x] تم فحص نطاق الوظائف، الأدوار، الحالات الحدية، الأمان، الأداء، التكاملات، ومعايير الإنهاء.
- [x] لا توجد غموضات مواصفات عالية التأثير؛ القرارات المتبقية تقنية/تشغيلية ومؤجلة إلى Phase 3.

### Phase 3 Speckit-Plan Evidence / إثبات التخطيط

- [x] تم تشغيل `SPECIFY_FEATURE=160-employee-realtime-refresh .specify/scripts/bash/setup-plan.sh --json`.
- [x] تم تنفيذ handoff إلى `speckit-plan` بعد قراءة `speckit-plan/SKILL.md` والدستور.
- [x] تم إنشاء `specs/160-employee-realtime-refresh/plan.md` و`research.md` و`data-model.md`.
- [x] تم إنشاء العقود: `contracts/current-session.md`, `contracts/staff-data-changed.md`, `contracts/query-invalidation.md`.
- [x] تم إنشاء `quickstart.md` وتحديث `AGENTS.md` تحت علامات Spec Kit، مع تشغيل `update-agent-context.sh` بنجاح.
- [x] البحث أثبت أن `SecurityStampVersion` هو مصدر الإصدار الحالي، وأن `StaffRealtimeChangeDetector` و`OutboxProcessor` و`usePlatformEvents` و`StaffRealtimeBoundary` هي نقاط التكامل الأساسية.
- [x] قرار TanStack Query موثق مع fallback typed registry، ولم تُترك clarifications غير محسومة.

### Phase 4 Speckit-Tasks Evidence / إثبات تفصيل المهام

- [x] تم إنشاء `specs/160-employee-realtime-refresh/tasks.md` بمهام T001-T084 ذرية ومحددة المسارات، منظمة حسب US1/US2/US3.
- [x] المهام تغطي baseline inventory، current-session، authorization version، query contracts، SignalR/reconnect، كل domain migrations، conflicts، reload allowlist، observability، وDocker/E2E.
- [x] تم تثبيت ترتيب الإغلاق الإلزامي: deep critique ثم `clean-code-guard` ثم `test-guard` ثم feature tests ثم build/Docker/validation.
- [x] تم تشغيل `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/160-employee-realtime-refresh/tasks.md` بنجاح.

### Phase 5 Implementation Evidence / إثبات التنفيذ

- [x] Baseline inventory and employee bug matrix created; initial scan recorded 32 service files, 55 service mutation signatures, 62 all-frontend mutation signatures, 31 cache registrations, 8 force calls, and 4 reload call sites.
- [x] Added authenticated `GET /api/auth/session`, `authorizationVersion` in `UserDto`, current-session auth store refresh, and bootstrap reconciliation.
- [x] Updated role/status/employee HR mutations to increment `SecurityStampVersion`, revoke stale refresh tokens where required, emit user-targeted `StaffDataChanged`, and reject stale employee profile updates.
- [x] Extended staff realtime payload with schema version, event ID, timestamp, operation, entity type, and entity IDs; frontend now deduplicates and reconciles on reconnect.
- [x] Added typed query keys, scope-to-invalidation mapping, legacy event parser, all-prefix cache invalidation, and reload allowlist script.
- [x] Migrated/updated content, codes, sales, finance, exams, homework, community, and notifications mutation invalidation surfaces through parallel workers.
- [x] Removed module-level content cache and replaced non-security reloads in student context and lesson carousel with targeted/route refresh behavior.
- [x] Focused backend tests passed: 54 tests for Auth/HR/StaffRealtime filters; worker-reported Auth/HR focused suite passed 51 tests and realtime contract suite passed 23 tests.
- [x] Frontend `npm run typecheck`, `npm run lint`, `git diff --check`, and `node scripts/check-no-unallowlisted-reloads.mjs` passed after resolving two stale `force` call-site type errors.
- [x] Remaining implementation scope from the previous checkpoint was completed by parallel workers: operations/CRM/support, forms/media/reports, public/shared-package flows, 217-mutation contract coverage, and observability metrics.

### Dynamic Issues / المشاكل الديناميكية

- [x] TypeScript errors at `TeacherProfilePageClient.tsx` and `TeacherCodesPageClient.tsx` caused by obsolete `force` arguments were fixed by using the cache-free `listCodeGroups()` contract.
- [x] `check-platform-event-contracts` false-positive for internal worker event `LiveSupportAITurnQueued` and the separate `LiveSupportEvent` hub was corrected by recognizing internal-only events and scanning `useLiveSupportHub.ts`; contract check now passes.
- [x] Playwright runtime checks were attempted; external blockers are the unavailable E2E backend/seed and missing Chromium executable.

### Phase 6 Deep Critique Evidence / إثبات المراجعة العميقة

- [x] Reviewed backend authorization/versioning, transaction/outbox behavior, event targeting, payload privacy, and employee conflict handling; fixed session bootstrap timing and guarded 403 session refresh recursion.
- [x] Reviewed frontend state ownership, realtime dedupe, active-query invalidation, draft preservation boundaries, cache removal, reload exceptions, and domain mutation invalidation.
- [x] `git diff --check`, backend build, frontend typecheck, frontend lint, query-contract check, platform-event contract check, and reload guard all pass.

### Phase 7 Clean Code Guard Evidence / إثبات حراسة الكود

- [x] Guard-pass review applied to changed production files using `clean-code-guard` imperatives: no new broad swallowed errors, verified imports/API calls, bounded helpers, no hardcoded production fixtures, and no unallowlisted reloads.
- [x] Mechanical verification: `dotnet build backend/NaderGorge.sln --no-restore`, `cd frontend && npm run typecheck && npm run lint`, and `git diff --check` passed.

### Phase 8 Test Guard Evidence / إثبات حراسة الاختبارات

- [x] Reviewed changed backend tests and Playwright/contract tests with `test-guard`: tests assert event/session/cache behavior at observable boundaries; no internal mock-heavy tests or duplicate scenario-only variants were introduced.
- [x] Frontend has no Jest/Vitest runner; executable contract assertions document that limitation, while backend xUnit and Playwright remain the runtime test boundaries.

### Feature Test Evidence / إثبات اختبارات الفيتشر

- [x] `dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~StaffRealtimeOutbox|FullyQualifiedName~Auth|FullyQualifiedName~HR" --no-restore` → 54 passed.
- [x] `dotnet test backend/NaderGorge.sln --no-build` → Application.Tests 368 passed, 1 pre-existing Redis test skipped; Integration.Tests 15 blocked because `ConnectionStrings__DefaultConnection` is unset.
- [x] `make verify` → restore/build/application tests/frontend lint+build/worker build/docker compose config passed; PostgreSQL integration tests skipped by Makefile because connection string is unset.
- [x] `cd frontend && npm run check:platform-events && node scripts/check-query-contracts.mjs` → both passed.
- [x] Realtime observability counters were added for accepted/duplicate events, invalidations, and reconnects; frontend typecheck/lint and contract checks still pass.
- [x] `cd frontend && node scripts/check-no-unallowlisted-reloads.mjs` → passed; only SecureVideoPlayer remains allowlisted.
- [x] `make verify-e2e` → attempted; blocked by API `127.0.0.1:5245` unavailable and missing Playwright Chromium executable.
- [x] `make up` → attempted; blocked because Docker daemon is unavailable at `unix:///Users/mazenelsbagh/.docker/run/docker.sock`.

### Final Completion Evidence / إثبات الإغلاق النهائي

- [x] Worker 1 completed employee create/version/rowVersion/hooks/permission evaluator work; backend focused tests and frontend checks passed.
- [x] Worker 2 completed `DataChangedEvent`, outbox retry identity, safe structured telemetry, and dispatch metrics; backend build and focused tests passed.
- [x] Worker 3 completed 217-mutation inventory/checker, canonical event mappings, failure/reconnect contract assertions, metrics, rollout docs, and final inventory updates.
- [x] `dotnet build backend/NaderGorge.sln --no-restore` passed with 0 warnings and 0 errors.
- [x] `dotnet test backend/NaderGorge.sln --no-build` passed application tests: 370 passed, 1 skipped; PostgreSQL integration suite is external-environment blocked when no connection string is supplied.
- [x] `make verify` passed restore/build/application tests/frontend lint+build/worker build/docker compose config; PostgreSQL integration is intentionally skipped by the Makefile without a connection string.
- [x] `python3 .agents/skills/speckit-all/scripts/validate_tasks_quality.py --tasks specs/160-employee-realtime-refresh/tasks.md` passed.
- [x] `python3 .agents/skills/speckit-all/scripts/validate_spec_plan_quality.py --spec-dir specs/160-employee-realtime-refresh` passed.

### Parallel Agent Evidence / إثبات التنفيذ المتوازي

- [x] Backend worker completed role/status/employee concurrency changes and 51 focused Auth/HR tests passed.
- [x] Frontend workers completed reload cleanup, content cache removal, codes/sales/finance, assessments/community/notifications, operations/CRM/support, forms/media/reports, and public/shared-package invalidation slices.
- [x] Main agent reviewed all worker diffs, fixed stale `force` call sites, reconciled event-contract checker behavior, and reran build/lint/typecheck.

### Worker 2 Realtime/Backend Observability Evidence / إثبات العامل 2

- [x] T020: Added typed `Domain.Events.DataChangedEvent` with schema, event identity, timestamp, scopes, operation, entity metadata, and allowlisted scope/operation validation.
- [x] T022/T066: `StaffRealtimeChangeDetector` now serializes the typed envelope once per outbox row; `OutboxProcessor` backfills missing IDs from the durable outbox ID before dispatch, preserving the same ID through retry/dead-letter processing.
- [x] T073 backend realtime slice: added `NaderGorge.Realtime` counters/histogram and structured dispatch/failure/dead-letter logs containing event metadata only; payloads and employee fields are never logged.
- [x] T063 remains frontend-owned; no frontend files were changed by Worker 2. Existing frontend reconnect handling was left for its owner.
- [x] Verification: `dotnet build backend/NaderGorge.sln --no-restore` passed with 0 warnings; focused Application tests passed 18/18. Full solution filter also attempted one PostgreSQL integration test, blocked only because `ConnectionStrings__DefaultConnection` was unset.

### Employee Slice Worker 1 Evidence / إثبات شريحة الموظفين

- [x] T029: `AdminCreateUserCommand` now returns the created user identity, role, authorization version, and employee profile version; the existing `AppDbContext` change detector emits the users/HR `StaffDataChanged` contract without a duplicate outbox row.
- [x] T034: HR employee read and update contracts now expose `rowVersion` backed by `EmployeeProfile.UpdatedAt`; no migration was required.
- [x] T036: Added `frontend/src/features/employee/` fallback-registry hooks for employee lists/details and create/update/disable mutations with explicit employees/detail/HR/session invalidation.
- [x] T037: Migrated employee profile, user creation, assistant status, and admin status screens to the hooks while preserving existing loading/error/empty behavior.
- [x] T039: Centralized permission evaluation in `useHasPermission.ts`; `StaffGuard` now consumes it and reacts to authorization-version snapshot changes. Admin layout already consumed the hook, and teacher access remains delegated to the existing reactive `TeacherGuard`.
- [x] Verification: `dotnet build backend/NaderGorge.sln --no-restore` (0 warnings, 0 errors); focused backend filter (44 passed); `npm run typecheck`; `npm run lint`; `npm run check:platform-events`; reload guard; and `git diff --check` all passed.

### Worker 3 Contracts/Tests Evidence / إثبات العامل 3

- [x] T045/T046: Added typed mutation source records for 27 service files and exact coverage checking for all 217 `apiClient` mutations. `node frontend/scripts/check-query-contracts.mjs` passes.
- [x] T058: Routed platform event invalidations through `realtime-invalidation-map.ts`, preserving unknown entity-detail keys while using the canonical registry for known keys.
- [x] T059/T071: Added non-mock-heavy executable contract assertions for dedupe, active invalidation, failure classification, rollback policy, and metrics; added Playwright validation/permission-denial coverage.
- [x] T074: Added frontend counters for invalidation/refetch, reconnect duration, snapshot reconciliation, duplicate events, and mutation-visible refresh; contract assertions verify the new counters.
- [x] T075/T076: Updated rollout/canary/rollback guidance and final inventory/bug-matrix evidence, including the SecureVideoPlayer-only reload allowlist and runtime blockers.
- [x] Verification: `node frontend/scripts/check-query-contracts.mjs`, `node frontend/scripts/check-platform-event-contracts.mjs`, `node frontend/scripts/check-no-unallowlisted-reloads.mjs`, `npm run lint`, `npm run typecheck`, and `git diff --check` pass. E2E execution remains blocked only when the E2E backend/Chromium is unavailable.

### Remediation Plan Completion Addendum / ملحق إغلاق خطة remediation

- [x] RF-R01: `cache-invalidation.ts` now supports multiple registrations per key with independent idempotent cleanup; all production call sites now return their own cleanup instead of unregistering by shared name.
- [x] RF-R04: executable contract assertions cover multi-consumer invalidation, cleanup isolation, batching dedupe, failure classification, active registration behavior, and realtime metrics.
- [x] LS-R01/LS-R02: staff and participant drafts are keyed by conversation; stale message responses are ignored by selection generation; transcript loading/error/retry states are explicit.
- [x] LS-R03: participant history remains readable when availability is closed; new conversation creation remains separately gated.
- [x] LS-R04/LS-R05: malformed SignalR envelopes are rejected safely, counted, and reconciled; reconnect/sequence-gap metrics are recorded; close/transfer/send actions have pending locks and distinct 409/403 handling.
- [x] LS-R09: AI admin backend verification covers preview zero-write behavior, authorization, versioned disable/202 response, and policy/evidence states. Backend AI-focused run: 71 passed.
- [x] Clean-code guard pass after the parallel changes: production imports/API calls, cleanup ownership, error handling, complexity, and reload allowlist reviewed; no unresolved findings.
- [x] Test-guard pass: changed xUnit/Playwright/contract tests reviewed for boundary mocking, scenario focus, observable assertions, and explicit external skips; no unresolved blocking findings.
- [x] Verification passed: `make verify` (backend build 0 warnings/0 errors, 372 application tests passed/1 pre-existing Redis skip, frontend lint/build, worker TypeScript build, Docker Compose config), frontend typecheck/lint, platform-event checker, query-contract checker, reload guard, and focused backend tests (124 passed).

### External Blockers / الحواجز الخارجية

- [x] T119 blocker recorded: real Vertex/AI provider callback and reconnect acceptance requires running worker/backend, valid provider credentials/quota/network, and a safe screenshot environment; it remains open and is not claimed as passed.
- [x] T122 blocker recorded: real PostgreSQL/Redis Docker health/restart checks and Chromium/WebKit multi-session E2E require Docker daemon, E2E backend seed, and installed Playwright browsers. The attempted Playwright run was not counted as success: 25 tests failed/blocked due to missing Chromium and unavailable API/app endpoints.
- [x] Worker full `npm test -- --runInBand` blocker recorded: one pre-existing unrelated failure in `generateChapterMindmaps.test.js` caused by the local ffmpeg/image fixture path; worker TypeScript build passes.
## 167 — Platform speed completion and three-node rollout (2026-07-29)

- [x] Reviewed the complete dirty workspace, including tracked, untracked, and
  user-authored changes, with path classification, hashes, Gitlink handling,
  and fail-closed secret detection.
- [x] Implemented persistent shells, safe navigation return/focus/scroll,
  constrained registration motion, canonical query caching/cancellation,
  bounded admin/live-support reads, security-state caching, durable outbox
  leases, privacy-safe RUM, cache headers, accessible overlays/carousels, and
  three-node release/rollback hardening.
- [x] Completed independent architecture, UI/UX, clean-code, test, and
  documentation reviews and fixed every actionable P0/P1/P2 finding.
- [x] Built all images on the reviewed remote builder; no dependency, browser,
  SDK, or image was downloaded to the workstation.
- [x] Proved encrypted backup, isolated restore, migration compatibility, N-1
  readiness, and one serialized forward production migration.
- [x] Exercised automatic app-only rollback while retaining the new database
  schema, then completed zero-downtime rolling deployment in the order
  node-3, node-2, node-1.
- [x] Final cluster status, audit, Cloudflare Tunnel, backup schedules,
  current-release parity, and public/auth browser smoke passed.
- [ ] Authenticated k6 workflows and the complete Chromium/WebKit/four-role
  accessibility matrix remain evidence gaps; no success is claimed without a
  disposable account and protected token files.

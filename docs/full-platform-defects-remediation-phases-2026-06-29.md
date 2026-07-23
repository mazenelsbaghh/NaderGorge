# Full Platform Defects Remediation Phases - 2026-06-29

المصدر: `docs/full-platform-defects-audit-2026-06-29.md`

الهدف من الملف: تحويل نتائج التدقيق إلى مراحل تنفيذ واضحة. كل مرحلة لها نطاق، ترتيب عمل، اختبارات مطلوبة، Manual QA، وبوابة خروج. لا تعتبر أي مرحلة منتهية لمجرد تعديل الكود؛ لازم تكون قابلة للتحقق بتستات وأوامر تشغيل واضحة.

## قواعد عامة قبل التنفيذ

- ممنوع تنفيذ P2/P3 قبل إغلاق P0/P1 المرتبطة بالدخول، الصلاحيات، الأسرار، الـ build، والفلوس.
- أي تعديل أمني أو مالي أو صلاحيات لازم يكون له Backend validation، Tests، وAudit/Log عند الحاجة.
- أي Migration لازم معها اختبار أو فحص يثبت الـ constraint المطلوب، خصوصا ledgers, balances, payouts, SMS matching, grants.
- أي تعديل UI لازم يتراجع على mobile وdesktop، وخصوصا student mobile لأنها surface أساسية.
- أي secret ظهر في repo أو Makefile يتم التعامل معه كأنه compromised إلى أن يثبت العكس.
- كل Phase تبدأ بـ `git status --short` وتوثيق الملفات المتأثرة حتى لا يتم خلط generated artifacts بتعديلات حقيقية.
- بوابة التحقق العامة بعد كل Phase:
  - `docker compose config -q`
  - Backend restore/build/test حسب المرحلة
  - Frontend lint/typecheck/build حسب الملفات المتأثرة
  - Worker build/test حسب الملفات المتأثرة
  - Manual QA checklist للـ flows الحرجة

## Phase 0: Baseline Hygiene and Verification Contract

### الهدف

إصلاح عقد التحقق الأساسي للمشروع قبل الدخول في إصلاحات منطقية كبيرة: الـ build، أوامر الاختبار، artifacts، secrets، وCI gates.

### يغطي من التدقيق

- P0-1 Backend solution build fails
- P0-2 Repository contains environment and generated build artifacts
- P0-3 Required project command is invalid
- P0-4 Production SSH password is hardcoded in Makefile
- P2-10 Docker defaults are unsafe for production if env is incomplete
- P2-12 Playwright reports are tracked/dirty
- P2-13 Mobile apps have build/cache artifacts and dependency zips in repo
- P2-18 Frontend lint warnings remain
- P2-22 Deploy workflow lacks full gates and rollback
- P2-23 Makefile deploy mutates git state dangerously
- P2-26 CI misses worker integration and security gate is failing
- P3-7 Documentation reports are numerous and may conflict
- P3-8 Root build/test commands are fragmented

### التاسكات

- [x] تشغيل restore/build للـ backend وتحديث `EventContractTests` مع constructor الحالي.
- [x] إصلاح warning الخاص بـ homework answer persistence أو فتح TODO موثق لو يحتاج مرحلة لاحقة.
- [x] تعريف أمر root verification حقيقي، مثل `make verify`، يشغل backend/frontend/worker/compose checks.
- [x] تحديث `AGENTS.md` أو docs command contract ليتطابق مع الأوامر الموجودة فعليا.
- [x] إضافة أو إصلاح `npm test` في `frontend/package.json` أو توثيق البديل الصحيح.
- [x] توحيد Playwright port مع Next scripts أو إضافة `webServer` في config.
- [x] مراجعة `.gitignore` وإضافة env files، build folders، Playwright reports، `__pycache__`، `.pyc`، `.next`، mobile build/cache، Gradle distributions غير المقصودة.
- [x] إزالة generated artifacts من Git tracking بـ `git rm --cached` بعد تأكيد أنها ليست مطلوبة كمصدر.
- [x] إزالة hardcoded SSH password من `Makefile` واستبداله بتدفق key-based أو CI secret managed.
- [x] توثيق تدوير أي أسرار حقيقية ظهرت في `.env` أو Makefile.
- [x] جعل Docker production-sensitive secrets تستخدم `${VAR:?message}` بدل defaults ضعيفة.
- [x] منع deploy target من staging/committing كل الملفات تلقائيا.
- [x] إضافة docs index يحدد أن audit بتاريخ 2026-06-29 هو baseline الحالي.

### الاختبارات الآلية المطلوبة

- [x] `dotnet restore backend/NaderGorge.sln`
- [x] `dotnet build backend/NaderGorge.sln --no-restore`
- [x] `dotnet test backend/NaderGorge.sln --no-build` أو subset موثق لو الاختبارات الثقيلة تحتاج services.
- [x] `npm run lint` داخل `frontend` بدون warnings غير مبررة.
- [x] `npm run build` داخل `worker`.
- [x] `docker compose config -q`.
- [x] security scan يمنع tracked secrets/generated artifacts، مع allowlist صريح فقط للملفات المقصودة.
- [x] CI workflow يفشل لو أي خطوة من الخطوات السابقة فشلت.

### Manual QA

- [x] تشغيل dev stack محليا والتأكد أن backend/frontend/worker يبدأوا بدون secrets حقيقية.
- [x] تشغيل Playwright أو smoke E2E واحد على port الصحيح.
- [x] مراجعة `git status --short` بعد التحقق والتأكد أن الاختبارات لا تولد ملفات tracked.

### بوابة الخروج

- backend build أخضر.
- command contract واضح وموجود في docs/Makefile/package scripts.
- لا توجد أسرار أو artifacts generated متتبعة بدون سبب موثق.
- deploy المحلي لا يستطيع نشر تغييرات عشوائية أو password hardcoded.

## Phase 1: Authentication, Sessions, and Permission Safety

### الهدف

إغلاق مخاطر الدخول والصلاحيات قبل أي إصلاحات مالية أو UI: منع session renewal للمستخدمين المعطلين، validation للـ long-lived tokens، وdeny-by-default في admin routes.

### يغطي من التدقيق

- P1-1 Long-lived student tokens need account-state validation
- P1-2 Disabled users can refresh tokens
- P1-3 Cross-surface login can lose the authenticated session
- P1-4 Admin route permission map is incomplete and not deny-by-default
- P2-1 Frontend access tokens are persisted in browser storage
- P2-5 Authz failures are mapped as 401 instead of 403
- P2-17 Public parent report token is passed in URL

### التاسكات

- [x] إضافة `OnTokenValidated` في backend للتحقق من `IsActive`, `PasswordResetVersion`, وtoken/user/role version.
- [x] رفض refresh token لو `storedToken.User.IsActive == false`.
- [x] إبطال refresh tokens عند تعطيل الحساب، reset password، role change، وdevice revocation.
- [x] Hydrate auth عند bootstrap من `/auth/refresh` باستخدام HttpOnly cookie عندما تكون storage فارغة.
- [x] نقل access token تدريجيا إلى in-memory state بدل local/session storage، مع fallback مؤقت موثق إذا لزم.
- [x] جعل `/admin/*` deny-by-default لو لا يوجد permission rule مطابق.
- [x] توليد route permissions من نفس مصدر navigation أو إنشاء route inventory موحد.
- [x] التفريق بين 401 و403 عبر `ForbiddenException` أو result type واضح.
- [x] تقليل تسريب parent-report token في URL: short-lived token، `Referrer-Policy`، أو exchange إلى HttpOnly cookie.

ملاحظة تنفيذ 2026-06-30: تم تنفيذ Phase 1 في spec `154-auth-session-permission-safety`، ثم أغلقت بنود E2E المتبقية في spec `155-platform-verification-hygiene` باستخدام `*.lvh.me` كـ same-site local domain. التفاصيل موثقة في `achievements.md`.

### الاختبارات الآلية المطلوبة

- [x] Unit/integration test: disabled user لا يستطيع refresh.
- [x] Unit/integration test: token قديم يفشل بعد password reset version change.
- [x] Unit/integration test: role/token version القديم يفشل بعد role change.
- [x] API test: forbidden action يرجع 403 وليس 401.
- [x] Frontend test: bootstrap يستدعي refresh cookie ويملأ auth state عند غياب storage.
- [x] E2E negative tests: assistant/staff لا يفتحوا direct admin URLs غير المصرح بها.
- [x] E2E: staff/admin login من surface ثم redirect إلى surface آخر لا يفقد session.

### Manual QA

- [ ] تعطيل طالب ثم محاولة refresh وفتح صفحة طالب.
- [x] تغيير role لمستخدم staff ثم تجربة admin direct URLs.
- [x] تسجيل دخول admin/teacher/student من surfaces مختلفة والتأكد من عدم وجود loops.
- [x] فتح parent report والتأكد أن token لا يظهر أو يكون قصير العمر ومحدود التسريب.

### بوابة الخروج

- لا يمكن لمستخدم inactive أو role قديم الاستمرار بتوكن قديم.
- admin route غير المعروفة ممنوعة افتراضيا.
- auth failures لا تسبب logout خاطئ عند 403.

## Phase 2: Financial and Data Integrity Hardening

### الهدف

تثبيت invariants الخاصة بالفلوس، الرصيد، الأكواد، المنح، التحويلات، والـ ledgers على مستوى database والمعاملات، وليس فقط في service code.

### يغطي من التدقيق

- P1-5 Serializable transaction conflicts can become 500s
- P1-6 Money/account balances lack enough database-level protection
- P1-14 Financial/audit history can be erased by cascade deletes
- P1-15 SMS recharge matching can double-match one recharge request
- P1-16 Teacher payout requests can overcommit available balance
- P2-20 Student access grants lack DB target-shape and duplicate-active safeguards
- P2-21 Recharge matching needs a pending-match composite index

### التاسكات

- [x] إضافة transaction retry helper لـ PostgreSQL `40001` مع max attempts وحدود زمنية.
- [x] ترجمة conflicts غير القابلة للإعادة إلى `409 Conflict` بكود خطأ ثابت.
- [x] إضافة atomic debit/update patterns مثل `UPDATE ... WHERE CurrentBalance >= amount`.
- [x] إضافة concurrency token أو row version للـ balances/accounts التي تتغير كثيرا.
- [x] إضافة DB check constraints للأرصدة غير السالبة حيث business rules تتطلب ذلك.
- [x] تحويل finance/audit cascade deletes إلى `Restrict` أو `NoAction`.
- [x] اعتماد soft-delete للـ principals التي لها financial history.
- [x] إضافة filtered unique index على `incoming_sms_logs("MatchedRechargeRequestId") WHERE "MatchedRechargeRequestId" IS NOT NULL`.
- [x] إضافة check يربط `IsMatched` مع `MatchedRechargeRequestId`.
- [x] جعل recharge status transition atomic بـ `UPDATE ... WHERE Status = Pending`.
- [x] إضافة idempotency unique constraint للـ recharge credits مثل `(TransactionType, ReferenceId)` عندما `ReferenceId IS NOT NULL`.
- [x] إدخال `ReservedBalance` أو payout ledger للمدرسين عند طلب الصرف.
- [x] تحويل payout request إلى reservation فوري، release عند الرفض، settle عند الدفع.
- [x] إضافة target-shape constraints لـ `StudentAccessGrant`: exactly one target matching `GrantType`.
- [x] إضافة uniqueness/idempotency للمنح حسب source والطالب والمحتوى، أو merge/extend بدل duplicate active grants.
- [x] إضافة composite index لمطابقة recharge pending: `(WalletId, Status, Amount, SenderPhoneNumber, CreatedAt)` مع partial filter مناسب.

### الاختبارات الآلية المطلوبة

- [x] Concurrency test: debit لا ينتج balance سالب تحت ضغط طلبين متزامنين.
- [x] Ledger reconciliation test: مجموع transactions يساوي current balance.
- [x] Delete restriction test: لا يمكن حذف user/student balance/teacher account/access code إذا توجد ledger/audit rows.
- [x] SMS double-match test: نفس recharge request لا يمكن ربطه بأكثر من SMS log.
- [x] Recharge idempotency test: duplicate SMS أو retry لا يضيف credit مرتين.
- [x] Payout overcommit test: طلبان صرف متزامنان لا يتجاوزان available balance.
- [x] Serialization failure retry test: `40001` يعاد محاولته أو يرجع 409 ثابت.
- [x] StudentAccessGrant constraint tests: grant بلا target أو multiple targets يفشل.
- [x] Migration test أو SQL smoke يثبت أن constraints/indexes موجودة.

### Manual QA

- [x] طالبان/طلبان متزامنان يحاولان شراء محتوى بنفس الرصيد المحدود.
- [x] رفع SMS متكرر لنفس التحويل والتأكد أن الرصيد لا يزيد مرتين.
- [x] مدرس يطلب أكثر من payout متزامن والتأكد من reservation الصحيح.
- [x] محاولة حذف entity لها سجل مالي والتأكد أن النظام يمنعها برسالة واضحة.

### بوابة الخروج

- invariants المالية محمية في DB أو atomic SQL.
- أي conflict مالي يرجع نتيجة مفهومة وليس 500 عشوائي.
- reconciliation tests خضراء.

## Phase 3: Uploads, Assets, Worker, and Infrastructure Security

### الهدف

تقليل مخاطر الملفات، static assets، worker admin surfaces، Redis، readiness، والاتصالات الخارجية التي يمكن أن تعطل أو تكشف النظام.

### يغطي من التدقيق

- P1-7 Resource upload trusts client MIME and serves from `wwwroot`
- P1-8 Nginx/assets expose broad static files with wildcard CORS
- P1-9 Worker uses fragile external download fallbacks for core AI analysis
- P1-10 Worker admin/Bull Board is only token-protected and port-exposed locally
- P1-11 E2E destructive controller is powerful enough to wipe data if environment is wrong
- P1-17 Redis Stream jobs can get stuck after worker crash
- P1-18 Job ingestion breaks idempotency and can undo cancellation
- P1-19 Container healthcheck reports healthy before worker readiness
- P1-20 Long-running worker external calls lack timeouts
- P2-11 Nginx has no HTTPS/TLS config in the checked-in production proxy
- P2-19 Worker logs expose operational details
- P2-24 Redis has no auth/persistence hardening in Compose variants
- P2-25 Worker image runs as root

### التاسكات

- [x] فحص upload extension + magic bytes بدل `file.ContentType` فقط.
- [x] تطبيع stored filename/extension ومنع browser-interpretable unsafe content.
- [x] نقل untrusted resources خارج `wwwroot` أو تقديمها عبر controller بـ `Content-Disposition: attachment`.
- [x] فصل public assets عن protected assets في storage/nginx.
- [x] حماية protected media بـ signed/authenticated URLs أو `X-Accel-Redirect`.
- [x] تقييد CORS للـ protected media على Massar origins فقط.
- [x] تعطيل Bull Board في production افتراضيا، أو وضعه خلف admin auth/VPN.
- [x] إضافة rate limits وaudit logs للـ worker admin endpoints.
- [x] منع نشر worker port في production compose.
- [x] إضافة database suffix/prefix guard وstartup fail-fast للـ E2E destructive controller.
- [x] إضافة `XAUTOCLAIM` أو `XCLAIM` recovery لـ Redis streams.
- [x] معاملة `jobId` كـ idempotency key وعدم إزالة existing BullMQ jobs في ingestion العادي.
- [x] عدم مسح cancellation markers إلا في explicit admin retry.
- [x] استخدام `/ready` في Docker healthcheck وترك `/health` liveness فقط.
- [x] إضافة timeout/retry wrapper مركزي لكل external fetch/provider/callback.
- [x] تصنيف failures في AI/download وتقديم remediation واضح للأدمن.
- [x] تحويل worker logs إلى structured redacted logger.
- [x] تفعيل Redis auth/persistence/maxmemory policy في compose variants المناسبة.
- [x] تشغيل worker image كمستخدم غير root.
- [x] توثيق TLS termination أو إضافة 443 config لو nginx يستخدم مباشرة في production.

### الاختبارات الآلية المطلوبة

- [x] Upload tests: MIME spoofed file مرفوض.
- [x] Upload tests: allowed file يقدم كـ attachment أو من storage محمي.
- [x] Asset access tests: private upload غير متاح من public assets domain.
- [x] Worker admin tests: `/ui` وadmin endpoints غير متاحة بدون auth صحيح.
- [x] E2E destructive controller tests: يفشل عند DB name غير test/e2e.
- [x] Redis stream recovery test: pending message من dead consumer يتم claim ومعالجته مرة واحدة.
- [x] Job idempotency test: duplicate stream message لا يعيد completed/cancelled job.
- [x] Timeout test: hung provider يفشل بتصنيف معروف ولا يعلق worker.
- [x] Docker healthcheck config test: worker يستخدم `/ready`.
- [x] Container smoke: worker لا يعمل كـ root.

### Manual QA

- [ ] رفع ملفات مسموحة ومرفوضة من admin resource upload.
- [ ] محاولة فتح protected asset مباشرة من browser بدون auth.
- [ ] فتح worker UI في production-like config والتأكد أنه مغلق.
- [ ] إيقاف worker أثناء job ثم تشغيله والتأكد من recovery.
- [ ] تجربة job cancelled ثم وصول duplicate stream message والتأكد أنه لا يعود للعمل.

### بوابة الخروج

- protected assets ليست public بالخطأ.
- worker readiness يعكس جاهزية فعلية.
- jobs لا تضيع ولا تعود بعد cancellation بسبب duplicate delivery.
- لا توجد external call بدون timeout في المسارات الحرجة.

## Phase 4: Stability, Performance, and Maintainability

### الهدف

تقليل خطر الملفات الضخمة، N+1 queries، orchestration المختلط، logging العشوائي، وأي code structure تجعل الإصلاحات القادمة عالية المخاطر.

### يغطي من التدقيق

- P2-3 SignalR hook can leak connections on fast unmount
- P2-4 Staff live-support bootstrap has N+1 query risk
- P2-6 AppDbContext is too large and high-risk
- P2-7 Frontend files are too large
- P2-8 Worker `index.ts` is a large mixed-responsibility orchestrator
- P2-9 Worker cron interval comment conflicts with behavior
- P3-1 Duplicate/unused usings in tests
- P3-2 Some comments document old phases or implementation history
- P3-3 Inconsistent route names and legacy paths
- P3-4 Mixed English/Arabic operational labels
- P3-6 Static analysis should enforce no production console logs

### التاسكات

- [ ] إصلاح `useSignalR` بحيث ref يتسجل قبل `start()` مع cancellation cleanup.
- [ ] Batch live-support bootstrap queries بدل N+1.
- [ ] تقسيم `AppDbContext` تدريجيا إلى `IEntityTypeConfiguration<T>` حسب bounded context.
- [ ] استخراج من الملفات الكبيرة: API slices، state machines، sections، hooks، domain helpers.
- [ ] تقسيم worker `index.ts` إلى `server.ts`, `queues.ts`, `streamConsumer.ts`, `health.ts`, `callbacks.ts`, `startup.ts`.
- [ ] تحويل cron cadence إلى config أو BullMQ repeatable jobs وتحديث الاسم/التعليق.
- [ ] إزالة duplicate usings وتعليقات phase القديمة.
- [ ] إنشاء route inventory وقواعد تسمية canonical.
- [ ] مركزية ترجمة statuses والـ role labels للمستخدمين.
- [ ] إضافة ESLint rule يمنع production `console.log` في frontend.
- [ ] استخدام structured logger wrapper في worker بدل raw console في production paths.

### الاختبارات الآلية المطلوبة

- [ ] SignalR lifecycle test أو component test يثبت cleanup عند unmount سريع.
- [ ] Live-support service test أو query-count regression test للـ bootstrap.
- [ ] Unit tests للـ extracted domain helpers قبل وبعد refactor.
- [ ] Worker startup tests بعد التقسيم.
- [ ] ESLint rule test/CI gate يمنع `console.log` غير المسموح.
- [ ] Route inventory snapshot أو script يكشف routes بلا permission/navigation mapping عند الحاجة.

### Manual QA

- [ ] فتح live support staff dashboard مع بيانات كثيرة والتأكد من سرعة التحميل.
- [ ] التنقل السريع بين صفحات SignalR والتأكد من عدم تكرار connections.
- [ ] تشغيل worker startup/shutdown محليا بعد التقسيم.
- [ ] مراجعة labels العربية في admin/student/teacher surfaces.

### بوابة الخروج

- لا يوجد refactor كبير بدون tests تغطي السلوك المنقول.
- worker وfrontend/backend builds خضراء بعد التقسيم.
- الملفات الضخمة الأساسية أصبحت قابلة للمراجعة أو لديها خطة extraction موثقة.

## Phase 5: UI/UX Normalization and Accessibility

### الهدف

إرجاع الواجهة إلى Massar design system وتقليل drift في الألوان، الكروت، الحركة، mobile touch targets، والـ empty states.

### يغطي من التدقيق

- P1-12 UI/UX brand drift across feature surfaces
- P1-13 Mobile student touch targets are inconsistent
- P1-21 Teacher mobile drawer is not keyboard-safe
- P1-22 Public about page conflicts with Massar brand direction
- P1-23 Reduced motion is incomplete for key animated components
- P2-14 UI overuses card/glass language where dense tools need calmer structure
- P2-15 Student community uses admin token names and gray social colors
- P2-16 `dangerouslySetInnerHTML` appears in homework result answers
- P2-27 Student bottom navigation is overloaded
- P2-28 Quick access empty state disappears
- P2-29 Landing/public nav uses generic glass morphing
- P2-30 Dark theme drifts toward warm cream/brown
- P2-31 Student packages hero is visually heavy on mobile
- P3-5 Some UI components use icon/color choices outside the design system
- P3-9 Duplicate student bottom-nav implementations can drift
- P3-10 Logo dark full variant collapses to mark only

### التاسكات

- [ ] استبدال ad hoc `cyan/slate/indigo/purple/emerald` بـ Massar semantic tokens.
- [ ] تعريف status/stage tokens مثل `status.info`, `status.success`, `stage.filming`.
- [ ] تقليل `rounded-3xl`, `rounded-[28px]`, `backdrop-blur`, `shadow-2xl` في admin/media/live-support dense tools.
- [ ] توحيد card radius للـ operational UI إلى 8-12px إلا لو design system يطلب غير ذلك.
- [ ] فرض 44x44px touch targets في video, exams, homework, recharge, community, packages.
- [ ] توسيع hit area للـ sliders مع إبقاء visual track رفيع.
- [ ] إصلاح Teacher mobile drawer: focus trap، Escape close، focus restore، inert outside، accessible labels.
- [ ] دعم `useReducedMotion` في `ShinyButton` وnavbar width/backdrop transitions.
- [ ] استبدال about page pharaonic direction بـ Massar learning-path imagery/copy.
- [ ] فصل student tokens عن `--admin-*` naming في community.
- [ ] مراجعة `dangerouslySetInnerHTML` في homework result والتأكد من sanitizer tests أو استبداله renderer آمن.
- [ ] تبسيط student bottom nav إلى 4 عناصر بحد أقصى وتوحيد implementation.
- [ ] إضافة empty state مفيد لـ QuickAccessPanel.
- [ ] تعديل public nav إلى header ثابت off-white/navy بدل glass morphing.
- [ ] إصلاح dark theme ليبتعد عن cream/brown dominance.
- [ ] تقليل packages hero على mobile وإظهار actions الأساسية مبكرا.
- [ ] إضافة dark full logo أو render متسق للنص مع العلامة.

### الاختبارات الآلية المطلوبة

- [ ] Accessibility tests للـ drawer focus trap وEscape/focus restore.
- [ ] Reduced-motion tests أو snapshots تثبت توقف infinite/morph animations.
- [ ] Component tests لـ QuickAccess empty state.
- [ ] Sanitizer tests لـ homework rich text: script/event handlers/unsafe links مرفوضة.
- [ ] Visual regression screenshots للصفحات: landing, about, student dashboard, packages, video player, teacher shell, admin media/live-support.
- [ ] Mobile viewport tests تؤكد عدم overflow أو overlapping في bottom nav والـ hero.

### Manual QA

- [ ] تجربة student mobile على عرض 360px و390px: bottom nav، video controls، packages.
- [ ] تجربة keyboard فقط للـ teacher drawer.
- [ ] تفعيل reduced motion من النظام ومراجعة navbar/shiny buttons.
- [ ] مراجعة admin dense pages والتأكد أنها أقل زخرفة وأسهل scan.
- [ ] مراجعة dark mode في student/admin surfaces.

### بوابة الخروج

- UI لا يظهر كمنتجات منفصلة بألوان وأنماط مختلفة.
- mobile touch targets في student paths الأساسية صالحة.
- لا توجد animations مستمرة عند reduced motion.
- لا توجد rich text XSS gaps معروفة.

## Phase 6: Final Regression, Release Readiness, and Operating Runbook

### الهدف

تجميع كل الإصلاحات في release candidate واحد مع regression testing، rollback، runbook، ومؤشرات مراقبة.

### يغطي من التدقيق

- جميع البنود المتبقية P3 وأي risks فتحت أثناء Phases 0-5.
- Verification gaps المذكورة في audit: backend tests لم تعمل بسبب build، frontend build لم يشغل، browser/manual QA لم يتم، mobile builds لم يتم.

### التاسكات

- [ ] تشغيل full backend tests بعد إصلاح build.
- [ ] تشغيل frontend lint/typecheck/build وPlaywright smoke.
- [ ] تشغيل worker build/tests وqueue smoke.
- [ ] تشغيل Docker compose config/startup smoke في بيئة محلية نظيفة.
- [ ] توثيق migration order وbackup/rollback plan.
- [ ] توثيق secrets rotation التي تمت أو المطلوبة.
- [ ] إضافة deploy readiness checklist: CI success، DB backup، migrations، health/readiness، queue backlog، nginx/TLS، rollback.
- [ ] تحديث docs index وتحديد superseded reports.
- [ ] فتح follow-up specs فقط للبنود الكبيرة التي لم تدخل في هذا remediation pass.

### الاختبارات الآلية المطلوبة

- [ ] `make verify` أو الأمر المعتمد الجديد من Phase 0.
- [ ] `dotnet test` للـ backend.
- [ ] `npm run lint`, `npm run typecheck`, `npm run build` للـ frontend إن كانت scripts موجودة.
- [ ] Playwright smoke على critical flows: login, admin permission denial, student purchase/access, recharge, video playback shell.
- [ ] `npm run build` وtests للـ worker.
- [ ] `docker compose config -q` وstartup smoke.
- [ ] Migration dry-run أو test database apply.

### Manual QA

- [ ] Admin: login، permission denial، upload resource، finance/recharge review، media/live support page.
- [ ] Student: login، dashboard، package/lesson access، video controls، homework/exam result.
- [ ] Teacher: login، mobile drawer، finance/payout flow إذا تم تغييره.
- [ ] Parent report: access path بدون token leakage غير مقبول.
- [ ] Worker: AI job status/retry/cancel حسب الصلاحيات.

### بوابة الخروج

- كل P0 وP1 مغلق أو له استثناء مكتوب ومقبول.
- كل test command موثق ويعمل.
- لا توجد أسرار أو generated artifacts في Git.
- يوجد rollback/runbook واضح قبل أي production deploy.

## ترتيب التنفيذ المقترح

1. Phase 0: Baseline Hygiene and Verification Contract.
2. Phase 1: Authentication, Sessions, and Permission Safety.
3. Phase 2: Financial and Data Integrity Hardening.
4. Phase 3: Uploads, Assets, Worker, and Infrastructure Security.
5. Phase 4: Stability, Performance, and Maintainability.
6. Phase 5: UI/UX Normalization and Accessibility.
7. Phase 6: Final Regression, Release Readiness, and Operating Runbook.

## Definition of Done لأي Phase

- [ ] كل defects المحددة للمرحلة تم إصلاحها أو تم نقلها صراحة لمرحلة لاحقة مع سبب.
- [ ] كل tests المطلوبة للمرحلة إما خضراء أو يوجد سبب موثق لعدم تشغيلها.
- [ ] Manual QA checklist مكتملة أو محدد من لم يتم ولماذا.
- [ ] أي DB migration لها rollback/forward note.
- [ ] أي security/secret issue له rotation note.
- [ ] لا توجد تغييرات generated أو unrelated في diff.
- [ ] تم تحديث docs/AGENTS/Makefile/package scripts عند تغيير command contract.

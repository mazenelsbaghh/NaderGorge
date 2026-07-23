# Project Achievements & SDD Phase Progress / الإنجازات وتقدم المراحل

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

# Phase 3: Product UX, Frontend Quality, and Polish

تاريخ الإنشاء: 2026-06-04  
المصدر: `docs/project-deep-audit-2026-06-04.md`  
الأولوية: متوسطة إلى منخفضة، لكنها مهمة لجاهزية المنتج  
الهدف: تحسين تجربة الطالب والأدمن، إزالة البيانات الوهمية، تقليل ضوضاء الواجهة والكود، وتثبيت جودة frontend.

## تعليمات تنفيذ عامة للموديل المنفذ

- نفذ هذه المرحلة بعد إغلاق مخاطر Phase 1 الأساسية.
- لا تجعل التصميم أجمل فقط. اربط كل تعديل بوظيفة واضحة للطالب أو الأدمن.
- الطالب mobile-first. الهدف: يدخل، يعرف المطلوب، ينجزه، ويخرج.
- الأدمن يحتاج معلومات حقيقية وقابلة للثقة، لا أرقام mock.
- لا تضف زخرفة عامة بلا معنى. الخلفية يمكن الحفاظ عليها، لكن الواجهة يجب أن تكون أهدأ وأوضح.
- بعد أي تغيير UI كبير، افتح الصفحة محليا وتحقق من desktop وmobile.

## نطاق المرحلة

هذه المرحلة تغطي البنود التالية من التدقيق:

- P2-10: إزالة/mock analytics أو استبدالها بempty state.
- P2-13: خفض lint warnings.
- P2-14: إعادة ضبط UI/UX للطالب والأدمن حسب design context.
- P2-15: تنظيف secure video player unfinished state.
- P3-1: الحفاظ على artifacts غير tracked.
- P3-2: قياس/تقليل مكونات UI الثقيلة.
- P3-3: توحيد UX copy والـ glossary وإزالة brand drift المتبقي.
- ملاحظات الطالب والأدمن في نهاية تقرير التدقيق.

## Task 1: إزالة mock analytics من صفحات الإدارة

### المشكلة

صفحات admin تعرض أرقام mock/random داخل صفحات حقيقية. هذا خطر ثقة لأن الأدمن قد يبني قرارات على أرقام غير حقيقية.

### ملفات يجب مراجعتها

- `frontend/src/components/admin/EntityOverviewDashboard.tsx`
- `frontend/src/app/admin/content/packages/[id]/page.tsx`
- `frontend/src/app/admin/content/sections/[id]/page.tsx`
- `frontend/src/app/admin/content/lessons/[id]/page.tsx`
- `frontend/src/app/admin/content/terms/[id]/page.tsx`
- `frontend/src/components/admin/AttachedExamViewer.tsx`

### خطوات التنفيذ

1. ابحث عن:
   - `mockStats`
   - `Math.random`
   - أي أرقام ثابتة تبدو analytics.
2. لا تعرض رقم analytics إلا إذا جاء من API حقيقي.
3. إن لم توجد API:
   - اعرض empty state صريح: "لا توجد بيانات تحليلية بعد".
   - لا تستخدم أرقام تقريبية أو random.
4. لو كان mock مطلوبا في development:
   - اجعله خلف feature flag مثل `NEXT_PUBLIC_ENABLE_MOCK_ANALYTICS=true`.
   - default يجب أن يكون false.
5. في `AttachedExamViewer`, أزل `Math.random()` للإحصائيات أو استبدله ببيانات حقيقية من props/API.

### اختبارات القبول

- refresh لا يغير أرقام analytics عشوائيا.
- production build لا يعرض mock data.
- صفحات content details تعرض empty state واضح عند عدم وجود analytics.

### أخطاء ممنوعة

- ممنوع تغيير label فقط مع بقاء الرقم الوهمي.
- ممنوع استخدام random كـ key أو metric.
- ممنوع وضع mock flag default true.

## Task 2: إعادة تصميم student shell حول "المهمة التالية"

### المشكلة

Student shell يطابق AdminShell تقريبا، وهذا يجعل تجربة الطالب أقرب إلى dashboard إداري. حسب `PRODUCT.md`، الطالب يدخل ليعرف المطلوب وينجزه بسرعة.

### ملفات يجب مراجعتها

- `PRODUCT.md`
- `DESIGN.md`
- `frontend/src/app/student/layout.tsx`
- `frontend/src/components/layout/StudentShellChrome.tsx`
- `frontend/src/app/student/page.tsx`
- `frontend/src/app/student/packages/page.tsx`
- `frontend/src/app/student/lessons/[id]` أو مسار lesson الحالي.
- `frontend/src/app/student/exams/[examId]`
- `frontend/src/app/student/mistakes/page.tsx`

### مبادئ التصميم المطلوبة

- الطالب mobile-first.
- أول شاشة يجب أن تجيب:
  - ماذا علي أن أفعل الآن؟
  - ما الذي تم فتحه؟
  - ما المقفل ولماذا؟
  - هل لدي امتحان/واجب/فيديو مطلوب؟
- navigation يجب أن يكون محدودا وواضحا.
- لا تستخدم shell إداري كثيف للطالب.
- الخلفية الحالية يمكن الحفاظ عليها، لكن الزخرفة يجب أن تكون motif موحد وخفيف.

### خطوات التنفيذ

1. افصل student shell وظيفيا عن admin shell:
   - لا تنسخ admin spacing/navigation كما هو.
   - اجعل الطالب يرى progress path.
2. في صفحة `/student`:
   - أبرز next task.
   - اعرض progress مختصر.
   - اعرض locked/unlocked state بوضوح.
   - اخف التفاصيل غير الضرورية خلف صفحات فرعية.
3. في `/student/packages`:
   - اجعل الباقات قابلة للمسح السريع.
   - وضح ما تم شراؤه/متاح/مقفل.
4. في lesson page:
   - ركز على الفيديو، الحالة، الفصل التالي، الامتحان المطلوب.
   - لا تجعل أدوات كثيرة تنافس player.
5. في exams/mistakes:
   - اعرض actionable review وليس dashboard عام.
6. اختبر mobile viewport أولا ثم desktop.

### اختبارات القبول

- على mobile، الطالب يرى next action بدون scroll طويل.
- لا يوجد overlap بين النصوص والأزرار.
- locked state مفهوم بدون قراءة شرح طويل.
- student shell لا يبدو نسخة مصغرة من admin shell.

### أخطاء ممنوعة

- ممنوع إضافة hero marketing للطالب.
- ممنوع cards داخل cards.
- ممنوع زخارف كثيرة تقلل وضوح المهام.
- ممنوع relying على 1px borders كفاصل أساسي في كل مكان.

## Task 3: Polish admin shell بدون mock أو ازدحام

### المشكلة

admin shell أفضل من student shell لكنه يحتوي borders/cards كثيرة وبعض الصفحات تعرض بيانات غير حقيقية. الأدمن يحتاج scan سريع وثقة في البيانات.

### ملفات يجب مراجعتها

- `frontend/src/components/layout/AdminShellChrome.tsx`
- `frontend/src/app/admin/users/[id]/page.tsx`
- `frontend/src/app/admin/ai-monitor` أو المسار الحالي.
- صفحات content details المذكورة في Task 1.

### خطوات التنفيذ

1. قلل الاعتماد على borders الكثيرة.
2. استخدم spacing وtonal surfaces بدلا من خطوط 1px في كل فصل.
3. اجعل actions الأساسية واضحة:
   - save
   - retry
   - cancel
   - approve/reject
4. في AI monitor:
   - لا تعرض raw worker internals للمستخدم.
   - اعرض status مفهوم: queued, processing, failed, completed.
   - actions الخطرة مثل cancel/retry تحتاج تأكيد.
5. في user details:
   - أصلح missing hook deps.
   - لا تستخدم random keys.

### اختبارات القبول

- admin pages لا تعرض mock analytics.
- AI monitor status مفهوم وقابل للتصرف.
- لا يوجد layout shift واضح بسبب random keys أو dynamic text.

## Task 4: تقليل lint warnings إلى صفر أو near-zero

### المشكلة

`npm run lint` ينجح لكنه ينتج 106 warnings. warnings الكثيرة تخفي المشاكل المهمة مثل missing hook dependencies.

### ملفات يجب مراجعتها

- ابدأ بنتيجة `npm run lint`.
- أمثلة من التقرير:
  - `frontend/src/app/admin/users/[id]/page.tsx`
  - `frontend/src/components/ui/animated-theme-toggler.tsx`
  - `frontend/src/components/video/PlayerControls.tsx`

### خطوات التنفيذ

1. شغل `cd frontend && npm run lint`.
2. صنف warnings:
   - missing hook dependencies
   - unused imports/vars
   - `<img>` بدل `next/image`
   - dependency instability
3. عالج missing hook dependencies بحذر:
   - لا تضف dependency فقط لإسكات ESLint إذا كان سيخلق loop.
   - استخدم `useCallback`, `useMemo`, أو ref حسب الحاجة.
4. احذف unused imports/vars.
5. استبدل `<img>` بـ `next/image` عندما يكون مناسبا.
6. بعد الوصول إلى 0 warnings، اجعل CI يفشل على warnings إن أمكن.

### اختبارات القبول

- `npm run lint` ينجح بدون warnings أو بعدد قليل موثق.
- لا توجد infinite render loops جديدة.
- player controls لا تفقد behavior.

### أخطاء ممنوعة

- ممنوع تعطيل قواعد lint على نطاق واسع.
- ممنوع إضافة comments `eslint-disable` إلا لحالة مبررة جدا ومحدودة بسطر.

## Task 5: تنظيف SecureVideoPlayer والـ video UX

### المشكلة

`SecureVideoPlayer` يحتوي variables/handlers غير مستخدمة مثل quality controls، والـ embed route كان به injection risk في Phase 1. بعد إصلاح الأمان، يجب تنظيف الحالة غير المكتملة.

### ملفات يجب مراجعتها

- `frontend/src/components/video/SecureVideoPlayer.tsx`
- `frontend/src/components/video/PlayerControls.tsx`
- `frontend/src/components/video/InteractiveTimeline.tsx`
- `frontend/src/app/api/video/embed/route.ts`

### خطوات التنفيذ

1. راجع `qualityLevels`, `currentQuality`, `handleQualityChange`, `onEnded`.
2. لو quality switching مطلوب:
   - وصله فعليا بمصادر الفيديو.
   - اعرض control واضح.
   - اختبر التغيير.
3. لو غير مطلوب الآن:
   - احذف state والhandlers غير المستخدمة.
4. تأكد أن player controls لا تغير layout عند hover أو عند تغيير النص.
5. تأكد أن chapters/timeline لا تغطي أزرار مهمة.
6. لا تدعي "anti-download" كحماية مطلقة. الواجهة يمكن أن تقلل misuse لكنها لا تمنع download تماما.

### اختبارات القبول

- لا توجد lint warnings من secure player.
- controls الظاهرة تعمل.
- mobile video controls قابلة للمس ولا تتداخل.
- chapter timeline لا يكسر player.

## Task 6: قياس وتقليل مكونات UI الثقيلة

### المشكلة

يوجد مكونات UI عامة قد تزيد bundle أو تضيف visual noise لو دخلت صفحات الطلاب بدون lazy loading.

### ملفات يجب مراجعتها

- `frontend/src/components/ui/circular-gallery.tsx`
- `frontend/src/components/ui/feature-carousel.tsx`
- `frontend/src/components/ui/ripple-grid.tsx`
- `frontend/src/components/ui/resizable-navbar.tsx`
- imports لهذه المكونات عبر المشروع.

### خطوات التنفيذ

1. ابحث عن استخدام كل مكون.
2. إن كان غير مستخدم:
   - احذفه إذا لا توجد خطة واضحة لاستخدامه.
   - أو اتركه فقط لو جزء من library مقصودة، لكن لا تستورده في الصفحات.
3. إن كان مستخدما في صفحات الطالب:
   - قيّم هل يخدم workflow أم مجرد زخرفة.
   - استخدم lazy loading/dynamic import للمكونات الثقيلة.
4. شغل bundle analyzer إذا متاح.
5. لا تضف animations ثقيلة للصفحات الدراسية الأساسية.

### اختبارات القبول

- لا توجد imports ثقيلة غير ضرورية في student first screen.
- bundle size لا يزيد.
- الصفحة تعمل بسلاسة على mobile.

## Task 7: توحيد UX copy والـ glossary العربي

### المشكلة

الـ copy مختلط بين عربي رسمي ومصطلحات تقنية وأسماء brand قديمة. هذا يضعف ثقة المنتج ويجعل الواجهة أقل وضوحا.

### ملفات يجب مراجعتها

- navigation components.
- admin AI monitor.
- worker/progress messages المعروضة في frontend.
- كل ظهور لـ:
  - `AI`
  - `basma`
  - `bsma`
  - `acadmy`
  - أي brand قديم.

### Glossary مقترح

- `AI Analysis` = `تحليل الفيديو بالذكاء الاصطناعي`
- `AI Monitor` = `متابعة التحليل`
- `Mindmaps` = `خرائط ذهنية`
- `Chapters` = `فصول الفيديو`
- `Worker` لا تعرض للطالب أو الأدمن غالبا. استخدم `النظام` أو `المعالجة`.
- `Retry` = `إعادة المحاولة`
- `Cancel` = `إلغاء`
- `Queued` = `في الانتظار`
- `Processing` = `قيد المعالجة`
- `Completed` = `اكتمل`
- `Failed` = `فشل`

### خطوات التنفيذ

1. ابحث عن copy المختلط.
2. استبدل المصطلحات التقنية الظاهرة للمستخدم بمصطلحات مفهومة.
3. لا تغير أسماء APIs أو enum values إلا لو مطلوب.
4. أزل كل brand قديم من النصوص الظاهرة والdomains hardcoded.
5. اجعل رسائل الخطأ قصيرة وعملية:
   - ماذا حدث؟
   - ماذا يفعل المستخدم الآن؟

### اختبارات القبول

- لا توجد brand strings قديمة في frontend visible text.
- admin status messages مفهومة بدون معرفة BullMQ/worker.
- student messages قصيرة ومباشرة.

## Task 8: الحفاظ على build artifacts خارج git

### المشكلة

الفحص وجد أن build artifacts غير tracked، وهذا جيد. المطلوب الحفاظ على هذا الوضع.

### ملفات يجب مراجعتها

- `.gitignore`
- أي generated folders:
  - `node_modules`
  - `.next`
  - `dist`
  - `bin`
  - `obj`

### خطوات التنفيذ

1. شغل:
   - `git ls-files | rg '(^|/)(node_modules|\\.next|dist|bin|obj)(/|$)'`
2. يجب أن يرجع 0.
3. لو ظهر artifact tracked:
   - لا تحذفه من disk بالضرورة.
   - أزله من git tracking فقط بعد التأكد.
4. راجع `.gitignore` يغطي frontend/worker/backend artifacts.

### اختبارات القبول

- artifacts غير tracked.
- لا يتم إدخال build output في commit.

## Task 9: Pass responsive/adapt على الصفحات الأساسية

### المشكلة

الطالب mobile-first، وأي overflow أو overlap سيؤثر مباشرة على الاستخدام اليومي.

### صفحات يجب اختبارها

- `/student`
- `/student/packages`
- `/student/lessons/[id]`
- `/student/exams/[examId]`
- `/student/mistakes`
- `/admin/users/[id]`
- `/admin/ai-monitor`

### خطوات التنفيذ

1. شغل dev server.
2. افتح الصفحات على viewports:
   - mobile: 390x844
   - tablet: 768x1024
   - desktop: 1440x900
3. تحقق من:
   - no horizontal scroll.
   - no overlapping text/buttons.
   - touch targets لا تقل عن 44px تقريبا.
   - nav لا يغطي المحتوى.
   - player controls لا تتداخل.
4. أصلح CSS بمقاييس stable:
   - `minmax`
   - `aspect-ratio`
   - `max-width`
   - responsive grid tracks
5. لا تستخدم font-size based on viewport width.

### اختبارات القبول

- كل الصفحات الأساسية تعمل على mobile بدون horizontal overflow.
- النصوص الطويلة لا تخرج من buttons/cards.
- controls المهمة قابلة للمس.

## Task 10: Pass visual polish نهائي

### الهدف

بعد إزالة mock data وتنظيف shell، اعمل pass بسيط يرفع جودة الشكل بدون إدخال تعقيد جديد.

### خطوات التنفيذ

1. قلل borders المتكررة.
2. اجعل spacing consistent.
3. لا تستخدم cards داخل cards.
4. اجعل headings داخل panels صغيرة ومناسبة.
5. راجع empty states:
   - admin analytics empty state.
   - no packages.
   - locked lesson.
   - no mistakes.
6. راجع loading states:
   - لا تعرض layout يقفز.
   - skeletons أو compact loaders.
7. راجع error states:
   - لا تعرض stack traces.
   - قدم action واضح.

### اختبارات القبول

- الواجهة لا تقرأ كلون واحد ممل أو كـ dashboard عام.
- الطالب يعرف المهمة التالية بسرعة.
- الأدمن لا يرى أرقام وهمية.
- لا يوجد visual overlap أو نص خارج الحاوية.

## أوامر تحقق مطلوبة قبل إنهاء المرحلة

```bash
cd frontend && npm run build && npm run lint
```

لو تم لمس backend أو worker بسبب copy/API:

```bash
dotnet test backend/NaderGorge.sln --no-restore
cd worker && npm run build
```

## تعريف اكتمال Phase 3

تعتبر المرحلة مكتملة عندما:

- لا توجد mock analytics ظاهرة في production.
- student shell موجه للمهمة التالية وليس نسخة admin.
- lint warnings وصلت إلى صفر أو تم توثيق الباقي كاستثناءات محدودة.
- secure video player لا يحتوي controls غير مكتملة ظاهرة أو warnings واضحة.
- copy العربي موحد وخال من brand قديم.
- الصفحات الأساسية مختبرة على mobile/desktop بدون overflow أو overlap.

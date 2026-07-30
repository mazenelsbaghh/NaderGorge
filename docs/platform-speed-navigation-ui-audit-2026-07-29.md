# تقرير سرعة المنصة والتنقل والشاشات والحركة

**التاريخ:** 2026-07-29  
**النطاق:** Next.js frontend، تجربة التنقل، الشاشات وحالات التحميل، الحركة والـcarousels، API/EF Core، طبقة الإنتاج الثلاثية، وقياسات Cloudflare المرفقة.  
**طريقة المراجعة:** قراءة كود ومخرجات build موجودة وأدلة تحميل موجودة، مع مراجعات مستقلة بواسطة ثلاثة sub-agents. تمت مراجعة صفحات تقرير Cloudflare الثلاث بصريًا مع استخراج نصي للتحقق من القيم والعناصر، من الملف المحلي المرفق فقط ومن دون بحث إنترنت.

> **حدّ زمني مهم:** هذا التقرير هو baseline قبل تنفيذ feature 167. أدلة
> الملفات وأرقام الأسطر وقوائم P1/P2 أدناه تصف حالة الـworking tree وقت
> المراجعة، وقد تحركت أو أزيلت بعض الأسطر أثناء الإصلاح. حالة المعالجة الحالية
> موثقة في
> `specs/167-platform-speed-completion/reports/phase9-architecture-review.md`
> و`specs/167-platform-speed-completion/reports/phase9-ui-ux-review.md`؛ أما
> نتائج المتصفح والبناء والحمل النهائية فتبقى pending حتى تشغيلها على المرشح
> المختوم.

## 1. الخلاصة التنفيذية

المنصة ليست بطيئة بسبب عامل واحد. طبقة الخوادم الأساسية تبدو سريعة في اختبار صناعي محدود، لكن تجربة المستخدم تتأثر أساسًا بأربع طبقات:

1. **تكلفة JavaScript والهيدريشن:** عدد كبير من Client Components وصفحات كبيرة جدًا، وبعض الـlayouts العامة تحمل كودًا لا تحتاجه كل المسارات.
2. **التنقل يعيد تهيئة أجزاء كبيرة من التطبيق:** root template والـshells داخل الصفحات يفقدان state وscroll ويعيدان effects وطلبات البيانات.
3. **نمط جلب البيانات:** لا توجد query cache موحدة، وبعض الشاشات تجلب بيانات ضخمة أو تعيد الطلب عند كل mount أو ضغطة بحث.
4. **الحركة والرسوم:** WebGL في التسجيل، تحريك خصائص layout، وعدم احترام reduced-motion على نطاق واسع، وكل ذلك يضغط INP على أجهزة الطلاب الضعيفة.

### الحكم المختصر

- **P0 Blocking:** لا يوجد شيء مؤكد يمنع استخدام المنصة.
- **P1 Major:** 10 مجموعات مشاكل يجب تنفيذها أولًا.
- **P2 Minor/Medium:** 9 مجموعات تحسينات مهمة بعد الـP1.
- **P3 Polish:** تحسينات دلالية وبصرية منخفضة المخاطر.
- **UI technical health score:** **11/20 - مقبول، لكنه يحتاج معالجة مهمة قبل وصف التجربة بأنها سريعة ومتسقة.**

أكبر مكسب متوقع لن يأتي من زيادة موارد السيرفر أولًا، بل من تثبيت الـshell، تقليل client JS، إعادة prefetch الانتقائي، cache موحدة للطلبات، وإزالة الحركة الثقيلة من مسارات الدخول والتسجيل.

## 2. ما الذي تثبته البيانات الحالية؟

### 2.1 تقرير Cloudflare المرفق

التقرير يغطي 24 ساعة من 28 إلى 29 يوليو 2026، مع استبعاد البوتات:

| المؤشر | Good | Needs improvement | Poor | النتيجة المهمة |
|---|---:|---:|---:|---|
| LCP | 77% | 14% | 8% | p75 = 2380ms، قريب جدًا من حد 2.5s |
| INP | 66% | 25% | 9% | أضعف مؤشر ويجب أن يكون الأولوية |
| CLS | 98% | 1% | 0% | جيد جدًا؛ ليس الأولوية العامة |

قيم LCP الإضافية:

- p50 = 1572ms
- p75 = 2380ms
- p90 = 3668ms
- p99 = 14828ms

### قيد مهم على العينة

صفحة نتيجة الثانوية استحوذت على أغلب مشاهدات التقرير. بناءً على توجيه صاحب المشروع، **تم استبعاد هذه الصفحة من ترتيب الأولويات**. لذلك لا يصح اعتبار نسب Cloudflare الحالية ممثلة بدقة لكل لوحات الطالب والإدارة والمعلم.

نستخدم التقرير لإثبات أن:

- LCP على الحافة.
- INP مشكلة حقيقية.
- CLS العام جيد.

لكن نحتاج RUM مفصولًا حسب `pathname/surface/device/connection` قبل إسناد السبب لمسار بعينه.

### 2.2 اختبار التحميل الموجود

الدليل:

`artifacts/production/load-src-0541078d-20rps-30m-20260728/evidence/src-0541078-steady-020-30m.load.json`

- 20 request/second لمدة 30 دقيقة.
- 0% أخطاء.
- p95 ≈ 13.33ms.
- توزيع متساوٍ 33.3% على كل node.

هذه نتيجة ممتازة لطبقة ingress والـlanding وAPI live، لكنها **لا تختبر workflows حقيقية، ولا database-heavy endpoints، ولا websocket load** (`workflowRps=0`, `websocketVus=0`). كما أن p99 مسجل بصفر، ولذلك لا يصلح كقياس tail latency موثوق.

الاستنتاج: لا يوجد دليل أن قدرة الخادم الخام هي سبب البطء الحالي، ولا يوجد أيضًا اختبار كافٍ ينفي بطء الـworkflows الفعلية.

### 2.3 حجم الواجهة الموجود

من مخرجات `.next` الحالية:

- 275 ملف JavaScript في `frontend/.next/static/chunks` بإجمالي خام يقارب 12.95MiB.
- chunk واحد يقارب 408KiB ويتجاوز ميزانية 350KiB.
- entry chunks خام تقريبية:
  - `/register`: 870KB.
  - `/student`: 661KB.
  - `/login`: 531KB.
  - بعض مسارات الإدارة: 1.4-1.57MB.

هذه أحجام خام ومن build موجود قبل آخر تغييرات المصدر؛ تُستخدم لتحديد الاتجاه، وليست transfer size إنتاجية نهائية.

## 3. أولويات P1

## P1.1 تثبيت الـshell ومنع إعادة تركيب شجرة التطبيق

**الدليل:**

- `frontend/src/app/template.tsx:1-5`
- `frontend/src/components/layout/StudentShellChrome.tsx:483-488`
- `frontend/src/components/admin/AdminShellChrome.tsx:601-614`
- `frontend/src/components/teacher/TeacherShellChrome.tsx:267-288`
- `frontend/src/app/admin/AdminRootPageClient.tsx:52-57`

الـroot `template.tsx` يعاد تركيبه في كل navigation. وفي الإدارة والمعلم والمساعد، الـshell موجود داخل صفحات كثيرة بدل layout ثابت. النتيجة:

- فقدان collapsed/expanded state.
- فقدان scroll داخل عناصر التمرير الداخلية.
- إعادة تشغيل effects والاشتراكات.
- إعادة طلب سياق المعلم والصلاحيات.
- وميض وحركة عند كل انتقال.

**الإجراء:**

1. إزالة root template أو قصره على route group يحتاج transition فعلًا.
2. وضع Admin/Teacher/Assistant shells في layouts ثابتة.
3. تمرير page title/actions عبر context أو parallel routes.
4. حفظ scroll per pathname فقط للشاشات التي تحتاج ذلك.

**معيار النجاح:** التنقل بين صفحتين داخل نفس الـsurface لا يعيد mount للـshell، ولا يعيد shell bootstrap، ويحفظ collapse وscroll.

## P1.2 إعادة prefetch الانتقائي

**الدليل:**

- 35 استخدامًا لـ`prefetch={false}`.
- `frontend/src/components/layout/StudentShellChrome.tsx:378-402`
- `frontend/src/components/admin/AdminShellChrome.tsx:793-858`
- `frontend/src/components/teacher/TeacherShellChrome.tsx:344`
- `frontend/src/components/assistant/AssistantShellChrome.tsx:112`

تعطيل prefetch في الروابط الرئيسية يجعل أول نقرة تنتظر RSC وJavaScript والبيانات.

**الإجراء:**

- تفعيل default prefetch للوجهات اليومية.
- استخدام intent prefetch عند hover/focus للصفحات الثقيلة.
- إبقاء التعطيل للمسارات النادرة فقط.
- قياس navigation latency وbytes prefetched قبل وبعد.

**معيار النجاح:** p75 client navigation أقل من 300ms للمسارات المتكررة بعد warm session.

## P1.3 تقليل Client Components والحزمة المشتركة

**الدليل:**

- 358 ملفًا يحتوي `use client` من نحو 688 ملف TS/TSX.
- `frontend/src/app/layout.tsx:114-117` يحمل `AuthBootstrap` و`GlobalNav` و`Toaster` لكل المسارات.
- `frontend/src/components/layout/GlobalNav.tsx:36-58` يحمل تبعياته ثم قد يعيد `null`.
- layouts الإدارة والطالب والمساعد Client Components:
  - `frontend/src/app/admin/layout.tsx:1`
  - `frontend/src/app/student/layout.tsx:1`
  - `frontend/src/app/assistant/layout.tsx:1`

**الإجراء:**

- نقل `GlobalNav` إلى public route group.
- إبقاء layouts كـServer Components.
- عزل auth/theme/realtime في client islands صغيرة.
- تقسيم كل tab أو editor ثقيل إلى dynamic chunk.

أكبر الملفات المرشحة للتقسيم:

- `AdminStudentProfileClient.tsx` - 1843 سطرًا.
- `AdminSettingsPageClient.tsx` - 1667 سطرًا.
- `AdminFinancePageClient.tsx` - 1450 سطرًا.
- `SecureVideoPlayer.tsx` - 1306 أسطر.

**معيار النجاح:** خفض initial JS لكل من login/register/student بنسبة 25-40%، وعدم تحميل تبعيات الـpublic nav داخل surfaces المحمية.

## P1.4 Query cache موحدة بدل `useEffect + useState`

**الدليل:**

- لا توجد `@tanstack/react-query` في dependencies.
- `frontend/src/app/student/StudentDashboardClient.tsx:71-89`
- `frontend/src/app/student/packages/PackagesPageClient.tsx:45`
- `frontend/src/app/student/teachers/StudentTeachersPageClient.tsx:44`

الصفحات تعيد الطلب عند mount، وتكرر نفس البيانات بين المسارات، ولا تستفيد من deduplication أو stale data أو cancellation بشكل منهجي.

**الإجراء:**

- اعتماد QueryClient واحد كما اقترح plan 160.
- تعريف query keys موحدة حسب domain.
- `staleTime` حسب نوع البيانات.
- `keepPreviousData` للقوائم.
- invalidation مستهدفة من SignalR بدل refetch شامل.
- prefetch للوجهة التالية.

**معيار النجاح:** لا يتكرر نفس GET المتطابق أثناء التنقل العادي، وتظل البيانات السابقة ظاهرة أثناء refresh.

## P1.5 إصلاح `/student` والـauth transition

**الدليل:**

- auth state يبدأ loading دائمًا: `frontend/src/stores/auth-store.ts:45-49`.
- bootstrap بعد hydration: `frontend/src/components/layout/AuthBootstrap.tsx:15-23`.
- dashboard يبدأ طلبين بعد hydration: `frontend/src/app/student/StudentDashboardClient.tsx:71-89`.
- `frontend/src/app/student/loading.tsx:5-36` يعيد StudentShell رغم وجوده في layout.
- guards ترسل إلى `/login` دون `returnUrl`.
- login يستخدم full document navigation في `frontend/src/app/(public)/login/LoginPageClient.tsx:94-100`.

**الإجراء:**

- حسم session/route boundary server-side قدر الإمكان.
- تمرير `returnUrl` آمنًا.
- استخدام `router.replace` للوجهة same-origin.
- server-prefetch أو hydration للـstudent dashboard.
- جعل `loading.tsx` content skeleton فقط.

**معيار النجاح:** لا يظهر shell مزدوج، ولا تضيع الوجهة المطلوبة، ولا يحدث hard reload داخل نفس origin.

## P1.6 إيقاف WebGL المستمر في التسجيل افتراضيًا

**الدليل:**

- `frontend/src/app/(public)/register/RegisterPageClient.tsx:24-25,54-64`
- `frontend/src/components/ui/ripple-grid.tsx:64,85,235-300`

`RippleGrid` يعمل بـrequestAnimationFrame ودقة حتى DPR=2 ويستمع لحركة الماوس أثناء الكتابة. هذا مرشح قوي لتدهور INP على الهواتف.

**الإجراء:**

- خلفية CSS افتراضية.
- تشغيل WebGL بعد idle فقط وعلى الأجهزة القوية.
- إيقافه أثناء الكتابة وعند hidden tab.
- إيقاف كامل عند `prefers-reduced-motion`.

**معيار النجاح:** تحسن INP p75 للتسجيل، وعدم وجود long task فوق 50ms أثناء الكتابة على جهاز Android متوسط.

## P1.7 إصلاح البحث والقوائم كبيرة البيانات

**الدليل:**

- `frontend/src/app/admin/students/AdminStudentsPageClient.tsx:79` يطلب pageSize=1000.
- كل تغيير search يعيد الطلب: `:99,108`.
- الجدول يعرض 8 صفوف فقط محليًا: `frontend/src/components/admin/AdminDataTable.tsx:57,89`.

**الإجراء:**

- server pagination من 25 إلى 50 صفًا.
- debounce 250-350ms.
- AbortController لإلغاء الطلب السابق.
- cursor pagination للبيانات التي تتغير سريعًا.
- virtualization فقط عند الحاجة لعرض مئات الصفوف فعليًا.

**معيار النجاح:** لا يزيد payload بحث الطلاب عن 100KB عادة، ولا يوجد أكثر من طلب فعال واحد أثناء الكتابة.

## P1.8 ضبط caching الإنتاجي والـLCP للشعار

**الدليل:**

- Cloudflare سجل `logo-mark.svg` كعنصر LCP عند 4856ms في 123 حالة.
- حجم الملف نحو 6.4KB فقط؛ الحجم ليس السبب المرجح.
- navbar يرسم نسختين light/dark مع `priority` و`unoptimized`:
  - `frontend/src/components/ui/resizable-navbar.tsx:248-268`
  - `frontend/src/components/shared/PlatformLogo.tsx:70-87`
- إعداد Nginx الإنتاجي الحالي يمرر كل frontend requests دون locations صريحة للـstatic caching:
  - `deploy/production/config/nginx/massar-node.conf.template:29-93`
- الإعداد القديم كان يضيف immutable سنة لـ`/_next/static` و7 أيام للصور:
  - `docker/nginx/massar.conf:50-60`

**الإجراء:**

- صورة شعار واحدة فقط حسب الثيم، أو inline SVG/CSS mask.
- عدم إعطاء priority لنسختين بديلتين.
- إضافة cache headers صريحة في production gateway:
  - `/_next/static/*`: سنة + immutable.
  - public versioned assets: مدة طويلة + immutable.
  - public non-versioned assets: مدة معقولة + ETag.
- التحقق الفعلي من `Cache-Control`, `Age`, `CF-Cache-Status`, وresource timing.

**معيار النجاح:** الشعار لا يكون LCP على صفحات auth إن كان العنوان أكبر، أو resource load للشعار أقل من 200ms عند cold edge وأقل من 20ms عند browser cache.

## P1.9 إزالة N+1 من Live Support

**الدليل:**

- سجل 100 محادثة ثم 5 طلبات تقريبًا لكل محادثة:
  - `backend/src/NaderGorge.Infrastructure/Services/LiveSupportService.cs:607-635`
- admin dashboard يرسم المحادثات والموظفين عبر loops وqueries:
  - `:715-725`
- timeline يستدعي `ActorNameAsync` داخل loops:
  - `:734-745`
- `MapManyAsync` ثم `MapAsync` ينفذ عدة queries لكل محادثة:
  - `:863-915`

**الإجراء:**

- projections مجمعة.
- تحميل dictionaries للأسماء والحالات مرة واحدة.
- aggregate queries للمجاميع والتقييمات.
- cursor pagination للسجل.
- query-count integration tests للشاشات الحرجة.

**معيار النجاح:** admin live-support dashboard لا يتجاوز 8-12 SQL commands لكل request بصرف النظر عن عدد الصفوف.

## P1.10 تحسين مسار التحقق من الجلسة

**الدليل:**

كل authenticated token validation ينفذ query على Users للتحقق من active/password/security versions:

- `backend/src/NaderGorge.API/Program.cs:219-260`

هذا صحيح أمنيًا، لكنه يضيف database round-trip لكل authenticated API request.

**الإجراء:**

- cache قصيرة 30-60 ثانية لمفاتيح `user security state` في Redis أو memory مع versioned invalidation.
- إلغاء cache فور تعطيل المستخدم أو تغيير كلمة المرور/الصلاحيات.
- قياس أثر DB round-trip قبل التطبيق.

**معيار النجاح:** لا يتم ضرب PostgreSQL في كل request مع الحفاظ على إبطال فوري للجلسات الحساسة.

## 4. أولويات P2

### P2.1 حركة لا تحترم reduced-motion

- 52 ملفًا يستورد Framer/Motion، و9 فقط تستخدم reduced-motion.
- أمثلة: `LessonCarousel.tsx:68-72,272-343` و`RegistrationForm.tsx:1001-1006`.

أضف `MotionConfig reducedMotion="user"` على مستوى التطبيق، ثم راجع الاستثناءات الوظيفية.

### P2.2 Carousels غير مكتملة أو غير قابلة للإيقاف

- Circular gallery يتحرك كل 2.6 ثانية بلا pause: `CircularGallerySection.tsx:121-125`.
- Testimonials arrows بلا `onClick`: `TestimonialsSection.tsx:67-74,116-129`.

نفذ pause/stop وkeyboard semantics، أو أزل controls الوهمية.

### P2.3 تحريك layout وblur

- `resizable-navbar.tsx:87-110,153-176` يحرك width وborderRadius وbackdropFilter.
- `frontend/src/lib/motion.ts:80-85` يحرك `height:auto`.

ثبّت الأبعاد واستخدم `transform/opacity`، واستبدل height animation بـgrid rows أو clipping.

### P2.4 Skeletons وerror boundaries

- skeleton عام لا يطابق كل الصفحات: `AdminPageSkeleton.tsx:8-42`.
- loading غير معلن بـ`aria-busy`.
- error boundaries تعرض `error.message` الخام ولا تنقل focus:
  - `app/admin/error.tsx:16-55`
  - `app/student/error.tsx:16-54`
  - `app/teacher/error.tsx:16-54`

أنشئ skeleton مطابقًا لكل family، وأضف `role=status/alert` وfocus management.

### P2.5 Zustand subscriptions واسعة

يوجد ما لا يقل عن 28 استدعاءً مباشرًا للـstores بلا selector، مثل `GlobalNav.tsx:38` و`usePlatformEvents.ts:336`.

استخدم selectors و`useShallow` وعزل state سريع التغير.

### P2.6 الصور والخطوط

- 39 `<img>` خام.
- 25 `unoptimized`.
- Hero image تتحدد بعد hydration وقد تُحمّل وهي مخفية على الهاتف.
- root يحمل 5 أوزان Tajawal و6 Montserrat: `frontend/src/app/layout.tsx:8-19`.

استخدم `<picture>`/responsive images، قلل الأوزان إلى الاحتياج الفعلي، ولا preload لمورد مخفي.

### P2.7 Design tokens والاتساق

`npm run check:design-tokens` وجد 2811 استخدام لون غير مسموح. هذا يرفع تكلفة الصيانة ويخلق تفاوت dark mode والتباين.

نفذ migration تدريجية حسب surface، مع منع مخالفات جديدة في CI بدل محاولة إصلاح 2811 موضعًا دفعة واحدة.

### P2.8 Outbox transaction طويلة نسبيًا

`OutboxProcessorBackgroundService`:

- poll كل ثانيتين.
- يمسك transaction وrow locks.
- يرسل حتى 50 event بالتتابع أثناء transaction.
- `backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs:65-198`

`SKIP LOCKED` جيد للتعددية، لكن network dispatch داخل transaction يطيل lock time.

افصل claim عن dispatch ثم ack، أو استخدم lease/status claim قصير ومعالجة متوازية محدودة.

### P2.9 القياس والمراقبة غير كافيين

الإيجابي:

- slow DB query logger عند 250ms.
- slow HTTP logger عند 500ms.
- Web Vitals reporter بنسبة 5% للمستخدمين المسجلين.

الفجوات:

- Web Vitals endpoint يحفظ row لكل metric دون dashboard/retention واضح.
- المسارات العامة لا تدخل reporter الداخلي لأنه authenticated فقط.
- لا يوجد ربط بين frontend trace وAPI/SQL.
- load tests لا تغطي workflows.
- budget الحالي يقيس chunks خامًا ولا يفرق initial/shared/async أو gzip/brotli.

المطلوب:

- RUM مفصولًا حسب route/surface/device/connection.
- LCP phase attribution.
- OpenTelemetry trace من browser correlation id إلى API وSQL.
- dashboards لـp50/p75/p95/p99.
- retention/aggregation لجدول WebVitals.
- workflow load: login، student dashboard، packages، admin students search، live support.

## 5. مشاكل التنقل وتجربة الاستخدام

### صلاحيات الإدارة

توجد matrices متضاربة بين:

- `frontend/src/packages/admin/route-permissions.ts`
- `frontend/src/app/admin/layout.tsx`
- `frontend/src/packages/admin/navigation.tsx`

الأثر: رابط قد يظهر ثم ينتهي Unauthorized، وهذا بطء مُدرك وفقد ثقة حتى لو الاستجابة سريعة.

الحل: policy واحدة typed تُستخدم في nav والguard والAPI contract tests.

### mobile drawers

Student drawer يطبق focus trap وEscape وinert وrestore focus جيدًا. استخدمه كأساس لمكون مشترك بدل implementations الإدارة والمعلم والمساعد والعامة التي لا تطبق كل ذلك.

### bottom navigation

شريط الطالب قد يعرض 6 عناصر على الهاتف رغم أن التصميم يفترض عددًا أقل. ثبت 4-5 slots وانقل الباقي للdrawer.

### breadcrumbs وfocus

- أضف skip link إلى `#main-content`.
- انقل focus إلى heading عند client navigation دون كسر back/forward.
- استخدم `aria-current`.
- لا تعرض IDs أو slugs كعناوين.
- اجعل home حسب الـsurface بدل `/`.

## 6. ما يعمل جيدًا ويجب الحفاظ عليه

- `lang="ar"` و`dir="rtl"` مضبوطتان في root.
- CLS جيد في 98% من العينة.
- focus styles عامة واضحة.
- touch targets تصل 44px على الأجهزة اللمسية.
- `AccessibleDialog` يطبق focus trap وinert وEscape وrestore focus وreduced-motion.
- `AdminDataTable` لديه responsive priorities وحالات empty/error/retry جيدة.
- response compression وBrotli/Gzip مفعلة في backend.
- output cache موجود ويستخدم على platform stats.
- Redis backplane وoutbox و`SKIP LOCKED` أساس جيد للتوسع.
- cluster background jobs المهمة تستخدم leases.
- اختبار 20rps يثبت توازنًا ممتازًا بين العقد الثلاث.

## 7. خطة التنفيذ المقترحة

## أول 7 أيام - قياس وQuick Wins

1. dashboard RUM حسب route/device/connection.
2. تثبيت cache headers للـstatic assets والتحقق منها من الخارج.
3. جعل الشعار صورة واحدة.
4. إعادة prefetch لأكثر 10 روابط استخدامًا.
5. إيقاف RippleGrid افتراضيًا على التسجيل.
6. debounce + server pagination لبحث الطلاب.
7. إصلاح student loading المزدوج وsame-origin redirect.

**توقع:** تحسين ملموس في LCP/INP دون refactor شامل.

## الأيام 8-30 - بنية التنقل والبيانات

1. تثبيت shells في layouts.
2. إزالة root template العام.
3. نقل GlobalNav إلى public group.
4. إدخال QueryClient موحد.
5. server-prefetch للـstudent dashboard.
6. تقسيم أكبر 5 Client pages حسب tabs.
7. توحيد route permission policy.

## الأيام 31-60 - backend والـrealtime

1. إزالة N+1 من Live Support.
2. cache أمنية قصيرة لمسار token validation مع invalidation صحيح.
3. إعادة تصميم outbox claim/dispatch/ack.
4. workflow load tests وWebSocket load.
5. query-count وpayload budgets في CI.

## الأيام 61-90 - جودة مستدامة

1. reduced-motion مركزي ومراجعة كل carousel.
2. token migration تدريجية.
3. Playwright + axe على المسارات الحرجة.
4. budgets مضغوطة لكل route.
5. SLOs وتنبيهات production.

## 8. مؤشرات النجاح المطلوبة

| المؤشر | الهدف |
|---|---:|
| LCP p75 mobile | أقل من 2.0s |
| INP p75 mobile | أقل من 200ms |
| CLS p75 | أقل من 0.1 |
| warm client navigation p75 | أقل من 300ms |
| login to dashboard p75 | أقل من 1.5s بعد نجاح API |
| API read p95 المعتاد | أقل من 250ms |
| database-heavy workflow p95 | أقل من 500ms |
| JS initial route reduction | 25-40% للمسارات الثلاثة الأولى |
| duplicate GETs during normal navigation | صفر |
| accessibility critical violations | صفر |
| slow request/SQL visibility | 100% مع route + correlation id |

## 9. ترتيب التنفيذ النهائي

1. القياس المفصول حسب المسار والجهاز.
2. shell/layout/remount.
3. prefetch + query cache.
4. register WebGL وheavy motion.
5. student dashboard/auth transition.
6. large lists وserver pagination.
7. static caching والشعار.
8. large Client page splitting.
9. Live Support N+1.
10. reduced-motion/accessibility/tokens.

القاعدة الحاكمة: كل تحسين يجب أن يملك **قياس قبل وبعد**. لا نعتمد على الإحساس فقط، ولا نعتبر نجاح build أو اختبار health endpoint دليلًا كافيًا على سرعة تجربة المستخدم.

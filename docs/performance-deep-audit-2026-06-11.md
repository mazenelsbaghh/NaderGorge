# Performance Deep Audit - 2026-06-11

النطاق: مراجعة بطء الويب سايت عموما، حتى على localhost، مع مراعاة تعديلات surfaces والدومينات الأخيرة.

الحالة: **Needs Performance Remediation**.

تم حذف ملفات تقارير التوسع القديمة بناء على الطلب:

- `platform_expansion_plan.md`
- `platform_expansion_action_plan_2026-06-09.md`
- `platform_expansion_gap_report_2026-06-09.md`
- `platform_expansion_remaining_access_surface_report_2026-06-09.md`

## ملخص تنفيذي

البطء الحالي ليس من سبب واحد. المشكلة موزعة بين:

- Next.js rendering: أغلب الصفحات Dynamic وClient Components.
- JavaScript bundle/runtime: تحميل animation/libs على نطاق واسع.
- API waterfall: صفحات الطالب والـ shells تعمل طلبات كثيرة ومتكررة.
- Backend EF queries: استعلامات تسحب object graphs كبيرة وتعمل حسابات في الذاكرة.
- Static assets: ملفات logo/svg كبيرة جدا.
- Cache/compression: لا توجد قواعد كافية للضغط والـ output caching.
- Docker/surface isolation: الخدمات منفصلة runtime لكن صور الواجهة لا تزال مشتركة.

## نتائج القياس السريع

الأوامر التي اشتغلت:

```bash
cd frontend && npm run build
node scripts/verify-surface-separation.mjs --static-only
node scripts/generate-endpoint-inventory.mjs --check
```

النتيجة:

- `npm run build`: نجح.
- Next build أظهر أن كل routes تقريبا Dynamic (`ƒ`) وليس Static.
- `verify-surface-separation --static-only`: نجح.
- endpoint inventory متزامن: 234 backend endpoints و228 frontend calls.
- `frontend/.next`: حوالي 2.1GB، ومعظمه Turbopack dev cache.
- أكبر public assets:
  - `frontend/public/images/logo.svg`: حوالي 3.2MB.
  - `frontend/public/images/logo-mark.svg`: حوالي 3.2MB.
  - `frontend/public/images/logo-mark-light.svg`: حوالي 3.2MB.
  - `frontend/public/images/landing-hero.png`: حوالي 1.4MB.
  - `frontend/public/images/landing-hero-dark.png`: حوالي 1.4MB.

## P0 - مشاكل لازم تتصلح أولا

### 1. Root layout يجعل كل التطبيق Dynamic

الملف: `frontend/src/app/layout.tsx`

المشكلة:

- يوجد `export const dynamic = 'force-dynamic';`.
- يوجد `headers()` داخل RootLayout لاستخراج host/surface.
- نتيجة build: كل صفحات التطبيق تقريبا dynamic server-rendered on demand.
- هذا يمنع static rendering والـ caching للصفحات العامة، ويزود TTFB حتى محليا.

المطلوب:

- إزالة `force-dynamic` من root layout.
- نقل surface detection إلى `frontend/src/proxy.ts`/middleware أو header injected من proxy.
- فصل البيانات الديناميكية في layouts داخلية فقط، وليس root layout.
- الصفحات العامة مثل `/`, `/about`, `/faq`, `/login`, `/register` يجب أن تكون static أو cached قدر الإمكان.

قاعدة مطلوبة:

- ممنوع إضافة `force-dynamic` على root layout.
- أي صفحة تحتاج dynamic يجب أن تعلن ذلك محليا وبسبب واضح.

### 2. معظم صفحات App Router هي Client Components

المشكلة:

- 82 من 85 ملف `page.tsx` يحتوي `use client`.
- هذا يحول صفحات كاملة إلى client-rendered bundles.
- المستخدم يدفع تكلفة JS/hydration حتى في صفحات قراءة أو dashboard يمكن SSR/Server Component.

الأثر:

- بطء أول تحميل.
- CPU أعلى في المتصفح.
- تفاعل أبطأ على أجهزة ضعيفة.
- bundle chunks أكثر.

المطلوب:

- تحويل الصفحات إلى Server Components افتراضيا.
- نقل التفاعل إلى components صغيرة client-only.
- أمثلة مرشحة:
  - صفحات teacher read dashboards.
  - صفحات admin list/detail التي يمكن جلب بياناتها server-side.
  - صفحات student packages/teachers/notifications.
  - صفحات public forms إن لم تكن كلها تفاعلية.

قاعدة مطلوبة:

- `page.tsx` لا يستخدم `use client` إلا لو الصفحة نفسها تحتاج hooks/browser APIs.
- الفورمز، modals، charts، scanners، video player تبقى client components منفصلة.

### 3. `app/template.tsx` يضيف framer-motion لكل navigation

الملف: `frontend/src/app/template.tsx`

المشكلة:

- الملف كله client component.
- يستورد `framer-motion`.
- يلف كل الصفحة في `motion.div`.
- هذا يضيف animation runtime لمسارات لا تحتاجه.

المطلوب:

- حذف template العام أو استبداله بـ CSS transition خفيف.
- إن كانت الحركة مطلوبة، تطبق داخل surfaces محددة وليس عالميا.

قاعدة مطلوبة:

- لا يوجد animation wrapper global على كل routes.
- `framer-motion` يستخدم فقط في components تحتاجه فعلا.

### 4. Student shell يعمل API waterfall ومتكرر

الملفات:

- `frontend/src/components/layout/StudentShellChrome.tsx`
- `frontend/src/components/layout/SidebarBalance.tsx`
- `frontend/src/components/layout/SidebarGamification.tsx`
- `frontend/src/app/student/page.tsx`

المشاكل:

- `StudentShellChrome` يجلب notifications count عند mount.
- نفس shell يجلب notifications count مرة أخرى عند كل route change.
- `SidebarBalance` يجلب balance منفصلا.
- `SidebarGamification` يجلب gamification منفصلا.
- `student/page.tsx` يجلب dashboard وquick-access.
- في أول دخول للطالب يمكن أن يحدث 4-5 API requests قبل اكتمال تجربة الصفحة.

المطلوب:

- endpoint واحد للـ student shell bootstrap:
  - unreadNotificationsCount
  - balance
  - gamification summary
  - theme/avatar basics
- cache client-side لمدة قصيرة، مثلا 30-60 ثانية.
- إعادة الجلب فقط عند event محدد: purchase/code activation/notification read.
- عدم إعادة notifications fetch على كل pathname change.

قاعدة مطلوبة:

- shell component لا يعمل أكثر من request واحد عند mount.
- navigation لا يعيد تحميل بيانات shell إلا بسبب invalidation event.

## P1 - Backend/API Performance

### 5. Student dashboard query تسحب شجرة كبيرة وفيها N+1

الملف: `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs`

المشاكل:

- `Include(p => p.Terms).ThenInclude(t => t.Sections).ThenInclude(s => s.Lessons)` يسحب كل الدروس للباقة في الذاكرة.
- الحسابات تتم في C# بدل SQL projections/aggregates.
- upcoming exams يعمل loop ثم `FirstOrDefaultAsync` لكل exam.

المطلوب:

- استخدام `AsNoTracking`.
- استخدام projections مباشرة إلى DTO.
- حساب counts في SQL.
- جلب exams المطلوبة دفعة واحدة.
- تجنب تحميل كل lessons إذا المطلوب count/resume point فقط.

قاعدة مطلوبة:

- read queries تستخدم `AsNoTracking` افتراضيا.
- ممنوع N+1 query داخل loops.
- ممنوع `Include` عميق إذا الهدف count/summary فقط.

### 6. Progress/Mistakes/Lesson detail queries تحتاج تبسيط

ملفات مهمة:

- `backend/src/NaderGorge.Application/Features/Student/Queries/GetProgressQuery.cs`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetMistakesQuery.cs`
- `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs`

المشاكل:

- `GetProgressQuery` يسحب packages/terms/sections/lessons كاملة.
- `GetMistakesQuery` يسحب attempts/exams/questions/options وقد يرجع payload كبير.
- `GetLessonDetailQuery` يفحص access والـ teacher auth ثم يسحب lesson/videos/chapters/resources/homework، ويعمل استعلامات إضافية للقفل والسابق.

المطلوب:

- pagination للـ mistakes.
- projection للـ progress بدل graph include.
- في lesson detail: split payload إلى:
  - lesson summary.
  - videos/chapters.
  - homework.
  - comments.
- lazy/deferred load للأجزاء غير الظاهرة في أول viewport.

قاعدة مطلوبة:

- أي endpoint يرجع list قابلة للنمو لازم pagination أو limit واضح.
- أي endpoint payload يتجاوز ~100KB يحتاج split أو compression أو justification.

### 7. Admin code groups تسحب كل الأكواد للعد فقط

الملف: `backend/src/NaderGorge.Application/Features/Admin/Queries/ListCodeGroupsQuery.cs`

المشكلة:

- `Include(cg => cg.AccessCodes)` ثم `Count` و`Count(c => c.IsConsumed)`.
- مع مجموعات أكواد كبيرة، هذا سيحمل آلاف أو عشرات آلاف السجلات بلا داع.

المطلوب:

- projection:
  - `CodeCount = cg.AccessCodes.Count()`
  - `UsedCount = cg.AccessCodes.Count(c => c.IsConsumed)`
- pagination لقائمة code groups إن لم تكن موجودة.

قاعدة مطلوبة:

- لا يتم تحميل collection فقط من أجل count.

### 8. API لا يستخدم response compression/output cache

الملف: `backend/src/NaderGorge.API/Program.cs`

المشكلة:

- يوجد `AddMemoryCache`.
- لا يوجد `AddResponseCompression`/`UseResponseCompression`.
- لا يوجد `AddOutputCache`/`UseOutputCache`.

المطلوب:

- تفعيل Brotli/Gzip responses.
- إضافة OutputCache للـ public endpoints:
  - `/api/public/stats`
  - `/api/public/teachers`
  - `/api/public/settings`
  - public forms metadata حسب الحاجة.
- إضافة cache داخلي لبيانات platform settings والـ public teacher list.

قاعدة مطلوبة:

- public GET endpoints يجب أن تملك cache policy أو سبب واضح لعدم الكاش.
- authenticated GET summary endpoints يمكن caching قصير per-user عند الحاجة.

## P1 - Frontend Bundle/Runtime

### 9. مكتبات ثقيلة مستخدمة على نطاق واسع

ملاحظات:

- `framer-motion` منتشر في صفحات ومكونات كثيرة.
- يوجد dependency مزدوج: `framer-motion` و`motion`.
- توجد مكتبات heavy مثل `three`, `ogl`, `gsap`, `react-quill-new`, QR scanner.

المطلوب:

- `three`/`ogl`/`gsap` تبقى dynamic import داخل landing/visual components فقط.
- `react-quill-new` dynamic import موجود في `QuestionEditor`، يجب التأكد أنه لا يدخل admin shell العام.
- QR scanner dynamic import عند فتح scanner فقط.
- إزالة dependency غير مستخدمة أو مكررة: راجع هل `motion` ضرورية بجانب `framer-motion`.

قاعدة مطلوبة:

- أي dependency visual/heavy لا تدخل shared layout أو root.
- أي component يستخدم camera/editor/canvas/video يجب dynamic import أو route-level split.

### 10. Logos وSVG assets ضخمة

المشكلة:

- SVG logo files حوالي 3.2MB لكل ملف.
- `icon.svg` في build خرج حوالي 3.2MB أيضا.

المطلوب:

- تنظيف SVG من embedded raster/base64 أو metadata.
- إنشاء favicon/icon حقيقي صغير.
- استخدام optimized SVG/AVIF/WebP حسب نوع الأصل.
- تحديد dimensions واستخدام cache headers طويلة للـ immutable assets.

قاعدة مطلوبة:

- logo SVG يجب أن يكون غالبا أقل من 50KB.
- favicon/icon يجب أن يكون أقل من 100KB إلا لو فيه سبب قوي.
- صور hero يجب أن تكون responsive وبصيغ حديثة.

### 11. Fonts كثيرة الأوزان

الملف: `frontend/src/app/layout.tsx`

المشكلة:

- Tajawal يحمل 6 أوزان.
- Montserrat يحمل 7 أوزان.
- تحميل أوزان كثيرة يزود font CSS وfont files.

المطلوب:

- تقليل الأوزان إلى المستخدمة فعليا:
  - Tajawal: 400, 500, 700, 800 مثلا.
  - Montserrat: 500, 700 أو إزالته إذا غير ضروري.
- راجع هل Montserrat محتاج في Arabic-first UI.

قاعدة مطلوبة:

- لا تضف font weight جديد إلا لو مستخدم في design token واضح.

## P2 - Routing, Rules, Surfaces

المقصود هنا بالـ routes/rules: قواعد routing والعزل والـ caching والأداء التي يجب تثبيتها حتى لا ترجع المشاكل.

### 12. Route rules مطلوبة

الحالي:

- `frontend/src/packages/surface-runtime/config.ts` يعيد routes الخطأ إلى `/not-found` داخل non-landing surfaces، وهذا جيد.
- `scripts/verify-surface-separation.mjs --static-only` نجح.

المطلوب إضافته:

- E2E لكل surface:
  - student domain لا يفتح `/admin`, `/teacher`, `/assistant`.
  - teacher domain لا يفتح `/admin`, `/student`, `/assistant`.
  - staff domain لا يفتح `/admin`, `/student`, `/teacher`.
  - admin domain لا يفتح `/student`, `/teacher`, `/assistant`.
- التأكد من response:
  - status/HTML marker واضح.
  - `x-massar-surface` header صحيح.
- اختبار login route لكل surface:
  - غير المسجل يذهب إلى `/login` داخل نفس surface.
  - المسجل يذهب إلى dashboard الصحيح.

قواعد routing مطلوبة:

- landing فقط مسموح لها تعمل redirect بين surfaces.
- non-landing surfaces تعرض not-found/forbidden عند route خارج surface.
- لا يتم redirect صامت إلى surface ثاني من داخل student/teacher/staff/admin.
- `returnUrl` يجب أن يكون relative ومطابق للـ active surface.

### 13. Docker image rules مطلوبة

الحالي:

- landing/student/admin/teacher/assistant كلهم يستخدمون `massar_frontend:local`.
- هذا يحقق runtime isolation عن طريق `APP_SURFACE` فقط.
- لا يحقق image-level isolation المذكور في خطة التوسع.

الاختيار المطلوب:

1. تنفيذ صور منفصلة:
   - `massar_landing_frontend:local`
   - `massar_student_frontend:local`
   - `massar_admin_frontend:local`
   - `massar_teacher_frontend:local`
   - `massar_assistant_frontend:local`

2. أو توثيق رسمي أن المشروع يعتمد runtime isolation:
   - نفس image.
   - APP_SURFACE mandatory.
   - tests تثبت أن route leakage غير ممكن.

قاعدة مطلوبة:

- إن بقيت image مشتركة، يجب إضافة test يفشل لو service missing `APP_SURFACE`.
- يجب منع تشغيل frontend production بدون `APP_SURFACE`.

### 14. Cache rules مطلوبة

Frontend:

- Public pages: static أو ISR قدر الإمكان.
- Student shell data: short client cache + invalidation events.
- Admin lists: pagination + cache disabled أو stale short حسب workflow.
- Video/session endpoints: `no-store` مقبول لأسباب أمنية.

Backend:

- Public GET: OutputCache.
- Authenticated read summaries: per-user short cache عند الحاجة.
- Mutations: invalidate relevant cache.
- Video/session/security endpoints: no cache.

Nginx:

- `_next/static/*`: immutable cache.
- images optimized assets: long cache.
- HTML/app routes: no aggressive cache إلا للـ public static.
- API: لا cache عام إلا لو backend يرسل headers واضحة.

### 15. Performance budget rules مطلوبة

أضف budget في CI أو سكريبت محلي:

- build must keep public route static unless marked dynamic.
- max initial JS per route: يحدد بعد bundle analyzer baseline.
- max public image/logo size:
  - logo SVG: 50KB.
  - favicon/icon: 100KB.
  - hero image: 300KB-500KB لكل variant بعد الضغط.
- max API response time local seeded:
  - `/student/dashboard`: أقل من 250ms بعد warmup.
  - `/content/lessons/{id}`: أقل من 300ms بعد warmup.
  - `/admin/code-groups`: أقل من 250ms مع آلاف الأكواد.
- max API payload:
  - dashboard summaries أقل من 100KB.
  - lists paginated.

## P2 - Dev/Local Performance

### 16. `.next` cache ضخم

المشكلة:

- `frontend/.next` حوالي 2.1GB.
- أغلب الحجم من `.next/dev/cache/turbopack`.
- هذا يمكن يبطئ local dev وfile watching.

المطلوب:

- تنظيف دوري عند بطء dev:

```bash
rm -rf frontend/.next
cd frontend && npm run build
```

- عدم إدخال `.next` في أي scanning أو backup.

قاعدة مطلوبة:

- أي scripts تستخدم `rg`/find يجب أن تستثني `.next`, `node_modules`, `dist`, `bin`, `obj`.

### 17. Source maps وdev artifacts

ملاحظات:

- `worker/src/*.d.ts.map` موجودة داخل `src`.
- هذا ليس سبب بطء مباشر للموقع، لكنه يزود ضوضاء repository ويؤثر على scans.

المطلوب:

- راجع `worker/tsconfig.json` output.
- generated artifacts يجب أن تخرج إلى `dist` لا `src`.

## خطة تنفيذ مقترحة

### Phase A - Quick Wins

الهدف: تقليل بطء الإحساس الأول بدون تغييرات كبيرة.

- إزالة global `app/template.tsx` أو تقليلها.
- ضغط/استبدال logos الضخمة.
- تقليل font weights.
- تنظيف `.next`.
- منع `StudentShellChrome` من إعادة notifications fetch عند كل route change.
- إضافة endpoint أو service method موحد لـ shell summary إن أمكن بسرعة.

التحقق:

```bash
cd frontend && npm run build
find frontend/public/images -maxdepth 2 -type f -exec du -h {} +
```

### Phase B - Rendering Fix

الهدف: استعادة Static/Server Components.

- إزالة `force-dynamic` من root layout.
- نقل host/surface logic من root layout.
- تحويل public pages إلى static/server.
- تحويل صفحات student read-heavy إلى server page + client islands.

التحقق:

```bash
cd frontend && npm run build
```

المطلوب من جدول build:

- `/`, `/about`, `/faq`, `/login`, `/register` لا تكون dynamic بلا سبب.
- routes الداخلية التي تحتاج auth قد تبقى dynamic أو client بشكل مضبوط.

### Phase C - API/DB Queries

الهدف: تقليل latency والـ payload.

- إصلاح `GetDashboardQuery`.
- إصلاح `ListCodeGroupsQuery`.
- مراجعة `GetProgressQuery`, `GetMistakesQuery`, `GetLessonDetailQuery`.
- إضافة `AsNoTracking` وprojections.
- إضافة response compression.
- إضافة output cache للـ public endpoints.

التحقق:

```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-build
python3 -m pytest -q
```

### Phase D - Rules/CI Guards

الهدف: منع رجوع نفس المشاكل.

- Bundle analyzer report في CI أو command.
- Performance budget script.
- Static route assertion.
- Surface E2E for forbidden routes.
- Endpoint inventory يبقى required.
- Docker surface config check يراجع image/runtime decision.

التحقق:

```bash
node scripts/generate-endpoint-inventory.mjs --check
node scripts/verify-surface-separation.mjs --static-only
cd frontend && npm run test:e2e -- --project=chromium
docker compose config -q
```

## ما هو ناقص حاليا

- لا يوجد Lighthouse/Core Web Vitals baseline.
- لا يوجد bundle analyzer baseline.
- لا يوجد API timing benchmark للـ seeded data.
- لا يوجد DB slow query log أو `EXPLAIN ANALYZE` للـ endpoints الثقيلة.
- لا يوجد performance budget في CI.
- لا يوجد route static/dynamic budget.
- لا يوجد قرار موثق بخصوص shared frontend image vs separate images.
- لا توجد cache matrix رسمية لكل endpoint.

## القرار المطلوب من المنتج/الفريق

1. هل نريد صور Docker منفصلة فعلا لكل surface، أم نعتمد runtime isolation؟
2. هل pages العامة يجب أن تكون static بالكامل؟
3. ما الحد المقبول لأول تحميل على أجهزة الطلاب الضعيفة؟
4. هل dashboard يحتاج كل البيانات مرة واحدة، أم يمكن تحميل sections تدريجيا؟
5. هل animations الحالية جزء من الهوية، أم يمكن تقليلها بشكل كبير لتحسين السرعة؟

## قائمة مختصرة بالملفات ذات الأولوية

Frontend:

- `frontend/src/app/layout.tsx`
- `frontend/src/app/template.tsx`
- `frontend/src/components/layout/StudentShellChrome.tsx`
- `frontend/src/components/layout/SidebarBalance.tsx`
- `frontend/src/components/layout/SidebarGamification.tsx`
- `frontend/src/app/student/page.tsx`
- `frontend/src/packages/surface-runtime/config.ts`
- `frontend/next.config.ts`
- `frontend/public/images/*`

Backend:

- `backend/src/NaderGorge.API/Program.cs`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetDashboardQuery.cs`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetProgressQuery.cs`
- `backend/src/NaderGorge.Application/Features/Student/Queries/GetMistakesQuery.cs`
- `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Queries/ListCodeGroupsQuery.cs`
- `backend/src/NaderGorge.Application/Features/Admin/Queries/GetCodeGroupCodesQuery.cs`

Ops:

- `docker-compose.yml`
- `docker/nginx/massar.conf`
- `scripts/verify-surface-separation.mjs`
- `scripts/generate-endpoint-inventory.mjs`


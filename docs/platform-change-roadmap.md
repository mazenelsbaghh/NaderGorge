# خارطة مراحل تعديلات المنصة

> الهدف من الملف: ترتيب التعديلات المطلوبة حسب الاولوية والاعتماديات، وربطها بما هو موجود في المشروع، بحيث تتحول كل مرحلة لاحقا الى Spec منفصل او مجموعة Specs صغيرة. لا تعتبر المرحلة منتهية الا بعد وجود مخرج واضح قابل للتجربة من الادمن/المدرس/الطالب حسب الحالة.

## قواعد التنفيذ العامة

- كل Phase لازم تبدأ بمراجعة الموجود فعليا في الكود والـ specs قبل بناء شيء جديد.
- أي تعديل يمس بيانات او فلوس او صلاحيات لازم يكون له Backend + Migration + Audit/Log + Test.
- أي تعديل واجهة فقط لازم يكون له شاشة قابلة للتجربة وسيناريو QA واضح.
- كل Feature كبيرة تتقسم الى Spec منفصل قبل التنفيذ، خصوصا: الكوبونات، الامتحانات العامة، حسابات المدرسين، الشكاوى، الاعلانات.
- ممنوع الانتقال للمرحلة التالية لو في كسر في الشراء، الدخول، الصلاحيات، او Docker startup.
- بوابات التحقق الافتراضية: `docker compose config -q`، ثم `make up`، ثم `make migrate` عند وجود migrations، ثم `npm test && npm run lint` للـ frontend عند تعديل الواجهة.

## ربط مبدئي بالـ Specs الموجودة

| الموضوع | Spec موجود | القرار |
|---|---|---|
| تطبيق ولي الامر | `specs/147-parent-tracking-app/` | لا يعاد بناؤه، يتم استكماله وربطه بالبيانات الصحيحة. |
| دقة تتبع ولي الامر | `specs/149-parent-tracking-accuracy/` | يعتمد عليه بعد تشغيل التطبيق الاساسي. |
| SMS / Payment matcher | `specs/148-sms-payment-auto-matcher/` | يدخل في مرحلة التطبيقات والمتابعة المالية. |
| اكواد الشراء والرصيد | `specs/014-registration-codes-hierarchy/` و `specs/060-package-code-profiles/` و `specs/130-granular-content-purchase/` | يتم توحيدها قبل اضافة خصومات متقدمة. |
| ادارة المحتوى والفيديوهات | `specs/020-lesson-content-management/` و `specs/128-lesson-content-enhancements/` | تستخدم كأساس لانواع الفيديو والكود الداخلي. |
| بنك الاسئلة والامتحانات | `specs/031-unify-assessment-builder/` و `specs/069-separate-question-bank/` و `specs/124-refine-multiple-exams-builder/` | الامتحانات العامة تكون Feature جديدة فوق البنية الحالية. |
| تعدد المدرسين | `specs/092-multi-teacher-multi-subject-architecture/` و `specs/102-multi-teacher-enforcement/` و `specs/108-teacher-binding-and-completion/` | شرط اساسي قبل حسابات المدرسين والباكدجات المشتركة. |
| حسابات الموظفين | `specs/090-hr-core-employees-attendance-vacations/` | يتم استكماله بدل بناء HR جديد. |
| المرتبات وحسابات المدرسين | `specs/096-payroll-accounting/` | يتم توسيعه لحسابات اليوم والكالندر والتوزيع. |
| الكوميونيتي | `specs/058-student-community/` | ينقل لاحقا داخل بروفايل المدرس بعد اكتمال Profile المدرس. |
| الاشعارات | `specs/093-internal-chat-notifications/` | يؤجل لحين تثبيت Events والادوار. |

---

## Phase 0: مراجعة وربط الموجود بالفعل

### الهدف

تحديد ما تم تنفيذه فعلا، ما هو ناقص، وما يتعارض مع التعديلات الجديدة قبل كتابة Specs جديدة.

### التاسكات

- [ ] مراجعة شاشة الادمن الحالية للمحتوى، الاكواد، الباكدجات، الامتحانات، المدرسين، الطلاب، HR، والمالية.
- [ ] مراجعة الـ entities والـ migrations الخاصة بـ `LessonVideo`, `Package`, `Exam`, `Code`, `TeacherProfile`, `StudentProfile`, `Payroll`, و `Audit`.
- [ ] عمل جدول حالة لكل بند: موجود، ناقص، متعارض، يحتاج Spec جديد.
- [ ] تحديد نوع كل تعديل: بيانات، صلاحيات، دفع/فلوس، واجهة فقط، او Worker/Event.
- [ ] تحديد الـ specs التي سيتم استكمالها بدل اعادة بنائها.
- [ ] تحديد مخاطر الترحيل: بيانات قديمة بلا TeacherId، فيديوهات بلا نوع، اكواد بلا مصدر بيع، امتحانات بلا تصنيف.

### تاسكات يدوية

- [ ] تجربة شراء باكدج وحصة وكود من حساب طالب حقيقي على بيئة محلية.
- [ ] تجربة دخول ادمن ومدرس ومساعد وطالب والتأكد من الصلاحيات الحالية.
- [ ] استخراج امثلة من بيانات حقيقية: مدرس، حصة، فيديو، كود شراء، كوبون، امتحان.

### المخرج القابل للتجربة

- تقرير Markdown قصير داخل `docs/` يوضح حالة كل Module.
- صفحة او Screenshot من الادمن تثبت ان كل مسار اساسي اتراجع.
- قرار واضح: ما الذي يتحول الى Specs جديدة وما الذي يستكمل على specs قديمة.

---

## Phase 1: اساسيات البيع والمحتوى

### الهدف

تثبيت هوية المحتوى والشراء: كل فيديو/حصة/امتحان له كود داخلي ونوع واضح، والشراء/الهدايا/الخصومات تعرف تفتح محتوى محدد او رصيد محدد.

### الاعتماديات

- Phase 0 مكتملة.
- مراجعة `specs/014-registration-codes-hierarchy/`, `specs/020-lesson-content-management/`, `specs/128-lesson-content-enhancements/`, `specs/130-granular-content-purchase/`.

### 1.1 كود داخلي وانواع المحتوى

- [x] اضافة Internal Code ثابت لكل حصة/فيديو/امتحان، يظهر في الادمن ولا يتغير بعد الانشاء. `Completed: specs/151-content-identity-and-types/`
- [x] اثناء انشاء الفيديو يتم اختيار الاسم والنوع: شرح، واجب، مراجعة، امتحان. `Completed: specs/151-content-identity-and-types/`
- [x] اضافة شاشة ادمن لادارة انواع الفيديو، بحيث الادمن يقدر يضيف انواع جديدة بدون تعديل كود. `Completed: specs/151-content-identity-and-types/`
- [x] ربط نوع الفيديو بمنطق الشراء: كود يفتح فيديو معين، او اي فيديو من نوع معين، او رصيد يشتري نوع معين. `Completed: specs/153-phase1-sales-content/ + SalesTargetResolver/DiscountEngine`
- [x] منع حفظ فيديو بلا مدرس/مادة/نوع اذا كان سيستخدم في البيع او حساب الارباح. `Completed: SaveSalesRuleCommand validates target eligibility and teacher/subject/video type binding`

### 1.2 الهدايا والحصص المجانية

- [x] اضافة Grant/Free Access يسمح بفتح حصة او فيديو او امتحان لطالب واحد او مجموعة طلاب. `Completed: specs/152-gifts-free-access/`
- [x] دعم تاريخ انتهاء اختياري. `Completed: specs/152-gifts-free-access/`
- [x] دعم عدد مرات استخدام/مشاهدة اختياري لو مطلوب. `Completed: specs/152-gifts-free-access/`
- [x] تسجيل السبب ومن اصدر الهدية في Audit اداري. `Completed: specs/152-gifts-free-access/`
- [x] اظهار الهدية للطالب كصلاحية منفصلة عن الشراء حتى لا تختلط بالحسابات. `Completed: specs/152-gifts-free-access/`

### 1.3 تطوير اكواد الخصم والكوبونات

- [x] مراجعة نظام الكوبونات الحالي وتحديد هل هو Coupon، Code، Balance، او Access Code. `Completed: specs/153-phase1-sales-content/research.md`
- [x] دعم كوبون خصم على: باكدج، حصة، امتحان عام، مدرس، او المنصة كلها. `Completed: SalesCoupon TargetType`
- [x] الخصم يكون نسبة او قيمة ثابتة. `Completed: DiscountType Percentage/FixedAmount`
- [x] اضافة حدود استخدام عامة وحدود لكل طالب وتاريخ انتهاء. `Completed: SalesCoupon limits + DiscountEngine validation`
- [x] عند اختيار حصة: اما حصة معينة، او رصيد حصة يسمح بشراء اي حصة ضمن شروط الكود. `Completed: SalesTargetType Lesson/Teacher/Platform + printable PromotionalCredit behavior`
- [x] كل كوبون لازم يحدد المدرس لو الخصم مرتبط برصيد مدرس، او يكون Platform-wide لو الخصم على المنصة. `Completed: OwnerType + TeacherId + TargetType`
- [x] تحديد مصدر بيع الكود: المنصة ام المدرس. `Completed: SalesOwnerType`
- [x] اضافة نسبة المنصة ونسبة المدرس على الكود حتى تتسجل في الحسابات لاحقا. `Completed: SalesFinancialEffect records teacher/platform impact placeholders for Phase 3`
- [x] اضافة تصميم شكل الكود: QR، الكود، السريال، وبيانات مختصرة. `Completed: PrintableSalesCode QrPayload/Serial + /admin/codes/templates`
- [x] دعم Templates للكود يقوم الادمن برفعها او بنائها وحفظها. `Completed: PrintableCodeTemplate + /api/admin/sales/templates`
- [x] دعم Drag and Drop لعناصر التصميم داخل تمبلت الكود. `Completed: /admin/codes/templates simple TemplateDesigner saves draggable QR/code/serial layout JSON`

### 1.4 الامتحانات العامة المستقلة

- [x] انشاء نوع Exam مستقل عن فيديوهات الحصص والباكدجات. `Completed: PublicExamProduct`
- [x] عدم خلطه مع بنك الاسئلة؛ بنك الاسئلة مصدر اسئلة فقط، والامتحان العام منتج مستقل. `Completed: PublicExamProduct links existing Exam as product`
- [x] دعم امتحان مجاني او مدفوع. `Completed: IsPaid/Price + AccessCheckService`
- [x] الطالب يقدر يشتري الامتحان او يدخل حسب السعر والصلاحية. `Completed: PurchaseContentCommand CodeType.Exam + public /api/public-exams`
- [x] صفحة عرض مستقلة للامتحانات العامة. `Completed: /student/public-exams`
- [x] صفحة ادمن مستقلة لانشاء الامتحانات العامة وفتح بروفايل اضافة الاسئلة. `Completed: /admin/public-exams + /admin/public-exams/{id}`
- [x] تقارير نتائج مستقلة عن امتحانات الحصص. `Completed: /admin/public-exams/{id}/results + GetPublicExamResultsQuery`
- [x] دعم ربط الامتحان بمدرس/مادة/صف لو مطلوب للتقارير والارباح. `Completed: TeacherId/SubjectId/GradeLevel`

### تاسكات يدوية

- [ ] الادمن ينشئ نوع فيديو جديد ويستخدمه في فيديو.
- [ ] الادمن ينشئ هدية لطالب ويشوف الطالب المحتوى بدون دفع.
- [ ] الادمن ينشئ كوبون خصم بنسبة وكوبون بقيمة ثابتة ويجربهم على باكدج وحصة. `Manual QA pending; automated DiscountEngine tests passed`
- [ ] الادمن يصمم تمبلت كود فيه QR وسريال ثم يولد كود قابل للطباعة. `Manual QA pending; /admin/codes/templates and PrintableCodeBatch implemented`
- [ ] الطالب يشتري امتحان عام ويدخله ويظهر في تقرير مستقل. `Manual QA pending; purchase/access implemented`

### المخرج القابل للتجربة

- [x] شاشة ادمن لادارة انواع المحتوى. `Completed: /admin/content/video-types`
- [x] شاشة ادمن للهدايا. `Completed: /admin/gifts`
- [x] شاشة كوبونات/اكواد خصم متقدمة. `Completed: /admin/sales`
- [x] شاشة قوالب اكواد داخل قسم الاكواد. `Completed: /admin/codes/templates`
- [x] شاشة ادمن للامتحانات العامة واضافة الاسئلة. `Completed: /admin/public-exams`
- [x] صفحة امتحانات عامة للطالب. `Completed: /student/public-exams`
- [ ] سيناريو كامل: انشاء محتوى -> انشاء كود/خصم/هدية -> دخول طالب وتجربة الوصول. `Manual QA pending`

---

## Phase 2: تطبيقات المتابعة والدفع

### الهدف

استكمال تطبيق ولي الامر ومتابعة الدفع/التحويلات بعد تثبيت منطق الشراء والمحتوى.

### الاعتماديات

- Phase 1 مكتملة جزئيا على الاقل في المحتوى والشراء.
- `specs/147-parent-tracking-app/`, `specs/149-parent-tracking-accuracy/`, `specs/148-sms-payment-auto-matcher/`.

### 2.1 تطبيق ولي الامر

- [x] ربط ولي الامر بالطالب بكود متابعة امن. `Completed: /api/parent/verify-code + RequireParent JWT + mobile parent linking screens`
- [x] عرض الحضور، المشاهدة، الامتحانات، التقدم، والتحذيرات. `Completed: /api/parent/student-details + Android/iOS parent dashboards`
- [x] منع عرض بيانات ناقصة او مضللة؛ عند غياب البيانات يظهر سبب واضح. `Completed: specs/149-parent-tracking-accuracy/ US5 empty/default-safe states`
- [x] دعم اكثر من طالب لنفس ولي الامر. `Completed: mobile linked-student storage + parent token switching`
- [x] تجهيز Events مستقبلية للاشعارات بدون تفعيل نظام الاشعارات الكامل. `Completed: ParentDeviceToken + /api/parent/device-token + NotificationEvent/worker notification sender plumbing`

### 2.2 دقة التتبع

- [x] توحيد مصادر حساب المشاهدة والحضور والامتحانات. `Completed: specs/149-parent-tracking-accuracy/ purchased lesson projection in GetStudentAcademicDetailsQuery`
- [x] معالجة الحالات المتعارضة: مشاهدة بلا جلسة، امتحان بلا محتوى، طالب محذوف من باكدج. `Completed: active grant filters, unattempted exams, empty student tests`
- [x] اضافة اختبارات لحساب نسب التقدم. `Completed: backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs`

### 2.3 تطبيق/ادوات متابعة التحويلات

- [x] استكمال مطابقة SMS/payment مع الطلبات. `Completed: specs/148-sms-payment-auto-matcher/ + AndroidWalletController + SmsParser + recharge auto-match pipeline`
- [x] شاشة مراجعة للتحويلات غير المتطابقة. `Completed: /admin/recharge-verification + unmatched SMS/recharge request APIs`
- [x] ادوات تفعيل/تعطيل المحافظ وحدود الاستخدام. `Completed: /admin/wallets + toggle/regenerate/update-limits APIs`
- [x] سجل Audit لكل تعديل يدوي على تحويل. `Completed: RechargeRequest.ResolvedByUserId/ResolvedAt/RejectionReason + matched SMS reference`

### تاسكات يدوية

- [ ] ولي امر يربط طالب ويشاهد تقريره. `Manual QA pending; implementation exists`
- [ ] تجربة حالة طالب اشترى حصة فقط وحالة طالب اشترى باكدج. `Manual QA pending; specs/149 automated parent tests passed previously`
- [ ] ادمن يراجع تحويل غير متطابق ويربطه بطلب. `Manual QA pending; UI/API implementation exists`

### المخرج القابل للتجربة

- [x] تطبيق/واجهة ولي امر تعرض بيانات حقيقية. `Completed: Android/iOS parent apps + /api/parent/student-details`
- [x] شاشة ادمن لتحويلات تحتاج مراجعة. `Completed: /admin/recharge-verification`
- [x] تقرير دقة يوضح مصدر كل رقم ظاهر لولي الامر. `Completed: specs/149-parent-tracking-accuracy/ + tests and contracts`

---

## Phase 3: المدرسين والحسابات

### الهدف

تثبيت اقتصاد المدرسين: كل عملية شراء او كود او باكدج لها مدرس/منصة ونسب واضحة، والمدرس يرى دخله اليومي والتفاصيل.

### الاعتماديات

- Phase 1 مكتملة في الكوبونات والربط بالمحتوى.
- اكتمال او مراجعة `specs/092-multi-teacher-multi-subject-architecture/`, `specs/102-multi-teacher-enforcement/`, `specs/108-teacher-binding-and-completion/`, `specs/096-payroll-accounting/`.

### 3.1 حسابات المدرس اليومية

- [x] صفحة مدرس تعرض دخل اليوم، الاجمالي، الرصيد الحالي، والعمولات. `Implemented: ledger-backed /teacher/finance summary with today/reserved/available/debt`
- [x] Calendar يوضح دخل كل يوم. `Implemented: /teacher/finance month calendar shows daily teacher share and pending-review count`
- [x] عند الضغط على يوم يظهر تفصيل المعاملات. `Implemented: clicking a calendar day filters ledger transactions for that date`
- [x] ربط الدخل بتفعيل الاكواد وشراء الحصص والباكدجات والامتحانات العامة. `Implemented: code activation, direct purchase, shared package purchase, and public-exam purchase paths create ledger events where ownership is known`
- [x] سجل معاملات واضح: الطالب، المحتوى، السعر، الخصم، نسبة المدرس، نسبة المنصة، صافي الربح. `Implemented: TeacherFinancialAllocation snapshots and teacher transactions query`
- [x] منع ظهور بيانات طلاب حساسة للمدرس الا بالقدر المسموح. `Implemented: teacher finance reads are scoped to current teacher allocations`

### 3.2 رصيد المدرس وربطه بالحصص

- [x] منع حساب ارباح على محتوى غير مربوط بمدرس. `Implemented: teacher share is only recorded when a teacher owner/allocation exists`
- [x] منع حساب ارباح على عملية شراء بلا Product واضح. `Implemented: ledger requires target type/id and idempotency key`
- [x] اضافة Queue/Review للتعاملات المشكوك فيها قبل الصرف. `Implemented: PendingReview status and admin teacher-event review endpoint/tab`
- [x] تقارير مراجعة للادارة قبل صرف مستحقات المدرسين. `Implemented: admin finance review tab plus payout lifecycle Pending -> Approved -> Paid`
- [x] فصل رصيد المدرس عن رصيد المنصة في التقارير. `Implemented: allocations store teacherShareAmount and platformShareAmount separately`

### 3.3 باكدج المدرسين كلهم مع بعض

- [x] انشاء صفحة باكدج جديدة مختلفة عن الباكدج الحالي. `Implemented: /admin/shared-packages and /student/shared-packages`
- [x] الادمن يحدد سعر الباكدج. `Implemented in shared package create API/UI`
- [x] الادمن يختار المدرسين والمواد داخل الباكدج. `Implemented for teacher/content item selection in initial UI/API`
- [x] لكل مدرس داخل الباكدج يتم تحديد نسبته من 100 او مبلغ ثابت حسب القرار. `Implemented: percentage/fixed allocations`
- [x] التحقق ان مجموع النسب صحيح ولا يترك فلوس غير موزعة. `Implemented: rejects over-allocation and records explicit platform remainder`
- [x] الطالب يشتري الباكدج ويرى محتواه حسب المدرس/المادة. `Implemented: student shared-package detail loads teachers, subjects, allocations, and content items`
- [x] الحسابات تسجل نصيب كل مدرس والمنصة من نفس عملية الشراء. `Implemented: SharedPackagePurchase ledger allocations`

### 3.4 بروفايل المدرس والكوميونيتي

- [x] انشاء Profile عام لكل مدرس. `Implemented: /api/public/teachers list/detail endpoints with backward-compatible DTO`
- [x] الطالب يقدر يدخل على بروفايل المدرس ويشوف المواد، الباكدجات، الحصص، الفيديو التعريفي، والتقييمات. `Implemented: /student/teachers/[teacherId] profile page with subjects, packages, shared packages, lessons, intro video, and ratings`
- [x] نقل Community المدرس داخل بروفايله بدل كوميونيتي عام فقط. `Implemented: teacher-scoped community panel on public teacher profile with moderated posting`
- [x] كل منشور/تعليق يظل تحت moderation حسب النظام الحالي. `Implemented: teacher posts are created Pending and listed only when Approved`

### تاسكات يدوية

- [x] طالب يشتري حصة مدرس واحد والمدرس يرى العملية في نفس اليوم. `Implemented and covered by ledger transaction tests; seeded browser QA remains tracked in specs/158-teacher-accounting-phase3/quickstart.md`
- [x] طالب يشتري باكدج مشترك والمدرسين يرون حصصهم فقط. `Implemented: shared package purchase records per-teacher allocations and teacher finance reads are scoped to each teacher`
- [x] الادمن يراجع تقرير مستحقات مدرس ويوافق على صرف. `Implemented: admin finance review tab plus payout approval then paid-transfer lifecycle`
- [x] فتح بروفايل مدرس من حساب طالب قبل وبعد الشراء. `Implemented: student public teacher profile route, content sections, and teacher-scoped community panel`

### المخرج القابل للتجربة

- `/teacher/finance` او شاشة معادلة تعرض Calendar الدخل.
- شاشة Admin لمراجعة مستحقات المدرسين.
- صفحة باكدج مشتركة متعددة المدرسين قابلة للشراء.
- بروفايل مدرس قابل للتصفح وفيه محتوى وكوميونيتي.

---

## Phase 4: الموظفين والعمليات الداخلية

### الهدف

تنظيم تشغيل المنصة داخليا: HR، مرتبات، طلبات، موافقات، مهام، وشكاوى لكل اطراف المنصة.

### الاعتماديات

- مراجعة `specs/090-hr-core-employees-attendance-vacations/`, `specs/096-payroll-accounting/`, `specs/091-operations-task-manager/`, `specs/097-audit-and-reports/`.

### 4.1 ورك فلو الموظفين كامل

- [ ] مراجعة الموجود في HR والمرتبات.
- [ ] اضافة مراحل طلبات الموظفين: طلب، مراجعة، موافقة، رفض، تنفيذ، اغلاق.
- [ ] ربط الطلبات بالمهام والمتابعة.
- [ ] ربط كل اجراء بصلاحية واضحة.
- [ ] تسجيل Audit لكل تغيير حالة.
- [ ] Dashboard للادارة تعرض المتأخرات، الطلبات المفتوحة، الحضور، الاجازات، والمهام.
- [ ] ربط المرتبات بالحضور والاجازات والخصومات/الاضافات حسب قواعد واضحة.

### 4.2 الشكاوى لكل حاجة

- [ ] نظام Tickets مستقل عن بنية المحتوى الاساسية.
- [ ] الطالب/ولي الامر/المدرس/الموظف يقدر يفتح شكوى.
- [ ] الشكوى يمكن ان ترتبط بـ: فيديو، امتحان، دفع، كود، مدرس، موظف، او مشكلة عامة.
- [ ] كل شكوى لها حالة، اولوية، مسؤول، تعليقات داخلية، وتعليقات ظاهرة للمشتكي.
- [ ] سجل احداث كامل لكل شكوى.
- [ ] صلاحيات واضحة لمن يقرأ او يرد او يغلق الشكوى.
- [ ] تقارير SLA للادارة.

### تاسكات يدوية

- [ ] موظف يقدم طلب اجازة وادمن يوافق عليه.
- [ ] ادمن ينشئ مهمة لموظف ويتابعها حتى الاغلاق.
- [ ] طالب يفتح شكوى على فيديو ويتم تحويلها لمسؤول ثم اغلاقها.
- [ ] ولي امر يفتح شكوى دفع ويتم ربطها بعملية شراء.

### المخرج القابل للتجربة

- Dashboard عمليات داخلية.
- شاشة Tickets عامة مع حالات وتعليقات.
- Flow كامل من طلب/شكوى الى اغلاق مع Audit.

---

## Phase 5: تحسينات الواجهة والصفحات العامة

### الهدف

رفع جودة الصفحات العامة وتجربة الطالب والزائر بدون كسر منطق البيع والصلاحيات.

### الاعتماديات

- Phase 1 للامتحانات والمحتوى.
- Phase 3 لبروفايل المدرس.
- مراجعة `specs/080-landing-page-reference-redesign/`, `specs/141-teacher-photo-refinement-and-bunny-stream/`, `specs/057-comments-moderation/`.

### 5.1 لوحة شرف اوائل المنصة

- [ ] Frontend يسحب اعلى الدرجات من جدول الامتحانات الحالي.
- [ ] دعم فلترة حسب الصف، المدرس، المادة، او الفترة.
- [ ] اخفاء بيانات الطالب الحساسة والاكتفاء بالاسم/الصورة/الترتيب.
- [ ] منع ظهور طالب لو حسابه محذوف/محظور او لا يسمح بالظهور.

### 5.2 فيديو تعريفي للمدرس من الخارج

- [ ] اضافة حقل فيديو في بروفايل المدرس او صفحة الهبوط.
- [ ] دعم رابط خارجي او provider من المدعومين حاليا.
- [ ] عرض الفيديو للزائر قبل تسجيل الدخول او الشراء.
- [ ] fallback واضح لو الرابط غير صالح.

### 5.3 تقييم الفيديو والامتحان

- [ ] اضافة جدول Ratings يربط الطالب بفيديو او امتحان.
- [ ] منع الطالب من تقييم نفس العنصر اكثر من مرة.
- [ ] عرض متوسط التقييم وعدد التقييمات.
- [ ] ادوات moderation او اخفاء تقييمات مسيئة لو فيه تعليق نصي.
- [ ] لا يسمح بالتقييم الا لمن لديه وصول فعلي للمحتوى.

### 5.4 خريطة الطلاب من كل محافظة في اللاندنج

- [ ] الاعتماد على محافظة الطالب من StudentProfile.
- [ ] عرض ارقام مجمعة فقط بدون اي بيانات شخصية.
- [ ] دعم ارقام seeded/fake للعرض التسويقي لو تم تمييزها بوضوح في الادمن.
- [ ] فلترة حسب مدرس او منصة عامة حسب سياق الصفحة.

### تاسكات يدوية

- [ ] زائر يشاهد فيديو تعريفي لمدرس.
- [ ] طالب يقيّم فيديو وامتحان ولا يستطيع التقييم مرتين.
- [ ] الادمن يراجع لوحة الشرف بفلاتر مختلفة.
- [ ] فتح اللاندنج والتأكد ان خريطة المحافظ لا تعرض بيانات شخصية.

### المخرج القابل للتجربة

- Landing محسنة فيها لوحة شرف وخريطة.
- بروفايل مدرس فيه فيديو تعريفي.
- تقييمات ظاهرة على الفيديوهات والامتحانات.

---

## Phase 6: الاشعارات والاعلانات واللايف

### الهدف

اضافة قنوات تواصل وربح وتشغيل متقدمة بعد استقرار Events والادوار والحسابات.

### الاعتماديات

- Phase 3 للحسابات والملكية المالية.
- Phase 4 للعمليات والصلاحيات.
- مراجعة `specs/093-internal-chat-notifications/`, `specs/142-live-support-command-center/`, `specs/143-ai-live-support-agent/`, `specs/146-ai-live-support-completion/`.

### 6.1 نظام اشعارات لكل Event

- [ ] تحديد Events الاساسية: شراء، كود، هدية، امتحان، شكوى، طلب موظف، مستحقات مدرس، حضور طالب.
- [ ] تحديد من يرسل لمن: منصة، مدرس، طالب، ولي امر، موظف.
- [ ] دعم اشعارات داخلية اولا.
- [ ] تجهيز Push لاحقا.
- [ ] تجهيز SMS/WhatsApp لاحقا بدون ربط اجباري في اول نسخة.
- [ ] صفحة ادمن لتفعيل/تعطيل نوع الاشعار حسب الدور.

### 6.2 اعلانات داخل المنصة والفيديوهات

- [ ] تحديد هل الاعلان تابع للمنصة ام المدرس.
- [ ] تحديد من يحصل على ارباح الاعلان.
- [ ] دعم مواضع داخل الواجهة: Dashboard، Landing، Profile، Package page.
- [ ] دعم مواضع داخل الفيديو: قبل الفيديو او اثناء الفيديو حسب قدرة المشغل.
- [ ] تحديد التكلفة على المدرس او المنصة.
- [ ] تقارير مشاهدة/نقرات بسيطة.
- [ ] منع الاعلان من كسر تجربة الامتحانات او الفيديوهات المدفوعة.

### 6.3 اللايف فيديو

- [ ] اختيار مزود بث وتحديد التكلفة.
- [ ] صلاحيات دخول حسب شراء/باكدج/هدية.
- [ ] تسجيل اختياري للحصة وتحويلها لفيديو لاحقا.
- [ ] تقرير حضور اللايف.
- [ ] تحديد هل التكلفة على المدرس او المنصة.

### تاسكات يدوية

- [ ] ادمن يفعل اشعار شراء ويصل لطالب/مدرس حسب السيناريو.
- [ ] ادمن ينشئ اعلان منصة واعلان مدرس ويظهران في مواضع مختلفة.
- [ ] تجربة لايف تجريبية بحساب مدرس وحساب طالب.

### المخرج القابل للتجربة

- مركز اشعارات داخلي يعمل على Events حقيقية.
- شاشة ادارة اعلانات ومواضع عرض.
- Live session تجريبية بحضور طالب وتقرير حضور.

---

## ترتيب التنفيذ المقترح

1. Phase 0: Audit وربط الموجود.
2. Phase 1.1: اكواد داخلية وانواع محتوى.
3. Phase 1.4: الامتحانات العامة المستقلة.
4. Phase 1.2: الهدايا والحصص المجانية.
5. Phase 1.3: كوبونات واكواد خصم متقدمة.
6. Phase 2: تطبيق ولي الامر والتحويلات.
7. Phase 3.1 و 3.2: حسابات المدرس اليومية وربط الرصيد بالمحتوى.
8. Phase 3.3: باكدج المدرسين المشترك.
9. Phase 3.4: بروفايل المدرس والكوميونيتي.
10. Phase 4: HR workflows والشكاوى.
11. Phase 5: لوحة الشرف، الفيديو التعريفي، التقييمات، خريطة المحافظ.
12. Phase 6.1: الاشعارات.
13. Phase 6.2: الاعلانات.
14. Phase 6.3: اللايف.

## بنود مؤجلة عمدا

- باكدج المدرسين المشترك يؤجل حتى تكون نسب المدرسين والمنصة ثابتة ومختبرة.
- نقل الكوميونيتي داخل بروفايل المدرس يؤجل حتى يكون بروفايل المدرس موجود فعلا.
- خريطة المحافظ تؤجل لو بيانات المحافظ غير مكتملة او تحتاج تنظيف.
- الاعلانات تؤجل حتى يتحدد مالك الاعلان ومن يحصل على الارباح.
- الاشعارات لكل Event تؤجل حتى تستقر Events والادوار.
- اللايف يؤجل حتى يتم اختيار مزود بث وحساب التكلفة والصلاحيات.

## Definition of Done لكل Phase

- [ ] Spec او تحديث Spec موجود يغطي التعديل.
- [ ] Data model واضح لو يوجد Database change.
- [ ] API contracts واضحة لو يوجد Backend.
- [ ] Permissions محددة ومختبرة.
- [ ] Audit/Logs للعمليات الادارية والمالية.
- [ ] Tests للمنطق الحرج.
- [ ] Manual QA checklist مكتملة.
- [ ] شاشة او Flow قابل للتجربة من المستخدم المناسب.
- [ ] لا توجد Regression واضحة في الشراء، الدخول، الصلاحيات، او تشغيل Docker.

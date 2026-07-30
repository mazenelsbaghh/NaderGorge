# Research: منظومة الموارد البشرية المتكاملة

## 1. تعريف الموظف

**Decision**: صفة الموظف علاقة صريحة عبر `EmployeeProfile` وحالة lifecycle، ولا تستنتج من كون الحساب غير طالب. المدرس أو الطالب لا يدخل HR إلا بعملية تعيين صريحة تنشئ الملف.

**Rationale**: `AdminGetEmployeesQuery` الحالي يستخدم `Role.Name != "Student"` ولذلك يضم المدرسين والأدوار الخدمية بالخطأ.

**Alternatives considered**: قائمة ثابتة لأسماء الأدوار رُفضت لأنها تنكسر عند إنشاء أدوار مخصصة؛ إضافة `IsEmployee` على Role رُفضت لأنها تجعل كل حاملي الدور موظفين قبل نجاح ملفهم.

## 2. إنشاء الحساب وملف الموظف

**Decision**: command واحد `CreateEmployee` يفتح EF transaction وينشئ User/UserRole/EmployeeProfile/EmploymentAssignment/Contract/ShiftAssignment/LeaveBalance/OnboardingTasks، ويقبل idempotency key. يفشل الكل عند فشل أي جزء.

**Rationale**: `AdminCreateUserCommand` و`AdminSaveEmployeeProfileCommand` منفصلان حاليًا، والـfrontend `useCreateEmployee` ينشئ User فقط.

**Alternatives considered**: orchestration من الواجهة رُفضت لأنها تترك حالات جزئية؛ saga رُفضت لأن كل البيانات في PostgreSQL ومعاملة محلية أبسط وأكثر أمانًا.

## 3. التاريخ الوظيفي

**Decision**: جداول effective-dated بفترات `[EffectiveFrom, EffectiveTo)` للتعيين والعقد والشفت وسياسات الراتب، مع منع التداخل بقواعد Application وقيود/فهارس PostgreSQL المناسبة.

**Rationale**: تعديل صف الموظف نفسه يمحو التاريخ ويجعل إعادة حساب راتب شهر سابق غير قابلة للتفسير.

**Alternatives considered**: JSON history رُفض لصعوبة القيود والاستعلام؛ event sourcing كامل رُفض لأنه توسع غير لازم.

## 4. الموافقات والتفويض والتصعيد

**Decision**: `ApprovalInstance` عام بنسخة immutable من تعريف المسار و`ApprovalStepInstance` مرتبة. resolver يحدد المدير ثم HR أو المالية/المدير العام حسب النوع. `ApprovalDelegation` مؤرخ، وhosted scheduler يصعّد بعد SLA. كل قرار يتحقق من actor والنطاق، ويمنع applicant/subject من اعتماد طلبه.

**Rationale**: نفس الضوابط مطلوبة للإجازة والتصحيح وتبديل الشفت والسلف والقضايا والراتب.

**Alternatives considered**: أعمدة `HandledBy` لكل كيان رُفضت لأنها لا تمثل أكثر من مستوى؛ workflows في Redis رُفضت لأن القرار يجب أن يكون durable ومراجعًا.

## 5. الحضور والإجازة

**Decision**: فصل `AttendanceSession` و`AttendanceBreak` عن `WorkdayClassification`. الإجازة تغيّر تصنيف اليوم ولا تنشئ session. `AttendanceAttempt` يسجل القبول/الرفض وأدلة IP/geofence/device. session يرتبط بـscheduled workday كي تعبر الشفتات منتصف الليل.

**Rationale**: `AdminApproveVacationCommand` الحالي ينشئ `AttendanceLog` بـ`ClockOut=null`، بينما `ClockInCommand` يعتبر أي سجل كهذا جلسة نشطة، فيمنع الحضور بسبب إجازة مستقبلية.

**Alternatives considered**: إصلاح query فقط رُفض لأنه يبقي نموذجًا ملتبسًا؛ سجل يوم واحد شامل رُفض لأنه لا يدعم الاستراحات والتصحيحات والشفت الليلي جيدًا.

## 6. إثبات الموقع والجهاز

**Decision**: policy precedence = employee exception ثم shift assignment ثم shift template ثم company default. geofence يحفظ دقة الإحداثي والمسافة ونسخة السياسة؛ trusted device يستخدم token موقّعًا/hashed registration مع revoke، وليس fingerprint خفيًا. remote exception مؤرخ وموافق عليه.

**Rationale**: أدلة قابلة للمراجعة مع احترام قيود المتصفح والخصوصية.

**Alternatives considered**: IP وحده رُفض كإثبات موقع؛ browser fingerprint رُفض لعدم الثبات ومخاطر الخصوصية.

## 7. الرواتب

**Decision**: payroll run يحفظ snapshot للموظف والقواعد والمدخلات، وline items ذات `SourceType/SourceId/RuleVersion/Explanation`. unique source key يمنع التكرار. الحالات: Draft→HRPrepared→FinanceReview→FinanceApproved→GMApproved→Paid→Closed، مع Returned وCancelled قبل الإغلاق. التصحيح بعد الإغلاق adjustment مستقل.

**Rationale**: الكيانات الحالية تحفظ `BasicSalary` وadjustments فقط ولا تفسر الإجمالي أو مصدره، و`AdminFinanceController` يعتمد roles عامة بدل permissions.

**Alternatives considered**: حساب حي عند العرض رُفض لأنه يغير التاريخ؛ تنفيذ صيغ C# ديناميكية رُفض أمنيًا، ويستخدم المحرك DSL محدودًا بعمليات وقيم مسموحة.

## 8. الصلاحيات ونطاق البيانات

**Decision**: مفاتيح دقيقة: `hr.employee.*`, `hr.organization.*`, `hr.shift.*`, `hr.attendance.*`, `hr.leave.*`, `hr.document.*`, `hr.performance.*`, `hr.case.*`, `hr.recruitment.*`, `payroll.configure`, `payroll.prepare`, `payroll.review`, `payroll.final_approve`, `payroll.view`. يطبق handler authorization وorganizational scope قبل query/mutation.

**Rationale**: `hr.manage` واسع، وواجهة الإدارة تربط المالية أحيانًا بـ`users.manage` بينما الخادم يستخدم Roles `Admin,Supervisor`.

**Alternatives considered**: حماية controller فقط رُفضت لإمكانية استدعاء handler داخليًا؛ UI guards فقط رُفضت لأنها ليست حدًا أمنيًا.

## 9. الملفات والاحتفاظ

**Decision**: metadata في PostgreSQL، والملف في asset storage الحالي بمفتاح غير قابل للتخمين، authorization عند التنزيل، hash/version/expiry. retention policy حسب النوع؛ payroll/audit append-only ولا direct delete؛ الباقي archive أو anonymize عبر job مع dry-run report.

**Rationale**: يحقق السرية والتاريخ ومتطلب الاحتفاظ دون اختراع مخزن جديد.

**Alternatives considered**: bytea في PostgreSQL رُفض لتضخم النسخ الاحتياطية؛ روابط عامة رُفضت أمنيًا.

## 10. الترحيل والتشغيل والرجوع

**Decision**: `HrModuleRollout` لكل وحدة بحالات Legacy/ShadowValidated/NewActive/RollingBack. كل migration batch يحفظ mapping وchecksum/count/totals/conflicts. التحويل يسمح read-shadow قبل cutover، لكن الكتابة تكون لمصدر واحد فقط. rollback يعيد routing دون حذف البيانات الجديدة.

**Rationale**: المستخدم اشترط ترتيبًا ثابتًا وتشغيلًا ورجوعًا مستقلين ومنع dual write.

**Alternatives considered**: big-bang رُفض لخطر البيانات؛ dual-write طويل رُفض لصعوبة ضمان التطابق والتكرار.

## 11. الإشعارات والوقت

**Decision**: PostgreSQL/outbox هو مصدر أحداث الإشعار، hosted services تفحص deadlines بقفل موزع، والـUI يحدث عبر SignalR invalidation الموجود. كل deadline مخزن UTC مع WorkDate محسوب بتوقيت القاهرة.

**Rationale**: يعيد استخدام البنية الحالية ويضمن replay وعدم فقد الإشعار.

**Alternatives considered**: timers داخل الطلب رُفضت؛ Redis-only deadlines رُفضت لأنها ليست سجلًا موثوقًا.

## 12. تجربة الواجهة

**Decision**: HR workbench كثيف البيانات desktop-first مع saved filters وdrawers، manager inbox مبسط، self-service mobile-first، RTL وWCAG AA و44px targets وحالات loading/empty/error. الهوية الحالية navy/teal/gold وTajawal/Montserrat ملزمة.

**Rationale**: `PRODUCT.md` و`DESIGN.md` يحددان شخصية Massar بوضوح.

**Alternatives considered**: توصية مولد التصميم العامة App Store/dark/green رُفضت لأنها تغير هوية المنتج؛ glassmorphism رُفض لتقليل الوضوح في الجداول.

## 13. حدود البنية

**Decision**: لا microservice ولا database جديدة ولا تعديل worker. تفصل الوحدات منطقيًا داخل التطبيق الحالي، وتستخدم outbox وhosted services الموجودة.

**Rationale**: شركة واحدة وحجم المنصة الحالي لا يبرران تعقيدًا تشغيليًا إضافيًا.

**Alternatives considered**: HR service مستقل رُفض بسبب distributed transactions في إنشاء الحساب والموظف؛ نظام workflow خارجي رُفض لزيادة التشغيل دون حاجة مثبتة.

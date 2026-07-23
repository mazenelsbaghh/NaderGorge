# Research: مركز التقارير المتقدمة

## 1. بنية محرك التقرير

**Decision**: كتالوج typed + provider مستقل لكل مجال، مع request AST محدود ومُتحقق منه.

**Rationale**: البيانات موزعة بين المحتوى، الوصول، المشاهدة، التقييم، المال، التشغيل والدعم. provider يمنع handler ضخم، والكتالوج يمنع حقن أسماء حقول/SQL ويحدد operators والأدوار.

**Alternatives considered**:

- Dynamic LINQ من أسماء العميل: رُفض لمخاطر الأمن وصعوبة الترجمة والاختبار.
- endpoint منفصل لكل تقرير جاهز: رُفض لأنه لا يحقق الفلاتر المركبة ولا إعادة الاستخدام.
- مستودع بيانات/OLAP جديد: مؤجل؛ الحجم الحالي وهدف 5 ثوانٍ يمكن تلبيتهما عبر projections وفهارس PostgreSQL.

## 2. تمثيل الفلاتر

**Decision**: شجرة `FilterGroup` بعامل `all|any`، ومجموعات بعمق أقصى 3، وداخلها شروط typed ذات field/operator/values.

**Rationale**: تحقق احتياج `(A أو B) و(C أو D)` مع حدود تمنع الاستعلامات المتفجرة. القيمة المتعددة داخل `in/notIn` لا تتحول تلقائياً إلى معنى غامض.

**Alternatives considered**:

- قائمة فلاتر مسطحة: لا تمثل المجموعات.
- expression string: صعبة الترجمة والـ localization وغير آمنة.

## 3. عزل المدرس

**Decision**: `IReportScopeResolver` يستخرج actor role وTeacherId/عضوية الاستاف من قاعدة البيانات، ثم يمرر `EffectiveReportScope` immutable إلى provider. لا توجد `teacherId` موثوقة في الطلب.

**Rationale**: إخفاء زر الواجهة لا يكفي، وإضافة scope بعد بناء query قد تسمح بتسريب counts أو exports.

**Alternatives considered**:

- قبول teacherId والتحقق منه: مساحة خطأ أكبر وIDOR محتمل.
- global query filters في EF: لا تناسب admin/teacher المختلفين وقد تؤثر على بقية التطبيق.

## 4. بيانات الطالب للمدرس

**Decision**: يسمح الاسم والكود والهواتف وبيانات الملف الأكاديمي والولي، لكن فقط لطلاب لهم علاقة موثقة بمحتوى المدرس. تمنع password hashes، refresh tokens، devices/IP، support/chat، security logs، raw parent tracking code وأسرار الدفع.

**Rationale**: يحقق قرار المنتج «كل بيانات الطالب بدون الدعم» مع إبقاء credentials/secrets خارج تعريف البيانات الشخصية المعتاد.

**Alternatives considered**:

- كشف كل الجداول المتعلقة بالطالب: رُفض بسبب أسرار المصادقة والأمان وبيانات خارج غرض التدريس.
- إخفاء كل الهواتف: لا يطابق قرار المنتج.

## 5. تعريف المشتري وغير المشتري

**Decision**: يحسب النظام أولاً cohort الطلاب الفعالين المطابقين للنطاق الأكاديمي ومحتوى منشور عند `asOf`; `purchased` يعني وجود grant/financial source مناسب، و`hasActiveAccess` يعني grant فعال عند `asOf`. `notPurchased` هو cohort بلا مصدر شراء/هدية/كود حسب filter المختار، بينما `notActiveAccess` يظل حالة منفصلة.

**Rationale**: عدم الفصل بين الشراء والوصول والانتهاء يؤدي إلى أرقام خاطئة.

**Alternatives considered**:

- عدم وجود grant = لم يشترِ: رُفض لأن grant قد ينتهي، وقد توجد عملية شراء أو هدية محفوظة بمصدر آخر.

## 6. اتساق المؤشرات والرسم والجدول

**Decision**: provider يبني `baseQuery` واحداً بعد scope+filters؛ summary/chart/rows projections مشتقة منه وتستخدم `asOfUtc` واحداً. response يعيد `snapshotHash`.

**Rationale**: يمنع اختلاف totals بسبب توقيت أو شروط مختلفة، ويتيح ربط export بنفس اللقطة.

## 7. الحفظ والإصدار

**Decision**: `ReportDefinition.ConfigurationJson` يحتوي DTO canonical فقط مع `SchemaVersion`; يعاد validation/migration والتفويض عند القراءة. optimistic concurrency عبر version.

**Rationale**: definitions ليست code ولا expression قابلاً للتنفيذ، والصلاحيات قد تتغير بعد الحفظ.

**Alternatives considered**:

- تخزين كل filter في جداول متعددة: تعقيد schema مبكر دون فائدة.
- إعادة JSON مباشرة للمنفذ: رُفض لأسباب versioning/security.

## 8. Excel وPDF

**Decision**: إنشاء الملفات من الخادم باستخدام ClosedXML وQuestPDF، بخط عربي مفتوح المصدر مضمن، من snapshot مُعاد تفويضها. XLSX يحتوي ورقة Summary وData؛ PDF يحتوي العنوان والفلاتر والمؤشرات والجدول مع pagination.

**Rationale**: التصدير في المتصفح يمكن التلاعب به ولا يضمن نفس الصفوف/الصلاحيات، وCSV ليس Excel حقيقياً ولا يحافظ على الأنواع العربية.

**Alternatives considered**:

- jsPDF في frontend: موجود حالياً لكنه لا يضمن authorization snapshot أو Arabic shaping المتسق.
- EPPlus: ترخيصه التجاري يحتاج قراراً منفصلاً.
- CSV: لا يحقق XLSX/PDF المطلوبين.

## 9. تنفيذ التصدير

**Decision**: عند POST export يعيد الخادم التفويض ويُجمد الصفوف المصرح بها عبر streaming إلى spool خاص immutable، ثم ينشئ `ReportExport` durable بحالات Queued/Running/Completed/Failed/Expired. `BackgroundService` محدود التوازي يحول الـspool نفسه إلى XLSX/PDF؛ الملف وspool private وTTL 24 ساعة، والتنزيل يمر عبر endpoint مصرح.

**Rationale**: التجميد عند القبول هو الطريقة العملية لضمان تطابق «لحظة الطلب» مع جدول قد تتغير صفوفه لاحقاً؛ streaming يمنع حمل الصفوف في الذاكرة، والتحويل الثقيل فقط يبقى خارج request thread. الحالة durable تتحمل إعادة تشغيل container.

**Alternatives considered**:

- إعادة query داخل worker: رُفض لأنها لا تضمن لقطة البيانات عند الطلب مع mutations/deletes.
- توليد الملف النهائي متزامناً: timeout/CPU risk.
- BullMQ worker: يضيف contract backend-worker بلا حاجة، بينما generation .NET.

## 10. الرسم البياني

**Decision**: API يعيد series محايدة (`categories`, `series{name,data}`) ونوعاً مقترحاً، والواجهة ترسم SVG/CSS component مع جدول بديل accessible. لا يُسمح للعميل بطلب measure غير مسموح.

**Rationale**: contract مستقل عن مكتبة الرسم ويسمح بجداول accessibility.

## 11. التوقيت

**Decision**: كل التخزين/الحدود الداخلية UTC، و`Africa/Cairo` هو timezone الافتراضي والإجباري v1 للتجميع والعرض والتصدير. يحول date-only إلى بداية/نهاية اليوم محلياً ثم UTC.

**Rationale**: القاهرة قد تتغير فيها إزاحة DST؛ offset ثابت غير صحيح.

## 12. التوافر الجزئي

**Decision**: كل domain/metric له `availability: available|partial|unavailable` و`reasonCode`. الحقول غير المدعومة لا تعيد صفراً.

**Rationale**: بعض telemetry مثل فتح ولي الأمر ليست مسجلة بصورة كاملة، والأرقام المصطنعة أخطر من غياب معلن.

## 13. الأداء والفهارس

**Decision**: projections فقط، `AsNoTracking`, keyset/offset pagination ضمن حد 200، aggregates منفصلة مشتقة من base filter، وفهارس مركبة على foreign keys + timestamps/status الأكثر استخداماً. تختبر SQL على PostgreSQL و`EXPLAIN ANALYZE` للـ presets الثقيلة.

**Rationale**: EF InMemory لا يكشف أخطاء الترجمة أو query plans.

## 14. التدقيق والخصوصية

**Decision**: audit يسجل actor/domain/definitionId/exportId/snapshotHash/rowCount ولا يسجل قيم phone أو filter values الحساسة كاملة. ملفات export random names، private، no-cache، وتحذف عند expiry.

**Rationale**: AuditLog نفسه لا ينبغي أن يتحول إلى نسخة ثانية من PII.

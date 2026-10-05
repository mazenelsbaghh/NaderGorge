# تحقق نسخة التجربة — 2026-09-26

## البيئة والنطاق

macOS، Python 3.14.2، SQLite المرفقة معه، ومتصفح Codex. كل التغييرات داخل `exam-room/`، ولا يستخدم التطبيق قاعدة بيانات المنصة. التشغيل من المصدر في المتصفح لا يحتاج حزمًا إضافية. التحديث المكتبي أدناه يضم Python وpywebview في حزمة مستقلة.

## الاختبارات الآلية

نجح `python3 -m unittest discover -s tests -v`: عشرة اختبارات سلوك بقاعدة SQLite حقيقية مؤقتة. تغطي انتظار الطالب بدون كشف الأسئلة، منع كشف الإجابة النموذجية والدرجة، حفظًا قابلاً لإعادة المحاولة بدون تكرار، ثبات التسليم، حساب الاختيارات وتصحيح المقالي، منع الإجابات لسؤال غير مخصص، الوقت الموحد والفردي والدخول المتأخر، استعادة الدخول بدون إعادة الوقت، إعادة فتح البيانات واستعادة نسخة احتياطية، ومنع وصول الطلاب لواجهات الإدارة والطلبات من مصدر آخر. يشمل التحقق منع الخروج إلى امتحان آخر قبل تسليم المحاولة الحالية.

نجح `python3 -m tests.load_room`: ١٠٠ جلسة متزامنة عبر HTTP المحلي، صفر طلبات فاشلة؛ زمن موجة الدخول 0.588 ثانية، الحفظ 0.483 ثانية، والتسليم 0.376 ثانية في هذا التشغيل. جميع المحاولات احتفظت بالإجابات ودرجات الاختيارات المتوقعة. هذه أزمنة موجات كاملة في تجربة قصيرة على الكمبيوتر نفسه، وليست قياسًا للراوتر أو وعدًا بزمن استجابة على موبايلات حقيقية.

نجح فحص صياغة Python ووحدات JavaScript. لا تغييرات على تطبيق المنصة تستدعي بناءه أو اختبارات نشره.

## تجربة المتصفح الفعلية

تمت دورة من لوحة الإدارة وصفحة طالب منفصلة: إنشاء نموذج، فتح الانتظار، دخول بالكود، بدء موحد، حل ثلاثة أسئلة اختيارات ومقالي، تحقق الحفظ، إعادة تحميل الصفحة مع بقاء الإجابات، محاكاة انقطاع اتصال تبويب الطالب، كتابة مسودة ثم عودة الاتصال وإعادة التحميل مع بقاء الإجابة الجديدة، تسليم، تصحيح المقالي، وفتح تقرير بنتيجة 9/10. شاشة الطالب النهائية لم تعرض درجة أو إجابة.

تم فحص عرض موبايل بعرض 390 بكسل؛ عرض المستند 390 أيضًا دون تجاوز أفقي. بعد إعادة تشغيل الخادم بقيت نتيجة الاختبار محفوظة. تم اختبار زر «دخول امتحان آخر»، وإنشاء نسخة جديدة وتعديل عنوانها وحفظها وتوليد عشرين كودًا وفتح انتظارها. بقيت الجلسة الجديدة بدون طلاب للمستخدم، والجلسة القديمة مسجلة كاختبار واجهة.

صورة القاعة الجاهزة: `artifacts/ready-room.png` (أثر محلي مستثنى من Git ومن حزمة التوزيع).

## حدود التحقق

- لم يتم اختبار Windows أو هواتف فعلية أو انقطاع كهرباء فعلي أو شبكة بها ١٠٠/٦٠٠ جهاز.
- التقرير HTML جاهز للطباعة، لكن لم يتم التحقق من ملف PDF صادر من نافذة طباعة نظام التشغيل.
- تعذر تشغيل اختبار Playwright المستقل لأن Chromium الخاص به غير مثبت؛ جرى تنفيذ السيناريو عبر متصفح Codex بدلًا منه.
- Gemini وMeta WhatsApp وبوابة الراوتر التلقائية خارج نسخة التجربة الحالية. أضيفت حزمة macOS مستقلة في التحديث أدناه؛ Windows لم يُبنَ بعد.


## تحديث كود الطالب وشاشة الخطوات

أُلغي توليد أكواد الامتحان. التسجيل الآن يقبل كود الطالب المكتوب بنفسه، مع توحيد الأرقام العربية والحفاظ على الأصفار في بدايته. قيد عدم التكرار أصبح لكل امتحان. الترقية تحفظ نسخة من قاعدة الإصدار الأول قبل نقل المحاولات بنفس المعرفات والإجابات والجلسات. تظهر في كشف الحضور المحاولات المسجلة فقط؛ الأكواد المحجوزة غير المستخدمة من النسخة السابقة لا تظهر، ولم تُحذف من البيانات.

نجحت ١٢ حالة اختبار، ومنها منع تكرار الكود والتسجيل بكود آخر من نفس جلسة المتصفح، وإعادة استخدام الكود في امتحان تالٍ، وترقية قاعدة قديمة مع الاحتفاظ بالإجابات وجلسة الدخول والنسخة الاحتياطية. نجح اختبار ١٠٠ جلسة HTTP مجددًا بلا فشل: الدخول 0.581 ثانية، الحفظ 0.489 ثانية، التسليم 0.393 ثانية لموجة الطلبات المحلية.

جرى اختبار واجهة السؤال الواحد في بيانات مؤقتة منفصلة عن امتحان المستخدم: كتابة كود غير مولّد، السابق والتالي والتنقل المباشر، بقاء الاختيارات عند الرجوع وإعادة التحميل، وحفظ رقم الخطوة والمقالي بعد إعادة التحميل، ثم التسليم من الخطوة الأخيرة. كان عرض الصفحة 390 بكسل بدون تجاوز أفقي، وظهر سؤال واحد فقط. لا أخطاء JavaScript في سجل التبويب. أعيد تشغيل نسخة المستخدم مع الاحتفاظ ببياناتها. الهوت سبوت لم يُفعّل أو يُختبر فعليًا.


## تحديث الواجهة والبيانات والبرنامج المستقل

نجحت ١٧ حالة اختبار على SQLite حقيقية مؤقتة. أضيف التحقق من منع تشغيل عمليتين على مجلد البيانات نفسه، وثبات الامتحان بعد إغلاق خادم البرنامج وإعادة فتحه، وعدادات التخزين والمكتبة، ونسخة احتياطية منزّلة عبر HTTP تحتفظ بالإجابات والتسليم والتصحيح. اختُبرت حدود الوصول: التخزين والنسخ وفتح المجلد متاحة للإدارة فقط، مع رفض أسماء الملفات غير المسموحة وروابط الملفات الرمزية.

اختبار HTTP المحلي لـ١٠٠ جلسة نجح مجددًا بصفر فشل: الدخول 1.011 ثانية، الحفظ 0.219 ثانية، التسليم 0.476 ثانية لكل موجة. لا يدل على سعة الهوت سبوت أو الشبكة.

اختُبرت مكتبة الامتحانات والبحث وحالة عدم وجود نتائج والتصفية، وتنبيه مغادرة التعديلات غير المحفوظة مع الرجوع والمغادرة. عرض التخزين عند 390 بكسل بقي بعرض مستند 390 دون تجاوز. الطالب يحتفظ بواجهة الخطوات التي تحقّقنا منها سابقًا.

بُنيت `.app` لـApple Silicon باستخدام Python 3.14.2 وPyInstaller 6.22.3 وpywebview 6.2.1، وجرى فتحها فعليًا من Finder عبر أدوات التحكم. ظهر خلل في مسارات الموارد الرمزية داخل حزمة macOS في أول بناء؛ صُحح بحل مسار web قبل التحقق من نطاق الملفات. فُتحت النافذة الجديدة بعد الإصلاح مع الخطوط والتصميم والبيانات، بدون نافذة طرفية أو تشغيل Python خارجي.

تم تشغيل فحص سلامة SQLite من نافذة البرنامج وظهر النجاح، وإنشاء نسخة احتياطية من الواجهة وتنزيل نسخة من نافذة حفظ macOS. فُتح الملف المنزّل بـSQLite وكانت نتيجة quick_check هي ok، مع الاحتفاظ بالامتحانين و٤٠ صف محاولة تاريخية (بينها الأكواد القديمة غير المستخدمة؛ الشاشة تعد المحاولات المسجلة فقط). اختُبر تنبيه إغلاق البرنامج والخروج، ثم أعيد فتحه مع بقاء البيانات.

نُسخت بيانات تجربة المستخدم باستخدام SQLite backup إلى مجلد Application Support الثابت، مع التحقق من تطابق صفوف exams وattempts وaudit وترك البيانات الأصلية في exam-room/data دون حذف. بدأ المستخدم تجربة جديدة في البرنامج أثناء المراجعة؛ تركنا العملية والجلسة تعملان، وبُنيت حزمة التسليم في dist/release بدون استبدال ملفات العملية المفتوحة. تحديث التسليم الأخير يضيف الأيقونة، وتعريب أزرار النظام، ودليل استرجاع يدوي بدون أوامر، ويستخدم نفس محرك التشغيل الذي اختُبر. لم تُعد تجربة إطلاق هذه الحزمة الأخيرة أثناء جلسة المستخدم.

حدود هذه الزيادة: الحزمة موقّعة محليًا فقط وليست Apple-notarized؛ ليست نسخة Intel. لم تُبنَ أو تُجرَّب EXE على Windows؛ يوجد سكربت بناء على Windows مع احتياج WebView2. الاسترجاع يدوي؛ اختُبرت صحة النسخ المسترجعة في اختبارات السلوك، ولم نستبدل قاعدة المستخدم لاختبار الاسترجاع. لا Gemini أو WhatsApp أو بوابة دخول تلقائية.

## تحديث التصحيح الجماعي للمقالي

أضيف زر «تصحيح المقالي للجميع» في «التصحيح والنتائج» بعد إغلاق الامتحان. يختار الإجابات المقالية المسلّمة غير المصححة، يرسل نص السؤال والإجابة النموذجية وإجابة الطالب إلى Gemini، ويخزن نتيجة صح/غلط وملاحظة مع إبقاء المراجعة اليدوية ممكنة. القرار الملتبس لا يمنح درجة تلقائيًا. الوظيفة قابلة للإيقاف وإعادة تشغيل الباقي، وتحفظ عداد العملية في SQLite؛ مفتاح API لا يخزن في SQLite، وقد تأكد استرجاعه من macOS Keychain بعد إعادة تشغيل خادم تجربة منفصل.

نجحت ٢٣ حالة اختبار بعد التغيير، منها عدم تعديل درجات المدرس، وتعامل المصحح مع السباق عند التصحيح اليدوي، واستئناف المتبقي، وتأجيل قراءة Keychain إلى وقت طلب التصحيح، وحدود واجهات الإدارة والطلاب. جُرّب طلب Gemini فعلي باستخدام المفتاح الذي قدمه المالك على سؤال وإجابة **تجريبيين فقط**؛ الرد جاء بصيغة JSON صالحة، واكتمل التصحيح من زر الواجهة لسؤال تجريبي واحد (١/١) بعد إعادة تشغيل خادم التجربة. اشتغلت الحزمة المكتبية الجديدة على منفذين تجريبيين وفتحت خادم الإدارة والطلاب بدون انتظار صلاحية Keychain عند بدء البرنامج. نجح أيضًا اختبار ١٠٠ جلسة HTTP محلية بلا فشل. لم يُرسل أي سؤال أو إجابة من بيانات المستخدم الحقيقية أثناء الاختبار، ولم يُعاد تشغيل التطبيق القديم المفتوح؛ يحتاج فتح الحزمة الجديدة بعد إغلاقه. لم يُختبر معدل مئات الإجابات مع حصة حساب Google الفعلية، ولا WhatsApp.

## تحديث إعدادات Gemini وملف .env

أُضيف قسم «إعدادات التصحيح» داخل «الإعدادات والبيانات». يحفظ مفتاح Gemini في `.env` بجانب `exams.sqlite3` بصلاحية 0600 على macOS؛ استجابة واجهة الإدارة تعرض حالة الحفظ والمسار فقط ولا تعيد قيمة المفتاح. يستعمل المصحح المفتاح من الملف بعد إعادة التشغيل، مع الاحتفاظ بقراءة Keychain القديمة عند غياب `.env`. نقلنا مفتاح المالك المحفوظ سابقًا إلى ملف `.env` الفعلي دون طباعة قيمته. الملف خارج حزمة التطبيق ونسخ SQLite المنزّلة.

نجحت ٢٥ حالة اختبار، منها حفظ المفتاح عبر واجهة الإدارة، منع وصول الطلاب إليه، عدم إرجاعه في الاستجابة، واستمرار قراءته بعد إعادة إنشاء المخزن. نجح اختبار ١٠٠ جلسة HTTP محلية بلا فشل. اختُبرت شاشة الإعدادات وحفظ مفتاح تجريبي في مجلد بيانات منفصل عبر المتصفح، وظهرت حالة «المفتاح محفوظ» بدون عرض قيمته. بُنيت الحزمة المكتبية الجديدة وفتح خادما الإدارة والطلاب على منفذين تجريبيين؛ لم نجرِ طلب Gemini آخر من بيانات المستخدم عند هذا التحديث.


## تحويل الباك إند إلى Go — 2026-09-27

نُقلت مسارات الطلاب والإدارة، صلاحيات الوصول، حفظ SQLite، النسخ الاحتياطية، التقارير، والتصحيح الجماعي إلى خادم Go. بقيت واجهة HTML/CSS/JavaScript كما هي؛ حزمة سطح المكتب تستخدم Python/pywebview كنافذة تشغيل فقط. ملفات Python القديمة محفوظة للرجوع إلى سلوك النسخة السابقة ولا تدخل في مسار التشغيل الجديد.

نجح `go test -race ./...` و`go vet ./...`. غطى الاختبار دورة امتحان كاملة، النتيجة والتقرير، منع فتح إدارة المدرس من الشبكة، وحالة تصحيح مقالي جماعي برد Gemini محاكى منح درجة السؤال كاملة. نجح `python3 tests/load_go.py` مع ١٠٠ طالب متزامن على loopback دون طلبات فاشلة؛ الموجات استغرقت 0.035 ثانية للدخول، 0.015 للحفظ، و0.017 للتسليم في هذا الجهاز. لا تقيس الأرقام شبكة الواي فاي أو مئات الهواتف الفعلية.

فتح خادم Go نسخة مؤقتة مأخوذة بطريقة SQLite backup من قاعدة البيانات الحالية ذات مخطط v2، وطابق عدد الامتحانات والطلاب (٣ و٣) دون تغيير البيانات الحية. اختُبرت أيضًا ترقية مخطط v1 إلى v2 على قاعدة مؤقتة مع حفظ محاولة الطالب وإنشاء نسخة قبل الترقية. بُني البرنامج لـmacOS Apple Silicon وتأكد وجود خادم Go داخله، ونجح فحص توقيع الحزمة المحلي وتشغيل الخادم المضمّن على منافذ اختبار وقاعدة مؤقتة. نجح تشغيل نافذة الحزمة المكتبية على منافذ تجريبية وقاعدة مؤقتة، وتوقف خادم Go التابع لها عند إنهاء عملية النافذة. نجح بناء ملف Go لويندوز amd64 فقط؛ لم يُشغّل على Windows. لم تُرسل بيانات المستخدم إلى Gemini في اختبارات التحويل، ولم تُستبدل العملية القديمة المفتوحة تلقائيًا.


## مثبّت النظام — 2026-09-27

بُني ملف `Massar-Exam-Room-macOS-arm64-Installer.pkg` باستخدام `pkgbuild --component` للتثبيت في `/Applications`. فُتحت الحمولة للتحقق، ووجد خادم Go بداخل التطبيق ولم يوجد ملف `.env` أو قاعدة بيانات طلاب في الحزمة. فحص `codesign --verify --deep --strict` للتطبيق المضمّن نجح. المثبّت نفسه غير موقّع بشهادة Developer ID Installer أو موثّق notarized، لذلك قد يحتاج السماح من إعدادات macOS عند أول فتح. لم يُثبّت على جهاز المستخدم أثناء التحقق، حفاظًا على التطبيق المفتوح وبياناته. أُضيفت وصفة Inno Setup لمثبّت Windows، ولم تُشغّل لأن بيئة التحقق macOS.

## واجهة إدارة React — 2026-09-27

نُقلت واجهة الإدارة في تطبيق سطح المكتب إلى React، وتُبنى بوساطة Vite إلى ملف ثابت يضمّه خادم Go. بقيت صفحة الطالب خفيفة كما كانت. أُعيد بناء مثبّت macOS والنسخة المحمولة؛ ويُشغّل بناء Windows خطوة الواجهة نفسها قبل التغليف.

في قاعدة اختبار مستقلة، أُنشئ امتحان بسؤال اختيار ومقالي من واجهة React، وفُتحت القاعة، ودخل طالب بكوده، وبدأ الامتحان، وسلّم الإجابتين. ظهرت درجة الاختيار تلقائيًا، وحُفظت درجة المقالي يدويًا وظهر رابط التقرير. فُحصت المكتبة والإعدادات ومسار SQLite والنسخ الاحتياطية. لم تُستخدم قاعدة المستخدم أو مفتاح Gemini في هذه التجربة. أُصلح احتساب إجمالي حجم النسخ الاحتياطية الذي كان يظهر صفرًا رغم وجود ملفات.

نجح بناء Vite، و`go test -race ./...`، و`go vet ./...`، وبناء Go لـWindows amd64. اختبار ١٠٠ طالب HTTP محلي انتهى بصفر فشل. نجح تشغيل تطبيق macOS المعبأ من مجلد اختبار مؤقت، وقدّم واجهة React وصفحة الطالب من الخادم المضمن؛ ونجح فحص توقيع التطبيق المحلي. ملف `.pkg` لم يُثبّت على جهاز المستخدم، ومثبّت Windows لم يُبنَ أو يُجرّب على Windows بعد.

## محاكاة ٢٠ طالب على عنوان الشبكة — 2026-09-27

شُغّل خادم Go بقاعدة مؤقتة، مع ٢٠ عميل HTTP متزامن على عنوان الطلاب `192.168.1.35`، و١٠ جولات فحص جلسة بفاصل ثلاث ثوانٍ، ثم حفظ وتسليم. صفر فشل؛ موجة الدخول 0.008 ثانية، أبطأ موجة فحص 0.014 ثانية، الحفظ 0.013 ثانية، والتسليم 0.011 ثانية. هذه أزمنة موجات كاملة على الكمبيوتر نفسه. أكد جدول التوجيه أن طلبات العنوان المحلي تمر عبر `lo0`، فلا تعبر الراوتر أو موجات الواي فاي. لذا تثبت التجربة قدرة التطبيق المحلية لهذا السيناريو فقط؛ قدرة الراوتر على ٢٠ موبايل فعلي تحتاج أجهزة متصلة به أو معدات اختبار لاسلكي مخصصة.

## اضطراب الاتصال لـ٢٠ جهازًا افتراضيًا — 2026-09-27

أضيف خيار `--chaos` إلى اختبار خادم Go بقواعد بيانات مؤقتة وجلسة مستقلة لكل جهاز. شملت التجربة ١٠ جولات تحديث، و٥ عملاء فشل اتصالهم أولًا ثم أرسلوا المسودة بعد العودة، و٥ أعادوا إرسال نفس الحفظ بعد اعتبار الرد ضائعًا، و٤ أعادوا التسليم بعد اعتبار الرد ضائعًا. نجحت العشرون محاولة دون خطأ، ولكل طالب إجابة مقالية مميزة، ومحاولة واحدة، ودرجة الاختيارات المتوقعة. أبطأ جولة تحديث استغرقت 0.012 ثانية على نفس الكمبيوتر. التأخير والانقطاع هنا مُدخلان في عملاء الاختبار؛ لا يحاكيان موجات الراديو أو سعة الراوتر، ولا يختبران سلوك متصفح الطالب عند فقدان الاتصال فعليًا.

## توسيع المحاكاة إلى ٣٢٤ جهازًا — 2026-09-27

شُغّل الأمر `python3 tests/load_go.py --clients 324 --chaos --poll-rounds 10` بقاعدة مؤقتة. لكل جهاز جلسة مستقلة وإجابة مقالية مميزة. خضعت ٨١ جلسة لانقطاع أولي ثم عودة، وأعادت ٨١ طلب الحفظ بعد اعتبار الرد ضائعًا، وأعادت ٦٥ طلب التسليم. اكتملت ٣٢٤ محاولة صحيحة بلا فشل أو تكرار، وكانت أبطأ جولة تحديث متزامنة 0.071 ثانية. الطلبات سارت عبر `127.0.0.1` داخل الكمبيوتر؛ النتيجة تخص خادم Go والتخزين المحلي وسلوك إعادة الطلب في الاختبار، ولا تقيس ٣٢٤ ارتباط واي فاي أو قدرة الراوتر أو هواتف فعلية.

## محاكاة ١٠٬٠٠٠ جلسة — 2026-09-27

نسخة التشغيل المعتادة تحد دخول الامتحان الواحد عند ١٠٠٠ طالب، لذلك بُني الخادم لقياس الضغط فقط بعلامة `loadtest` التي ترفع الحد إلى ١٠٬٠٠٠؛ بناء التطبيق والمثبت لا يستخدم هذه العلامة. أعيد استخدام اتصالات HTTP في أداة القياس كي لا تنفد المنافذ المحلية، وحُددت الطلبات المتزامنة عند ٢٥٦. شُغّل `python3 tests/load_go.py --clients 10000 --chaos --poll-rounds 10 --workers 256` على قاعدة مؤقتة. انضم ١٠٬٠٠٠ طالب في 12.591 ثانية، وكانت أبطأ جولة تحديث لكل الجلسات 0.912 ثانية، واكتمل حفظ وتسليم كل الجلسات خلال 10.978 ثانية. شملت المحاكاة ٢٥٠٠ انقطاع أولي وعودة، و٢٥٠٠ إعادة حفظ بعد اعتبار الرد ضائعًا، و٢٠٠٠ إعادة تسليم؛ صفر فشل، مع تحقق من الإجابة المميزة والدرجة وعدم تكرار المحاولة لكل طالب. الطلبات سارت داخل نفس الكمبيوتر ولم تمر بالراوتر. لا تثبت هذه التجربة أن نسخة الإنتاج تقبل ١٠٬٠٠٠ في امتحان واحد، أو أن ١٠٬٠٠٠ موبايل يمكنهم الاتصال بالواي فاي في المكان.

## 2026-10-05 — شيتات وتقارير PDF وتهيئة واتساب

- `go test ./...` passed, including group/session export matching, preserved text identifiers and incomplete-result handling, and WhatsApp template persistence.
- Generated XLSX read with openpyxl: RTL sheet, frozen header, phone/code retained as strings, unfinished score blank, numeric score/maximum/percentage valid.
- PDF sample and a five-page long answer rendered with Poppler and visually inspected; Arabic joining, name, score, model answers and feedback readable. Frozen bundled renderer produced valid PDF on this Mac.
- End-to-end local HTTP smoke on an isolated test database: blocked PDF before essay grading (409), then downloaded PDF and XLSX after grading. Browser verified group/session selection, result row and WhatsApp preview without sending messages.
- macOS arm64 installer version 0.3.0. Windows build source updated with the bundled renderer; no Windows installer was built or tested in this session.
- Existing attached raster PDF cannot be repaired accurately without its source answers. Regenerate the student's report from the updated application on the Mac containing that student's data.

## 2026-10-05 — الإصدار 0.3.1

- Tests passed for stable varied choice orders, live time extension in shared/individual modes, late arrival duration, and unchanged submitted attempts.
- Batch grader integration exercised both full score and saved fractional score (2.5/7), with external response mocked; negative and excessive scores rejected.
- HTTP smoke confirmed two distinct choice orders, stable choices after time extension, both students' deadlines extended 300 seconds, and correct MCQ grading using original choice IDs.
- Browser verified the running timer, +2 minutes control, and partial-credit checkbox.
- Generated a ZIP containing 500 sample PDFs, checked archive integrity and unique filenames for duplicate student names. Packaged helper also served a PDF ZIP over the authenticated admin HTTP endpoint.
- macOS arm64 package 0.3.1 built; Windows source updated, Windows execution not tested here.

## 2026-10-05 — الإصدار 0.3.2

- Fixed expired-session polling that recreated the same toast every 2–3 seconds, preventing its six-second dismissal.
- Student page switches to lounge polling after expiry, preserves typed join-form fields, and resumes session polling after successful login. The server clears the invalid student cookie without altering saved attempts.
- Added an accessible dismiss button to notifications and avoided resetting their timeout for an identical visible message.
- Go tests pass, including invalid-cookie deletion and a subsequent anonymous session response.
- Browser smoke on isolated test data: teacher reset login, student returned to join form, notice automatically disappeared, and same student re-entered successfully without page reload.

## 2026-10-05 — الإصدار 0.3.3

- Resume tests preserve attempt ID, answers, revision, question IDs and deadline for code+phone re-entry; reject wrong phone, invalidate prior token, and block reopening submitted attempts.
- Presence tests cover grace reset at exam start, hidden-page timeout, teacher resume, rejected answer writes while paused, unchanged deadline, and zero-second manual-only mode.
- Browser on isolated test data saved Cairo selection, then displayed pause notice with disabled radio controls after teacher pause. Resume preserved the saved answer and deadline.
- Visibility notifications and missing visible heartbeats are indicators, not a locked-down browser or a distinction between cheating and network failure. Configurable absence timer defaults to off.
- macOS arm64 0.3.3 installer; no physical iPhone or Windows test performed during this session.

## 2026-10-05 — الإصدار 0.3.4

- Go tests passed. Student session response includes the authenticated student's own code.
- Browser smoke on isolated test data at 320×640: name and code visible in sticky header; no horizontal overflow. After scrolling to the bottom, the final MCQ option ended at 417px with controls starting at 565px; essay input ended at 428px with wrapped final-step controls starting at 550px.
- Footer remained at the viewport bottom on both question types. ResizeObserver updates reserved paper space and heading scroll offset when the header/footer size changes. Viewport override reset after testing.
- Physical iPhone keyboard behavior was not tested in this session.
- macOS arm64 installer 0.3.4 (build 13) built successfully.

## 2026-10-05 — الإصدار 0.3.5

- Integration tests cover cancellation before/after submission, rejected blank/duplicate cancellation, persisted reason after database reopen, unchanged answers/grades/revision/submission, blocked writes/re-entry/reset/resume/PDF, and no finalization on expiry.
- XLSX XML verification: cancelled status and escaped Arabic reason present in the new column P; score and combined result blank for cancelled students. CSV includes reason.
- Browser on isolated test data: teacher opened cancellation dialog, entered reason and confirmed; roster showed «ملغي» and the saved reason. Student page showed cancellation and reason without answer controls.
- Authenticated HTTP XLSX download read with openpyxl: cancelled status and reason confirmed, score/percentage/combined result blank, filter A4:P5. Browser Sheets table also showed status and reason. macOS arm64 installer 0.3.5 built successfully.

## 2026-10-05 — الإصدار 0.3.6

- Go tests pass for targeted extension in shared/individual modes; unselected deadlines and global duration unchanged. Invalid, duplicate, empty, submitted, cancelled and expired selections rejected; mixed submitted selection rolls back all extensions.
- Shared deadline simulation: unselected student finalized at original deadline, selected student remained active, same-code return allowed, new arrivals rejected after the common deadline, and session closed after the final extended deadline.
- Browser on isolated database: selected TIME1, clicked +2 minutes for selected students, saw 16:51 versus unchanged TIME2 14:51. Authenticated HTTP check confirmed exact +120 seconds for TIME1 and zero for TIME2, no global extraSeconds.
- macOS arm64 installer 0.3.6 (build 15) built successfully.

## 2026-10-05 — الإصدار 0.3.7

- Go integration test: default blocks a second publish; enabling allows two rooms; no-choice/invalid-room joins rejected; selected room receives student; current cookie/code cannot move or start a parallel active attempt in another room. Saved answers/revision/deadline unchanged.
- Database reopened with two active rooms and retained enabled setting. Disabling rejected while two remain, accepted after closing one, then single-room guard enforced again. Closing one room did not close the other student's running session.
- Browser on isolated database: first student entered directly and selected Cairo. Teacher enabled multiple rooms and opened second lesson through the launcher while first ran. New student saw required room selector, chose second room and reached its waiting page; first student's selected Cairo remained visible and saved.
- macOS arm64 installer 0.3.7 (build 16) built successfully.

## 2026-10-05 — الإصدار 0.3.8

- Go tests pass. A 20-question bank with questionCount 10 and shuffle false displayed 10 unique bank questions. Return preserved question IDs, answers and deadline. Grading/report counted only assigned questions (20/20), unequal bank points rejected.
- Local authenticated HTTP smoke: five students each received 10 of 20 questions, with five distinct unordered question sets.
- Browser editor showed «بنك الأسئلة: ٢٠ سؤال · يظهر لكل طالب: ١٠ سؤال», field value 10 and explanation of independent sampling and stable recovery.
- macOS arm64 installer 0.3.8 (build 17) built successfully.

## 2026-10-05 — الإصدار 0.3.9

- Frontend production build and Go build passed. Presence classification is shared between row badges, filter matching and counts.
- Browser on isolated data: «يحل الآن» showed only SOLVING; paused showed BANK0; disconnected showed BANK2–BANK4; cancelled showed BANK1 and its reason.
- Combined search BANK2 with disconnected filter showed one matching row and counts changed to one disconnected / zero solving. Clearing search and switching to solving restored SOLVING.
- macOS arm64 installer 0.3.9 (build 18) built successfully; application version and installer presence verified.

## 2026-10-05 — الإصدار 0.4.0

- Go test suite and frontend production build pass. Tests cover rotation, keyboard height, reduced width even with keyboard, ordinary browser chrome changes, fullscreen exit, unsupported fullscreen and browser marker change.
- Integration test verifies five-second grace, server sweep enforcement after reports stop, blocked writes while paused, unchanged answers/revision/deadline, persisted screen evidence after reopening SQLite, teacher-only resume, and disabled monitoring.
- Browser on isolated database: answer controls disabled before confirmation; confirmation entered fullscreen and enabled answers. Teacher roster showed browser marker, screen and viewport size. Escape exited fullscreen; student paused with Cairo answer still selected and clock running. Teacher resume plus return to fullscreen enabled answers again.
- Resize measurements are suppressed while fullscreen layout settles. Physical iPhone/Android split screen and keyboard behavior require device testing; client telemetry is not tamper-proof.
- macOS arm64 installer 0.4.0 (build 19) built successfully; application version and package verified.

## 2026-10-05 — الإصدار 0.4.1

- Frontend production build and Go build pass. Cards preserve the existing roster filtering and student selection logic.
- Browser on isolated fixtures: paused card showed PAUSE; pending showed PENDING; graded showed SUBMITTED; cancelled showed CANCEL and its reason. Submitted + PENDING search showed one row and matching counts. All restored five rows.
- Screen cards: fullscreen and reduced-area each showed OFFLINE; changed marker and fullscreen exit each showed PAUSE. Submitted telemetry excluded from monitoring counts. Labels identify last reported screen state.
- Responsive browser check at 390×844: both groups rendered two columns of 156px; all 18 cards fit their grid. Viewport override reset. Preview saved from card area.
- macOS arm64 installer 0.4.1 (build 20) built successfully; application version and package verified.

## 2026-10-05 — الإصدار 0.4.2

- Frontend production build and Go build passed. Roster sorting is applied after search and card filtering without mutating source attempts.
- Browser on isolated six-student fixtures: newest join showed codes 20/100/2/30/3/10; oldest reversed the join order; numeric codes sorted 2/3/10/20/30/100; Arabic names sorted أحمد/باسم/حسن/زياد/عمر/يوسف. First-entry timestamp appeared under each name.
- Submitted card plus latest-submission sort showed 20/10/3; search زياد narrowed to code 3 and submitted count one. Highest graded score placed 3 before 10, lowest placed zero-score 10 before 3; pending, active and cancelled attempts followed completed grades.
- Responsive check at 390×844: sorting control fitted between x=32 and x=358 with no horizontal clipping. Viewport override reset; screenshot saved. Date formatter is reused across rows for large rosters.
- macOS arm64 installer 0.4.2 (build 21) built successfully after the formatter adjustment; embedded application version and installer verified.

## 2026-10-05 — الإصدار 0.4.3

- Frontend production build and Go build pass. Priority uses current roster status and active screen issue, excludes submitted/cancelled attempts and closed rooms, and partitions the chosen order without changing source data.
- Browser on isolated fixtures: paused code 2 appeared before newer joins; disabling priority restored newest order 20/100/30/2/3/10. Re-enabling and teacher resume removed its alert, reduced problem count to zero, and restored its ordinary position immediately.
- After pausing code 2 again, the non-submitted card showed 2 before healthy code 30; the first row displayed amber emphasis and «يحتاج متابعة: موقوف مؤقتًا». Submitted and cancelled fixtures were not counted as problems.
- Responsive check at 390×844: priority label and count fit inside the toolbar (x=213 to x=358); viewport override reset. Preview saved from the two active rows.
- macOS arm64 installer 0.4.3 (build 22) built successfully; embedded application version and installer verified. Clean-code guard pass confirmed shared status rules, stable ordering within groups, and no API or exam-data mutation from priority controls.

## 2026-10-05 — الإصدار 0.4.4: واتساب السنتر

- Passed `go test ./...` and `go test -race ./...` on real temporary SQLite databases. HTTP provider fixtures verified private configuration, WABA/phone ownership checks, approved Document template restrictions, recipient and per-student PDF content, stable send history, stale-preview rejection, and no second provider message after accepted or ambiguous responses. No real student message was sent.
- Built the React admin bundle and inspected the account/templates screen and report recipient preview in the browser using isolated test data. Verified student name, code, 7/10 result, PDF filename and body-variable substitution; saved screenshot privately under `build/whatsapp-report-preview.jpg`.
- Real read-only Meta checks accepted the supplied phone token and verified the supplied App Secret against its App ID. The supplied numeric IDs identify a phone and an app; the WABA ID is still required for the real template sync and webhook account configuration.
- Built and locally signed macOS arm64 application 0.4.4 / build 23 and installer; no Windows installer, Apple notarization, real delivery or webhook activation claimed.
- Requested production build scope: **all**. Production publication uses a reviewed source-only commit on the fetched shared parent; desktop source is included without local databases, credentials, test fixtures, installers or build outputs. Existing platform webhook code is reused; new account activation depends on the correct WABA ID.

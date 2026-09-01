# تشغيل دعم Facebook Messenger

يجمع التكامل رسائل ما يصل إلى ثلاث صفحات Facebook داخل مساحة الدعم المباشر الحالية. كل محادثات Messenger بشرية فقط: لا ينشئ مسار الاستقبال حالة AI، ويرفض مسار الإرسال أي رسالة ليست من موظف أو Admin قبل الاتصال بـMeta.

## ما يدعمه التكامل

- الاستقبال: نصوص، صور، صوت، وملفات PDF. النوع غير المدعوم يظهر للموظف كرسالة وصفية آمنة.
- الإرسال: نص فقط، ومن الصفحة التي استقبلت المحادثة نفسها.
- هوية العميل هي `(PageId, PSID)`؛ نفس المستخدم على صفحتين يظل محادثتين منفصلتين.
- نافذة الرد الافتراضية 24 ساعة. خيار `HUMAN_AGENT` يتيح رد الموظف حتى سبعة أيام فقط بعد اعتماد الميزة من Meta.
- App Secret وPage Access Tokens وVerify Token تُحفظ مشفرة باستخدام ASP.NET Core Data Protection، ولا يعيدها `GET` الخاص بالإعدادات.
- Verify Token يظهر مرة واحدة عند إنشائه أو تدويره. إذا فُقد، أنشئ قيمة جديدة وحدّثها في Meta.

## الإعداد من لوحة Admin

افتح **الإعدادات والصلاحيات ← Facebook Messenger** بحساب يحمل دور `Admin`، ثم نفّذ الخطوات بالترتيب:

1. أدخل `App ID` و`App Secret` واحفظهما. الإصدار المدعوم حاليًا هو Graph API `v26.0`.
2. اضغط **إنشاء Verify Token**، وانسخ القيمة التي تظهر مرة واحدة.
3. انسخ `Callback URL` من اللوحة مع Verify Token إلى إعداد Webhooks في Meta App، واختر كائن `Page`.
4. ألصق Page Access Token في قسم الصفحات. الخادم يستدعي `GET /me?fields=id,name` للتأكد من هوية الصفحة، ثم يفحص `debug_token` للتأكد أن التوكن صادر لنفس `App ID` ويحمل `pages_messaging` و`pages_manage_metadata`، ثم يشترك ويعيد فحص `/{PAGE_ID}/subscribed_apps`.
5. كرر الربط للصفحات الأخرى حتى ثلاث صفحات. زر **فحص الربط** يراجع صلاحية التوكن والاشتراك من دون إرسال رسالة.

الربط الآلي يشترك في الحقول التي يعالجها التطبيق: `messages` و`message_deliveries` و`message_reads` و`message_echoes`. إعداد Callback URL على مستوى Meta App يظل خطوة يدوية؛ النظام يعرض الرابط والتوكن المطلوبين ولا يغيّر اشتراكات التطبيق العامة تلقائيًا.

إلغاء ربط صفحة يستدعي `DELETE /{PAGE_ID}/subscribed_apps` ثم يعيد الفحص. إذا انقطع الاتصال أو لم يؤكد Meta النتيجة، يحتفظ النظام بإعداد الصفحة في حالة محجوزة بدل حذفها محليًا أو السماح بعملية عكسية. عامل استرجاع موزع يعيد نفس اتجاه العملية بعد دقيقتين، ثم يفرض حجر استقرار لخمس دقائق وينفذ فحص `GET` فقط قبل فتح الاتجاه العكسي. هذه نافذة أمان محلية محافظة فوق مهلة عميل Meta البالغة 30 ثانية، وليست ضمان ترتيب توفره Meta API.

إذا انتهت صلاحية Page Access Token أثناء حالة غير مؤكدة، استخدم **تحديث التوكن** لنفس الصفحة. في حالة الربط يستكمل النظام الاشتراك في الاتجاه نفسه؛ وفي حالة إلغاء الربط يحفظ التوكن الجديد مشفّرًا ولا يرسل أي اشتراك، ثم يستخدمه العامل لإكمال `DELETE`. أثناء طلب نشط أو حجر الاستقرار يظل التحديث محجوبًا حتى لا يتداخل مع عملية جارية.

لا يمكن تغيير `App ID` ما دامت هناك صفحات مرتبطة. ألغِ ربط الصفحات أولًا حتى يؤكد Meta إزالة الاشتراك، ثم احفظ `App ID` و`App Secret` الجديدين. أثناء فك الربط تدخل الصفحة حالة تشغيل محجوزة تمنع فحصًا أو ربطًا متزامنًا من إعادة تفعيلها قبل اكتمال المصالحة مع Meta.

## متطلبات Meta

- Meta App واحدة مع Page Access Token مستقل لكل صفحة.
- الصلاحيتان `pages_messaging` و`pages_manage_metadata` لمسار الربط والرسائل الحالي.
- Advanced Access وApp Review قبل استقبال رسائل مستخدمين حقيقيين لا يحملون دورًا داخل التطبيق؛ Standard Access لا يكفي لهذا الاختبار الإنتاجي.
- اعتماد ميزة `HUMAN_AGENT` قبل تفعيل خيار الرد حتى سبعة أيام. الخيار مخصص لرد موظف حقيقي على نفس الاستفسار ولا يسمح برد AI أو محتوى آلي غير متعلق.

راجع مصادر Meta الرسمية عند إعداد التطبيق: [Messenger Platform API](https://www.postman.com/meta/messenger-platform-api/folder/22794852-b5d97624-14d8-4e67-a2e4-529add49ca58)، و[Page subscribed_apps](https://developers.facebook.com/docs/graph-api/reference/page/subscribed_apps/)، و[HUMAN_AGENT](https://developers.facebook.com/docs/messenger-platform/send-messages/message-tags/#human_agent).

## التخزين والتشغيل

الإعدادات التي يديرها Admin محفوظة في الجدولين:

- `live_support_messenger_configurations`
- `live_support_messenger_pages`

الأسرار داخل أعمدة `bytea` مشفرة، وتتطلب أن تشترك عقد backend في Data Protection key ring نفسه. إعداد الإنتاج الحالي يثبت المفاتيح تحت مسار Data Protection المشترك؛ فقد هذا الـkey ring يمنع فك الأسرار المحفوظة ويستلزم إدخالها من جديد.

`FacebookMessenger:WebhookPublicUrl` هو إعداد تشغيل غير سري يحدد Callback URL المعروض في اللوحة. قيمته في الإنتاج:

```text
https://api.massar-academy.net/api/live-support/messenger/webhook
```

متغيرات `FACEBOOK_MESSENGER_*` القديمة تظل fallback انتقاليًا فقط عندما لا يوجد سجل إعدادات في قاعدة البيانات. بمجرد حفظ الإعداد من لوحة Admin تصبح قاعدة البيانات هي المصدر الموثوق، ولا يرجع التشغيل إلى توكنات البيئة عند تعطل صفحة أو حذفها.

قبل تشغيل النسخة الجديدة، طبّق migrations التالية بالترتيب الموجود في المشروع:

- `AddFacebookMessengerLiveSupportChannel`
- `AddFacebookMessengerAdminConfiguration`

## فحص القبول

1. اربط كل صفحة من لوحة Admin وتأكد أن حالتها تعرض توكنًا صالحًا واشتراكًا فعالًا.
2. أرسل رسالة من حساب Facebook حقيقي لا يحمل دورًا داخل Meta App إلى كل صفحة.
3. تأكد أن المحادثات تظهر باسم الصفحة الصحيح وأن رد الموظف يخرج من الصفحة نفسها.
4. أرسل من الحساب نفسه إلى صفحتين وتأكد أن النظام يعرض محادثتين منفصلتين.
5. فعّل سياسة AI منشورة في بيئة الاختبار، ثم أرسل رسالة Messenger وتأكد أن المحادثة تظل بشرية ولا يظهر أي رد AI.
6. اختبر انتهاء نافذة الرد؛ يجب أن تمنع الواجهة الإرسال، كما يرفض backend الإرسال المتأخر.
7. دوّر App Secret أو Verify Token من Admin وتأكد أن القيمة الجديدة تُستخدم فورًا من عقد backend من دون restart.

التحقق الآلي المركز:

```bash
dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj \
  --filter 'FullyQualifiedName~FacebookMessenger'

cd frontend
npm run check:live-support-contracts
```

لا تسجل Webhook payload أو PSID أو أي secret عند التشخيص. استخدم أكواد الفشل المنقحة وحالة صفوف inbox/outbound فقط.
عميل Graph المستخدم لفحص التوكن معطّل له تسجيل طلبات `HttpClient` لأن Meta يمرر التوكن المراد فحصه في query string؛ لا تفعّل تسجيل URL لهذا العميل في الإنتاج.

# نتيجة اختبار المزامنة والتحديثات — ٣ أكتوبر ٢٠٢٦

**قرار الإصدار: متوقف — لم يحدث رفع أو نشر أو بناء حزم برنامج جديدة.**

| الفحص | النتيجة |
| --- | --- |
| Flutter: الفحص الشامل بعد إصلاح التعليق | ٣٣٠ ناجحًا، ٢٦٣ فاشلًا؛ اكتمل خلال ٢:١٥ |
| المزامنة والتحديثات والسجلات وواجهتها وربط SQLite/LAN وشاشة الربط | ٧٦ اختبارًا ناجحًا، مشمولة في نتيجة الفحص الشامل |
| خدمة الدعم Go | ١٣ اختبارًا أساسيًا بحالات متعددة، مع race detector وvet ناجحين |
| بوابة LAN Go | race detector وvet ناجحان |
| التحليل الساكن للملفات المتأثرة | دون ملاحظات |
| فحص حالة الإنتاج للقراءة فقط | العقد الثلاث سليمة |

## ما أُصلح

- نوافذ الربط اليدوي وإدخال الرمز تحتفظ بمتحكم الإدخال حتى اكتمال خروج النافذة. كان التخلص منه قبل انتهاء الحركة يسبب استعمال متحكم متوقف.
- اختبارات الشاشة تنتظر انتقالات النوافذ انتظارًا محدودًا، وتنهي موارد الشبكة داخل سياق اختبار فعّال. أُزيل التعليق في الإغلاق دون إسقاط تأكيدات هوية الجهاز أو البصمة أو عدم حفظ بيانات الفرعي.
- اختبارات سجل المشاكل أصبحت تتحقق من إصدار البناء الفعلي ومن تصدير رأس صحيح عندما لا توجد أحداث، مع بقاء اختبارات منع التسرب واستبدال الملفات.
- أضيفت اختبارات فعلية لنسخة ثابتة من SQLite، وصلاحيات الموظف وقت الالتقاط، وانقطاع اتصال الفرعي، وحدود الرفع، وعدم تكرار الإرسال، وتحقق ملفات التحديث. بياناتها صناعية وفي مجلدات مؤقتة فقط.

## أسباب منع الرفع

الفشل الشامل لم يُتجاوز أو يتحول إلى skip. مراجعة العينات وجدت توقعات قديمة في بدء الحصة الصريح، وأنشطة الحصص المشتركة، والشهور المسماة، ومبلغ مراجعة الإيصال، والتنبيهات التي ألغيت. لا يكفي هذا التفسير لاعتبار كل إخفاق آمنًا؛ يجب تكييف السيناريوهات وإعادة إثبات النتائج المالية والحفظ وعدم التكرار قبل نشر التطبيق.

هناك تجهيز نشر مستقل مطلوب لخدمة الدعم أيضًا: مسار الإنتاج الحالي يدير صور منصة الويب ولا يتضمن خدمة Go الجديدة أو مسارها الخاص. أمثلة إعداد الخدمة ليست خدمة منشورة. لم تُنشر تغييرات الويب الأخرى الموجودة في مساحة العمل، ولم نمرر ملفات الطلبة أو مفاتيح الربط إلى مستودع عام. قبل نشر حزم التطبيق يلزم رقم إصدار أحدث، وتهيئة عنوان خدمة مسار ورموز الأجهزة الخاصة.

## توزيع الإخفاقات حسب ملفات الاختبار

| الملف | عدد الحالات الفاشلة |
| --- | --- |
| `features/attendance_workspace_test.dart` | 27 |
| `features/package_confirmation_sequence_test.dart` | 17 |
| `features/academic_quick_entry_panel_test.dart` | 11 |
| `features/entry_confirmation_test.dart` | 11 |
| `domain_academic_activities_test.dart` | 11 |
| `features/attendance_discount_shortcut_test.dart` | 10 |
| `comprehensive_management_scenarios_test.dart` | 10 |
| `features/academic_keyboard_workflow_test.dart` | 9 |
| `features/duplicate_attendance_warning_test.dart` | 9 |
| `features/closed_session_popup_test.dart` | 8 |
| `features/student_codes_editor_test.dart` | 8 |
| `features/comprehensive_ui_scenarios_test.dart` | 8 |
| `features/corrections_page_test.dart` | 8 |
| `report_engine_test.dart` | 8 |
| `named_academic_reports_test.dart` | 7 |
| `features/student_discount_dialog_test.dart` | 7 |
| `features/record_cancellation_dialog_test.dart` | 7 |
| `group_sessions_store_test.dart` | 6 |
| `features/reports_workspace_test.dart` | 6 |
| `finance_session_store_test.dart` | 6 |
| `features/attendance_session_review_test.dart` | 5 |
| `features/management_workspace_test.dart` | 5 |
| `features/session_review_page_test.dart` | 5 |
| `features/review_closing_test.dart` | 4 |
| `features/named_academics_test.dart` | 4 |
| `comprehensive_persistence_scenarios_test.dart` | 4 |
| `features/student_cards_focus_test.dart` | 3 |
| `features/cards_workspace_test.dart` | 3 |
| `lan_store_integration_test.dart` | 3 |
| `persistence_center_store_test.dart` | 3 |
| `domain_center_store_test.dart` | 3 |
| `domain_session_reopen_test.dart` | 3 |
| `installation_admin_test.dart` | 2 |
| `domain_session_categories_test.dart` | 2 |
| `features/problem_log_export_test.dart` | 2 |
| `features/closing_categories_test.dart` | 2 |
| `features/academics_quick_entry_test.dart` | 2 |
| `discount_store_test.dart` | 2 |
| `domain_corrections_test.dart` | 1 |
| `client_only_startup_test.dart` | 1 |
| `app/comprehensive_bootstrap_test.dart` | 1 |
| `app/appearance_test.dart` | 1 |
| `app/staff_login_test.dart` | 1 |
| `features/dialog_scrolling_test.dart` | 1 |
| `features/auth_notice_test.dart` | 1 |
| `features/comprehensive_backup_workflow_test.dart` | 1 |
| `card_finance_reports_test.dart` | 1 |
| `correction_reports_test.dart` | 1 |
| `notice_dialog_test.dart` | 1 |
| `problem_reporting_test.dart` | 1 |

السجل الكامل والنتائج المنظمة في `build/verification/support-release-20261003/`. لم تُفتح قاعدة المستخدم للتعديل أو الاستعادة أو الرفع.

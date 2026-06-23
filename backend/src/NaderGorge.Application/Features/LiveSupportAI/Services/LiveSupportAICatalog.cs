using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Services;

public static class LiveSupportAICatalog
{
    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> ReadableData = Items(
        Item("identity.basic", "البيانات الأساسية", "اسم الطالب وكود الحساب والبيانات التعريفية الآمنة."),
        Item("identity.contact", "بيانات التواصل", "بيانات التواصل المسموح بعرضها بعد تطبيق قواعد الخصوصية."),
        Item("account.status", "حالة الحساب", "حالة تفعيل الحساب وأسباب الإيقاف المتاحة للدعم."),
        Item("education.profile", "الملف التعليمي", "الصف الدراسي والمحافظة والمدرسة والبيانات التعليمية."),
        Item("packages.active", "الباقات المفعّلة", "الباقات الحالية وفترات صلاحيتها."),
        Item("access.grants", "صلاحيات الوصول", "المحتوى والدروس المتاحة للطالب."),
        Item("balance.summary", "ملخص الرصيد", "الرصيد والحركات الآمنة المرتبطة به."),
        Item("devices.summary", "ملخص الأجهزة", "الأجهزة والجلسات المسجلة دون بيانات تقنية حساسة."),
        Item("watch.summary", "ملخص المشاهدة", "مرات المشاهدة والتقدم والقيود الحالية."),
        Item("exams.summary", "ملخص الامتحانات", "المحاولات والنتائج والحالة الحالية للامتحانات."),
        Item("homework.summary", "ملخص الواجبات", "حالة الواجبات والمحاولات والنتائج."),
        Item("requests.summary", "ملخص الطلبات", "طلبات المشاهدة أو الدعم وحالتها الحالية."),
        Item("gamification.summary", "ملخص النقاط والإنجازات", "النقاط والمستويات والإنجازات المسجلة."),
        Item("notes.safe", "ملاحظات الدعم الآمنة", "الملاحظات المسموح للمساعد بقراءتها."),
        Item("crm.safe", "بيانات المتابعة الآمنة", "ملخص المتابعة والتواصل المسجل لخدمة الطالب."),
        Item("audit.safe_recent", "آخر الأنشطة الآمنة", "أحدث الأنشطة المرتبطة بالحساب بعد إخفاء البيانات الحساسة."));

    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> Actions = Items(true,
        Item("student.profile.update", "تحديث ملف الطالب", "اقتراح تعديل البيانات الشخصية أو التعليمية."),
        Item("student.password.reset", "إعادة تعيين كلمة المرور", "بدء إعادة تعيين آمنة دون إظهار كلمة المرور للمساعد."),
        Item("student.account.status.set", "تغيير حالة الحساب", "تفعيل الحساب أو إيقافه مع توضيح السبب."),
        Item("student.note.add", "إضافة ملاحظة دعم", "إضافة ملاحظة مرتبطة بمتابعة الطالب."),
        Item("student.note.delete", "حذف ملاحظة دعم", "حذف ملاحظة مسموح بحذفها."),
        Item("student.device.disconnect", "فصل جهاز", "إنهاء جلسة جهاز محدد."),
        Item("student.devices.disconnect-all", "فصل كل الأجهزة", "إنهاء جميع جلسات الطالب الحالية."),
        Item("student.package.cancel", "إلغاء باقة", "إلغاء باقة الطالب بعد توضيح الأثر."),
        Item("student.balance.adjust", "تعديل الرصيد", "اقتراح إضافة أو خصم رصيد بسبب واضح."),
        Item("student.gamification.adjust", "تعديل النقاط", "اقتراح تعديل نقاط أو مكافآت الطالب."),
        Item("student.video.override.add", "إضافة سماح لفيديو", "إتاحة مشاهدة إضافية لفيديو محدد."),
        Item("student.watch.reset", "إعادة ضبط المشاهدة", "إعادة ضبط سجل مشاهدة فيديو محدد."),
        Item("student.watch.count.set", "تحديد عدد المشاهدات", "تعديل عدد المشاهدات المسموح به."),
        Item("student.watch-request.approve", "قبول طلب مشاهدة", "الموافقة على طلب مشاهدة إضافية."),
        Item("student.watch-request.reject", "رفض طلب مشاهدة", "رفض طلب مشاهدة إضافية مع ذكر السبب."),
        Item("student.lesson.unlock", "فتح درس", "إتاحة درس محدد للطالب."),
        Item("student.crm.assign", "تعيين مسؤول متابعة", "ربط الطالب بمسؤول متابعة."),
        Item("student.crm.call.add", "تسجيل مكالمة متابعة", "إضافة نتيجة مكالمة إلى سجل المتابعة."),
        Item("student.create-and-link", "إنشاء حساب وربطه", "إنشاء حساب طالب وربطه بالمحادثة باستخدام حقول آمنة."));

    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> LookupKeys = Items(
        Item("phone.full", "رقم الهاتف كاملًا", "البحث بالتطابق الكامل دون عرض اقتراحات أو نتائج جزئية."),
        Item("student_code.full", "كود الطالب كاملًا", "البحث بكود الطالب كاملًا دون كشف وجود حساب."));

    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> VerificationQuestions = Items(
        Item("profile.full_name", "الاسم الكامل", "مطابقة الاسم الكامل المسجل بالحساب."),
        Item("profile.birth_date", "تاريخ الميلاد", "مطابقة تاريخ الميلاد المسجل."),
        Item("profile.governorate", "المحافظة", "مطابقة المحافظة المسجلة في الملف."),
        Item("profile.school_name", "اسم المدرسة", "مطابقة اسم المدرسة المسجل."),
        Item("contact.parent_phone_last4", "آخر 4 أرقام من هاتف ولي الأمر", "مطابقة آخر أربعة أرقام فقط من هاتف ولي الأمر."));

    public static LiveSupportAICatalogsDto Snapshot() => new(
        ReadableData.Values.ToArray(), Actions.Values.ToArray(), LookupKeys.Values.ToArray(), VerificationQuestions.Values.ToArray());

    private static LiveSupportAICatalogItemDto Item(string key, string label, string description) => new(key, label, description);

    private static IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> Items(params LiveSupportAICatalogItemDto[] catalogItems) =>
        catalogItems.ToDictionary(catalogItem => catalogItem.Key, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> Items(
        bool requiresVerification,
        params LiveSupportAICatalogItemDto[] catalogItems) =>
        catalogItems.ToDictionary(
            catalogItem => catalogItem.Key,
            catalogItem => catalogItem with { RequiresVerification = requiresVerification },
            StringComparer.Ordinal);
}

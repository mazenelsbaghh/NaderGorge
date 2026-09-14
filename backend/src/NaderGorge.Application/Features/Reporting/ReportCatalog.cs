namespace NaderGorge.Application.Features.Reporting;

public static class ReportCatalog
{
    private static readonly string[] TextOperators = ["eq", "neq", "contains", "in", "is-empty", "not-empty"];
    private static readonly string[] NumberOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between", "in"];
    private static readonly string[] DateOperators = ["eq", "before", "after", "between"];
    private static readonly string[] BooleanOperators = ["eq"];

    private static ReportFieldDto T(string key, string label, bool sensitive = false) => new(key, label, "text", TextOperators, sensitive);
    private static ReportFieldDto N(string key, string label) => new(key, label, "number", NumberOperators);
    private static ReportFieldDto D(string key, string label) => new(key, label, "date", DateOperators);
    private static ReportFieldDto B(string key, string label) => new(key, label, "boolean", BooleanOperators);
    private static ReportFieldDto[] StudentIdentityFields() =>
        [T("phone", "رقم الهاتف", true), T("stage", "المرحلة"), T("grade", "الصف"), T("studyTrack", "الشعبة")];

    public static readonly string[] StudentIdentityColumnKeys = ["phone", "stage", "grade", "studyTrack"];

    private static readonly IReadOnlyList<ReportDomainDto> AllDomains =
    [
        Domain(ReportDomains.StudentJourney, "رحلة الطالب", "الشراء والحضور والمشاهدة والامتحانات والواجبات في تقرير واحد", [T("studentName", "الطالب"), ..StudentIdentityFields(), T("packageName", "الكورس"), T("teacherName", "المدرس"), T("purchaseStatus", "حالة الشراء"), T("attendanceStatus", "الحضور"), T("videoStatus", "المشاهدة"), T("examStatus", "الامتحانات"), T("homeworkStatus", "الواجبات"), D("lastActivityAt", "آخر نشاط")], ["studentName", ..StudentIdentityColumnKeys, "packageName", "purchaseStatus", "attendanceStatus", "videoStatus", "examStatus", "homeworkStatus", "lastActivityAt"]),
        Domain(ReportDomains.Students, "الطلاب", "بيانات ونشاط الطلاب", [T("studentName", "اسم الطالب"), ..StudentIdentityFields(), T("secondaryPhone", "الهاتف البديل", true), T("studentCode", "كود الطالب"), T("governorate", "المحافظة"), T("district", "المنطقة"), T("address", "العنوان", true), T("schoolName", "المدرسة"), T("parentPhone", "هاتف ولي الأمر", true), T("motherPhone", "هاتف الأم", true), T("nationality", "الجنسية"), B("isActive", "الحساب فعال"), D("registeredAt", "تاريخ التسجيل")], ["studentName", ..StudentIdentityColumnKeys, "studentCode", "isActive", "registeredAt"]),
        Domain(ReportDomains.Purchases, "الشراء والوصول", "المشترون وغير المشترين المؤهلين وحالة مصدر الوصول", [T("studentName", "الطالب"), ..StudentIdentityFields(), T("packageId", "معرف الباقة"), T("packageName", "الباقة"), T("teacherName", "المدرس"), T("grantType", "نوع الوصول"), T("contentName", "المحتوى"), T("purchaseStatus", "حالة الشراء"), T("source", "المصدر"), B("isActive", "فعال"), D("grantedAt", "تاريخ المنح"), D("expiresAt", "الانتهاء")], ["studentName", ..StudentIdentityColumnKeys, "packageName", "teacherName", "purchaseStatus", "source", "expiresAt"]),
        Domain(ReportDomains.Codes, "الأكواد", "مجموعات الأكواد والاستخدام", [T("groupName", "المجموعة"), T("codeType", "النوع"), B("isConsumed", "مستخدم"), T("studentName", "الطالب"), ..StudentIdentityFields(), D("consumedAt", "وقت الاستخدام"), D("expiresAt", "الانتهاء")], ["groupName", "codeType", "isConsumed", "studentName", ..StudentIdentityColumnKeys, "consumedAt", "expiresAt"]),
        Domain(ReportDomains.BalanceRecharge, "الرصيد والشحن", "الأرصدة وطلبات الشحن", [T("recordType", "نوع السجل"), T("studentName", "الطالب"), ..StudentIdentityFields(), N("amount", "القيمة"), T("status", "الحالة"), T("transactionType", "نوع العملية"), D("createdAt", "التاريخ")], ["recordType", "studentName", ..StudentIdentityColumnKeys, "amount", "status", "createdAt"]),
        Domain(ReportDomains.Content, "المحتوى", "الكورسات والترمات والحصص والفيديوهات", [T("contentType", "نوع المحتوى"), T("name", "الاسم"), T("teacherName", "المدرس"), N("price", "السعر"), B("isActive", "فعال"), D("createdAt", "تاريخ الإضافة")], ["contentType", "name", "teacherName", "price", "isActive"]),
        Domain(ReportDomains.Engagement, "المشاهدة والتفاعل", "وقت المشاهدة والتقدم واستخدام الفيديو", [T("studentName", "الطالب"), ..StudentIdentityFields(), T("videoName", "الفيديو"), T("teacherName", "المدرس"), N("watchedSeconds", "ثواني المشاهدة"), N("watchCount", "عدد المشاهدات"), B("isLocked", "مغلق"), D("lastActivityAt", "آخر نشاط")], ["studentName", ..StudentIdentityColumnKeys, "videoName", "teacherName", "watchedSeconds", "watchCount", "isLocked", "lastActivityAt"]),
        Domain(ReportDomains.Attendance, "الحضور والغياب", "حضور الطالب يعني مشاهدة محتوى الكورس بعد حصوله على الصلاحية", [T("studentName", "الطالب"), ..StudentIdentityFields(), T("packageName", "الكورس"), T("teacherName", "المدرس"), T("attendanceStatus", "الحالة"), D("grantedAt", "تاريخ الحصول على الكورس"), D("lastActivityAt", "آخر مشاهدة")], ["studentName", ..StudentIdentityColumnKeys, "packageName", "attendanceStatus", "lastActivityAt"]),
        Domain(ReportDomains.Assessments, "الاختبارات والواجبات", "المحاولات والتسليم والنتائج", [T("assessmentType", "النوع"), T("title", "الاختبار/الواجب"), T("studentName", "الطالب"), ..StudentIdentityFields(), N("score", "الدرجة"), B("isPassed", "ناجح"), D("createdAt", "التاريخ")], ["assessmentType", "title", "studentName", ..StudentIdentityColumnKeys, "score", "isPassed", "createdAt"]),
        Domain(ReportDomains.TeachersFinance, "المدرسون والأرباح", "توزيعات الإيراد والأرباح", [T("teacherName", "المدرس"), T("studentName", "الطالب", true), ..StudentIdentityFields(), T("contentName", "المحتوى"), N("grossAmount", "الإجمالي"), N("teacherShare", "نصيب المدرس"), T("payoutStatus", "حالة الصرف"), D("occurredAt", "التاريخ")], ["teacherName", "studentName", ..StudentIdentityColumnKeys, "contentName", "grossAmount", "teacherShare", "payoutStatus", "occurredAt"]),
        Domain(ReportDomains.Staff, "الاستاف", "الحسابات الوظيفية والحالة", [T("employeeName", "الموظف"), T("phone", "الهاتف", true), T("role", "الدور"), B("isActive", "فعال"), D("createdAt", "تاريخ الإنشاء")], ["employeeName", "role", "isActive", "createdAt"]),
        Domain(ReportDomains.Support, "الدعم", "المحادثات وحالتها", [T("status", "الحالة"), T("channel", "القناة"), T("studentName", "الطالب", true), ..StudentIdentityFields(), D("createdAt", "تاريخ الإنشاء")], ["status", "channel", "studentName", ..StudentIdentityColumnKeys, "createdAt"]),
        Domain(ReportDomains.CommentsCommunity, "التعليقات والمجتمع", "تعليقات الدروس ومنشورات المجتمع", [T("recordType", "النوع"), T("authorName", "الكاتب"), ..StudentIdentityFields(), T("contentName", "المحتوى"), T("status", "الحالة"), D("createdAt", "التاريخ")], ["recordType", "authorName", ..StudentIdentityColumnKeys, "contentName", "status", "createdAt"]),
        Domain(ReportDomains.ParentTracking, "متابعة ولي الأمر", "حالة تفعيل متابعة ولي الأمر بدون كشف الكود", [T("studentName", "الطالب"), ..StudentIdentityFields(), B("hasCode", "لديه كود"), B("popupSeen", "شاهد التنبيه"), D("registeredAt", "تاريخ التسجيل")], ["studentName", ..StudentIdentityColumnKeys, "hasCode", "popupSeen", "registeredAt"]),
        Domain(ReportDomains.OperationsSecurity, "التشغيل والأمان", "سجل إجراءات النظام", [T("action", "الإجراء"), T("entityType", "نوع الكيان"), T("performedBy", "المنفذ", true), D("createdAt", "التاريخ")], ["action", "entityType", "performedBy", "createdAt"])
    ];

    private static ReportDomainDto Domain(string key, string label, string description, IReadOnlyList<ReportFieldDto> fields, IReadOnlyList<string> columns) =>
        new(key, label, description, true, fields, columns);

    private static readonly HashSet<string> TeacherDomains =
    [
        ReportDomains.StudentJourney, ReportDomains.Students, ReportDomains.Purchases, ReportDomains.Codes,
        ReportDomains.BalanceRecharge, ReportDomains.Content, ReportDomains.Engagement, ReportDomains.Assessments,
        ReportDomains.Attendance,
        ReportDomains.TeachersFinance, ReportDomains.CommentsCommunity, ReportDomains.ParentTracking
    ];

    public static ReportCatalogDto Get(bool isTeacher) => new(
        AllDomains.Where(domain => !isTeacher || TeacherDomains.Contains(domain.Key)).ToArray(),
        "Africa/Cairo");

    public static ReportDomainDto? Find(string domain, bool isTeacher) =>
        Get(isTeacher).Domains.FirstOrDefault(item => item.Key.Equals(domain, StringComparison.OrdinalIgnoreCase));
}

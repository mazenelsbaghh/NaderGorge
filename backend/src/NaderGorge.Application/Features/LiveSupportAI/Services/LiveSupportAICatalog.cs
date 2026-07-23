using System.Text.Json;
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

    private static readonly IReadOnlyDictionary<string, LiveSupportAIActionContract> ActionContracts = new Dictionary<string, LiveSupportAIActionContract>(StringComparer.Ordinal)
    {
        ["student.profile.update"] = Contract("تحديث ملف الطالب", "تعديل بيانات الطالب المسموح بها.", Schema(
            ["fullName", "string"], ["phone", "string"], ["parentPhone", "string"], ["governorate", "string"], ["schoolName", "string"], ["educationStage", "string"], ["gradeLevel", "string"])),
        ["student.password.reset"] = Contract("إعادة تعيين كلمة المرور", "بدء إعادة تعيين آمنة دون إظهار كلمة المرور للمساعد.", Schema(["newPassword", "string", true])),
        ["student.account.status.set"] = Contract("تغيير حالة الحساب", "تفعيل الحساب أو إيقافه مع توضيح السبب.", Schema(["isActive", "boolean", true])),
        ["student.note.add"] = Contract("إضافة ملاحظة دعم", "إضافة ملاحظة مرتبطة بمتابعة الطالب.", Schema(["content", "string", true], ["isPinned", "boolean"])),
        ["student.note.delete"] = Contract("حذف ملاحظة دعم", "حذف ملاحظة مسموح بحذفها.", Schema(["noteId", "guid", true])),
        ["student.device.disconnect"] = Contract("فصل جهاز", "إنهاء جلسة جهاز محدد.", Schema(["deviceId", "guid", true])),
        ["student.devices.disconnect-all"] = Contract("فصل كل الأجهزة", "إنهاء جميع جلسات الطالب الحالية.", Schema()),
        ["student.package.cancel"] = Contract("إلغاء باقة", "إلغاء باقة الطالب بعد توضيح الأثر.", Schema(["accessGrantId", "guid", true], ["refundBalance", "boolean", true], ["reason", "string", true])),
        ["student.balance.adjust"] = Contract("تعديل الرصيد", "اقتراح إضافة أو خصم رصيد بسبب واضح.", Schema(["amount", "number", true], ["reason", "string", true])),
        ["student.gamification.adjust"] = Contract("تعديل النقاط", "اقتراح تعديل نقاط أو مكافآت الطالب.", Schema(["points", "integer", true], ["reason", "string", true])),
        ["student.video.override.add"] = Contract("إضافة سماح لفيديو", "إتاحة مشاهدة إضافية لفيديو محدد.", Schema(["videoId", "guid", true], ["addedViews", "integer", true], ["reason", "string", true])),
        ["student.watch.reset"] = Contract("إعادة ضبط المشاهدة", "إعادة ضبط سجل مشاهدة فيديو محدد.", Schema(["lessonVideoId", "guid", true])),
        ["student.watch.count.set"] = Contract("تحديد عدد المشاهدات", "تعديل عدد المشاهدات المسموح به.", Schema(["lessonVideoId", "guid", true], ["newWatchCount", "integer", true])),
        ["student.watch-request.approve"] = Contract("قبول طلب مشاهدة", "الموافقة على طلب مشاهدة إضافية.", Schema(["requestId", "guid", true], ["addedViews", "integer"], ["reason", "string"])),
        ["student.watch-request.reject"] = Contract("رفض طلب مشاهدة", "رفض طلب مشاهدة إضافية مع ذكر السبب.", Schema(["requestId", "guid", true], ["reason", "string", true])),
        ["student.lesson.unlock"] = Contract("فتح درس", "إتاحة درس محدد للطالب.", Schema(["lessonId", "guid", true])),
        ["student.crm.assign"] = Contract("تعيين مسؤول متابعة", "ربط الطالب بمسؤول متابعة.", Schema(["assignedAgentId", "guid"], ["priority", "string", true], ["notes", "string"])),
        ["student.crm.call.add"] = Contract("تسجيل مكالمة متابعة", "إضافة نتيجة مكالمة إلى سجل المتابعة.", Schema(["outcome", "string", true], ["notes", "string"], ["nextFollowUpDate", "date"])),
        ["student.create-and-link"] = Contract("إنشاء حساب وربطه", "إنشاء حساب طالب وربطه بالمحادثة باستخدام حقول آمنة.", Schema(
            ["fullName", "string", true], ["phoneNumber", "string", true], ["password", "string", true], ["reason", "string", true], ["packageIds", "guid[]"] , ["governorate", "string"], ["educationStage", "string"], ["gradeLevel", "string"], ["schoolName", "string"], ["parentPhoneNumber", "string"] ))
    };

    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> Actions = ActionContracts.ToDictionary(
        item => item.Key,
        item => new LiveSupportAICatalogItemDto(item.Key, item.Value.Label, item.Value.Description, true),
        StringComparer.Ordinal);

    public static JsonElement GetArgumentsSchema(string key) =>
        ActionContracts.TryGetValue(key, out var contract)
            ? JsonDocument.Parse(contract.ArgumentsSchemaJson).RootElement.Clone()
            : throw new InvalidOperationException("ACTION_NOT_IMPLEMENTED");

    public static void ValidateActionArguments(string key, JsonElement arguments)
    {
        if (!ActionContracts.TryGetValue(key, out var contract))
            throw new InvalidOperationException("ACTION_NOT_IMPLEMENTED");
        contract.Validate(arguments);
    }

    private static LiveSupportAIActionContract Contract(string label, string description, string schema) => new(label, description, schema);

    private static string Schema(params object[][] fields)
    {
        var properties = fields.ToDictionary(field => (string)field[0], field => (object)PropertySchema((string)field[1]), StringComparer.Ordinal);
        var required = fields.Where(field => field.Length > 2 && Convert.ToBoolean(field[2])).Select(field => (string)field[0]).ToArray();
        return JsonSerializer.Serialize(new { type = "object", additionalProperties = false, properties, required });
    }

    private static object PropertySchema(string type) => type switch
    {
        "guid" => new { type = "string", format = "uuid" },
        "date" => new { type = "string", format = "date-time" },
        "guid[]" => new { type = "array", items = new { type = "string", format = "uuid" } },
        _ => new { type }
    };
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

public sealed record LiveSupportAIActionContract(string Label, string Description, string ArgumentsSchemaJson)
{
    public void Validate(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("ACTION_ARGUMENTS_INVALID");
        using var schema = JsonDocument.Parse(ArgumentsSchemaJson);
        var properties = schema.RootElement.GetProperty("properties");
        ValidateRequired(arguments, schema.RootElement.GetProperty("required"));
        ValidatePropertyNames(arguments, properties);
        foreach (var property in arguments.EnumerateObject()) ValidateProperty(property, properties.GetProperty(property.Name));
    }

    private static void ValidateRequired(JsonElement arguments, JsonElement required)
    {
        foreach (var property in required.EnumerateArray())
            if (!arguments.TryGetProperty(property.GetString()!, out var value) || value.ValueKind is JsonValueKind.Null || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
                throw new InvalidOperationException("ACTION_ARGUMENTS_INVALID");
    }

    private static void ValidatePropertyNames(JsonElement arguments, JsonElement properties)
    {
        foreach (var property in arguments.EnumerateObject())
            if (!properties.TryGetProperty(property.Name, out _)) throw new InvalidOperationException("ACTION_ARGUMENTS_INVALID");
    }

    private static void ValidateProperty(JsonProperty property, JsonElement definition)
    {
        var type = definition.GetProperty("type").GetString();
        var valid = type switch
        {
            "string" => property.Value.ValueKind == JsonValueKind.String,
            "boolean" => property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => property.Value.TryGetInt32(out _),
            "number" => property.Value.TryGetDecimal(out _),
            "array" => property.Value.ValueKind == JsonValueKind.Array && property.Value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out _)),
            _ => false
        };
        if (!valid || (definition.TryGetProperty("format", out var format) && !Guid.TryParse(property.Value.GetString(), out _) && format.GetString() == "uuid") ||
            (definition.TryGetProperty("format", out format) && !DateTime.TryParse(property.Value.GetString(), out _) && format.GetString() == "date-time"))
            throw new InvalidOperationException("ACTION_ARGUMENTS_INVALID");
    }
}

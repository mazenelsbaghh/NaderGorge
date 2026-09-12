using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public sealed record AssessmentResultParameter(string Source, string? Literal = null);

public sealed record AssessmentParentNotificationSettings(
    bool Enabled, Guid? TemplateId, string? TemplateFingerprint, AssessmentResultParameter[] Parameters)
{
    public static readonly AssessmentParentNotificationSettings Disabled = new(false, null, null, []);
    public static AssessmentParentNotificationSettings Read(string? json) => json is null
        ? Disabled : JsonSerializer.Deserialize<AssessmentParentNotificationSettings>(json)
            ?? throw new InvalidOperationException("Invalid assessment notification settings.");
    public string? ToJson() => Enabled ? JsonSerializer.Serialize(this) : null;

    public async Task<string?> AuthorizeChangeAsync(
        IAppDbContext db, Guid? actorId, AssessmentParentNotificationSettings current, CancellationToken ct)
    {
        if (ToJson() == current.ToJson()) return null;
        var isAdmin = actorId.HasValue && await db.Users.AsNoTracking().AnyAsync(user =>
            user.Id == actorId.Value && user.IsActive &&
            user.UserRoles.Any(role => role.Role.Type == NaderGorge.Domain.Enums.RoleType.Admin), ct);
        return isAdmin ? null : "إعداد إرسال واتساب للامتحان والواجب متاح للأدمن فقط.";
    }

    public async Task<string?> ValidateAsync(IAppDbContext db, CancellationToken ct)
    {
        if (!Enabled) return null;
        if (TemplateId is null || TemplateId == Guid.Empty || Parameters is null || Parameters.Length > 30)
            return "اختر قالب رسالة النتيجة وحدد قيم متغيراته.";
        if (Parameters.Any(parameter => !ValidParameter(parameter)))
            return "راجع مصادر متغيرات رسالة النتيجة.";
        var template = await db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(template => template.Id == TemplateId, ct);
        if (template is null || template.Category != "UTILITY" || template.Fingerprint != TemplateFingerprint)
            return "اختر قالب خدمة معتمدًا ومحدّثًا لرسالة النتيجة.";
        return WhatsAppDirectTemplatePolicy.Validate(template, Parameters.Select(_ => "قيمة").ToArray()) is null
            ? "القالب غير معتمد أو تركيب متغيراته غير مدعوم. اختر قالبًا نصيًا لرسالة النتيجة." : null;
    }

    private static bool ValidParameter(AssessmentResultParameter? parameter) => parameter is not null && parameter.Source switch
    {
        "ParentName" or "StudentName" or "ParentTrackingCode" or "AssessmentName" or "Score" or "TotalScore" or "Percentage"
            or "Evaluation" or "SubjectName" or "LessonName" or "TeacherName" => parameter.Literal is null,
        "Literal" => !string.IsNullOrWhiteSpace(parameter.Literal) && parameter.Literal.Length <= 1000
            && !parameter.Literal.Any(char.IsControl),
        _ => false
    };
}

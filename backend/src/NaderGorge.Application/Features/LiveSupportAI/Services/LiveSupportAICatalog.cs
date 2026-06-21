using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Services;

public static class LiveSupportAICatalog
{
    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> ReadableData = Items(
        "identity.basic", "identity.contact", "account.status", "education.profile", "packages.active",
        "access.grants", "balance.summary", "devices.summary", "watch.summary", "exams.summary",
        "homework.summary", "requests.summary", "gamification.summary", "notes.safe", "crm.safe", "audit.safe_recent");

    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> Actions = Items([
        "student.profile.update", "student.password.reset", "student.account.status.set", "student.note.add",
        "student.note.delete", "student.device.disconnect", "student.devices.disconnect-all", "student.package.cancel",
        "student.balance.adjust", "student.gamification.adjust", "student.video.override.add", "student.watch.reset",
        "student.watch.count.set", "student.watch-request.approve", "student.watch-request.reject",
        "student.lesson.unlock", "student.crm.assign", "student.crm.call.add", "student.create-and-link"], true);

    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> LookupKeys = Items("phone.full", "student_code.full");
    public static readonly IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> VerificationQuestions = Items(
        "profile.full_name", "profile.birth_date", "profile.governorate", "profile.school_name", "contact.parent_phone_last4");

    public static LiveSupportAICatalogsDto Snapshot() => new(
        ReadableData.Values.ToArray(), Actions.Values.ToArray(), LookupKeys.Values.ToArray(), VerificationQuestions.Values.ToArray());

    private static IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> Items(params string[] keys) => Items(keys, false);

    private static IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> Items(string[] keys, bool requiresVerification) =>
        keys.ToDictionary(key => key, key => new LiveSupportAICatalogItemDto(key, key, key, requiresVerification), StringComparer.Ordinal);
}

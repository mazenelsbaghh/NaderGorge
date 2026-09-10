using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Common;

public static class ParentWhatsAppRecipients
{
    public const string SettingKey = "ParentWhatsAppPhonePriority";
    public const string DefaultPriority = "FatherSecondary,FatherPrimary,Mother";

    public static bool IsValidPriority(string? priority)
    {
        if (priority is null) return false;
        var roles = priority.Split(',');
        return roles.Length == 3 && roles.Distinct(StringComparer.Ordinal).Count() == 3
            && roles.All(role => role is "FatherSecondary" or "FatherPrimary" or "Mother");
    }

    public static async Task<string> ReadPriorityAsync(IAppDbContext db, CancellationToken ct) =>
        await db.PlatformSettings.AsNoTracking().Where(setting => setting.Key == SettingKey)
            .Select(setting => setting.Value).SingleOrDefaultAsync(ct) ?? DefaultPriority;

    public static string? Resolve(StudentProfile? profile, string priority)
    {
        if (!IsValidPriority(priority)) throw new InvalidOperationException("Invalid parent phone priority.");
        foreach (var role in priority.Split(','))
        {
            var phone = role switch
            {
                "FatherSecondary" => profile?.SecondaryParentPhone,
                "FatherPrimary" => profile?.ParentPhone,
                "Mother" => profile?.MotherPhone,
                _ => null
            };
            if (EgyptMobilePhone.Normalize(phone) is { } normalized) return normalized;
        }
        return null;
    }
}

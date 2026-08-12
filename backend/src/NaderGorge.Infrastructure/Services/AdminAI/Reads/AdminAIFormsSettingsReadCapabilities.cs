using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIFormsSettingsSummary(int Forms, int Submissions, int SafeSettings, int PopupSettings, int Notifications, int ParentDevices, int InternalChatRooms, int SupportConversations, DateTime DataAsOf);
public sealed class AdminAIFormsSettingsSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "forms-settings.summary"; public Type OutputType => typeof(AdminAIFormsSettingsSummary);
    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAIFormsSettingsSummary(
            await db.CustomForms.AsNoTracking().CountAsync(ct),
            await db.FormSubmissions.AsNoTracking().CountAsync(ct),
            await db.PlatformSettings.AsNoTracking().CountAsync(ct),
            await db.PlatformSettings.AsNoTracking().CountAsync(setting => setting.Key.StartsWith("PlatformPopup"), ct),
            await db.NotificationEvents.AsNoTracking().CountAsync(ct),
            await db.ParentDeviceTokens.AsNoTracking().CountAsync(ct),
            await db.ChatRooms.AsNoTracking().CountAsync(ct),
            await db.LiveSupportConversations.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.forms", "admin.settings"]);
    }
}

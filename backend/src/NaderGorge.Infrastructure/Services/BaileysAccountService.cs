using NaderGorge.Application.Features.LiveSupport.Interfaces;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed record BaileysAccountDto(Guid Id, string Name, string Status, string? PhoneNumber, bool IsEnabled);
public sealed record BaileysConnectionDto(BaileysAccountDto Account, string? QrDataUrl, DateTime? QrExpiresAt);

public sealed class BaileysAccountService(IAppDbContext db, BaileysWhatsAppClient client)
{
    public async Task<IReadOnlyList<BaileysAccountDto>> ListAsync(CancellationToken ct) =>
        await db.LiveSupportWhatsAppAccounts.AsNoTracking().OrderBy(account => account.CreatedAt)
            .Select(account => new BaileysAccountDto(account.Id, account.Name, account.Status, account.PhoneNumber, account.IsEnabled))
            .ToListAsync(ct);

    public async Task<BaileysAccountDto> CreateAsync(Guid actor, string name, CancellationToken ct)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80) throw new LiveSupportException("VALIDATION_ERROR", "اكتب اسمًا للرقم، بحد أقصى 80 حرفًا.");
        if (!client.IsConfigured) throw new LiveSupportException("BAILEYS_NOT_CONFIGURED", "اتصال واتساب QR غير مهيأ على الخادم.");
        var account = new LiveSupportWhatsAppAccount { Name = name, CreatedByUserId = actor, Version = 1 };
        account.InstanceName = "massar-support-" + account.Id.ToString("N");
        db.LiveSupportWhatsAppAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return Map(account);
    }

    public async Task<BaileysConnectionDto> ConnectAsync(Guid id, CancellationToken ct)
    {
        var account = await RequireAsync(id, ct);
        try { await client.StateAsync(account.InstanceName, ct); }
        catch (LiveSupportException exception) when (exception.Code == "BAILEYS_INSTANCE_NOT_FOUND")
        {
            await client.CreateAsync(account.InstanceName, ct);
        }
        var response = await client.ConnectAsync(account.InstanceName, ct);
        var qr = BaileysWhatsAppClient.Text(response, "base64");
        if (qr is not null && (!qr.StartsWith("data:image/png;base64,", StringComparison.Ordinal) || qr.Length > 300_000))
            throw new LiveSupportException("BAILEYS_INVALID_QR", "تعذر تحميل رمز الربط. حاول مجددًا.");
        var state = response.TryGetProperty("instance", out var instance) ? BaileysWhatsAppClient.Text(instance, "state") : null;
        account.Status = state == "open" ? "Connected" : qr is null ? "Connecting" : "AwaitingQr";
        account = await SaveStateAsync(account, true, ct);
        if (account.Status == "Connected") qr = null;
        return new(Map(account), qr, qr is null ? null : DateTime.UtcNow.AddSeconds(30));
    }

    public async Task<BaileysAccountDto> RefreshAsync(Guid id, CancellationToken ct)
    {
        var account = await RequireAsync(id, ct);
        var response = await client.StateAsync(account.InstanceName, ct);
        var state = response.TryGetProperty("instance", out var instance) ? BaileysWhatsAppClient.Text(instance, "state") : null;
        account.Status = state == "open" ? "Connected" : state == "connecting" ? "Connecting" : "Disconnected";
        return Map(await SaveStateAsync(account, null, ct));
    }

    public async Task<BaileysAccountDto> DisconnectAsync(Guid id, CancellationToken ct)
    {
        var account = await RequireAsync(id, ct);
        await client.LogoutAsync(account.InstanceName, ct);
        account.Status = "Disconnected";
        return Map(await SaveStateAsync(account, false, ct));
    }

    private async Task<LiveSupportWhatsAppAccount> SaveStateAsync(
        LiveSupportWhatsAppAccount observed, bool? enabled, CancellationToken ct)
    {
        // Webhooks may advance the connection while the bridge request is in flight.
        // Preserve their newer state; logout still explicitly disables the account.
        await db.LiveSupportWhatsAppAccounts.Where(account => account.Id == observed.Id)
            .ExecuteUpdateAsync(update => update
                .SetProperty(account => account.Status, account =>
                    enabled == false || account.Version == observed.Version ? observed.Status : account.Status)
                .SetProperty(account => account.IsEnabled, account => enabled ?? account.IsEnabled)
                .SetProperty(account => account.UpdatedAt, DateTime.UtcNow)
                .SetProperty(account => account.Version, account => account.Version + 1), ct);
        return await RequireAsync(observed.Id, ct);
    }

    private async Task<LiveSupportWhatsAppAccount> RequireAsync(Guid id, CancellationToken ct) =>
        await db.LiveSupportWhatsAppAccounts.AsNoTracking().SingleOrDefaultAsync(account => account.Id == id, ct)
        ?? throw new LiveSupportException("NOT_FOUND", "رقم واتساب غير موجود.");

    private static BaileysAccountDto Map(LiveSupportWhatsAppAccount account) =>
        new(account.Id, account.Name, account.Status, account.PhoneNumber, account.IsEnabled);
}

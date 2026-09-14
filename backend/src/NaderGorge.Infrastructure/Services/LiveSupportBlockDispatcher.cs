using NaderGorge.Application.Features.LiveSupport.Interfaces;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportBlockDispatcher(IAppDbContext db, WhatsAppCloudService cloud, BaileysWhatsAppClient baileys)
{
    public async Task DispatchAsync(Guid id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var claimed = await db.LiveSupportBlockDeliveries.Where(item => item.Id == id && item.Status == "Pending" &&
            !db.LiveSupportBlockDeliveries.Any(other => other.Id != item.Id && other.PhoneNumber == item.PhoneNumber &&
                other.AccountId == item.AccountId && (other.Status == "Processing" ||
                    other.Status == "Pending" && (other.CreatedAt < item.CreatedAt ||
                        other.CreatedAt == item.CreatedAt && other.Id.CompareTo(item.Id) < 0))))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Processing")
                .SetProperty(item => item.ClaimedAt, now).SetProperty(item => item.Version, item => item.Version + 1), ct);
        if (claimed == 0) return;
        var delivery = await db.LiveSupportBlockDeliveries.SingleAsync(item => item.Id == id, ct);
        var block = await db.LiveSupportContactBlocks.AsNoTracking().SingleAsync(item => item.Id == delivery.BlockId, ct);
        var activeBlock = await db.LiveSupportContactBlocks.AsNoTracking()
            .Where(item => item.PhoneNumber == delivery.PhoneNumber && item.UnblockedAt == null)
            .OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(ct);
        if (delivery.DesiredBlocked ? activeBlock?.Id != block.Id : activeBlock is not null)
        {
            delivery.Status = "Superseded";
            delivery.UpdatedAt = now;
            delivery.Version++;
            await db.SaveChangesAsync(ct);
            return;
        }
        try
        {
            var account = delivery.AccountId.HasValue
                ? await db.LiveSupportWhatsAppAccounts.AsNoTracking().SingleAsync(item => item.Id == delivery.AccountId, ct) : null;
            if (account is { IsEnabled: false }) throw new LiveSupportException("BAILEYS_DISCONNECTED", "رقم واتساب غير متصل.");
            if (delivery.DesiredBlocked && delivery.NoticeStatus == "Pending")
            {
                delivery.NoticeStatus = "Uncertain";
                await db.SaveChangesAsync(ct);
                var text = "تم حظرك من الدعم. السبب: " + block.Reason;
                try
                {
                    var notice = account is null ? await cloud.SendTextAsync(delivery.PhoneNumber, text, ct)
                        : await baileys.SendTextAsync(account.InstanceName, delivery.PhoneNumber, text, ct);
                    delivery.NoticeStatus = notice.Success ? "Sent" : "Failed";
                }
                catch (LiveSupportException) { delivery.NoticeStatus = "Uncertain"; }
                catch (WhatsAppCloudService.WhatsAppCloudException) { delivery.NoticeStatus = "Failed"; }
                catch (HttpRequestException) { delivery.NoticeStatus = "Uncertain"; }
                await db.SaveChangesAsync(ct);
            }
            if (account is null) await cloud.SetBlockedAsync(delivery.PhoneNumber, delivery.DesiredBlocked, ct);
            else await baileys.SetBlockedAsync(account.InstanceName, delivery.PhoneNumber, delivery.DesiredBlocked, ct);
            delivery.Status = "Succeeded";
            delivery.FailureCode = null;
        }
        catch (LiveSupportException exception) { delivery.Status = "Failed"; delivery.FailureCode = exception.Code; }
        catch (WhatsAppCloudService.WhatsAppCloudException exception) { delivery.Status = "Failed"; delivery.FailureCode = exception.ErrorCode; }
        catch (HttpRequestException) { delivery.Status = "Failed"; delivery.FailureCode = "WHATSAPP_BLOCK_UNCERTAIN"; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { delivery.Status = "Failed"; delivery.FailureCode = "WHATSAPP_BLOCK_UNCERTAIN"; }
        delivery.UpdatedAt = DateTime.UtcNow;
        delivery.Version++;
        await db.SaveChangesAsync(ct);
    }
}

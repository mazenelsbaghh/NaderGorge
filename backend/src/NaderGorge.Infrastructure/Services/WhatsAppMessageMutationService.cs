using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class WhatsAppMessageMutationService(IAppDbContext db, BaileysWhatsAppClient client)
{
    public async Task ApplyAsync(LiveSupportMessage message, string? content, CancellationToken ct)
    {
        var delivery = await db.LiveSupportWhatsAppMessages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.LiveSupportMessageId == message.Id, ct);
        if (delivery is null) return;
        var binding = await db.LiveSupportWhatsAppBindings.AsNoTracking()
            .SingleAsync(item => item.ConversationId == message.ConversationId, ct);
        var account = await db.LiveSupportWhatsAppAccounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == binding.AccountId && item.IsEnabled, ct);
        if (account is null || delivery.Direction != "Outbound" || string.IsNullOrEmpty(delivery.MetaMessageId))
            throw new LiveSupportException("WHATSAPP_MUTATION_UNAVAILABLE", "تعديل وحذف الرسائل متاحان للرسائل المرسلة عبر واتساب QR بعد تأكيد إرسالها.");
        var prefix = $"baileys:{account.InstanceName}:";
        if (!delivery.MetaMessageId.StartsWith(prefix, StringComparison.Ordinal))
            throw new LiveSupportException("WHATSAPP_MESSAGE_ACCOUNT_MISMATCH", "الرسالة لا تتبع رقم واتساب الحالي.");
        await client.MutateTextAsync(account.InstanceName,
            new(binding.WhatsAppUserId, delivery.MetaMessageId[prefix.Length..], content), ct);
    }
}

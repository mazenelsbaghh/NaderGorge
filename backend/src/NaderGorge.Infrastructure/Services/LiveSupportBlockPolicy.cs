using NaderGorge.Application.Features.LiveSupport.Interfaces;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public static class LiveSupportBlockPolicy
{
    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 11 && digits.StartsWith("01", StringComparison.Ordinal)
            ? "20" + digits[1..] : digits;
    }

    public static IQueryable<LiveSupportContactBlock> ForParticipant(
        IAppDbContext db, Guid? studentId, Guid? guestId, string? phone = null) =>
        db.LiveSupportContactBlocks.Where(block => block.UnblockedAt == null &&
            ((studentId != null && block.StudentUserId == studentId) ||
             (guestId != null && block.GuestSessionId == guestId) ||
             (phone != null && block.PhoneNumber == phone)));

    public static async Task<LiveSupportContactBlock?> FindAsync(
        IAppDbContext db, LiveSupportConversation conversation, CancellationToken ct)
    {
        var phone = await db.LiveSupportWhatsAppBindings.AsNoTracking()
            .Where(binding => binding.ConversationId == conversation.Id)
            .Select(binding => binding.WhatsAppUserId).SingleOrDefaultAsync(ct);
        return await ForParticipant(db, conversation.StudentUserId ?? conversation.LinkedStudentUserId, conversation.GuestSessionId, phone)
            .OrderByDescending(block => block.CreatedAt).FirstOrDefaultAsync(ct);
    }

    public static async Task EnsureAllowedAsync(
        IAppDbContext db, LiveSupportConversation conversation, CancellationToken ct)
    {
        var block = await FindAsync(db, conversation, ct);
        if (block is not null)
            throw new LiveSupportException("SUPPORT_BLOCKED", "تم حظرك من الدعم. السبب: " + block.Reason);
    }
}

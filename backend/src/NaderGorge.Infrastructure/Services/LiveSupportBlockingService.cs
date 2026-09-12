using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed record SupportBlockRequest(bool Blocked, string? Reason, long ExpectedVersion);
public sealed record SupportBlockStatus(LiveSupportContactBlock? Block, IReadOnlyList<LiveSupportBlockDelivery> Deliveries, long ConversationVersion);

public sealed class LiveSupportBlockingService(IAppDbContext db, ILiveSupportEventWriter events)
{
    public async Task<SupportBlockStatus> GetAsync(Guid conversationId, CancellationToken ct)
    {
        var conversation = await RequireConversationAsync(conversationId, ct);
        var block = await LiveSupportBlockPolicy.FindAsync(db, conversation, ct)
            ?? await db.LiveSupportContactBlocks.Where(item => item.ConversationId == conversationId)
                .OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(ct);
        var deliveries = block is null ? [] : await db.LiveSupportBlockDeliveries.AsNoTracking()
            .Where(item => item.BlockId == block.Id).OrderBy(item => item.CreatedAt).ToListAsync(ct);
        return new(block, deliveries, conversation.Version);
    }

    public async Task<SupportBlockStatus> SetAsync(Guid conversationId, Guid actor, SupportBlockRequest request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (request.Blocked && (string.IsNullOrWhiteSpace(reason) || reason.Length > 500))
            throw new LiveSupportException("VALIDATION_ERROR", "اكتب سبب الحظر، بحد أقصى 500 حرف.");
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var conversation = await RequireConversationAsync(conversationId, ct);
        if (conversation.Version != request.ExpectedVersion)
            throw new LiveSupportException("VERSION_CONFLICT", "تغيرت المحادثة. حدّثها ثم حاول مجددًا.");
        var binding = await db.LiveSupportWhatsAppBindings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ConversationId == conversationId, ct);
        var studentId = conversation.StudentUserId ?? conversation.LinkedStudentUserId;
        var phone = binding?.WhatsAppUserId;
        if (phone is null && studentId.HasValue)
        {
            var registeredPhone = await db.Users.Where(user => user.Id == studentId).Select(user => user.PhoneNumber).SingleOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(registeredPhone)) phone = LiveSupportBlockPolicy.NormalizePhone(registeredPhone);
        }
        var blocks = await LiveSupportBlockPolicy.ForParticipant(db, studentId,
            conversation.GuestSessionId, phone).ToListAsync(ct);
        if (phone is not null && await db.LiveSupportBlockDeliveries.AnyAsync(item => item.PhoneNumber == phone && item.Status == "Processing", ct))
            throw new LiveSupportException("VERSION_CONFLICT", "تنفيذ حظر واتساب جارٍ. حاول مجددًا بعد لحظات.");
        if (request.Blocked && blocks.Count > 0)
            throw new LiveSupportException("VERSION_CONFLICT", "الشخص محظور بالفعل.");
        if (request.Blocked)
        {
            var block = new LiveSupportContactBlock
            {
                ConversationId = conversationId, StudentUserId = studentId,
                GuestSessionId = conversation.GuestSessionId, PhoneNumber = phone,
                Reason = reason!, BlockedByUserId = actor, Version = 1
            };
            db.LiveSupportContactBlocks.Add(block);
            await CloseBlockedConversationsAsync(block, actor, ct);
            if (phone is not null)
            {
                var accountIds = await db.LiveSupportWhatsAppBindings.AsNoTracking()
                    .Where(item => item.WhatsAppUserId == phone)
                    .Select(item => item.AccountId).Distinct().ToListAsync(ct);
                foreach (var accountId in accountIds)
                    db.LiveSupportBlockDeliveries.Add(new LiveSupportBlockDelivery
                    {
                        BlockId = block.Id, AccountId = accountId,
                        PhoneNumber = phone, Version = 1
                    });
            }
        }
        else
        {
            var blockIds = blocks.Select(block => block.Id).ToArray();
            var deliveries = await db.LiveSupportBlockDeliveries.Where(item => blockIds.Contains(item.BlockId)).ToListAsync(ct);
            if (deliveries.Any(item => item.Status == "Processing"))
                throw new LiveSupportException("VERSION_CONFLICT", "تنفيذ حظر واتساب جارٍ. حاول فك الحظر بعد لحظات.");
            foreach (var block in blocks)
            {
                block.UnblockedAt = DateTime.UtcNow;
                block.UnblockedByUserId = actor;
                block.Version++;
            }
            foreach (var delivery in deliveries)
            {
                delivery.DesiredBlocked = false;
                delivery.Status = "Pending";
                delivery.FailureCode = null;
                delivery.Version++;
            }
        }
        conversation.Version++;
        conversation.AllowsAI = false;
        await events.AppendAsync(new(conversationId, LiveSupportEventType.AdminIntervened,
            ActorUserId: actor, SafeMetadataJson: JsonSerializer.Serialize(new
            {
                operation = request.Blocked ? "support_block" : "support_unblock",
                reason = request.Blocked ? reason : null
            })), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(conversationId, ct);
    }

    public async Task<SupportBlockStatus> RetryAsync(Guid conversationId, CancellationToken ct)
    {
        var status = await GetAsync(conversationId, ct);
        if (status.Block is null) throw new LiveSupportException("NOT_FOUND", "لا يوجد طلب حظر لهذه المحادثة.");
        await db.LiveSupportBlockDeliveries.Where(item => item.BlockId == status.Block.Id && item.Status == "Failed")
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Pending")
                .SetProperty(item => item.FailureCode, (string?)null)
                .SetProperty(item => item.Version, item => item.Version + 1), ct);
        return await GetAsync(conversationId, ct);
    }

    private async Task<LiveSupportConversation> RequireConversationAsync(Guid id, CancellationToken ct) =>
        await db.LiveSupportConversations.SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");

    private async Task CloseBlockedConversationsAsync(LiveSupportContactBlock block, Guid actor, CancellationToken ct)
    {
        var conversations = await db.LiveSupportConversations.Where(conversation =>
            conversation.Status != LiveSupportConversationStatus.Closed && conversation.Status != LiveSupportConversationStatus.Abandoned &&
            (block.StudentUserId != null && (conversation.StudentUserId == block.StudentUserId || conversation.LinkedStudentUserId == block.StudentUserId) ||
             block.GuestSessionId != null && conversation.GuestSessionId == block.GuestSessionId ||
             block.PhoneNumber != null && db.LiveSupportWhatsAppBindings.Any(binding =>
                 binding.ConversationId == conversation.Id && binding.WhatsAppUserId == block.PhoneNumber))).ToListAsync(ct);
        var ids = conversations.Select(conversation => conversation.Id).ToArray();
        var now = DateTime.UtcNow;
        foreach (var assignment in await db.LiveSupportAssignments.Where(item => ids.Contains(item.ConversationId) && item.EndedAt == null).ToListAsync(ct))
        { assignment.EndedAt = now; assignment.EndReason = LiveSupportAssignmentEndReason.Closed; }
        foreach (var queue in await db.LiveSupportQueueEntries.Where(item => ids.Contains(item.ConversationId) && item.DequeuedAt == null).ToListAsync(ct))
        { queue.DequeuedAt = now; queue.DequeueReason = "SupportBlocked"; }
        foreach (var conversation in conversations)
        {
            conversation.Status = LiveSupportConversationStatus.Closed;
            conversation.ClosedAt = now; conversation.ClosedByUserId = actor;
            conversation.CloseReason = block.Reason;
            conversation.CurrentOwnerUserId = null; conversation.AllowsAI = false; conversation.Version++;
            await events.AppendAsync(new(conversation.Id, LiveSupportEventType.Closed, ActorUserId: actor,
                SafeMetadataJson: JsonSerializer.Serialize(new { reason = "support_block", blockId = block.Id })), ct);
        }
    }
}

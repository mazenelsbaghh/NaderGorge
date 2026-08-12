using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Commands;

public sealed partial class AdminAIConversationService(IAppDbContext db, IAdminAIAccessGate access, IAdminAIDataProtector protector) : IAdminAIConversationService
{
    public async Task<AdminAIConversationSummary> CreateAsync(Guid actorId, string? title, string idempotencyKey, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new ArgumentException("Invalid idempotency key.", nameof(idempotencyKey));
        var normalizedTitle = NormalizeTitle(title);
        var digest = protector.Digest("conversation-create", Encoding.UTF8.GetBytes($"{actorId:N}:{idempotencyKey}"));
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedTitle))).ToLowerInvariant();
        var replay = await db.AdminAIConversations.AsNoTracking().SingleOrDefaultAsync(
            x => x.OwnerAdminUserId == actorId && x.CreateIdempotencyDigest == digest, ct);
        if (replay is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(replay.CreatePayloadHash!), Encoding.ASCII.GetBytes(payloadHash)))
                throw new InvalidOperationException("Idempotency payload conflict.");
            return Summary(replay);
        }

        var conversation = new AdminAIConversation
        {
            OwnerAdminUserId = actorId,
            Title = normalizedTitle,
            CreateIdempotencyDigest = digest,
            CreatePayloadHash = payloadHash
        };
        db.AdminAIConversations.Add(conversation);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // A concurrent request may have committed the same durable key first.
            db.AdminAIConversations.Remove(conversation);
            var committed = await db.AdminAIConversations.AsNoTracking().SingleOrDefaultAsync(
                x => x.OwnerAdminUserId == actorId && x.CreateIdempotencyDigest == digest, ct);
            if (committed is null) throw;
            if (committed.CreatePayloadHash != payloadHash) throw new InvalidOperationException("Idempotency payload conflict.");
            return Summary(committed);
        }
        return Summary(conversation);
    }

    public async Task<AdminAIConversationSummary> RenameAsync(Guid actorId, Guid conversationId, string title, long expectedVersion, string idempotencyKey, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        var normalizedTitle = NormalizeTitle(title, required: true);
        var receipt = await ExistingReceiptAsync(actorId, "rename", conversationId, expectedVersion, normalizedTitle, idempotencyKey, ct);
        if (receipt is not null) return ReceiptSummary(receipt);
        var conversation = await OwnedConversation(actorId, conversationId, ct);
        RequireVersion(conversation, expectedVersion);
        conversation.Title = normalizedTitle; conversation.Version++; conversation.LastActivityAt = DateTime.UtcNow;
        AddReceipt(actorId, conversation, "rename", expectedVersion, normalizedTitle, idempotencyKey);
        await db.SaveChangesAsync(ct);
        return Summary(conversation);
    }

    public async Task<AdminAIConversationSummary> SetArchivedAsync(Guid actorId, Guid conversationId, bool archived, long expectedVersion, string idempotencyKey, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        var operation = archived ? "archive" : "restore";
        var receipt = await ExistingReceiptAsync(actorId, operation, conversationId, expectedVersion, operation, idempotencyKey, ct);
        if (receipt is not null) return ReceiptSummary(receipt);
        var conversation = await OwnedConversation(actorId, conversationId, ct);
        RequireVersion(conversation, expectedVersion);
        conversation.Status = archived ? AdminAIConversationStatus.Archived : AdminAIConversationStatus.Active;
        conversation.ArchivedAt = archived ? DateTime.UtcNow : null; conversation.Version++; conversation.LastActivityAt = DateTime.UtcNow;
        if (archived)
            foreach (var turn in await db.AdminAITurns.Where(x => x.ConversationId == conversationId && !x.Status.IsTerminal()).ToListAsync(ct))
            { turn.Status = AdminAITurnStatus.CancelRequested; turn.CancellationRequestedAt = DateTime.UtcNow; turn.Version++; }
        AddReceipt(actorId, conversation, operation, expectedVersion, operation, idempotencyKey);
        await db.SaveChangesAsync(ct);
        return Summary(conversation);
    }

    private async Task<AdminAIConversationCommandReceipt?> ExistingReceiptAsync(Guid actorId, string operation, Guid conversationId, long expectedVersion, string requestedValue, string idempotencyKey, CancellationToken ct)
    {
        var (digest, payloadHash) = CommandIdentity(actorId, operation, conversationId, expectedVersion, requestedValue, idempotencyKey);
        var receipt = await db.AdminAIConversationCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerAdminUserId == actorId && x.IdempotencyDigest == digest, ct);
        if (receipt is not null && receipt.PayloadHash != payloadHash) throw new InvalidOperationException("Idempotency payload conflict.");
        return receipt;
    }

    private void AddReceipt(Guid actorId, AdminAIConversation conversation, string operation, long expectedVersion, string requestedValue, string idempotencyKey)
    {
        var (digest, payloadHash) = CommandIdentity(actorId, operation, conversation.Id, expectedVersion, requestedValue, idempotencyKey);
        db.AdminAIConversationCommandReceipts.Add(new AdminAIConversationCommandReceipt
        {
            OwnerAdminUserId = actorId, ConversationId = conversation.Id, Operation = operation,
            IdempotencyDigest = digest, PayloadHash = payloadHash, ResponseTitle = conversation.Title,
            ResponseStatus = conversation.Status, ResponseLastActivityAt = conversation.LastActivityAt, ResponseVersion = conversation.Version
        });
    }

    private (string Digest, string PayloadHash) CommandIdentity(Guid actorId, string operation, Guid conversationId, long expectedVersion, string requestedValue, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new ArgumentException("Invalid idempotency key.", nameof(idempotencyKey));
        var digest = protector.Digest("conversation-command", Encoding.UTF8.GetBytes($"{actorId:N}:{idempotencyKey}"));
        var payload = $"{operation}:{conversationId:N}:{expectedVersion}:{requestedValue}";
        return (digest, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant());
    }

    private async Task<AdminAIConversation> OwnedConversation(Guid actorId, Guid id, CancellationToken ct) =>
        await db.AdminAIConversations.SingleOrDefaultAsync(x => x.Id == id && x.OwnerAdminUserId == actorId, ct)
        ?? throw new KeyNotFoundException("Admin AI conversation was not found.");

    private static string NormalizeTitle(string? title, bool required = false)
    {
        var normalized = title?.Trim();
        if (string.IsNullOrEmpty(normalized)) { if (required) throw new ArgumentException("A title is required.", nameof(title)); return "محادثة جديدة"; }
        if (normalized.Length > 160) throw new ArgumentOutOfRangeException(nameof(title));
        return normalized;
    }

    private static void RequireVersion(AdminAIConversation conversation, long expectedVersion)
    { if (expectedVersion < 1 || conversation.Version != expectedVersion) throw new InvalidOperationException("Admin AI conversation version conflict."); }

    private static AdminAIConversationSummary Summary(AdminAIConversation x) => new(x.Id, x.Title, x.Status, x.LastActivityAt, x.Version);
    private static AdminAIConversationSummary ReceiptSummary(AdminAIConversationCommandReceipt x) => new(x.ConversationId, x.ResponseTitle, x.ResponseStatus, x.ResponseLastActivityAt, x.ResponseVersion);
}

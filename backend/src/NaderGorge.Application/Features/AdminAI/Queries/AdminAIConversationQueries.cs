using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Commands;

public sealed partial class AdminAIConversationService
{
    public async Task<AdminAIConversationPage> ListAsync(Guid actorId, AdminAIConversationStatus? status, string? cursor, int pageSize, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var query = db.AdminAIConversations.AsNoTracking().Where(x => x.OwnerAdminUserId == actorId);
        if (status is not null) query = query.Where(x => x.Status == status);
        if (cursor is not null)
        {
            var boundary = DecodeCursor(cursor);
            query = query.Where(x => x.LastActivityAt < boundary.ActivityAt || x.LastActivityAt == boundary.ActivityAt && x.Id.CompareTo(boundary.Id) < 0);
        }
        var rows = await query.OrderByDescending(x => x.LastActivityAt).ThenByDescending(x => x.Id).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        return new(rows.Take(pageSize).Select(Summary).ToArray(), hasMore ? EncodeCursor(rows[pageSize - 1]) : null);
    }

    public async Task<AdminAIConversationSnapshot> SnapshotAsync(Guid actorId, Guid conversationId, long? beforeSequence, int pageSize, CancellationToken ct)
    {
        await access.RequireCurrentAdminAsync(actorId, null, ct);
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var conversation = await db.AdminAIConversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conversationId && x.OwnerAdminUserId == actorId, ct)
            ?? throw new KeyNotFoundException("Admin AI conversation was not found.");
        var query = db.AdminAIMessages.AsNoTracking().Where(x => x.ConversationId == conversationId);
        if (beforeSequence is not null) query = query.Where(x => x.Sequence < beforeSequence);
        var messages = await query.OrderByDescending(x => x.Sequence).Take(pageSize + 1).ToListAsync(ct);
        var activeTurn = await db.AdminAITurns.AsNoTracking()
            .Where(x => x.ConversationId == conversationId &&
                        x.Status != AdminAITurnStatus.Completed &&
                        x.Status != AdminAITurnStatus.Cancelled &&
                        x.Status != AdminAITurnStatus.Failed &&
                        x.Status != AdminAITurnStatus.AccessRevoked)
            .OrderByDescending(x => x.QueuedAt)
            .FirstOrDefaultAsync(ct);
        return new(Summary(conversation), messages.Take(pageSize).OrderBy(x => x.Sequence).Select(x => new AdminAIMessageDto(x.Id, x.Sequence, x.Role, x.Content, null, x.TurnId, x.CreatedAt)).ToArray(),
            activeTurn is null ? null : new AdminAITurnDto(activeTurn.Id, activeTurn.Status, activeTurn.CurrentStepNumber, activeTurn.ReadInvocationCount, activeTurn.FailureCode, activeTurn.QueuedAt, activeTurn.CompletedAt, activeTurn.Version), messages.Count > pageSize);
    }

    private static string EncodeCursor(NaderGorge.Domain.Entities.AdminAI.AdminAIConversation value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{value.LastActivityAt.Ticks}:{value.Id:N}"));

    private static (DateTime ActivityAt, Guid Id) DecodeCursor(string cursor)
    {
        try
        {
            var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':');
            if (parts.Length == 2 && long.TryParse(parts[0], out var ticks) && Guid.TryParseExact(parts[1], "N", out var id)) return (new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (FormatException) { }
        throw new ArgumentException("Invalid conversation cursor.", nameof(cursor));
    }
}

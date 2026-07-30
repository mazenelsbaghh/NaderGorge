using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LiveSupport.Services;

public sealed class LiveSupportEventWriter(IAppDbContext db) : ILiveSupportEventWriter
{
    public async Task<long> AppendAsync(LiveSupportEventWriteRequest request, CancellationToken ct)
    {
        var sequence = (await db.LiveSupportEvents.Where(x => x.ConversationId == request.ConversationId)
            .MaxAsync(x => (long?)x.Sequence, ct) ?? 0) + 1;
        var eventId = Guid.NewGuid();
        db.LiveSupportEvents.Add(new LiveSupportEvent
        {
            Id = eventId,
            ConversationId = request.ConversationId,
            Type = request.Type,
            ActorUserId = request.ActorUserId,
            ActorGuestSessionId = request.ActorGuestSessionId,
            RelatedEntityId = request.RelatedEntityId,
            SafeMetadataJson = request.SafeMetadataJson,
            OccurredAt = DateTime.UtcNow,
            Sequence = sequence
        });

        var groups = request.TargetGroups is { Count: > 0 }
            ? request.TargetGroups
            : [$"LiveSupport:Conversation:{request.ConversationId:N}", "LiveSupport:Admins"];
        foreach (var group in groups.Where(IsAllowedGroup).Distinct(StringComparer.Ordinal))
        {
            db.OutboxEvents.Add(new OutboxEvent
            {
                Type = "LiveSupportEvent",
                TargetGroup = group,
                PayloadJson = JsonSerializer.Serialize(new { eventId, conversationId = request.ConversationId, sequence, occurredAt = DateTime.UtcNow, type = request.Type.ToString(), payload = request.SafeMetadataJson })
            });
        }
        return sequence;
    }

    private static bool IsAllowedGroup(string group) =>
        group == "LiveSupport:Admins" || group == "LiveSupport:Queue" ||
        group.StartsWith("LiveSupport:Conversation:", StringComparison.Ordinal) ||
        group.StartsWith("LiveSupport:Participant:", StringComparison.Ordinal) ||
        group.StartsWith("LiveSupport:Staff:", StringComparison.Ordinal);
}

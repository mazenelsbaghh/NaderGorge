using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Commands;
using NaderGorge.Application.Features.AdminAI.Queries;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIAuditAuthorizationTests
{
    [Fact]
    public async Task AnotherAdmin_SeesRedactedEvidenceButNotPrivateTranscript()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var owner = Guid.NewGuid(); var auditor = Guid.NewGuid();
        var conversation = new AdminAIConversation { OwnerAdminUserId = owner, Title = "private" }; db.Add(conversation);
        db.AdminAIAuditEvents.Add(new AdminAIAuditEvent { ActorAdminUserId = owner, ConversationId = conversation.Id, EventType = AdminAIAuditEventType.ProposalCreated, SafeEvidenceJson = "{\"safe\":true}", EvidenceHash = new string('a', 64), CorrelationId = "corr", TraceId = "trace", CapabilityKey = "test.action" }); await db.SaveChangesAsync();
        var evidence = await new AdminAIAuditQueries(db, new AdminAIConversationTests.AllowAccess(auditor)).ListAsync(auditor, null, 20, null, null, null, null, default);
        Assert.Single(evidence.Items); Assert.Equal("{\"safe\":true}", evidence.Items[0].SafeEvidenceJson);
        var conversations = new AdminAIConversationService(db, new AdminAIConversationTests.AllowAccess(auditor), AdminAIStrongConfirmationTests.Protector());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => conversations.SnapshotAsync(auditor, conversation.Id, null, 20, default));
    }

    [Fact]
    public async Task Evidence_FiltersAndOpaqueCursorAreStable()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var auditor = Guid.NewGuid(); var actor = Guid.NewGuid(); var now = DateTime.UtcNow;
        db.AdminAIAuditEvents.AddRange(Enumerable.Range(0, 3).Select(index => new AdminAIAuditEvent { ActorAdminUserId = actor, EventType = AdminAIAuditEventType.ExecutionSucceeded, CapabilityKey = "match", SafeEvidenceJson = "{}", EvidenceHash = new string((char)('a' + index), 64), CorrelationId = $"c{index}", TraceId = $"t{index}", OccurredAt = now.AddSeconds(index) })); await db.SaveChangesAsync();
        var query = new AdminAIAuditQueries(db, new AdminAIConversationTests.AllowAccess(auditor)); var first = await query.ListAsync(auditor, null, 2, "match", actor, now.AddSeconds(-1), now.AddSeconds(5), default);
        Assert.Equal(2, first.Items.Count); Assert.NotNull(first.NextCursor);
        Assert.Single((await query.ListAsync(auditor, first.NextCursor, 2, "match", actor, null, null, default)).Items);
    }
}

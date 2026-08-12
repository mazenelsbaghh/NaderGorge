using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.AdminAI.Security;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIAuditWriterTests
{
    [Fact]
    public async Task Writer_AddsAppendOnlyEvidenceAndRedactedLinkedSummary()
    {
        await using var db = CreateDb();
        var writer = new AdminAIAuditWriter(db, new AdminAISensitiveDataPolicy(), CreateProtector());
        var actorId = Guid.NewGuid(); var conversationId = Guid.NewGuid();

        await writer.WriteAsync("TurnQueued", actorId, conversationId, null, null, new { Scope = "users", Count = 2 }, default);

        var evidence = Assert.Single(db.AdminAIAuditEvents.Local);
        var summary = Assert.Single(db.AuditLogs.Local);
        Assert.Equal(actorId, evidence.ActorAdminUserId);
        Assert.Equal(conversationId, evidence.ConversationId);
        Assert.Equal(64, evidence.EvidenceHash.Length);
        Assert.Null(summary.OldValues);
        Assert.Null(summary.NewValues);
        Assert.Contains(evidence.EvidenceHash, summary.Reason!, StringComparison.Ordinal);
        Assert.Equal(evidence.CorrelationId, summary.CorrelationId);
    }

    [Fact]
    public async Task TerminalExecution_RequiresCompleteCorrelationAndStoresOnlySafeMetadata()
    {
        await using var db = CreateDb();
        var writer = new AdminAIAuditWriter(db, new AdminAISensitiveDataPolicy(), CreateProtector());
        var proposalId = Guid.NewGuid(); var executionId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(
            "ExecutionSucceeded", Guid.NewGuid(), null, null, proposalId, new { CapabilityKey = "external.job" }, default));

        await writer.WriteAsync("ExecutionSucceeded", Guid.NewGuid(), null, null, proposalId,
            new { ExecutionId = executionId, CapabilityKey = "external.job", SafeTargetReference = "job:42", AffectedCount = 1 }, default);

        var evidence = Assert.Single(db.AdminAIAuditEvents.Local);
        Assert.Equal(executionId, evidence.ExecutionId);
        Assert.Equal("external.job", evidence.CapabilityKey);
        Assert.Equal("job:42", evidence.SafeTargetReference);
        var summary = Assert.Single(db.AuditLogs.Local);
        Assert.Null(summary.OldValues);
        Assert.Null(summary.NewValues);
    }

    [Fact]
    public async Task Writer_FailsClosedForUnknownEventOrSensitiveSchema()
    {
        await using var db = CreateDb();
        var writer = new AdminAIAuditWriter(db, new AdminAISensitiveDataPolicy(), CreateProtector());
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync("MadeUp", null, null, null, null, new { Value = 1 }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync("TurnQueued", null, null, null, null, new { ApiToken = "canary" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync("TurnQueued", null, null, null, null, new { RawTranscript = "private admin conversation" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync("TurnQueued", null, null, null, null, new { OldValues = "unrestricted audit data" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync("TurnQueued", null, null, null, null, new { NewValues = "unrestricted audit data" }, default));
        Assert.Empty(db.AdminAIAuditEvents.Local);
        Assert.Empty(db.AuditLogs.Local);
    }

    [Fact]
    public async Task PersistedEvidence_CannotBeUpdatedOrDeleted()
    {
        await using var db = CreateDb(); var evidence = new NaderGorge.Domain.Entities.AdminAI.AdminAIAuditEvent { EventType = NaderGorge.Domain.Enums.AdminAIAuditEventType.TurnQueued, SafeEvidenceJson = "{}", EvidenceHash = new string('a', 64), CorrelationId = "c", TraceId = "t" };
        db.Add(evidence); await db.SaveChangesAsync();
        evidence.SafeEvidenceJson = "{\"changed\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(evidence).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-audit-{Guid.NewGuid()}").Options);

    private static AdminAIDataProtector CreateProtector()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminAI:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        return new AdminAIDataProtector(new EphemeralDataProtectionProvider(), configuration);
    }
}

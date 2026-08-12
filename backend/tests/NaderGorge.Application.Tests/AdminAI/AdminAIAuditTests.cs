using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.AdminAI.Security;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIAuditTests
{
    [Fact]
    public async Task Evidence_IsAppendOnlyHasStableCorrelationAndLinkedRedactedSummary()
    {
        await using var db = CreateDb();
        var writer = Writer(db);
        using var activity = new Activity("admin-ai-test").Start();
        var actor = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        var turn = Guid.NewGuid();
        var proposal = Guid.NewGuid();

        await writer.WriteAsync("ProposalCreated", actor, conversation, turn, proposal,
            new SafeEvidence("identity.user.update", "user:opaque", 1), default);
        await db.SaveChangesAsync();

        var evidence = Assert.Single(db.AdminAIAuditEvents);
        var summary = Assert.Single(db.AuditLogs);
        Assert.Equal(activity.TraceId.ToString(), evidence.CorrelationId);
        Assert.Equal(evidence.CorrelationId, summary.CorrelationId);
        Assert.Equal(64, evidence.EvidenceHash.Length);
        Assert.Equal(proposal, summary.EntityId);
        Assert.Null(summary.OldValues);
        Assert.Null(summary.NewValues);
        Assert.DoesNotContain("identity.user.update", summary.Reason!, StringComparison.Ordinal);
        Assert.DoesNotContain("user:opaque", summary.Reason!, StringComparison.Ordinal);
        Assert.Contains(evidence.EvidenceHash, summary.Reason!, StringComparison.Ordinal);

        evidence.SafeEvidenceJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TranscriptOrSensitiveEvidence_IsRejectedBeforeAnyAuditWrite()
    {
        await using var db = CreateDb();
        var writer = Writer(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(
            "TurnQueued", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            new { Transcript = "private conversation", PasswordHash = "sentinel" }, default));

        Assert.Empty(db.AdminAIAuditEvents.Local);
        Assert.Empty(db.AuditLogs.Local);
    }

    private sealed record SafeEvidence(string CapabilityKey, string TargetReference, int AffectedCount);

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-audit-contract-{Guid.NewGuid()}").Options);

    private static AdminAIAuditWriter Writer(AppDbContext db)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminAI:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        return new AdminAIAuditWriter(db, new AdminAISensitiveDataPolicy(),
            new AdminAIDataProtector(new EphemeralDataProtectionProvider(), configuration));
    }
}

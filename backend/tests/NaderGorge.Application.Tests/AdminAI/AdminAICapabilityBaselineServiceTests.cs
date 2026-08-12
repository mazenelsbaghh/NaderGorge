using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAICapabilityBaselineServiceTests
{
    [Fact]
    public async Task Activation_SupersedesActiveAndInvalidatesPendingOlderProposal()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var old = Baseline("old", "ready", "supported", AdminAICapabilityBaselineStatus.Active);
        var draft = Baseline("new", "ready", "supported", AdminAICapabilityBaselineStatus.Draft);
        db.AdminAICapabilityBaselines.AddRange(old, draft);
        var proposal = new AdminAIActionProposal
        {
            ActorAdminUserId = actor,
            CapabilityBaselineId = old.Id,
            Status = AdminAIProposalStatus.PendingConfirmation
        };
        db.AdminAIActionProposals.Add(proposal);
        await db.SaveChangesAsync();
        var service = new AdminAICapabilityBaselineService(db, new AllowAccess(actor));

        var activated = await service.ActivateAsync(actor, draft.Id, default);

        Assert.Equal(AdminAICapabilityBaselineStatus.Active, activated.Status);
        Assert.Equal(AdminAICapabilityBaselineStatus.Superseded, old.Status);
        Assert.Equal(AdminAIProposalStatus.Invalidated, proposal.Status);
        Assert.Equal("admin_ai_baseline_superseded", proposal.InvalidatedReasonCode);
    }

    [Theory]
    [InlineData("blocked")]
    [InlineData("excluded")]
    public async Task Activation_RejectsCurrentBusinessMutationGap(string status)
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var draft = Baseline("candidate", "ready", status, AdminAICapabilityBaselineStatus.Draft);
        db.AdminAICapabilityBaselines.Add(draft);
        await db.SaveChangesAsync();
        var service = new AdminAICapabilityBaselineService(db, new AllowAccess(actor));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActivateAsync(actor, draft.Id, default));

        Assert.Equal(AdminAICapabilityBaselineStatus.Draft, draft.Status);
    }

    [Fact]
    public async Task Draft_RejectsManifestHashMismatchWithoutWriting()
    {
        await using var db = CreateDb();
        var actor = Guid.NewGuid();
        var json = Manifest("ready", "supported");
        var service = new AdminAICapabilityBaselineService(db, new AllowAccess(actor));
        var input = new AdminAIBaselineDraft("v1", new string('a', 64), json, "source", new string('b', 64), new string('c', 64), 1, 1, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDraftAsync(actor, input, default));

        Assert.Empty(db.AdminAICapabilityBaselines);
    }

    private static AdminAICapabilityBaseline Baseline(string version, string activation, string status, AdminAICapabilityBaselineStatus baselineStatus)
    {
        var json = Manifest(activation, status);
        return new AdminAICapabilityBaseline
        {
            Version = version,
            SafeManifestJson = json,
            ManifestHash = Hash(json),
            SourceRevision = "source",
            RuntimeInventoryHash = new string('b', 64),
            FrontendInventoryHash = new string('c', 64),
            SupportedReadCount = 1,
            SupportedActionCount = status == "supported" ? 1 : 0,
            Status = baselineStatus
        };
    }

    private static string Manifest(string activation, string status) =>
        $$"""{"activation":"{{activation}}","items":[{"effect":"mutation","status":"{{status}}"}],"exclusions":[]}""";

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-baseline-{Guid.NewGuid()}").Options);

    private sealed class AllowAccess(Guid actor) : IAdminAIAccessGate
    {
        public Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion, CancellationToken cancellationToken) =>
            userId == actor
                ? Task.FromResult(new AdminAIAccessSnapshot(actor, 1, DateTime.UtcNow))
                : throw new UnauthorizedAccessException();
    }
}

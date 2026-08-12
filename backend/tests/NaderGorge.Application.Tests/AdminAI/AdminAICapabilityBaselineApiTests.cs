using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.AdminAI.Commands;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Queries;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAICapabilityBaselineApiTests
{
    [Fact]
    public async Task ActiveSummary_ReturnsOnlySafeMetadata()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var actor = Guid.NewGuid();
        db.AdminAICapabilityBaselines.Add(new AdminAICapabilityBaseline
        {
            Version = "sealed-v1", ManifestHash = new string('a', 64), SafeManifestJson = "{\"secretSchema\":true}",
            SourceRevision = "revision", RuntimeInventoryHash = new string('b', 64), FrontendInventoryHash = new string('c', 64),
            SupportedReadCount = 18, SupportedActionCount = 0, ExcludedCount = 2,
            Status = AdminAICapabilityBaselineStatus.Active, ApprovedAt = DateTime.UtcNow, ApprovedByAdminUserId = actor
        });
        await db.SaveChangesAsync();

        var result = await Controller(db, actor).CapabilityBaseline(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("sealed-v1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secretSchema", json, StringComparison.Ordinal);
        Assert.DoesNotContain("runtimeInventory", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("approvedBy", json, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminAIAgentController Controller(NaderGorge.Infrastructure.Data.AppDbContext db, Guid actor)
    {
        var access = new AdminAIConversationTests.AllowAccess(actor);
        var commands = new AdminAIProposalCommands(db, access, new NoChallenge(), new NoExecutor());
        return new AdminAIAgentController(new NoConversations(), new NoTurns(), commands, new NoSecureInput(),
            new AdminAIAuditQueries(db, access), new AdminAICapabilityBaselineQueries(db, access),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminAI:Enabled"] = "true" }).Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Role, "Admin")], "test")) } }
        };
    }

    private sealed class NoConversations : IAdminAIConversationService
    {
        public Task<AdminAIConversationSummary> CreateAsync(Guid a, string? b, string k, CancellationToken c) => throw new NotSupportedException();
        public Task<AdminAIConversationSummary> RenameAsync(Guid a, Guid b, string c, long d, string k, CancellationToken e) => throw new NotSupportedException();
        public Task<AdminAIConversationSummary> SetArchivedAsync(Guid a, Guid b, bool c, long d, string k, CancellationToken e) => throw new NotSupportedException();
        public Task<AdminAIConversationPage> ListAsync(Guid a, AdminAIConversationStatus? b, string? c, int d, CancellationToken e) => throw new NotSupportedException();
        public Task<AdminAIConversationSnapshot> SnapshotAsync(Guid a, Guid b, long? c, int d, CancellationToken e) => throw new NotSupportedException();
    }
    private sealed class NoTurns : IAdminAITurnOrchestrator
    {
        public Task<AdminAITurnDto> QueueAsync(Guid a, Guid b, string c, long d, string e, CancellationToken f) => throw new NotSupportedException();
        public Task<AdminAITurnDto> CancelAsync(Guid a, Guid b, Guid c, long d, CancellationToken e) => throw new NotSupportedException();
    }
    private sealed class NoChallenge : IAdminAIConfirmationChallengeService
    {
        public Task<string> IssueAsync(Guid a, Guid b, string c, CancellationToken d) => throw new NotSupportedException();
        public Task<string?> PhraseAsync(Guid a, Guid b, CancellationToken c) => Task.FromResult<string?>(null);
        public Task<bool> VerifyAsync(Guid a, Guid b, string c, CancellationToken d) => throw new NotSupportedException();
    }
    private sealed class NoExecutor : IAdminAIActionExecutor { public Task<AdminAIExecutionResultDto> ExecuteAsync(Guid a, Guid b, string c, CancellationToken d) => throw new NotSupportedException(); }
    private sealed class NoSecureInput : IAdminAISecureInputService
    {
        public Task<AdminAISecureGrantResult> IssueAsync(Guid a, Guid b, string c, long d, CancellationToken e) => throw new NotSupportedException();
        public Task<AdminAISecureGrantResult> SubmitAsync(Guid a, Guid b, string c, string d, ReadOnlyMemory<byte> e, CancellationToken f) => throw new NotSupportedException();
        public Task<AdminAIProtectedValue> ConsumeAsync(Guid a, Guid b, CancellationToken c) => throw new NotSupportedException();
    }
}

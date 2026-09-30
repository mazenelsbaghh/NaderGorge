using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIInternalRuntimeBaselineTests
{
    [Fact]
    public async Task Readiness_FailsClosedUntilTheActiveBaselineMatchesTheLocalRuntimeCatalog()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-runtime-baseline-{Guid.NewGuid()}")
            .Options);
        var registry = AdminAICapabilityRegistry.CreateProductionReadRegistry();
        var baseline = new AdminAICapabilityBaseline
        {
            Version = "read-old",
            ManifestHash = new string('a', 64),
            SafeManifestJson = "{}",
            SourceRevision = "test",
            RuntimeInventoryHash = new string('b', 64),
            FrontendInventoryHash = new string('b', 64),
            SupportedReadCount = registry.All.Count,
            Status = AdminAICapabilityBaselineStatus.Active
        };
        db.AdminAICapabilityBaselines.Add(baseline);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AdminAI:Enabled"] = "true",
                ["AdminAI:CallbackSecret"] = "test-secret"
            }).Build();
        var controller = new AdminAIInternalController(
            configuration,
            db,
            registry,
            null!,
            null!,
            null!,
            null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["X-Internal-Token"] = "test-secret";

        var mismatch = Assert.IsType<ObjectResult>(await controller.Ready(default));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, mismatch.StatusCode);

        baseline.RuntimeInventoryHash = registry.BaselineHash;
        await db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await controller.Ready(default));
    }

    [Fact]
    public async Task LeaseRenewalAndReads_CannotExtendPastTheTurnDeadline()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-deadline-{Guid.NewGuid()}").Options);
        var actorId = Guid.NewGuid();
        var registry = AdminAICapabilityRegistry.CreateProductionReadRegistry();
        var baseline = new AdminAICapabilityBaseline
        {
            Version = "deadline-test", ManifestHash = new string('a', 64), SourceRevision = "test",
            RuntimeInventoryHash = registry.BaselineHash, FrontendInventoryHash = new string('b', 64),
            Status = AdminAICapabilityBaselineStatus.Active
        };
        var policy = new AdminAISensitiveDataPolicyVersion
        {
            Version = "deadline-test", PolicyHash = new string('c', 64),
            Status = AdminAISensitiveDataPolicyStatus.Active
        };
        var conversation = new AdminAIConversation { OwnerAdminUserId = actorId, Title = "Deadline test" };
        var message = new AdminAIMessage
        {
            ConversationId = conversation.Id, Sequence = 1, Role = AdminAIMessageRole.Admin,
            Content = "Test"
        };
        var turn = new AdminAITurn
        {
            Conversation = conversation, SourceMessageId = message.Id, ActorAdminUserId = actorId,
            CapabilityBaselineId = baseline.Id, SensitiveDataPolicyVersionId = policy.Id,
            QueuedAt = DateTime.UtcNow.AddSeconds(-90),
            Steps = [new AdminAITurnStep { StepNumber = 1, Status = AdminAITurnStepStatus.Queued }]
        };
        db.AddRange(baseline, policy, conversation, message, turn);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminAI:Enabled"] = "true", ["AdminAI:CallbackSecret"] = "test-secret",
            ["AdminAI:TurnDeadlineSeconds"] = "120", ["AdminAI:LeaseSeconds"] = "60"
        }).Build();
        var controller = new AdminAIInternalController(configuration, db, registry,
            new AllowAccess(actorId), null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["X-Internal-Token"] = "test-secret";

        var claimed = JsonSerializer.SerializeToElement(
            Assert.IsType<OkObjectResult>(await controller.Claim(turn.Id, new("1", "worker-1"), default)).Value);
        var deadline = turn.QueuedAt.AddSeconds(120);
        Assert.True(claimed.GetProperty("leaseExpiresAt").GetDateTime() <= deadline);
        var claimedVersion = turn.Version;
        Assert.IsType<ConflictObjectResult>(await controller.Claim(turn.Id, new("1", "worker-1"), default));
        Assert.Equal(claimedVersion, turn.Version);

        var renewed = JsonSerializer.SerializeToElement(
            Assert.IsType<OkObjectResult>(await controller.Renew(turn.Id,
                new("1", claimed.GetProperty("leaseToken").GetString()!, turn.Version, "worker-1"), default)).Value);
        Assert.True(renewed.GetProperty("leaseExpiresAt").GetDateTime() <= deadline);

        turn.QueuedAt = DateTime.UtcNow.AddSeconds(-125);
        await db.SaveChangesAsync();
        var token = renewed.GetProperty("leaseToken").GetString()!;
        var deniedRenewal = Assert.IsType<ObjectResult>(await controller.Renew(turn.Id,
            new("1", token, turn.Version, "worker-1"), default));
        Assert.Equal(StatusCodes.Status410Gone, deniedRenewal.StatusCode);
        var deniedRead = Assert.IsType<ObjectResult>(await controller.ReadBatch(turn.Id, 1,
            new("1", token, turn.Version, baseline.Version, policy.Version, "batch-1",
                [new AdminAIInternalReadCall("read-1", "students.search", new { query = "test" })]), default));
        Assert.Equal(StatusCodes.Status410Gone, deniedRead.StatusCode);
        var deniedCompletion = Assert.IsType<ObjectResult>(await controller.Complete(turn.Id,
            new("1", token, turn.Version, 1, baseline.Version, policy.Version, new { }, "hash",
                "callback-1", "test", "test", null, null, null, 0), default));
        Assert.Equal(StatusCodes.Status410Gone, deniedCompletion.StatusCode);
        var deniedFailure = Assert.IsType<ObjectResult>(await controller.Fail(turn.Id,
            new("1", token, "callback-1", AdminAIInternalFailureCode.AI_PROVIDER_TIMEOUT,
                null, null, 0), default));
        Assert.Equal(StatusCodes.Status410Gone, deniedFailure.StatusCode);
        Assert.Equal(0, turn.ReadInvocationCount);
    }

    [Fact]
    public async Task ExpiredWorkerLease_CanBeClaimedAgainAfterReadsWithoutLosingTheTurnPhase()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-resume-{Guid.NewGuid()}").Options);
        var actorId = Guid.NewGuid();
        var registry = AdminAICapabilityRegistry.CreateProductionReadRegistry();
        var baseline = new AdminAICapabilityBaseline
        {
            Version = "resume-test", ManifestHash = new string('a', 64), SourceRevision = "test",
            RuntimeInventoryHash = registry.BaselineHash, FrontendInventoryHash = new string('b', 64),
            Status = AdminAICapabilityBaselineStatus.Active
        };
        var policy = new AdminAISensitiveDataPolicyVersion
        {
            Version = "resume-test", PolicyHash = new string('c', 64),
            Status = AdminAISensitiveDataPolicyStatus.Active
        };
        var conversation = new AdminAIConversation { OwnerAdminUserId = actorId, Title = "Resume test" };
        var message = new AdminAIMessage
        {
            ConversationId = conversation.Id, Sequence = 1, Role = AdminAIMessageRole.Admin, Content = "Test"
        };
        var step = new AdminAITurnStep
        {
            StepNumber = 1, Status = AdminAITurnStepStatus.ReadsCompleted, CallbackStatus = "Claimed",
            Provider = "stopped-worker", NextCallbackAttemptAt = DateTime.UtcNow.AddSeconds(-1)
        };
        var turn = new AdminAITurn
        {
            Conversation = conversation, SourceMessageId = message.Id, ActorAdminUserId = actorId,
            CapabilityBaselineId = baseline.Id, SensitiveDataPolicyVersionId = policy.Id,
            Status = AdminAITurnStatus.Retrieving, CurrentStepNumber = 1,
            QueuedAt = DateTime.UtcNow.AddSeconds(-20), Steps = [step]
        };
        db.AddRange(baseline, policy, conversation, message, turn);
        await db.SaveChangesAsync();
        var controller = new AdminAIInternalController(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminAI:Enabled"] = "true", ["AdminAI:CallbackSecret"] = "test-secret"
            }).Build(), db, registry, new AllowAccess(actorId), null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["X-Internal-Token"] = "test-secret";

        Assert.IsType<OkObjectResult>(await controller.Claim(turn.Id, new("1", "replacement-worker"), default));
        Assert.Equal(AdminAITurnStatus.Retrieving, turn.Status);
        Assert.Equal("replacement-worker", step.Provider);
        Assert.True(step.NextCallbackAttemptAt > DateTime.UtcNow);
    }

    private sealed class AllowAccess(Guid actorId) : IAdminAIAccessGate
    {
        public Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion,
            CancellationToken cancellationToken) => userId == actorId
                ? Task.FromResult(new AdminAIAccessSnapshot(actorId, expectedSecurityVersion ?? 0, DateTime.UtcNow))
                : throw new UnauthorizedAccessException();
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIReadBatchReplayPostgresTests
{
    [Fact]
    public async Task LostResponseRetryReturnsDurableResultWithoutSpendingReadBudgetAgain()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seedDb = fixture.CreateDbContext();
        await seedDb.Database.MigrateAsync();
        var registry = AdminAICapabilityRegistry.CreateProductionReadRegistry();
        var actor = new User
        {
            FullName = "Admin AI read replay test", PhoneNumber = "01000000172", PasswordHash = "test",
            IsActive = true, UserRoles = [new UserRole { Role = new Role { Name = "Admin AI read replay test", Type = RoleType.Admin } }]
        };
        var baseline = new AdminAICapabilityBaseline
        {
            Version = "replay-test", ManifestHash = new string('a', 64), SourceRevision = "test",
            RuntimeInventoryHash = registry.BaselineHash, FrontendInventoryHash = new string('b', 64),
            Status = AdminAICapabilityBaselineStatus.Active
        };
        var policy = new AdminAISensitiveDataPolicyVersion
        {
            Version = "replay-test", PolicyHash = new string('c', 64), Status = AdminAISensitiveDataPolicyStatus.Active
        };
        var conversation = new AdminAIConversation { OwnerAdminUserId = actor.Id, Title = "Read replay test" };
        var message = new AdminAIMessage
        {
            ConversationId = conversation.Id, Sequence = 1, Role = AdminAIMessageRole.Admin, Content = "Test"
        };
        var turn = new AdminAITurn
        {
            Conversation = conversation, SourceMessageId = message.Id, ActorAdminUserId = actor.Id,
            CapabilityBaselineId = baseline.Id, SensitiveDataPolicyVersionId = policy.Id,
            CallbackIdempotencyDigest = new string('d', 64),
            Steps = [new AdminAITurnStep { StepNumber = 1, Status = AdminAITurnStepStatus.Queued }]
        };
        seedDb.AddRange(actor, baseline, policy, conversation, message, turn);
        await seedDb.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminAI:Enabled"] = "true", ["AdminAI:CallbackSecret"] = "test-secret",
            ["AdminAI:HmacKey"] = Convert.ToBase64String(new byte[32])
        }).Build();
        var protector = new AdminAIDataProtector(new EphemeralDataProtectionProvider(), configuration);
        var reads = new CountingReads();
        var access = new AllowAccess(actor.Id);
        await using var firstDb = fixture.CreateDbContext();
        var first = Controller(firstDb, configuration, registry, access, reads, protector);
        var claim = Assert.IsType<OkObjectResult>(await first.Claim(turn.Id, new("1", "worker-one"), default));
        var claimJson = JsonSerializer.SerializeToElement(claim.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var request = new AdminAIInternalReadRequest(
            "1", claimJson.GetProperty("leaseToken").GetString()!,
            claimJson.GetProperty("expectedTurnVersion").GetInt64(), baseline.Version, policy.Version,
            "read-batch-one", [new("call-one", registry.All.First(x => x.Kind == "read").Key, new { query = "test" })]);
        var initial = Assert.IsType<OkObjectResult>(await first.ReadBatch(turn.Id, 1, request, default));
        var initialJson = JsonSerializer.SerializeToElement(initial.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await using var retryDb = fixture.CreateDbContext();
        var retry = Controller(retryDb, configuration, registry, access, reads, protector);
        var replay = Assert.IsType<OkObjectResult>(await retry.ReadBatch(turn.Id, 1, request, default));
        var replayJson = JsonSerializer.SerializeToElement(replay.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(initialJson.GetRawText(), replayJson.GetRawText());
        Assert.Equal(1, reads.CallCount);

        var changed = request with { Calls = [new("call-one", registry.All.First(x => x.Kind == "read").Key, new { query = "different" })] };
        Assert.IsType<ConflictObjectResult>(await retry.ReadBatch(turn.Id, 1, changed, default));
        await using var verifyDb = fixture.CreateDbContext();
        var persisted = await verifyDb.AdminAITurns.Include(x => x.Steps).SingleAsync(x => x.Id == turn.Id);
        Assert.Equal(1, persisted.ReadInvocationCount);
        Assert.Equal(1, Assert.Single(persisted.Steps).ToolCallsRequested);
        Assert.Equal(1, await verifyDb.AdminAIReadBatchReceipts.CountAsync());
    }

    private static AdminAIInternalController Controller(AppDbContext db, IConfiguration configuration,
        AdminAICapabilityRegistry registry, IAdminAIAccessGate access, IAdminAIReadExecutor reads,
        IAdminAIDataProtector protector)
    {
        var controller = new AdminAIInternalController(configuration, db, registry, access, reads, protector, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["X-Internal-Token"] = "test-secret";
        return controller;
    }

    private sealed class CountingReads : IAdminAIReadExecutor
    {
        public int CallCount { get; private set; }
        public Task<object> ExecuteAsync(Guid actorId, AdminAIReadCall call, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<object>(new { data = new { items = Array.Empty<object>() } });
        }
    }

    private sealed class AllowAccess(Guid actorId) : IAdminAIAccessGate
    {
        public Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion,
            CancellationToken cancellationToken)
        {
            if (userId != actorId) throw new UnauthorizedAccessException();
            return Task.FromResult(new AdminAIAccessSnapshot(userId, expectedSecurityVersion ?? 0, DateTime.UtcNow));
        }
    }
}

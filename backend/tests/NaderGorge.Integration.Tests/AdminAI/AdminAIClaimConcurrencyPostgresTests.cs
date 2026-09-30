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

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIClaimConcurrencyPostgresTests
{
    [Fact]
    public async Task TwoWorkersClaimingOneTurn_ReturnOneLeaseAndOneSafeConflict()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seedDb = fixture.CreateDbContext();
        await seedDb.Database.MigrateAsync();
        var registry = AdminAICapabilityRegistry.CreateProductionReadRegistry();
        var actor = new User
        {
            FullName = "Admin AI claim test", PhoneNumber = "01000000171", PasswordHash = "test",
            IsActive = true, UserRoles = [new UserRole { Role = new Role { Name = "Admin AI claim test", Type = RoleType.Admin } }]
        };
        var baseline = new AdminAICapabilityBaseline
        {
            Version = "claim-test", ManifestHash = new string('a', 64), SourceRevision = "test",
            RuntimeInventoryHash = registry.BaselineHash, FrontendInventoryHash = new string('b', 64),
            Status = AdminAICapabilityBaselineStatus.Active
        };
        var policy = new AdminAISensitiveDataPolicyVersion
        {
            Version = "claim-test", PolicyHash = new string('c', 64),
            Status = AdminAISensitiveDataPolicyStatus.Active
        };
        var conversation = new AdminAIConversation { OwnerAdminUserId = actor.Id, Title = "Claim test" };
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

        await using var firstDb = fixture.CreateDbContext();
        await using var secondDb = fixture.CreateDbContext();
        var barrier = new TwoClaimBarrier(actor.Id);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminAI:Enabled"] = "true", ["AdminAI:CallbackSecret"] = "test-secret"
        }).Build();
        var first = Controller(firstDb, configuration, registry, barrier);
        var second = Controller(secondDb, configuration, registry, barrier);

        var results = await Task.WhenAll(
            first.Claim(turn.Id, new AdminAIInternalClaimRequest("1", "worker-one"), default),
            second.Claim(turn.Id, new AdminAIInternalClaimRequest("1", "worker-two"), default));

        Assert.Single(results.OfType<OkObjectResult>());
        Assert.Single(results.OfType<ConflictObjectResult>());
        await using var verifyDb = fixture.CreateDbContext();
        var persisted = await verifyDb.AdminAITurns.Include(item => item.Steps).SingleAsync(item => item.Id == turn.Id);
        Assert.Equal(2, persisted.Version);
        Assert.Equal(AdminAITurnStatus.Planning, persisted.Status);
        var step = Assert.Single(persisted.Steps);
        Assert.Equal(AdminAITurnStepStatus.Claimed, step.Status);
        Assert.Contains(step.Provider, new[] { "worker-one", "worker-two" });
        Assert.True(step.NextCallbackAttemptAt > DateTime.UtcNow);
    }

    private static AdminAIInternalController Controller(AppDbContext db, IConfiguration configuration,
        AdminAICapabilityRegistry registry, IAdminAIAccessGate access)
    {
        var controller = new AdminAIInternalController(configuration, db, registry, access, null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["X-Internal-Token"] = "test-secret";
        return controller;
    }

    private sealed class TwoClaimBarrier(Guid actorId) : IAdminAIAccessGate
    {
        private readonly TaskCompletionSource _bothLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public async Task<AdminAIAccessSnapshot> RequireCurrentAdminAsync(Guid userId, int? expectedSecurityVersion,
            CancellationToken cancellationToken)
        {
            if (userId != actorId) throw new UnauthorizedAccessException();
            if (Interlocked.Increment(ref _arrivals) == 2) _bothLoaded.TrySetResult();
            await _bothLoaded.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return new AdminAIAccessSnapshot(userId, expectedSecurityVersion ?? 0, DateTime.UtcNow);
        }
    }
}

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

public sealed class AdminAIProposalApiTests
{
    [Fact]
    public async Task Get_IsOwnerScoped_AndForeignProposalIsClosed404()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var owner = Guid.NewGuid();
        var proposal = Proposal(owner);
        db.Add(proposal);
        await db.SaveChangesAsync();

        var result = await Controller(db, Guid.NewGuid()).Proposal(proposal.Id, default);

        var missing = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(AdminAIErrorCodes.CapabilityUnavailable, Assert.IsType<AdminAIError>(missing.Value).Code);
    }

    [Fact]
    public async Task Cancel_RequiresExpectedVersion_AndReturnsClosedConflict()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var actor = Guid.NewGuid();
        var proposal = Proposal(actor);
        db.Add(proposal);
        await db.SaveChangesAsync();

        var result = await Controller(db, actor).CancelProposal(proposal.Id, new AdminAIExpectedVersionRequest(99), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(AdminAIErrorCodes.StaleState, Assert.IsType<AdminAIError>(conflict.Value).Code);
        Assert.Equal(AdminAIProposalStatus.PendingConfirmation, proposal.Status);
    }

    [Fact]
    public async Task Confirm_RejectsMissingIdempotencyKey_BeforeExecution()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var actor = Guid.NewGuid();
        var proposal = Proposal(actor);
        db.Add(proposal);
        await db.SaveChangesAsync();
        var executor = new FakeExecutor();

        var result = await Controller(db, actor, executor).Confirm(proposal.Id, new ConfirmAdminAIProposalRequest(null, proposal.Version), "", default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(AdminAIErrorCodes.InvalidRequest, Assert.IsType<AdminAIError>(bad.Value).Code);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Confirm_MapsCompatibleReplay_AndConflictingIntentWithoutSecondEffect()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var actor = Guid.NewGuid();
        var proposal = Proposal(actor, AdminAIProposalStatus.Succeeded);
        db.Add(proposal);
        await db.SaveChangesAsync();
        var executor = new FakeExecutor("intent-1");
        var controller = Controller(db, actor, executor);

        var replay = await controller.Confirm(proposal.Id, new ConfirmAdminAIProposalRequest(null, proposal.Version), "intent-1", default);
        var conflict = await controller.Confirm(proposal.Id, new ConfirmAdminAIProposalRequest(null, proposal.Version), "intent-2", default);

        Assert.IsType<OkObjectResult>(replay);
        var conflictResult = Assert.IsType<ConflictObjectResult>(conflict);
        Assert.Equal(AdminAIErrorCodes.IdempotencyConflict, Assert.IsType<AdminAIError>(conflictResult.Value).Code);
        Assert.Equal(2, executor.Calls);
        Assert.Equal(1, executor.EffectCalls);
    }

    private static AdminAIAgentController Controller(NaderGorge.Infrastructure.Data.AppDbContext db, Guid actor, IAdminAIActionExecutor? executor = null)
    {
        var access = new AdminAIConversationTests.AllowAccess(actor);
        var commands = new AdminAIProposalCommands(db, access, new FakeChallenge(), executor ?? new FakeExecutor());
        var controller = new AdminAIAgentController(new NoConversations(), new NoTurns(), commands, new NoSecureInput(), new AdminAIAuditQueries(db, access), new AdminAICapabilityBaselineQueries(db, access), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminAI:Enabled"] = "true" }).Build())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Role, "Admin")], "test"))
                }
            }
        };
        return controller;
    }

    private static AdminAIActionProposal Proposal(Guid actor, AdminAIProposalStatus status = AdminAIProposalStatus.PendingConfirmation) => new()
    {
        ActorAdminUserId = actor,
        ConversationId = Guid.NewGuid(),
        TurnId = Guid.NewGuid(),
        CapabilityBaselineId = Guid.NewGuid(),
        SensitiveDataPolicyVersionId = Guid.NewGuid(),
        CapabilityKey = "test.action",
        CapabilityVersion = "1",
        SafeTargetType = "user",
        SafeTargetReference = "user:1",
        Status = status,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        PayloadHash = new string('a', 64),
        StateFingerprint = new string('b', 64)
    };

    private sealed class FakeExecutor(string? acceptedKey = null) : IAdminAIActionExecutor
    {
        public int Calls { get; private set; }
        public int EffectCalls { get; private set; }
        public Task<AdminAIExecutionResultDto> ExecuteAsync(Guid actorId, Guid proposalId, string idempotencyKey, CancellationToken ct)
        {
            Calls++;
            if (acceptedKey is not null && idempotencyKey != acceptedKey) throw new InvalidOperationException("Idempotency payload conflict.");
            if (EffectCalls == 0) EffectCalls++;
            return Task.FromResult(new AdminAIExecutionResultDto(Guid.NewGuid(), AdminAIExecutionStatus.Succeeded, 1, null, null, null, ["users"], null));
        }
    }

    private sealed class FakeChallenge : IAdminAIConfirmationChallengeService
    {
        public Task<string> IssueAsync(Guid actorId, Guid proposalId, string label, CancellationToken ct) => Task.FromResult("phrase");
        public Task<string?> PhraseAsync(Guid actorId, Guid proposalId, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<bool> VerifyAsync(Guid actorId, Guid proposalId, string phrase, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class NoConversations : IAdminAIConversationService
    {
        public Task<AdminAIConversationSummary> CreateAsync(Guid a, string? t, string k, CancellationToken c) => throw new NotSupportedException();
        public Task<AdminAIConversationPage> ListAsync(Guid a, AdminAIConversationStatus? s, string? p, int z, CancellationToken c) => throw new NotSupportedException();
        public Task<AdminAIConversationSummary> RenameAsync(Guid a, Guid i, string t, long v, string k, CancellationToken c) => throw new NotSupportedException();
        public Task<AdminAIConversationSummary> SetArchivedAsync(Guid a, Guid i, bool r, long v, string k, CancellationToken c) => throw new NotSupportedException();
        public Task<AdminAIConversationSnapshot> SnapshotAsync(Guid a, Guid i, long? b, int p, CancellationToken c) => throw new NotSupportedException();
    }
    private sealed class NoTurns : IAdminAITurnOrchestrator
    {
        public Task<AdminAITurnDto> CancelAsync(Guid a, Guid c, Guid t, long v, CancellationToken x) => throw new NotSupportedException();
        public Task<AdminAITurnDto> QueueAsync(Guid a, Guid c, string m, long v, string i, CancellationToken x) => throw new NotSupportedException();
    }
    private sealed class NoSecureInput : IAdminAISecureInputService
    {
        public Task<AdminAIProtectedValue> ConsumeAsync(Guid a, Guid p, CancellationToken c) => throw new NotSupportedException();
        public Task<AdminAISecureGrantResult> IssueAsync(Guid a, Guid p, string i, long v, CancellationToken c) => throw new NotSupportedException();
        public Task<AdminAISecureGrantResult> SubmitAsync(Guid a, Guid g, string t, string i, ReadOnlyMemory<byte> p, CancellationToken c) => throw new NotSupportedException();
    }
}

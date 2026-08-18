using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.AdminAI.Commands;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Queries;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIConversationApiTests
{
    [Fact]
    public async Task ConversationAndTurnRoutes_MapFailuresToClosedSafeErrors()
    {
        var actor = Guid.NewGuid();
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var cases = new (Exception Error, Func<AdminAIAgentController, Task<IActionResult>> Invoke, int Status, string Code)[]
        {
            (new ArgumentException("private argument"), c => c.List(null, null, 999, default), 400, AdminAIErrorCodes.InvalidRequest),
            (new InvalidOperationException("Idempotency payload conflict private"), c => c.Create(new(null), "key", default), 409, AdminAIErrorCodes.IdempotencyConflict),
            (new UnauthorizedAccessException("private auth"), c => c.Rename(Guid.NewGuid(), new("x", 1), "key", default), 403, AdminAIErrorCodes.AccessDenied),
            (new InvalidOperationException("private stale"), c => c.Archive(Guid.NewGuid(), new(1), "key", default), 409, AdminAIErrorCodes.StaleState),
            (new KeyNotFoundException("private target"), c => c.Restore(Guid.NewGuid(), new(1), "key", default), 404, AdminAIErrorCodes.CapabilityUnavailable),
            (new KeyNotFoundException("private snapshot"), c => c.Snapshot(Guid.NewGuid(), null, 20, default), 404, AdminAIErrorCodes.CapabilityUnavailable),
            (new AdminAIConflictException(AdminAIErrorCodes.ActiveTurnLimit), c => c.Queue(Guid.NewGuid(), new("q", 1), "key", default), 409, AdminAIErrorCodes.ActiveTurnLimit),
            (new AdminAIConflictException(AdminAIErrorCodes.ActiveTurnExists), c => c.Queue(Guid.NewGuid(), new("q", 1), "key", default), 409, AdminAIErrorCodes.ActiveTurnExists),
            (new KeyNotFoundException("private turn"), c => c.Cancel(Guid.NewGuid(), Guid.NewGuid(), new(1), default), 404, AdminAIErrorCodes.CapabilityUnavailable)
        };

        foreach (var test in cases)
        {
            var result = await test.Invoke(Controller(db, actor, test.Error));
            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(test.Status, objectResult.StatusCode);
            var error = Assert.IsType<AdminAIError>(objectResult.Value);
            Assert.Equal(test.Code, error.Code);
            Assert.DoesNotContain("private", error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Create_RequiresBoundedIdempotencyKeyBeforeServiceMutation()
    {
        var actor = Guid.NewGuid();
        await using var db = AdminAIStrongConfirmationTests.CreateDb();
        var conversations = new CapturingConversations();
        var controller = Controller(db, actor, conversations: conversations);

        var missing = await controller.Create(new("title"), "", default);
        var oversized = await controller.Create(new("title"), new string('x', 201), default);

        Assert.Equal(AdminAIErrorCodes.InvalidRequest, Assert.IsType<AdminAIError>(Assert.IsType<BadRequestObjectResult>(missing).Value).Code);
        Assert.Equal(AdminAIErrorCodes.InvalidRequest, Assert.IsType<AdminAIError>(Assert.IsType<BadRequestObjectResult>(oversized).Value).Code);
        Assert.Equal(2, conversations.Calls); // validation is authoritative in the service, not only model binding
    }

    private static AdminAIAgentController Controller(NaderGorge.Infrastructure.Data.AppDbContext db, Guid actor, Exception? error = null, IAdminAIConversationService? conversations = null)
    {
        var access = new AdminAIConversationTests.AllowAccess(actor);
        var proposals = new AdminAIProposalCommands(db, access, new NoChallenge(), new NoExecutor());
        return new AdminAIAgentController(conversations ?? new ThrowingConversations(error!), new ThrowingTurns(error!), proposals, new NoSecureInput(),
            new AdminAIAuditQueries(db, access), new AdminAICapabilityBaselineQueries(db, access),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminAI:Enabled"] = "true" }).Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Role, "Admin")], "test")) } }
        };
    }

    private sealed class ThrowingConversations(Exception error) : IAdminAIConversationService
    {
        private Task<T> Fail<T>() => Task.FromException<T>(error);
        public Task<AdminAIConversationSummary> CreateAsync(Guid a, string? b, string k, CancellationToken c) => Fail<AdminAIConversationSummary>();
        public Task<AdminAIConversationPage> ListAsync(Guid a, AdminAIConversationStatus? b, string? c, int d, CancellationToken e) => Fail<AdminAIConversationPage>();
        public Task<AdminAIConversationSummary> RenameAsync(Guid a, Guid b, string c, long d, string k, CancellationToken e) => Fail<AdminAIConversationSummary>();
        public Task<AdminAIConversationSummary> SetArchivedAsync(Guid a, Guid b, bool c, long d, string k, CancellationToken e) => Fail<AdminAIConversationSummary>();
        public Task<AdminAIConversationSnapshot> SnapshotAsync(Guid a, Guid b, long? c, int d, CancellationToken e) => Fail<AdminAIConversationSnapshot>();
    }

    private sealed class CapturingConversations : IAdminAIConversationService
    {
        public int Calls { get; private set; }
        public Task<AdminAIConversationSummary> CreateAsync(Guid a, string? b, string k, CancellationToken c) { Calls++; throw new ArgumentException(); }
        public Task<AdminAIConversationPage> ListAsync(Guid a, AdminAIConversationStatus? b, string? c, int d, CancellationToken e) => throw new NotSupportedException();
        public Task<AdminAIConversationSummary> RenameAsync(Guid a, Guid b, string c, long d, string k, CancellationToken e) => throw new NotSupportedException();
        public Task<AdminAIConversationSummary> SetArchivedAsync(Guid a, Guid b, bool c, long d, string k, CancellationToken e) => throw new NotSupportedException();
        public Task<AdminAIConversationSnapshot> SnapshotAsync(Guid a, Guid b, long? c, int d, CancellationToken e) => throw new NotSupportedException();
    }

    private sealed class ThrowingTurns(Exception error) : IAdminAITurnOrchestrator
    {
        public Task<AdminAITurnDto> QueueAsync(Guid a, Guid b, string c, long d, string e, CancellationToken f) => Task.FromException<AdminAITurnDto>(error);
        public Task<AdminAITurnDto> CancelAsync(Guid a, Guid b, Guid c, long d, CancellationToken e) => Task.FromException<AdminAITurnDto>(error);
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

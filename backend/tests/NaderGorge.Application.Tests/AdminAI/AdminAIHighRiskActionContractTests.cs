using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;
using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Wallets;
using NaderGorge.Application.Features.Admin.Commands;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIHighRiskActionContractTests
{
    [Fact]
    public void Registration_RequiresEveryStrongKeyExactlyOnceAndRejectsRiskDowngrade()
    {
        var adapter = new StubAction("admin.strong.one");
        var strong = Definition(adapter.Key, "strong", "strong");
        var catalog = new AdminAICapabilityRegistry([strong]);

        Assert.Single(AdminAIActionCapabilityRegistration.ValidateHighRiskCoverage(catalog, [adapter]));
        Assert.Throws<InvalidOperationException>(() => AdminAIActionCapabilityRegistration.ValidateHighRiskCoverage(catalog, []));
        Assert.Throws<InvalidOperationException>(() => AdminAIActionCapabilityRegistration.ValidateHighRiskCoverage(catalog, [adapter, adapter]));

        var ordinaryCatalog = new AdminAICapabilityRegistry([Definition(adapter.Key, "ordinary", "ordinary")]);
        Assert.Throws<InvalidOperationException>(() => AdminAIActionCapabilityRegistration.ValidateHighRiskCoverage(ordinaryCatalog, [adapter]));
    }

    [Fact]
    public async Task ProtectedAuditMutation_HasNoPreviewOrExecutionPath()
    {
        var refusal = new AdminAIProtectedAuditMutationRefusal();
        await Assert.ThrowsAsync<NotSupportedException>(() => refusal.PreviewAsync(Guid.NewGuid(), new { }, default));
        await Assert.ThrowsAsync<NotSupportedException>(() => refusal.ExecuteAsync(Guid.NewGuid(), new { }, "operation", default));
    }

    [Fact]
    public async Task HighRiskPreviews_AreReadOnlyAndExecutionRequiresDurableOperationIdentity()
    {
        var mediator = new BoundaryMediator(_ => ApiResponse.Ok("done"));
        var preview = new SafePreviewSource();
        var adapters = new (IAdminAIActionCapability Adapter, object Input)[]
        {
            (new AdminAIToggleSystemAccessAction(mediator, preview), new AdminAIToggleSystemAccessInput(Guid.NewGuid(), false, "reviewed")),
            (new AdminAIDeleteVideoAction(mediator, preview), new AdminAIDeleteVideoInput(Guid.NewGuid())),
            (new AdminAIDeleteExamAttemptAction(mediator, preview), new AdminAIDeleteExamAttemptInput(Guid.NewGuid(), Guid.NewGuid())),
            (new AdminAIDeleteFormAction(mediator, preview), new AdminAIDeleteFormInput(Guid.NewGuid())),
            (new AdminAIToggleWalletAction(mediator, preview), new AdminAIToggleWalletInput(Guid.NewGuid(), false))
        };

        foreach (var (adapter, input) in adapters)
        {
            var snapshot = await adapter.PreviewAsync(Guid.NewGuid(), input, default);
            Assert.Equal("state-v1", snapshot.StateFingerprint);
            await Assert.ThrowsAsync<ArgumentException>(() => adapter.ExecuteAsync(Guid.NewGuid(), input, "", default));
        }

        Assert.Equal(adapters.Length, preview.Calls);
        Assert.Equal(0, mediator.SendCalls);
    }

    [Fact]
    public async Task WalletSecurityValues_NeverEnterAgentExecutionResult()
    {
        const string secret = "PAIR-SECRET-123";
        var walletId = Guid.NewGuid();
        var mediator = new BoundaryMediator(request => request switch
        {
            RegenerateWalletTokenCommand => ApiResponse<string>.Ok(secret),
            CreateWalletCommand => ApiResponse<WalletDto>.Ok(new WalletDto { Id = walletId, Label = "Main", IsActive = true, PairingToken = secret }),
            _ => throw new InvalidOperationException("Unexpected command")
        });
        var preview = new SafePreviewSource();

        var rotated = await new AdminAIRegenerateWalletTokenAction(mediator, preview)
            .ExecuteAsync(Guid.NewGuid(), new AdminAIRegenerateWalletTokenInput(walletId), "rotate-1", default);
        var created = await new AdminAICreateWalletAction(mediator, preview)
            .ExecuteAsync(Guid.NewGuid(), new AdminAICreateWalletInput("01000000000", "Main", 100, 1000, []), "create-1", default);

        var serialized = System.Text.Json.JsonSerializer.Serialize(new[] { rotated.SafeResult, created.SafeResult });
        Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("pairingToken", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PasswordReset_RequiresSecureContinuation_AndKeepsPasswordOutOfSafeResult()
    {
        const string password = "Strong-Pass-123!";
        AdminResetPasswordCommand? captured = null;
        var mediator = new BoundaryMediator(request =>
        {
            captured = Assert.IsType<AdminResetPasswordCommand>(request);
            return ApiResponse.Ok("updated");
        });
        var action = new AdminAIResetPasswordAction(mediator, new SafePreviewSource());
        var input = new AdminAIResetPasswordInput(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            action.ExecuteAsync(Guid.NewGuid(), input, "reset-1", default));

        var result = await action.ExecuteSecureAsync(
            Guid.NewGuid(), input, System.Text.Encoding.UTF8.GetBytes(password), "reset-1", default);

        Assert.Equal(password, captured!.NewPassword);
        Assert.DoesNotContain(password, System.Text.Json.JsonSerializer.Serialize(result.SafeResult), StringComparison.Ordinal);
        Assert.Equal("Password", action.SecureInputKind);
    }

    [Theory]
    [InlineData("ordinary", "strong")]
    [InlineData("strong", "ordinary")]
    [InlineData("strong", "none")]
    public void HighRiskCatalog_RejectsEveryRiskOrConfirmationDowngrade(string risk, string confirmation)
    {
        Assert.Throws<InvalidOperationException>(() => new AdminAICapabilityRegistry([Definition("admin.high-risk", risk, confirmation)]));
    }

    private static AdminAICapabilityDefinition Definition(string key, string risk, string confirmation) =>
        new(key, "1", "action", risk, confirmation, "closed-input-v1", "safe-output-v1", 0, 4096, 5000, "AuthoritativeCommand", ["test"]);

    private sealed class StubAction(string key) : IAdminAIActionCapability
    {
        public string Key { get; } = key;
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class SafePreviewSource : IAdminAIActionPreviewSource
    {
        public int Calls { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync<TInput>(string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class
        {
            Calls++;
            return Task.FromResult(new AdminAIActionPreview("target", "safe:1", new { }, new { }, new { affected = 1 }, new { valid = true }, "state-v1"));
        }
    }

    private sealed class BoundaryMediator(Func<object, object> response) : IMediator
    {
        public int SendCalls { get; private set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            return Task.FromResult((TResponse)response(request));
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}

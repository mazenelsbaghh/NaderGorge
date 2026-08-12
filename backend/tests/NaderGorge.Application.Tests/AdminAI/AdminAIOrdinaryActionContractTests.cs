using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.AdminAI;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using MediatR;
using NaderGorge.Application.Features.AdminAI.Catalog;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIOrdinaryActionContractTests
{
    [Fact]
    public async Task TypedMediatRAdapter_PreviewHasNoCommandEffect_AndExecutionBindsActor()
    {
        var actor = Guid.NewGuid();
        var mediator = new CapturingMediator();
        var previews = new PreviewSource();
        var adapter = new AdminAIAddStudentNoteAction(mediator, previews);
        var input = new AdminAIAddStudentNoteInput(Guid.NewGuid(), "safe note", true);

        var preview = await adapter.PreviewAsync(actor, input, default);
        Assert.Equal(1, previews.Calls);
        Assert.Equal(0, mediator.SendCalls);
        Assert.Equal("state-v1", preview.StateFingerprint);

        var outcome = await adapter.ExecuteAsync(actor, input, "execution-1", default);
        var command = Assert.IsType<AddStudentNoteCommand>(mediator.Request);
        Assert.Equal(actor, command.AdminId);
        Assert.Equal(input.StudentId, command.StudentId);
        Assert.Equal(1, mediator.SendCalls);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, outcome.Status);
    }

    [Fact]
    public async Task TypedMediatRAdapter_RejectsUnknownJsonBeforeCommandDispatch()
    {
        var mediator = new CapturingMediator();
        var adapter = new AdminAIAddStudentNoteAction(mediator, new PreviewSource());
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"studentId\":\"00000000-0000-0000-0000-000000000001\",\"content\":\"x\",\"isPinned\":false,\"extra\":true}");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => adapter.ExecuteAsync(Guid.NewGuid(), json, "execution-1", default));
        Assert.Equal(0, mediator.SendCalls);
    }

    [Fact]
    public void Registration_FailsClosedForMissingDuplicateAndWrongRiskAdapters()
    {
        var adapter = new AdminAIAddStudentNoteAction(new CapturingMediator(), new PreviewSource());
        var ordinary = new AdminAICapabilityDefinition(adapter.Key, "1", "action", "ordinary", "ordinary", "{}", "{}", 0, 4096, 5000, "AddStudentNoteCommand", ["students"]);
        var catalog = new AdminAICapabilityRegistry([ordinary]);

        Assert.Single(AdminAIActionCapabilityRegistration.ValidateOrdinaryCoverage(catalog, [adapter]));
        Assert.Throws<InvalidOperationException>(() => AdminAIActionCapabilityRegistration.ValidateOrdinaryCoverage(catalog, []));
        Assert.Throws<InvalidOperationException>(() => AdminAIActionCapabilityRegistration.ValidateOrdinaryCoverage(catalog, [adapter, adapter]));

        var strong = new AdminAICapabilityDefinition(adapter.Key, "1", "action", "strong", "strong", "{}", "{}", 0, 4096, 5000, "AddStudentNoteCommand", ["students"]);
        Assert.Throws<InvalidOperationException>(() => AdminAIActionCapabilityRegistration.ValidateOrdinaryCoverage(new AdminAICapabilityRegistry([strong]), [adapter]));
    }

    [Fact]
    public async Task EveryImplementedOrdinaryKey_HasUniqueClosedAdapterAndReadOnlyPreview()
    {
        var mediator = new CapturingMediator();
        var preview = new PreviewSource();
        var adapters = AdminAIActionCapabilityRegistration.CreateImplementedOrdinaryAdapters(mediator, preview);
        var expected = new[]
        {
            "admin.assessment.community-post.approve", "admin.assessment.lesson-comment.approve",
            "admin.commercial.form.create", "admin.commercial.form.update",
            "admin.content.subject.create", "admin.content.subject.update",
            "admin.content.video-type.create", "admin.content.video-type.update",
            "admin.identity.student-note.create", "admin.operations.task-comment.create",
            "admin.operations.task.create", "admin.operations.task.status.update",
            "admin.tools.media-pipeline.create", "admin.tools.social-plan.create"
        };

        Assert.Equal(expected, adapters.Select(adapter => adapter.Key).Order(StringComparer.Ordinal));
        Assert.Equal(adapters.Count, adapters.Select(adapter => adapter.Key).Distinct(StringComparer.Ordinal).Count());

        foreach (var adapter in adapters)
        {
            var result = await adapter.PreviewAsync(Guid.NewGuid(), InputFor(adapter.Key), default);
            Assert.Equal("state-v1", result.StateFingerprint);
        }
        Assert.Equal(adapters.Count, preview.Calls);
        Assert.Equal(0, mediator.SendCalls);
    }

    [Fact]
    public async Task ConfirmedOrdinaryProposal_ExecutesOnceAndReplaysLedgerResult()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var protector = AdminAIStrongConfirmationTests.Protector();
        var protectedPayload = protector.Protect("proposal-payload", "{\"note\":\"safe\"}"u8);
        var proposal = new AdminAIActionProposal { ActorAdminUserId = actor, CapabilityKey = "test.action", CapabilityVersion = "1", ConfirmationType = AdminAIConfirmationType.Explicit, PrimaryRisk = AdminAIRiskCategory.Ordinary, Status = AdminAIProposalStatus.Confirming, ExpiresAt = DateTime.UtcNow.AddMinutes(5), ProtectedNormalizedPayload = protectedPayload.Ciphertext, PayloadHash = protectedPayload.Digest, StateFingerprint = "state-v1" };
        db.Add(proposal); await db.SaveChangesAsync(); var adapter = new SuccessfulAction();
        var executor = new AdminAIActionExecutor(db, new AdminAIConversationTests.AllowAccess(actor), protector, new NoSecureInput(), [adapter]);
        var first = await executor.ExecuteAsync(actor, proposal.Id, "intent-1", default);
        var replay = await executor.ExecuteAsync(actor, proposal.Id, "intent-1", default);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, first.Status); Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, adapter.ExecuteCalls); Assert.Single(db.AdminAIActionExecutions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(actor, proposal.Id, "different-intent", default));
    }

    [Fact]
    public async Task ChangedState_InvalidatesWithZeroExecution()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var protector = AdminAIStrongConfirmationTests.Protector();
        var protectedPayload = protector.Protect("proposal-payload", "{}"u8);
        var proposal = new AdminAIActionProposal { ActorAdminUserId = actor, CapabilityKey = "test.action", CapabilityVersion = "1", Status = AdminAIProposalStatus.Confirming, ExpiresAt = DateTime.UtcNow.AddMinutes(5), ProtectedNormalizedPayload = protectedPayload.Ciphertext, PayloadHash = protectedPayload.Digest, StateFingerprint = "old" }; db.Add(proposal); await db.SaveChangesAsync();
        var adapter = new SuccessfulAction(); var executor = new AdminAIActionExecutor(db, new AdminAIConversationTests.AllowAccess(actor), protector, new NoSecureInput(), [adapter]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(actor, proposal.Id, "intent", default));
        Assert.Equal(AdminAIProposalStatus.Invalidated, proposal.Status); Assert.Equal(0, adapter.ExecuteCalls); Assert.Empty(db.AdminAIActionExecutions);
    }

    [Fact]
    public async Task ExternalTimeout_PersistsDeterministicIdentityAndRequiresRecovery()
    {
        await using var db = AdminAIStrongConfirmationTests.CreateDb(); var actor = Guid.NewGuid(); var protector = AdminAIStrongConfirmationTests.Protector();
        var protectedPayload = protector.Protect("proposal-payload", "{}"u8);
        var proposal = new AdminAIActionProposal { ActorAdminUserId = actor, CapabilityKey = "external.timeout", CapabilityVersion = "1", Status = AdminAIProposalStatus.Confirming, ExpiresAt = DateTime.UtcNow.AddMinutes(5), ProtectedNormalizedPayload = protectedPayload.Ciphertext, PayloadHash = protectedPayload.Digest, StateFingerprint = "state-v1" }; db.Add(proposal); await db.SaveChangesAsync();
        var adapter = new TimeoutAction(); var executor = new AdminAIActionExecutor(db, new AdminAIConversationTests.AllowAccess(actor), protector, new NoSecureInput(), [adapter]);

        var result = await executor.ExecuteAsync(actor, proposal.Id, "stable-intent", default);

        var execution = Assert.Single(db.AdminAIActionExecutions);
        Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, result.Status);
        Assert.Equal(AdminAIExecutionStatus.RecoveryRequired, execution.Status);
        Assert.Equal(AdminAIProposalStatus.RecoveryRequired, proposal.Status);
        Assert.Equal(execution.Id.ToString("N"), execution.ExternalOperationId);
        Assert.Equal(execution.ExternalOperationId, adapter.SeenOperationId);
        Assert.Equal("external_outcome_unknown", execution.FailureCode);
        Assert.Null(execution.CompletedAt);
    }

    private sealed class SuccessfulAction : IAdminAIActionCapability
    {
        public string Key => "test.action"; public int ExecuteCalls { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct) => Task.FromResult(new AdminAIActionPreview("user", "user:1", new { }, new { }, new { }, new { valid = true }, "state-v1"));
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct) { ExecuteCalls++; return Task.FromResult(new AdminAIActionOutcome(AdminAIExecutionStatus.Succeeded, new { done = true }, 1, ["users"])); }
    }
    private sealed class TimeoutAction : IAdminAIActionCapability
    {
        public string Key => "external.timeout"; public string? SeenOperationId { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct) => Task.FromResult(new AdminAIActionPreview("provider-job", "job:pending", new { }, new { }, new { }, new { valid = true }, "state-v1"));
        public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct) { SeenOperationId = operationId; throw new TimeoutException(); }
    }
    private sealed class NoSecureInput : IAdminAISecureInputService
    {
        public Task<AdminAISecureGrantResult> IssueAsync(Guid actorId, Guid proposalId, string inputKind, long version, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminAISecureGrantResult> SubmitAsync(Guid actorId, Guid grantId, string token, string kind, ReadOnlyMemory<byte> payload, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminAIProtectedValue> ConsumeAsync(Guid actorId, Guid proposalId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class PreviewSource : IAdminAIActionPreviewSource
    {
        public int Calls { get; private set; }
        public Task<AdminAIActionPreview> PreviewAsync<TInput>(string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class
        {
            Calls++;
            return Task.FromResult(new AdminAIActionPreview("student", "student:1", new { notes = 0 }, new { notes = 1 }, new { affected = 1 }, new { valid = true }, "state-v1"));
        }
    }

    private sealed class CapturingMediator : IMediator
    {
        public object? Request { get; private set; }
        public int SendCalls { get; private set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Request = request; SendCalls++;
            object response = ApiResponse.Ok("done");
            return Task.FromResult((TResponse)response);
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    private static object InputFor(string key) => key switch
    {
        "admin.identity.student-note.create" => new AdminAIAddStudentNoteInput(Guid.NewGuid(), "note", false),
        "admin.content.subject.create" => new AdminAICreateSubjectInput("subject", "description"),
        "admin.content.subject.update" => new AdminAIUpdateSubjectInput(Guid.NewGuid(), "subject", "description"),
        "admin.content.video-type.create" => new AdminAICreateVideoTypeInput("type", 1, true),
        "admin.content.video-type.update" => new AdminAIUpdateVideoTypeInput(Guid.NewGuid(), "type", 1),
        "admin.assessment.lesson-comment.approve" => new AdminAIApproveLessonCommentInput(Guid.NewGuid()),
        "admin.assessment.community-post.approve" => new AdminAIApproveCommunityPostInput(Guid.NewGuid()),
        "admin.commercial.form.create" => new AdminAICreateFormInput("form", "description", "form", true, null, null, null, "[]"),
        "admin.commercial.form.update" => new AdminAIUpdateFormInput(Guid.NewGuid(), "form", "description", "form", true, null, null, null, "[]"),
        "admin.operations.task.create" => new AdminAICreateTaskInput("task", "description", Guid.NewGuid(), TaskPriority.Medium, null),
        "admin.operations.task.status.update" => new AdminAIUpdateTaskStatusInput(Guid.NewGuid(), NaderGorge.Domain.Enums.TaskStatus.InProgress),
        "admin.operations.task-comment.create" => new AdminAIAddTaskCommentInput(Guid.NewGuid(), "comment", null),
        "admin.tools.media-pipeline.create" => new AdminAICreateMediaPipelineInput("pipeline", null, null, null),
        "admin.tools.social-plan.create" => new AdminAICreateSocialPlanInput("plan", null, null, SocialPlatform.Facebook, SocialPlanStatus.Draft, DateTime.UtcNow, null),
        _ => throw new InvalidOperationException($"No contract fixture for {key}.")
    };
}

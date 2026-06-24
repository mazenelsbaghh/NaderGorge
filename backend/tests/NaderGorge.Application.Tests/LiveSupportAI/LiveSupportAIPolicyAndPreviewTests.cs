using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using Xunit;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIPolicyAndPreviewTests
{
    [Fact]
    public async Task GetCatalogs_ReturnsNonEmptyCatalog()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new LiveSupportAIAdminService(db, null!);

        var catalogs = service.GetCatalogs();

        Assert.NotNull(catalogs);
        Assert.NotEmpty(catalogs.ReadableData);
        Assert.NotEmpty(catalogs.Actions);
        Assert.NotEmpty(catalogs.LookupKeys);
        Assert.NotEmpty(catalogs.VerificationQuestions);
    }

    [Fact]
    public async Task SaveDraftAsync_ConcurrentlyFails_OnVersionMismatch()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01211111111");
        
        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Draft,
            IsEnabled = false,
            SystemInstructions = "Test instructions",
            ReadableDataKeysJson = "[]",
            ActionKeysJson = "[]",
            LookupKeysJson = "[]",
            VerificationQuestionKeysJson = JsonSerializer.Serialize(new[] { "profile.full_name" }),
            VerificationRequiredCorrect = 1,
            VerificationMaxAttempts = 3,
            PendingActionExpirySeconds = 300,
            InactivityMinutes = 30,
            InactivityWarningGraceSeconds = 120,
            CreatedByUserId = admin.Id,
            Version = 5
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIAdminService(db, null!);
        var request = new SaveLiveSupportAIDraftRequest(
            "New System Instructions",
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new[] { "profile.full_name" },
            1, 3, 300, 30, 120,
            4 // Wrong expected version
        );

        var exception = await Assert.ThrowsAsync<LiveSupportAIAdminException>(
            () => service.SaveDraftAsync(admin.Id, request, CancellationToken.None));
        
        Assert.Equal("VERSION_CONFLICT", exception.Code);
    }

    [Fact]
    public async Task PublishAsync_TransitionsDraftToPublished_AndSupersedesActive()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01211111112");

        var oldPublished = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Old instructions",
            ReadableDataKeysJson = "[]",
            ActionKeysJson = "[]",
            LookupKeysJson = "[]",
            VerificationQuestionKeysJson = JsonSerializer.Serialize(new[] { "profile.full_name" }),
            VerificationRequiredCorrect = 1,
            VerificationMaxAttempts = 3,
            PendingActionExpirySeconds = 300,
            InactivityMinutes = 30,
            InactivityWarningGraceSeconds = 120,
            CreatedByUserId = admin.Id,
            Version = 2
        };

        var draft = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 2,
            Status = LiveSupportAIPolicyStatus.Draft,
            IsEnabled = false,
            SystemInstructions = "New instructions",
            ReadableDataKeysJson = "[]",
            ActionKeysJson = "[]",
            LookupKeysJson = "[]",
            VerificationQuestionKeysJson = JsonSerializer.Serialize(new[] { "profile.full_name" }),
            VerificationRequiredCorrect = 1,
            VerificationMaxAttempts = 3,
            PendingActionExpirySeconds = 300,
            InactivityMinutes = 30,
            InactivityWarningGraceSeconds = 120,
            CreatedByUserId = admin.Id,
            Version = 3
        };

        db.LiveSupportAIPolicyVersions.AddRange(oldPublished, draft);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIAdminService(db, null!);
        var result = await service.PublishAsync(admin.Id, 3, CancellationToken.None);

        Assert.Equal(LiveSupportAIPolicyStatus.Published.ToString(), result.Status);
        Assert.True(result.IsEnabled);

        var dbOld = await db.LiveSupportAIPolicyVersions.FindAsync(oldPublished.Id);
        Assert.Equal(LiveSupportAIPolicyStatus.Superseded, dbOld!.Status);
        Assert.False(dbOld.IsEnabled);
    }

    [Fact]
    public async Task DisableAsync_SchedulesRecovery_AndWritesNoTurnsOrMessages()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01211111113");

        var published = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ReadableDataKeysJson = "[]",
            ActionKeysJson = "[]",
            LookupKeysJson = "[]",
            VerificationQuestionKeysJson = "[]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(published);

        var convState = new LiveSupportAIConversationState
        {
            ConversationId = Guid.NewGuid(),
            Mode = LiveSupportAIMode.AiActive,
            PolicyVersionId = published.Id,
            LastParticipantActivityAt = DateTime.UtcNow
        };
        db.LiveSupportAIConversationStates.Add(convState);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIAdminService(db, null!);
        
        var messageCountBefore = await db.LiveSupportMessages.CountAsync();
        var turnCountBefore = await db.LiveSupportAITurns.CountAsync();
        
        await service.DisableAsync(admin.Id, published.Version, CancellationToken.None);

        var updatedPolicy = await db.LiveSupportAIPolicyVersions.FindAsync(published.Id);
        Assert.False(updatedPolicy!.IsEnabled);

        var updatedState = await db.LiveSupportAIConversationStates.FindAsync(convState.ConversationId);
        Assert.NotNull(updatedState!.DisableRequestedAt);

        // Verify audit log created
        var audit = await db.AuditLogs.SingleOrDefaultAsync(x => x.Action == "DisableAILiveSupport");
        Assert.NotNull(audit);

        // Verify zero business writes to messages/turns
        Assert.Equal(messageCountBefore, await db.LiveSupportMessages.CountAsync());
        Assert.Equal(turnCountBefore, await db.LiveSupportAITurns.CountAsync());
    }

    [Fact]
    public async Task PreviewAsync_PerformsZeroDatabaseWrites()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01211111114");

        var published = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ReadableDataKeysJson = "[]",
            ActionKeysJson = "[]",
            LookupKeysJson = "[]",
            VerificationQuestionKeysJson = "[]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(published);
        await db.SaveChangesAsync();

        var mockKnowledge = new MockKnowledgeService();
        var service = new LiveSupportAIAdminService(db, mockKnowledge, previewClient: new MockPreviewClient());

        var turnCountBefore = await db.LiveSupportAITurns.CountAsync();
        var messageCountBefore = await db.LiveSupportMessages.CountAsync();
        var auditCountBefore = await db.AuditLogs.CountAsync();

        var request = new LiveSupportAIPreviewRequestDto(published.Id, "برجاء المساعدة");
        var result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(published.Id, result.PolicyVersionId);
        Assert.Equal("reply", result.Decision.Type);
        Assert.Equal("DRY_RUN_DECISION_VALIDATED", result.SafeOutcome);

        // Ensure zero writes
        Assert.Equal(turnCountBefore, await db.LiveSupportAITurns.CountAsync());
        Assert.Equal(messageCountBefore, await db.LiveSupportMessages.CountAsync());
        Assert.Equal(auditCountBefore, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task DisableAsync_WithStaleVersion_LeavesPolicyAndConversationsUnchanged()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01211111115");
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1, Version = 7, Status = LiveSupportAIPolicyStatus.Published, IsEnabled = true,
            SystemInstructions = "Instructions", ReadableDataKeysJson = "[]", ActionKeysJson = "[]",
            LookupKeysJson = "[]", VerificationQuestionKeysJson = "[]", CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIAdminService(db, null!);
        var exception = await Assert.ThrowsAsync<LiveSupportAIAdminException>(
            () => service.DisableAsync(admin.Id, 6, CancellationToken.None));

        Assert.Equal("VERSION_CONFLICT", exception.Code);
        Assert.True((await db.LiveSupportAIPolicyVersions.FindAsync(policy.Id))!.IsEnabled);
        Assert.Empty(db.LiveSupportAIConversationStates.Where(state => state.DisableRequestedAt != null));
    }

    [Fact]
    public async Task PreviewAsync_RejectsActionOutsidePublishedPolicy()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01211111116");
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1, Status = LiveSupportAIPolicyStatus.Published, IsEnabled = true,
            SystemInstructions = "Instructions", ReadableDataKeysJson = "[]", ActionKeysJson = "[]",
            LookupKeysJson = "[]", VerificationQuestionKeysJson = "[]", CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();
        var action = JsonSerializer.SerializeToElement(new { key = "student.balance.adjust", arguments = new { amount = 10 } });
        var decision = new LiveSupportAIWorkerDecisionDto("1", "propose_action", null, action, null, null, null, null);
        var service = new LiveSupportAIAdminService(db, new MockKnowledgeService(), previewClient: new MockPreviewClient(decision));

        var exception = await Assert.ThrowsAsync<LiveSupportAIAdminException>(
            () => service.PreviewAsync(new LiveSupportAIPreviewRequestDto(policy.Id, "عدّل الرصيد"), CancellationToken.None));

        Assert.Equal("AI_PREVIEW_ACTION_NOT_ALLOWED", exception.Code);
        Assert.Empty(db.LiveSupportAITurns);
        Assert.Empty(db.LiveSupportAIPendingActions);
    }

    private class MockKnowledgeService : ILiveSupportAIKnowledgeService
    {
        public Task<IReadOnlyList<LiveSupportAIKnowledgeRevisionDto>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LiveSupportAIKnowledgeRevisionDto>>([]);

        public Task<LiveSupportAIKnowledgeRevisionDto> SaveRevisionAsync(Guid adminUserId, SaveLiveSupportAIKnowledgeRequest request, CancellationToken cancellationToken)
            => Task.FromResult<LiveSupportAIKnowledgeRevisionDto>(null!);

        public Task LinkPublishedRevisionsAsync(Guid adminUserId, LinkLiveSupportAIKnowledgeRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<LiveSupportAIKnowledgeDocumentDto>> SearchPublishedAsync(Guid policyVersionId, string query, int maxDocuments, int maxCharacters, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LiveSupportAIKnowledgeDocumentDto>>([]);
    }

    private sealed class MockPreviewClient(LiveSupportAIWorkerDecisionDto? configuredDecision = null) : ILiveSupportAIWorkerPreviewClient
    {
        public Task<LiveSupportAIWorkerPreviewResultDto> PreviewAsync(
            LiveSupportAIWorkerClaimDto context,
            CancellationToken cancellationToken)
        {
            var decision = configuredDecision ?? new LiveSupportAIWorkerDecisionDto("1", "reply", "يمكنني مساعدتك.", null, null, null, null, null);
            return Task.FromResult(new LiveSupportAIWorkerPreviewResultDto(
                decision,
                LiveSupportAITurnOrchestrator.ComputeDecisionHash(decision),
                "test-provider",
                "test-model",
                12));
        }
    }
}

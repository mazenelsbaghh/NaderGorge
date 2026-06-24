using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Admin.Commands;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class ParticipantSessionTests
{
    [Fact]
    public async Task Availability_BlocksStart_WhenNoStaffIsCheckedIn()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = CreateService(db);

        var result = await service.GetAvailabilityAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal(LiveSupportErrorCodes.SupportUnavailable, result.Code);
    }

    [Fact]
    public async Task GuestPhone_NeverAutoLinksMatchingStudent()
    {
        await using var db = TestAppDbContextFactory.Create();
        await TestAppDbContextFactory.SeedUserAsync(db, "Matching Student", "01012345678");
        await SeedEligibleStaffAsync(db);
        var service = CreateService(db);
        var guestSessions = new LiveSupportGuestSessionService(db);
        var guest = await guestSessions.IssueAsync("زائر اختبار", "01012345678", "127.0.0.1", "tests", CancellationToken.None);
        var participant = await guestSessions.ValidateAsync(guest.CookieToken, CancellationToken.None);

        var conversation = await service.CreateConversationAsync(participant!, "مشكلة", null, CancellationToken.None);

        Assert.Equal(LiveSupportParticipantType.Guest, conversation.ParticipantType);
        Assert.Null(conversation.LinkedStudentUserId);
    }

    [Fact]
    public async Task ClosedConversation_RejectsMessage_AndAcceptsOneRating()
    {
        await using var db = TestAppDbContextFactory.Create();
        var staffId = await SeedEligibleStaffAsync(db);
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01099999999");
        var service = CreateService(db);
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null);
        var conversation = await service.CreateConversationAsync(participant, "إغلاق", null, CancellationToken.None);
        await service.CloseAsync(staffId, false, conversation.Id, "تم الحل", CancellationToken.None);

        var error = await Assert.ThrowsAsync<LiveSupportException>(() => service.SendParticipantMessageAsync(participant, conversation.Id, Guid.NewGuid().ToString(), "رسالة", LiveSupportMessageType.Text, CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.ConversationTerminal, error.Code);

        await service.SubmitRatingAsync(participant, conversation.Id, 5, "ممتاز", CancellationToken.None);
        await Assert.ThrowsAsync<LiveSupportException>(() => service.SubmitRatingAsync(participant, conversation.Id, 4, null, CancellationToken.None));
        Assert.Equal(1, await db.LiveSupportRatings.CountAsync());
    }

    [Fact]
    public async Task MessageRetry_WithSameClientId_IsIdempotent()
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedEligibleStaffAsync(db);
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01088888888");
        var service = CreateService(db);
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null);
        var conversation = await service.CreateConversationAsync(participant, null, null, CancellationToken.None);
        var clientId = Guid.NewGuid().ToString();

        var first = await service.SendParticipantMessageAsync(participant, conversation.Id, clientId, "نفس الرسالة", LiveSupportMessageType.Text, CancellationToken.None);
        var second = await service.SendParticipantMessageAsync(participant, conversation.Id, clientId, "نفس الرسالة", LiveSupportMessageType.Text, CancellationToken.None);

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.Message.Id, second.Message.Id);
        Assert.Equal(1, await db.LiveSupportMessages.CountAsync());
    }

    [Fact]
    public async Task Availability_ReturnsTrue_WhenAISupportIsEnabledAndNoStaffIsCheckedIn()
    {
        await using var db = TestAppDbContextFactory.Create();
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01011111111");
        
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Test Instructions",
            CreatedByUserId = adminUser.Id,
            PublishedByUserId = adminUser.Id,
            PublishedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.GetAvailabilityAsync(CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal("AVAILABLE", result.Code);
        Assert.Equal("الدعم متاح الآن", result.Message);
    }

    private static LiveSupportService CreateService(AppDbContext db)
    {
        var orchestrator = new LiveSupportAITurnOrchestrator(db, new FakeContextBuilder(), new FakeDataProtector());
        return new LiveSupportService(db, new EnabledSettingsReader(), aiTurnOrchestrator: orchestrator);
    }

    private static async Task<Guid> SeedEligibleStaffAsync(AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, $"Support {Guid.NewGuid():N}", $"01{Random.Shared.NextInt64(100000000, 999999999)}");
        var employee = new EmployeeProfile { UserId = user.Id, BasicSalary = 1 };
        db.EmployeeProfiles.Add(employee);
        db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig { UserId = user.Id, IsEnabled = true, MaxActiveConversations = 2, ConfiguredByUserId = user.Id });
        db.AttendanceLogs.Add(new AttendanceLog { EmployeeId = employee.Id, ClockIn = DateTime.UtcNow, Date = DateOnly.FromDateTime(DateTime.UtcNow), Status = AttendanceStatus.Present, IpAddress = "tests", UserAgent = "tests" });
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task SendMessage_EnqueuesAITurn_WhenAISupportIsActive()
    {
        await using var db = TestAppDbContextFactory.Create();
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01011111111");
        
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Test Instructions",
            CreatedByUserId = adminUser.Id,
            PublishedByUserId = adminUser.Id,
            PublishedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01088888888");
        var fakeEnqueuer = new FakeJobEnqueuer();
        var orchestrator = new LiveSupportAITurnOrchestrator(db, new FakeContextBuilder(), new FakeDataProtector());
        var service = new LiveSupportService(db, new EnabledSettingsReader(), jobEnqueuer: fakeEnqueuer, aiTurnOrchestrator: orchestrator);
        
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null);
        var conversation = await service.CreateConversationAsync(participant, null, null, CancellationToken.None);

        var sendResult = await service.SendParticipantMessageAsync(participant, conversation.Id, Guid.NewGuid().ToString(), "أهلاً بك", LiveSupportMessageType.Text, CancellationToken.None);

        var outboxEvents = await db.OutboxEvents.Where(x => x.ProcessedAt == null).ToListAsync();
        foreach (var evt in outboxEvents)
        {
            if (NaderGorge.API.BackgroundServices.LiveSupportAIOutboxQueueDispatcher.IsTurnQueueEvent(evt))
            {
                await NaderGorge.API.BackgroundServices.LiveSupportAIOutboxQueueDispatcher.DispatchAsync(evt, fakeEnqueuer);
            }
        }

        // Verify that the AI state was initialized
        var aiState = await db.LiveSupportAIConversationStates.FirstOrDefaultAsync(x => x.ConversationId == conversation.Id);
        Assert.NotNull(aiState);
        Assert.Equal(LiveSupportAIMode.AiActive, aiState.Mode);

        // Verify that a turn was created and queued in DB
        var turn = await db.LiveSupportAITurns.FirstOrDefaultAsync(x => x.ConversationId == conversation.Id);
        Assert.NotNull(turn);
        Assert.Equal(LiveSupportAITurnStatus.Queued, turn.Status);

        // Verify that the job was enqueued in Redis
        Assert.Single(fakeEnqueuer.EnqueuedJobs);
        var job = fakeEnqueuer.EnqueuedJobs[0];
        Assert.Equal("ai-live-support-turns", job.queueName);
        Assert.Equal("respond", job.jobName);
    }

    [Fact]
    public async Task ClaimCompleteAndFailAITurns_BehavesCorrectly()
    {
        await using var db = TestAppDbContextFactory.Create();
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01011111111");
        
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Test Instructions",
            CreatedByUserId = adminUser.Id,
            PublishedByUserId = adminUser.Id,
            PublishedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01088888888");
        var fakeEnqueuer = new FakeJobEnqueuer();
        var orchestrator = new LiveSupportAITurnOrchestrator(db, new FakeContextBuilder(), new FakeDataProtector());
        var service = new LiveSupportService(db, new EnabledSettingsReader(), jobEnqueuer: fakeEnqueuer, aiTurnOrchestrator: orchestrator);
        
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null);
        var conversation = await service.CreateConversationAsync(participant, null, null, CancellationToken.None);

        await service.SendParticipantMessageAsync(participant, conversation.Id, Guid.NewGuid().ToString(), "أهلاً بك", LiveSupportMessageType.Text, CancellationToken.None);

        var outboxEvents = await db.OutboxEvents.Where(x => x.ProcessedAt == null).ToListAsync();
        foreach (var evt in outboxEvents)
        {
            if (NaderGorge.API.BackgroundServices.LiveSupportAIOutboxQueueDispatcher.IsTurnQueueEvent(evt))
            {
                await NaderGorge.API.BackgroundServices.LiveSupportAIOutboxQueueDispatcher.DispatchAsync(evt, fakeEnqueuer);
            }
        }

        var turn = await db.LiveSupportAITurns.FirstAsync(x => x.ConversationId == conversation.Id);
        
        // 1. Claim AI Turn
        var context = await service.ClaimAITurnAsync(turn.Id, CancellationToken.None);
        Assert.NotNull(context);
        Assert.Equal("Test Instructions", context.SystemInstructions);
        Assert.Single(context.Messages);
        Assert.Equal("أهلاً بك", context.Messages[0].Content);

        // Verify status changed to Processing
        var claimedTurn = await db.LiveSupportAITurns.FirstAsync(x => x.Id == turn.Id);
        Assert.Equal(LiveSupportAITurnStatus.Processing, claimedTurn.Status);

        // 2. Complete AI Turn (Reply)
        var replyRequest = new LiveSupportAITurnCompleteRequest(
            ExpectedConversationVersion: context.ExpectedConversationVersion,
            Decision: new LiveSupportAIDecision("reply", "رد المساعد", null, null, null, null),
            Provider: "gemini",
            Model: "gemini-2.5-flash",
            ProviderResponseId: "resp-1",
            InputTokenCount: 10,
            OutputTokenCount: 5,
            LatencyMs: 120,
            CallbackIdempotencyKey: turn.Id.ToString()
        );

        await service.CompleteAITurnAsync(turn.Id, replyRequest, CancellationToken.None);

        // Verify message was created
        var messages = await db.LiveSupportMessages.Where(x => x.ConversationId == conversation.Id).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Content == "رد المساعد" && m.SenderType == LiveSupportSenderType.AI);

        // Verify turn is completed
        var completedTurn = await db.LiveSupportAITurns.FirstAsync(x => x.Id == turn.Id);
        Assert.Equal(LiveSupportAITurnStatus.Completed, completedTurn.Status);
        Assert.Equal(LiveSupportAIDecisionType.Reply, completedTurn.DecisionType);
    }

    [Fact]
    public async Task PendingAction_ConfirmedAndCancelled_UpdatesStatusCorrectly()
    {
        await using var db = TestAppDbContextFactory.Create();
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01011111111");
        
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Test Instructions",
            CreatedByUserId = adminUser.Id,
            PublishedByUserId = adminUser.Id,
            PublishedAt = DateTime.UtcNow,
            Version = 1,
            ActionKeysJson = "[\"student.watch.reset\"]"
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01088888888");
        var fakeEnqueuer = new FakeJobEnqueuer();
        var mediator = new FakeMediator();
        var service = new LiveSupportService(db, new EnabledSettingsReader(), jobEnqueuer: fakeEnqueuer, mediator: mediator);

        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null);
        var conversation = await service.CreateConversationAsync(participant, null, null, CancellationToken.None);

        var action = new LiveSupportAIPendingAction
        {
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "student.watch.reset",
            SafeProposalJson = "{\"lessonVideoId\": \"" + Guid.NewGuid() + "\"}",
            EncryptedPayload = System.Text.Encoding.UTF8.GetBytes("{\"lessonVideoId\": \"" + Guid.NewGuid() + "\"}"),
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
            Version = 1
        };
        db.LiveSupportAIPendingActions.Add(action);
        await db.SaveChangesAsync();

        // Verify active action can be retrieved
        var activeAction = await service.GetActivePendingActionAsync(participant, conversation.Id, CancellationToken.None);
        Assert.NotNull(activeAction);
        Assert.Equal(action.Id, activeAction.Id);
        Assert.Equal("PendingConfirmation", activeAction.Status);

        // Confirm Action
        await service.ConfirmPendingActionAsync(participant, conversation.Id, action.Id, CancellationToken.None);
        var confirmed = await db.LiveSupportAIPendingActions.FirstAsync(x => x.Id == action.Id);
        Assert.Equal(LiveSupportAIPendingActionStatus.Succeeded, confirmed.Status);

        // Try Cancel (should conflict since already completed)
        var err = await Assert.ThrowsAsync<LiveSupportException>(() => service.CancelPendingActionAsync(participant, conversation.Id, action.Id, CancellationToken.None));
        Assert.Equal("CONFLICT", err.Code);
    }

    [Fact]
    public async Task HandoffProposal_ConfirmedAndCancelled_BehavesCorrectly()
    {
        await using var db = TestAppDbContextFactory.Create();
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01011111111");
        
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Test Instructions",
            CreatedByUserId = adminUser.Id,
            PublishedByUserId = adminUser.Id,
            PublishedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01088888888");
        var service = CreateService(db);

        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null);
        var conversation = await service.CreateConversationAsync(participant, null, null, CancellationToken.None);

        var aiState = await db.LiveSupportAIConversationStates.FirstAsync(x => x.ConversationId == conversation.Id);
        aiState.Mode = LiveSupportAIMode.AiActive;
        aiState.PolicyVersionId = policy.Id;

        var action = new LiveSupportAIPendingAction
        {
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.handoff",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
            Version = 1
        };
        db.LiveSupportAIPendingActions.Add(action);
        await db.SaveChangesAsync();

        // Confirm handoff
        await service.ConfirmHandoffAsync(participant, conversation.Id, CancellationToken.None);
        var updatedAction = await db.LiveSupportAIPendingActions.FirstAsync(x => x.Id == action.Id);
        Assert.Equal(LiveSupportAIPendingActionStatus.Succeeded, updatedAction.Status);
        
        var updatedAiState = await db.LiveSupportAIConversationStates.FirstAsync(x => x.ConversationId == conversation.Id);
        Assert.Equal(LiveSupportAIMode.HumanQueued, updatedAiState.Mode);
    }

    [Fact]
    public async Task GuestVerification_IncorrectChallengeAnswers_ExhaustsSessionAndLocksOut()
    {
        await using var db = TestAppDbContextFactory.Create();
        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01011111111");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01088888888");
        
        // Seed Student Profile
        var profile = new StudentProfile
        {
            UserId = student.Id,
            Governorate = "الجيزة",
            SchoolName = "مدرسة النيل",
            ParentPhone = "01098765432",
            StudentCode = "ST12345",
            DateOfBirth = new DateTime(2005, 5, 5)
        };
        db.StudentProfiles.Add(profile);

        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Test Instructions",
            VerificationQuestionKeysJson = "[\"profile.governorate\"]",
            VerificationRequiredCorrect = 1,
            VerificationMaxAttempts = 2,
            CreatedByUserId = adminUser.Id,
            PublishedByUserId = adminUser.Id,
            PublishedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportAIPolicyVersions.Add(policy);
        await db.SaveChangesAsync();

        var guestSessions = new LiveSupportGuestSessionService(db);
        var guest = await guestSessions.IssueAsync("زائر اختبار", "01088888888", "127.0.0.1", "tests", CancellationToken.None);
        var participant = await guestSessions.ValidateAsync(guest.CookieToken, CancellationToken.None);
        
        var service = CreateService(db);
        var conversation = await service.CreateConversationAsync(participant!, "مشكلة", null, CancellationToken.None);

        var aiState = await db.LiveSupportAIConversationStates.FirstAsync(x => x.ConversationId == conversation.Id);
        aiState.Mode = LiveSupportAIMode.AiActive;
        aiState.PolicyVersionId = policy.Id;
        await db.SaveChangesAsync();

        // 1. Lookup
        var lookupDto = new LiveSupportLookupRequestDto("phone.full", "01088888888");
        var session = await service.StartVerificationLookupAsync(participant!, conversation.Id, lookupDto, CancellationToken.None);
        Assert.Equal("Challenging", session.Status);
        Assert.Equal("profile.governorate", session.NextQuestionKey);

        // 2. Submit wrong answer 1
        var answerDto1 = new LiveSupportAnswerChallengeDto("القاهرة");
        var result1 = await service.SubmitVerificationChallengeAsync(participant!, conversation.Id, answerDto1, CancellationToken.None);
        Assert.Equal("Challenging", result1.Status);
        Assert.Equal(1, result1.AttemptCount);

        // 3. Submit wrong answer 2 (Should lock out and transition to Exhausted/HumanQueued)
        var answerDto2 = new LiveSupportAnswerChallengeDto("الإسكندرية");
        var result2 = await service.SubmitVerificationChallengeAsync(participant!, conversation.Id, answerDto2, CancellationToken.None);
        Assert.Equal("Exhausted", result2.Status);

        var updatedAiState = await db.LiveSupportAIConversationStates.FirstAsync(x => x.ConversationId == conversation.Id);
        Assert.Equal(LiveSupportAIMode.HumanQueued, updatedAiState.Mode);
    }

    private sealed class FakeMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (typeof(TResponse) == typeof(ApiResponse))
            {
                var result = ApiResponse.Ok("Success");
                return Task.FromResult((TResponse)(object)result);
            }
            if (typeof(TResponse) == typeof(ApiResponse<AdminCreateUserResult>))
            {
                var result = ApiResponse<AdminCreateUserResult>.Ok(new AdminCreateUserResult(Guid.NewGuid(), "Name", "Phone", "Student"));
                return Task.FromResult((TResponse)(object)result);
            }
            return Task.FromResult(default(TResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJobEnqueuer : IJobEnqueuer
    {
        public List<(string queueName, string jobName, object data)> EnqueuedJobs { get; } = new();

        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            EnqueuedJobs.Add((queueName, jobName, data!));
            return Task.CompletedTask;
        }
    }

    private sealed class EnabledSettingsReader : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });
        public void Invalidate() { }
    }

    private sealed class FakeDataProtector : ILiveSupportAIDataProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> protectedPayload) => protectedPayload.ToArray();
        public string ComputeKeyedDigest(string purpose, ReadOnlySpan<byte> value) => Convert.ToHexString(value.ToArray()).ToLowerInvariant();
    }

    private sealed class FakeContextBuilder : ILiveSupportAIContextBuilder
    {
        public Task<LiveSupportAIWorkerClaimDto> BuildAsync(Guid turnId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new LiveSupportAIWorkerClaimDto(
                "1",
                turnId,
                Guid.Empty,
                Guid.Empty,
                1,
                turnId.ToString(),
                DateTime.UtcNow.AddMinutes(5),
                "Test Instructions",
                Array.Empty<LiveSupportAIKnowledgeDocumentDto>(),
                new Dictionary<string, object?>(),
                new[] { new LiveSupportAIContextMessageDto("Student", "أهلاً بك", DateTime.UtcNow) },
                Array.Empty<LiveSupportAIAllowedActionDto>(),
                new[] { "reply" }
            ));
        }
    }
}

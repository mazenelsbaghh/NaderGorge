using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using Xunit;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIVerificationTests
{
    private readonly IConfiguration _configuration;
    private readonly LiveSupportAIDataProtector _protector;

    public LiveSupportAIVerificationTests()
    {
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789",
            ["LiveSupportAI:SystemActorUserId"] = Guid.NewGuid().ToString()
        }).Build();
        _protector = new LiveSupportAIDataProtector(_configuration);
    }

    [Fact]
    public void Verification_storage_has_digest_and_outcome_only_without_raw_lookup_or_answer()
    {
        var sessionProperties = typeof(LiveSupportAIVerificationSession).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var attemptProperties = typeof(LiveSupportAIVerificationAttempt).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(sessionProperties, property => property.Name == nameof(LiveSupportAIVerificationSession.LookupValueHash));
        Assert.DoesNotContain(sessionProperties, property => property.Name is "LookupValue" or "ExpectedAnswer");
        Assert.DoesNotContain(attemptProperties, property => property.Name.Contains("Answer", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attemptProperties, property => property.Name == nameof(LiveSupportAIVerificationAttempt.OutcomeCodesJson));
    }

    [Fact]
    public async Task Lookup_WithNonExistentUser_ReturnsChallengingState_WithoutExistenceDisclosure()
    {
        await using var db = TestAppDbContextFactory.Create();
        var systemUser = await TestAppDbContextFactory.SeedUserAsync(db, "System Actor", "01200000000");
        var customConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789",
            ["LiveSupportAI:SystemActorUserId"] = systemUser.Id.ToString()
        }).Build();

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            VerificationRequiredCorrect = 1,
            VerificationMaxAttempts = 3,
            CreatedByUserId = systemUser.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var question = new LiveSupportAIVerificationPolicyQuestion
        {
            Id = Guid.NewGuid(),
            PolicyVersionId = policy.Id,
            QuestionKey = "profile.governorate",
            PromptText = "ما هي المحافظة المسجلة بحسابك؟",
            Order = 1
        };
        db.LiveSupportAIVerificationPolicyQuestions.Add(question);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            GuestSessionId = Guid.NewGuid(),
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var state = new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            PolicyVersionId = policy.Id,
            Mode = LiveSupportAIMode.AiActive,
            Version = 1
        };
        db.LiveSupportAIConversationStates.Add(state);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIVerificationService(db, _protector, customConfig);
        var request = new LiveSupportAIVerificationLookupCommandDto("phone.full", "01299999999", "idemp-key-1");
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, conversation.GuestSessionId);

        var result = await service.StartLookupAsync(participant, conversation.Id, request, CancellationToken.None);

        Assert.Equal(LiveSupportAIVerificationStatus.Challenging, result.Status);
        Assert.Equal("ما هي المحافظة المسجلة بحسابك؟", result.PromptText);
        Assert.Equal(0, result.AttemptCount);
        Assert.Equal(3, result.MaxAttempts);

        var savedSession = await db.LiveSupportAIVerificationSessions.SingleAsync(s => s.Id == result.SessionId);
        Assert.Null(savedSession.CandidateStudentUserId);
        Assert.Equal("phone.full", savedSession.LookupKey);
        Assert.Equal(_protector.ComputeKeyedDigest("verification-lookup", Encoding.UTF8.GetBytes("01299999999")), savedSession.LookupValueHash);
    }

    [Fact]
    public async Task Lookup_WithMultipleMatches_ReturnsChallengingState_WithNullCandidateStudentUserId()
    {
        await using var db = TestAppDbContextFactory.Create();
        var systemUser = await TestAppDbContextFactory.SeedUserAsync(db, "System Actor", "01200000000");
        var customConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789",
            ["LiveSupportAI:SystemActorUserId"] = systemUser.Id.ToString()
        }).Build();

        // Two users with same phone number
        var user1 = await TestAppDbContextFactory.SeedUserAsync(db, "User 1", "01211112222");
        var user2 = await TestAppDbContextFactory.SeedUserAsync(db, "User 2", "01211112222");

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            VerificationRequiredCorrect = 1,
            VerificationMaxAttempts = 3,
            CreatedByUserId = systemUser.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var question = new LiveSupportAIVerificationPolicyQuestion
        {
            Id = Guid.NewGuid(),
            PolicyVersionId = policy.Id,
            QuestionKey = "profile.governorate",
            PromptText = "ما هي المحافظة المسجلة بحسابك؟",
            Order = 1
        };
        db.LiveSupportAIVerificationPolicyQuestions.Add(question);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            GuestSessionId = Guid.NewGuid(),
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var state = new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            PolicyVersionId = policy.Id,
            Mode = LiveSupportAIMode.AiActive,
            Version = 1
        };
        db.LiveSupportAIConversationStates.Add(state);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIVerificationService(db, _protector, customConfig);
        var request = new LiveSupportAIVerificationLookupCommandDto("phone.full", "01211112222", "idemp-key-1");
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, conversation.GuestSessionId);

        var result = await service.StartLookupAsync(participant, conversation.Id, request, CancellationToken.None);

        var savedSession = await db.LiveSupportAIVerificationSessions.SingleAsync(s => s.Id == result.SessionId);
        Assert.Null(savedSession.CandidateStudentUserId); // Since multiple matches, should be null to avoid disclosure and link issues
    }

    [Fact]
    public async Task SubmitAnswer_ChallengingSessionExceedsExpiry_LocksSessionAndThrowsVerificationExpired()
    {
        await using var db = TestAppDbContextFactory.Create();
        var systemUser = await TestAppDbContextFactory.SeedUserAsync(db, "System Actor", "01200000000");

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            GuestSessionId = Guid.NewGuid(),
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var session = new LiveSupportAIVerificationSession
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            PolicyVersionId = Guid.NewGuid(),
            CandidateStudentUserId = Guid.NewGuid(),
            LookupKey = "phone.full",
            LookupValueHash = "hash",
            SelectedQuestionKeysJson = "[\"profile.governorate\"]",
            RequiredCorrect = 1,
            MaxAttempts = 3,
            Status = LiveSupportAIVerificationStatus.Challenging,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Expired!
            Version = 1
        };
        db.LiveSupportAIVerificationSessions.Add(session);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIVerificationService(db, _protector, _configuration);
        var request = new LiveSupportAIVerificationAnswerCommandDto(session.Id, "Cairo", "idemp-key-1");
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, conversation.GuestSessionId);

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => service.SubmitAnswerAsync(participant, conversation.Id, request, CancellationToken.None));
        Assert.Equal("VERIFICATION_EXPIRED", ex.Code);

        var savedSession = await db.LiveSupportAIVerificationSessions.SingleAsync(s => s.Id == session.Id);
        Assert.Equal(LiveSupportAIVerificationStatus.Failed, savedSession.Status);
        Assert.NotNull(savedSession.LockedAt);
        Assert.NotNull(savedSession.CompletedAt);
    }

    [Fact]
    public async Task SubmitAnswer_IncorrectAnswersUpToMaxAttempts_ExhaustsSessionAndQueuesHuman()
    {
        await using var db = TestAppDbContextFactory.Create();
        var systemUser = await TestAppDbContextFactory.SeedUserAsync(db, "System Actor", "01200000000");

        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student User", "01233334444");
        var profile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            Governorate = "Cairo",
            DateOfBirth = new DateTime(2005, 5, 5),
            SchoolName = "School"
        };
        db.StudentProfiles.Add(profile);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            GuestSessionId = Guid.NewGuid(),
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var state = new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            PolicyVersionId = Guid.NewGuid(),
            Mode = LiveSupportAIMode.AiActive,
            Version = 1
        };
        db.LiveSupportAIConversationStates.Add(state);

        var session = new LiveSupportAIVerificationSession
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            PolicyVersionId = state.PolicyVersionId,
            CandidateStudentUserId = student.Id,
            LookupKey = "phone.full",
            LookupValueHash = "hash",
            SelectedQuestionKeysJson = "[\"profile.governorate\"]",
            RequiredCorrect = 1,
            MaxAttempts = 2, // Only 2 attempts
            Status = LiveSupportAIVerificationStatus.Challenging,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Version = 1
        };
        db.LiveSupportAIVerificationSessions.Add(session);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIVerificationService(db, _protector, _configuration);
        var request1 = new LiveSupportAIVerificationAnswerCommandDto(session.Id, "Alexandria", "idemp-1"); // Incorrect
        var request2 = new LiveSupportAIVerificationAnswerCommandDto(session.Id, "Giza", "idemp-2"); // Incorrect again, exhausts!
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, conversation.GuestSessionId);

        // First attempt (fails but remains challenging)
        var result1 = await service.SubmitAnswerAsync(participant, conversation.Id, request1, CancellationToken.None);
        Assert.Equal(LiveSupportAIVerificationStatus.Challenging, result1.Status);
        Assert.Equal(1, result1.AttemptCount);

        // Second attempt (fails and exhausts, triggers queue)
        var result2 = await service.SubmitAnswerAsync(participant, conversation.Id, request2, CancellationToken.None);
        Assert.Equal(LiveSupportAIVerificationStatus.Exhausted, result2.Status);
        Assert.Equal(2, result2.AttemptCount);

        var savedSession = await db.LiveSupportAIVerificationSessions.SingleAsync(s => s.Id == session.Id);
        Assert.Equal(LiveSupportAIVerificationStatus.Exhausted, savedSession.Status);
        Assert.NotNull(savedSession.LockedAt);
        Assert.NotNull(savedSession.CompletedAt);

        var savedState = await db.LiveSupportAIConversationStates.SingleAsync(s => s.ConversationId == conversation.Id);
        Assert.Equal(LiveSupportAIMode.HumanQueued, savedState.Mode);
        Assert.Equal("VERIFICATION_EXHAUSTED", savedState.HandoffReasonCode);

        var queueEntryExists = await db.LiveSupportQueueEntries.AnyAsync(q => q.ConversationId == conversation.Id && q.DequeuedAt == null);
        Assert.True(queueEntryExists);

        var savedConversation = await db.LiveSupportConversations.SingleAsync(c => c.Id == conversation.Id);
        Assert.Equal(LiveSupportConversationStatus.Waiting, savedConversation.Status);
    }

    [Fact]
    public async Task SubmitAnswer_CorrectAnswers_ProgressesQuestionIndex_AndVerifiesOnSuccess()
    {
        await using var db = TestAppDbContextFactory.Create();
        var systemUser = await TestAppDbContextFactory.SeedUserAsync(db, "System Actor", "01200000000");
        var customConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789",
            ["LiveSupportAI:SystemActorUserId"] = systemUser.Id.ToString()
        }).Build();

        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student User", "01233334444");
        var profile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            Governorate = "Cairo",
            SchoolName = "Elite School",
            DateOfBirth = new DateTime(2005, 5, 5)
        };
        db.StudentProfiles.Add(profile);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            GuestSessionId = Guid.NewGuid(),
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var state = new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            PolicyVersionId = Guid.NewGuid(),
            Mode = LiveSupportAIMode.AiActive,
            Version = 1
        };
        db.LiveSupportAIConversationStates.Add(state);

        var session = new LiveSupportAIVerificationSession
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            PolicyVersionId = state.PolicyVersionId,
            CandidateStudentUserId = student.Id,
            LookupKey = "phone.full",
            LookupValueHash = "hash",
            SelectedQuestionKeysJson = "[\"profile.governorate\",\"profile.school_name\"]",
            RequiredCorrect = 2,
            MaxAttempts = 3,
            Status = LiveSupportAIVerificationStatus.Challenging,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Version = 1
        };
        db.LiveSupportAIVerificationSessions.Add(session);
        await db.SaveChangesAsync();

        var service = new LiveSupportAIVerificationService(db, _protector, customConfig);
        var request1 = new LiveSupportAIVerificationAnswerCommandDto(session.Id, "Cairo", "idemp-1"); // Correct governorate
        var request2 = new LiveSupportAIVerificationAnswerCommandDto(session.Id, "Elite School", "idemp-2"); // Correct school, verifies!
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, conversation.GuestSessionId);

        // Submit first correct answer
        var result1 = await service.SubmitAnswerAsync(participant, conversation.Id, request1, CancellationToken.None);
        Assert.Equal(LiveSupportAIVerificationStatus.Challenging, result1.Status);
        Assert.Equal(1, result1.AttemptCount);

        var sessionAfterFirst = await db.LiveSupportAIVerificationSessions.SingleAsync(s => s.Id == session.Id);
        Assert.Equal(1, sessionAfterFirst.CorrectCount);
        Assert.Equal(1, sessionAfterFirst.CurrentQuestionIndex);

        // Submit second correct answer
        var result2 = await service.SubmitAnswerAsync(participant, conversation.Id, request2, CancellationToken.None);
        Assert.Equal(LiveSupportAIVerificationStatus.Verified, result2.Status);

        var sessionAfterSecond = await db.LiveSupportAIVerificationSessions.SingleAsync(s => s.Id == session.Id);
        Assert.Equal(LiveSupportAIVerificationStatus.Verified, sessionAfterSecond.Status);
        Assert.NotNull(sessionAfterSecond.VerifiedAt);
        Assert.NotNull(sessionAfterSecond.CompletedAt);

        var savedConversation = await db.LiveSupportConversations.SingleAsync(c => c.Id == conversation.Id);
        Assert.Equal(student.Id, savedConversation.LinkedStudentUserId);

        var savedState = await db.LiveSupportAIConversationStates.SingleAsync(s => s.ConversationId == conversation.Id);
        Assert.Equal(student.Id, savedState.VerifiedStudentUserId);

        var linkHistoryExists = await db.LiveSupportStudentLinkHistories.AnyAsync(h => h.ConversationId == conversation.Id && h.NewStudentUserId == student.Id);
        Assert.True(linkHistoryExists);
    }
}

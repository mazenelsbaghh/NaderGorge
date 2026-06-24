using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Auth.Commands;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using Xunit;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIRegistrationTests
{
    private readonly IConfiguration _configuration;

    public LiveSupportAIRegistrationTests()
    {
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789",
            ["LiveSupportAI:SystemActorUserId"] = Guid.NewGuid().ToString()
        }).Build();
    }

    private sealed class FakeMediator(Guid? returnedUserId, bool success, string? errorMessage = null) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is RegisterCommand regCmd)
            {
                if (success)
                {
                    var response = new RegisterResponse(returnedUserId ?? Guid.NewGuid(), "Success");
                    return (TResponse)(object)ApiResponse<RegisterResponse>.Ok(response);
                }
                else
                {
                    return (TResponse)(object)ApiResponse<RegisterResponse>.Fail(errorMessage ?? "Registration failed", ["REGISTRATION_FAILED"]);
                }
            }
            return default(TResponse)!;
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

    [Fact]
    public async Task RegisterAndLink_WithStudentParticipant_ThrowsForbidden()
    {
        await using var db = TestAppDbContextFactory.Create();
        var mediator = new FakeMediator(null, true);
        var service = new LiveSupportAIRegistrationService(db, mediator, _configuration);

        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, Guid.NewGuid(), null);
        var dto = new LiveSupportAISecureRegistrationDto(
            Guid.NewGuid(), "idemp-key", "اسم ثلاثي طالب", "01234567890", "password123",
            new DateTime(2005, 5, 5), "Male", "Cairo", "Address", "Secondary", "SecondaryGrade3",
            "School", "01209876543");

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => service.RegisterAndLinkAsync(participant, Guid.NewGuid(), dto, CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.Forbidden, ex.Code);
    }

    [Fact]
    public async Task RegisterAndLink_WhenAlreadyLinked_ThrowsStudentAlreadyLinked()
    {
        await using var db = TestAppDbContextFactory.Create();
        var mediator = new FakeMediator(null, true);
        var service = new LiveSupportAIRegistrationService(db, mediator, _configuration);

        var guestSessionId = Guid.NewGuid();
        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            GuestSessionId = guestSessionId,
            LinkedStudentUserId = Guid.NewGuid(), // Already linked!
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var decision = new LiveSupportAIPendingAction
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.AccountCreation,
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ActionKey = "student.create-and-link",
            SafeProposalJson = "{}",
            PolicyVersionId = Guid.NewGuid(),
            PayloadHash = "hash",
            EncryptedPayload = [1, 2, 3]
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guestSessionId);
        var dto = new LiveSupportAISecureRegistrationDto(
            decision.Id, "idemp-key", "اسم ثلاثي طالب", "01234567890", "password123",
            new DateTime(2005, 5, 5), "Male", "Cairo", "Address", "Secondary", "SecondaryGrade3",
            "School", "01209876543");

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => service.RegisterAndLinkAsync(participant, conversation.Id, dto, CancellationToken.None));
        Assert.Equal("STUDENT_ALREADY_LINKED", ex.Code);
    }

    [Fact]
    public async Task RegisterAndLink_WithRevokedActionInPolicy_ThrowsRegistrationRevoked()
    {
        await using var db = TestAppDbContextFactory.Create();
        var mediator = new FakeMediator(null, true);
        var service = new LiveSupportAIRegistrationService(db, mediator, _configuration);

        var guestSessionId = Guid.NewGuid();
        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            GuestSessionId = guestSessionId,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[]", // Registration not in allowed actions list!
            CreatedByUserId = Guid.NewGuid()
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var decision = new LiveSupportAIPendingAction
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.AccountCreation,
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ActionKey = "student.create-and-link",
            SafeProposalJson = "{}",
            PolicyVersionId = policy.Id,
            PayloadHash = "hash",
            EncryptedPayload = [1, 2, 3]
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guestSessionId);
        var dto = new LiveSupportAISecureRegistrationDto(
            decision.Id, "idemp-key", "اسم ثلاثي طالب", "01234567890", "password123",
            new DateTime(2005, 5, 5), "Male", "Cairo", "Address", "Secondary", "SecondaryGrade3",
            "School", "01209876543");

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => service.RegisterAndLinkAsync(participant, conversation.Id, dto, CancellationToken.None));
        Assert.Equal("REGISTRATION_REVOKED", ex.Code);
    }

    [Fact]
    public async Task RegisterAndLink_Success_CreatesAccountAndLinksConversation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var systemUser = await TestAppDbContextFactory.SeedUserAsync(db, "System Actor", "01200000000");

        var customConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789",
            ["LiveSupportAI:SystemActorUserId"] = systemUser.Id.ToString()
        }).Build();

        var guestSessionId = Guid.NewGuid();
        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            GuestSessionId = guestSessionId,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[\"student.create-and-link\"]",
            CreatedByUserId = systemUser.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var state = new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            PolicyVersionId = policy.Id,
            Mode = LiveSupportAIMode.AiActive,
            Version = 1
        };
        db.LiveSupportAIConversationStates.Add(state);

        var decision = new LiveSupportAIPendingAction
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.AccountCreation,
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ActionKey = "student.create-and-link",
            SafeProposalJson = "{}",
            PolicyVersionId = policy.Id,
            PayloadHash = "hash",
            EncryptedPayload = [1, 2, 3]
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var newUserId = Guid.NewGuid();
        var mediator = new FakeMediator(newUserId, true);
        var service = new LiveSupportAIRegistrationService(db, mediator, customConfig);

        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guestSessionId);
        var dto = new LiveSupportAISecureRegistrationDto(
            decision.Id, "idemp-key", "اسم ثلاثي طالب", "01234567890", "password123",
            new DateTime(2005, 5, 5), "Male", "Cairo", "Address", "Secondary", "SecondaryGrade3",
            "School", "01209876543");

        var resultUserId = await service.RegisterAndLinkAsync(participant, conversation.Id, dto, CancellationToken.None);

        Assert.Equal(newUserId, resultUserId);

        var updatedConversation = await db.LiveSupportConversations.SingleAsync(c => c.Id == conversation.Id);
        Assert.Equal(newUserId, updatedConversation.LinkedStudentUserId);

        var updatedDecision = await db.LiveSupportAIPendingActions.SingleAsync(d => d.Id == decision.Id);
        Assert.Equal(LiveSupportAIPendingActionStatus.Succeeded, updatedDecision.Status);
        Assert.Equal(guestSessionId, updatedDecision.ConfirmedByGuestSessionId);

        var updatedState = await db.LiveSupportAIConversationStates.SingleAsync(s => s.ConversationId == conversation.Id);
        Assert.Equal(newUserId, updatedState.VerifiedStudentUserId);

        var linkHistoryExists = await db.LiveSupportStudentLinkHistories.AnyAsync(h => h.ConversationId == conversation.Id && h.NewStudentUserId == newUserId);
        Assert.True(linkHistoryExists);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Auth.Commands;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using NaderGorge.Integration.Tests.LiveSupport;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NaderGorge.Integration.Tests.LiveSupportAI;

public sealed class LiveSupportAIRegistrationIntegrationTests
{
    private sealed class IntegrationFakeMediator(AppDbContext db) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is RegisterCommand regCmd)
            {
                var handler = new RegisterCommandHandler(db, new AcademicValidationService());
                var res = await handler.Handle(regCmd, cancellationToken);
                return (TResponse)(object)res;
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
    public async Task ConcurrentRegistrations_EnsureExactlyOneAccountCreated_AndRestRollback()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();

        // Seed Student Role
        var studentRole = new Role { Type = RoleType.Student, Name = "Student", AllowedDomain = "all" };
        fixture.Db.Roles.Add(studentRole);

        // Seed System Actor User
        var systemUser = new User { FullName = "System Actor", PhoneNumber = "01200000000", PasswordHash = "hash" };
        fixture.Db.Users.Add(systemUser);

        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 14603,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[\"student.create-and-link\"]",
            CreatedByUserId = systemUser.Id,
            Version = 1
        };
        fixture.Db.LiveSupportAIPolicyVersions.Add(policy);

        var guestSessionId = Guid.NewGuid();
        var guestSession = new LiveSupportGuestSession
        {
            Id = guestSessionId,
            DisplayName = "زائر اختبار",
            PhoneNumber = "01233334444",
            SecurityStampHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            LastSeenAt = DateTime.UtcNow,
            CreatedIpHash = "hash"
        };
        fixture.Db.LiveSupportGuestSessions.Add(guestSession);

        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest,
            GuestSessionId = guestSessionId,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Help",
            Version = 1
        };
        fixture.Db.LiveSupportConversations.Add(conversation);
        await fixture.Db.SaveChangesAsync();

        var message = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Guest,
            ClientMessageId = Guid.NewGuid().ToString(),
            Type = LiveSupportMessageType.Text,
            Content = "Guest message",
            SentAt = DateTime.UtcNow
        };
        fixture.Db.LiveSupportMessages.Add(message);
        await fixture.Db.SaveChangesAsync();

        var turn = new LiveSupportAITurn
        {
            ConversationId = conversation.Id,
            SourceMessageId = message.Id,
            PolicyVersionId = policy.Id,
            Status = LiveSupportAITurnStatus.Completed,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        fixture.Db.LiveSupportAITurns.Add(turn);
        await fixture.Db.SaveChangesAsync();

        var state = new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            PolicyVersionId = policy.Id,
            Mode = LiveSupportAIMode.AiActive,
            Version = 1
        };
        fixture.Db.LiveSupportAIConversationStates.Add(state);

        var decision = new LiveSupportAIPendingAction
        {
            ConversationId = conversation.Id,
            TurnId = turn.Id,
            DecisionKind = LiveSupportAIPendingDecisionKind.AccountCreation,
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ActionKey = "student.create-and-link",
            SafeProposalJson = "{}",
            PolicyVersionId = policy.Id,
            PayloadHash = "hash",
            EncryptedPayload = [1, 2, 3]
        };
        fixture.Db.LiveSupportAIPendingActions.Add(decision);
        await fixture.Db.SaveChangesAsync();

        var customConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789",
            ["LiveSupportAI:SystemActorUserId"] = systemUser.Id.ToString()
        }).Build();

        var numTasks = 4;
        var tasks = new Task[numTasks];
        var successCount = 0;
        var failureCount = 0;
        var serializationFailureCount = 0;
        var exceptionMessages = new ConcurrentBag<string>();

        for (int i = 0; i < numTasks; i++)
        {
            var taskIndex = i;
            tasks[i] = Task.Run(async () =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options;
                await using var db = new AppDbContext(options);

                var mediator = new IntegrationFakeMediator(db);
                var service = new LiveSupportAIRegistrationService(db, mediator, customConfig);

                var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, guestSessionId);
                var dto = new LiveSupportAISecureRegistrationDto(
                    decision.Id,
                    "idemp-key",
                    $"طالب اختبار رباعي {taskIndex}",
                    $"0122222333{taskIndex}",
                    "password123",
                    new DateTime(2005, 5, 5),
                    "Male",
                    "Cairo",
                    "Address",
                    "Secondary",
                    "SecondaryGrade3",
                    "School",
                    "01209876543");

                try
                {
                    await service.RegisterAndLinkAsync(participant, conversation.Id, dto, CancellationToken.None);
                    Interlocked.Increment(ref successCount);
                }
                catch (LiveSupportException ex)
                {
                    exceptionMessages.Add($"LiveSupportException Code: {ex.Code}, Message: {ex.Message}");
                    if (ex.Code == "REGISTRATION_NOT_CONFIRMABLE" || ex.Code == "STUDENT_ALREADY_LINKED" || ex.Code == "CONFLICT")
                    {
                        Interlocked.Increment(ref failureCount);
                    }
                }
                catch (Exception ex)
                {
                    exceptionMessages.Add($"Exception: {ex}");
                    if (ex.ToString().Contains("40001"))
                    {
                        Interlocked.Increment(ref serializationFailureCount);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Write captured exceptions to /tmp/test_output.txt
        File.WriteAllLines("/tmp/test_output.txt", exceptionMessages);

        // Verify exactly one registration succeeded
        Assert.Equal(1, successCount);
        // Verify others failed due to conflict or serializable transaction retry
        Assert.Equal(numTasks - 1, failureCount + serializationFailureCount);

        // Verify conversation is linked to exactly one student
        var optionsVerify = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        await using var verifyDb = new AppDbContext(optionsVerify);
        var finalConversation = await verifyDb.LiveSupportConversations.SingleAsync(c => c.Id == conversation.Id);
        Assert.NotNull(finalConversation.LinkedStudentUserId);

        // Verify only one student link history was added
        var linkHistories = await verifyDb.LiveSupportStudentLinkHistories.Where(h => h.ConversationId == conversation.Id).ToListAsync();
        Assert.Single(linkHistories);
    }
}

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Commands;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using NaderGorge.Integration.Tests.LiveSupport;
using Xunit;

namespace NaderGorge.Integration.Tests.LiveSupportAI;

public sealed class LiveSupportAIActionConcurrencyTests
{
    private sealed class ThreadSafeFakeActionExecutor : ILiveSupportAIActionExecutor
    {
        private int _executeCount;
        public int ExecuteCount => _executeCount;
        public AsyncLocal<IAppDbContext> CurrentDb { get; } = new();

        public Task<Guid> ExecuteAsync(Guid conversationId, Guid studentUserId, Guid pendingDecisionId, string actionKey, IReadOnlyDictionary<string, object?> payload, string idempotencyKey, CancellationToken ct)
        {
            Interlocked.Increment(ref _executeCount);
            var execId = Guid.NewGuid();
            var db = CurrentDb.Value;
            if (db is AppDbContext appDb)
            {
                var execution = new LiveSupportActionExecution
                {
                    Id = execId,
                    ConversationId = conversationId,
                    StudentUserId = studentUserId,
                    StaffUserId = studentUserId,
                    ActionKey = actionKey,
                    IdempotencyKey = idempotencyKey,
                    PayloadHash = "hash",
                    SafeRequestJson = "{}",
                    Status = LiveSupportActionStatus.Succeeded,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
                appDb.LiveSupportActionExecutions.Add(execution);
            }
            return Task.FromResult(execId);
        }
    }

    [Fact]
    public async Task ConcurrentConfirmations_EnsureExactlyOneExecution()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();

        // Seed data
        var admin = new User { FullName = "Admin", PhoneNumber = "01000000000", PasswordHash = "hash" };
        var student = new User { FullName = "Student", PhoneNumber = "01000000001", PasswordHash = "hash" };
        fixture.Db.Users.AddRange(admin, student);

        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 14602,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[\"system.some_action\"]",
            CreatedByUserId = admin.Id,
            Version = 1
        };
        fixture.Db.LiveSupportAIPolicyVersions.Add(policy);

        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Student,
            StudentUserId = student.Id,
            LinkedStudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        fixture.Db.LiveSupportConversations.Add(conversation);
        await fixture.Db.SaveChangesAsync();

        var message = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Student,
            SenderUserId = student.Id,
            ClientMessageId = Guid.NewGuid().ToString(),
            Type = LiveSupportMessageType.Text,
            Content = "User message",
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

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);

        var payloadBytes = Encoding.UTF8.GetBytes("{\"arguments\": {}}");
        var encrypted = protector.Protect(payloadBytes);
        var payloadHash = protector.ComputeKeyedDigest("pending-decision", payloadBytes);

        var decision = new LiveSupportAIPendingAction
        {
            ConversationId = conversation.Id,
            TurnId = turn.Id,
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
            EncryptedPayload = encrypted,
            PayloadHash = payloadHash,
            StateFingerprint = "fingerprint"
        };
        fixture.Db.LiveSupportAIPendingActions.Add(decision);
        await fixture.Db.SaveChangesAsync();

        var executor = new ThreadSafeFakeActionExecutor();
        var numTasks = 8;
        var tasks = new Task[numTasks];
        var successCount = 0;
        var conflictCount = 0;
        var serializationFailureCount = 0;

        for (int i = 0; i < numTasks; i++)
        {
            var taskIndex = i;
            tasks[i] = Task.Run(async () =>
            {
                await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
                executor.CurrentDb.Value = db;
                var handler = new ConfirmLiveSupportAIActionCommandHandler(db, protector, executor);
                var command = new ConfirmLiveSupportAIActionCommand(
                    new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
                    conversation.Id,
                    decision.Id,
                    $"nonce-{taskIndex}"
                );

                try
                {
                    await handler.Handle(command, CancellationToken.None);
                    Interlocked.Increment(ref successCount);
                }
                catch (Exception ex)
                {
                    if (ex is LiveSupportException lex && (lex.Code == "IDEMPOTENCY_PAYLOAD_CONFLICT" || lex.Code == "DECISION_NOT_CONFIRMABLE"))
                    {
                        Interlocked.Increment(ref conflictCount);
                    }
                    else if (ex.ToString().Contains("40001"))
                    {
                        Interlocked.Increment(ref serializationFailureCount);
                    }
                    else
                    {
                        throw;
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Verify only 1 execution was invoked on the executor
        Assert.Equal(1, executor.ExecuteCount);
        // Verify only 1 task succeeded
        Assert.Equal(1, successCount);
        // Verify all other tasks failed with conflict or serialization failure
        Assert.Equal(numTasks - 1, conflictCount + serializationFailureCount);
    }
}

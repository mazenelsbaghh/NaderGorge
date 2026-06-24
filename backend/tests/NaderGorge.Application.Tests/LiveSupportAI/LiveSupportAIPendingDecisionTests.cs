using System.Text;
using System.Text.Json;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Commands;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Validation;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services.LiveSupportAI;
using Xunit;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIPendingDecisionTests
{
    private sealed class FakeActionExecutor : ILiveSupportAIActionExecutor
    {
        public Guid ExecutedId { get; private set; } = Guid.Empty;
        public string? ExecutedActionKey { get; private set; }
        public IReadOnlyDictionary<string, object?>? ExecutedArguments { get; private set; }

        public Task<Guid> ExecuteAsync(Guid conversationId, Guid studentUserId, Guid pendingDecisionId, string actionKey, IReadOnlyDictionary<string, object?> payload, string idempotencyKey, CancellationToken ct)
        {
            ExecutedId = Guid.NewGuid();
            ExecutedActionKey = actionKey;
            ExecutedArguments = payload;
            return Task.FromResult(ExecutedId);
        }
    }

    [Fact]
    public void Protected_payload_is_not_plaintext_and_keyed_digests_are_purpose_bound()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);
        var plaintext = Encoding.UTF8.GetBytes("{\"password\":\"never-store-me\"}");

        var protectedPayload = protector.Protect(plaintext);

        Assert.DoesNotContain("never-store-me", Encoding.UTF8.GetString(protectedPayload), StringComparison.Ordinal);
        Assert.Equal(plaintext, protector.Unprotect(protectedPayload));
        Assert.Equal(protector.ComputeKeyedDigest("action", plaintext), protector.ComputeKeyedDigest("action", plaintext));
        Assert.NotEqual(protector.ComputeKeyedDigest("action", plaintext), protector.ComputeKeyedDigest("verification", plaintext));
    }

    [Fact]
    public void Registration_validator_requires_complete_authoritative_profile_and_distinct_valid_phones()
    {
        var validator = new LiveSupportAISecureRegistrationValidator();
        var invalid = new LiveSupportAISecureRegistrationDto(
            Guid.NewGuid(), "short", "اسم ثنائي", "123", "weak", DateTime.UtcNow.AddDays(1), "Unknown",
            "", "", "Secondary", "ThirdSecondary", "", "123");

        var result = validator.TestValidate(invalid);

        result.ShouldHaveValidationErrorFor(item => item.FullName);
        result.ShouldHaveValidationErrorFor(item => item.PhoneNumber);
        result.ShouldHaveValidationErrorFor(item => item.Password);
        result.ShouldHaveValidationErrorFor(item => item.DateOfBirth);
        result.ShouldHaveValidationErrorFor(item => item.Gender);
        result.ShouldHaveValidationErrorFor(item => item.Address);
        result.ShouldHaveValidationErrorFor(item => item.ParentPhoneNumber);
    }

    [Fact]
    public async Task ConfirmAction_RevokedPolicyOrAction_ThrowsActionRevoked()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01200000000");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01200000001");
        
        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = false, // policy is disabled!
            SystemInstructions = "Instructions",
            ActionKeysJson = "[]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student.Id,
            LinkedStudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var decision = new LiveSupportAIPendingAction
        {
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}",
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
            ConfirmationNonceHash = "",
            PayloadHash = "valid-hash",
            EncryptedPayload = [1, 2, 3]
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);
        var executor = new FakeActionExecutor();
        var handler = new ConfirmLiveSupportAIActionCommandHandler(db, protector, executor);

        var command = new ConfirmLiveSupportAIActionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("ACTION_REVOKED", ex.Code);
    }

    [Fact]
    public async Task ConfirmAction_TargetMismatch_ThrowsActionTargetMismatch()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01200000000");
        var student1 = await TestAppDbContextFactory.SeedUserAsync(db, "Student 1", "01200000001");
        var student2 = await TestAppDbContextFactory.SeedUserAsync(db, "Student 2", "01200000002");

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[\"system.some_action\"]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student1.Id,
            LinkedStudentUserId = student2.Id, // Mismatch with decision target student1
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var decision = new LiveSupportAIPendingAction
        {
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}",
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student1.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);
        var executor = new FakeActionExecutor();
        var handler = new ConfirmLiveSupportAIActionCommandHandler(db, protector, executor);

        var command = new ConfirmLiveSupportAIActionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student1.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("ACTION_TARGET_MISMATCH", ex.Code);
    }

    [Fact]
    public async Task ConfirmAction_PayloadHashInvalid_ThrowsActionPayloadInvalid()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01200000000");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01200000001");

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[\"system.some_action\"]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student.Id,
            LinkedStudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);

        var payloadBytes = Encoding.UTF8.GetBytes("{\"arguments\": {}}");
        var encrypted = protector.Protect(payloadBytes);

        var decision = new LiveSupportAIPendingAction
        {
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}",
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
            EncryptedPayload = encrypted,
            PayloadHash = "invalid-payload-hash" // Mismatched payload hash!
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var executor = new FakeActionExecutor();
        var handler = new ConfirmLiveSupportAIActionCommandHandler(db, protector, executor);

        var command = new ConfirmLiveSupportAIActionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("ACTION_PAYLOAD_INVALID", ex.Code);
    }

    [Fact]
    public async Task ConfirmAction_Expired_SetsStatusExpiredAndThrows()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01200000000");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01200000001");

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[\"system.some_action\"]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student.Id,
            LinkedStudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var decision = new LiveSupportAIPendingAction
        {
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}",
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired!
            IdempotencyKey = Guid.NewGuid(),
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);
        var executor = new FakeActionExecutor();
        var handler = new ConfirmLiveSupportAIActionCommandHandler(db, protector, executor);

        var command = new ConfirmLiveSupportAIActionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("CONFIRMATION_EXPIRED", ex.Code);
        
        var updated = await db.LiveSupportAIPendingActions.FindAsync(decision.Id);
        Assert.Equal(LiveSupportAIPendingActionStatus.Expired, updated!.Status);
    }

    [Fact]
    public async Task CancelAction_Cancelled_SavesStatusAndPreventsDoubleCancel()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01200000001");

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var decision = new LiveSupportAIPendingAction
        {
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}",
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = Guid.NewGuid(),
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var handler = new CancelLiveSupportAIDecisionCommandHandler(db);
        var command = new CancelLiveSupportAIDecisionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        // Cancel
        await handler.Handle(command, CancellationToken.None);
        var updated = await db.LiveSupportAIPendingActions.FindAsync(decision.Id);
        Assert.Equal(LiveSupportAIPendingActionStatus.Cancelled, updated!.Status);
        Assert.NotNull(updated.CancelledAt);

        // Double cancel should be idempotent
        await handler.Handle(command, CancellationToken.None);
    }

    [Fact]
    public async Task CancelAction_NonCancellable_ThrowsDecisionNotCancellable()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01200000001");

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var decision = new LiveSupportAIPendingAction
        {
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}",
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = Guid.NewGuid(),
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.Succeeded, // Already Succeeded!
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var handler = new CancelLiveSupportAIDecisionCommandHandler(db);
        var command = new CancelLiveSupportAIDecisionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("DECISION_NOT_CANCELLABLE", ex.Code);
    }

    [Fact]
    public async Task ConfirmAction_DuplicateAndConflictIdempotencyKey_BehavesCorrectly()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01200000000");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01200000001");

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[\"system.some_action\"]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student.Id,
            LinkedStudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

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
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}",
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
            EncryptedPayload = encrypted,
            PayloadHash = payloadHash
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var executor = new FakeActionExecutor();
        var handler = new ConfirmLiveSupportAIActionCommandHandler(db, protector, executor);

        var command = new ConfirmLiveSupportAIActionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        // First execution
        var execId1 = await handler.Handle(command, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, execId1);

        // Same idempotency key should return same execution ID (idempotent replay)
        var execId2 = await handler.Handle(command, CancellationToken.None);
        Assert.Equal(execId1, execId2);

        // Different idempotency key should throw conflict
        var commandConflict = new ConfirmLiveSupportAIActionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-different"
        );

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => handler.Handle(commandConflict, CancellationToken.None));
        Assert.Equal("IDEMPOTENCY_PAYLOAD_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task ConfirmAction_StateFingerprintMismatch_InvalidatesDecisionAndThrowsActionStateChanged()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01200000000");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01200000001");

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[\"system.some_action\"]",
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student.Id,
            LinkedStudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 2 // Current version is 2
        };
        db.LiveSupportConversations.Add(conversation);

        var decision = new LiveSupportAIPendingAction
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
            StateFingerprint = $"{conversation.Id:N}:1" // State fingerprint expects version 1!
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);
        var executor = new FakeActionExecutor();
        var handler = new ConfirmLiveSupportAIActionCommandHandler(db, protector, executor);

        var command = new ConfirmLiveSupportAIActionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("ACTION_STATE_CHANGED", ex.Code);

        var updated = await db.LiveSupportAIPendingActions.FindAsync(decision.Id);
        Assert.Equal(LiveSupportAIPendingActionStatus.Invalidated, updated!.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task ConfirmAction_ActionNotAllowed_ThrowsActionRevoked()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "01200000000");
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01200000001");

        var policy = new LiveSupportAIPolicyVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "Instructions",
            ActionKeysJson = "[]", // Action is not allowed!
            CreatedByUserId = admin.Id
        };
        db.LiveSupportAIPolicyVersions.Add(policy);

        var conversation = new LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            StudentUserId = student.Id,
            LinkedStudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Active,
            Subject = "Subject",
            Version = 1
        };
        db.LiveSupportConversations.Add(conversation);

        var decision = new LiveSupportAIPendingAction
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TurnId = Guid.NewGuid(),
            DecisionKind = LiveSupportAIPendingDecisionKind.Action,
            StudentUserId = student.Id,
            PolicyVersionId = policy.Id,
            ActionKey = "system.some_action",
            SafeProposalJson = "{}",
            Status = LiveSupportAIPendingActionStatus.PendingConfirmation,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IdempotencyKey = Guid.NewGuid(),
            StateFingerprint = $"{conversation.Id:N}:{conversation.Version}"
        };
        db.LiveSupportAIPendingActions.Add(decision);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);
        var executor = new FakeActionExecutor();
        var handler = new ConfirmLiveSupportAIActionCommandHandler(db, protector, executor);

        var command = new ConfirmLiveSupportAIActionCommand(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
            conversation.Id,
            decision.Id,
            "nonce-123"
        );

        var ex = await Assert.ThrowsAsync<LiveSupportException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("ACTION_REVOKED", ex.Code);
    }
}

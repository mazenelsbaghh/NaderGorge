using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Infrastructure.Services.LiveSupportAI;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class FacebookMessengerConfigurationAndWebhookTests
{
    [Fact]
    public void ThreeConfiguredPages_AreResolvedWithTheirOwnTokens()
    {
        var configuration = MessengerConfiguration(
            ("page-1", "صفحة أولى", "token-1", false),
            ("page-2", "صفحة ثانية", "token-2", true),
            ("page-3", "صفحة ثالثة", "token-3", false));

        Assert.Equal(3, configuration.Pages.Count);
        Assert.Equal("token-1", configuration.RequirePage("page-1").AccessToken);
        Assert.Equal("token-2", configuration.RequirePage("page-2").AccessToken);
        Assert.Equal("token-3", configuration.RequirePage("page-3").AccessToken);
        Assert.True(configuration.RequirePage("page-2").HumanAgentEnabled);
    }

    [Fact]
    public void PartiallyConfiguredPage_FailsClosed()
    {
        var rawConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FacebookMessenger:Pages:0:PageId"] = "page-1",
                ["FacebookMessenger:Pages:0:DisplayName"] = "صفحة أولى"
            })
            .Build();

        var exception = Assert.Throws<FacebookMessengerConfigurationException>(
            () => new FacebookMessengerConfiguration(rawConfiguration));

        Assert.Equal("MESSENGER_ACCESSTOKEN_REQUIRED", exception.ErrorCode);
    }

    [Fact]
    public void EmptyProvisioningSlots_AreIgnored()
    {
        var rawConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FacebookMessenger:VerifyToken"] = "verify-token",
                ["FacebookMessenger:AppSecret"] = "app-secret",
                ["FacebookMessenger:Pages:0:PageId"] = "page-1",
                ["FacebookMessenger:Pages:0:DisplayName"] = "صفحة أولى",
                ["FacebookMessenger:Pages:0:AccessToken"] = "token-1",
                ["FacebookMessenger:Pages:1:HumanAgentEnabled"] = "false",
                ["FacebookMessenger:Pages:2:HumanAgentEnabled"] = "false"
            })
            .Build();

        var configuration = new FacebookMessengerConfiguration(rawConfiguration);

        var configuredPage = Assert.Single(configuration.Pages);
        Assert.Equal("page-1", configuredPage.PageId);
    }

    [Theory]
    [InlineData("FacebookMessenger:VerifyToken", "MESSENGER_VERIFY_TOKEN_REQUIRED")]
    [InlineData("FacebookMessenger:AppSecret", "MESSENGER_APP_SECRET_REQUIRED")]
    public void EnabledIntegration_RequiresWebhookCredentials(
        string missingSetting,
        string expectedErrorCode)
    {
        var values = CompletePageSettings();
        values.Remove(missingSetting);

        var exception = Assert.Throws<FacebookMessengerConfigurationException>(() =>
            new FacebookMessengerConfiguration(
                new ConfigurationBuilder().AddInMemoryCollection(values).Build()));

        Assert.Equal(expectedErrorCode, exception.ErrorCode);
    }

    [Fact]
    public void MoreThanThreePages_IsRejected()
    {
        var exception = Assert.Throws<FacebookMessengerConfigurationException>(() =>
            MessengerConfiguration(
                ("page-1", "Page 1", "token-1", false),
                ("page-2", "Page 2", "token-2", false),
                ("page-3", "Page 3", "token-3", false),
                ("page-4", "Page 4", "token-4", false)));

        Assert.Equal("MESSENGER_PAGE_LIMIT_EXCEEDED", exception.ErrorCode);
    }

    [Fact]
    public void InvalidGraphApiVersion_IsRejected()
    {
        var values = CompletePageSettings();
        values["FacebookMessenger:ApiVersion"] = "latest";

        var exception = Assert.Throws<FacebookMessengerConfigurationException>(() =>
            new FacebookMessengerConfiguration(
                new ConfigurationBuilder().AddInMemoryCollection(values).Build()));

        Assert.Equal("MESSENGER_API_VERSION_INVALID", exception.ErrorCode);
    }

    [Fact]
    public void MixedPageWebhook_IsolatesIncomingMessagesAndEchoDirection()
    {
        var parser = new FacebookMessengerWebhookParser(MessengerConfiguration(
            ("page-1", "صفحة أولى", "token-1", false),
            ("page-2", "صفحة ثانية", "token-2", false),
            ("page-3", "صفحة ثالثة", "token-3", false)));
        using var webhook = JsonDocument.Parse("""
            {
              "object": "page",
              "entry": [
                {
                  "id": "page-1",
                  "messaging": [
                    {
                      "sender": { "id": "shared-psid" },
                      "recipient": { "id": "page-1" },
                      "timestamp": 1788000000000,
                      "message": { "mid": "mid.inbound", "text": "مرحبا" }
                    },
                    {
                      "sender": { "id": "shared-psid" },
                      "recipient": { "id": "page-2" },
                      "timestamp": 1788000000001,
                      "message": { "mid": "mid.wrong-page", "text": "يجب تجاهلها" }
                    }
                  ]
                },
                {
                  "id": "page-2",
                  "messaging": [
                    {
                      "sender": { "id": "page-2" },
                      "recipient": { "id": "shared-psid" },
                      "timestamp": 1788000000002,
                      "message": { "mid": "mid.echo", "is_echo": true, "text": "رد الموظف" }
                    }
                  ]
                },
                {
                  "id": "foreign-page",
                  "messaging": [
                    {
                      "sender": { "id": "shared-psid" },
                      "recipient": { "id": "foreign-page" },
                      "timestamp": 1788000000003,
                      "message": { "mid": "mid.foreign", "text": "خارج الإعداد" }
                    }
                  ]
                }
              ]
            }
            """);

        var events = parser.Parse(webhook.RootElement);

        Assert.Collection(
            events,
            incoming =>
            {
                Assert.Equal("page-1", incoming.PageId);
                Assert.Equal("message", incoming.EventKind);
                Assert.Equal("message:mid.inbound", incoming.DeduplicationKey);
            },
            echo =>
            {
                Assert.Equal("page-2", echo.PageId);
                Assert.Equal("message_echo", echo.EventKind);
                Assert.Equal("message_echo:mid.echo", echo.DeduplicationKey);
            });
    }

    [Fact]
    public async Task HumanOnlyConversation_RemainsOutsideAiDespitePublishedPolicy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var guestId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        Guid conversationId;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new User
            {
                Id = adminId,
                FullName = "مدير الاختبار",
                PhoneNumber = "01000000001",
                PasswordHash = "test-only"
            });
            db.LiveSupportGuestSessions.Add(new LiveSupportGuestSession
            {
                Id = guestId,
                DisplayName = "عميل ماسنجر",
                SecurityStampHash = new string('A', 64),
                CreatedIpHash = new string('B', 64),
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                LastSeenAt = DateTime.UtcNow
            });
            db.LiveSupportAIPolicyVersions.Add(new LiveSupportAIPolicyVersion
            {
                VersionNumber = 1,
                Status = LiveSupportAIPolicyStatus.Published,
                IsEnabled = true,
                SystemInstructions = "تعليمات اختبار",
                CreatedByUserId = adminId,
                PublishedByUserId = adminId,
                PublishedAt = DateTime.UtcNow,
                Version = 1
            });
            await db.SaveChangesAsync();
            var service = new LiveSupportService(db, new EnabledSettings());

            var created = await service.CreateHumanOnlyAsync(
                new LiveSupportParticipantIdentity(
                    LiveSupportParticipantType.Guest,
                    StudentUserId: null,
                    GuestSessionId: guestId),
                "رسالة ماسنجر",
                previousConversationId: null,
                CancellationToken.None);

            conversationId = created.Id;
            Assert.False(created.IsAiActive);
        }

        await using var verificationDb = new AppDbContext(options);
        var persistedConversation = await verificationDb.LiveSupportConversations
            .SingleAsync(conversation => conversation.Id == conversationId);
        Assert.False(persistedConversation.AllowsAI);
        Assert.True(await verificationDb.LiveSupportQueueEntries.AnyAsync(
            entry => entry.ConversationId == conversationId));
        Assert.False(await verificationDb.LiveSupportAIConversationStates.AnyAsync(
            state => state.ConversationId == conversationId));
        Assert.False(await verificationDb.LiveSupportAITurns.AnyAsync(
            turn => turn.ConversationId == conversationId));
    }

    [Fact]
    public async Task CorruptAiArtifacts_OnHumanOnlyConversation_AreDiscardedBeforeContextOrReply()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var admin = new User
        {
            FullName = "مدير اختبار الحارس",
            PhoneNumber = "01000000002",
            PasswordHash = "test-only"
        };
        var guest = new LiveSupportGuestSession
        {
            DisplayName = "عميل ماسنجر",
            SecurityStampHash = new string('C', 64),
            CreatedIpHash = new string('D', 64),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            LastSeenAt = DateTime.UtcNow
        };
        var policy = new LiveSupportAIPolicyVersion
        {
            VersionNumber = 2,
            Status = LiveSupportAIPolicyStatus.Published,
            IsEnabled = true,
            SystemInstructions = "تعليمات يجب ألا تُستخدم",
            CreatedByUserId = admin.Id,
            PublishedByUserId = admin.Id,
            PublishedAt = DateTime.UtcNow,
            Version = 1
        };
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Guest,
            GuestSessionId = guest.Id,
            Status = LiveSupportConversationStatus.Waiting,
            AllowsAI = false,
            Version = 1
        };
        db.AddRange(admin, guest, policy, conversation);
        db.LiveSupportAIConversationStates.Add(new LiveSupportAIConversationState
        {
            ConversationId = conversation.Id,
            PolicyVersionId = policy.Id,
            Mode = LiveSupportAIMode.AiActive,
            LastParticipantActivityAt = DateTime.UtcNow,
            Version = 1
        });
        var sourceMessage = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Guest,
            SenderGuestSessionId = guest.Id,
            ClientMessageId = "messenger-corrupt-source",
            Type = LiveSupportMessageType.Text,
            Content = "رسالة يجب أن يرد عليها موظف فقط",
            SentAt = DateTime.UtcNow
        };
        var completionSourceMessage = new LiveSupportMessage
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Guest,
            SenderGuestSessionId = guest.Id,
            ClientMessageId = "messenger-corrupt-completion-source",
            Type = LiveSupportMessageType.Text,
            Content = "رسالة ثانية يجب أن يرد عليها موظف فقط",
            SentAt = DateTime.UtcNow
        };
        db.LiveSupportMessages.AddRange(sourceMessage, completionSourceMessage);
        var queuedTurn = new LiveSupportAITurn
        {
            ConversationId = conversation.Id,
            SourceMessageId = sourceMessage.Id,
            PolicyVersionId = policy.Id,
            ExpectedConversationVersion = conversation.Version,
            Status = LiveSupportAITurnStatus.Queued,
            CallbackStatus = LiveSupportAICallbackStatus.NotReady,
            QueuedAt = DateTime.UtcNow,
            Version = 1
        };
        var processingTurn = new LiveSupportAITurn
        {
            ConversationId = conversation.Id,
            SourceMessageId = completionSourceMessage.Id,
            PolicyVersionId = policy.Id,
            ExpectedConversationVersion = conversation.Version,
            Status = LiveSupportAITurnStatus.Processing,
            CallbackStatus = LiveSupportAICallbackStatus.NotReady,
            QueuedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            Version = 1
        };
        db.LiveSupportAITurns.AddRange(queuedTurn, processingTurn);
        await db.SaveChangesAsync();

        var contextBuilder = new RecordingContextBuilder();
        var orchestrator = new LiveSupportAITurnOrchestrator(db, contextBuilder);

        var beforeQueueCount = await db.LiveSupportAITurns.CountAsync();
        await orchestrator.QueueForParticipantMessageAsync(
            conversation.Id,
            sourceMessage.Id,
            CancellationToken.None);
        await db.SaveChangesAsync();
        Assert.Equal(beforeQueueCount, await db.LiveSupportAITurns.CountAsync());

        Assert.Null(await orchestrator.ClaimAsync(queuedTurn.Id, CancellationToken.None));
        Assert.Equal(0, contextBuilder.BuildCalls);

        var decision = new LiveSupportAIWorkerDecisionDto(
            "1", "reply", "رد AI ممنوع", null, null, null, null, null);
        var completion = new LiveSupportAIWorkerCompletionDto(
            SchemaVersion: "1",
            ExpectedConversationVersion: conversation.Version,
            ExpectedPolicyVersionId: policy.Id,
            Decision: decision,
            DecisionHash: LiveSupportAITurnOrchestrator.ComputeDecisionHash(decision),
            CallbackIdempotencyKey: processingTurn.Id.ToString("N"),
            Provider: "test",
            Model: "test",
            ProviderResponseId: null,
            InputTokenCount: null,
            OutputTokenCount: null,
            LatencyMs: 1);
        Assert.Equal(
            "DISCARDED_AI_NOT_ACTIVE",
            await orchestrator.CompleteAsync(processingTurn.Id, completion, CancellationToken.None));

        db.ChangeTracker.Clear();
        Assert.Equal(
            LiveSupportAITurnStatus.DiscardedAfterDisable,
            (await db.LiveSupportAITurns.SingleAsync(turn => turn.Id == queuedTurn.Id)).Status);
        Assert.Equal(
            LiveSupportAITurnStatus.DiscardedAfterDisable,
            (await db.LiveSupportAITurns.SingleAsync(turn => turn.Id == processingTurn.Id)).Status);
        Assert.False(await db.LiveSupportMessages.AnyAsync(
            message => message.ConversationId == conversation.Id &&
                       message.SenderType == LiveSupportSenderType.AI));
        Assert.False(await db.OutboxEvents.AnyAsync(
            item => item.Type == "LiveSupportAITurnQueued"));
    }

    private static FacebookMessengerConfiguration MessengerConfiguration(
        params (string PageId, string DisplayName, string AccessToken, bool HumanAgentEnabled)[] pages)
    {
        var values = new Dictionary<string, string?>
        {
            ["FacebookMessenger:VerifyToken"] = "verify-token",
            ["FacebookMessenger:AppSecret"] = "app-secret",
            ["FacebookMessenger:ApiVersion"] = "v25.0"
        };
        for (var index = 0; index < pages.Length; index++)
        {
            var page = pages[index];
            values[$"FacebookMessenger:Pages:{index}:PageId"] = page.PageId;
            values[$"FacebookMessenger:Pages:{index}:DisplayName"] = page.DisplayName;
            values[$"FacebookMessenger:Pages:{index}:AccessToken"] = page.AccessToken;
            values[$"FacebookMessenger:Pages:{index}:HumanAgentEnabled"] = page.HumanAgentEnabled.ToString();
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new FacebookMessengerConfiguration(configuration);
    }

    private static Dictionary<string, string?> CompletePageSettings() => new()
    {
        ["FacebookMessenger:VerifyToken"] = "verify-token",
        ["FacebookMessenger:AppSecret"] = "app-secret",
        ["FacebookMessenger:ApiVersion"] = "v25.0",
        ["FacebookMessenger:Pages:0:PageId"] = "page-1",
        ["FacebookMessenger:Pages:0:DisplayName"] = "صفحة أولى",
        ["FacebookMessenger:Pages:0:AccessToken"] = "token-1"
    };

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });

        public void Invalidate() { }
    }

    private sealed class RecordingContextBuilder : ILiveSupportAIContextBuilder
    {
        public int BuildCalls { get; private set; }

        public Task<LiveSupportAIWorkerClaimDto> BuildAsync(
            Guid turnId,
            CancellationToken cancellationToken)
        {
            BuildCalls++;
            throw new InvalidOperationException("Human-only conversations must not build AI context.");
        }
    }
}

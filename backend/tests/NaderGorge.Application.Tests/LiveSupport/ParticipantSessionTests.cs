using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Application.Interfaces;

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

    private static LiveSupportService CreateService(AppDbContext db) => new(db, new EnabledSettingsReader());

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
        var service = new LiveSupportService(db, new EnabledSettingsReader(), jobEnqueuer: fakeEnqueuer);
        
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null);
        var conversation = await service.CreateConversationAsync(participant, null, null, CancellationToken.None);

        var sendResult = await service.SendParticipantMessageAsync(participant, conversation.Id, Guid.NewGuid().ToString(), "أهلاً بك", LiveSupportMessageType.Text, CancellationToken.None);

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
        var service = new LiveSupportService(db, new EnabledSettingsReader(), jobEnqueuer: fakeEnqueuer);
        
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null);
        var conversation = await service.CreateConversationAsync(participant, null, null, CancellationToken.None);

        await service.SendParticipantMessageAsync(participant, conversation.Id, Guid.NewGuid().ToString(), "أهلاً بك", LiveSupportMessageType.Text, CancellationToken.None);

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
            Decision: new LiveSupportAIDecision("reply", "رد المساعد", null),
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
}

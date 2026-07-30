using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class LiveSupportQueryBudgetTests
{
    private static readonly int MaximumDatabaseCommands = ReadMaximumDatabaseCommands();

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public async Task RepresentativeRows_KeepDashboardHistoryAndTimelineWithinFixedBudgets(
        int rowCount)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seeded = await SeedAsync(fixture.Db, rowCount);
        var counter = new CommandCounterInterceptor();
        await using var db = CreateMeasuredContext(fixture.ConnectionString, counter);
        var service = new LiveSupportService(db, new EnabledSettings());

        counter.Reset();
        var dashboard = await service.GetAdminDashboardAsync(CancellationToken.None);
        Assert.InRange(counter.CommandCount, 1, MaximumDatabaseCommands);
        Assert.Equal(rowCount, dashboard.Conversations.Count);

        counter.Reset();
        var history = await service.GetStudentSupportHistoryAsync(
            seeded.StaffId,
            true,
            seeded.ConversationId,
            CancellationToken.None);
        Assert.InRange(counter.CommandCount, 1, MaximumDatabaseCommands);
        Assert.Equal(rowCount, history.Count);

        counter.Reset();
        var timeline = await service.GetAdminTimelineAsync(
            seeded.ConversationId,
            CancellationToken.None);
        Assert.InRange(counter.CommandCount, 1, MaximumDatabaseCommands);
        Assert.True(timeline.Items.Count >= rowCount);
    }

    private static int ReadMaximumDatabaseCommands()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var path = Path.Combine(directory.FullName, "frontend", "performance-budgets.json");
                if (File.Exists(path))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(path));
                    var maximum = document.RootElement
                        .GetProperty("workflows")
                        .GetProperty("live-support-admin")
                        .GetProperty("maximumDatabaseCommands")
                        .GetInt32();
                    Assert.InRange(maximum, 1, 100);
                    return maximum;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException(
            "frontend/performance-budgets.json is required for query-budget verification.");
    }

    private static AppDbContext CreateMeasuredContext(
        string connectionString,
        DbCommandInterceptor interceptor) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .AddInterceptors(interceptor)
                .Options);

    private static async Task<SeededSupportData> SeedAsync(
        AppDbContext db,
        int rowCount)
    {
        var staff = NewUser($"budget-staff-{Guid.NewGuid():N}");
        var student = NewUser($"budget-student-{Guid.NewGuid():N}");
        db.Users.AddRange(staff, student);
        db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig
        {
            UserId = staff.Id,
            IsEnabled = true,
            MaxActiveConversations = rowCount,
            ConfiguredByUserId = staff.Id,
            Version = 1
        });

        var conversations = Enumerable.Range(0, rowCount)
            .Select(index => NewConversation(student.Id, staff.Id, index))
            .ToArray();
        db.LiveSupportConversations.AddRange(conversations);
        for (var index = 0; index < conversations.Length; index++)
        {
            AddConversationRows(db, conversations[index], student.Id, staff.Id, index);
        }
        AddTimelineRows(db, conversations[0], student.Id, rowCount);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new SeededSupportData(staff.Id, conversations[0].Id);
    }

    private static void AddConversationRows(
        AppDbContext db,
        LiveSupportConversation conversation,
        Guid studentId,
        Guid staffId,
        int index)
    {
        db.LiveSupportAssignments.Add(new LiveSupportAssignment
        {
            ConversationId = conversation.Id,
            StaffUserId = staffId,
            StartedAt = conversation.AssignedAt!.Value,
            AssignmentSequence = 1
        });
        db.LiveSupportMessages.Add(NewMessage(conversation, studentId, index));
        db.LiveSupportEvents.Add(NewEvent(conversation, studentId, index));
    }

    private static void AddTimelineRows(
        AppDbContext db,
        LiveSupportConversation conversation,
        Guid studentId,
        int rowCount)
    {
        for (var index = 1; index < rowCount; index++)
        {
            db.LiveSupportMessages.Add(NewMessage(conversation, studentId, 10_000 + index));
            db.LiveSupportEvents.Add(NewEvent(conversation, studentId, 10_000 + index));
        }
    }

    private static LiveSupportConversation NewConversation(
        Guid studentId,
        Guid staffId,
        int index)
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-index - 1);
        return new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Student,
            StudentUserId = studentId,
            LinkedStudentUserId = studentId,
            CurrentOwnerUserId = staffId,
            Status = LiveSupportConversationStatus.Active,
            AssignedAt = createdAt.AddSeconds(1),
            LastMessageAt = createdAt.AddSeconds(2),
            Subject = $"budget-{index}",
            Version = 1,
            CreatedAt = createdAt
        };
    }

    private static LiveSupportMessage NewMessage(
        LiveSupportConversation conversation,
        Guid studentId,
        int index) =>
        new()
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Student,
            SenderUserId = studentId,
            ClientMessageId = $"budget-message-{index}",
            Type = LiveSupportMessageType.Text,
            Content = "representative",
            SentAt = conversation.CreatedAt.AddSeconds(index + 2)
        };

    private static LiveSupportEvent NewEvent(
        LiveSupportConversation conversation,
        Guid studentId,
        int index) =>
        new()
        {
            ConversationId = conversation.Id,
            Type = LiveSupportEventType.MessageSent,
            ActorUserId = studentId,
            OccurredAt = conversation.CreatedAt.AddSeconds(index + 2),
            Sequence = index + 1
        };

    private static User NewUser(string prefix) =>
        new()
        {
            FullName = prefix,
            PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}",
            PasswordHash = "integration"
        };

    private sealed record SeededSupportData(Guid StaffId, Guid ConversationId);

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(
                CachedPlatformSettings.Default with { LiveSupportEnabled = true });

        public void Invalidate()
        {
        }
    }

    private sealed class CommandCounterInterceptor : DbCommandInterceptor
    {
        private int _commandCount;
        public int CommandCount => Volatile.Read(ref _commandCount);
        public void Reset() => Interlocked.Exchange(ref _commandCount, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commandCount);
            return ValueTask.FromResult(result);
        }
    }
}

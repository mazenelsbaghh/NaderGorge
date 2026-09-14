using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Integration.Tests.LiveSupport;

namespace NaderGorge.Integration.Tests.Performance;

/// <summary>
/// Captures the pre-optimization database-command and latency shape without
/// turning the current N+1 behavior into an accepted budget. Set
/// PLATFORM_PERFORMANCE_BASELINE_OUTPUT to persist the evidence JSON.
/// </summary>
public sealed class PlatformPerformanceBaselineTests
{
    [Fact]
    public async Task LiveSupportAdminDashboard_RecordsRepresentativeBaseline()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        await SeedDashboardAsync(fixture.Db, conversationCount: 12);

        var counter = new CommandCounterInterceptor();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .AddInterceptors(counter)
                .Options);
        var service = new LiveSupportService(db, new EnabledSettings());

        var samples = new List<BaselineSample>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            counter.Reset();
            var started = Stopwatch.GetTimestamp();
            var dashboard = await service.GetAdminDashboardAsync(CancellationToken.None);
            samples.Add(new BaselineSample(
                attempt + 1,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                counter.CommandCount,
                dashboard.Conversations.Count,
                dashboard.StaffPerformance.Count));
        }

        Assert.All(samples, sample =>
        {
            Assert.Equal(12, sample.ConversationCount);
            Assert.Equal(2, sample.StaffCount);
            Assert.True(sample.DatabaseCommandCount > 0);
            Assert.True(sample.DurationMs >= 0);
        });

        var orderedDurations = samples.Select(sample => sample.DurationMs).Order().ToArray();
        var evidence = new
        {
            schemaVersion = 1,
            capturedAt = DateTime.UtcNow,
            workflow = "live-support-admin-dashboard",
            representativeData = new { conversations = 12, staff = 2 },
            samples,
            summary = new
            {
                p50Ms = Percentile(orderedDurations, 0.50),
                p95Ms = Percentile(orderedDurations, 0.95),
                minDatabaseCommands = samples.Min(sample => sample.DatabaseCommandCount),
                maxDatabaseCommands = samples.Max(sample => sample.DatabaseCommandCount)
            },
            interpretation = "Baseline evidence only. Current command count is not an accepted release budget; the optimized contract is fixed independently of row count."
        };

        var output = Environment.GetEnvironmentVariable("PLATFORM_PERFORMANCE_BASELINE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var fullPath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(
                fullPath,
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static async Task SeedDashboardAsync(AppDbContext db, int conversationCount)
    {
        var staff = Enumerable.Range(0, 2)
            .Select(index => NewUser($"baseline-staff-{index}"))
            .ToArray();
        var students = Enumerable.Range(0, conversationCount)
            .Select(index => NewUser($"baseline-student-{index}"))
            .ToArray();
        db.Users.AddRange(staff.Concat(students));
        await db.SaveChangesAsync();

        foreach (var user in staff)
        {
            db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig
            {
                UserId = user.Id,
                IsEnabled = true,
                MaxActiveConversations = conversationCount,
                ConfiguredByUserId = user.Id,
                Version = 1
            });
        }

        for (var index = 0; index < students.Length; index++)
        {
            var owner = staff[index % staff.Length];
            var createdAt = DateTime.UtcNow.AddMinutes(-students.Length + index);
            var conversation = new LiveSupportConversation
            {
                ParticipantType = LiveSupportParticipantType.Student,
                StudentUserId = students[index].Id,
                LinkedStudentUserId = students[index].Id,
                CurrentOwnerUserId = owner.Id,
                Status = LiveSupportConversationStatus.Active,
                AssignedAt = createdAt.AddSeconds(2),
                LastMessageAt = createdAt.AddSeconds(5),
                Subject = $"baseline-{index}",
                Version = 1,
                CreatedAt = createdAt
            };
            db.LiveSupportConversations.Add(conversation);
            db.LiveSupportAssignments.Add(new LiveSupportAssignment
            {
                ConversationId = conversation.Id,
                StaffUserId = owner.Id,
                StartedAt = conversation.AssignedAt!.Value,
                AssignmentSequence = 1
            });
            db.LiveSupportMessages.Add(new LiveSupportMessage
            {
                ConversationId = conversation.Id,
                SenderType = LiveSupportSenderType.Student,
                SenderUserId = students[index].Id,
                ClientMessageId = $"baseline-{index}",
                Type = LiveSupportMessageType.Text,
                Content = "baseline",
                SentAt = createdAt.AddSeconds(5)
            });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static User NewUser(string prefix) => new()
    {
        FullName = prefix,
        PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}",
        PasswordHash = "integration"
    };

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        if (ordered.Count == 0)
            return 0;
        var index = (int)Math.Ceiling(percentile * ordered.Count) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Count - 1)];
    }

    private sealed record BaselineSample(
        int Attempt,
        double DurationMs,
        int DatabaseCommandCount,
        int ConversationCount,
        int StaffCount);

    private sealed class CommandCounterInterceptor : DbCommandInterceptor
    {
        private int _commandCount;
        public int CommandCount => Volatile.Read(ref _commandCount);
        public void Reset() => Interlocked.Exchange(ref _commandCount, 0);

        private void Count() => Interlocked.Increment(ref _commandCount);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Count();
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count();
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Count();
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count();
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            Count();
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Count();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });

        public void Invalidate()
        {
        }
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Student.Welcome;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.Student;

public sealed class StudentWelcomeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(730)]
    public async Task NewAndExistingStudentsReceiveFirstWelcomeUntilCompleted(int accountAgeDays)
    {
        await using var fixture = await Fixture.Create(accountAgeDays);
        var first = (await fixture.Claim()).Data!;
        Assert.Equal("first", first.Kind);
        Assert.Null((await fixture.Claim()).Data); // Another tab cannot claim the active welcome.
        Assert.True((await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(fixture.UserId, first.Token), default)).Data);
        Assert.True((await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(fixture.UserId, first.Token), default)).Data);
        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.StudentProfiles.SingleAsync();
        Assert.Equal(fixture.Clock.Now.UtcDateTime, persisted.FirstWelcomeCompletedAt);
        Assert.Equal(CairoTime.ToDate(fixture.Clock.Now.UtcDateTime), persisted.LastWelcomeDate);
        Assert.Null((await fixture.Claim()).Data);
    }

    [Fact]
    public async Task ReturningWelcomeResetsAtCairoMidnightAndPreservesFirstCompletion()
    {
        await using var fixture = await Fixture.Create();
        fixture.Clock.Now = new DateTimeOffset(2026, 9, 24, 20, 59, 45, TimeSpan.Zero);
        var first = (await fixture.Claim()).Data!;
        await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(fixture.UserId, first.Token), default);
        var completedAt = fixture.Clock.Now.UtcDateTime;
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(14);
        Assert.Null((await fixture.Claim()).Data);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(2);
        var returning = (await fixture.Claim()).Data!;
        Assert.Equal("returning", returning.Kind);
        await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(fixture.UserId, returning.Token), default);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(completedAt, (await fixture.Db.StudentProfiles.SingleAsync()).FirstWelcomeCompletedAt);
        Assert.Null((await fixture.Claim()).Data);
    }

    [Fact]
    public async Task AbandonedExpiredAndForeignClaimsDoNotMarkFirstWelcomeComplete()
    {
        await using var fixture = await Fixture.Create();
        var first = (await fixture.Claim()).Data!;
        Assert.False((await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(Guid.NewGuid(), first.Token), default)).Data);
        Assert.False((await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(fixture.UserId, Guid.NewGuid()), default)).Data);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(4);
        Assert.False((await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(fixture.UserId, first.Token), default)).Data);
        var retry = (await fixture.Claim()).Data!;
        Assert.Equal("first", retry.Kind);
        Assert.NotEqual(first.Token, retry.Token);
        await fixture.Handler.Handle(new ReleaseStudentWelcomeCommand(fixture.UserId, retry.Token), default);
        Assert.Equal("first", (await fixture.Claim()).Data!.Kind);
        fixture.Db.ChangeTracker.Clear();
        Assert.Null((await fixture.Db.StudentProfiles.SingleAsync()).FirstWelcomeCompletedAt);
    }

    [Fact]
    public async Task StaleTabCannotReleaseOrCompleteNewerClaim()
    {
        await using var fixture = await Fixture.Create();
        var stale = (await fixture.Claim()).Data!;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(4);
        var current = (await fixture.Claim()).Data!;
        await fixture.Handler.Handle(new ReleaseStudentWelcomeCommand(fixture.UserId, stale.Token), default);
        Assert.False((await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(fixture.UserId, stale.Token), default)).Data);
        Assert.Null((await fixture.Claim()).Data);
        Assert.True((await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(fixture.UserId, current.Token), default)).Data);
    }

    [Fact]
    public async Task MissingProfileDoesNotProduceWelcomeOrReceipt()
    {
        await using var fixture = await Fixture.Create();
        var missing = Guid.NewGuid();
        Assert.Null((await fixture.Handler.Handle(new ClaimStudentWelcomeCommand(missing), default)).Data);
        Assert.False((await fixture.Handler.Handle(new CompleteStudentWelcomeCommand(missing, Guid.NewGuid()), default)).Data);
    }

    [Fact]
    public async Task StatsCountFirstCompletionOnlyAndExcludeDeletedAccounts()
    {
        await using var fixture = await Fixture.Create();
        var today = CairoTime.ToLocal(fixture.Clock.Now.UtcDateTime).Date;
        var (start, end) = CairoTime.GetDayRangeUtc(today);
        foreach (var (completion, deleted, active) in new[] {
            ((DateTime?)start, false, true),
            ((DateTime?)start.AddTicks(-1), false, false),
            ((DateTime?)end, false, true),
            ((DateTime?)start, true, true) })
        {
            fixture.Db.StudentProfiles.Add(new StudentProfile {
                User = new User { FullName = "Stats test", PhoneNumber = Guid.NewGuid().ToString(), PasswordHash = "test", IsDeleted = deleted, IsActive = active },
                FirstWelcomeCompletedAt = completion,
                LastWelcomeDate = CairoTime.ToDate(fixture.Clock.Now.UtcDateTime)
            });
        }
        await fixture.Db.SaveChangesAsync();
        var stats = (await new GetStudentWelcomeStatsQueryHandler(fixture.Db, fixture.Clock)
            .Handle(new GetStudentWelcomeStatsQuery(), default)).Data!;
        Assert.Equal(4, stats.TotalStudents);
        Assert.Equal(3, stats.Completed);
        Assert.Equal(1, stats.Pending);
        Assert.Equal(1, stats.CompletedToday);
        Assert.Equal(75m, stats.CompletionPercent);
    }

    [Fact]
    public async Task EmptyStudentPopulationReturnsZeroPercent()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Db.StudentProfiles.ExecuteDeleteAsync();
        var stats = (await new GetStudentWelcomeStatsQueryHandler(fixture.Db, fixture.Clock)
            .Handle(new GetStudentWelcomeStatsQuery(), default)).Data!;
        Assert.Equal(new StudentWelcomeStatsDto(0, 0, 0, 0, 0), stats);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture(SqliteConnection connection, AppDbContext db, Guid userId) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = db;
        public Guid UserId { get; } = userId;
        public Clock Clock { get; } = new();
        public StudentWelcomeCommandHandler Handler => new(Db, Clock);
        public Task<ApiResponse<WelcomeClaimDto?>> Claim() => Handler.Handle(new ClaimStudentWelcomeCommand(UserId), default);
        public static async Task<Fixture> Create(int accountAgeDays = 0)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var user = new User { FullName = "Welcome test", PhoneNumber = "01000000000", PasswordHash = "test-only" };
            db.StudentProfiles.Add(new StudentProfile { User = user, CreatedAt = DateTime.UtcNow.AddDays(-accountAgeDays) });
            await db.SaveChangesAsync();
            return new(connection, db, user.Id);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}

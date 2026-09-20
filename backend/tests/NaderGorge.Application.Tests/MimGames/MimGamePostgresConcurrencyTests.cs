using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.MimGames;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using Npgsql;

namespace NaderGorge.Application.Tests.MimGames;

public sealed class MimGamePostgresFactAttribute : FactAttribute
{
    public MimGamePostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MIM_GAME_TEST_DB") is null)
            Skip = "Requires an isolated loopback MIM_GAME_TEST_DB.";
    }
}

[Collection("MimGamePostgres")]
public sealed class MimGamePostgresConcurrencyTests
{
    static MimGamePostgresConcurrencyTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    [MimGamePostgresFact]
    public async Task SimultaneousGenerationClaims_AllowExactlyOneRun()
    {
        var connection = Connection();
        await ResetAsync(connection);
        Guid lessonId;
        await using (var seed = Open(connection)) lessonId = (await SeedAsync(seed)).LessonId;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var first = Open(connection);
        await using var second = Open(connection);
        var firstHandler = new GenerateLessonMimGameCommandHandler(first, new NoOpJobs());
        var secondHandler = new GenerateLessonMimGameCommandHandler(second, new NoOpJobs());
        async Task<NaderGorge.Application.Common.ApiResponse<Guid>> Claim(GenerateLessonMimGameCommandHandler handler)
        { await gate.Task; return await handler.Handle(new(lessonId), default); }
        var claims = new[] { Claim(firstHandler), Claim(secondHandler) };
        gate.SetResult();
        var results = await Task.WhenAll(claims);

        Assert.Single(results, result => result.Success);
        Assert.Single(results, result => !result.Success && result.Errors!.Contains("MIM_ALREADY_GENERATING"));
        await using var verify = Open(connection);
        var game = await verify.LessonMimGames.SingleAsync();
        Assert.Equal(LessonMimGameStatus.Generating, game.Status);
        Assert.NotNull(game.CurrentGenerationRunId);
        Assert.Equal(1, game.Version);
    }

    [MimGamePostgresFact]
    public async Task SourceInvalidation_ConflictsWithStalePublishAndCompletionWrites()
    {
        var connection = Connection();
        await ResetAsync(connection);
        Guid lessonId;
        await using (var seed = Open(connection)) lessonId = (await SeedAsync(seed)).LessonId;
        await using var stalePublish = Open(connection);
        await using var staleCompletion = Open(connection);
        var publishCopy = await stalePublish.LessonMimGames.SingleAsync();
        var completionCopy = await staleCompletion.LessonMimGames.SingleAsync();

        await using (var invalidator = Open(connection))
            Assert.Equal(1, await invalidator.LessonMimGames.Where(game => game.LessonId == lessonId).ExecuteUpdateAsync(setters => setters
                .SetProperty(game => game.IsEnabled, false)
                .SetProperty(game => game.Status, LessonMimGameStatus.Stale)
                .SetProperty(game => game.CurrentGenerationRunId, (Guid?)null)
                .SetProperty(game => game.Version, game => game.Version + 1)));

        publishCopy.IsEnabled = true; publishCopy.PublishedContentJson = "{}"; publishCopy.Version++;
        completionCopy.Status = LessonMimGameStatus.Ready; completionCopy.DraftContentJson = "{}"; completionCopy.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stalePublish.SaveChangesAsync());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleCompletion.SaveChangesAsync());
        await using var verify = Open(connection);
        var game = await verify.LessonMimGames.SingleAsync();
        Assert.False(game.IsEnabled);
        Assert.Equal(LessonMimGameStatus.Stale, game.Status);
        Assert.Null(game.PublishedContentJson);
        Assert.Null(game.DraftContentJson);
        Assert.Equal(1, game.Version);
    }

    [MimGamePostgresFact]
    public async Task DisableDuringCallback_CannotBeOverwrittenByStaleCompletion()
    {
        var connection = Connection();
        await ResetAsync(connection);
        await using (var seed = Open(connection)) await SeedAsync(seed, enabled: true);
        await using var callback = Open(connection);
        var stale = await callback.LessonMimGames.SingleAsync();
        await using (var admin = Open(connection))
        {
            var lessonId = await admin.LessonMimGames.Select(game => game.LessonId).SingleAsync();
            Assert.True((await new DisableLessonMimGameCommandHandler(admin).Handle(new(lessonId), default)).Success);
        }
        stale.Status = LessonMimGameStatus.Ready; stale.DraftContentJson = "{}"; stale.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => callback.SaveChangesAsync());
        await using var verify = Open(connection);
        Assert.False((await verify.LessonMimGames.SingleAsync()).IsEnabled);
    }

    private static string Connection()
    {
        var value = Environment.GetEnvironmentVariable("MIM_GAME_TEST_DB")!;
        var parsed = new NpgsqlConnectionStringBuilder(value);
        Assert.Equal("mim_game_test", parsed.Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        return value;
    }

    private static AppDbContext Open(string connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private static async Task ResetAsync(string connection)
    { await using var db = Open(connection); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync(); }

    private static async Task<(Guid LessonId, Guid GameId)> SeedAsync(AppDbContext db, bool enabled = false)
    {
        var user = new User { FullName = "MIM PostgreSQL teacher", PhoneNumber = $"9{Guid.NewGuid():N}"[..15], PasswordHash = "test" };
        var teacher = new TeacherProfile { User = user, UserId = user.Id, Bio = "test", Specialization = "test", ContactInfo = "test" };
        var subject = new Subject { Name = "MIM", NormalizedName = $"MIM_{Guid.NewGuid():N}" };
        var package = new Package { Name = "MIM", Description = "test", TargetGrade = "3", Subject = subject, SubjectId = subject.Id, Teacher = teacher, TeacherId = teacher.Id };
        var term = new Term { Title = "Term", Package = package, PackageId = package.Id };
        var section = new ContentSection { Title = "Section", Term = term, TermId = term.Id };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary", ContentSection = section, ContentSectionId = section.Id };
        var videoType = new VideoType { Name = "Lesson", NormalizedName = $"LESSON_{Guid.NewGuid():N}" };
        var video = new LessonVideo { Title = "Video", Provider = "youtube", ProviderVideoId = "x", Lesson = lesson, LessonId = lesson.Id,
            VideoType = videoType, VideoTypeId = videoType.Id, IsActive = true, SubtitleUrl = "/srt", SourceRevision = 1 };
        video.VideoChapters.Add(new VideoChapter { Title = "Chapter", SummaryText = "Grounded", StartTime = 0, EndTime = 10, Order = 1, LessonVideo = video, LessonVideoId = video.Id });
        var game = new LessonMimGame { Lesson = lesson, LessonId = lesson.Id, Status = LessonMimGameStatus.Ready, IsEnabled = enabled };
        db.AddRange(user, teacher, subject, package, term, section, lesson, videoType, video, game);
        await db.SaveChangesAsync();
        return (lesson.Id, game.Id);
    }

    private sealed class NoOpJobs : IJobEnqueuer
    { public Task EnqueueJobAsync<T>(string queueName, string jobName, T data) => Task.CompletedTask; }
}

[CollectionDefinition("MimGamePostgres", DisableParallelization = true)]
public sealed class MimGamePostgresCollection;

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Commands.MindmapOps;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class AiOutputLanguageJobContractTests
{
    [Fact]
    public async Task AnalyzeVideo_EnglishPackageQueuesFencedEnglishRun()
    {
        await using var fixture = await SeededVideoFixture.CreateAsync(AiOutputLanguage.English);
        var jobs = new RecordingJobEnqueuer();
        var handler = new AnalyzeVideoAICommandHandler(fixture.Db, jobs, new NoOpAiJobCancellationStore());

        var response = await handler.Handle(
            new AnalyzeVideoAICommand(fixture.VideoId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("en", jobs.Payload.GetProperty("outputLanguage").GetString());
        var runId = jobs.Payload.GetProperty("generationRunId").GetGuid();
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(runId, await fixture.Db.LessonVideos
            .Where(video => video.Id == fixture.VideoId)
            .Select(video => video.CurrentAiAnalysisRunId)
            .SingleAsync());
    }

    [Fact]
    public async Task GenerateMindmaps_ArabicPackageQueuesFencedArabicRun()
    {
        await using var fixture = await SeededVideoFixture.CreateAsync(AiOutputLanguage.Arabic);
        var jobs = new RecordingJobEnqueuer();
        var handler = new GenerateChapterMindmapsCommandHandler(
            fixture.Db,
            jobs,
            new NoOpAiJobCancellationStore());

        var response = await handler.Handle(
            new GenerateChapterMindmapsCommand(fixture.VideoId),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("ar", jobs.Payload.GetProperty("outputLanguage").GetString());
        var runId = jobs.Payload.GetProperty("generationRunId").GetGuid();
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(runId, await fixture.Db.LessonVideos
            .Where(video => video.Id == fixture.VideoId)
            .Select(video => video.CurrentMindmapGenerationRunId)
            .SingleAsync());
    }

    [Fact]
    public async Task RegenerateMindmap_AutoPackageQueuesFencedAutoRun()
    {
        await using var fixture = await SeededVideoFixture.CreateAsync(AiOutputLanguage.Auto);
        var jobs = new RecordingJobEnqueuer();
        var handler = new RegenerateChapterMindmapCommandHandler(
            fixture.Db,
            jobs,
            new NoOpAiJobCancellationStore());

        var response = await handler.Handle(
            new RegenerateChapterMindmapCommand(fixture.ChapterId),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("auto", jobs.Payload.GetProperty("outputLanguage").GetString());
        var runId = jobs.Payload.GetProperty("generationRunId").GetGuid();
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(runId, await fixture.Db.VideoChapters
            .Where(chapter => chapter.Id == fixture.ChapterId)
            .Select(chapter => chapter.CurrentMindmapGenerationRunId)
            .SingleAsync());
    }

    private sealed class RecordingJobEnqueuer : IJobEnqueuer
    {
        public JsonElement Payload { get; private set; }

        public Task EnqueueJobAsync<T>(string queueName, string jobName, T payload)
        {
            Payload = JsonSerializer.SerializeToElement(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpAiJobCancellationStore : IAiJobCancellationStore
    {
        public Task RequestVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task RequestMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearVideoAnalysisCancellationAsync(Guid videoId) => Task.CompletedTask;
        public Task ClearMindmapCancellationAsync(Guid videoId) => Task.CompletedTask;
    }

    private sealed class SeededVideoFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SeededVideoFixture(
            SqliteConnection connection,
            AppDbContext db,
            Guid videoId,
            Guid chapterId)
        {
            _connection = connection;
            Db = db;
            VideoId = videoId;
            ChapterId = chapterId;
        }

        public AppDbContext Db { get; }
        public Guid VideoId { get; }
        public Guid ChapterId { get; }

        public static async Task<SeededVideoFixture> CreateAsync(AiOutputLanguage language)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();

            var teacherUser = new User
            {
                FullName = "AI Teacher",
                PhoneNumber = $"9{Guid.NewGuid():N}"[..11],
                PasswordHash = "hashed"
            };
            var teacher = new TeacherProfile
            {
                User = teacherUser,
                Specialization = "FirstSecondary",
                ContactInfo = "contact"
            };
            var subject = new Subject
            {
                Name = "Science",
                NormalizedName = Guid.NewGuid().ToString("N")
            };
            var package = new Package
            {
                Name = "Science Package",
                Subject = subject,
                Teacher = teacher,
                TargetGrade = "FirstSecondary",
                AiOutputLanguage = language
            };
            var term = new Term { Title = "Term", Package = package };
            var section = new ContentSection { Title = "Section", Term = term };
            var lesson = new Lesson { Title = "Lesson", ContentSection = section };
            var videoType = new VideoType
            {
                Name = "شرح",
                NormalizedName = Guid.NewGuid().ToString("N")
            };
            var video = new LessonVideo
            {
                Title = "Video",
                Provider = "youtube",
                ProviderVideoId = "video-id",
                Lesson = lesson,
                VideoType = videoType
            };
            var chapter = new VideoChapter
            {
                Title = "Chapter",
                SummaryText = "Summary",
                Order = 1,
                LessonVideo = video
            };
            var teacherPhoto = new TeacherPhoto
            {
                Teacher = teacherUser,
                FileUrl = "/teacher-photos/teacher.webp",
                IsActive = true
            };

            db.AddRange(video, chapter, teacherPhoto);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return new SeededVideoFixture(connection, db, video.Id, chapter.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

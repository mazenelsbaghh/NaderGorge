using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.MimGames;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Tests.MimGames;

public class MimGameStateTests
{
    [Fact]
    public async Task Generation_EnqueuesWorkerSourcePackWithCamelCaseContract()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db);
        var videoId = await db.LessonVideos.Select(video => video.Id).SingleAsync();
        db.LessonMimGames.Remove(game);
        await db.SaveChangesAsync();
        var jobs = new RecordingJobs();

        var result = await new GenerateLessonMimGameCommandHandler(db, jobs)
            .Handle(new(lesson.Id, videoId), default);

        Assert.True(result.Success);
        using var payload = JsonDocument.Parse(jobs.PayloadJson!);
        var sourcePack = payload.RootElement.GetProperty("sourcePack");
        Assert.Equal(lesson.Id, sourcePack.GetProperty("lessonId").GetGuid());
        Assert.False(sourcePack.TryGetProperty("LessonId", out _));
        var sourceVideo = sourcePack.GetProperty("videos")[0];
        Assert.Equal(videoId, sourceVideo.GetProperty("id").GetGuid());
        Assert.True(sourceVideo.GetProperty("chapters")[0].TryGetProperty("startTime", out _));
    }

    [Fact]
    public async Task Completion_UpdatesDraftButNeverRepublishesOrEnables()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db);
        var videoId = await db.LessonVideos.Select(video => video.Id).SingleAsync();
        game.GenerationSourceVideoId = videoId;
        var source = await MimGameSource.BuildAsync(db, lesson.Id, videoId, default);
        game.IsEnabled = false;
        game.PublishedContentJson = ValidJson("النسخة المنشورة");
        game.PublishedFingerprint = "old-fingerprint";
        await db.SaveChangesAsync();

        var result = await new CompleteLessonMimGameCommandHandler(db).Handle(
            new(game.Id, game.CurrentGenerationRunId!.Value, source.Fingerprint!, ValidJson("المسودة الجديدة", source.Pack!)), default);

        Assert.True(result.Success);
        Assert.False(game.IsEnabled);
        Assert.Equal("النسخة المنشورة", JsonDocument.Parse(game.PublishedContentJson!).RootElement.GetProperty("title").GetString());
        Assert.Equal("المسودة الجديدة", JsonDocument.Parse(game.DraftContentJson!).RootElement.GetProperty("title").GetString());
        Assert.Equal(LessonMimGameStatus.Ready, game.Status);
        Assert.Equal(videoId, game.DraftSourceVideoId);
        Assert.Null(game.GenerationSourceVideoId);
    }

    [Fact]
    public async Task StaleRunCannotOverwriteDraft()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (_, game) = await SeedAsync(db);
        var result = await new CompleteLessonMimGameCommandHandler(db).Handle(
            new(game.Id, Guid.NewGuid(), "stale", ValidJson("قديم")), default);
        Assert.True(result.Success);
        Assert.Null(game.DraftContentJson);
        Assert.Equal(LessonMimGameStatus.Generating, game.Status);
    }

    [Fact]
    public async Task DisableDuringGenerationSurvivesLateCompletion()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db);
        var source = await MimGameSource.BuildAsync(db, lesson.Id, default);
        await new DisableLessonMimGameCommandHandler(db).Handle(new(lesson.Id), default);
        await new CompleteLessonMimGameCommandHandler(db).Handle(
            new(game.Id, game.CurrentGenerationRunId!.Value, source.Fingerprint!, ValidJson("نتيجة", source.Pack!)), default);
        Assert.False(game.IsEnabled);
    }

    [Fact]
    public async Task MissingVideoAnalysisBlocksGenerationSource()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, _) = await SeedAsync(db);
        var video = await db.LessonVideos.SingleAsync();
        video.SubtitleUrl = null;
        await db.SaveChangesAsync();

        var source = await MimGameSource.BuildAsync(db, lesson.Id, default);

        Assert.False(source.Success);
        Assert.Equal("MIM_ANALYSIS_REQUIRED", source.Error);
        Assert.Contains(video.Title, source.MissingVideos);
    }

    [Fact]
    public async Task SelectedAnalyzedVideo_DoesNotWaitForOtherLessonVideos()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, _) = await SeedAsync(db, includePendingVideo: true);
        var analyzedVideo = await db.LessonVideos.SingleAsync(video => video.SubtitleUrl != null);
        var pendingVideo = await db.LessonVideos.SingleAsync(video => video.SubtitleUrl == null);

        var selected = await MimGameSource.BuildAsync(db, lesson.Id, analyzedVideo.Id, default);
        var wholeLesson = await MimGameSource.BuildAsync(db, lesson.Id, default);
        var pending = await MimGameSource.BuildAsync(db, lesson.Id, pendingVideo.Id, default);

        Assert.True(selected.Success);
        Assert.Single(selected.Pack!.Videos);
        Assert.Equal(analyzedVideo.Id, selected.Pack.Videos[0].Id);
        Assert.False(wholeLesson.Success);
        Assert.Equal("MIM_ANALYSIS_REQUIRED", wholeLesson.Error);
        Assert.False(pending.Success);
        Assert.Contains(pendingVideo.Title, pending.MissingVideos);
    }

    [Fact]
    public async Task PublishRejectsDraftFromPreviousSourceRevision()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db);
        game.Status = LessonMimGameStatus.Ready;
        game.DraftContentJson = ValidJson("مسودة");
        game.DraftFingerprint = "previous-source";
        await db.SaveChangesAsync();

        var response = await new PublishLessonMimGameCommandHandler(db).Handle(new(lesson.Id), default);

        Assert.False(response.Success);
        Assert.False(game.IsEnabled);
        Assert.Null(game.PublishedContentJson);
    }

    [Fact]
    public async Task PublishAndStudentRead_UseOnlyTheSelectedVideoSource()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db, includePendingVideo: true);
        var selectedVideo = await db.LessonVideos.SingleAsync(video => video.SubtitleUrl != null);
        var source = await MimGameSource.BuildAsync(db, lesson.Id, selectedVideo.Id, default);
        game.Status = LessonMimGameStatus.Ready;
        game.DraftContentJson = ValidJson("مسودة الجزء المختار", source.Pack!);
        game.DraftFingerprint = source.Fingerprint;
        game.DraftSourceVideoId = selectedVideo.Id;
        await db.SaveChangesAsync();

        var publish = await new PublishLessonMimGameCommandHandler(db).Handle(new(lesson.Id), default);
        var response = await new GetLessonDetailQueryHandler(
                db,
                new FullAccess(),
                new TeacherAuthorizationService(db),
                archiveAccess: new AllowArchiveAccess())
            .Handle(new GetLessonDetailQuery(lesson.Id, Guid.NewGuid()), default);

        Assert.True(publish.Success);
        Assert.True(game.IsEnabled);
        Assert.Equal(selectedVideo.Id, game.PublishedSourceVideoId);
        Assert.NotNull(response.Data!.MimGame);
    }

    [Fact]
    public async Task ChangingAnUnselectedVideo_DoesNotInvalidateTheSelectedGame()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db, includePendingVideo: true);
        var selectedVideo = await db.LessonVideos.SingleAsync(video => video.SubtitleUrl != null);
        var unselectedVideo = await db.LessonVideos.SingleAsync(video => video.SubtitleUrl == null);
        game.CurrentGenerationRunId = null;
        game.GenerationSourceVideoId = null;
        game.Status = LessonMimGameStatus.Ready;
        game.DraftFingerprint = "draft";
        game.DraftSourceVideoId = selectedVideo.Id;
        game.PublishedFingerprint = "published";
        game.PublishedSourceVideoId = selectedVideo.Id;
        game.IsEnabled = true;
        await db.SaveChangesAsync();

        var unselectedChanges = await LessonVideoSourceMutation.InvalidateMimGameAsync(
            db, lesson.Id, unselectedVideo.Id, default);
        var selectedChanges = await LessonVideoSourceMutation.InvalidateMimGameAsync(
            db, lesson.Id, selectedVideo.Id, default);

        Assert.Equal(0, unselectedChanges);
        Assert.Equal(1, selectedChanges);
        Assert.False(game.IsEnabled);
        Assert.Equal(LessonMimGameStatus.Stale, game.Status);
    }

    [Fact]
    public async Task SingleVideoPurchaseCannotReceiveLessonWideGame()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db);
        var source = await MimGameSource.BuildAsync(db, lesson.Id, default);
        game.IsEnabled = true;
        game.PublishedContentJson = ValidJson("منشورة", source.Pack!);
        game.PublishedFingerprint = source.Fingerprint;
        await db.SaveChangesAsync();
        var videoId = await db.LessonVideos.Select(video => video.Id).SingleAsync();
        var access = new VideoOnlyAccess(videoId);

        var response = await new GetLessonDetailQueryHandler(db, access, new TeacherAuthorizationService(db), archiveAccess: new AllowArchiveAccess())
            .Handle(new GetLessonDetailQuery(lesson.Id, Guid.NewGuid()), default);

        Assert.True(response.Success);
        Assert.True(response.Data!.IsVideoOnlyAccess);
        Assert.Null(response.Data.MimGame);
    }

    [Fact]
    public async Task LockedLessonCannotReceivePublishedGameWithFullEntitlement()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db);
        var source = await MimGameSource.BuildAsync(db, lesson.Id, default);
        var exam = new Exam { Title = "اختبار إلزامي", IsActive = true, IsMandatory = true, CreatedByTeacherId = Guid.NewGuid() };
        lesson.ExamId = exam.Id;
        game.IsEnabled = true;
        game.PublishedContentJson = ValidJson("منشورة", source.Pack!);
        game.PublishedFingerprint = source.Fingerprint;
        db.Exams.Add(exam);
        await db.SaveChangesAsync();

        var response = await new GetLessonDetailQueryHandler(db, new FullAccess(), new TeacherAuthorizationService(db), archiveAccess: new AllowArchiveAccess())
            .Handle(new GetLessonDetailQuery(lesson.Id, Guid.NewGuid()), default);

        Assert.True(response.Success);
        Assert.True(response.Data!.IsLocked);
        Assert.Null(response.Data.MimGame);
    }

    private static async Task<(Lesson Lesson, LessonMimGame Game)> SeedAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        bool includePendingVideo = false)
    {
        var package = new Package { Name = "باقة", Description = "", TargetGrade = "3", AiOutputLanguage = NaderGorge.Domain.Enums.AiOutputLanguage.Arabic };
        var term = new Term { Title = "ترم", Package = package, PackageId = package.Id };
        var section = new ContentSection { Title = "قسم", Term = term, TermId = term.Id };
        var lesson = new Lesson { Title = "الحصة", Summary = "ملخص", ContentSection = section, ContentSectionId = section.Id };
        var video = new LessonVideo { Title = "الفيديو", Provider = "youtube", ProviderVideoId = "x", Lesson = lesson, LessonId = lesson.Id,
            IsActive = true, SubtitleUrl = "/subtitle.srt", SourceRevision = 1 };
        video.VideoChapters.Add(new VideoChapter { Title = "الفصل", SummaryText = "شرح موثوق", StartTime = 0, EndTime = 60, Order = 1, LessonVideo = video, LessonVideoId = video.Id });
        lesson.Videos.Add(video);
        if (includePendingVideo)
        {
            lesson.Videos.Add(new LessonVideo
            {
                Title = "جزء آخر غير محلل",
                Provider = "youtube",
                ProviderVideoId = "not-analyzed",
                Lesson = lesson,
                LessonId = lesson.Id,
                IsActive = true,
                SourceRevision = 1,
                Order = 2
            });
        }
        var game = new LessonMimGame { Lesson = lesson, LessonId = lesson.Id, Status = LessonMimGameStatus.Generating,
            CurrentGenerationRunId = Guid.NewGuid(), GenerationExpiresAtUtc = DateTime.UtcNow.AddMinutes(10) };
        db.AddRange(package, term, section, lesson, video, game);
        await db.SaveChangesAsync();
        return (lesson, game);
    }

    private static string ValidJson(string title, MimSourcePack? pack = null)
    {
        var chapter = pack?.Videos[0].Chapters[0];
        var source = new[] { new { videoId = pack?.Videos[0].Id ?? Guid.NewGuid(), chapterId = chapter?.Id ?? Guid.NewGuid(), startTime = chapter?.StartTime ?? 0, endTime = chapter?.EndTime ?? 60 } };
        var missions = Enumerable.Range(1, 3).Select(index => new { title = $"مهمة {index}", instruction = "تعليمات", hint = "تلميح",
            reward = "ختم", icon = "book", sourceRefs = source, choices = new[] { "أ", "ب" },
            tasks = Enumerable.Range(1, 3).Select(task => new { label = $"سؤال {task}", icon = "target", correctChoiceIndex = 0, explanation = "شرح" }) });
        return JsonSerializer.Serialize(new { schemaVersion = 1, title, intro = "مقدمة", sourceLabel = "الحصة", missions });
    }

    private sealed class VideoOnlyAccess(Guid accessibleVideoId) : IAccessCheckService
    {
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default) => Task.FromResult(lessonVideoId == accessibleVideoId);
        public Task<IReadOnlySet<Guid>> GetAccessibleVideoIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonVideoIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid> { accessibleVideoId });
        public Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FullAccess : IAccessCheckService
    {
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlySet<Guid>> GetAccessibleVideoIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonVideoIds, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<Guid>>(lessonVideoIds.ToHashSet());
        public Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class AllowArchiveAccess : IContentArchiveAccessService
    {
        public Task<bool> CanViewAsync(Guid userId, NaderGorge.Domain.Enums.ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlySet<Guid>> GetViewableLessonIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<Guid>>(lessonIds.ToHashSet());
        public Task<IReadOnlySet<Guid>> GetViewableLessonVideoIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonVideoIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<Guid>>(lessonVideoIds.ToHashSet());
        public Task<bool> CanAcquireAsync(NaderGorge.Domain.Enums.ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RecordingJobs : IJobEnqueuer
    {
        public string? PayloadJson { get; private set; }
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            Assert.Equal("ai-lesson-game-queue", queueName);
            Assert.Equal("generate-mim", jobName);
            PayloadJson = JsonSerializer.Serialize(data);
            return Task.CompletedTask;
        }
    }
}

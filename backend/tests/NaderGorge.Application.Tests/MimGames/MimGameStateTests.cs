using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.MimGames;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Tests.MimGames;

public class MimGameStateTests
{
    [Fact]
    public async Task Completion_UpdatesDraftButNeverRepublishesOrEnables()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (lesson, game) = await SeedAsync(db);
        var source = await MimGameSource.BuildAsync(db, lesson.Id, default);
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

    private static async Task<(Lesson Lesson, LessonMimGame Game)> SeedAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var package = new Package { Name = "باقة", Description = "", TargetGrade = "3", AiOutputLanguage = NaderGorge.Domain.Enums.AiOutputLanguage.Arabic };
        var term = new Term { Title = "ترم", Package = package, PackageId = package.Id };
        var section = new ContentSection { Title = "قسم", Term = term, TermId = term.Id };
        var lesson = new Lesson { Title = "الحصة", Summary = "ملخص", ContentSection = section, ContentSectionId = section.Id };
        var video = new LessonVideo { Title = "الفيديو", Provider = "youtube", ProviderVideoId = "x", Lesson = lesson, LessonId = lesson.Id,
            IsActive = true, SubtitleUrl = "/subtitle.srt", SourceRevision = 1 };
        video.VideoChapters.Add(new VideoChapter { Title = "الفصل", SummaryText = "شرح موثوق", StartTime = 0, EndTime = 60, Order = 1, LessonVideo = video, LessonVideoId = video.Id });
        lesson.Videos.Add(video);
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
}

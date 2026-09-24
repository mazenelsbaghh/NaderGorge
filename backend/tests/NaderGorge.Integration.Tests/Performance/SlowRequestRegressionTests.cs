using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Integration.Tests.LiveSupport;
using Xunit.Abstractions;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using HomeworkQuestion = NaderGorge.Domain.Entities.Homework.HomeworkQuestion;

namespace NaderGorge.Integration.Tests.Performance;

// Production incident 2026-09-24: lesson reads issued 67–92 commands and progress dominated slow requests.
public sealed class SlowRequestRegressionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task LessonLists_KeepQueryCountBoundedAsOwnedLessonsGrow()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var small = await MeasureListsAsync(fixture, 2);
        var large = await MeasureListsAsync(fixture, 20);
        Assert.InRange(large.MyLessons, 1, small.MyLessons + 2);
        Assert.InRange(large.Section, 1, small.Section + 2);
    }

    private async Task<(int MyLessons, int Section)> MeasureListsAsync(PostgresLiveSupportFixture fixture, int count)
    {
        var graph = await SeedAsync(fixture.Db, count);
        var counter = new QueryCounter();
        await using var db = Open(fixture, counter);
        var scope = new AcademicScopeService(db);
        IContentArchiveAccessService archives = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, scope, archives);
        var timer = Stopwatch.StartNew();
        var mine = await new GetMyLessonsQueryHandler(db, scope, archives)
            .Handle(new GetMyLessonsQuery(graph.Student.Id), default);
        Assert.True(mine.Success, mine.Message);
        Assert.Equal(count, mine.Data!.Count);
        var myCount = counter.Count;
        output.WriteLine($"My lessons ({count}): {myCount} queries, {timer.ElapsedMilliseconds} ms");
        counter.Reset();
        timer.Restart();
        var section = await new GetLessonsQueryHandler(db, access, scope, archives)
            .Handle(new GetLessonsQuery(graph.Section.Id, graph.Student.Id), default);
        Assert.True(section.Success, section.Message);
        Assert.Equal(count, section.Data!.Count);
        Assert.All(section.Data, lesson => Assert.True(lesson.HasAccess));
        Assert.False(section.Data[0].IsLocked);
        Assert.All(section.Data.Skip(1), lesson => Assert.NotNull(lesson.BlockingHomeworkLessonId));
        output.WriteLine($"Section ({count}): {counter.Count} queries, {timer.ElapsedMilliseconds} ms");
        return (myCount, counter.Count);
    }

    [Fact]
    public async Task LessonDetail_ExamVisibilityQueriesStayBoundedAsPartsGrow()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var graph = await SeedAsync(fixture.Db, 1);
        var small = await MeasureDetailAsync(fixture, graph);
        for (var index = 1; index < 20; index++)
        {
            var video = new LessonVideo
            {
                Title = $"Part {index}", LessonId = graph.Videos[0].LessonId,
                VideoTypeId = graph.Videos[0].VideoTypeId, Provider = "youtube",
                ProviderVideoId = $"part-{index}", IsActive = true, Order = index
            };
            fixture.Db.Exams.Add(new Exam
            {
                Title = "Part exam", CreatedByTeacherId = graph.Section.Term.Package.TeacherId,
                LessonVideo = video, IsMandatory = false
            });
        }
        await fixture.Db.SaveChangesAsync();
        var large = await MeasureDetailAsync(fixture, graph);
        Assert.InRange(large, 1, small + 2);
    }

    private async Task<int> MeasureDetailAsync(PostgresLiveSupportFixture fixture, Graph graph)
    {
        var counter = new QueryCounter();
        await using var db = Open(fixture, counter);
        var scope = new AcademicScopeService(db);
        IContentArchiveAccessService archives = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, scope, archives);
        var detail = await new GetLessonDetailQueryHandler(db, access, new TeacherAuthorizationService(db), scope, archives)
            .Handle(new GetLessonDetailQuery(graph.Videos[0].LessonId, graph.Student.Id), default);
        Assert.True(detail.Success, detail.Message);
        Assert.All(detail.Data!.Videos, video => Assert.True(video.HasAccess));
        output.WriteLine($"Lesson detail ({detail.Data.Videos.Count} parts): {counter.Count} queries");
        return counter.Count;
    }

    [Fact]
    public async Task ExamVisibilityBatch_PreservesArchiveRulesWithoutPerExamQueries()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var graph = await SeedAsync(fixture.Db, 8);
        graph.Section.Term.Package.ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly;
        graph.Videos[1].ArchiveMode = ContentArchiveMode.HiddenFromEveryone;
        graph.Exams[2].ArchiveMode = ContentArchiveMode.HiddenFromEveryone;
        // A lesson-linked exam takes precedence over the video-linked path.
        graph.Videos[3].ArchiveMode = ContentArchiveMode.HiddenFromEveryone;
        graph.Videos[0].Lesson.ExamId = graph.Exams[3].Id;
        await fixture.Db.SaveChangesAsync();
        var counter = new QueryCounter();
        await using var db = Open(fixture, counter);
        IContentArchiveAccessService archives = new ContentArchiveAccessService(db);
        var ids = graph.Exams.Select(exam => exam.Id).Append(Guid.NewGuid()).ToArray();
        var visible = await archives.GetViewableAssessmentIdsAsync(graph.Student.Id, ContentArchiveTargetType.Exam, ids);
        var batchCount = counter.Count;
        Assert.DoesNotContain(graph.Exams[1].Id, visible);
        Assert.DoesNotContain(graph.Exams[2].Id, visible);
        Assert.Contains(graph.Exams[3].Id, visible);
        foreach (var id in ids)
            Assert.Equal(await archives.CanViewAsync(graph.Student.Id, ContentArchiveTargetType.Exam, id), visible.Contains(id));
        Assert.InRange(batchCount, 1, 5);
        output.WriteLine($"Archive checks ({ids.Length}): {batchCount} queries");
        graph.Grant.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await archives.GetViewableAssessmentIdsAsync(graph.Student.Id, ContentArchiveTargetType.Exam, ids));
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("expired", false)]
    [InlineData("exhausted", false)]
    [InlineData("different-exam", false)]
    public async Task BatchedExamArchiveAccess_EnforcesPublicProductGrantBoundaries(string state, bool expected)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var graph = await SeedAsync(fixture.Db, 2);
        graph.Grant.IsActive = false;
        graph.Section.Term.Package.ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly;
        var product = new PublicExamProduct
        {
            Exam = state == "different-exam" ? graph.Exams[1] : graph.Exams[0],
            Slug = Guid.NewGuid().ToString("N"), CreatedByUserId = graph.Section.Term.Package.Teacher.UserId
        };
        fixture.Db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = graph.Student.Id, GrantType = CodeType.Exam, ExamId = product.Exam.Id, PublicExamProduct = product,
            IsActive = true, ExpiresAt = state == "expired" ? DateTime.UtcNow.AddMinutes(-1) : null,
            MaxUses = 1, UsesConsumed = state == "exhausted" ? 1 : 0
        });
        await fixture.Db.SaveChangesAsync();
        await using var db = Open(fixture, new QueryCounter());
        IContentArchiveAccessService archives = new ContentArchiveAccessService(db);
        var ids = await archives.GetViewableAssessmentIdsAsync(
            graph.Student.Id, ContentArchiveTargetType.Exam, [graph.Exams[0].Id]);
        Assert.Equal(expected, ids.Contains(graph.Exams[0].Id));
        Assert.Equal(expected, await archives.CanViewAsync(graph.Student.Id, ContentArchiveTargetType.Exam, graph.Exams[0].Id));
    }

    [Fact]
    public async Task PlaybackProgress_UsesOneStateReadAndPreservesIdempotency()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var graph = await SeedAsync(fixture.Db, 1);
        var session = new VideoPlaybackSession
        {
            UserId = graph.Student.Id, LessonVideoId = graph.Videos[0].Id,
            SessionToken = Guid.NewGuid().ToString("N"), EncryptionKey = "test-only",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2), ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            TrackingDurationSeconds = 100, TrackingThresholdPercentage = 30, TrackingThresholdSeconds = 30
        };
        fixture.Db.VideoPlaybackSessions.Add(session);
        await fixture.Db.SaveChangesAsync();
        var counter = new QueryCounter();
        await using var db = Open(fixture, counter);
        var scope = new AcademicScopeService(db);
        var access = new AccessCheckService(db, scope, new ContentArchiveAccessService(db));
        Assert.True(await access.HasAccessToVideoSessionAsync(session));
        output.WriteLine($"Playback access: {counter.Count} queries");
        var accessQueries = counter.Count;
        counter.Reset();
        var handler = new TrackWatchProgressCommandHandler(db, new Settings(), new PostgresVideoPlaybackConcurrency(db));
        var command = new TrackWatchProgressCommand(session.LessonVideoId, session.UserId, session.Id, 1, 30, 1, 100);
        var first = await handler.Handle(command, default);
        Assert.True(first.Success, first.Message);
        Assert.Equal(1, first.Data!.CurrentCount);
        var firstQueries = counter.Count;
        db.ChangeTracker.Clear();
        var replay = await handler.Handle(command, default);
        Assert.True(replay.Data!.Duplicate);
        Assert.Equal(30m, replay.Data.LearningWatchedSeconds);
        Assert.Equal(1, replay.Data.CurrentCount);
        output.WriteLine($"Progress: {firstQueries} commands");
        Assert.InRange(firstQueries, 1, 3);
        Assert.InRange(accessQueries, 1, 12);
    }

    [Fact]
    public async Task ExamDashboard_AggregatesAnswersAndPreservesHistoricalScores()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var graph = await SeedAsync(fixture.Db, 1);
        var exam = graph.Exams[0];
        var question = new ExamQuestion
        {
            Exam = exam, Points = 10,
            Question = new QuestionBankItem { Text = "Question", Subject = graph.Section.Term.Package.Subject,
                CreatedByTeacher = graph.Section.Term.Package.Teacher }
        };
        exam.ExamQuestions.Add(question);
        var snapshot = AssessmentDefinitionSnapshot.FromExam(exam);
        var attempt = new StudentExamAttempt
        {
            Exam = exam, User = graph.Student, ScoreAchieved = 10, IsPassed = true,
            Evaluation = "Passed", DefinitionSnapshotJson = snapshot.ToJson(),
            Answers = [new StudentAnswer { ExamQuestion = question, IsCorrect = true, PointsAwarded = 10 }]
        };
        fixture.Db.StudentExamAttempts.Add(attempt);
        fixture.Db.StudentExamAttempts.Add(new StudentExamAttempt
        {
            Exam = exam, User = NewUser("Other student"), ScoreAchieved = 0,
            Answers = [new StudentAnswer { ExamQuestion = question, IsCorrect = false, PointsAwarded = 0 }]
        });
        await fixture.Db.SaveChangesAsync();
        var counter = new QueryCounter();
        await using var db = Open(fixture, counter);
        var dashboard = await new GetExamDashboardQueryHandler(db)
            .Handle(new GetExamDashboardQuery(exam.Id, graph.Section.Term.Package.Teacher.UserId), default);
        Assert.True(dashboard.Success, dashboard.Message);
        var questionSummary = Assert.Single(dashboard.Data!.Questions);
        Assert.Equal(2, questionSummary.TotalAttempts);
        Assert.Equal(1, questionSummary.CorrectCount);
        Assert.Equal(50m, questionSummary.CorrectPercentage);
        Assert.Equal(10m, dashboard.Data.Attempts.Single(row => row.AttemptId == attempt.Id).ScoreAchieved);
        Assert.Contains(dashboard.Data.Attempts, row => row.Evaluation == "يحتاج تسوية يدوية");
        output.WriteLine($"Exam dashboard: {counter.Count} queries; {db.ChangeTracker.Entries<StudentAnswer>().Count()} tracked answers");
        Assert.Empty(db.ChangeTracker.Entries<StudentAnswer>());
    }

    private static async Task<Graph> SeedAsync(AppDbContext db, int count)
    {
        var student = NewUser("Performance student");
        var subject = new Subject { Name = "Performance", NormalizedName = Guid.NewGuid().ToString("N") };
        var teacher = new TeacherProfile { User = NewUser("Performance teacher"), IsContentVisibleToStudents = true };
        var package = new Package { Name = "Performance", Subject = subject, Teacher = teacher };
        var section = new ContentSection { Title = "Performance", Term = new Term { Title = "Term", Package = package } };
        var typeId = await db.VideoTypes.Select(type => type.Id).FirstAsync();
        var videos = new List<LessonVideo>();
        var exams = new List<Exam>();
        db.StudentProfiles.Add(new StudentProfile
        {
            User = student, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondSecondary,
            Governorate = "Cairo", Address = "Test", Gender = Gender.Male
        });
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package, OwnerId = package.Id, ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        for (var index = 0; index < count; index++)
        {
            var lesson = new Lesson { Title = $"Lesson {index}", ContentSection = section, Order = index };
            var video = new LessonVideo { Title = "Video", Lesson = lesson, VideoTypeId = typeId,
                Provider = "youtube", ProviderVideoId = "test", IsActive = true, MaxWatchCount = 3 };
            var exam = new Exam { Title = "Exam", CreatedByTeacher = teacher, LessonVideo = video, IsMandatory = false, TotalScore = 10 };
            db.Exams.Add(exam);
            db.Homeworks.Add(new HomeworkEntity
            {
                LessonId = lesson.Id, Title = "Homework", IsMandatory = true, IsActive = true,
                Questions = [new HomeworkQuestion { BodyText = "Test", PointsActive = 1 }]
            });
            videos.Add(video);
            exams.Add(exam);
        }
        var grant = new StudentAccessGrant { User = student, GrantType = CodeType.Package,
            PackageId = package.Id, IsActive = true, GrantedAt = DateTime.UtcNow };
        db.StudentAccessGrants.Add(grant);
        await db.SaveChangesAsync();
        return new(student, section, videos, exams, grant);
    }

    private static User NewUser(string name) => new()
    {
        FullName = name, PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "test-only"
    };

    private static AppDbContext Open(PostgresLiveSupportFixture fixture, QueryCounter counter) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).AddInterceptors(counter).Options);

    private sealed record Graph(User Student, ContentSection Section, List<LessonVideo> Videos, List<Exam> Exams, StudentAccessGrant Grant);

    private sealed class Settings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken ct) => Task.FromResult(CachedPlatformSettings.Default);
        public void Invalidate() { }
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public void Reset() => Count = 0;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}

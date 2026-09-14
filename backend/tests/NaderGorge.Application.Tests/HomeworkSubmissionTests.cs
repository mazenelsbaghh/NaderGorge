using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Homework.Commands;
using NaderGorge.Application.Features.Homework.Queries;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Tests;

public sealed class HomeworkSubmissionTests
{
    [Theory]
    [InlineData(5, 60, 0)]
    [InlineData(null, 5, 10)]
    public async Task HomeworkTimerUsesAttemptDefinitionNotLaterSettings(int? originalMinutes, int currentMinutes, decimal expectedScore)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Timer student", "201000000092");
        var lesson = new Lesson { Title = "Timer lesson", ContentSectionId = Guid.NewGuid(), Order = 1 };
        var homework = new Homework { LessonId = lesson.Id, Title = "Timed work", TotalScore = 10, DurationMinutes = originalMinutes };
        var question = new HomeworkQuestion { HomeworkId = homework.Id, BodyText = "Choose", PointsActive = 1,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ, PossibleAnswers = ["A", "B"], CorrectAnswerKey = "A" };
        db.Lessons.Add(lesson); db.Homeworks.Add(homework); db.HomeworkQuestions.Add(question);
        await db.SaveChangesAsync();
        var start = new StartHomeworkAttemptQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService());
        var first = await start.Handle(new(homework.Id, student.Id), default);
        Assert.True(first.Success, first.Message);
        var submission = await db.HomeworkSubmissions.SingleAsync();
        submission.StartedAt = DateTime.UtcNow.AddMinutes(-10);
        homework.DurationMinutes = currentMinutes;
        await db.SaveChangesAsync();
        var resumed = await start.Handle(new(homework.Id, student.Id), default);
        Assert.Equal(originalMinutes, resumed.Data!.DurationMinutes);
        Assert.Equal(originalMinutes.HasValue ? 0 : (int?)null, resumed.Data.RemainingSeconds);
        var submitted = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id, [new(question.Id, "A")]), default);
        Assert.True(submitted.Success, submitted.Message);
        Assert.Equal(expectedScore, submission.OverallScore);
        Assert.Equal("A", (await db.HomeworkAnswers.SingleAsync()).ProvidedAnswer);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(null, false)]
    public async Task StartingNextHomeworkUsesPreviousAttemptPassingThreshold(int? savedPassingScore, bool allowed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Snapshot student", "201000000091");
        var sectionId = Guid.NewGuid();
        var previousLesson = new Lesson { Title = "Previous", ContentSectionId = sectionId, Order = 1 };
        var nextLesson = new Lesson { Title = "Next", ContentSectionId = sectionId, Order = 2 };
        var previous = new Homework { LessonId = previousLesson.Id, Title = "Previous work", TotalScore = 10, PassingScoreThreshold = 9 };
        var next = new Homework { LessonId = nextLesson.Id, Title = "Next work", TotalScore = 10 };
        db.Lessons.AddRange(previousLesson, nextLesson);
        db.Homeworks.AddRange(previous, next);
        db.HomeworkQuestions.AddRange(
            new HomeworkQuestion { HomeworkId = previous.Id, BodyText = "Previous question", PointsActive = 10 },
            new HomeworkQuestion { HomeworkId = next.Id, BodyText = "Next question", PointsActive = 10 });
        db.HomeworkSubmissions.Add(new HomeworkSubmission
        {
            HomeworkId = previous.Id, StudentId = student.Id, Status = SubmissionStatus.Graded,
            OverallScore = 6, PassingScoreSnapshot = savedPassingScore, SubmittedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await new StartHomeworkAttemptQueryHandler(db,
                new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(next.Id, student.Id), CancellationToken.None);

        Assert.Equal(allowed, response.Success);
        Assert.Equal(allowed, await db.HomeworkSubmissions.AnyAsync(s => s.HomeworkId == next.Id));
    }

    [Fact]
    public async Task SubmitHomework_PersistsAnswersForSubmission()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "201000000001");
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            Title = "Lesson",
            Summary = "Summary",
            ContentSectionId = Guid.NewGuid(),
            Order = 1
        };
        var homework = new Homework
        {
            Id = Guid.NewGuid(),
            LessonId = lesson.Id,
            Title = "Homework",
            TotalScore = 10,
            PassingScoreThreshold = 5
        };
        var firstQuestion = new HomeworkQuestion
        {
            Id = Guid.NewGuid(),
            HomeworkId = homework.Id,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ,
            BodyText = "First",
            CorrectAnswerKey = "A",
            PointsActive = 1,
            AudioUrl = "/uploads/audio/homework-correction.mp3",
            WrittenCorrection = "التصحيح المسجل للسؤال"
        };
        var secondQuestion = new HomeworkQuestion
        {
            Id = Guid.NewGuid(),
            HomeworkId = homework.Id,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ,
            BodyText = "Second",
            CorrectAnswerKey = "B",
            PointsActive = 1
        };
        db.Lessons.Add(lesson);
        db.Homeworks.Add(homework);
        db.HomeworkQuestions.AddRange(firstQuestion, secondQuestion);
        await db.SaveChangesAsync();

        var handler = new SubmitHomeworkCommandHandler(
            db,
            new HomeworkNoOpPublisher(),
            new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(),
            new HomeworkAllowArchiveAccessService());

        var response = await handler.Handle(
            new SubmitHomeworkCommand(
                homework.Id,
                student.Id,
                [
                    new StudentAnswerInput(firstQuestion.Id, "A"),
                    new StudentAnswerInput(secondQuestion.Id, "B")
                ]),
            CancellationToken.None);

        Assert.True(response.Success);
        var submission = await db.HomeworkSubmissions.SingleAsync(s => s.HomeworkId == homework.Id && s.StudentId == student.Id);
        var persistedAnswers = await db.HomeworkAnswers
            .Where(a => a.HomeworkSubmissionId == submission.Id)
            .OrderBy(a => a.QuestionId)
            .ToListAsync();

        Assert.Equal(SubmissionStatus.Graded, submission.Status);
        Assert.Equal(2, persistedAnswers.Count);
        Assert.All(persistedAnswers, answer => Assert.Equal(1, answer.ScoreReceived));
        Assert.Contains(persistedAnswers, answer => answer.QuestionId == firstQuestion.Id && answer.ProvidedAnswer == "A");
        Assert.Contains(persistedAnswers, answer => answer.QuestionId == secondQuestion.Id && answer.ProvidedAnswer == "B");

        var resultHandler = new GetHomeworkResultQueryHandler(
            db,
            new HomeworkAllowAccessService(),
            new HomeworkAllowArchiveAccessService());
        var result = await resultHandler.Handle(
            new GetHomeworkResultQuery(homework.Id, student.Id),
            CancellationToken.None);

        Assert.True(result.Success);
        var questionReview = result.Data!.QuestionReviews.Single(q => q.QuestionId == firstQuestion.Id);
        Assert.Equal("/uploads/audio/homework-correction.mp3", questionReview.AudioUrl);
        Assert.Equal("التصحيح المسجل للسؤال", questionReview.WrittenCorrection);
    }

    private sealed class HomeworkAllowAccessService : IAccessCheckService
    {
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class HomeworkAllowArchiveAccessService : IContentArchiveAccessService
    {
        public Task<bool> CanViewAsync(Guid userId, NaderGorge.Domain.Enums.ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlySet<Guid>> GetViewableLessonIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(lessonIds.ToHashSet());
        public Task<IReadOnlySet<Guid>> GetViewableLessonVideoIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonVideoIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(lessonVideoIds.ToHashSet());
        public Task<bool> CanAcquireAsync(NaderGorge.Domain.Enums.ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class HomeworkNoOpJobEnqueuer : IJobEnqueuer
    {
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data) => Task.CompletedTask;
    }

    private sealed class HomeworkNoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}

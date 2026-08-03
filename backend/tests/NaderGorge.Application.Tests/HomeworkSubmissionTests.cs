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
            new HomeworkNoOpJobEnqueuer());

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

        var resultHandler = new GetHomeworkResultQueryHandler(db, new HomeworkAllowAccessService());
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

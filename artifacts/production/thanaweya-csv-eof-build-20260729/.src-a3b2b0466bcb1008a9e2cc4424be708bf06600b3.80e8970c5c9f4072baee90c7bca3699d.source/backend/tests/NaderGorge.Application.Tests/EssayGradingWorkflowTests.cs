using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class EssayGradingWorkflowTests
{
    [Fact]
    public async Task SubmitExam_WithEssay_ReturnsPendingResultAndCreatesWaitAIEssay()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "501");
        var (exam, mcqExamQuestion, essayExamQuestion, _, _, correctOption, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var handler = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        var result = await handler.Handle(
            new SubmitExamCommand(exam.Id, attempt.Id, student.Id, new List<AnswerSubmissionDto>
            {
                new(mcqExamQuestion.Id, correctOption.Id, null),
                new(essayExamQuestion.Id, null, "Gravity pulls objects together.")
            }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Pending", result.Data!.ResultState);
        Assert.False(result.Data.IsPassed);
        var pendingEssayReview = result.Data.Questions.Single(q => q.ExamQuestionId == essayExamQuestion.Id);
        Assert.Null(pendingEssayReview.CorrectOptionText);
        Assert.Null(pendingEssayReview.WrittenCorrection);
        var savedEssay = db.EssaySubmissions.Single(e => e.StudentExamAttemptId == attempt.Id && e.QuestionId == essayExamQuestion.QuestionBankItemId);
        Assert.Equal(EssaySubmissionStatus.WaitAI, savedEssay.Status);
        var queuedEvaluation = db.OutboxEvents.Single(e => e.Type == "EssayEvaluationQueued");
        Assert.Contains(savedEssay.Id.ToString(), queuedEvaluation.PayloadJson);

        var statusQuery = new GetExamAttemptGradingStatusQueryHandler(db);
        var status = await statusQuery.Handle(new GetExamAttemptGradingStatusQuery(attempt.Id, student.Id), CancellationToken.None);
        Assert.Equal("Pending", status.Data!.ResultState);
    }

    [Fact]
    public async Task EssayCallback_WhenAiReturnsTrue_AwardsEssayPointsAndFinalizesAttempt()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "502");
        var (exam, mcqExamQuestion, essayExamQuestion, _, _, correctOption, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var submitHandler = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        await submitHandler.Handle(
            new SubmitExamCommand(exam.Id, attempt.Id, student.Id, new List<AnswerSubmissionDto>
            {
                new(mcqExamQuestion.Id, correctOption.Id, null),
                new(essayExamQuestion.Id, null, "Gravity explanation")
            }),
            CancellationToken.None);

        var essay = db.EssaySubmissions.Single(e => e.StudentExamAttemptId == attempt.Id && e.QuestionId == essayExamQuestion.QuestionBankItemId);

        var aiHandler = new WebhookEssayGradedCommandHandler(db);
        var aiResult = await aiHandler.Handle(new WebhookEssayGradedCommand(essay.Id, 1m, "AI says correct"), CancellationToken.None);
        Assert.True(aiResult.Success);

        db.ChangeTracker.Clear();
        var persistedEssay = db.EssaySubmissions.AsNoTracking().Single(e => e.Id == essay.Id);
        Assert.Equal(EssaySubmissionStatus.TeacherGraded, persistedEssay.Status);
        Assert.Equal(8m, persistedEssay.TeacherFinalScore);

        var persistedAttempt = db.StudentExamAttempts.AsNoTracking().Single(a => a.Id == attempt.Id);
        Assert.Equal(10m, persistedAttempt.ScoreAchieved);
        Assert.True(persistedAttempt.IsPassed);
        Assert.NotNull(persistedAttempt.Evaluation);

        var persistedAnswer = db.StudentAnswers.AsNoTracking().Single(a => a.StudentExamAttemptId == attempt.Id && a.ExamQuestionId == essayExamQuestion.Id);
        Assert.True(persistedAnswer.IsCorrect);
        Assert.Equal(8m, persistedAnswer.PointsAwarded);

        var statusQuery = new GetExamAttemptGradingStatusQueryHandler(db);
        var status = await statusQuery.Handle(new GetExamAttemptGradingStatusQuery(attempt.Id, student.Id), CancellationToken.None);
        Assert.Equal("Completed", status.Data!.ResultState);

        var resultQuery = new GetExamAttemptResultQueryHandler(db);
        var completedResult = await resultQuery.Handle(new GetExamAttemptResultQuery(attempt.Id, student.Id), CancellationToken.None);
        Assert.True(completedResult.Success);
        var completedEssayReview = completedResult.Data!.Questions.Single(q => q.ExamQuestionId == essayExamQuestion.Id);
        Assert.Equal("A force attracting masses.", completedEssayReview.CorrectOptionText);
        Assert.Equal("A force attracting masses.", completedEssayReview.WrittenCorrection);
    }

    [Fact]
    public async Task EssayCallback_WhenAiReturnsFalse_AwardsZeroAndFinalizesAttempt()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "503");
        var (exam, mcqExamQuestion, essayExamQuestion, _, _, correctOption, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var submitHandler = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        await submitHandler.Handle(
            new SubmitExamCommand(exam.Id, attempt.Id, student.Id, new List<AnswerSubmissionDto>
            {
                new(mcqExamQuestion.Id, correctOption.Id, null),
                new(essayExamQuestion.Id, null, "Wrong gravity explanation")
            }),
            CancellationToken.None);

        var essay = db.EssaySubmissions.Single(e => e.StudentExamAttemptId == attempt.Id && e.QuestionId == essayExamQuestion.QuestionBankItemId);

        var aiHandler = new WebhookEssayGradedCommandHandler(db);
        var aiResult = await aiHandler.Handle(new WebhookEssayGradedCommand(essay.Id, 0m, "AI says incorrect"), CancellationToken.None);
        Assert.True(aiResult.Success);

        db.ChangeTracker.Clear();
        var persistedEssay = db.EssaySubmissions.AsNoTracking().Single(e => e.Id == essay.Id);
        Assert.Equal(EssaySubmissionStatus.TeacherGraded, persistedEssay.Status);
        Assert.Equal(0m, persistedEssay.TeacherFinalScore);

        var persistedAttempt = db.StudentExamAttempts.AsNoTracking().Single(a => a.Id == attempt.Id);
        Assert.Equal(2m, persistedAttempt.ScoreAchieved);
        Assert.False(persistedAttempt.IsPassed);

        var persistedAnswer = db.StudentAnswers.AsNoTracking().Single(a => a.StudentExamAttemptId == attempt.Id && a.ExamQuestionId == essayExamQuestion.Id);
        Assert.False(persistedAnswer.IsCorrect);
        Assert.Equal(0m, persistedAnswer.PointsAwarded);

        var statusQuery = new GetExamAttemptGradingStatusQueryHandler(db);
        var status = await statusQuery.Handle(new GetExamAttemptGradingStatusQuery(attempt.Id, student.Id), CancellationToken.None);
        Assert.Equal("Completed", status.Data!.ResultState);
    }
}

internal sealed class FakeJobEnqueuer : IJobEnqueuer
{
    public readonly List<(string QueueName, string JobName)> Jobs = new();

    public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
    {
        Jobs.Add((queueName, jobName));
        return Task.CompletedTask;
    }
}

internal sealed class NoOpPublisher : IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => Task.CompletedTask;
}

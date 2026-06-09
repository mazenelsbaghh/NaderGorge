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
        var savedEssay = db.EssaySubmissions.Single(e => e.StudentExamAttemptId == attempt.Id && e.QuestionId == essayExamQuestion.QuestionBankItemId);
        Assert.Equal(EssaySubmissionStatus.WaitAI, savedEssay.Status);

        var statusQuery = new GetExamAttemptGradingStatusQueryHandler(db);
        var status = await statusQuery.Handle(new GetExamAttemptGradingStatusQuery(attempt.Id, student.Id), CancellationToken.None);
        Assert.Equal("Pending", status.Data!.ResultState);
    }

    [Fact]
    public async Task EssayCallbackAndTeacherGrade_AdvanceLifecycleAndFinalizeAttempt()
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

        var auth = new TeacherAuthorizationService(db);
        var gradeBeforeAi = await new GradeEssayCommandHandler(db, auth)
            .Handle(new GradeEssayCommand(essay.Id, 7m, "Teacher review"), CancellationToken.None);
        Assert.False(gradeBeforeAi.Success);

        var aiHandler = new WebhookEssayGradedCommandHandler(db);
        var aiResult = await aiHandler.Handle(new WebhookEssayGradedCommand(essay.Id, 6m, "AI review"), CancellationToken.None);
        Assert.True(aiResult.Success);
        Assert.Equal(EssaySubmissionStatus.AIScored, db.EssaySubmissions.Single(e => e.Id == essay.Id).Status);

        essay.Status = EssaySubmissionStatus.WaitTeacher;
        await db.SaveChangesAsync();

        var gradeResult = await new GradeEssayCommandHandler(db, auth)
            .Handle(new GradeEssayCommand(essay.Id, 8m, "Teacher final"), CancellationToken.None);

        Assert.True(gradeResult.Success);
        Assert.Equal(EssaySubmissionStatus.TeacherGraded, db.EssaySubmissions.Single(e => e.Id == essay.Id).Status);
        db.ChangeTracker.Clear();
        var persistedEssay = db.EssaySubmissions.AsNoTracking().Single(e => e.Id == essay.Id);
        Assert.Equal(EssaySubmissionStatus.TeacherGraded, persistedEssay.Status);
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

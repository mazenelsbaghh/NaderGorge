using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Assessments;
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
    public async Task Missing_teacher_answer_key_still_queues_text_essay_for_ai()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var essay = new EssaySubmission
        {
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            StudentExamAttemptId = Guid.NewGuid(),
            AnswerText = "Student answer",
            Status = EssaySubmissionStatus.WaitAI
        };

        EssayEvaluationQueue.Enqueue(db, essay, "Question text", null);

        Assert.Equal(EssaySubmissionStatus.WaitAI, essay.Status);
        Assert.NotNull(essay.AiNextRetryAt);
        var queued = Assert.Single(db.OutboxEvents.Local);
        Assert.Equal("EssayEvaluationQueued", queued.Type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmittedEssayQueuesTextForAiAndRecordingForTeacherReview(bool hasRecording)
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
                new(essayExamQuestion.Id, null, "Gravity pulls objects together.", AudioUrl: hasRecording ? "/answer.webm" : null)
            }),
            CancellationToken.None);

        Assert.True(result.Success);
        var expectedState = hasRecording ? "PartiallyGraded" : "Pending";
        Assert.Equal(expectedState, result.Data!.ResultState);
        Assert.False(result.Data.IsPassed);
        var pendingEssayReview = result.Data.Questions.Single(q => q.ExamQuestionId == essayExamQuestion.Id);
        Assert.Null(pendingEssayReview.CorrectOptionText);
        Assert.Null(pendingEssayReview.WrittenCorrection);
        Assert.Null(pendingEssayReview.GradingFeedback);
        var savedEssay = db.EssaySubmissions.Single(e => e.StudentExamAttemptId == attempt.Id && e.QuestionId == essayExamQuestion.QuestionBankItemId);
        Assert.Equal(hasRecording ? EssaySubmissionStatus.WaitTeacher : EssaySubmissionStatus.WaitAI, savedEssay.Status);
        var queuedEvaluations = db.OutboxEvents.Where(e => e.Type == "EssayEvaluationQueued").ToList();
        if (hasRecording) Assert.Empty(queuedEvaluations);
        else
        {
            var bridge = new FakeJobEnqueuer();
            await NaderGorge.API.BackgroundServices.EssayEvaluationOutboxQueueDispatcher.DispatchAsync(Assert.Single(queuedEvaluations), bridge);
            using var payload = System.Text.Json.JsonDocument.Parse(Assert.Single(bridge.Payloads));
            Assert.Equal(savedEssay.Id, payload.RootElement.GetProperty("essaySubmissionId").GetGuid());
            Assert.Equal("Explain gravity", payload.RootElement.GetProperty("questionText").GetString());
            Assert.Equal("A force attracting masses.", payload.RootElement.GetProperty("expectedAnswer").GetString());
            Assert.Equal("Gravity pulls objects together.", payload.RootElement.GetProperty("answerText").GetString());
        }

        var statusQuery = new GetExamAttemptGradingStatusQueryHandler(db);
        var status = await statusQuery.Handle(new GetExamAttemptGradingStatusQuery(attempt.Id, student.Id), CancellationToken.None);
        Assert.Equal(expectedState, status.Data!.ResultState);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EssayFinalization_PreservesAttemptScaleAfterDefinitionEdit(bool editDefinition, bool manualGrading)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "502");
        var (exam, mcqExamQuestion, essayExamQuestion, _, _, correctOption, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        if (editDefinition)
        {
            attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson();
            await db.SaveChangesAsync();
        }

        var submitHandler = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        await submitHandler.Handle(
            new SubmitExamCommand(exam.Id, attempt.Id, student.Id, new List<AnswerSubmissionDto>
            {
                new(mcqExamQuestion.Id, correctOption.Id, null),
                new(essayExamQuestion.Id, null, "Gravity explanation")
            }),
            CancellationToken.None);

        var essay = db.EssaySubmissions.Single(e => e.StudentExamAttemptId == attempt.Id && e.QuestionId == essayExamQuestion.QuestionBankItemId);

        if (editDefinition)
        {
            exam.TotalScore = 100;
            exam.PassingScore = 95;
            essayExamQuestion.Points = 1;
            essayExamQuestion.Question.WrittenCorrection = "Updated correction";
            await db.SaveChangesAsync();
        }
        if (manualGrading)
        {
            var grade = await new GradeEssayCommandHandler(db, new TeacherAuthorizationService(db))
                .Handle(new GradeEssayCommand(essay.Id, 8m, "Teacher says correct"), CancellationToken.None);
            Assert.True(grade.Success, grade.Message);
        }
        else
        {
            var grade = await new WebhookEssayGradedCommandHandler(db)
                .Handle(new WebhookEssayGradedCommand(essay.Id, 1m, "AI says correct"), CancellationToken.None);
            Assert.True(grade.Success, grade.Message);
        }

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
        Assert.Equal(manualGrading ? "Teacher says correct" : "AI says correct", completedEssayReview.GradingFeedback);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(0.33)]
    [InlineData(0.75)]
    [InlineData(1)]
    public async Task EssayCallback_PreservesEarnedCreditAndExplainsDeductions(double scoreRatio)
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
        const string feedback = "شرحت الجزء الأول صح؛ ناقص شرح الجزء الثاني وفق الإجابة النموذجية.";
        var aiResult = await aiHandler.Handle(new WebhookEssayGradedCommand(essay.Id, (decimal)scoreRatio, feedback), CancellationToken.None);
        Assert.True(aiResult.Success);
        var expectedPoints = 8m * (decimal)scoreRatio;

        var duplicate = await aiHandler.Handle(new WebhookEssayGradedCommand(essay.Id, 0m, "Duplicate delivery"), CancellationToken.None);
        Assert.True(duplicate.Success);

        db.ChangeTracker.Clear();
        var persistedEssay = db.EssaySubmissions.AsNoTracking().Single(e => e.Id == essay.Id);
        Assert.Equal(EssaySubmissionStatus.TeacherGraded, persistedEssay.Status);
        Assert.Equal(expectedPoints, persistedEssay.TeacherFinalScore);
        Assert.Equal(feedback, persistedEssay.TeacherFeedback);

        var persistedAttempt = db.StudentExamAttempts.AsNoTracking().Single(a => a.Id == attempt.Id);
        Assert.Equal(2m + expectedPoints, persistedAttempt.ScoreAchieved);
        Assert.Equal(persistedAttempt.ScoreAchieved >= exam.PassingScore, persistedAttempt.IsPassed);

        var persistedAnswer = db.StudentAnswers.AsNoTracking().Single(a => a.StudentExamAttemptId == attempt.Id && a.ExamQuestionId == essayExamQuestion.Id);
        Assert.Equal(scoreRatio == 1, persistedAnswer.IsCorrect);
        Assert.Equal(expectedPoints, persistedAnswer.PointsAwarded);

        var statusQuery = new GetExamAttemptGradingStatusQueryHandler(db);
        var status = await statusQuery.Handle(new GetExamAttemptGradingStatusQuery(attempt.Id, student.Id), CancellationToken.None);
        Assert.Equal("Completed", status.Data!.ResultState);

        var result = await new GetExamAttemptResultQueryHandler(db)
            .Handle(new GetExamAttemptResultQuery(attempt.Id, student.Id), CancellationToken.None);
        var review = result.Data!.Questions.Single(q => q.ExamQuestionId == essayExamQuestion.Id);
        Assert.Equal(expectedPoints, review.PointsAwarded);
        Assert.Equal(8m, review.MaximumPoints);
        Assert.Equal(feedback, review.GradingFeedback);
        Assert.Equal(scoreRatio == 1, review.IsCorrect);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(0.333)]
    public async Task EssayCallback_RejectsUnsupportedGradesWithoutFinalizing(double scoreRatio)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "504");
        var (exam, mcq, essayQuestion, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer()).Handle(
            new(exam.Id, attempt.Id, student.Id, [new(mcq.Id, correct.Id, null), new(essayQuestion.Id, null, "Answer")]), default);
        var essay = db.EssaySubmissions.Single(e => e.StudentExamAttemptId == attempt.Id);

        var response = await new WebhookEssayGradedCommandHandler(db)
            .Handle(new(essay.Id, (decimal)scoreRatio, "Reason"), default);

        Assert.False(response.Success);
        db.ChangeTracker.Clear();
        var saved = await db.EssaySubmissions.SingleAsync(e => e.Id == essay.Id);
        Assert.Equal(EssaySubmissionStatus.WaitAI, saved.Status);
        Assert.Null(saved.TeacherFinalScore);
        Assert.Null(saved.AiInitialScore);
    }
}

internal sealed class FakeJobEnqueuer : IJobEnqueuer
{
    public readonly List<(string QueueName, string JobName)> Jobs = new();
    public readonly List<string> Payloads = new();

    public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
    {
        Jobs.Add((queueName, jobName));
        Payloads.Add(System.Text.Json.JsonSerializer.Serialize(data));
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

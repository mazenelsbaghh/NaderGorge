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
using NaderGorge.Domain.Enums;
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
    [InlineData(null)]
    [InlineData("Gravity pulls objects together.")]
    public async Task SubmittedRecordingIsRejectedWithoutSavingOtherAnswers(string? answerText)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "501");
        var (exam, mcq, essay, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var result = await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer()).Handle(
            new(exam.Id, attempt.Id, student.Id,
                [new(mcq.Id, correct.Id, null), new(essay.Id, null, answerText, AudioUrl: "/answer.webm")]),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("الإجابات الصوتية غير مسموح بها", result.Message);
        Assert.Empty(await db.EssaySubmissions.ToListAsync());
        Assert.Empty(await db.OutboxEvents.ToListAsync());
        db.ChangeTracker.Clear();
        Assert.Null((await db.StudentExamAttempts.SingleAsync()).Evaluation);
        Assert.All(await db.StudentAnswers.ToListAsync(), answer =>
        {
            Assert.Null(answer.SubmittedText);
            Assert.Null(answer.SelectedOptionId);
        });
    }

    [Fact]
    public async Task SubmittedTextEssayQueuesForAi()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "501");
        var (exam, mcqExamQuestion, essayExamQuestion, _, _, correctOption, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var result = await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer()).Handle(
            new(exam.Id, attempt.Id, student.Id,
                [new(mcqExamQuestion.Id, correctOption.Id, null), new(essayExamQuestion.Id, null, "Gravity pulls objects together.")]),
            CancellationToken.None);

        Assert.True(result.Success);
        var expectedState = "Pending";
        Assert.Equal(expectedState, result.Data!.ResultState);
        Assert.False(result.Data.IsPassed);
        var pendingEssayReview = result.Data.Questions.Single(q => q.ExamQuestionId == essayExamQuestion.Id);
        Assert.Null(pendingEssayReview.CorrectOptionText);
        Assert.Null(pendingEssayReview.WrittenCorrection);
        Assert.Null(pendingEssayReview.GradingFeedback);
        var savedEssay = db.EssaySubmissions.Single(e => e.StudentExamAttemptId == attempt.Id && e.QuestionId == essayExamQuestion.QuestionBankItemId);
        Assert.Equal(EssaySubmissionStatus.WaitAI, savedEssay.Status);
        Assert.Null(savedEssay.AudioUrl);
        var queuedEvaluations = db.OutboxEvents.Where(e => e.Type == "EssayEvaluationQueued").ToList();
        var bridge = new FakeJobEnqueuer();
        await NaderGorge.API.BackgroundServices.EssayEvaluationOutboxQueueDispatcher.DispatchAsync(Assert.Single(queuedEvaluations), bridge, db, default);
        using var payload = System.Text.Json.JsonDocument.Parse(Assert.Single(bridge.Payloads));
        Assert.Equal(savedEssay.Id, payload.RootElement.GetProperty("essaySubmissionId").GetGuid());
        Assert.Equal("Explain gravity", payload.RootElement.GetProperty("questionText").GetString());
        Assert.Equal("A force attracting masses.", payload.RootElement.GetProperty("expectedAnswer").GetString());
        Assert.Equal("Gravity pulls objects together.", payload.RootElement.GetProperty("answerText").GetString());

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
    [InlineData(ExamEssayGradingMode.Automatic, 0, 0)]
    [InlineData(ExamEssayGradingMode.Automatic, 0.5, 4)]
    [InlineData(ExamEssayGradingMode.Automatic, 0.33, 2.64)]
    [InlineData(ExamEssayGradingMode.Automatic, 0.75, 6)]
    [InlineData(ExamEssayGradingMode.Automatic, 1, 8)]
    [InlineData(ExamEssayGradingMode.AutomaticFullCredit, 0, 0)]
    [InlineData(ExamEssayGradingMode.AutomaticFullCredit, 0.5, 0)]
    [InlineData(ExamEssayGradingMode.AutomaticFullCredit, 0.99, 0)]
    [InlineData(ExamEssayGradingMode.AutomaticFullCredit, 1, 8)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCredit, 0, 0)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCredit, 0.01, 4)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCredit, 0.33, 4)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCredit, 0.5, 4)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCredit, 0.75, 4)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCredit, 0.99, 4)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCredit, 1, 8)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent, 0, 0)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent, 0.01, 4)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent, 0.69, 4)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent, 0.7, 8)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent, 0.99, 8)]
    [InlineData(ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent, 1, 8)]
    public async Task EssayCallback_PreservesEarnedCreditAndExplainsDeductions(
        ExamEssayGradingMode mode, double scoreRatio, double awardedPoints)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "503");
        var (exam, mcqExamQuestion, essayExamQuestion, _, _, correctOption, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        exam.EssayGradingMode = mode;
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        // The saved attempt policy must win even if the live exam changes while AI is running.
        exam.EssayGradingMode = ExamEssayGradingMode.Manual;
        await db.SaveChangesAsync();

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
        var expectedPoints = (decimal)awardedPoints;

        var duplicate = await aiHandler.Handle(new WebhookEssayGradedCommand(essay.Id, 0m, "Duplicate delivery"), CancellationToken.None);
        Assert.True(duplicate.Success);

        db.ChangeTracker.Clear();
        var persistedEssay = db.EssaySubmissions.AsNoTracking().Single(e => e.Id == essay.Id);
        Assert.Equal(EssaySubmissionStatus.TeacherGraded, persistedEssay.Status);
        Assert.Equal(expectedPoints, persistedEssay.TeacherFinalScore);
        Assert.Equal((decimal)scoreRatio, persistedEssay.AiInitialScore);
        Assert.Equal(feedback, persistedEssay.AiFeedback);
        Assert.StartsWith(feedback, persistedEssay.TeacherFeedback);
        if (mode != ExamEssayGradingMode.Automatic)
            Assert.Contains("إعداد تصحيح المقالي", persistedEssay.TeacherFeedback);

        var persistedAttempt = db.StudentExamAttempts.AsNoTracking().Single(a => a.Id == attempt.Id);
        Assert.Equal(2m + expectedPoints, persistedAttempt.ScoreAchieved);
        Assert.Equal(persistedAttempt.ScoreAchieved >= exam.PassingScore, persistedAttempt.IsPassed);

        var persistedAnswer = db.StudentAnswers.AsNoTracking().Single(a => a.StudentExamAttemptId == attempt.Id && a.ExamQuestionId == essayExamQuestion.Id);
        Assert.Equal(expectedPoints == 8m, persistedAnswer.IsCorrect);
        Assert.Equal(expectedPoints, persistedAnswer.PointsAwarded);

        var statusQuery = new GetExamAttemptGradingStatusQueryHandler(db);
        var status = await statusQuery.Handle(new GetExamAttemptGradingStatusQuery(attempt.Id, student.Id), CancellationToken.None);
        Assert.Equal("Completed", status.Data!.ResultState);

        var result = await new GetExamAttemptResultQueryHandler(db)
            .Handle(new GetExamAttemptResultQuery(attempt.Id, student.Id), CancellationToken.None);
        var review = result.Data!.Questions.Single(q => q.ExamQuestionId == essayExamQuestion.Id);
        Assert.Equal(expectedPoints, review.PointsAwarded);
        Assert.Equal(8m, review.MaximumPoints);
        Assert.Equal(persistedEssay.TeacherFeedback, review.GradingFeedback);
        Assert.Equal(expectedPoints == 8m, review.IsCorrect);
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

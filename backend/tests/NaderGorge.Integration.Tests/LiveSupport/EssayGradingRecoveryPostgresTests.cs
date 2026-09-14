using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.BackgroundServices;
using NaderGorge.API.Hubs;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class EssayGradingRecoveryPostgresTests
{
    [Fact]
    public async Task SubmittedEssayBurstReachesWorkerWithoutTwoSecondDelayBetweenEveryQuestion()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        for (var index = 0; index < 8; index++) fixture.Db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "EssayEvaluationQueued", PayloadJson = JsonSerializer.Serialize(new
            {
                essaySubmissionId = Guid.NewGuid(), questionId = Guid.NewGuid(), studentId = Guid.NewGuid(),
                questionText = "Question", expectedAnswer = "Teacher key", answerText = "Student answer"
            })
        });
        await fixture.Db.SaveChangesAsync();
        var queue = new EssayQueueProbe();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<IJobEnqueuer>(queue);
        await using var provider = services.BuildServiceProvider();
        using var processor = new OutboxProcessorBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHubContext<PlatformHub>>(), NullLogger<OutboxProcessorBackgroundService>.Instance,
            new ConfigurationBuilder().Build());

        await processor.StartAsync(default);
        try { await queue.AllDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { await processor.StopAsync(default); }
        Assert.Equal(8, queue.DeliveredCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDeliveredJobIsRecoveredOnceUsingTheAttemptOriginalQuestion(bool delivered)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var essay = await Seed(fixture.Db);
        essay.CreatedAt = DateTime.UtcNow.AddDays(-30);
        essay.Question.Text = "Changed question";
        essay.Question.WrittenCorrection = "Changed answer";
        if (delivered)
        {
            EssayEvaluationQueue.Enqueue(fixture.Db, essay, "Original question", "Original answer");
            essay.AiNextRetryAt = DateTime.UtcNow.AddMinutes(-1);
            fixture.Db.OutboxEvents.Local.Single(e => e.Type == "EssayEvaluationQueued").ProcessedAt = DateTime.UtcNow.AddMinutes(-20);
        }
        await fixture.Db.SaveChangesAsync();

        var recovered = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = Open(fixture);
            return await new EssayGradingRecoveryService(db).RecoverAsync(essay.Id, default);
        }));

        Assert.Single(recovered, success => success);
        fixture.Db.ChangeTracker.Clear();
        var events = await fixture.Db.OutboxEvents.Where(e => e.Type == "EssayEvaluationQueued" && e.ProcessedAt == null).ToListAsync();
        using var payload = JsonDocument.Parse(Assert.Single(events).PayloadJson);
        Assert.Equal("Original question", payload.RootElement.GetProperty("questionText").GetString());
        Assert.Equal("Original answer", payload.RootElement.GetProperty("expectedAnswer").GetString());
        Assert.Equal(essay.Id, payload.RootElement.GetProperty("essaySubmissionId").GetGuid());
        var recovery = new EssayGradingRecoveryService(fixture.Db);
        Assert.DoesNotContain(essay.Id, await recovery.FindDueAsync(default));
        Assert.False(await recovery.RecoverAsync(essay.Id, default));
    }

    [Fact]
    public async Task DueSweepSkipsRecentAndManuallyGradedAnswersAndRoutesRecordingsForReview()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var recent = await Seed(fixture.Db);
        var manual = await Seed(fixture.Db);
        var recording = await Seed(fixture.Db);
        var due = await Seed(fixture.Db);
        foreach (var essay in new[] { manual, recording, due }) essay.CreatedAt = DateTime.UtcNow.AddHours(-1);
        manual.Status = EssaySubmissionStatus.TeacherGraded;
        manual.TeacherFinalScore = 2;
        recording.AudioUrl = "/recording.webm";
        await fixture.Db.SaveChangesAsync();

        var recovery = new EssayGradingRecoveryService(fixture.Db);
        var ids = await recovery.FindDueAsync(default);
        Assert.Contains(due.Id, ids);
        Assert.Contains(recording.Id, ids);
        Assert.DoesNotContain(recent.Id, ids);
        Assert.DoesNotContain(manual.Id, ids);
        Assert.True(await recovery.RecoverAsync(recording.Id, default));
        Assert.False(await recovery.RecoverAsync(manual.Id, default));
        Assert.Equal(EssaySubmissionStatus.WaitTeacher, (await fixture.Db.EssaySubmissions.FindAsync(recording.Id))!.Status);
        Assert.Empty(await fixture.Db.OutboxEvents.Where(e => e.Type == "EssayEvaluationQueued").ToListAsync());
    }

    [Fact]
    public async Task ConcurrentCallbacksFinalizeAllEssaysOnceAndPreserveTheCombinedScore()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var first = await Seed(fixture.Db);
        var secondQuestion = new QuestionBankItem { Type = QuestionType.Essay, Text = "Second", WrittenCorrection = "Answer",
            SubjectId = first.Question.SubjectId, CreatedByTeacherId = first.Question.CreatedByTeacherId };
        var examQuestion = new ExamQuestion { Exam = first.Attempt.Exam, Question = secondQuestion, Points = 4 };
        first.Attempt.Exam.ExamQuestions.Add(examQuestion);
        fixture.Db.ExamQuestions.Add(examQuestion);
        var secondAnswer = new StudentAnswer { Attempt = first.Attempt, ExamQuestion = examQuestion, SubmittedText = "Answer" };
        fixture.Db.StudentAnswers.Add(secondAnswer);
        var second = new EssaySubmission { Attempt = first.Attempt, StudentId = first.StudentId, Question = secondQuestion, AnswerText = "Answer" };
        fixture.Db.EssaySubmissions.Add(second);
        first.Attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(first.Attempt.Exam).ToJson();
        await fixture.Db.SaveChangesAsync();

        var responses = await Task.WhenAll(new[] { first.Id, second.Id, first.Id }.Select(async id =>
        {
            await using var db = Open(fixture);
            return await new WebhookEssayGradedCommandHandler(db).Handle(new(id, 1, "Correct"), default);
        }));

        Assert.All(responses, response => Assert.True(response.Success, response.Message));
        fixture.Db.ChangeTracker.Clear();
        var attempt = await fixture.Db.StudentExamAttempts.FindAsync(first.StudentExamAttemptId);
        Assert.Equal(10m, attempt!.ScoreAchieved);
        Assert.True(attempt.IsPassed);
        Assert.Single(await fixture.Db.OutboxEvents.Where(e => e.Type == "ExamGraded").ToListAsync());
        Assert.Single(await fixture.Db.OutboxEvents.Where(e => e.Type == "ExamResultReady").ToListAsync());
        Assert.All(await fixture.Db.EssaySubmissions.Where(e => e.StudentExamAttemptId == attempt.Id).ToListAsync(),
            essay => { Assert.Equal(EssaySubmissionStatus.TeacherGraded, essay.Status); Assert.Null(essay.AiNextRetryAt); });
    }

    [Fact]
    public async Task LateAiCallbackCannotReplaceTeacherGrade()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var essay = await Seed(fixture.Db);
        EssayEvaluationQueue.Enqueue(fixture.Db, essay, essay.Question.Text, essay.Question.WrittenCorrection);
        await fixture.Db.SaveChangesAsync();
        var manual = await new GradeEssayCommandHandler(fixture.Db, new TeacherAuthorizationService(fixture.Db))
            .Handle(new(essay.Id, 2, "Teacher grade"), default);
        Assert.True(manual.Success, manual.Message);
        var callback = await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "AI grade"), default);
        Assert.True(callback.Success, callback.Message);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.EssaySubmissions.FindAsync(essay.Id);
        Assert.Equal(2, saved!.TeacherFinalScore);
        Assert.Equal("Teacher grade", saved.TeacherFeedback);
        Assert.Null(saved.AiNextRetryAt);
        Assert.Single(await fixture.Db.OutboxEvents.Where(e => e.Type == "ExamGraded").ToListAsync());
        Assert.Empty(await fixture.Db.OutboxEvents.Where(e => e.Type == "HomeworkGraded").ToListAsync());
    }

    [Theory]
    [InlineData("invalid-score")]
    [InlineData("recording")]
    public async Task InvalidAiResultOrUnseenRecordingNeverBecomesAnAutomaticGrade(string scenario)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var essay = await Seed(fixture.Db);
        if (scenario == "recording") essay.AudioUrl = "/answer.webm";
        await fixture.Db.SaveChangesAsync();
        var result = await new WebhookEssayGradedCommandHandler(fixture.Db)
            .Handle(new(essay.Id, scenario == "invalid-score" ? 5 : 1, "AI grade"), default);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.EssaySubmissions.FindAsync(essay.Id);
        Assert.Equal(scenario == "recording", result.Success);
        Assert.Null(saved!.TeacherFinalScore);
        Assert.Equal(scenario == "recording" ? EssaySubmissionStatus.WaitTeacher : EssaySubmissionStatus.WaitAI, saved.Status);
        Assert.Empty(await fixture.Db.OutboxEvents.Where(e => e.Type == "ExamGraded").ToListAsync());
    }

    private static AppDbContext Open(PostgresLiveSupportFixture fixture) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);

    [Fact]
    public async Task CorruptLegacySnapshotIsDeferredSoItCannotStarveOtherUnfinishedAnswers()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var broken = await Seed(fixture.Db);
        var healthy = await Seed(fixture.Db);
        broken.CreatedAt = healthy.CreatedAt = DateTime.UtcNow.AddDays(-1);
        broken.Attempt.DefinitionSnapshotJson = "[]";
        await fixture.Db.SaveChangesAsync();
        var recovery = new EssayGradingRecoveryService(fixture.Db);

        await Assert.ThrowsAsync<JsonException>(() => recovery.RecoverAsync(broken.Id, default));
        await recovery.DeferFailureAsync(broken.Id, default);
        Assert.DoesNotContain(broken.Id, await recovery.FindDueAsync(default));
        Assert.True(await recovery.RecoverAsync(healthy.Id, default));
        var queued = Assert.Single(await fixture.Db.OutboxEvents.Where(e => e.Type == "EssayEvaluationQueued").ToListAsync());
        Assert.Contains(healthy.Id.ToString(), queued.PayloadJson);
    }

    private static async Task<EssaySubmission> Seed(AppDbContext db)
    {
        var student = new User { FullName = "Essay recovery", PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = new User { FullName = "Teacher", PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "test" } };
        var subject = new Subject { Name = "Essay", NormalizedName = Guid.NewGuid().ToString("N") };
        var question = new QuestionBankItem { Type = QuestionType.Essay, Text = "Original question", WrittenCorrection = "Original answer", Subject = subject, CreatedByTeacher = teacher };
        var exam = new Exam { Title = "Recovery exam", TotalScore = 10, PassingScore = 5, CreatedByTeacher = teacher };
        var examQuestion = new ExamQuestion { Question = question, Points = 4 };
        exam.ExamQuestions.Add(examQuestion);
        var attempt = new StudentExamAttempt { Exam = exam, User = student, Evaluation = "قيد التصحيح" };
        attempt.Answers.Add(new StudentAnswer { ExamQuestion = examQuestion, SubmittedText = "Student answer" });
        var essay = new EssaySubmission { Student = student, Attempt = attempt, Question = question, AnswerText = "Student answer" };
        db.EssaySubmissions.Add(essay);
        await db.SaveChangesAsync();
        attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson();
        await db.SaveChangesAsync();
        return essay;
    }

    private sealed class EssayQueueProbe : IJobEnqueuer
    {
        public TaskCompletionSource AllDelivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DeliveredCount;
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            if (Interlocked.Increment(ref DeliveredCount) == 8) AllDelivered.TrySetResult();
            return Task.CompletedTask;
        }
    }
}

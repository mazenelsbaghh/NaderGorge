using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Exams;

public sealed class ExamSubmissionRecoveryTests
{
    // 2026-10-19: an accepted submit could appear failed when its post-commit queue operation failed.
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task Answered120QuestionExam_AcceptsDuringEffectOutageAndRetryPreservesOriginalAnswers(int essayCount)
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var command = await SeedExamAsync(db, essayCount);
        var unavailable = new UnavailableEffects();
        var handler = new SubmitExamCommandHandler(db, unavailable, unavailable);

        var accepted = await handler.Handle(command, default);
        var replayed = await handler.Handle(command with { Answers = [] }, default);

        Assert.True(accepted.Success, accepted.Message);
        Assert.True(replayed.Success, replayed.Message);
        Assert.Equal(120m - essayCount, replayed.Data!.ScoreAchieved);
        Assert.Equal(essayCount == 0, replayed.Data.IsPassed);
        Assert.Equal(essayCount == 0 ? "Completed" : "Pending", replayed.Data.ResultState);
        Assert.Equal(accepted.Data!.AttemptId, replayed.Data.AttemptId);
        db.ChangeTracker.Clear();
        Assert.Equal(120 - essayCount, await db.StudentAnswers.CountAsync(answer => answer.IsCorrect && answer.SelectedOptionId != null));
        var savedEssays = await db.EssaySubmissions.ToListAsync();
        Assert.Equal(essayCount, savedEssays.Count);
        Assert.All(savedEssays, essay => Assert.Equal(SyntheticEssayAnswer, essay.AnswerText));
        Assert.Equal(essayCount, await db.OutboxEvents.CountAsync(e => e.Type == "EssayEvaluationQueued"));
        Assert.Equal(1, await db.OutboxEvents.CountAsync(e => e.Type == "ExamSubmitted"));
        Assert.Equal(1, await db.OutboxEvents.CountAsync(e => e.Type == ExamSubmissionEffects.ParentPushEventType));
        Assert.Equal(essayCount == 0 ? 1 : 0, await db.OutboxEvents.CountAsync(e => e.Type == ExamSubmissionEffects.AcademicRewardEventType));
    }

    [Fact]
    public async Task AcademicRewardRedelivery_AwardsAcceptedExamOnlyOnce()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var command = await SeedExamAsync(db);
        var unavailable = new UnavailableEffects();
        var accepted = await new SubmitExamCommandHandler(db, unavailable, unavailable).Handle(command, default);
        Assert.True(accepted.Success, accepted.Message);
        var reward = await db.OutboxEvents.AsNoTracking().SingleAsync(e => e.Type == ExamSubmissionEffects.AcademicRewardEventType);

        for (var delivery = 0; delivery < 2; delivery++)
        {
            await using var deliveryDb = fixture.CreateDbContext();
            await using var services = new ServiceCollection().AddSingleton<IAppDbContext>(deliveryDb).BuildServiceProvider();
            await ExamSubmissionOutboxDispatcher.DispatchAsync(reward, services, default);
        }

        db.ChangeTracker.Clear();
        Assert.Equal(120, (await db.StudentGamifications.SingleAsync()).TotalPoints);
        Assert.Equal(reward.Id, (await db.GamificationActionLogs.SingleAsync()).Id);
    }

    [Fact]
    public async Task PendingRevision_SubmitsUsingItsOwnTransactionAndRetryReturnsTheCompletedResult()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var command = await SeedExamAsync(db);
        var attempt = await db.StudentExamAttempts.SingleAsync();
        var definition = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", attempt.ExamId);
        var revisionId = Guid.NewGuid();
        attempt.DefinitionSnapshotJson = (definition with
        {
            RevisionId = revisionId,
            CompletionStartedAt = DateTime.UtcNow,
            Revision = new AttemptRevisionPlan(definition.Questions.Select(question =>
                new RevisionAnswer(question.Id, question.Points, null, false, RequiresCompletion: true)).ToArray())
        }).ToJson();
        await db.SaveChangesAsync();
        var unavailable = new UnavailableEffects();
        var handler = new SubmitExamCommandHandler(db, unavailable, unavailable);

        var completed = await handler.Handle(command with { RevisionId = revisionId }, default);
        var replayed = await handler.Handle(command with { RevisionId = revisionId, Answers = [] }, default);

        Assert.True(completed.Success, completed.Message);
        Assert.True(replayed.Success, replayed.Message);
        Assert.Equal(120m, replayed.Data!.ScoreAchieved);
        Assert.True(replayed.Data.IsPassed);
        db.ChangeTracker.Clear();
        var saved = await db.StudentExamAttempts.SingleAsync();
        Assert.False(AssessmentDefinitionSnapshot.Read(saved.DefinitionSnapshotJson!, "exam", saved.ExamId).Revision!.RequiresCompletion);
        Assert.Equal(1, await db.OutboxEvents.CountAsync(e => e.Type == "ExamSubmitted"));
    }

    private static readonly string SyntheticEssayAnswer = string.Concat(Enumerable.Repeat("إجابة عربية تجريبية كاملة توضح الفكرة والأسباب والنتائج دون حذف. ", 100)).Trim();

    private static async Task<SubmitExamCommand> SeedExamAsync(NaderGorge.Infrastructure.Data.AppDbContext db, int essayCount = 0)
    {
        var student = new User { FullName = "Recovery student", PhoneNumber = "01000000891", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = new User { FullName = "Recovery teacher", PhoneNumber = "01000000892", PasswordHash = "test" } };
        var subject = new Subject { Name = "Recovery subject", NormalizedName = "RECOVERY_SUBJECT" };
        var exam = new Exam { Title = "120 question exam", TotalScore = 120, PassingScore = 60, CreatedByTeacher = teacher,
            IsRandomized = true, DisplayQuestionCount = 120, EssayGradingMode = essayCount > 0
                ? ExamEssayGradingMode.AutomaticFullOrHalfCreditFrom70Percent : ExamEssayGradingMode.Automatic };
        var attempt = new StudentExamAttempt { User = student, Exam = exam, StartedAt = DateTime.UtcNow };
        var answers = new List<AnswerSubmissionDto>();
        for (var order = 1; order <= 120; order++)
        {
            var isEssay = order > 120 - essayCount;
            QuestionBankItem question = isEssay ? new EssayQuestion() : new QuestionBankItem();
            question.Text = $"سؤال تجريبي {order}";
            question.Type = isEssay ? QuestionType.Essay : QuestionType.MCQ;
            question.Subject = subject;
            question.CreatedByTeacher = teacher;
            var correct = new QuestionOption { Question = question, Text = "Correct", IsCorrect = true };
            if (!isEssay) question.Options.Add(correct);
            var assigned = new ExamQuestion { Exam = exam, Question = question, Points = 1, Order = order };
            exam.ExamQuestions.Add(assigned);
            db.StudentAnswers.Add(new StudentAnswer { Attempt = attempt, ExamQuestion = assigned, HintUsed = order == 1 });
            answers.Add(new(assigned.Id, isEssay ? null : correct.Id, isEssay ? SyntheticEssayAnswer : null));
        }
        attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson();
        await db.SaveChangesAsync();
        return new(exam.Id, attempt.Id, student.Id, answers);
    }

    private sealed class UnavailableEffects : IPublisher, IJobEnqueuer
    {
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
            => throw new InvalidOperationException("Notification queue unavailable");
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Reward service unavailable");
        public Task Publish<T>(T notification, CancellationToken cancellationToken = default) where T : INotification
            => Publish((object)notification, cancellationToken);
    }
}

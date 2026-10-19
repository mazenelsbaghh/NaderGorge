using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.BackgroundServices;
using NaderGorge.API.Hubs;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Gamification.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Background;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Exams;

public sealed class ExamSubmissionConsumerCompatibilityTests
{
    // 2026-10-19: old consumers acknowledge unknown effect types, so the bridge must not emit them.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BridgeProducer_PreservesAcceptedAnswersAndEmitsOnlyLegacyEvents(bool includeEssay)
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var command = await SeedLegacyExamAsync(db, includeEssay);
        var effects = new ExternalEffects();
        var handler = new SubmitExamCommandHandler(db, effects, effects);

        var accepted = await handler.Handle(command, default);
        var duplicate = await handler.Handle(command with { Answers = [] }, default);

        Assert.True(accepted.Success, accepted.Message);
        Assert.False(duplicate.Success);
        Assert.Equal("This attempt has already been submitted.", duplicate.Message);
        Assert.Equal(includeEssay ? "Pending" : "Completed", accepted.Data!.ResultState);
        Assert.Equal(1m, accepted.Data.ScoreAchieved);
        Assert.Equal(!includeEssay, accepted.Data.IsPassed);
        db.ChangeTracker.Clear();
        Assert.Equal(includeEssay ? 2 : 1, await db.StudentAnswers.CountAsync(answer =>
            answer.SelectedOptionId != null || answer.SubmittedText != null));
        Assert.Equal(includeEssay ? 1 : 0, await db.EssaySubmissions.CountAsync());
        Assert.Equal(1, await db.OutboxEvents.CountAsync(e => e.Type == "ExamSubmitted"));
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.Type == ExamSubmissionEffects.ParentPushEventType
            || e.Type == ExamSubmissionEffects.AcademicRewardEventType));
        Assert.Single(effects.ParentJobs);
        Assert.Equal(includeEssay ? 0 : 1, effects.AcademicEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BridgeConsumer_HandlesFutureEffectAndRedeliveryWithoutDuplicatingItsIdentity(bool academicReward)
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var student = new User { FullName = "Bridge consumer student", PhoneNumber = "01000000981", PasswordHash = "test" };
        var effect = new OutboxEvent
        {
            Type = academicReward ? ExamSubmissionEffects.AcademicRewardEventType : ExamSubmissionEffects.ParentPushEventType,
            TargetUserId = academicReward ? null : student.Id.ToString(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        effect.PayloadJson = academicReward
            ? JsonSerializer.Serialize(new AcademicTaskCompletedEvent(student.Id, GamificationEventType.PerfectExam, 120, effect.Id))
            : JsonSerializer.Serialize(new ExamParentPushPayload(student.Id, "امتحان تجريبي"));
        db.AddRange(student, effect);
        await db.SaveChangesAsync();
        var effects = new ExternalEffects();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        services.AddScoped<IAppDbContext>(_ => fixture.CreateDbContext());
        services.AddSingleton<IJobEnqueuer>(effects);
        await using var provider = services.BuildServiceProvider();
        using var processor = new OutboxProcessorBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHubContext<PlatformHub>>(), NullLogger<OutboxProcessorBackgroundService>.Instance,
            new ConfigurationBuilder().Build());

        await DispatchOneAsync(processor);
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.OutboxEvents.SingleAsync(e => e.Id == effect.Id)).ProcessedAt);
        await db.OutboxEvents.Where(e => e.Id == effect.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(e => e.ProcessedAt, (DateTime?)null));
        await DispatchOneAsync(processor);

        db.ChangeTracker.Clear();
        Assert.NotNull((await db.OutboxEvents.SingleAsync(e => e.Id == effect.Id)).ProcessedAt);
        if (academicReward)
        {
            Assert.Equal(120, (await db.StudentGamifications.SingleAsync()).TotalPoints);
            Assert.Equal(effect.Id, (await db.GamificationActionLogs.SingleAsync()).Id);
            Assert.Empty(effects.ParentJobs);
        }
        else
        {
            Assert.Equal(2, effects.ParentJobs.Count);
            Assert.All(effects.ParentJobs, payload => Assert.Equal(effect.Id.ToString(),
                RedisJobEnqueuer.ResolveStableJobId("notifications", payload)));
            using var delivered = JsonDocument.Parse(effects.ParentJobs.Last());
            Assert.Equal(student.Id, delivered.RootElement.GetProperty("StudentId").GetGuid());
            Assert.Contains("امتحان تجريبي", delivered.RootElement.GetProperty("Body").GetString());
            Assert.Empty(await db.GamificationActionLogs.ToListAsync());
        }
    }

    private static async Task DispatchOneAsync(OutboxProcessorBackgroundService processor)
    {
        var dispatch = typeof(OutboxProcessorBackgroundService)
            .GetMethod("ProcessOutboxEventsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task<bool>)dispatch.Invoke(processor, [CancellationToken.None])!;
    }

    private static async Task<SubmitExamCommand> SeedLegacyExamAsync(AppDbContext db, bool includeEssay)
    {
        var student = new User { FullName = "Bridge producer student", PhoneNumber = "01000000991", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = new User { FullName = "Bridge teacher", PhoneNumber = "01000000992", PasswordHash = "test" } };
        var subject = new Subject { Name = "Bridge subject", NormalizedName = "BRIDGE_SUBJECT" };
        var question = new QuestionBankItem { Text = "Choose correct", Subject = subject, CreatedByTeacher = teacher };
        var correct = new QuestionOption { Question = question, Text = "Correct", IsCorrect = true };
        question.Options.Add(correct);
        var exam = new Exam { Title = "Bridge exam", TotalScore = includeEssay ? 2 : 1, PassingScore = 1, CreatedByTeacher = teacher };
        var mcq = new ExamQuestion { Exam = exam, Question = question, Points = 1, Order = 1 };
        exam.ExamQuestions.Add(mcq);
        var attempt = new StudentExamAttempt { User = student, Exam = exam, StartedAt = DateTime.UtcNow };
        db.StudentAnswers.Add(new StudentAnswer { Attempt = attempt, ExamQuestion = mcq });
        var answers = new List<AnswerSubmissionDto> { new(mcq.Id, correct.Id, null) };
        if (includeEssay)
        {
            var essay = new EssayQuestion { Text = "اشرح الفكرة", Type = QuestionType.Essay, Subject = subject, CreatedByTeacher = teacher };
            var assigned = new ExamQuestion { Exam = exam, Question = essay, Points = 1, Order = 2 };
            exam.ExamQuestions.Add(assigned);
            db.StudentAnswers.Add(new StudentAnswer { Attempt = attempt, ExamQuestion = assigned });
            answers.Add(new(assigned.Id, null, "إجابة عربية تجريبية"));
        }
        attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson();
        await db.SaveChangesAsync();
        return new(exam.Id, attempt.Id, student.Id, answers);
    }

    private sealed class ExternalEffects : IPublisher, IJobEnqueuer
    {
        public List<string> ParentJobs { get; } = [];
        public int AcademicEvents { get; private set; }
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            ParentJobs.Add(JsonSerializer.Serialize(data));
            return Task.CompletedTask;
        }
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            AcademicEvents++;
            return Task.CompletedTask;
        }
        public Task Publish<T>(T notification, CancellationToken cancellationToken = default) where T : INotification
            => Publish((object)notification, cancellationToken);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Integration.Tests.AdminAI;
using Npgsql;

namespace NaderGorge.Integration.Tests.Exams;

public sealed class SubmitExamProgressRaceTests
{
    // Production 2026-10-11: submission lost to a concurrent lesson_progress insert (23505).
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ConcurrentProgressInsert_SavesSubmissionOnceAndPreservesWinnerFlags(
        bool passed, bool completedByWinner)
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seed = fixture.CreateDbContext();
        await seed.Database.MigrateAsync();
        var scenario = await SeedAsync(seed, passed);
        var effects = new Effects();
        var race = new ProgressRace(async () =>
        {
            await using var winner = fixture.CreateDbContext();
            winner.LessonProgresses.Add(new LessonProgress
            {
                UserId = scenario.UserId, LessonId = scenario.LessonId,
                IsManuallyUnlocked = true, IsCompleted = completedByWinner
            });
            await winner.SaveChangesAsync();
        });
        await using var loser = CreateLoser(fixture, race);

        var result = await new SubmitExamCommandHandler(loser, effects, effects)
            .Handle(scenario.Command, default);

        Assert.True(result.Success, result.Message);
        Assert.Equal(passed, result.Data!.IsPassed);
        Assert.False(result.Data.BlocksNextLesson);
        await using var read = fixture.CreateDbContext();
        var progress = await read.LessonProgresses.SingleAsync();
        Assert.True(progress.IsManuallyUnlocked);
        Assert.Equal(passed || completedByWinner, progress.IsCompleted);
        var attempt = await read.StudentExamAttempts.SingleAsync();
        Assert.NotNull(attempt.Evaluation);
        Assert.Equal(result.Data.ScoreAchieved, attempt.ScoreAchieved);
        Assert.Equal(1, await read.OutboxEvents.CountAsync(e => e.Type == "ExamSubmitted"));
        Assert.Equal(1, effects.ParentJobs);
        Assert.Equal(passed ? 1 : 0, effects.AcademicEvents);
        Assert.Equal("IX_lesson_progress_UserId_LessonId", race.Conflict?.ConstraintName);
        Assert.Equal("23505", race.Conflict?.SqlState);
    }

    [Fact]
    public async Task ConcurrentSameAttempt_ReturnsPersistedResultWithoutRepeatingEffects()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seed = fixture.CreateDbContext();
        await seed.Database.MigrateAsync();
        var scenario = await SeedAsync(seed, passed: true);
        var effects = new Effects();
        var race = new ProgressRace(async () =>
        {
            await using var winner = fixture.CreateDbContext();
            var saved = await new SubmitExamCommandHandler(winner, effects, effects)
                .Handle(scenario.Command, default);
            Assert.True(saved.Success, saved.Message);
        });
        await using var loser = CreateLoser(fixture, race);

        var result = await new SubmitExamCommandHandler(loser, effects, effects)
            .Handle(scenario.Command, default);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Data!.IsPassed);
        Assert.Equal(2m, result.Data.ScoreAchieved);
        await using var read = fixture.CreateDbContext();
        Assert.Equal(1, await read.LessonProgresses.CountAsync());
        Assert.Equal(1, await read.OutboxEvents.CountAsync(e => e.Type == "ExamSubmitted"));
        Assert.Equal(1, effects.ParentJobs);
        Assert.Equal(1, effects.AcademicEvents);
        Assert.Equal("23505", race.Conflict?.SqlState);
    }

    private static AppDbContext CreateLoser(PostgresAdminAIFixture fixture, ProgressRace race) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(race).Options);

    private static async Task<Scenario> SeedAsync(AppDbContext db, bool passed)
    {
        var student = new User { FullName = "Race test student", PhoneNumber = "01000000671", PasswordHash = "test" };
        var teacherUser = new User { FullName = "Race test teacher", PhoneNumber = "01000000672", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "Race subject", NormalizedName = "RACE_SUBJECT" };
        var package = new Package { Name = "Race package", Description = "Test", Subject = subject, Teacher = teacher, TargetGrade = "SecondSecondary" };
        var term = new Term { Title = "Race term", Package = package };
        var section = new ContentSection { Title = "Race section", Term = term };
        var question = new QuestionBankItem { Text = "Choose correct", Type = QuestionType.MCQ, Subject = subject, CreatedByTeacher = teacher };
        var correct = new QuestionOption { Question = question, Text = "Correct", IsCorrect = true };
        var wrong = new QuestionOption { Question = question, Text = "Wrong", IsCorrect = false };
        question.Options.Add(correct);
        question.Options.Add(wrong);
        var exam = new Exam { Title = "Race exam", Description = "Test", TotalScore = 2, PassingScore = 1, CreatedByTeacher = teacher };
        var examQuestion = new ExamQuestion { Exam = exam, Question = question, Points = 2, Order = 1 };
        exam.ExamQuestions.Add(examQuestion);
        var lesson = new Lesson { Title = "Race lesson", ContentSection = section, ExamId = exam.Id };
        var attempt = new StudentExamAttempt
        {
            Exam = exam, User = student, StartedAt = DateTime.UtcNow,
            DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson()
        };
        var answer = new StudentAnswer { Attempt = attempt, ExamQuestion = examQuestion };
        db.AddRange(student, teacherUser, teacher, subject, package, term, section, question, correct, wrong,
            exam, examQuestion, lesson, attempt, answer);
        await db.SaveChangesAsync();
        return new(student.Id, lesson.Id, new(exam.Id, attempt.Id, student.Id,
            [new(examQuestion.Id, passed ? correct.Id : wrong.Id, null)]));
    }

    private sealed record Scenario(Guid UserId, Guid LessonId, SubmitExamCommand Command);

    // Coordinate two real PostgreSQL writers at the persistence boundary; no fake DB failure.
    private sealed class ProgressRace(Func<Task> commitWinner) : SaveChangesInterceptor
    {
        private bool _winnerCommitted;
        public PostgresException? Conflict { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_winnerCommitted && eventData.Context!.ChangeTracker.Entries<LessonProgress>()
                .Any(e => e.State == EntityState.Added))
            {
                _winnerCommitted = true;
                await commitWinner();
            }
            return result;
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Conflict = (eventData.Exception as DbUpdateException)?.InnerException as PostgresException;
            return Task.CompletedTask;
        }
    }

    private sealed class Effects : IPublisher, IJobEnqueuer
    {
        public int ParentJobs { get; private set; }
        public int AcademicEvents { get; private set; }
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data)
        {
            if (jobName == "parent-push") ParentJobs++;
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

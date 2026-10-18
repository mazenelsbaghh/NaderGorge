using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Integration.Tests.AdminAI;
using Npgsql;

namespace NaderGorge.Integration.Tests.Exams;

public sealed class ExamLongAnswerSubmissionTests
{
    // Production 2026-10-18: 22001 rolled back exam submissions with long essay answers.
    [Fact]
    public async Task LongEssay_SubmitsAfterSchemaUpgradeWithoutLosingAnyText()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.GetService<IMigrator>().MigrateAsync("20261018083933_AddExamAnswerReleaseVisibility");
        var student = new User { FullName = "Long answer student", PhoneNumber = "01000000881", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = new User { FullName = "Long answer teacher", PhoneNumber = "01000000882", PasswordHash = "test" } };
        var subject = new Subject { Name = "History", NormalizedName = "LONG_ANSWER_HISTORY" };
        var question = new QuestionBankItem { Text = "اشرح الإجابة بالتفصيل", Type = QuestionType.Essay, Subject = subject, CreatedByTeacher = teacher };
        var exam = new Exam { Title = "Long answer exam", TotalScore = 10, PassingScore = 5, CreatedByTeacher = teacher, EssayGradingMode = ExamEssayGradingMode.Manual };
        var assigned = new ExamQuestion { Exam = exam, Question = question, Points = 10, Order = 1 };
        exam.ExamQuestions.Add(assigned);
        var attempt = new StudentExamAttempt { User = student, Exam = exam, StartedAt = DateTime.UtcNow, DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson() };
        db.StudentAnswers.Add(new StudentAnswer { Attempt = attempt, ExamQuestion = assigned });
        await db.SaveChangesAsync();
        var fullAnswer = string.Concat(Enumerable.Repeat("شرح تاريخي مفصل دون حذف أي جزء من الإجابة. ", 100));
        var command = new SubmitExamCommand(exam.Id, attempt.Id, student.Id, [new(assigned.Id, null, fullAnswer)]);
        var effects = new ExternalEffects();

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() =>
            new SubmitExamCommandHandler(db, effects, effects).Handle(command, default));
        Assert.Equal("22001", Assert.IsType<PostgresException>(failure.InnerException).SqlState);
        db.ChangeTracker.Clear();
        Assert.Null((await db.StudentExamAttempts.SingleAsync()).SubmittedAt);
        Assert.Empty(await db.EssaySubmissions.ToListAsync());

        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var submitted = await new SubmitExamCommandHandler(db, effects, effects).Handle(command, default);

        Assert.True(submitted.Success, submitted.Message);
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.StudentExamAttempts.SingleAsync()).SubmittedAt);
        Assert.Equal(fullAnswer.Trim(), (await db.StudentAnswers.SingleAsync()).SubmittedText);
        Assert.Equal(fullAnswer.Trim(), (await db.EssaySubmissions.SingleAsync()).AnswerText);
        Assert.Equal(1, await db.OutboxEvents.CountAsync(e => e.Type == "ExamSubmitted"));
    }

    private sealed class ExternalEffects : IPublisher, IJobEnqueuer
    {
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data) => Task.CompletedTask;
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<T>(T notification, CancellationToken cancellationToken = default) where T : INotification => Task.CompletedTask;
    }
}

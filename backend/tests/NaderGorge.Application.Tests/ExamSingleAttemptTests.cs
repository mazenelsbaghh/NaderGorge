using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class ExamSingleAttemptTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SingleAttemptBlocksAnotherSubmissionUntilAnAuthorizedOperatorDeletesIt(bool publicExam, bool correct)
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, exam, right, wrong) = await SeedAsync(db, singleAttemptOnly: true, publicExam);
        var access = new AccessCheckService(db, new AcademicScopeService(db));
        var starter = new StartExamAttemptCommandHandler(db, access);
        var started = await starter.Handle(new(exam.Id, student.Id), default);
        Assert.True(started.Success, started.Message);
        var question = Assert.Single(started.Data!.Questions);
        var submitted = await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer())
            .Handle(new(exam.Id, started.Data.AttemptId, student.Id, [new(question.Id, correct ? right.Id : wrong.Id, null)]), default);
        Assert.True(submitted.Success, submitted.Message);
        Assert.False(submitted.Data!.AllowRetake);

        var blocked = await starter.Handle(new(exam.Id, student.Id), default);
        Assert.False(blocked.Success);
        Assert.Contains("EXAM_ATTEMPT_LIMIT_REACHED", blocked.Errors!);
        Assert.Single(await db.StudentExamAttempts.ToListAsync());
        var result = await new GetExamAttemptResultQueryHandler(db).Handle(new(started.Data.AttemptId, student.Id), default);
        Assert.True(result.Success, result.Message);
        Assert.False(result.Data!.AllowRetake);

        var removed = await new DeleteExamAttemptCommandHandler(db, new TeacherAuthorizationService(db))
            .Handle(new(exam.Id, started.Data.AttemptId, student.Id, "فتح محاولة بديلة بطلب الطالب"), default);
        Assert.True(removed.Success, removed.Message);
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "ExamAttemptDeleted");
        Assert.Equal(student.Id, audit.PerformedByUserId);
        Assert.Equal("فتح محاولة بديلة بطلب الطالب", audit.Reason);
        var replacement = await starter.Handle(new(exam.Id, student.Id), default);
        Assert.True(replacement.Success, replacement.Message);
        Assert.NotEqual(started.Data.AttemptId, replacement.Data!.AttemptId);
        Assert.Single(await db.StudentExamAttempts.ToListAsync());
    }

    [Fact]
    public async Task LeavingAnUnsubmittedExamResumesTheSameAttemptWithoutRestartingItsTimer()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, exam, _, _) = await SeedAsync(db, singleAttemptOnly: true, publicExam: false);
        var starter = new StartExamAttemptCommandHandler(db, new AccessCheckService(db, new AcademicScopeService(db)));
        var started = await starter.Handle(new(exam.Id, student.Id), default);
        Assert.True(started.Success, started.Message);

        var resumed = await starter.Handle(new(exam.Id, student.Id), default);

        Assert.True(resumed.Success, resumed.Message);
        Assert.Equal(started.Data!.AttemptId, resumed.Data!.AttemptId);
        Assert.Equal(started.Data.StartedAt, resumed.Data.StartedAt);
        Assert.Single(await db.StudentExamAttempts.ToListAsync());
    }

    [Fact]
    public async Task TurningOffSingleAttemptKeepsTheExistingFailedAttemptRetryBehavior()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, exam, _, _) = await SeedAsync(db, singleAttemptOnly: false, publicExam: false);
        db.StudentExamAttempts.Add(new StudentExamAttempt { ExamId = exam.Id, UserId = student.Id, Evaluation = "ضعيف", IsPassed = false });
        await db.SaveChangesAsync();

        var started = await new StartExamAttemptCommandHandler(db, new AccessCheckService(db, new AcademicScopeService(db)))
            .Handle(new(exam.Id, student.Id), default);

        Assert.True(started.Success, started.Message);
        Assert.Equal(2, await db.StudentExamAttempts.CountAsync());
    }

    [Fact]
    public async Task ExpiredSingleAttemptCannotBeReopenedByStartingAgain()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (student, exam, _, _) = await SeedAsync(db, singleAttemptOnly: true, publicExam: false);
        var starter = new StartExamAttemptCommandHandler(db, new AccessCheckService(db, new AcademicScopeService(db)));
        var started = await starter.Handle(new(exam.Id, student.Id), default);
        var attempt = await db.StudentExamAttempts.SingleAsync();
        attempt.StartedAt = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var expired = await starter.Handle(new(exam.Id, student.Id), default);
        var blocked = await starter.Handle(new(exam.Id, student.Id), default);

        Assert.False(expired.Success);
        Assert.False(blocked.Success);
        Assert.Contains("EXAM_ATTEMPT_LIMIT_REACHED", blocked.Errors!);
        Assert.Single(await db.StudentExamAttempts.ToListAsync());
    }

    private static async Task<(User Student, Exam Exam, QuestionOption Right, QuestionOption Wrong)> SeedAsync(
        AppDbContext db, bool singleAttemptOnly, bool publicExam)
    {
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Exam policy preview", "single-attempt");
        db.UserRoles.Add(new UserRole { User = student, Role = new Role { Name = "Admin", Type = RoleType.Admin } });
        var right = new QuestionOption { Text = "Correct", IsCorrect = true, Order = 1 };
        var wrong = new QuestionOption { Text = "Wrong", Order = 2 };
        var bank = new QuestionBankItem { Text = "Choose", Type = QuestionType.MCQ, Options = [right, wrong] };
        var exam = new Exam
        {
            Title = "Single attempt policy", TotalScore = 1, PassingScore = 1, DurationMinutes = 2,
            SingleAttemptOnly = singleAttemptOnly,
            ExamQuestions = [new ExamQuestion { Question = bank, Points = 1, Order = 1 }]
        };
        db.Exams.Add(exam);
        if (publicExam) db.PublicExamProducts.Add(new PublicExamProduct { Exam = exam, Slug = "one-attempt", IsPublished = true });
        await db.SaveChangesAsync();
        return (student, exam, right, wrong);
    }
}

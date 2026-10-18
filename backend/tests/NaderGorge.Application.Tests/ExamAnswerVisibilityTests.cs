using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class ExamAnswerVisibilityTests
{
    [Theory]
    [InlineData(0, false, "Pending")]
    [InlineData(1, false, "PartiallyIncluded")]
    [InlineData(2, false, "Included")]
    [InlineData(0, true, "Excluded")]
    public void ScoreDescriptionTracksEssayCoverageWithoutRevealingTheQuestions(int graded, bool excluded, string expected)
    {
        var exam = new Exam { TotalScore = 12, PassingScore = 6, ShowAnswersToStudents = false,
            EssayGradingMode = excluded ? ExamEssayGradingMode.ModelAnswerOnly : ExamEssayGradingMode.Automatic };
        var mcq = new ExamQuestion { Points = 2, Order = 1, Question = new QuestionBankItem { Type = QuestionType.MCQ, Text = "MCQ" } };
        exam.ExamQuestions.Add(mcq);
        for (var index = 0; index < 2; index++)
            exam.ExamQuestions.Add(new ExamQuestion { Points = 5, Order = index + 2,
                Question = new QuestionBankItem { Type = QuestionType.Essay, Text = $"Essay {index}" } });
        var snapshots = exam.ExamQuestions.ToDictionary(q => q.Id, q => new QuestionReviewSnapshot(
            "Saved answer", true, false, q == mcq ? 2 : q.Order - 2 < graded ? 3 : 0,
            IsPendingGrading: q != mcq && q.Order - 2 >= graded));
        var attempt = new StudentExamAttempt { ExamId = exam.Id, ScoreAchieved = 2 + 3 * graded,
            DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson() };
        var result = ExamResultBuilder.Build(exam, attempt, false, null, null, snapshots, true,
            excluded || graded == 2 ? "Completed" : "PartiallyGraded");
        Assert.Equal(expected, result.EssayScoreStatus);
        Assert.Empty(result.Questions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GradeOnlyHidesEveryReviewPathAndReleasingRevealsSavedAnswersWithoutRegrading(bool pending)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Answer preview", "71001");
        db.UserRoles.Add(new UserRole { User = student, Role = new Role { Name = "Admin", Type = RoleType.Admin } });
        var (exam, mcq, essay, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        exam.ShowAnswersToStudents = false;
        exam.ShowResultsWhileEssayPending = true;
        if (!pending) { db.ExamQuestions.Remove(essay); exam.ExamQuestions.Remove(essay); exam.TotalScore = 2; exam.PassingScore = 1; }
        await db.SaveChangesAsync();
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        List<AnswerSubmissionDto> answers = [new(mcq.Id, correct.Id, null)];
        if (pending) answers.Add(new(essay.Id, null, "Student explanation"));
        var submitted = await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer())
            .Handle(new(exam.Id, attempt.Id, student.Id, answers), default);
        Assert.True(submitted.Success, submitted.Message);
        Assert.False(submitted.Data!.AnswersVisible);
        Assert.Equal(pending ? "Pending" : "NotApplicable", submitted.Data.EssayScoreStatus);
        Assert.Empty(submitted.Data.Questions);
        Assert.Equal(2, submitted.Data.ScoreAchieved);
        if (pending) Assert.NotNull(submitted.Data.PartialResult);
        var signature = attempt.DefinitionSnapshotJson;
        var saved = await new GetExamAttemptResultQueryHandler(db).Handle(new(attempt.Id, student.Id), default);
        Assert.False(saved.Data!.AnswersVisible);
        Assert.Empty(saved.Data.Questions);
        var access = new AccessCheckService(db, new AcademicScopeService(db));
        var latest = await new GetLatestExamAttemptResultQueryHandler(db, access).Handle(new(exam.Id, student.Id), default);
        Assert.True(latest.Success, latest.Message);
        Assert.Empty(latest.Data!.Questions);
        if (!pending)
        {
            var passed = await new GetLatestPassedExamResultQueryHandler(db, access).Handle(new(exam.Id, student.Id), default);
            Assert.True(passed.Success, passed.Message);
            Assert.Empty(passed.Data!.Questions);
        }
        exam.ShowAnswersToStudents = true;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var released = await new GetExamAttemptResultQueryHandler(db).Handle(new(attempt.Id, student.Id), default);
        Assert.True(released.Data!.AnswersVisible);
        Assert.Equal(correct.Text, released.Data.Questions.Single(q => q.ExamQuestionId == mcq.Id).CorrectOptionText);
        Assert.Equal(submitted.Data.ScoreAchieved, released.Data.ScoreAchieved);
        Assert.Equal(signature, (await db.StudentExamAttempts.SingleAsync()).DefinitionSnapshotJson);
        var stranger = await new GetExamAttemptResultQueryHandler(db).Handle(new(attempt.Id, Guid.NewGuid()), default);
        Assert.False(stranger.Success);
    }

    [Fact]
    public async Task ReleaseNotifiesOnlySubmittersOnceAndAuditsVisibilityWithoutChangingTheAttempt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var actor = new User { FullName = "Exam owner", PhoneNumber = "71002", PasswordHash = "test" };
        var student = new User { FullName = "Submitter", PhoneNumber = "71003", PasswordHash = "test" };
        var draftUser = new User { FullName = "Unsubmitted", PhoneNumber = "71004", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = actor, Specialization = "History", ContactInfo = "test" };
        var exam = new Exam { CreatedByTeacher = teacher, Title = "Released exam", ShowAnswersToStudents = false, TotalScore = 10 };
        var attempt = new StudentExamAttempt { Exam = exam, User = student, ScoreAchieved = 7, Evaluation = "جيد", SubmittedAt = DateTime.UtcNow,
            DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson() };
        var draft = new StudentExamAttempt { Exam = exam, User = draftUser };
        db.AddRange(teacher, student, draftUser, exam, attempt, draft,
            new UserRole { User = actor, Role = new Role { Name = "Admin", Type = RoleType.Admin } });
        await db.SaveChangesAsync();
        var signature = attempt.DefinitionSnapshotJson;
        var handler = new SetExamAnswerVisibilityCommandHandler(db);
        var released = await handler.Handle(new(exam.Id, true, actor.Id), default);
        Assert.True(released.Success, released.Message);
        Assert.Equal(1, released.Data!.NotifiedStudents);
        var notification = await db.NotificationEvents.SingleAsync();
        Assert.Equal(student.Id, notification.UserId);
        Assert.Equal($"/student/exams/{exam.Id}?attemptId={attempt.Id}", notification.ActionUrl);
        Assert.Contains(exam.Title, notification.Body);
        Assert.Single(await db.OutboxEvents.Where(e => e.Type == "NotificationCreated" && e.TargetUserId == student.Id.ToString()).ToListAsync());
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.TargetUserId == draftUser.Id.ToString()));
        var repeated = await handler.Handle(new(exam.Id, true, actor.Id), default);
        Assert.Equal(0, repeated.Data!.NotifiedStudents);
        var hidden = await handler.Handle(new(exam.Id, false, actor.Id), default);
        Assert.True(hidden.Success, hidden.Message);
        var reopened = await handler.Handle(new(exam.Id, true, actor.Id), default);
        Assert.Equal(0, reopened.Data!.NotifiedStudents);
        Assert.Single(await db.NotificationEvents.ToListAsync());
        Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.Action == "ExamAnswerVisibilityChanged"));
        db.ChangeTracker.Clear();
        var retained = await db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id);
        Assert.Equal(7, retained.ScoreAchieved);
        Assert.Equal(signature, retained.DefinitionSnapshotJson);
        actor = await db.Users.SingleAsync(u => u.Id == actor.Id);
        actor.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False((await handler.Handle(new(exam.Id, false, actor.Id), default)).Success);
        Assert.True((await db.Exams.SingleAsync()).ShowAnswersToStudents);
    }
}

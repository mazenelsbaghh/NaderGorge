using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using HomeworkQuestionType = NaderGorge.Domain.Entities.Homework.QuestionType;
using ExamQuestionType = NaderGorge.Domain.Entities.QuestionType;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class AssessmentReviewPostgresTests
{
    [Fact]
    public async Task HomeworkReviewShowsAnswerAndManualGradePersistsScaledScoreAndAudit()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, seed.Submission.Id, seed.Teacher.UserId);
        var auth = new TeacherAuthorizationService(fixture.Db);
        var review = await new GetAssessmentReviewQueryHandler(fixture.Db, auth).Handle(new(target), default);
        Assert.Equal("إجابة الطالب المقالي", Assert.Single(review.Data!.Questions).Answer);
        Assert.True(review.Data.CanGrade);
        var graded = await new GradeAssessmentCommandHandler(fixture.Db, auth).Handle(new(target,
            [new(seed.Homework.Questions.Single().Id, 3)], "راجع السبب"), default);
        Assert.True(graded.Success);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync(s => s.Id == seed.Submission.Id);
        Assert.Equal(15m, saved.OverallScore);
        Assert.Equal(SubmissionStatus.Graded, saved.Status);
        Assert.Equal(3, Assert.Single(saved.Answers).ScoreReceived);
        Assert.Equal(seed.Teacher.UserId, saved.AssistantReviewerId);
        Assert.Equal("راجع السبب", saved.AssistantNotes);
        Assert.True(await fixture.Db.AuditLogs.AnyAsync(a => a.EntityId == saved.Id && a.Action == "AssessmentManuallyGraded"));
    }

    [Fact]
    public async Task ManualGradePersistsZeroForUnansweredHomeworkQuestion()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        fixture.Db.HomeworkAnswers.RemoveRange(await fixture.Db.HomeworkAnswers.Where(a => a.HomeworkSubmissionId == seed.Submission.Id).ToListAsync());
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, seed.Submission.Id, seed.Teacher.UserId);
        var result = await new GradeAssessmentCommandHandler(fixture.Db, new(fixture.Db))
            .Handle(new(target, [new(seed.Homework.Questions.Single().Id, 0)], null), default);
        Assert.True(result.Success);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.HomeworkAnswers.SingleAsync(a => a.HomeworkSubmissionId == seed.Submission.Id);
        Assert.Equal(0, saved.ScoreReceived);
        Assert.Equal("", saved.ProvidedAnswer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(1.5)]
    public async Task InvalidHomeworkGradeCannotPartiallyChangeAnswers(decimal score)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, seed.Submission.Id, seed.Teacher.UserId);
        var result = await new GradeAssessmentCommandHandler(fixture.Db, new(fixture.Db))
            .Handle(new(target, [new(seed.Homework.Questions.Single().Id, score)], null), default);
        Assert.False(result.Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Null((await fixture.Db.HomeworkAnswers.SingleAsync(a => a.HomeworkSubmissionId == seed.Submission.Id)).ScoreReceived);
    }

    [Fact]
    public async Task OtherTeacherCannotReviewGradeDeleteOrExportHomework()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var other = await Teacher(fixture.Db);
        var auth = new TeacherAuthorizationService(fixture.Db);
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, seed.Submission.Id, other.UserId);
        Assert.False((await new GetAssessmentReviewQueryHandler(fixture.Db, auth).Handle(new(target), default)).Success);
        Assert.False((await new GradeAssessmentCommandHandler(fixture.Db, auth).Handle(new(target, [new(seed.Homework.Questions.Single().Id, 4)], null), default)).Success);
        Assert.False((await new DeleteHomeworkAttemptCommandHandler(fixture.Db, auth).Handle(new(seed.Homework.Id, seed.Submission.Id, other.UserId), default)).Success);
        Assert.False((await new GetMissingHomeworkStudentsQueryHandler(fixture.Db, auth, new AccessCheckService(fixture.Db)).Handle(new(seed.Homework.Id, other.UserId), default)).Success);
        Assert.True(await fixture.Db.HomeworkSubmissions.AnyAsync(s => s.Id == seed.Submission.Id));
    }

    [Fact]
    public async Task MissingHomeworkExportIncludesStartedButNotSubmittedAndExcludesExpiredAccess()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var missing = await Student(fixture.Db);
        var expired = await Student(fixture.Db);
        fixture.Db.StudentAccessGrants.Add(new() { UserId = missing.Id, LessonId = seed.Homework.LessonId, GrantType = CodeType.Lesson, IsActive = true });
        fixture.Db.StudentAccessGrants.Add(new() { UserId = expired.Id, LessonId = seed.Homework.LessonId, GrantType = CodeType.Lesson, IsActive = true, ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        fixture.Db.HomeworkSubmissions.Add(new() { HomeworkId = seed.Homework.Id, StudentId = missing.Id, Status = SubmissionStatus.InProgress });
        await fixture.Db.SaveChangesAsync();
        var result = await new GetMissingHomeworkStudentsQueryHandler(fixture.Db, new(fixture.Db), new AccessCheckService(fixture.Db))
            .Handle(new(seed.Homework.Id, seed.Teacher.UserId), default);
        Assert.True(result.Success);
        Assert.Equal(missing.Id, Assert.Single(result.Data!.Students).StudentId);
        Assert.False(result.Data.HasMore);
    }

    [Fact]
    public async Task DeletingHomeworkAttemptRemovesOnlyItsAnswersAndRetainsAudit()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var other = await Student(fixture.Db);
        var retained = new HomeworkSubmission { HomeworkId = seed.Homework.Id, StudentId = other.Id };
        fixture.Db.HomeworkSubmissions.Add(retained);
        await fixture.Db.SaveChangesAsync();
        var result = await new DeleteHomeworkAttemptCommandHandler(fixture.Db, new(fixture.Db))
            .Handle(new(seed.Homework.Id, seed.Submission.Id, seed.Teacher.UserId), default);
        Assert.True(result.Success);
        Assert.False(await fixture.Db.HomeworkAnswers.AnyAsync(a => a.HomeworkSubmissionId == seed.Submission.Id));
        Assert.False(await fixture.Db.HomeworkSubmissions.AnyAsync(s => s.Id == seed.Submission.Id));
        Assert.True(await fixture.Db.HomeworkSubmissions.AnyAsync(s => s.Id == retained.Id));
        Assert.True(await fixture.Db.AuditLogs.AnyAsync(a => a.EntityId == seed.Submission.Id && a.Action == "HomeworkAttemptDeleted"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualEssayGradeWorksBeforeOrAfterAiWithoutDoubleCounting(bool aiFirst)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var essay = await Essay(fixture.Db, seed.Teacher, seed.Submission.StudentId);
        if (aiFirst) Assert.True((await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "AI"), default)).Success);
        var manual = await new GradeEssayCommandHandler(fixture.Db, new(fixture.Db)).Handle(new(essay.Id, 2, "manual", seed.Teacher.UserId), default);
        Assert.True(manual.Success);
        Assert.True((await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "late AI"), default)).Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(5m, (await fixture.Db.StudentExamAttempts.SingleAsync(a => a.Id == essay.StudentExamAttemptId)).ScoreAchieved);
        Assert.Equal("manual", (await fixture.Db.EssaySubmissions.SingleAsync(e => e.Id == essay.Id)).TeacherFeedback);
    }

    [Fact]
    public async Task ConcurrentAiAndManualGradingKeepsTeacherGradeAndDeleteMakesLateCallbackNoOp()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var essay = await Essay(fixture.Db, seed.Teacher, seed.Submission.StudentId);
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
        var teacher = new GradeEssayCommandHandler(fixture.Db, new(fixture.Db)).Handle(new(essay.Id, 1, "teacher wins", seed.Teacher.UserId), default);
        var ai = new WebhookEssayGradedCommandHandler(second).Handle(new(essay.Id, 1, "AI"), default);
        await Task.WhenAll(teacher, ai);
        Assert.True((await teacher).Success);
        Assert.True((await ai).Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(1m, (await fixture.Db.EssaySubmissions.SingleAsync(e => e.Id == essay.Id)).TeacherFinalScore);
        var examId = await fixture.Db.StudentExamAttempts.Where(a => a.Id == essay.StudentExamAttemptId).Select(a => a.ExamId).SingleAsync();
        Assert.True((await new DeleteExamAttemptCommandHandler(fixture.Db, new(fixture.Db)).Handle(new(examId, essay.StudentExamAttemptId, seed.Teacher.UserId), default)).Success);
        var late = await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "late"), default);
        Assert.Equal("Deleted", late.Data!.Status);
    }

    [Fact]
    public async Task ExamReviewAndManualGradeUseOnlyQuestionsAssignedToThisAttempt()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var essay = await Essay(fixture.Db, seed.Teacher, seed.Submission.StudentId);
        var attempt = await fixture.Db.StudentExamAttempts.Include(a => a.Exam).ThenInclude(e => e.ExamQuestions)
            .SingleAsync(a => a.Id == essay.StudentExamAttemptId);
        var assigned = attempt.Exam.ExamQuestions.Single().Id;
        fixture.Db.ExamQuestions.Add(new ExamQuestion
        {
            ExamId = attempt.ExamId, Points = 20, Order = 2, Question = new QuestionBankItem
            {
                Text = "Not assigned to student", CreatedByTeacherId = seed.Teacher.Id, SubjectId = essay.Question.SubjectId
            }
        });
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Exam, attempt.ExamId, attempt.Id, seed.Teacher.UserId);
        var auth = new TeacherAuthorizationService(fixture.Db);
        var review = await new GetAssessmentReviewQueryHandler(fixture.Db, auth).Handle(new(target), default);
        Assert.Equal(assigned, Assert.Single(review.Data!.Questions).QuestionId);
        Assert.Equal("شرح", review.Data.Questions[0].Answer);
        var result = await new GradeAssessmentCommandHandler(fixture.Db, auth).Handle(new(target, [new(assigned, 3)], "manual"), default);
        Assert.True(result.Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(7.5m, (await fixture.Db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id)).ScoreAchieved);
        Assert.True((await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "late"), default)).Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(7.5m, (await fixture.Db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id)).ScoreAchieved);
    }

    private static User User(string name) => new() { FullName = name, PasswordHash = "test-only", PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", IsActive = true };
    private static async Task<User> Student(AppDbContext db)
    {
        var student = User("طالب اختبار");
        student.UserRoles.Add(new UserRole { RoleId = await db.Roles.Where(r => r.Type == RoleType.Student).Select(r => r.Id).FirstAsync() });
        db.Users.Add(student); await db.SaveChangesAsync(); return student;
    }
    private static async Task<TeacherProfile> Teacher(AppDbContext db)
    {
        var user = User("مدرس اختبار");
        user.UserRoles.Add(new UserRole { RoleId = await db.Roles.Where(r => r.Type == RoleType.Teacher).Select(r => r.Id).FirstAsync() });
        var teacher = new TeacherProfile { User = user, IsContentVisibleToStudents = true };
        db.TeacherProfiles.Add(teacher); await db.SaveChangesAsync(); return teacher;
    }
    private static async Task<(Homework Homework, HomeworkSubmission Submission, TeacherProfile Teacher)> Seed(AppDbContext db)
    {
        var teacher = await Teacher(db);
        var student = await Student(db);
        var package = new Package { Name = "اختبار", TeacherId = teacher.Id, Subject = new Subject { Name = "اختبار", NormalizedName = Guid.NewGuid().ToString("N") } };
        var lesson = new Lesson { Title = "الحصة", ContentSection = new ContentSection { Title = "الشهر", Term = new Term { Title = "الترم", Package = package } } };
        db.Lessons.Add(lesson); await db.SaveChangesAsync();
        var homework = new Homework { LessonId = lesson.Id, Title = "الواجب", TotalScore = 20, PassingScoreThreshold = 10, IsActive = true };
        var question = new HomeworkQuestion { BodyText = "علل؟", QuestionType = HomeworkQuestionType.Essay, PointsActive = 4, Order = 1 };
        homework.Questions.Add(question);
        var submission = new HomeworkSubmission { Homework = homework, StudentId = student.Id, SubmittedAt = DateTime.UtcNow, Status = SubmissionStatus.PendingReview };
        submission.Answers.Add(new HomeworkAnswer { Question = question, ProvidedAnswer = "إجابة الطالب المقالي" });
        db.HomeworkSubmissions.Add(submission);
        db.StudentAccessGrants.Add(new() { UserId = student.Id, LessonId = lesson.Id, GrantType = CodeType.Lesson, IsActive = true });
        await db.SaveChangesAsync(); return (homework, submission, teacher);
    }
    private static async Task<EssaySubmission> Essay(AppDbContext db, TeacherProfile teacher, Guid studentId)
    {
        var question = new QuestionBankItem { Text = "علل؟", Type = ExamQuestionType.Essay, CreatedByTeacherId = teacher.Id, Subject = new Subject { Name = "Essay subject", NormalizedName = Guid.NewGuid().ToString("N") } };
        var exam = new Exam { Title = "امتحان", TotalScore = 10, PassingScore = 5, CreatedByTeacherId = teacher.Id };
        var examQuestion = new ExamQuestion { Question = question, Points = 4, Order = 1 };
        exam.ExamQuestions.Add(examQuestion);
        var attempt = new StudentExamAttempt { Exam = exam, UserId = studentId, Evaluation = "قيد التصحيح", StartedAt = DateTime.UtcNow };
        attempt.Answers.Add(new StudentAnswer { ExamQuestion = examQuestion, SubmittedText = "شرح" });
        var essay = new EssaySubmission { Attempt = attempt, StudentId = studentId, Question = question, AnswerText = "شرح", Status = EssaySubmissionStatus.WaitAI };
        db.EssaySubmissions.Add(essay); await db.SaveChangesAsync(); return essay;
    }
}

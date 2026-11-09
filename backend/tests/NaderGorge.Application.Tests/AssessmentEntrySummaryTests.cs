using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using HomeworkQuestionType = NaderGorge.Domain.Entities.Homework.QuestionType;

namespace NaderGorge.Application.Tests;

public sealed class AssessmentEntrySummaryTests
{
    [Theory]
    [InlineData(false, 1, 1)]
    [InlineData(true, 0, 1)]
    public async Task ExamSummaryHonorsTheQuestionLimitWithoutStartingAnAttempt(
        bool randomized, int essayMinimum, int essayMaximum)
    {
        await using var db = TestAppDbContextFactory.Create();
        var userId = await SeedAuthorizedUser(db);
        var exam = new Exam
        {
            Title = "Entry summary exam",
            IsRandomized = randomized,
            DisplayQuestionCount = 2,
            ExamQuestions =
            [
                new() { Order = 1, Question = new() { Text = "Written", Type = QuestionType.Essay } },
                new() { Order = 2, Question = new() { Text = "Choice A", Type = QuestionType.MCQ } },
                new() { Order = 3, Question = new() { Text = "Choice B", Type = QuestionType.MCQ } },
                new() { Order = 4, IsRetired = true, Question = new() { Text = "Retired", Type = QuestionType.Essay } },
            ]
        };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new(exam.Id, userId, AssessmentEntryKind.Exam), default);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new AssessmentEntrySummary(2,
            new(essayMinimum, essayMaximum), new(2 - essayMaximum, 2 - essayMinimum), new(0, 0)), result.Data);
        Assert.False(await db.StudentExamAttempts.AnyAsync());
    }

    [Fact]
    public async Task HomeworkSummaryCountsActiveTypesWithoutCreatingASubmission()
    {
        await using var db = TestAppDbContextFactory.Create();
        var userId = await SeedAuthorizedUser(db);
        var homework = new HomeworkEntity
        {
            Title = "Entry summary homework",
            LessonId = Guid.NewGuid(),
            Questions =
            [
                new() { QuestionType = HomeworkQuestionType.Essay, BodyText = "Written" },
                new() { QuestionType = HomeworkQuestionType.MCQ, BodyText = "Choice" },
                new() { QuestionType = HomeworkQuestionType.FindTheMistake, BodyText = "Mistake" },
                new() { QuestionType = HomeworkQuestionType.Essay, BodyText = "Retired", IsRetired = true },
            ]
        };
        db.Homeworks.Add(homework);
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new(homework.Id, userId, AssessmentEntryKind.Homework), default);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new AssessmentEntrySummary(3, new(1, 1), new(1, 1), new(1, 1)), result.Data);
        Assert.False(await db.HomeworkSubmissions.AnyAsync());
    }

    [Theory]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public async Task ConfiguredRandomExamKeepsAllEssaysAndDrawsTheExactScoredChoiceCount(int totalScore, bool valid)
    {
        await using var db = TestAppDbContextFactory.Create();
        var userId = await SeedAuthorizedUser(db);
        var exam = new Exam
        {
            Title = "Fixed question mix",
            IsRandomized = true,
            DisplayQuestionCount = 50,
            TotalScore = totalScore,
            ExamQuestions = Enumerable.Range(1, 75).Select(order => new ExamQuestion
            {
                Order = order,
                Points = order <= 55 ? 2 : 1,
                Question = new QuestionBankItem
                {
                    Text = $"Question {order}",
                    Type = order <= 55 ? QuestionType.MCQ : QuestionType.Essay
                }
            }).ToList()
        };
        db.Exams.Add(exam);
        db.PlatformSettings.Add(new PlatformSetting { Key = ExamQuestionDraw.MultipleChoiceCountKey(exam.Id), Value = "30" });
        await db.SaveChangesAsync();

        var summary = await Handler(db).Handle(new(exam.Id, userId, AssessmentEntryKind.Exam), default);
        var starter = new StartExamAttemptCommandHandler(db, new AccessCheckService(db, new AcademicScopeService(db)));
        var started = await starter.Handle(new(exam.Id, userId), default);
        Assert.Equal(valid, summary.Success);
        Assert.Equal(valid, started.Success);
        if (!valid)
        {
            Assert.False(await db.StudentExamAttempts.AnyAsync());
            return;
        }

        Assert.Equal(new AssessmentEntrySummary(50, new(20, 20), new(30, 30), new(0, 0)), summary.Data);
        var attempt = Assert.IsType<ActiveExamAttemptDto>(started.Data);
        Assert.Equal(80, attempt.TotalScore);
        Assert.All(attempt.Questions.Take(30), q => { Assert.Equal("MCQ", q.Type); Assert.Equal(2, q.Points); });
        var essays = attempt.Questions.Skip(30).ToArray();
        Assert.All(essays, q => { Assert.Equal("Essay", q.Type); Assert.Equal(1, q.Points); });
        Assert.Equal(exam.ExamQuestions.Where(q => q.Question.Type == QuestionType.Essay)
            .OrderBy(q => q.Order).Select(q => q.Id), essays.Select(q => q.Id));
        Assert.Equal(50, attempt.Questions.Select(q => q.Id).Distinct().Count());

        var resumed = await starter.Handle(new(exam.Id, userId), default);
        Assert.Equal(attempt.AttemptId, resumed.Data!.AttemptId);
        Assert.Equal(attempt.Questions.Select(q => q.Id).Order(), resumed.Data.Questions.Select(q => q.Id).Order());

        var swapper = new SwapQuestionCommandHandler(db);
        var essaySwap = await swapper.Handle(new(exam.Id, attempt.AttemptId, essays[0].Id, userId), default);
        Assert.False(essaySwap.Success);
        var choiceSwap = await swapper.Handle(new(exam.Id, attempt.AttemptId, attempt.Questions[0].Id, userId), default);
        Assert.True(choiceSwap.Success, choiceSwap.Message);
        Assert.Equal("MCQ", choiceSwap.Data!.Question.Type);
        Assert.Equal(2, choiceSwap.Data.Question.Points);
        Assert.Equal(80, choiceSwap.Data.TotalScore);
    }

    private static async Task<Guid> SeedAuthorizedUser(AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Summary test", "20000000002");
        db.UserRoles.Add(new UserRole { User = user, Role = new Role { Name = "Admin", Type = RoleType.Admin } });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static GetAssessmentEntrySummaryQueryHandler Handler(AppDbContext db) =>
        new(db, new AccessCheckService(db, new AcademicScopeService(db)), new ContentArchiveAccessService(db));
}

using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
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

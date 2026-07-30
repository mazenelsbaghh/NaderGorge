using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class GetLatestPassedExamResultQueryTests
{
    [Fact]
    public async Task Handle_IgnoresFailedAttempts()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "501");
        var (exam, _, _, _, _) = await TestAppDbContextFactory.SeedFindTheMistakeExamAsync(db);
        var failedAttempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        failedAttempt.Evaluation = "ضعيف";
        failedAttempt.IsPassed = false;
        failedAttempt.ScoreAchieved = 0;
        await db.SaveChangesAsync();

        var handler = new GetLatestPassedExamResultQueryHandler(db, AllowAccess.Instance);
        var result = await handler.Handle(new GetLatestPassedExamResultQuery(exam.Id, student.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Handle_ReturnsLatestPassedAttempt()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "502");
        var (exam, _, _, _, _) = await TestAppDbContextFactory.SeedFindTheMistakeExamAsync(db);
        var failedAttempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        failedAttempt.Evaluation = "ضعيف";
        failedAttempt.IsPassed = false;

        var passedAttempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        passedAttempt.Evaluation = "ممتاز";
        passedAttempt.IsPassed = true;
        passedAttempt.ScoreAchieved = exam.TotalScore;
        await db.SaveChangesAsync();

        var handler = new GetLatestPassedExamResultQueryHandler(db, AllowAccess.Instance);
        var result = await handler.Handle(new GetLatestPassedExamResultQuery(exam.Id, student.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(passedAttempt.Id, result.Data!.AttemptId);
        Assert.True(result.Data.IsPassed);
    }

    [Fact]
    public async Task LatestResult_ReturnsMostRecentSubmittedAttemptEvenBeforeItPasses()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "503");
        var (exam, _, _, _, _) = await TestAppDbContextFactory.SeedFindTheMistakeExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        attempt.Evaluation = "قيد التصحيح";
        attempt.IsPassed = false;
        await db.SaveChangesAsync();

        var handler = new GetLatestExamAttemptResultQueryHandler(db, AllowAccess.Instance);
        var result = await handler.Handle(new GetLatestExamAttemptResultQuery(exam.Id, student.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(attempt.Id, result.Data!.AttemptId);
        Assert.False(result.Data.IsPassed);
    }

    private sealed class AllowAccess : IAccessCheckService
    {
        public static readonly AllowAccess Instance = new();
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default) => Task.FromResult(true);
    }
}

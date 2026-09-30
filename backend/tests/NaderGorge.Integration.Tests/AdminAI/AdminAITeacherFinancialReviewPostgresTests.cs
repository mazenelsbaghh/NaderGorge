using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Finance.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAITeacherFinancialReviewPostgresTests
{
    [Fact]
    public async Task ConcurrentReviews_CreditOnlyOneTeacherShare()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var seed = fixture.CreateDbContext();
        await seed.Database.MigrateAsync();
        var actor = new User { FullName = "Concurrent admin", PhoneNumber = "01000007173", PasswordHash = "test" };
        var teacher = new TeacherProfile
        {
            User = new User { FullName = "Concurrent teacher", PhoneNumber = "01000007174", PasswordHash = "test" }
        };
        var allocation = new TeacherFinancialAllocation
        {
            Teacher = teacher,
            TeacherShareAmount = 25m,
            ReviewStatus = TeacherFinancialReviewStatus.PendingReview,
            TeacherFinancialEvent = new TeacherFinancialEvent
            {
                SourceType = TeacherFinancialSourceType.ManualCompensation,
                SourceId = Guid.NewGuid(),
                TargetType = SalesTargetType.Teacher,
                TargetId = teacher.Id,
                PaidAmount = 25m,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                ReviewStatus = TeacherFinancialReviewStatus.PendingReview
            }
        };
        seed.AddRange(actor, allocation);
        await seed.SaveChangesAsync();

        async Task<bool> ReviewAsync()
        {
            await using var db = fixture.CreateDbContext();
            var result = await new ReviewTeacherFinancialAllocationCommandHandler(db).Handle(
                new ReviewTeacherFinancialAllocationCommand(allocation.Id,
                    TeacherFinancialReviewStatus.Approved, actor.Id, null, Guid.NewGuid().ToString("N")), default);
            if (!result.Success)
                Assert.True(result.Errors?.Any(error => error is "CONCURRENT_REVIEW" or "ALREADY_REVIEWED"));
            return result.Success;
        }

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(ReviewAsync)));
        Assert.Equal(1, outcomes.Count(success => success));
        await using var verify = fixture.CreateDbContext();
        var account = await verify.TeacherAccounts.AsNoTracking().SingleAsync(item => item.TeacherId == teacher.Id);
        Assert.Equal(25m, account.TotalEarnings);
        Assert.Equal(25m, account.CurrentBalance);
        Assert.Equal(1, await verify.TeacherFinancialAllocations.AsNoTracking()
            .CountAsync(item => item.Id == allocation.Id && item.ReviewStatus == TeacherFinancialReviewStatus.Approved));
    }

    [Fact]
    public async Task ReviewedAllocation_ReplaysDurably_WithoutCreditingTeacherTwice()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var actor = new User { FullName = "Finance admin", PhoneNumber = "01000007171", PasswordHash = "test" };
        var teacher = new TeacherProfile
        {
            User = new User { FullName = "Finance teacher", PhoneNumber = "01000007172", PasswordHash = "test" }
        };
        var financialEvent = new TeacherFinancialEvent
        {
            SourceType = TeacherFinancialSourceType.ManualCompensation,
            SourceId = Guid.NewGuid(),
            TargetType = SalesTargetType.Teacher,
            TargetId = teacher.Id,
            PaidAmount = 100m,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            ReviewStatus = TeacherFinancialReviewStatus.PendingReview
        };
        var first = new TeacherFinancialAllocation
        {
            Teacher = teacher, TeacherFinancialEvent = financialEvent,
            TeacherShareAmount = 25m, ReviewStatus = TeacherFinancialReviewStatus.PendingReview
        };
        var second = new TeacherFinancialAllocation
        {
            Teacher = teacher, TeacherFinancialEvent = financialEvent,
            TeacherShareAmount = 15m, ReviewStatus = TeacherFinancialReviewStatus.PendingReview
        };
        db.AddRange(actor, teacher, financialEvent, first, second,
            new TeacherAccount { Teacher = teacher, TotalEarnings = 10m, CurrentBalance = 10m });
        await db.SaveChangesAsync();

        var operationId = Guid.NewGuid().ToString("N");
        var command = new ReviewTeacherFinancialAllocationCommand(
            first.Id, TeacherFinancialReviewStatus.Approved, actor.Id, "Reviewed", operationId);
        Assert.True((await new ReviewTeacherFinancialAllocationCommandHandler(db).Handle(command, default)).Success);

        await using var replayDb = fixture.CreateDbContext();
        var replayHandler = new ReviewTeacherFinancialAllocationCommandHandler(replayDb);
        Assert.True((await replayHandler.Handle(command, default)).Success);
        var conflict = await replayHandler.Handle(command with { Status = TeacherFinancialReviewStatus.Rejected }, default);
        Assert.False(conflict.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflict.Errors!);
        var secondOperation = await replayHandler.Handle(command with { OperationId = Guid.NewGuid().ToString("N") }, default);
        Assert.False(secondOperation.Success);
        Assert.Contains("ALREADY_REVIEWED", secondOperation.Errors!);

        var reject = await replayHandler.Handle(new ReviewTeacherFinancialAllocationCommand(
            second.Id, TeacherFinancialReviewStatus.Rejected, actor.Id, "Rejected", Guid.NewGuid().ToString("N")), default);
        Assert.True(reject.Success);

        await using var verifyDb = fixture.CreateDbContext();
        var account = await verifyDb.TeacherAccounts.AsNoTracking().SingleAsync(item => item.TeacherId == teacher.Id);
        Assert.Equal(35m, account.TotalEarnings);
        Assert.Equal(35m, account.CurrentBalance);
        var approved = await verifyDb.TeacherFinancialAllocations.AsNoTracking().SingleAsync(item => item.Id == first.Id);
        Assert.Equal(operationId, approved.ReviewOperationId);
        Assert.Equal(actor.Id, approved.ReviewActorUserId);
        Assert.Equal("Reviewed", approved.ReviewNote);
        Assert.Equal(TeacherFinancialPayoutStatus.Unpaid, approved.PayoutStatus);
        var rejected = await verifyDb.TeacherFinancialAllocations.AsNoTracking().SingleAsync(item => item.Id == second.Id);
        Assert.Equal(TeacherFinancialPayoutStatus.NotEligible, rejected.PayoutStatus);
        Assert.Equal(TeacherFinancialReviewStatus.Approved,
            (await verifyDb.TeacherFinancialEvents.AsNoTracking().SingleAsync(item => item.Id == financialEvent.Id)).ReviewStatus);
    }
}

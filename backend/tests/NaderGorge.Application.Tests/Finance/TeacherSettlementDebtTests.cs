using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherSettlementDebtTests
{
    [Fact]
    public async Task Debt_larger_than_earnings_is_partially_deducted_without_losing_remaining_debt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var teacher = new TeacherProfile { User = new User { FullName = "Teacher", PhoneNumber = "01055555555", PasswordHash = "test" } };
        var earning = new TeacherFinancialEvent { IdempotencyKey = Guid.NewGuid().ToString(), OccurredAt = DateTime.UtcNow };
        var allocation = new TeacherFinancialAllocation { Teacher = teacher, TeacherFinancialEvent = earning,
            TeacherShareAmount = 100m, ReviewStatus = TeacherFinancialReviewStatus.Approved,
            PayoutStatus = TeacherFinancialPayoutStatus.Unpaid };
        var account = new TeacherAccount { Teacher = teacher, CurrentBalance = 100m, TotalEarnings = 100m };
        var debt = new TeacherPayoutAdjustment { Teacher = teacher, Amount = -150m,
            RelatedFinancialEvent = earning, Status = TeacherPayoutAdjustmentStatus.Open, Reason = "previous refund" };
        db.AddRange(allocation, account, debt);
        await db.SaveChangesAsync();
        var service = new TeacherSettlementAuthorityService(db);
        var input = new SettlementCreationInput(teacher.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null, [allocation.Id]);

        var preview = await service.PreviewAsync(input, CancellationToken.None);
        Assert.Equal(100m, preview.DebtDeductionAmount);
        Assert.Equal(0m, preview.NetPayableAmount);
        Assert.Equal(-100m, Assert.Single(preview.Adjustments).Amount);
        var created = await service.CreateAsync(teacher.UserId, input, CancellationToken.None);
        Assert.Equal(TeacherFinanceCommandStatus.Success, created.Status);
        var settlement = await db.TeacherSettlements.Include(x => x.Lines).SingleAsync();
        Assert.Equal(100m, settlement.DebtDeductionAmount);
        Assert.Equal(-100m, settlement.Lines.Single(x => x.AdjustmentId.HasValue).Amount);
        await service.TransitionAsync(teacher.UserId, settlement.Id, TeacherSettlementStatus.Draft, TeacherSettlementStatus.Reviewed, CancellationToken.None);
        await service.TransitionAsync(teacher.UserId, settlement.Id, TeacherSettlementStatus.Reviewed, TeacherSettlementStatus.Approved, CancellationToken.None);
        var paid = await service.PayAsync(teacher.UserId, settlement.Id, new("Debt offset", "test-offset", null, 0m), CancellationToken.None);
        Assert.Equal(TeacherFinanceCommandStatus.Success, paid.Status);
        Assert.Equal(0m, account.CurrentBalance);
        Assert.Equal(0m, account.ReservedBalance);
        Assert.Equal(TeacherPayoutAdjustmentStatus.Applied, debt.Status);
        Assert.Equal(-50m, (await db.TeacherPayoutAdjustments.SingleAsync(x => x.Status == TeacherPayoutAdjustmentStatus.Open)).Amount);
        Assert.Equal(0m, (await db.TeacherSettlementPayments.SingleAsync()).Amount);
        Assert.Equal(TeacherFinanceCommandStatus.Conflict,
            (await service.PayAsync(teacher.UserId, settlement.Id, new("Debt offset", "test-offset", null, 0m), CancellationToken.None)).Status);
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Finance.Commands;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Application.Features.Teacher.Finance.Commands;
using NaderGorge.Application.Features.Teacher.Finance.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Application.Tests.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherFinanceAccountTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly TeacherProfile teacher = new() { User = new User { FullName = "Account teacher", PhoneNumber = "01098765432", PasswordHash = "test" } };

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Add(teacher); await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    [Fact]
    public async Task Account_and_teacher_screen_use_signed_recognized_sources_and_actual_cash_payments()
    {
        db.Add(new TeacherAccount { Teacher = teacher, TotalEarnings = 180m, CurrentBalance = 80m, ReservedBalance = 30m });
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 120m);
        AddIncome(TeacherFinancialSourceType.PublicExamPurchase, 60m);
        AddIncome(TeacherFinancialSourceType.Refund, -20m, TeacherFinancialReviewStatus.Reversed, TeacherFinancialPayoutStatus.Debt);
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 900m, TeacherFinancialReviewStatus.PendingReview);
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 700m, TeacherFinancialReviewStatus.Rejected);
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, Amount = -20m, Status = TeacherPayoutAdjustmentStatus.Open });
        db.Add(new TeacherPayout { Teacher = teacher, Amount = 60m, Status = PayoutStatus.Paid });
        db.Add(new TeacherPayout { Teacher = teacher, Amount = 30m, Status = PayoutStatus.Pending });
        db.Add(new TeacherSettlement { TeacherId = teacher.Id, CreatedByUserId = teacher.UserId, Status = TeacherSettlementStatus.Paid,
            Payments = [new TeacherSettlementPayment { Amount = 40m, PaidByUserId = teacher.UserId }] });
        await db.SaveChangesAsync();

        var account = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(160m, account.TotalEarned);
        Assert.Equal(100m, account.Paid);
        Assert.Equal(60m, account.NetBalance);
        Assert.Equal(30m, account.NetPayable);
        Assert.Equal(160m, account.SourceEarnings);
        Assert.Equal(0m, account.SourceDifference);
        Assert.Equal(0m, account.BalanceDifference);
        Assert.Equal(3, account.Sources.Count);
        var self = await new GetTeacherAccountQueryHandler(db).Handle(new(teacher.UserId), default);
        Assert.Equal(account.NetPayable, self.Data!.AvailableBalance);
        Assert.Equal(account.TotalEarned, self.Data.TotalEarnings);
        Assert.Equal(account.Paid, self.Data.Account!.Paid);
        var day = CairoTime.ToLocal(DateTime.UtcNow).Date;
        var calendar = await new GetTeacherFinanceCalendarQueryHandler(db).Handle(new(teacher.UserId, day, day), default);
        Assert.Equal(160m, Assert.Single(calendar.Data!).TeacherShareAmount);
        Assert.Equal(1, calendar.Data![0].PendingReviewCount);
    }

    [Fact]
    public async Task Reserved_debt_is_deducted_once_and_request_cannot_exceed_the_same_available_amount()
    {
        db.Add(new TeacherAccount { Teacher = teacher, TotalEarnings = 200m, CurrentBalance = 200m, ReservedBalance = 100m });
        var debt = new TeacherPayoutAdjustment { Teacher = teacher, Amount = -150m };
        db.Add(new TeacherSettlement { TeacherId = teacher.Id, CreatedByUserId = teacher.UserId, Status = TeacherSettlementStatus.Draft,
            GrossDueAmount = 100m, DebtDeductionAmount = 100m,
            Lines = [new TeacherSettlementLine { Adjustment = debt, Amount = -100m }] });
        await db.SaveChangesAsync();
        var before = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(50m, before.NetPayable);
        Assert.Equal(50m, before.UnreservedDebt);
        var handler = new RequestPayoutCommandHandler(db, new TestAuditRepository());
        Assert.False((await handler.Handle(new(teacher.UserId, 50.01m), default)).Success);
        Assert.False((await handler.Handle(new(teacher.UserId, 0.001m), default)).Success);
        Assert.Empty(await db.TeacherPayouts.ToListAsync());
        var requested = await handler.Handle(new(teacher.UserId, 50m), default);
        Assert.True(requested.Success, requested.Message);
        Assert.Equal(0m, requested.Data!.AvailableBalance);
        var after = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(0m, after.NetPayable);
        Assert.Equal(150m, after.Reserved);
    }

    [Fact]
    public async Task New_debt_after_reservation_blocks_payment_and_rejection_still_releases_money()
    {
        var account = new TeacherAccount { Teacher = teacher, CurrentBalance = 100m, TotalEarnings = 100m, ReservedBalance = 100m };
        var payout = new TeacherPayout { Teacher = teacher, Amount = 100m, Status = PayoutStatus.Approved };
        db.AddRange(account, payout, new TeacherPayoutAdjustment { Teacher = teacher, Amount = -20m });
        await db.SaveChangesAsync();
        var handler = new ResolvePayoutCommandHandler(db, new TestAuditRepository(), new FinancialPostingService(db));
        Assert.False((await handler.Handle(new(payout.Id, PayoutStatus.Paid, null, teacher.UserId), default)).Success);
        Assert.Equal(PayoutStatus.Approved, payout.Status);
        Assert.Equal(100m, account.CurrentBalance);
        Assert.Empty(await db.JournalEntries.ToListAsync());
        Assert.True((await handler.Handle(new(payout.Id, PayoutStatus.Rejected, "recalculate after refund", teacher.UserId), default)).Success);
        Assert.Equal(80m, (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!.NetPayable);
    }

    [Fact]
    public async Task Historical_balance_without_sources_is_flagged_without_recalculating_at_current_rate()
    {
        teacher.CommissionRate = 95m;
        db.Add(new TeacherAccount { Teacher = teacher, CurrentBalance = 80m, TotalEarnings = 100m });
        await db.SaveChangesAsync();
        var snapshot = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(100m, snapshot.TotalEarned);
        Assert.Equal(100m, snapshot.SourceDifference);
        Assert.Equal(-20m, snapshot.BalanceDifference);
        Assert.Empty(snapshot.Sources);
    }

    [Fact]
    public async Task Retrying_a_failed_reservation_creates_only_one_payout_and_one_reservation()
    {
        db.Add(new TeacherAccount { Teacher = teacher, CurrentBalance = 100m, TotalEarnings = 100m });
        await db.SaveChangesAsync();
        var interceptor = new FailFirstPayoutSave();
        await using var retryDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(interceptor).Options);
        var result = await new RequestPayoutCommandHandler(retryDb, new TestAuditRepository()).Handle(new(teacher.UserId, 75m), default);
        Assert.True(result.Success, result.Message);
        Assert.True(interceptor.Failed);
        Assert.Equal(75m, (await retryDb.TeacherPayouts.SingleAsync()).Amount);
        Assert.Equal(75m, (await retryDb.TeacherAccounts.SingleAsync()).ReservedBalance);
        Assert.Equal(25m, result.Data!.AvailableBalance);
    }

    [Fact]
    public async Task Second_settlement_cannot_skip_debt_partially_reserved_in_the_first()
    {
        db.Add(new TeacherAccount { Teacher = teacher, CurrentBalance = 200m, TotalEarnings = 200m });
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 100m);
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 100m);
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, Amount = -150m });
        await db.SaveChangesAsync();
        var allocations = await db.TeacherFinancialAllocations.OrderBy(x => x.Id).ToListAsync();
        var service = new TeacherSettlementAuthorityService(db, new FinancialPostingService(db));
        var first = new SettlementCreationInput(teacher.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null, [allocations[0].Id]);
        var second = first with { AllocationIds = [allocations[1].Id] };
        var created = await service.CreateAsync(teacher.UserId, first, default);
        Assert.Equal(TeacherFinanceCommandStatus.Success, created.Status);
        Assert.Equal(TeacherFinanceCommandStatus.Conflict, (await service.CreateAsync(teacher.UserId, second, default)).Status);
        var settlement = await db.TeacherSettlements.SingleAsync();
        Assert.Equal(0m, settlement.NetPayableAmount);
        await service.TransitionAsync(teacher.UserId, settlement.Id, TeacherSettlementStatus.Draft, TeacherSettlementStatus.Reviewed, default);
        await service.TransitionAsync(teacher.UserId, settlement.Id, TeacherSettlementStatus.Reviewed, TeacherSettlementStatus.Approved, default);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await service.PayAsync(teacher.UserId, settlement.Id, new("Debt offset", "first", null, 0m), default)).Status);
        var preview = await service.PreviewAsync(second, default);
        Assert.Equal(50m, preview.DebtDeductionAmount);
        Assert.Equal(50m, preview.NetPayableAmount);
        Assert.Equal(50m, (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!.NetPayable);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await service.CreateAsync(teacher.UserId, second, default)).Status);
    }

    private void AddIncome(TeacherFinancialSourceType source, decimal share,
        TeacherFinancialReviewStatus review = TeacherFinancialReviewStatus.AutoApproved,
        TeacherFinancialPayoutStatus payout = TeacherFinancialPayoutStatus.Unpaid)
    {
        db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = share,
            GrossBasisAmount = share, ReviewStatus = review, PayoutStatus = payout,
            TeacherFinancialEvent = new TeacherFinancialEvent { SourceType = source, IdempotencyKey = Guid.NewGuid().ToString(), OccurredAt = DateTime.UtcNow } });
    }

    private sealed class FailFirstPayoutSave : SaveChangesInterceptor
    {
        public bool Failed { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Failed && eventData.Context!.ChangeTracker.Entries<TeacherPayout>().Any(x => x.State == EntityState.Added))
            {
                Failed = true;
                throw new InvalidOperationException("could not serialize access due to concurrent update");
            }
            return ValueTask.FromResult(result);
        }
    }
}

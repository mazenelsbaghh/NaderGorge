using Microsoft.EntityFrameworkCore;
using System.Data;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed record TeacherWithdrawalBalance(decimal Balance, decimal Reserved, decimal Debt, decimal DebtReserved)
{
    public decimal UnreservedDebt => Math.Max(0m, Debt - DebtReserved);
    public decimal NetBalance => Balance - Debt;
    public decimal UnreservedBalance => Balance - Reserved;
    public decimal WithdrawalAvailable => Math.Max(0m, UnreservedBalance - UnreservedDebt);
    public bool CoversReservedPayment => UnreservedBalance >= UnreservedDebt;
}

public sealed record TeacherIncomeSource(string SourceType, int Count, decimal TeacherShare, decimal PlatformShare);
public sealed record TeacherFinanceAccountSnapshot(Guid TeacherId, string TeacherName, decimal TotalEarned,
    decimal Available, decimal Reserved, decimal Paid, decimal Debt, decimal NetPayable,
    decimal NetBalance, decimal DebtReserved, decimal UnreservedDebt, decimal TodayEarnings,
    decimal CommissionRate, decimal SourceEarnings, decimal SourceDifference, decimal BalanceDifference,
    IReadOnlyList<TeacherIncomeSource> Sources, decimal Retained = 0m, decimal CodeAmountDue = 0m, decimal CodeAmountCollected = 0m);

// Teacher-domain records own entitlement and withdrawal eligibility. The general ledger is their control projection.
public sealed class TeacherFinanceAccountService(IAppDbContext db)
{
    public static readonly TeacherFinancialReviewStatus[] RecognizedStatuses =
        [TeacherFinancialReviewStatus.AutoApproved, TeacherFinancialReviewStatus.Approved, TeacherFinancialReviewStatus.Reversed];

    public static IQueryable<TeacherFinancialAllocation> RecognizedAllocations(IAppDbContext context) =>
        context.TeacherFinancialAllocations.AsNoTracking().Where(a => RecognizedStatuses.Contains(a.ReviewStatus));

    public async Task<TeacherWithdrawalBalance> GetWithdrawalAsync(Guid teacherId, CancellationToken ct)
    {
        var account = await db.TeacherAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.TeacherId == teacherId, ct);
        var debt = await db.TeacherPayoutAdjustments.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && x.Status == TeacherPayoutAdjustmentStatus.Open && x.Amount < 0m)
            .SumAsync(x => (decimal?)-x.Amount, ct) ?? 0m;
        var reservedDebt = await ActiveDebtReservations().Where(x => x.TeacherSettlement.TeacherId == teacherId)
            .SumAsync(x => (decimal?)-x.Amount, ct) ?? 0m;
        return new(account?.CurrentBalance ?? 0m, account?.ReservedBalance ?? 0m, debt, reservedDebt);
    }

    public async Task<TeacherFinanceAccountSnapshot?> GetAsync(Guid teacherId, CancellationToken ct) =>
        (await ReadAsync(teacherId, ct)).SingleOrDefault();

    public Task<IReadOnlyList<TeacherFinanceAccountSnapshot>> GetAllAsync(CancellationToken ct) => ReadAsync(null, ct);

    private IQueryable<TeacherSettlementLine> ActiveDebtReservations() => db.TeacherSettlementLines.AsNoTracking()
        .Where(x => x.AdjustmentId != null && x.Amount < 0m
            && x.TeacherSettlement.Status != TeacherSettlementStatus.Cancelled
            && x.TeacherSettlement.Status != TeacherSettlementStatus.Paid);

    private async Task<IReadOnlyList<TeacherFinanceAccountSnapshot>> ReadAsync(Guid? teacherId, CancellationToken ct)
    {
        await using var transaction = db is DbContext context && context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory" && context.Database.CurrentTransaction is null
            ? await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var teachers = await db.TeacherProfiles.AsNoTracking().Where(x => !teacherId.HasValue || x.Id == teacherId)
            .Select(x => new { x.Id, x.User.FullName, x.CommissionRate }).ToListAsync(ct);
        var ids = teachers.Select(x => x.Id).ToArray();
        var accounts = await db.TeacherAccounts.AsNoTracking().Where(x => ids.Contains(x.TeacherId)).ToDictionaryAsync(x => x.TeacherId, ct);
        var debts = await db.TeacherPayoutAdjustments.AsNoTracking()
            .Where(x => ids.Contains(x.TeacherId) && x.Status == TeacherPayoutAdjustmentStatus.Open && x.Amount < 0m)
            .GroupBy(x => x.TeacherId).Select(g => new { Id = g.Key, Amount = -g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var reservedDebt = await ActiveDebtReservations().Where(x => ids.Contains(x.TeacherSettlement.TeacherId))
            .GroupBy(x => x.TeacherSettlement.TeacherId).Select(g => new { Id = g.Key, Amount = -g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var payouts = await db.TeacherPayouts.AsNoTracking().Where(x => ids.Contains(x.TeacherId) && x.Status == PayoutStatus.Paid)
            .GroupBy(x => x.TeacherId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var settlements = await db.TeacherSettlementPayments.AsNoTracking()
            .Where(x => ids.Contains(x.TeacherSettlement.TeacherId) && x.TeacherSettlement.Status == TeacherSettlementStatus.Paid)
            .GroupBy(x => x.TeacherSettlement.TeacherId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var codeReceivables = await db.CodeGroupDeliveryConfirmations.AsNoTracking()
            .Where(x => x.CodeGroup.TeacherId.HasValue && ids.Contains(x.CodeGroup.TeacherId.Value) && x.PlatformAmountDue != null)
            .Select(x => new { TeacherId = x.CodeGroup.TeacherId!.Value, Due = x.PlatformAmountDue!.Value,
                Collected = x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m }).ToListAsync(ct);
        var (today, tomorrow) = CairoTime.GetCurrentDayRangeUtc();
        var sources = await RecognizedAllocations(db).Where(x => ids.Contains(x.TeacherId))
            .GroupBy(x => new { x.TeacherId, x.TeacherFinancialEvent.SourceType })
            .Select(g => new {
                g.Key.TeacherId, g.Key.SourceType, Count = g.Count(), Teacher = g.Sum(x => x.TeacherShareAmount), Platform = g.Sum(x => x.PlatformShareAmount),
                Retained = g.Sum(x => x.RetainedByTeacher ? x.TeacherShareAmount : 0m),
                PaidReversals = g.Sum(x => x.TeacherShareAmount < 0m && x.PayoutStatus == TeacherFinancialPayoutStatus.Debt ? x.TeacherShareAmount : 0m),
                Today = g.Sum(x => x.TeacherFinancialEvent.OccurredAt >= today && x.TeacherFinancialEvent.OccurredAt < tomorrow ? x.TeacherShareAmount : 0m)
            }).ToListAsync(ct);
        var byTeacher = sources.ToLookup(x => x.TeacherId);
        var snapshots = teachers.Select(teacher => {
            var account = accounts.GetValueOrDefault(teacher.Id);
            var balance = new TeacherWithdrawalBalance(account?.CurrentBalance ?? 0m, account?.ReservedBalance ?? 0m,
                debts.GetValueOrDefault(teacher.Id), reservedDebt.GetValueOrDefault(teacher.Id));
            var income = byTeacher[teacher.Id].ToArray();
            var earned = (account?.TotalEarnings ?? 0m) + income.Sum(x => x.PaidReversals);
            var paid = payouts.GetValueOrDefault(teacher.Id) + settlements.GetValueOrDefault(teacher.Id);
            var sourceEarnings = income.Sum(x => x.Teacher);
            var retained = income.Sum(x => x.Retained);
            var codeAmounts = codeReceivables.Where(x => x.TeacherId == teacher.Id).ToArray();
            return new TeacherFinanceAccountSnapshot(teacher.Id, teacher.FullName, earned, balance.Balance,
                balance.Reserved, paid, balance.Debt, balance.WithdrawalAvailable, balance.NetBalance,
                balance.DebtReserved, balance.UnreservedDebt, income.Sum(x => x.Today), account?.CommissionRate ?? teacher.CommissionRate,
                sourceEarnings, earned - sourceEarnings, balance.NetBalance - (earned - paid - retained),
                income.Select(x => new TeacherIncomeSource(x.SourceType.ToString(), x.Count, x.Teacher, x.Platform)).ToArray(),
                retained, codeAmounts.Sum(x => x.Due - x.Collected), codeAmounts.Sum(x => x.Collected));
        }).ToArray();
        if (transaction is not null) await transaction.CommitAsync(ct);
        return snapshots;
    }
}

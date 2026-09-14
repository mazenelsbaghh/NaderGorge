using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

internal sealed class AdminAIStudentBalanceSnapshotReader(IAppDbContext db)
{
    private const int TeacherScopeLimit = 10;

    public async Task<AdminAIStudentSnapshotSection<AdminAIStudentBalancesSection>> LoadAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var contextTeacherName = await LoadContextTeacherNameAsync(request.BalanceContextTeacherId, ct);
        var generalCash = await LoadGeneralCashAsync(request.StudentId, ct);
        var allocationTotals = await LoadAllocationTotalsAsync(request, ct);
        var teacherScopeCount = await CountTeacherScopesAsync(request, ct);
        var teacherScopes = await LoadTeacherScopesAsync(request, ct);
        var recentTransactions = await LoadRecentTransactionsAsync(request, ct);
        var contextualPurchasingPower = allocationTotals.ContextEligible.HasValue
            ? generalCash + allocationTotals.ContextEligible.Value
            : (decimal?)null;

        var balances = new AdminAIStudentBalancesSection(
            generalCash,
            allocationTotals.General,
            teacherScopes.Take(TeacherScopeLimit).ToArray(),
            teacherScopeCount,
            request.BalanceContextTeacherId,
            contextTeacherName,
            allocationTotals.ContextEligible,
            contextualPurchasingPower,
            recentTransactions.Take(request.RecentLimit).ToArray(),
            "الرصيد النقدي العام والترويجي العام منفصلان عن رصيد كل مدرس؛ أرصدة المدرسين لا تُجمع معًا ولا تثبت اشتراكًا.");
        var isTruncated = teacherScopeCount > TeacherScopeLimit ||
                          recentTransactions.Count > request.RecentLimit;
        return new(balances, isTruncated);
    }

    private async Task<string?> LoadContextTeacherNameAsync(Guid? teacherId, CancellationToken ct)
    {
        if (!teacherId.HasValue)
            return null;

        var teacherName = await db.TeacherProfiles.AsNoTracking()
            .Where(teacher => teacher.Id == teacherId.Value && !teacher.User.IsDeleted)
            .Select(teacher => teacher.User.FullName)
            .SingleOrDefaultAsync(ct);
        if (teacherName is null)
            throw new InvalidOperationException("The context teacher is unavailable.");

        return AdminAIReadArguments.SafeText(teacherName, 120);
    }

    private async Task<decimal> LoadGeneralCashAsync(Guid studentId, CancellationToken ct) =>
        await db.StudentBalances.AsNoTracking()
            .Where(balance => balance.UserId == studentId)
            .Select(balance => (decimal?)balance.CurrentBalance)
            .SingleOrDefaultAsync(ct) ?? 0m;

    private IQueryable<PromotionalBalanceAllocation> BuildAvailableAllocationQuery(
        AdminAIStudentSnapshotRequest request) =>
        db.PromotionalBalanceAllocations.AsNoTracking()
            .Where(allocation =>
                allocation.StudentId == request.StudentId &&
                allocation.AvailableAmount > 0 &&
                (allocation.Status == PromotionalBalanceStatus.Active ||
                 allocation.Status == PromotionalBalanceStatus.PartiallyUsed) &&
                (!allocation.ExpiresAt.HasValue || allocation.ExpiresAt > request.DataAsOf) &&
                (!allocation.MaxPurchaseCount.HasValue ||
                 allocation.PurchaseCount < allocation.MaxPurchaseCount.Value));

    private async Task<AllocationTotals> LoadAllocationTotalsAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var totals = await BuildAvailableAllocationQuery(request)
            .GroupBy(_ => 1)
            .Select(group => new AllocationTotals(
                group.Where(allocation => !allocation.TeacherId.HasValue)
                    .Sum(allocation => allocation.AvailableAmount),
                request.BalanceContextTeacherId.HasValue
                    ? group.Where(allocation =>
                            !allocation.TeacherId.HasValue ||
                            allocation.TeacherId == request.BalanceContextTeacherId)
                        .Sum(allocation => allocation.AvailableAmount)
                    : (decimal?)null))
            .SingleOrDefaultAsync(ct);
        return totals ?? new(0m, request.BalanceContextTeacherId.HasValue ? 0m : null);
    }

    private async Task<int> CountTeacherScopesAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct) =>
        await BuildAvailableAllocationQuery(request)
            .Where(allocation => allocation.TeacherId.HasValue)
            .Select(allocation => allocation.TeacherId!.Value)
            .Distinct()
            .CountAsync(ct);

    private async Task<IReadOnlyList<AdminAIStudentPromotionalBalance>> LoadTeacherScopesAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var scopes = await BuildAvailableAllocationQuery(request)
            .Where(allocation => allocation.TeacherId.HasValue)
            .GroupBy(allocation => new
            {
                TeacherId = allocation.TeacherId!.Value,
                TeacherName = allocation.Teacher!.User.FullName
            })
            .Select(group => new
            {
                group.Key.TeacherId,
                group.Key.TeacherName,
                AvailableEgp = group.Sum(allocation => allocation.AvailableAmount),
                NearestExpiryAt = group.Min(allocation => allocation.ExpiresAt)
            })
            .OrderByDescending(scope => scope.AvailableEgp)
            .ThenBy(scope => scope.TeacherName)
            .Take(TeacherScopeLimit)
            .ToArrayAsync(ct);
        return scopes.Select(scope => new AdminAIStudentPromotionalBalance(
            scope.TeacherId,
            AdminAIReadArguments.SafeText(scope.TeacherName, 120),
            scope.AvailableEgp,
            scope.NearestExpiryAt)).ToArray();
    }

    private async Task<IReadOnlyList<AdminAIStudentBalanceTransaction>> LoadRecentTransactionsAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        if (request.RecentLimit == 0)
            return [];

        var transactions = await db.BalanceTransactions.AsNoTracking()
            .Where(transaction => transaction.StudentBalance.UserId == request.StudentId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Take(request.RecentLimit + 1)
            .Select(transaction => new AdminAIStudentBalanceTransaction(
                transaction.Amount,
                transaction.BalanceAfter,
                transaction.TransactionType,
                transaction.CreatedAt))
            .ToArrayAsync(ct);
        return transactions
            .Select(transaction => transaction with
            {
                TransactionType = AdminAIReadArguments.SafeText(transaction.TransactionType, 80)
            })
            .ToArray();
    }

    private sealed record AllocationTotals(decimal General, decimal? ContextEligible);
}

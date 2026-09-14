using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIPlatformFinanceSummary(
    int Accounts, int JournalEntries, int JournalLines, int TreasuryAccounts, int Expenses, int Refunds,
    int Budgets, int BudgetLines, int Reconciliations, int Periods, int HistoryCheckpoints, int WalletReviews,
    decimal DebitsEgp, decimal CreditsEgp, decimal ExpensesEgp, decimal RefundsEgp,
    decimal BudgetedEgp, decimal ReconciliationVarianceEgp, DateTime DataAsOf);

public sealed class AdminAIPlatformFinanceSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "platform-finance.summary";
    public Type OutputType => typeof(AdminAIPlatformFinanceSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAIPlatformFinanceSummary(
            await db.FinancialAccounts.AsNoTracking().CountAsync(ct),
            await db.JournalEntries.AsNoTracking().CountAsync(ct),
            await db.JournalLines.AsNoTracking().CountAsync(ct),
            await db.TreasuryAccounts.AsNoTracking().CountAsync(ct),
            await db.PlatformExpenses.AsNoTracking().CountAsync(ct),
            await db.PlatformRefunds.AsNoTracking().CountAsync(ct),
            await db.FinanceBudgetPlans.AsNoTracking().CountAsync(ct),
            await db.FinanceBudgetLines.AsNoTracking().CountAsync(ct),
            await db.TreasuryReconciliations.AsNoTracking().CountAsync(ct),
            await db.AccountingPeriods.AsNoTracking().CountAsync(ct),
            await db.FinancialProjectionCheckpoints.AsNoTracking().CountAsync(ct),
            await db.WalletTransferReviews.AsNoTracking().CountAsync(ct),
            await db.JournalLines.AsNoTracking().SumAsync(row => (decimal?)row.Debit, ct) ?? 0m,
            await db.JournalLines.AsNoTracking().SumAsync(row => (decimal?)row.Credit, ct) ?? 0m,
            await db.PlatformExpenses.AsNoTracking().SumAsync(row => (decimal?)row.Amount, ct) ?? 0m,
            await db.PlatformRefunds.AsNoTracking().SumAsync(row => (decimal?)(row.PlatformAmount + row.TeacherAmount), ct) ?? 0m,
            await db.FinanceBudgetLines.AsNoTracking().SumAsync(row => (decimal?)row.PlannedAmount, ct) ?? 0m,
            await db.TreasuryReconciliations.AsNoTracking().SumAsync(row => (decimal?)(row.CountedOrStatementBalance - row.SystemBalance), ct) ?? 0m,
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.finance"]);
    }
}

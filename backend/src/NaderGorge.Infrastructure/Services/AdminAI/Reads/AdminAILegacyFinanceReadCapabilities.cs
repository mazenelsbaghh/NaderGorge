using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAILegacyFinanceSummary(
    int PayrollRecords, int Adjustments, int TeacherAccounts, int Payouts, int Events, int Allocations,
    decimal BasicSalariesEgp, decimal PayrollAdjustmentsEgp, decimal TeacherBalancesEgp,
    decimal PayoutsEgp, decimal GrossSalesEgp, decimal TeacherSharesEgp, decimal PlatformSharesEgp,
    DateTime DataAsOf);

public sealed class AdminAILegacyFinanceSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "legacy-finance.summary";
    public Type OutputType => typeof(AdminAILegacyFinanceSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAILegacyFinanceSummary(
            await db.PayrollRecords.AsNoTracking().CountAsync(ct),
            await db.PayrollAdjustments.AsNoTracking().CountAsync(ct),
            await db.TeacherAccounts.AsNoTracking().CountAsync(ct),
            await db.TeacherPayouts.AsNoTracking().CountAsync(ct),
            await db.TeacherFinancialEvents.AsNoTracking().CountAsync(ct),
            await db.TeacherFinancialAllocations.AsNoTracking().CountAsync(ct),
            await db.PayrollRecords.AsNoTracking().SumAsync(row => (decimal?)row.BasicSalary, ct) ?? 0m,
            await db.PayrollAdjustments.AsNoTracking().SumAsync(row => (decimal?)row.Amount, ct) ?? 0m,
            await db.TeacherAccounts.AsNoTracking().SumAsync(row => (decimal?)row.CurrentBalance, ct) ?? 0m,
            await db.TeacherPayouts.AsNoTracking().SumAsync(row => (decimal?)row.Amount, ct) ?? 0m,
            await db.TeacherFinancialEvents.AsNoTracking().Where(row => row.Currency == "EGP").SumAsync(row => (decimal?)row.GrossAmount, ct) ?? 0m,
            await db.TeacherFinancialAllocations.AsNoTracking().SumAsync(row => (decimal?)row.TeacherShareAmount, ct) ?? 0m,
            await db.TeacherFinancialAllocations.AsNoTracking().SumAsync(row => (decimal?)row.PlatformShareAmount, ct) ?? 0m,
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.finance.legacy"]);
    }
}

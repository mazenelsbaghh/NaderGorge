using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAITeacherFinanceSummary(
    int Agreements, int Allocations, int Settlements, int SettlementLines, int Payments, int Invoices,
    int FinancialTerms, int DeliveryConfirmations, decimal GrossDueEgp, decimal DebtDeductionsEgp,
    decimal NetPayableEgp, decimal PaymentsEgp, decimal InvoicesEgp, DateTime DataAsOf);

public sealed class AdminAITeacherFinanceSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "teacher-finance.summary";
    public Type OutputType => typeof(AdminAITeacherFinanceSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAITeacherFinanceSummary(
            await db.TeacherFinancialAgreements.AsNoTracking().CountAsync(ct),
            await db.TeacherFinancialAllocations.AsNoTracking().CountAsync(ct),
            await db.TeacherSettlements.AsNoTracking().CountAsync(ct),
            await db.TeacherSettlementLines.AsNoTracking().CountAsync(ct),
            await db.TeacherSettlementPayments.AsNoTracking().CountAsync(ct),
            await db.FinancialInvoices.AsNoTracking().CountAsync(ct),
            await db.CodeGroupFinancialTerms.AsNoTracking().CountAsync(ct),
            await db.CodeGroupDeliveryConfirmations.AsNoTracking().CountAsync(ct),
            await db.TeacherSettlements.AsNoTracking().Where(row => row.Currency == "EGP").SumAsync(row => (decimal?)row.GrossDueAmount, ct) ?? 0m,
            await db.TeacherSettlements.AsNoTracking().Where(row => row.Currency == "EGP").SumAsync(row => (decimal?)row.DebtDeductionAmount, ct) ?? 0m,
            await db.TeacherSettlements.AsNoTracking().Where(row => row.Currency == "EGP").SumAsync(row => (decimal?)row.NetPayableAmount, ct) ?? 0m,
            await db.TeacherSettlementPayments.AsNoTracking().Where(row => row.TeacherSettlement.Currency == "EGP").SumAsync(row => (decimal?)row.Amount, ct) ?? 0m,
            await db.FinancialInvoices.AsNoTracking().Where(row => row.Currency == "EGP").SumAsync(row => (decimal?)row.Amount, ct) ?? 0m,
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.finance.teachers"]);
    }
}

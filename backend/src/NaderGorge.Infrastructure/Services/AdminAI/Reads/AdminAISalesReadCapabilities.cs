using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAISalesSummary(int Gifts, int Promotions, int Rules, int Coupons, int CouponUsages, int Templates, int PublicExamProducts, int FinancialEffects, DateTime DataAsOf);

public sealed class AdminAISalesSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "sales.summary";
    public Type OutputType => typeof(AdminAISalesSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAISalesSummary(
            await db.GiftIssuances.AsNoTracking().CountAsync(ct),
            await db.PromotionalBalanceAllocations.AsNoTracking().CountAsync(ct),
            await db.SalesRules.AsNoTracking().CountAsync(ct),
            await db.SalesCoupons.AsNoTracking().CountAsync(ct),
            await db.SalesCouponUsages.AsNoTracking().CountAsync(ct),
            await db.PrintableCodeTemplates.AsNoTracking().CountAsync(ct),
            await db.PublicExamProducts.AsNoTracking().CountAsync(ct),
            await db.SalesFinancialEffects.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.sales"]);
    }
}

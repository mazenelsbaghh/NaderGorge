using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed record CodeGroupFinanceQuote(decimal UnitPrice, int Units, decimal Gross, decimal Net,
    decimal TeacherShare, decimal PlatformShare, decimal Basis, TeacherAllocationMode AllocationMode,
    TeacherAgreementResolution Agreement, SalesTargetType TargetType, Guid TargetId, string ContentName, string Key)
{
    public static async Task<CodeGroupFinanceQuote> CalculateAsync(IAppDbContext db, CodeGroup group,
        CodeGroupFinancialTerms terms, DateTime at, CancellationToken ct)
    {
        if (!group.TeacherId.HasValue || group.CodeType == CodeType.Balance)
            throw new InvalidOperationException("اختر دفعة محتوى مرتبطة بالمدرّس");
        var policy = new CodeGroupFinancePolicy(db);
        var (price, targetType, targetId, name) = await policy.ResolvePricingAsync(group, ct);
        var units = CodeGroupFinancePolicy.Trigger(group, terms) == TeacherAgreementTrigger.CodeDelivery ? group.TotalCodes : 1;
        var gross = decimal.Round(Math.Max(0m, price) * units, 2, MidpointRounding.AwayFromZero);
        var net = decimal.Round(gross * (1m - Math.Clamp(group.DiscountPercentage ?? 0m, 0m, 100m) / 100m), 2, MidpointRounding.AwayFromZero);
        var agreement = await policy.ResolveAgreementAsync(group, terms, targetType, targetId, terms.Trigger, at, ct);
        var (mode, share, basis) = TeacherAgreementResolver.CalculateAllocation(agreement, gross, net, units);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            { group.Id, group.TeacherId, terms.Trigger, units, gross, net, share, agreement }))));
        return new(price, units, gross, net, share, net - share, basis, mode, agreement, targetType, targetId, name, key);
    }
}

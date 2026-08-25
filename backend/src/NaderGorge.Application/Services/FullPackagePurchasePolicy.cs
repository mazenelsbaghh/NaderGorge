using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

/// <summary>
/// Controls only direct acquisition of a complete TermWithSections package.
/// Existing grants and narrower content/code/gift acquisition paths are intentionally unaffected.
/// </summary>
public static class FullPackagePurchasePolicy
{
    public const string ErrorCode = "FULL_PACKAGE_PURCHASE_DISABLED";
    public const string ErrorMessage = "شراء الباقة كاملة متوقف حالياً. يمكنك شراء الترم أو القسم أو الحصة بشكل منفصل.";

    public static bool IsDisabled(Package package) =>
        package.ContentMode == PackageContentMode.TermWithSections
        && !package.AllowFullPackagePurchase;

    public static async Task<bool> ContainsDisabledPackageAsync(
        IAppDbContext db,
        IReadOnlyCollection<Guid> packageIds,
        CancellationToken ct = default)
    {
        if (packageIds.Count == 0)
            return false;

        return await db.Packages.AnyAsync(package =>
            packageIds.Contains(package.Id)
            && package.ContentMode == PackageContentMode.TermWithSections
            && !package.AllowFullPackagePurchase,
            ct);
    }
}

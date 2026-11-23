using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Gifts.Models;

internal static class GiftPackageAvailability
{
    public static IQueryable<Guid> EligibleIds(IAppDbContext db) => db.Packages
        // Compensation paths explicitly disable package sales; gifting them must
        // not require making them visible in the student purchase catalog.
        .Where(package => package.IsActive || package.ShowWhenUnavailable || !package.AllowFullPackagePurchase)
        .Select(package => package.Id);
}

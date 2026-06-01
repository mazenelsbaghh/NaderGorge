using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetPackageCodeProfileQuery(Guid PackageId) : IRequest<ApiResponse<AdminPackageCodePageProfileDto>>;

public record AdminPackageCodePageProfileDto(
    Guid PackageId,
    string PackageName,
    PackageCodePageProfileStatus Status,
    bool IsUsingFallback,
    string HeroEyebrow,
    string HeroTitle,
    string HeroDescription,
    string OfferTitle,
    string OfferDescription,
    string ActivationTitle,
    string ActivationDescription,
    string SupportTitle,
    string SupportDescription,
    string ThemeAccentKey,
    DateTime? PublishedAt,
    DateTime? LastUpdatedAt
);

public class GetPackageCodeProfileQueryHandler : IRequestHandler<GetPackageCodeProfileQuery, ApiResponse<AdminPackageCodePageProfileDto>>
{
    private readonly IAppDbContext _db;

    public GetPackageCodeProfileQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<AdminPackageCodePageProfileDto>> Handle(GetPackageCodeProfileQuery request, CancellationToken ct)
    {
        var package = await _db.Packages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, ct);

        if (package == null)
        {
            return ApiResponse<AdminPackageCodePageProfileDto>.Fail("Package not found");
        }

        var profile = await _db.PackageCodePageProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PackageId == request.PackageId, ct);

        var defaults = PackageCodePageProfileDefaults.Build(package.Name, package.Description, package.Price);
        var dto = new AdminPackageCodePageProfileDto(
            PackageId: package.Id,
            PackageName: package.Name,
            Status: profile?.Status ?? PackageCodePageProfileStatus.Fallback,
            IsUsingFallback: profile == null || profile.Status == PackageCodePageProfileStatus.Fallback,
            HeroEyebrow: profile?.HeroEyebrow ?? defaults.HeroEyebrow,
            HeroTitle: profile?.HeroTitle ?? defaults.HeroTitle,
            HeroDescription: profile?.HeroDescription ?? defaults.HeroDescription,
            OfferTitle: profile?.OfferTitle ?? defaults.OfferTitle,
            OfferDescription: profile?.OfferDescription ?? defaults.OfferDescription,
            ActivationTitle: profile?.ActivationTitle ?? defaults.ActivationTitle,
            ActivationDescription: profile?.ActivationDescription ?? defaults.ActivationDescription,
            SupportTitle: profile?.SupportTitle ?? defaults.SupportTitle,
            SupportDescription: profile?.SupportDescription ?? defaults.SupportDescription,
            ThemeAccentKey: profile?.ThemeAccentKey ?? defaults.ThemeAccentKey,
            PublishedAt: profile?.PublishedAt,
            LastUpdatedAt: profile?.UpdatedAt ?? profile?.CreatedAt
        );

        return ApiResponse<AdminPackageCodePageProfileDto>.Ok(dto);
    }
}
